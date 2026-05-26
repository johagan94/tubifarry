using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using System.Text.Json;
using Tubifarry.Download.Clients.SquidQobuz;
using Tubifarry.Indexers.CatalogSearch;

namespace Tubifarry.Indexers.SquidQobuz
{
    public interface ISquidQobuzRequestGenerator : IIndexerRequestGenerator
    {
        void SetSetting(SquidQobuzIndexerSettings settings);
    }

    public class SquidQobuzRequestGenerator : ISquidQobuzRequestGenerator
    {
        private readonly Logger _logger;
        private SquidQobuzIndexerSettings? _settings;

        public SquidQobuzRequestGenerator(Logger logger) => _logger = logger;

        public IndexerPageableRequestChain GetRecentRequests() => new();

        public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
        {
            CatalogSearchRequestContext context = new(
                searchCriteria.ArtistQuery,
                GetAliases(searchCriteria.Artist?.Metadata.Value.Aliases, null),
                [searchCriteria.AlbumQuery],
                false);

            return Generate(context, searchCriteria.AlbumQuery);
        }

        public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
        {
            CatalogSearchRequestContext context = new(
                searchCriteria.ArtistQuery,
                GetAliases(searchCriteria.Artist?.Metadata.Value.Aliases, searchCriteria.CleanArtistQuery),
                GetExpectedAlbums(searchCriteria.Albums),
                true);

            return Generate(context, null);
        }

        public void SetSetting(SquidQobuzIndexerSettings settings) => _settings = settings;

        private static string StripBrackets(string title)
        {
            while (true)
            {
                int start = title.LastIndexOf('[');
                if (start < 0) break;
                string candidate = title[..start].TrimEnd();
                if (candidate.Length == 0) break;
                title = candidate;
            }
            return title;
        }

        private IndexerPageableRequestChain Generate(CatalogSearchRequestContext context, string? album)
        {
            IndexerPageableRequestChain chain = new();
            if (string.IsNullOrWhiteSpace(context.SearchArtist) && string.IsNullOrWhiteSpace(album))
            {
                _logger.Warn("Empty query, skipping search request");
                return chain;
            }

            string baseUrl = _settings!.BaseUrl.TrimEnd('/');
            bool first = true;
            foreach (string rawQuery in CatalogSearchNormalizer.BuildQueryVariants(context.SearchArtist, album))
            {
                string query = StripBrackets(rawQuery);
                string url = $"{baseUrl}/get-music?q={Uri.EscapeDataString(query)}&limit=30";

                HttpRequest req = new(url)
                {
                    RequestTimeout = TimeSpan.FromSeconds(_settings.RequestTimeout),
                    ContentSummary = JsonSerializer.Serialize(context)
                };
                SquidQobuzApi.AddHeaders(req, _settings.TokenCountry);

                if (first)
                {
                    chain.Add([new IndexerRequest(req)]);
                    first = false;
                }
                else
                {
                    chain.AddTier([new IndexerRequest(req)]);
                }
            }
            return chain;
        }

        private static IReadOnlyList<string> GetExpectedAlbums(IEnumerable<NzbDrone.Core.Music.Album> albums) =>
            albums
                .Select(album => album.Title)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static IReadOnlyList<string> GetAliases(IEnumerable<string>? aliases, string? cleanArtistQuery)
        {
            List<string> result = aliases?.Where(alias => !string.IsNullOrWhiteSpace(alias)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
            if (!string.IsNullOrWhiteSpace(cleanArtistQuery) && !result.Contains(cleanArtistQuery, StringComparer.OrdinalIgnoreCase))
                result.Add(cleanArtistQuery);
            return result;
        }
    }
}
