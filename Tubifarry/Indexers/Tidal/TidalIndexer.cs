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

        public override ProviderMessage Message => new("TIDAL catalog search can use Direct OpenAPI credentials or Monochrome Proxy mode for personal testing.", ProviderMessageType.Info);

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

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            _requestGenerator.SetSetting(Settings);
            return _requestGenerator;
        }

        public override IParseIndexerResponse GetParser() => _parser;
    }
}
