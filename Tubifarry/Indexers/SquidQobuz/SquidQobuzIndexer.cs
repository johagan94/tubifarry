using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser;
using NzbDrone.Core.ThingiProvider;

namespace Tubifarry.Indexers.SquidQobuz
{
    public class SquidQobuzIndexer : HttpIndexerBase<SquidQobuzIndexerSettings>
    {
        private readonly ISquidQobuzRequestGenerator _requestGenerator;
        private readonly ISquidQobuzParser _parser;

        public override string Name => "SquidQobuz";
        public override string Protocol => nameof(SquidQobuzDownloadProtocol);
        public override bool SupportsRss => false;
        public override bool SupportsSearch => true;
        public override int PageSize => 30;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(2);

        public override ProviderMessage Message => new(
            "SquidQobuz searches Qobuz via squid.wtf's API. " +
            "Configure a region-specific API endpoint in settings.",
            ProviderMessageType.Info);

        public SquidQobuzIndexer(
            ISquidQobuzRequestGenerator requestGenerator,
            ISquidQobuzParser parser,
            IHttpClient httpClient,
            IIndexerStatusService statusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger)
            : base(httpClient, statusService, configService, parsingService, logger)
        {
            _requestGenerator = requestGenerator;
            _parser = parser;
        }

        protected override async Task Test(List<ValidationFailure> failures)
        {
            string baseUrl = Settings.BaseUrl.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                failures.Add(new ValidationFailure("BaseUrl", "Base URL is required"));
                return;
            }

            try
            {
                string testUrl = $"{baseUrl}/get-music?q=test&limit=1";
                HttpRequest req = new(testUrl) { RequestTimeout = TimeSpan.FromSeconds(Settings.RequestTimeout) };
                HttpResponse response = await _httpClient.ExecuteAsync(req);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    failures.Add(new ValidationFailure("BaseUrl", $"Cannot connect to squid.wtf Qobuz API: HTTP {(int)response.StatusCode}"));
                    return;
                }

                if (string.IsNullOrWhiteSpace(response.Content) || !response.Content.Contains("\"data\""))
                {
                    failures.Add(new ValidationFailure("BaseUrl", "Unexpected response from squid.wtf Qobuz API"));
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error connecting to squid.wtf Qobuz API");
                failures.Add(new ValidationFailure("BaseUrl", ex.Message));
            }
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            _requestGenerator.SetSetting(Settings);
            return _requestGenerator;
        }

        public override IParseIndexerResponse GetParser() => _parser;
    }
}
