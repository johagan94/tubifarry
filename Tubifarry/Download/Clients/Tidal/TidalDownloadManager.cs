using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using Tubifarry.Download.Base;

namespace Tubifarry.Download.Clients.Tidal
{
    public interface ITidalDownloadManager : IBaseDownloadManager<TidalDownloadRequest, TidalDownloadOptions, TidalClient>
    { }

    public class TidalDownloadManager : BaseDownloadManager<TidalDownloadRequest, TidalDownloadOptions, TidalClient>, ITidalDownloadManager
    {
        public TidalDownloadManager(Logger logger) : base(logger)
        {
        }

        protected override Task<TidalDownloadRequest> CreateDownloadRequest(
            RemoteAlbum remoteAlbum,
            IIndexer indexer,
            NamingConfig namingConfig,
            TidalClient provider)
        {
            bool isTrack = remoteAlbum.Release.DownloadUrl?.StartsWith("tidal://track/") == true;
            string downloadUrl = remoteAlbum.Release.DownloadUrl ?? "";

            string itemId = "";
            if (downloadUrl.StartsWith("tidal://album/"))
                itemId = downloadUrl.Replace("tidal://album/", "");
            else if (downloadUrl.StartsWith("tidal://track/"))
                itemId = downloadUrl.Replace("tidal://track/", "");

            if (string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(remoteAlbum.Release.InfoUrl))
            {
                string[] parts = remoteAlbum.Release.InfoUrl.Split('/');
                itemId = parts.Length > 0 ? parts[^1] : "";
            }

            _logger.Trace($"Type from URL: {(isTrack ? "Track" : "Album")}, Extracted ID: {itemId}");

            TidalDownloadOptions options = new()
            {
                Handler = _requesthandler,
                DownloadPath = provider.Settings.DownloadPath,
                BaseUrl = "https://openapi.tidal.com/v2",
                MaxDownloadSpeed = provider.Settings.MaxDownloadSpeed * 1024,
                ConnectionRetries = provider.Settings.ConnectionRetries,
                NamingConfig = namingConfig,
                RequestInterceptors = [],
                DelayBetweenAttemps = TimeSpan.FromSeconds(3),
                NumberOfAttempts = (byte)provider.Settings.ConnectionRetries,
                ClientInfo = DownloadClientItemClientInfo.FromDownloadClient(provider, false),
                IsTrack = isTrack,
                ItemId = itemId,
                CountryCode = provider.Settings.CountryCode,
                DownloadQuality = provider.Settings.DownloadQuality,
                OutputFormat = provider.Settings.OutputFormat,
                Mp3Bitrate = provider.Settings.Mp3Bitrate
            };

            _requesthandler.MaxParallelism = provider.Settings.MaxParallelDownloads;
            return Task.FromResult(new TidalDownloadRequest(remoteAlbum, options));
        }
    }
}
