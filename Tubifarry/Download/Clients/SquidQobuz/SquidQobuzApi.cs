using NzbDrone.Common.Http;

namespace Tubifarry.Download.Clients.SquidQobuz
{
    public static class SquidQobuzApi
    {
        public const string TokenCountryHeader = "Token-Country";
        private const string UpstreamQobuzError = "Upstream Qobuz error";

        public static string NormalizeTokenCountry(string? country) =>
            string.IsNullOrWhiteSpace(country) ? "AU" : country.Trim().ToUpperInvariant();

        public static void AddHeaders(HttpRequest request, string? tokenCountry)
        {
            request.Headers["User-Agent"] = Tubifarry.UserAgent;
            request.Headers[TokenCountryHeader] = NormalizeTokenCountry(tokenCountry);
        }

        public static string BuildFailureMessage(string serviceName, int statusCode, string? body)
        {
            if (body?.Contains(UpstreamQobuzError, StringComparison.OrdinalIgnoreCase) == true)
            {
                return $"{serviceName} is reachable, but its upstream Qobuz request is currently blocked (HTTP {statusCode}). Try another Token Country or wait for SquidWTF/Qobuz to recover.";
            }

            return $"Cannot connect to {serviceName}: HTTP {statusCode}";
        }
    }
}
