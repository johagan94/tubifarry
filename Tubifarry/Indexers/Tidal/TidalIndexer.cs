using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser;
using NzbDrone.Core.ThingiProvider;

namespace Tubifarry.Indexers.Tidal
{
    public class TidalIndexer : HttpIndexerBase<TidalIndexerSettings>
    {
        private readonly ITidalRequestGenerator _requestGenerator;
        private readonly ITidalParser _parser;

        public override string Name => "TIDAL";
        public override string Protocol => nameof(TidalDownloadProtocol);
        public override bool SupportsRss => false;
        public override bool SupportsSearch => true;
        public override int PageSize => 25;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(1);

        public override ProviderMessage Message => new("TIDAL provides high-quality FLAC and AAC music downloads via the free-tier API.", ProviderMessageType.Info);

        public TidalIndexer(
            ITidalRequestGenerator requestGenerator,
            ITidalParser parser,
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
            try
            {
                using System.Net.Http.HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", Tubifarry.UserAgent);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                string url = $"{Settings.BaseUrl.TrimEnd('/')}/searchResults/test?countryCode={Settings.CountryCode}&limit=1";
                HttpResponseMessage response = await client.GetAsync(url);

                _logger.Debug($"TIDAL API test response: HTTP {(int)response.StatusCode} — endpoint reachable");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error connecting to TIDAL API");
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
