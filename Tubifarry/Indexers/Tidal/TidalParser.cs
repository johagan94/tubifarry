using NLog;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using System.Text.Json;
using Tubifarry.Core.Model;
using Tubifarry.Core.Utilities;
using Tubifarry.Indexers.CatalogSearch;

namespace Tubifarry.Indexers.Tidal
{
    public interface ITidalParser : IParseIndexerResponse
    { }

    public class TidalParser : ITidalParser
    {
        private readonly Logger _logger;

        public TidalParser(Logger logger) => _logger = logger;

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            List<ReleaseInfo> releases = [];
            CatalogSearchRequestContext requestContext = CatalogSearchRequestContext.FromJson(indexerResponse.Request.HttpRequest.ContentSummary);
            try
            {
                TidalSearchResponse? searchResponse = JsonSerializer.Deserialize<TidalSearchResponse>(
                    indexerResponse.Content,
                    IndexerParserHelper.StandardJsonOptions);

                if (searchResponse == null)
                    return releases;

                if (searchResponse.Data?.Albums?.Items != null)
                {
                    foreach (TidalMonochromeAlbum album in searchResponse.Data.Albums.Items)
                    {
                        ReleaseInfo release = CreateMonochromeAlbumData(album);
                        if (ShouldInclude(release, requestContext))
                            releases.Add(release);
                    }

                    return releases;
                }

                Dictionary<string, TidalIncludedItem>? included = searchResponse.Included?
                    .GroupBy(GetIncludedKey)
                    .ToDictionary(g => g.Key, g => g.First());

                if (searchResponse.Data?.Relationships?.Albums?.Data != null)
                {
                    foreach (TidalIdItem albumRef in searchResponse.Data.Relationships.Albums.Data)
                    {
                        if (included != null && included.TryGetValue(GetRelationshipKey(albumRef), out TidalIncludedItem? album))
                        {
                            ReleaseInfo release = CreateAlbumData(album, included);
                            if (ShouldInclude(release, requestContext))
                                releases.Add(release);
                        }
                    }
                }

                if (searchResponse.Data?.Relationships?.Tracks?.Data != null)
                {
                    foreach (TidalIdItem trackRef in searchResponse.Data.Relationships.Tracks.Data)
                    {
                        if (included != null && included.TryGetValue(GetRelationshipKey(trackRef), out TidalIncludedItem? track))
                        {
                            ReleaseInfo? release = CreateTrackData(track, included);
                            if (release != null && ShouldInclude(release, requestContext))
                                releases.Add(release);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error parsing TIDAL search response");
            }
            return releases;
        }

        private static ReleaseInfo CreateMonochromeAlbumData(TidalMonochromeAlbum item)
        {
            string artistName = item.Artists?.FirstOrDefault()?.Name ?? item.Artist?.Name ?? "Unknown Artist";
            (AudioFormat format, int bitrate, int bitDepth) = GetQuality(item.AudioQuality);
            int trackCount = item.NumberOfTracks ?? Math.Max(item.Items?.Count ?? 0, 1);
            long duration = (long)(item.Duration ?? 0);
            long estimatedSize = IndexerParserHelper.EstimateSize(0, duration, bitrate, trackCount);

            AlbumData data = new("TIDAL", nameof(TidalDownloadProtocol))
            {
                AlbumId = $"tidal://album/{item.Id}",
                AlbumName = item.Title ?? "Unknown Album",
                ArtistName = artistName,
                InfoUrl = $"https://tidal.com/album/{item.Id}",
                TotalTracks = trackCount,
                ReleaseDate = NormalizeReleaseDate(item.ReleaseDate),
                ReleaseDatePrecision = "day",
                CustomString = item.Cover ?? "",
                ExplicitContent = item.Explicit ?? false,
                Codec = format,
                Bitrate = bitrate,
                BitDepth = bitDepth,
                Duration = duration,
                Size = estimatedSize
            };
            data.ParseReleaseDate();
            return data.ToReleaseInfo();
        }

        private static ReleaseInfo CreateAlbumData(TidalIncludedItem item, Dictionary<string, TidalIncludedItem>? included)
        {
            string artistName = "Unknown Artist";
            string? coverUrl = item.Attributes?.ImageUrl ?? item.Attributes?.Cover;

            if (item.Relationships?.Artists?.Data?.Count > 0 && included != null)
            {
                TidalIdItem artistRef = item.Relationships.Artists.Data[0];
                if (included.TryGetValue(GetRelationshipKey(artistRef), out TidalIncludedItem? artistItem))
                    artistName = artistItem.Attributes?.Name ?? artistName;
            }

            (AudioFormat format, int bitrate, int bitDepth) = GetQuality(item.Attributes?.AudioQuality);
            int trackCount = item.Attributes?.NumberOfItems ?? item.Attributes?.NumberOfTracks ?? 1;
            long estimatedSize = EstimateAlbumSize(trackCount, bitrate);

            AlbumData data = new("TIDAL", nameof(TidalDownloadProtocol))
            {
                AlbumId = $"tidal://album/{item.Id}",
                AlbumName = item.Attributes?.Title ?? "Unknown Album",
                ArtistName = artistName,
                InfoUrl = $"https://tidal.com/album/{item.Id}",
                TotalTracks = trackCount,
                ReleaseDate = item.Attributes?.ReleaseDate ?? DateTime.Now.Year.ToString(),
                ReleaseDatePrecision = "day",
                CustomString = coverUrl ?? "",
                Codec = format,
                Bitrate = bitrate,
                BitDepth = bitDepth,
                Size = estimatedSize
            };
            return data.ToReleaseInfo();
        }

        private static ReleaseInfo? CreateTrackData(TidalIncludedItem item, Dictionary<string, TidalIncludedItem>? included)
        {
            string artistName = "Unknown Artist";
            string? albumTitle = null;
            string? albumId = null;
            string? coverUrl = item.Attributes?.ImageUrl ?? item.Attributes?.Cover;

            if (item.Relationships?.Artists?.Data?.Count > 0 && included != null)
            {
                TidalIdItem artistRef = item.Relationships.Artists.Data[0];
                if (included.TryGetValue(GetRelationshipKey(artistRef), out TidalIncludedItem? artistItem))
                    artistName = artistItem.Attributes?.Name ?? artistName;
            }

            if (item.Relationships?.Albums?.Data?.Count > 0 && included != null)
            {
                TidalIdItem albumRef = item.Relationships.Albums.Data[0];
                albumId = albumRef.Id;
                if (included.TryGetValue(GetRelationshipKey(albumRef), out TidalIncludedItem? albumItem))
                {
                    albumTitle = albumItem.Attributes?.Title ?? albumTitle;
                    coverUrl = albumItem.Attributes?.ImageUrl ?? albumItem.Attributes?.Cover ?? coverUrl;
                }
            }

            if (string.IsNullOrWhiteSpace(albumTitle) || string.IsNullOrWhiteSpace(albumId))
                return null;

            (AudioFormat format, int bitrate, int bitDepth) = GetQuality(item.Attributes?.AudioQuality);
            int duration = (int)(item.Attributes?.Duration ?? 0);
            long estimatedSize = IndexerParserHelper.EstimateSize(0, duration, bitrate);

            AlbumData data = new("TIDAL", nameof(TidalDownloadProtocol))
            {
                AlbumId = $"tidal://track/{item.Id}",
                AlbumName = albumTitle,
                ArtistName = artistName,
                InfoUrl = $"https://tidal.com/track/{item.Id}",
                TotalTracks = 1,
                ReleaseDate = item.Attributes?.ReleaseDate ?? DateTime.Now.Year.ToString(),
                ReleaseDatePrecision = "day",
                Duration = duration,
                CustomString = coverUrl ?? "",
                Codec = format,
                Bitrate = bitrate,
                BitDepth = bitDepth,
                Size = estimatedSize
            };
            return data.ToReleaseInfo();
        }

        private static bool ShouldInclude(ReleaseInfo release, CatalogSearchRequestContext requestContext)
        {
            if (!string.IsNullOrWhiteSpace(requestContext.SearchArtist) &&
                !CatalogSearchNormalizer.IsArtistMatch(release.Artist, requestContext.SearchArtist, requestContext.ArtistAliases))
            {
                return false;
            }

            if (requestContext.ExpectedAlbums.Count > 0 &&
                !CatalogSearchNormalizer.IsAlbumMatch(release.Album, requestContext.ExpectedAlbums))
            {
                return false;
            }

            return true;
        }

        private static string GetIncludedKey(TidalIncludedItem item) => $"{item.Type}:{item.Id}";

        private static string GetRelationshipKey(TidalIdItem item) => $"{item.Type}:{item.Id}";

        private static (AudioFormat Format, int Bitrate, int BitDepth) GetQuality(string? audioQuality)
        {
            return audioQuality?.ToUpperInvariant() switch
            {
                "HI_RES_LOSSLESS" or "HI_RES" => (AudioFormat.FLAC, 3000, 24),
                "LOSSLESS" => (AudioFormat.FLAC, 1000, 16),
                "HIGH" => (AudioFormat.AAC, 320, 16),
                "LOW" => (AudioFormat.AAC, 96, 16),
                _ => (AudioFormat.AAC, 320, 16)
            };
        }

        private static string NormalizeReleaseDate(string? releaseDate)
        {
            if (DateTime.TryParse(releaseDate, out DateTime parsed))
                return parsed.ToString("yyyy-MM-dd");

            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        private static long EstimateAlbumSize(int trackCount, int bitrate)
        {
            const int averageTrackDurationSeconds = 240;

            return IndexerParserHelper.EstimateSize(
                0,
                averageTrackDurationSeconds * Math.Max(trackCount, 1),
                bitrate);
        }
    }
}
