using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;

namespace Tubifarry.Download.Clients.SquidQobuz
{
    public class SquidQobuzClient : DownloadClientBase<SquidQobuzProviderSettings>
    {
        private readonly IHttpClient _httpClient;
        private readonly INamingConfigService _namingService;
        private readonly SquidQobuzCaptchaSolver _captchaSolver;
        private readonly SquidQobuzCompletedItemRecovery _completedItemRecovery;
        private readonly ConcurrentDictionary<string, DownloadClientItem> _items = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public override string Name => "SquidQobuz";
        public override string Protocol => nameof(SquidQobuzDownloadProtocol);
        public new SquidQobuzProviderSettings Settings => base.Settings;

        public SquidQobuzClient(
            IHttpClient httpClient,
            IConfigService configService,
            INamingConfigService namingConfigService,
            IDiskProvider diskProvider,
            ILocalizationService localizationService,
            SquidQobuzCaptchaSolver captchaSolver,
            SquidQobuzCompletedItemRecovery completedItemRecovery,
            Logger logger)
            : base(configService, diskProvider, null, localizationService, logger)
        {
            _httpClient = httpClient;
            _namingService = namingConfigService;
            _captchaSolver = captchaSolver;
            _completedItemRecovery = completedItemRecovery;
        }

        public override async Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
        {
            string baseUrl = Settings.BaseUrl.TrimEnd('/');
            string artist = remoteAlbum.Artist?.Name ?? "Unknown";
            string albumTitle = StripBrackets(remoteAlbum.Release?.Album ?? remoteAlbum.Albums?.FirstOrDefault()?.Title ?? remoteAlbum.Release?.Title ?? "Unknown");

            int quality = Settings.Quality switch
            {
                0 => 27,
                1 => 6,
                2 => 5,
                _ => 27
            };

            try
            {
                // 1. Search for album
                string query = $"{artist} {albumTitle}";
                _logger.Trace($"SquidQobuz search: {query}");

                string searchJson = await GetJsonAsync($"{baseUrl}/get-music?q={Uri.EscapeDataString(query)}&limit=10");
                SquidAlbumResponse? search = JsonSerializer.Deserialize<SquidAlbumResponse>(searchJson, JsonOptions);
                List<SquidAlbumItem>? albums = search?.Data?.Albums?.Items;

                if (albums == null || albums.Count == 0)
                {
                    _logger.Warn($"SquidQobuz: No results for '{query}'");

                    // Try just album title
                    query = albumTitle;
                    searchJson = await GetJsonAsync($"{baseUrl}/get-music?q={Uri.EscapeDataString(query)}&limit=10");
                    search = JsonSerializer.Deserialize<SquidAlbumResponse>(searchJson, JsonOptions);
                    albums = search?.Data?.Albums?.Items;
                }

                if (albums == null || albums.Count == 0)
                    throw new Exception($"No results found on squid.wtf Qobuz for: {query}");

                SquidAlbumItem album = albums[0];
                string albumId = album.Id ?? throw new Exception("Missing album ID");
                _logger.Debug($"Found: {album.Title} by {album.Artist?.Name} ({albumId})");

                // 2. Get album details (tracks)
                string detailJson = await GetJsonAsync($"{baseUrl}/get-album?album_id={albumId}");
                SquidAlbumDetailResponse? detail = JsonSerializer.Deserialize<SquidAlbumDetailResponse>(detailJson, JsonOptions);
                List<SquidTrackItem>? tracks = detail?.Data?.Tracks?.Items;

                if (tracks == null || tracks.Count == 0)
                    throw new Exception("No tracks found");

                string albumArtist = detail?.Data?.Artist?.Name ?? album.Artist?.Name ?? artist;
                string albumName = detail?.Data?.Title ?? album.Title ?? albumTitle;
                string safeDir = Sanitize($"{albumArtist} - {albumName}");
                string destDir = Path.Combine(Settings.DownloadPath, safeDir);
                Directory.CreateDirectory(destDir);
                string downloadId = destDir;
                long totalSize = 0;

                _logger.Debug($"Downloading {tracks.Count} tracks to: {destDir}");

                // 3. Download tracks
                foreach (SquidTrackItem track in tracks)
                {
                    string trackId = track.Id?.ToString() ?? throw new Exception("Missing track ID");
                    string? captchaCookie = _captchaSolver.GetCaptchaCookie(baseUrl);
                    string dlJson = await GetJsonWithCaptchaAsync($"{baseUrl}/download-music?track_id={trackId}&quality={quality}", captchaCookie);
                    SquidDownloadResponse? dl = JsonSerializer.Deserialize<SquidDownloadResponse>(dlJson, JsonOptions);
                    string? fileUrl = dl?.Data?.Url;

                    if (string.IsNullOrEmpty(fileUrl))
                    {
                        _logger.Warn($"No download URL for track: {track.Title}");
                        continue;
                    }

                    string trackNum = (track.TrackNumber ?? tracks.IndexOf(track) + 1).ToString().PadLeft(2, '0');
                    string ext = GuessExtension(fileUrl);
                    string filename = Sanitize($"{trackNum} - {track.Title}.{ext}");
                    string filePath = Path.Combine(destDir, filename);

                    _logger.Trace($"Downloading: {filename}");
                    HttpResponse fileResponse = await _httpClient.GetAsync(new HttpRequest(fileUrl));
                    if (fileResponse.StatusCode != System.Net.HttpStatusCode.OK || fileResponse.ResponseData == null)
                        throw new Exception($"Track download failed for '{track.Title}': HTTP {(int)fileResponse.StatusCode}");

                    await File.WriteAllBytesAsync(filePath, fileResponse.ResponseData);
                    totalSize += fileResponse.ResponseData.Length;
                }

                _items[downloadId] = new DownloadClientItem
                {
                    DownloadId = downloadId,
                    Title = $"{albumArtist} - {albumName}",
                    TotalSize = totalSize,
                    RemainingSize = 0,
                    Status = DownloadItemStatus.Completed,
                    OutputPath = new OsPath(destDir),
                    RemainingTime = TimeSpan.Zero,
                    CanBeRemoved = true,
                    CanMoveFiles = true,
                    DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false)
                };

                return downloadId;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"SquidQobuz download failed: {artist} - {albumTitle}");
                throw;
            }
        }

        private async Task<string> GetJsonAsync(string url)
        {
            HttpRequest req = new(url) { RequestTimeout = TimeSpan.FromSeconds(Settings.RequestTimeout) };
            SquidQobuzApi.AddHeaders(req, Settings.TokenCountry);
            HttpResponse response;
            try
            {
                response = await _httpClient.GetAsync(req);
            }
            catch (HttpException ex) when (ex.Response != null)
            {
                _logger.Warn(ex, "SquidQobuz request failed: HTTP {0}", (int)ex.Response.StatusCode);
                throw new Exception(SquidQobuzApi.BuildFailureMessage("SquidQobuz", (int)ex.Response.StatusCode, ex.Response.Content), ex);
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.Warn($"SquidQobuz request failed: HTTP {(int)response.StatusCode}");
                throw new Exception(SquidQobuzApi.BuildFailureMessage("SquidQobuz", (int)response.StatusCode, response.Content));
            }
            return response.Content ?? "";
        }

        private async Task<string> GetJsonWithCaptchaAsync(string url, string? captchaCookie)
        {
            HttpRequest req = new(url) { RequestTimeout = TimeSpan.FromSeconds(Settings.RequestTimeout) };
            SquidQobuzApi.AddHeaders(req, Settings.TokenCountry);

            if (captchaCookie != null)
                req.Headers["Cookie"] = captchaCookie;

            HttpResponse response;
            try
            {
                response = await _httpClient.GetAsync(req);
            }
            catch (HttpException ex) when (ex.Response != null && ex.Response.StatusCode == System.Net.HttpStatusCode.Forbidden && ex.Response.Content?.Contains("Captcha required") == true)
            {
                response = ex.Response;
            }
            catch (HttpException ex) when (ex.Response != null)
            {
                _logger.Warn(ex, "SquidQobuz request failed: HTTP {0}", (int)ex.Response.StatusCode);
                throw new Exception(SquidQobuzApi.BuildFailureMessage("SquidQobuz", (int)ex.Response.StatusCode, ex.Response.Content), ex);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden && response.Content?.Contains("Captcha required") == true)
            {
                _logger.Debug("SquidQobuz: captcha required, solving...");
                string? cookie = _captchaSolver.GetCaptchaCookie(Settings.BaseUrl, forceRefresh: true);
                if (cookie == null)
                    throw new Exception("SquidQobuz captcha could not be solved");

                HttpRequest retryReq = new(url) { RequestTimeout = TimeSpan.FromSeconds(Settings.RequestTimeout) };
                SquidQobuzApi.AddHeaders(retryReq, Settings.TokenCountry);
                retryReq.Headers["Cookie"] = cookie;
                try
                {
                    response = await _httpClient.GetAsync(retryReq);
                }
                catch (HttpException ex) when (ex.Response != null)
                {
                    _logger.Warn(ex, "SquidQobuz retry request failed: HTTP {0}", (int)ex.Response.StatusCode);
                    throw new Exception(SquidQobuzApi.BuildFailureMessage("SquidQobuz", (int)ex.Response.StatusCode, ex.Response.Content), ex);
                }
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                string body = response.Content ?? "";
                if (body.Length > 200) body = body[..200];
                _logger.Warn($"SquidQobuz request failed: HTTP {(int)response.StatusCode} {body}");
                throw new Exception(SquidQobuzApi.BuildFailureMessage("SquidQobuz", (int)response.StatusCode, response.Content));
            }
            return response.Content ?? "";
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            foreach (DownloadClientItem item in _items.Values)
                yield return item;

            DownloadClientItemClientInfo clientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false);
            foreach (DownloadClientItem item in _completedItemRecovery.GetCompletedItems(Settings.DownloadPath, clientInfo))
            {
                if (!_items.ContainsKey(item.DownloadId))
                    yield return item;
            }
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            if (deleteData && Directory.Exists(item.OutputPath.FullPath))
            {
                try { Directory.Delete(item.OutputPath.FullPath, true); } catch { }
            }
            else if (deleteData && File.Exists(item.OutputPath.FullPath))
            {
                try { File.Delete(item.OutputPath.FullPath); } catch { }
            }
            _items.TryRemove(item.DownloadId, out _);
        }

        public override DownloadClientInfo GetStatus() => new()
        {
            IsLocalhost = false,
            OutputRootFolders = [new OsPath(Settings.DownloadPath)]
        };

        protected override void Test(List<ValidationFailure> failures)
        {
            if (!_diskProvider.FolderExists(Settings.DownloadPath))
            {
                failures.Add(new ValidationFailure("DownloadPath", "Download path does not exist"));
                return;
            }

            if (!_diskProvider.FolderWritable(Settings.DownloadPath))
            {
                failures.Add(new ValidationFailure("DownloadPath", "Download path is not writable"));
                return;
            }

            try
            {
                string testUrl = $"{Settings.BaseUrl.TrimEnd('/')}/get-music?q=test&limit=1";
                HttpRequest req = new(testUrl) { RequestTimeout = TimeSpan.FromSeconds(Settings.RequestTimeout) };
                SquidQobuzApi.AddHeaders(req, Settings.TokenCountry);
                HttpResponse response = _httpClient.Get(req);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    failures.Add(new ValidationFailure("BaseUrl", SquidQobuzApi.BuildFailureMessage("squid.wtf Qobuz", (int)response.StatusCode, response.Content)));
                else
                    _logger.Debug("Successfully connected to squid.wtf Qobuz API");
            }
            catch (HttpException ex) when (ex.Response != null)
            {
                _logger.Warn(ex, "squid.wtf Qobuz returned HTTP {0}", (int)ex.Response.StatusCode);
                failures.Add(new ValidationFailure("BaseUrl", SquidQobuzApi.BuildFailureMessage("squid.wtf Qobuz", (int)ex.Response.StatusCode, ex.Response.Content)));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error connecting to squid.wtf Qobuz API");
                failures.Add(new ValidationFailure("BaseUrl", ex.Message));
            }
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

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

        private static string GuessExtension(string url)
        {
            if (url.Contains(".flac", StringComparison.OrdinalIgnoreCase)) return "flac";
            if (url.Contains(".mp3", StringComparison.OrdinalIgnoreCase)) return "mp3";
            if (url.Contains(".m4a", StringComparison.OrdinalIgnoreCase)) return "m4a";
            return "flac";
        }

        // JSON models
        private class SquidAlbumResponse { [JsonPropertyName("data")] public SquidData? Data { get; set; } }
        private class SquidData
        {
            [JsonPropertyName("albums")] public SquidAlbumContainer? Albums { get; set; }
            [JsonPropertyName("tracks")] public SquidTrackContainer? Tracks { get; set; }
            [JsonPropertyName("artist")] public SquidArtistInfo? Artist { get; set; }
            [JsonPropertyName("title")] public string? Title { get; set; }
        }
        private class SquidAlbumContainer { [JsonPropertyName("items")] public List<SquidAlbumItem>? Items { get; set; } }
        private class SquidAlbumItem
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("title")] public string? Title { get; set; }
            [JsonPropertyName("artist")] public SquidArtistInfo? Artist { get; set; }
        }
        private class SquidArtistInfo { [JsonPropertyName("name")] public string? Name { get; set; } }
        private class SquidAlbumDetailResponse { [JsonPropertyName("data")] public SquidData? Data { get; set; } }
        private class SquidTrackContainer { [JsonPropertyName("items")] public List<SquidTrackItem>? Items { get; set; } }
        private class SquidTrackItem
        {
            [JsonPropertyName("id")] public int? Id { get; set; }
            [JsonPropertyName("title")] public string? Title { get; set; }
            [JsonPropertyName("track_number")] public int? TrackNumber { get; set; }
        }
        private class SquidDownloadResponse { [JsonPropertyName("data")] public SquidDownloadData? Data { get; set; } }
        private class SquidDownloadData { [JsonPropertyName("url")] public string? Url { get; set; } }
    }
}
