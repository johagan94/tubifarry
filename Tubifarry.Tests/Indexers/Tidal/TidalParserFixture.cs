using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using System.Text.Json;
using Tubifarry.Indexers.CatalogSearch;
using Tubifarry.Indexers.Tidal;
using Xunit;

namespace Tubifarry.Tests.Indexers.Tidal;

public class TidalParserFixture
{
    [Fact]
    public void ParseResponse_maps_monochrome_album_search_results_to_tidal_releases()
    {
        const string payload = """
        {
          "version": "2.10",
          "data": {
            "albums": {
              "limit": 25,
              "offset": 0,
              "totalNumberOfItems": 1,
              "items": [
                {
                  "id": 1550545,
                  "title": "Discovery",
                  "duration": 3669,
                  "numberOfTracks": 14,
                  "releaseDate": "2001-03-13",
                  "audioQuality": "LOSSLESS",
                  "cover": "7d3b9810-5634-400c-ad89-50609e0ce800",
                  "explicit": false,
                  "artists": [
                    {
                      "id": 8847,
                      "name": "Daft Punk"
                    }
                  ]
                }
              ]
            }
          }
        }
        """;

        HttpRequest request = new("https://us-west.monochrome.tf/search/?al=Discovery%20Daft%20Punk")
        {
            ContentSummary = JsonSerializer.Serialize(new CatalogSearchRequestContext("Daft Punk", [], ["Discovery"], false))
        };
        IndexerResponse response = new(new IndexerRequest(request), new HttpResponse(request, new HttpHeader(), payload));

        var releases = new TidalParser(LogManager.GetCurrentClassLogger()).ParseResponse(response);

        var release = Assert.Single(releases);
        Assert.Equal("Daft Punk", release.Artist);
        Assert.Equal("Discovery", release.Album);
        Assert.Equal("tidal://album/1550545", release.DownloadUrl);
        Assert.Equal("https://tidal.com/album/1550545", release.InfoUrl);
        Assert.Equal("FLAC", release.Codec);
    }
}
