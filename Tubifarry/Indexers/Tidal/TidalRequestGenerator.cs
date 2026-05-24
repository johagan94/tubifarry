using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using System.Collections.Concurrent;
using System.Text.Json;
using Tubifarry.Download.Clients.Tidal;
using Tubifarry.Indexers.CatalogSearch;

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
            if (_tokenExpiry != DateTime.MinValue && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5) && !string.IsNullOrEmpty(_token))
                return;

            _tokenLock.Wait();
            try
            {
                if (_tokenExpiry != DateTime.MinValue && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5) && !string.IsNullOrEmpty(_token))
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

        private IndexerPageableRequestChain Generate(CatalogSearchRequestContext context, string? album)
        {
            IndexerPageableRequestChain chain = new();
            if (string.IsNullOrWhiteSpace(context.SearchArtist) && string.IsNullOrWhiteSpace(album))
            {
                _logger.Warn("Empty query, skipping search request");
                return chain;
            }

            EnforceRateLimit();
            EnsureToken();

            string baseUrl = _settings!.BaseUrl.TrimEnd('/');
            string countryCode = _settings.CountryCode;
            int limit = _settings.SearchLimit;

            bool first = true;
            foreach (string query in CatalogSearchNormalizer.BuildQueryVariants(context.SearchArtist, album))
            {
                string url = $"{baseUrl}/searchResults/{Uri.EscapeDataString(query)}?countryCode={countryCode}&include=albums,albums.artists&limit={limit}";
                _logger.Trace("Creating TIDAL search request: {Url}", url);

                HttpRequest req = new(url)
                {
                    RequestTimeout = TimeSpan.FromSeconds(_settings.RequestTimeout),
                    ContentSummary = JsonSerializer.Serialize(context),
                    SuppressHttpError = false,
                    LogHttpError = true
                };
                req.Headers["User-Agent"] = Tubifarry.UserAgent;
                req.Headers["Accept"] = "application/vnd.api+json, application/json;q=0.9, */*;q=0.8";

                if (!string.IsNullOrEmpty(_token))
                    req.Headers["Authorization"] = $"Bearer {_token}";

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
