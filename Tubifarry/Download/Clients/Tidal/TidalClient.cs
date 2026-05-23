using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using Tubifarry.Download.Base;

namespace Tubifarry.Download.Clients.Tidal
{
    public class TidalClient : DownloadClientBase<TidalProviderSettings>
    {
        private readonly ITidalDownloadManager _downloadManager;
        private readonly INamingConfigService _namingService;

        public TidalClient(
            ITidalDownloadManager downloadManager,
            IConfigService configService,
            IDiskProvider diskProvider,
            INamingConfigService namingConfigService,
            IRemotePathMappingService remotePathMappingService,
            ILocalizationService localizationService,
            Logger logger)
            : base(configService, diskProvider, remotePathMappingService, localizationService, logger)
        {
            _downloadManager = downloadManager;
            _namingService = namingConfigService;
        }

        public override string Name => "TIDAL";
        public override string Protocol => nameof(TidalDownloadProtocol);
        public new TidalProviderSettings Settings => base.Settings;

        public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer) => _downloadManager.Download(remoteAlbum, indexer, _namingService.GetConfig(), this);

        public override IEnumerable<DownloadClientItem> GetItems() => _downloadManager.GetItems();

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            if (deleteData)
                DeleteItemData(item);
            _downloadManager.RemoveItem(item);
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
                using System.Net.Http.HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", Tubifarry.UserAgent);

                string authUrl = "https://auth.tidal.com/v1/oauth2/token";
                Dictionary<string, string> body = new()
                {
                    ["client_id"] = TidalAuthHelper.ClientId,
                    ["client_secret"] = TidalAuthHelper.ClientSecret,
                    ["grant_type"] = "client_credentials"
                };

                using FormUrlEncodedContent content = new(body);
                HttpResponseMessage response = client.PostAsync(authUrl, content).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add(new ValidationFailure("", $"Cannot authenticate with TIDAL: HTTP {(int)response.StatusCode}"));
                    return;
                }

                _logger.Debug("Successfully authenticated with TIDAL API");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error connecting to TIDAL API");
                failures.Add(new ValidationFailure("", $"Cannot connect to TIDAL API: {ex.Message}"));
            }
        }
    }
}
