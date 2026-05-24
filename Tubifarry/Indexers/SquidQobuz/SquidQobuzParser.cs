using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using Tubifarry.Core.Model;
using Tubifarry.Core.Utilities;
using Tubifarry.Indexers.CatalogSearch;

namespace Tubifarry.Indexers.SquidQobuz
{
    public interface ISquidQobuzParser : IParseIndexerResponse
    { }

    public class SquidQobuzParser : ISquidQobuzParser
    {
        private readonly Logger _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public SquidQobuzParser(Logger logger) => _logger = logger;

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            List<ReleaseInfo> releases = [];
            CatalogSearchRequestContext requestContext = CatalogSearchRequestContext.FromJson(indexerResponse.Request.HttpRequest.ContentSummary);

            try
            {
                SquidSearchResponse? search = JsonSerializer.Deserialize<SquidSearchResponse>(indexerResponse.Content, JsonOptions);
                List<SquidAlbumItem>? albums = search?.Data?.Albums?.Items;

                if (albums == null || albums.Count == 0)
                {
                    _logger.Debug("SquidQobuz: No album results in response");
                    return releases;
                }

                foreach (SquidAlbumItem album in albums)
                {
                    if (string.IsNullOrEmpty(album.Id) || string.IsNullOrEmpty(album.Title))
                        continue;

                    string artistName = album.Artist?.Name ?? "Unknown";
                    if (!ShouldInclude(artistName, album.Title, requestContext))
                        continue;

                    AlbumData albumData = new("SquidQobuz", nameof(SquidQobuzDownloadProtocol))
                    {
                        AlbumId = album.Id,
                        AlbumName = album.Title,
                        ArtistName = artistName,
                        InfoUrl = $"https://qobuz.squid.wtf/album/{album.Id}",
                        Codec = AudioFormat.FLAC,
                        BitDepth = 16,
                        Bitrate = 1411,
                        TotalTracks = 10
                    };

                    releases.Add(albumData.ToReleaseInfo());
                }

                _logger.Debug("SquidQobuz: Parsed {Count} album results", releases.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error parsing SquidQobuz search response");
            }

            return releases;
        }

        private static bool ShouldInclude(string artistName, string albumTitle, CatalogSearchRequestContext requestContext)
        {
            if (!string.IsNullOrWhiteSpace(requestContext.SearchArtist) &&
                !CatalogSearchNormalizer.IsArtistMatch(artistName, requestContext.SearchArtist, requestContext.ArtistAliases))
            {
                return false;
            }

            if (requestContext.ExpectedAlbums.Count > 0 &&
                !CatalogSearchNormalizer.IsAlbumMatch(albumTitle, requestContext.ExpectedAlbums))
            {
                return false;
            }

            return true;
        }

        private class SquidSearchResponse
        {
            [JsonPropertyName("data")]
            public SquidSearchData? Data { get; set; }
        }

        private class SquidSearchData
        {
            [JsonPropertyName("albums")]
            public SquidAlbumContainer? Albums { get; set; }
        }

        private class SquidAlbumContainer
        {
            [JsonPropertyName("items")]
            public List<SquidAlbumItem>? Items { get; set; }
        }

        private class SquidAlbumItem
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("artist")]
            public SquidArtistInfo? Artist { get; set; }
        }

        private class SquidArtistInfo
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}
