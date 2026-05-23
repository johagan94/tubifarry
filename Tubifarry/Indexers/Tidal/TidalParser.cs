using NLog;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using System.Text.Json;
using Tubifarry.Core.Model;
using Tubifarry.Core.Utilities;

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
            try
            {
                TidalSearchResponse? searchResponse = JsonSerializer.Deserialize<TidalSearchResponse>(
                    indexerResponse.Content,
                    IndexerParserHelper.StandardJsonOptions);

                if (searchResponse == null)
                    return releases;

                Dictionary<string, TidalIncludedItem>? included = searchResponse.Included?
                    .GroupBy(i => i.Id)
                    .ToDictionary(g => g.Key, g => g.First());

                if (searchResponse.Data != null)
                {
                    foreach (TidalSearchItem item in searchResponse.Data)
                    {
                        switch (item.Type)
                        {
                            case "albums":
                                releases.Add(CreateAlbumData(item, included));
                                break;
                            case "tracks":
                                releases.Add(CreateTrackData(item, included));
                                break;
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

        private static ReleaseInfo CreateAlbumData(TidalSearchItem item, Dictionary<string, TidalIncludedItem>? included)
        {
            string artistName = "Unknown Artist";
            string? coverUrl = item.Attributes?.Url;

            if (item.Relationships?.Artists?.Data?.Count > 0 && included != null)
            {
                string artistId = item.Relationships.Artists.Data[0].Id;
                if (included.TryGetValue(artistId, out TidalIncludedItem? artistItem))
                    artistName = artistItem.Attributes?.Name ?? artistName;
            }

            (AudioFormat format, int bitrate, int bitDepth) = GetQuality(item.Attributes?.AudioQuality);
            int trackCount = item.Attributes?.NumberOfTracks ?? 1;
            long estimatedSize = IndexerParserHelper.EstimateSize(0, 0, bitrate, trackCount);

            AlbumData data = new("TIDAL", nameof(TidalDownloadProtocol))
            {
                AlbumId = $"https://tidal.com/album/{item.Id}",
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

        private static ReleaseInfo CreateTrackData(TidalSearchItem item, Dictionary<string, TidalIncludedItem>? included)
        {
            string artistName = "Unknown Artist";
            string albumTitle = "Unknown Album";
            string? albumId = null;
            string? coverUrl = item.Attributes?.Url;

            if (item.Relationships?.Artists?.Data?.Count > 0 && included != null)
            {
                string artistId = item.Relationships.Artists.Data[0].Id;
                if (included.TryGetValue(artistId, out TidalIncludedItem? artistItem))
                    artistName = artistItem.Attributes?.Name ?? artistName;
            }

            if (item.Relationships?.Albums?.Data?.Count > 0 && included != null)
            {
                string aid = item.Relationships.Albums.Data[0].Id;
                albumId = aid;
                if (included.TryGetValue(aid, out TidalIncludedItem? albumItem))
                {
                    albumTitle = albumItem.Attributes?.Title ?? albumTitle;
                    coverUrl = albumItem.Attributes?.ImageUrl ?? albumItem.Attributes?.Cover ?? coverUrl;
                }
            }

            (AudioFormat format, int bitrate, int bitDepth) = GetQuality(item.Attributes?.AudioQuality);
            int duration = item.Attributes?.Duration ?? 0;
            long estimatedSize = IndexerParserHelper.EstimateSize(0, duration, bitrate);

            string downloadUrl = albumId != null
                ? $"tidal://album/{albumId}"
                : $"tidal://track/{item.Id}";

            AlbumData data = new("TIDAL", nameof(TidalDownloadProtocol))
            {
                AlbumId = downloadUrl,
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
    }
}
