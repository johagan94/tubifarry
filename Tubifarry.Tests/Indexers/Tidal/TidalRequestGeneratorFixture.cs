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

    [Fact]
    public void Settings_allow_monochrome_proxy_without_api_credentials()
    {
        TidalIndexerSettings settings = new()
        {
            ConnectionMode = (int)TidalConnectionMode.MonochromeProxy,
            MonochromeBaseUrl = "https://us-west.monochrome.tf"
        };

        Assert.True(settings.Validate().IsValid);
    }

    [Fact]
    public void Monochrome_proxy_settings_require_valid_url()
    {
        TidalIndexerSettings settings = new()
        {
            ConnectionMode = (int)TidalConnectionMode.MonochromeProxy,
            MonochromeBaseUrl = "not a url"
        };

        Assert.False(settings.Validate().IsValid);
    }

    [Fact]
    public void Download_client_settings_allow_monochrome_proxy_without_api_credentials()
    {
        TidalProviderSettings settings = new()
        {
            DownloadPath = "C:\\Music",
            ConnectionMode = (int)TidalConnectionMode.MonochromeProxy,
            MonochromeBaseUrl = "https://us-west.monochrome.tf"
        };

        Assert.True(settings.Validate().IsValid);
    }

    [Fact]
    public void GetRecentRequests_builds_monochrome_proxy_catalog_probe_without_authorization()
    {
        TidalRequestGenerator generator = new(LogManager.GetCurrentClassLogger(), new StaticTokenProvider("unused-token"));
        generator.SetSetting(new TidalIndexerSettings
        {
            ConnectionMode = (int)TidalConnectionMode.MonochromeProxy,
            MonochromeBaseUrl = "https://us-west.monochrome.tf",
            CountryCode = "US",
            SearchLimit = 1
        });

        IndexerRequest request = Assert.Single(Assert.Single(generator.GetRecentRequests().GetAllTiers()));

        Assert.Equal("https://us-west.monochrome.tf/search/?al=Discovery%20Daft%20Punk", request.Url.FullUri);
        Assert.False(request.HttpRequest.Headers.ContainsKey("Authorization"));
    }

    private sealed class StaticTokenProvider(string token) : ITidalTokenProvider
    {
        public string GetAccessToken(string clientId, string clientSecret) => token;

        public void InvalidateToken(string clientId)
        {
        }
    }
}
