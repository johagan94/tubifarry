using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NLog;
using NzbDrone.Common.Http;

namespace Tubifarry.Download.Clients.SquidQobuz
{
    public class SquidQobuzCaptchaSolver
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        private static readonly TimeSpan CookieValidity = TimeSpan.FromMinutes(28);
        private const int MaxSolverIterations = 1_000_000;

        private string? _cookieHeader;
        private DateTime _cookieExpiresAt = DateTime.MinValue;
        private readonly object _lock = new();

        public SquidQobuzCaptchaSolver(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public string? GetCaptchaCookie(string baseUrl, bool forceRefresh = false)
        {
            if (!forceRefresh && _cookieHeader != null && DateTime.UtcNow < _cookieExpiresAt)
                return _cookieHeader;

            lock (_lock)
            {
                if (!forceRefresh && _cookieHeader != null && DateTime.UtcNow < _cookieExpiresAt)
                    return _cookieHeader;

                _cookieHeader = SolveAndVerify(baseUrl);
                _cookieExpiresAt = DateTime.UtcNow + CookieValidity;
                return _cookieHeader;
            }
        }

        private string? SolveAndVerify(string baseUrl)
        {
            try
            {
                string trimmed = baseUrl.TrimEnd('/');

                HttpRequest challengeReq = new($"{trimmed}/altcha/challenge");
                HttpResponse challengeResp = _httpClient.Get(challengeReq);

                if (challengeResp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.Warn("SquidQobuz captcha: failed to get challenge, HTTP {0}", (int)challengeResp.StatusCode);
                    return null;
                }

                using var challengeDoc = JsonDocument.Parse(challengeResp.Content);
                var parameters = challengeDoc.RootElement.GetProperty("parameters");

                var (counter, derivedKeyHex, elapsedMs) = SolveChallenge(parameters);

                var solutionJson = JsonSerializer.Serialize(new { counter, derivedKey = derivedKeyHex, time = elapsedMs });
                var payloadJson = $"{{\"challenge\":{challengeResp.Content.TrimEnd()},\"solution\":{solutionJson}}}";
                var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));
                var verifyBody = JsonSerializer.Serialize(new { payload = payloadB64 });

                HttpRequest verifyReq = new($"{trimmed}/altcha/verify")
                {
                    Method = HttpMethod.Post
                };
                verifyReq.Headers.Add("Content-Type", "application/json");
                verifyReq.SetContent(verifyBody);
                HttpResponse verifyResp = _httpClient.Execute(verifyReq);

                if (verifyResp.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.Warn("SquidQobuz captcha: verify failed, HTTP {0}", (int)verifyResp.StatusCode);
                    return null;
                }

                string? setCookie = verifyResp.Headers["Set-Cookie"];
                string? captchaCookie = setCookie?
                    .Split(',')
                    .Select(c => c.Split(';')[0].Trim())
                    .FirstOrDefault(c => c.StartsWith("captcha_verified_at=", StringComparison.Ordinal));

                if (captchaCookie == null)
                {
                    _logger.Warn("SquidQobuz captcha: no captcha_verified_at cookie in verify response");
                    return null;
                }

                _logger.Debug("SquidQobuz captcha solved in {0}ms (counter={1})", elapsedMs, counter);
                return captchaCookie;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SquidQobuz captcha solver failed");
                return null;
            }
        }

        public static (int Counter, string DerivedKeyHex, long ElapsedMs) SolveChallenge(JsonElement parameters)
        {
            var nonce = Convert.FromHexString(parameters.GetProperty("nonce").GetString()!);
            var salt = Convert.FromHexString(parameters.GetProperty("salt").GetString()!);
            var cost = parameters.GetProperty("cost").GetInt32();
            var keyLength = parameters.GetProperty("keyLength").GetInt32();
            var keyPrefix = Convert.FromHexString(parameters.GetProperty("keyPrefix").GetString()!);

            var password = new byte[nonce.Length + 4];
            Array.Copy(nonce, password, nonce.Length);

            var initial = new byte[salt.Length + password.Length];
            Array.Copy(salt, 0, initial, 0, salt.Length);

            var derived = new byte[keyLength];
            Span<byte> hashBuf = stackalloc byte[32];

            var sw = Stopwatch.StartNew();
            for (var counter = 0; counter < MaxSolverIterations; counter++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(password.AsSpan(nonce.Length), (uint)counter);
                Array.Copy(password, 0, initial, salt.Length, password.Length);

                SHA256.HashData(initial, hashBuf);
                hashBuf[..keyLength].CopyTo(derived);

                for (var i = 1; i < cost; i++)
                {
                    SHA256.HashData(derived, hashBuf);
                    hashBuf[..keyLength].CopyTo(derived);
                }

                if (derived.AsSpan(0, keyPrefix.Length).SequenceEqual(keyPrefix))
                {
                    return (counter, Convert.ToHexString(derived).ToLowerInvariant(), sw.ElapsedMilliseconds);
                }
            }

            throw new InvalidOperationException(
                $"Captcha solver exhausted {MaxSolverIterations} iterations without finding a match");
        }
    }
}
