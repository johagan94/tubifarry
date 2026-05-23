using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using NLog;

namespace Tubifarry.Download.Clients.Tidal
{
    public static class TidalAuthHelper
    {
        public const string ClientId = "txNoH4kkV41MfH25";
        public const string ClientSecret = "dQjy0MinCEvxi1O4UmxvxWnDjt4cgHBPw8ll6nYBk98=";
        public const string AuthUrl = "https://auth.tidal.com/v1/oauth2/token";

        private static readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new();
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static async Task<string> GetAccessTokenAsync(Logger logger, CancellationToken token = default)
        {
            string cacheKey = "tidal_token";

            if (_tokenCache.TryGetValue(cacheKey, out CachedToken? cached) && cached.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
            {
                logger.Trace("Using cached TIDAL access token");
                return cached.Token;
            }

            await _lock.WaitAsync(token);
            try
            {
                if (_tokenCache.TryGetValue(cacheKey, out cached) && cached.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
                    return cached.Token;

                logger.Debug("Requesting new TIDAL access token");

                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", Tubifarry.UserAgent);

                Dictionary<string, string> body = new()
                {
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["grant_type"] = "client_credentials"
                };

                using FormUrlEncodedContent content = new(body);
                using HttpResponseMessage response = await client.PostAsync(AuthUrl, content, token);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync(token);
                TidalAuthResponse? auth = System.Text.Json.JsonSerializer.Deserialize<TidalAuthResponse>(responseBody);

                if (auth == null || string.IsNullOrEmpty(auth.AccessToken))
                    throw new Exception("Failed to parse TIDAL auth response");

                _tokenCache[cacheKey] = new CachedToken(auth.AccessToken, DateTime.UtcNow.AddSeconds(auth.ExpiresIn));
                logger.Debug($"Obtained new TIDAL access token (expires in {auth.ExpiresIn}s)");

                return auth.AccessToken;
            }
            finally
            {
                _lock.Release();
            }
        }

        public static void InvalidateToken()
        {
            _tokenCache.TryRemove("tidal_token", out _);
        }

        private record CachedToken(string Token, DateTime ExpiresAt);

        private record TidalAuthResponse(
            [property: JsonPropertyName("access_token")] string AccessToken,
            [property: JsonPropertyName("token_type")] string TokenType,
            [property: JsonPropertyName("expires_in")] int ExpiresIn);
    }
}
