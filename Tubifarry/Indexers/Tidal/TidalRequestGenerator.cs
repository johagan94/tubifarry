using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using System.Collections.Concurrent;
using Tubifarry.Download.Clients.Tidal;

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
        private string _token = string.Empty;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private static readonly SemaphoreSlim _tokenLock = new(1, 1);
        private static readonly SemaphoreSlim _rateLimitLock = new(1, 1);
        private const int MinRequestIntervalMs = 250;
        private const int TokenRefreshRetries = 1;

        public IndexerPageableRequestChain GetRecentRequests() => new();

        public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
        {
            string query = string.Join(' ', new[] { searchCriteria.AlbumQuery, searchCriteria.ArtistQuery }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return Generate(query);
        }

        public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria) => Generate(searchCriteria.ArtistQuery);

        public void SetSetting(TidalIndexerSettings settings) => _settings = settings;

        private void EnforceRateLimit()
        {
            _rateLimitLock.Wait();
            try
            {
                TimeSpan sinceLast = DateTime.UtcNow - _lastRequestTime;
                int remainingMs = MinRequestIntervalMs - (int)sinceLast.TotalMilliseconds;
                if (remainingMs > 0)
                {
                    _logger.Trace($"TIDAL rate limit: waiting {remainingMs}ms");
                    Thread.Sleep(remainingMs);
                }
                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                _rateLimitLock.Release();
            }
        }

        private void EnsureToken()
        {
            if (DateTime.UtcNow < _tokenExpiry.AddMinutes(-5) && !string.IsNullOrEmpty(_token))
                return;

            _tokenLock.Wait();
            try
            {
                if (DateTime.UtcNow < _tokenExpiry.AddMinutes(-5) && !string.IsNullOrEmpty(_token))
                    return;

                for (int attempt = 0; attempt <= TokenRefreshRetries; attempt++)
                {
                    try
                    {
                        _token = TidalAuthHelper.GetAccessTokenAsync(_logger).GetAwaiter().GetResult();
                        _tokenExpiry = DateTime.UtcNow.AddHours(3);
                        _logger.Debug("Obtained new TIDAL access token");
                        return;
                    }
                    catch (Exception ex) when (attempt < TokenRefreshRetries)
                    {
                        _logger.Warn(ex, $"TIDAL token request failed (attempt {attempt + 1}), retrying...");
                        Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to obtain TIDAL access token after retries");
                        _token = string.Empty;
                    }
                }
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public void InvalidateToken()
        {
            _tokenLock.Wait();
            try
            {
                _token = string.Empty;
                _tokenExpiry = DateTime.MinValue;
                _logger.Debug("TIDAL token invalidated, will refresh on next request");
                TidalAuthHelper.InvalidateToken();
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private IndexerPageableRequestChain Generate(string query)
        {
            IndexerPageableRequestChain chain = new();
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.Warn("Empty query, skipping search request");
                return chain;
            }

            EnforceRateLimit();
            EnsureToken();

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

            if (!string.IsNullOrEmpty(_token))
                req.Headers["Authorization"] = $"Bearer {_token}";

            chain.Add([new IndexerRequest(req)]);
            return chain;
        }
    }
}
