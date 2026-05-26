using NLog;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Music;
using Tubifarry.Download.Clients.SquidQobuz;
using Tubifarry.Indexers.SquidQobuz;
using Xunit;

namespace Tubifarry.Tests.Indexers.SquidQobuz;

public class SquidQobuzRequestGeneratorFixture
{
    [Fact]
    public void GetSearchRequests_sends_token_country_header()
    {
        SquidQobuzRequestGenerator generator = new(LogManager.GetCurrentClassLogger());
        generator.SetSetting(new SquidQobuzIndexerSettings
        {
            BaseUrl = "https://qobuz.squid.wtf/api",
            RequestTimeout = 60,
            TokenCountry = "AU"
        });

        IndexerRequest request = Assert.Single(Assert.Single(generator.GetSearchRequests(new AlbumSearchCriteria
        {
            Artist = new Artist { Name = "The Avalanches" },
            AlbumTitle = "Since I Left You"
        }).GetAllTiers()));

        Assert.Contains("/get-music?", request.Url.FullUri);
        Assert.Contains("&limit=30", request.Url.FullUri);
        Assert.Equal("AU", request.HttpRequest.Headers["Token-Country"]);
    }

    [Fact]
    public void Settings_require_two_letter_token_country()
    {
        Assert.False(new SquidQobuzIndexerSettings { TokenCountry = "Australia" }.Validate().IsValid);
        Assert.True(new SquidQobuzIndexerSettings { TokenCountry = "AU" }.Validate().IsValid);

        Assert.False(new SquidQobuzProviderSettings { DownloadPath = @"C:\Music", TokenCountry = "Australia" }.Validate().IsValid);
        Assert.True(new SquidQobuzProviderSettings { DownloadPath = @"C:\Music", TokenCountry = "AU" }.Validate().IsValid);
    }

    [Fact]
    public void BuildFailureMessage_explains_upstream_qobuz_blocks()
    {
        string message = SquidQobuzApi.BuildFailureMessage(
            "squid.wtf Qobuz",
            403,
            """{"success":false,"error":"Upstream Qobuz error (403)"}""");

        Assert.Contains("upstream Qobuz request is currently blocked", message);
        Assert.Contains("Token Country", message);
    }
}
