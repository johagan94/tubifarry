using FluentValidation.Results;
using Moq;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Validation;
using Tubifarry.Core.Replacements;
using Tubifarry.Core.Telemetry;
using Tubifarry.Core.Utilities;
using Xunit;

namespace Tubifarry.Tests.Core.Replacements;

public class ExtendedHttpIndexerBaseFixture
{
    [Fact]
    public async Task Fetch_artist_search_returns_empty_without_generating_requests_when_search_is_disabled()
    {
        Mock<IIndexerRequestGenerator<LazyIndexerPageableRequest>> generator = new();
        TestExtendedIndexer indexer = new(generator.Object)
        {
            Definition = new IndexerDefinition
            {
                Id = 42,
                Name = "Disabled Test Indexer",
                Settings = new TestIndexerSettings()
            }
        };

        IList<ReleaseInfo> releases = await indexer.Fetch(new ArtistSearchCriteria
        {
            Artist = new Artist { Name = "Test Artist" }
        });

        Assert.Empty(releases);
        generator.Verify(g => g.GetSearchRequests(It.IsAny<ArtistSearchCriteria>()), Times.Never);
    }

    private sealed class TestExtendedIndexer : ExtendedHttpIndexerBase<TestIndexerSettings, LazyIndexerPageableRequest>
    {
        private readonly IIndexerRequestGenerator<LazyIndexerPageableRequest> _generator;

        public TestExtendedIndexer(IIndexerRequestGenerator<LazyIndexerPageableRequest> generator)
            : base(
                Mock.Of<IHttpClient>(),
                Mock.Of<IIndexerStatusService>(),
                Mock.Of<IConfigService>(),
                Mock.Of<IParsingService>(),
                Mock.Of<ISentryHelper>(),
                LogManager.GetCurrentClassLogger())
        {
            _generator = generator;
        }

        public override string Name => "Disabled Test Indexer";
        public override string Protocol => "test";
        public override bool SupportsRss => false;
        public override bool SupportsSearch => false;
        public override int PageSize => 50;
        public override TimeSpan RateLimit => TimeSpan.Zero;

        public override IIndexerRequestGenerator<LazyIndexerPageableRequest> GetExtendedRequestGenerator() => _generator;

        public override IParseIndexerResponse GetParser() => Mock.Of<IParseIndexerResponse>();

        protected override Task Test(List<ValidationFailure> failures) => Task.CompletedTask;
    }

    private sealed class TestIndexerSettings : IIndexerSettings
    {
        public string BaseUrl { get; set; } = "https://example.com";
        public int? EarlyReleaseLimit { get; set; }

        public NzbDroneValidationResult Validate() => new();
    }
}
