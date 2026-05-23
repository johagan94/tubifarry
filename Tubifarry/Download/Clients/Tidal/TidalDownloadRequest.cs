using DownloadAssistant.Options;
using DownloadAssistant.Requests;
using NLog;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;
using Requests;
using Requests.Options;
using System.Text.Json;
using System.Xml.Linq;
using Tubifarry.Core.Model;
using Tubifarry.Download.Base;
using Tubifarry.Indexers.Tidal;

namespace Tubifarry.Download.Clients.Tidal
{
    public class TidalDownloadRequest : BaseDownloadRequest<TidalDownloadOptions>
    {
        private readonly string _baseUrl = "https://openapi.tidal.com/v2";
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public TidalDownloadRequest(RemoteAlbum remoteAlbum, TidalDownloadOptions? options) : base(remoteAlbum, options)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Tubifarry.UserAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json, application/json;q=0.9, */*;q=0.8");

            _requestContainer.Add(new OwnRequest(async (token) =>
            {
                try
                {
                    await ProcessDownloadAsync(token);
                    return true;
                }
                catch (Exception ex)
                {
                    LogAndAppendMessage($"Error processing download: {ex.Message}", LogLevel.Error);
                    throw;
                }
            }, new RequestOptions<VoidStruct, VoidStruct>()
            {
                CancellationToken = Token,
                DelayBetweenAttemps = Options.DelayBetweenAttemps,
                NumberOfAttempts = Options.NumberOfAttempts,
                Priority = RequestPriority.Low,
                Handler = Options.Handler
            }));
        }

        protected override async Task ProcessDownloadAsync(CancellationToken token)
        {
            _logger.Trace($"Processing {(Options.IsTrack ? "track" : "album")}: {ReleaseInfo.Title}");

            string accessToken = await TidalAuthHelper.GetAccessTokenAsync(_logger, token);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            if (Options.IsTrack)
                await ProcessSingleTrackAsync(Options.ItemId, token);
            else
                await ProcessAlbumAsync(Options.ItemId, token);
        }

        private async Task ProcessSingleTrackAsync(string trackId, CancellationToken token)
        {
            _logger.Trace($"Processing track ID: {trackId}");

            string manifestUrl = await GetTrackManifestUrlAsync(trackId, token);
            if (string.IsNullOrEmpty(manifestUrl))
                throw new Exception("Failed to get track manifest URL");

            string tempFile = Path.Combine(_destinationPath.FullPath, $"tidal_temp_{Guid.NewGuid():N}.mp4");
            try
            {
                await DownloadDashSegmentsAsync(manifestUrl, tempFile, token);
                string outputFile = Path.Combine(_destinationPath.FullPath,
                    BuildTrackFilename(
                        new Track { Title = ReleaseInfo.Title, TrackNumber = "1", AbsoluteTrackNumber = 1, Artist = new LazyLoaded<Artist>(new Artist { Name = ReleaseInfo.Artist ?? "Unknown Artist" }) },
                        new Album { Title = ReleaseInfo.Album ?? ReleaseInfo.Title }));
                await ConvertAudioAsync(tempFile, outputFile, token);

                InitiateDownload(outputFile, outputFile, token);
                _requestContainer.Add(_trackContainer);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        private async Task ProcessAlbumAsync(string albumId, CancellationToken token)
        {
            _logger.Trace($"Processing album ID: {albumId}");

            TidalAlbumResponse? albumResponse = await GetAlbumTracksAsync(albumId, token);
            if (albumResponse?.Included == null || albumResponse.Included.Count == 0)
                throw new Exception("No tracks found in album");

            List<TidalIncludedItem> tracks = albumResponse.Included.Where(i => i.Type == "tracks").ToList();
            _expectedTrackCount = tracks.Count;
            _logger.Trace($"Found {tracks.Count} tracks in album");

            string albumTitle = albumResponse.Data?.Attributes?.Title ?? ReleaseInfo.Album ?? "Unknown Album";
            string? albumCoverUrl = albumResponse.Data?.Attributes?.ImageUrl ?? albumResponse.Data?.Attributes?.Cover;

            if (!string.IsNullOrEmpty(albumCoverUrl))
                _ = DownloadAlbumCoverAsync(albumCoverUrl, token);

            for (int i = 0; i < tracks.Count; i++)
            {
                TidalIncludedItem trackItem = tracks[i];
                try
                {
                    string manifestUrl = await GetTrackManifestUrlAsync(trackItem.Id, token);
                    if (string.IsNullOrEmpty(manifestUrl))
                    {
                        _logger.Warn($"No manifest URL for track: {trackItem.Attributes?.Title}");
                        continue;
                    }

                    string tempFile = Path.Combine(_destinationPath.FullPath, $"tidal_temp_{Guid.NewGuid():N}.mp4");
                    await DownloadDashSegmentsAsync(manifestUrl, tempFile, token);

                    Track trackMeta = new()
                    {
                        Title = trackItem.Attributes?.Title ?? $"Track {i + 1}",
                        TrackNumber = (trackItem.Attributes?.TrackNumber ?? i + 1).ToString(),
                        AbsoluteTrackNumber = trackItem.Attributes?.TrackNumber ?? i + 1,
                        Duration = trackItem.Attributes?.Duration ?? 0,
                        Artist = new LazyLoaded<Artist>(new Artist { Name = ReleaseInfo.Artist ?? "Unknown Artist" })
                    };

                    Album albumMeta = new()
                    {
                        Title = albumTitle,
                        ReleaseDate = ParseDate(albumResponse.Data?.Attributes?.ReleaseDate),
                        Artist = new LazyLoaded<Artist>(new Artist { Name = ReleaseInfo.Artist ?? "Unknown Artist" })
                    };

                    string fileName = BuildTrackFilename(trackMeta, albumMeta);
                    string outputFile = Path.Combine(_destinationPath.FullPath, fileName);
                    await ConvertAudioAsync(tempFile, outputFile, token);

                    InitiateDownload(outputFile, outputFile, token);
                    _logger.Trace($"Track {i + 1}/{tracks.Count} processed: {trackItem.Attributes?.Title}");
                }
                catch (Exception ex)
                {
                    LogAndAppendMessage($"Track {i + 1}/{tracks.Count} failed: {trackItem.Attributes?.Title} - {ex.Message}", LogLevel.Error);
                }
            }
            _requestContainer.Add(_trackContainer);
        }

        private async Task<string> GetTrackManifestUrlAsync(string trackId, CancellationToken token)
        {
            string qualityFormats = Options.DownloadQuality switch
            {
                0 => "FLAC_HIRES&formats=FLAC",           // HI_RES_LOSSLESS
                1 => "formats=FLAC",                        // LOSSLESS
                2 => "formats=AACLC",                       // HIGH
                _ => "formats=HEAACV1"                      // LOW
            };

            string url = $"{_baseUrl}/trackManifests/{trackId}?adaptive=true&manifestType=MPEG_DASH&uriScheme=HTTPS&usage=PLAYBACK&{qualityFormats}";

            _logger.Trace($"Requesting manifest: {url}");
            string response = await _httpClient.GetStringAsync(url, token);

            TidalManifestResponse? manifestResponse = JsonSerializer.Deserialize<TidalManifestResponse>(response, JsonOptions);
            string? manifestUri = manifestResponse?.Data?.Attributes?.Uri;

            if (string.IsNullOrEmpty(manifestUri))
            {
                _logger.Warn($"No manifest URI found for track {trackId}. Response: {response[..Math.Min(200, response.Length)]}");
                throw new Exception($"No manifest URI in response for track {trackId}");
            }

            _logger.Trace($"Got manifest URI: {manifestUri}");
            return manifestUri;
        }

        private async Task<TidalAlbumResponse?> GetAlbumTracksAsync(string albumId, CancellationToken token)
        {
            string url = $"{_baseUrl}/albums/{albumId}?countryCode={Options.CountryCode}&include=tracks";
            string response = await _httpClient.GetStringAsync(url, token);
            return JsonSerializer.Deserialize<TidalAlbumResponse>(response, JsonOptions);
        }

        private async Task DownloadDashSegmentsAsync(string manifestUrl, string outputFile, CancellationToken token)
        {
            _logger.Trace($"Fetching DASH manifest: {manifestUrl}");

            using HttpClient cdnClient = new();
            cdnClient.DefaultRequestHeaders.Add("User-Agent", Tubifarry.UserAgent);

            string manifestXml = await cdnClient.GetStringAsync(manifestUrl, token);
            XDocument mpd = XDocument.Parse(manifestXml);
            XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";

            string? baseUrl = null;
            XElement? baseUrlElement = mpd.Descendants(ns + "BaseURL").FirstOrDefault();
            if (baseUrlElement != null)
                baseUrl = baseUrlElement.Value;
            else
            {
                Uri manifestUri = new(manifestUrl);
                baseUrl = manifestUri.GetLeftPart(UriPartial.Authority) + "/" + string.Join("/", manifestUri.Segments.Take(manifestUri.Segments.Length - 1));
            }

            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            XElement? adaptationSet = mpd.Descendants(ns + "AdaptationSet")
                .FirstOrDefault(a => a.Attribute("mimeType")?.Value?.Contains("audio") == true);
            if (adaptationSet == null)
                throw new Exception("No audio AdaptationSet found in DASH manifest");

            XElement? segmentTemplate = adaptationSet.Element(ns + "SegmentTemplate");
            if (segmentTemplate == null)
            {
                XElement? representation = adaptationSet.Element(ns + "Representation");
                segmentTemplate = representation?.Element(ns + "SegmentTemplate");
            }

            if (segmentTemplate == null)
                throw new Exception("No SegmentTemplate found in DASH manifest");

            string? initSegment = segmentTemplate.Attribute("initialization")?.Value;
            string? mediaPattern = segmentTemplate.Attribute("media")?.Value;
            int startNumber = int.Parse(segmentTemplate.Attribute("startNumber")?.Value ?? "1");

            XElement? segmentTimeline = segmentTemplate.Element(ns + "SegmentTimeline");
            if (segmentTimeline == null)
                throw new Exception("No SegmentTimeline found in DASH manifest");

            List<int> segmentNumbers = [];
            int segNum = startNumber;
            foreach (XElement s in segmentTimeline.Elements(ns + "S"))
            {
                int repeat = int.Parse(s.Attribute("r")?.Value ?? "0");
                for (int r = 0; r <= repeat; r++)
                {
                    segmentNumbers.Add(segNum);
                    segNum++;
                }
            }

            _logger.Trace($"DASH manifest: {segmentNumbers.Count} segments, init={initSegment}, baseUrl={baseUrl}");

            using FileStream output = new(outputFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.SequentialScan);

            if (!string.IsNullOrEmpty(initSegment))
            {
                string initUrl = baseUrl + initSegment.Replace("$RepresentationID$", "1");
                byte[] initData = await cdnClient.GetByteArrayAsync(initUrl, token);
                await output.WriteAsync(initData, token);
                _logger.Trace($"Downloaded init segment: {initData.Length} bytes");
            }

            foreach (int num in segmentNumbers)
            {
                token.ThrowIfCancellationRequested();
                string segUrl = mediaPattern!
                    .Replace("$RepresentationID$", "1")
                    .Replace("$Number$", num.ToString())
                    .Replace("$Time$", "");

                string fullUrl = segUrl.StartsWith("http") ? segUrl : baseUrl + segUrl;
                byte[] segData = await cdnClient.GetByteArrayAsync(fullUrl, token);
                await output.WriteAsync(segData, token);

                if (num % 10 == 0)
                    _logger.Trace($"Downloaded segment {num}/{segmentNumbers.Count}");
            }

            _logger.Trace($"Downloaded all {segmentNumbers.Count} segments to: {outputFile}");
        }

        private async Task ConvertAudioAsync(string inputFile, string outputFile, CancellationToken token)
        {
            string outputExt = Options.OutputFormat switch
            {
                0 => ".flac",
                1 => ".mp3",
                2 => ".m4a",
                _ => ".flac"
            };

            if (!outputFile.EndsWith(outputExt, StringComparison.OrdinalIgnoreCase))
                outputFile = Path.ChangeExtension(outputFile, outputExt);

            string? ffmpegPath = FindFfmpeg();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                _logger.Warn("FFmpeg not found, keeping raw M4S concatenation");
                if (inputFile != outputFile)
                    File.Copy(inputFile, outputFile, true);
                return;
            }

            string args = Options.OutputFormat switch
            {
                0 => $"-i \"{inputFile}\" -map_metadata -1 -c:a flac -compression_level 8 \"{outputFile}\" -y",
                1 => BuildMp3Args(inputFile, outputFile),
                2 => $"-i \"{inputFile}\" -map_metadata -1 -c:a copy \"{outputFile}\" -y",
                _ => $"-i \"{inputFile}\" -map_metadata -1 -c:a flac -compression_level 8 \"{outputFile}\" -y"
            };

            _logger.Trace($"Running FFmpeg: {ffmpegPath} {args}");

            using System.Diagnostics.Process process = new()
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                _logger.Warn($"FFmpeg exited with code {process.ExitCode}: {error[..Math.Min(200, error.Length)]}");
                if (!File.Exists(outputFile) && inputFile != outputFile)
                    File.Copy(inputFile, outputFile, true);
            }
            else
            {
                _logger.Trace($"FFmpeg conversion complete: {outputFile}");
                try { File.Delete(inputFile); } catch { }
            }
        }

        private string BuildMp3Args(string inputFile, string outputFile)
        {
            string bitrate = Options.Mp3Bitrate switch
            {
                0 => "320k",
                1 => "256k",
                2 => "192k",
                3 => "128k",
                _ => "320k"
            };
            return $"-i \"{inputFile}\" -map_metadata -1 -c:a libmp3lame -b:a {bitrate} -ar 44100 \"{outputFile}\" -y";
        }

        private static string? FindFfmpeg()
        {
            string[] names = OperatingSystem.IsWindows()
                ? ["ffmpeg.exe"]
                : ["ffmpeg", "ffmpeg.exe"];

            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            string[] paths = pathEnv?.Split(Path.PathSeparator) ?? [];

            foreach (string dir in paths)
            {
                foreach (string name in names)
                {
                    string fullPath = Path.Combine(dir, name);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }

            string[] commonPaths = OperatingSystem.IsWindows()
                ? ["C:\\ffmpeg\\bin\\ffmpeg.exe", "C:\\tools\\ffmpeg\\bin\\ffmpeg.exe"]
                : ["/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg"];

            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private void InitiateDownload(string sourceFile, string targetFile, CancellationToken token)
        {
            if (!File.Exists(sourceFile))
            {
                LogAndAppendMessage($"Source file not found: {sourceFile}", LogLevel.Error);
                return;
            }

            string finalDest = targetFile;

            LoadRequest downloadRequest = new(sourceFile, new LoadRequestOptions()
            {
                CancellationToken = token,
                CreateSpeedReporter = true,
                SpeedReporterTimeout = 1000,
                Priority = RequestPriority.Normal,
                MaxBytesPerSecond = Options.MaxDownloadSpeed,
                DelayBetweenAttemps = Options.DelayBetweenAttemps,
                Filename = Path.GetFileName(finalDest),
                AutoStart = true,
                DestinationPath = _destinationPath.FullPath,
                Handler = Options.Handler,
                DeleteFilesOnFailure = true,
                RequestFailed = (_, __) => LogAndAppendMessage($"File copy failed: {finalDest}", LogLevel.Error),
                WriteMode = WriteMode.AppendOrTruncate,
            });

            _trackContainer.Add(downloadRequest);
        }

        private async Task DownloadAlbumCoverAsync(string? coverUrl, CancellationToken token)
        {
            if (string.IsNullOrEmpty(coverUrl))
                return;

            try
            {
                byte[] data = await _httpClient.GetByteArrayAsync(coverUrl, token);
                _albumCover = data;
                _logger.Trace($"Downloaded album cover: {data.Length} bytes");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to download album cover");
                _albumCover = null;
            }
        }

        private static DateTime ParseDate(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.Now;

            if (DateTime.TryParse(dateStr, out DateTime result))
                return result;

            return DateTime.Now;
        }

        public override void Dispose()
        {
            _httpClient.Dispose();
            base.Dispose();
        }
    }
}
