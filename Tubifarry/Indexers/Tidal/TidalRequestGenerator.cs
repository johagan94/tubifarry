using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace Tubifarry.Indexers.Tidal
{
    public interface ITidalRequestGenerator : IIndexerRequestGenerator
    {
        public void SetSetting(TidalIndexerSettings settings);
    }

    public class TidalRequestGenerator(Logger logger) : ITidalRequestGenerator
    {
        private readonly Logger _logger = logger;
        private TidalIndexerSettings? _settings;

        public IndexerPageableRequestChain GetRecentRequests() => new();

        public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
        {
            string query = string.Join(' ', new[] { searchCriteria.AlbumQuery, searchCriteria.ArtistQuery }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return Generate(query);
        }

        public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria) => Generate(searchCriteria.ArtistQuery);

        public void SetSetting(TidalIndexerSettings settings) => _settings = settings;

        private IndexerPageableRequestChain Generate(string query)
        {
            IndexerPageableRequestChain chain = new();
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.Warn("Empty query, skipping search request");
                return chain;
            }

            string baseUrl = _settings!.BaseUrl.TrimEnd('/');
            string countryCode = _settings.CountryCode;
            int limit = _settings.SearchLimit;

            string url = $"{baseUrl}/searchResults/{Uri.EscapeDataString(query)}?countryCode={countryCode}&include=albums,tracks&limit={limit}";
            _logger.Trace("Creating TIDAL search request: {Url}", url);

            HttpRequest req = new(url)
            {
                RequestTimeout = TimeSpan.FromSeconds(_settings.RequestTimeout),
                ContentSummary = new TidalRequestData(baseUrl, "search", limit).ToJson(),
                SuppressHttpError = false,
                LogHttpError = true
            };
            req.Headers["User-Agent"] = Tubifarry.UserAgent;
            req.Headers["Accept"] = "application/vnd.api+json, application/json;q=0.9, */*;q=0.8";

            chain.Add([new IndexerRequest(req)]);
            return chain;
        }
    }
}
