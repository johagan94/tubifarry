using NLog;
using NzbDrone.Core.Indexers;
using Tubifarry.Download.Clients.Tidal;
using Tubifarry.Indexers.Tidal;
using Xunit;

namespace Tubifarry.Tests.Indexers.Tidal;

public class TidalRequestGeneratorFixture
{
    [Fact]
    public void GetRecentRequests_builds_authenticated_catalog_probe()
    {
        TidalRequestGenerator generator = new(LogManager.GetCurrentClassLogger(), new StaticTokenProvider("catalog-token"));
        generator.SetSetting(new TidalIndexerSettings
        {
            BaseUrl = "https://openapi.tidal.com/v2",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            CountryCode = "US",
            SearchLimit = 1
        });

        IndexerRequest request = Assert.Single(Assert.Single(generator.GetRecentRequests().GetAllTiers()));

        Assert.Contains("/searchResults/Discovery%20Daft%20Punk?", request.Url.FullUri);
        Assert.Equal("Bearer catalog-token", request.HttpRequest.Headers["Authorization"]);
    }

    [Fact]
    public void Settings_require_explicit_api_credentials()
    {
        Assert.False(new TidalIndexerSettings().Validate().IsValid);

        TidalIndexerSettings settings = new()
        {
            ClientId = "client-id",
            ClientSecret = "client-secret"
        };

        Assert.True(settings.Validate().IsValid);
    }

    private sealed class StaticTokenProvider(string token) : ITidalTokenProvider
    {
        public string GetAccessToken(string clientId, string clientSecret) => token;

        public void InvalidateToken(string clientId)
        {
        }
    }
}
