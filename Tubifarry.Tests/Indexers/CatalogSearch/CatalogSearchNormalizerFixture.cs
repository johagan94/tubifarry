using Tubifarry.Indexers.CatalogSearch;
using System.Text.Json;
using Xunit;

namespace Tubifarry.Tests.Indexers.CatalogSearch;

public class CatalogSearchNormalizerFixture
{
    [Fact]
    public void IsArtistMatch_handles_unicode_hyphens()
    {
        Assert.True(CatalogSearchNormalizer.IsArtistMatch("Mach-Hommy", "Mach‐Hommy", []));
    }

    [Fact]
    public void IsAlbumMatch_handles_punctuation_variants()
    {
        Assert.True(CatalogSearchNormalizer.IsAlbumMatch("B.O.A.T.S. II #METIME", ["BOATS II METIME"]));
    }

    [Fact]
    public void IsAlbumMatch_handles_apostrophes()
    {
        Assert.True(CatalogSearchNormalizer.IsAlbumMatch("Mach's Hard Lemonade", ["Machs Hard Lemonade"]));
    }

    [Fact]
    public void BuildQueryVariants_prefers_exact_album_artist_then_normalized_fallbacks()
    {
        string[] variants = CatalogSearchNormalizer.BuildQueryVariants("Mach‐Hommy", "Mach's Hard Lemonade").ToArray();

        Assert.Equal("Mach's Hard Lemonade Mach‐Hommy", variants[0]);
        Assert.Contains("Machs Hard Lemonade Mach Hommy", variants);
        Assert.Equal(variants.Length, variants.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void RequestContext_round_trips_for_parser_filtering()
    {
        CatalogSearchRequestContext context = new("Mach‐Hommy", ["Mach Hommy"], ["Mach's Hard Lemonade"], true);

        CatalogSearchRequestContext result = CatalogSearchRequestContext.FromJson(JsonSerializer.Serialize(context));

        Assert.Equal("Mach‐Hommy", result.SearchArtist);
        Assert.Equal(["Mach Hommy"], result.ArtistAliases);
        Assert.Equal(["Mach's Hard Lemonade"], result.ExpectedAlbums);
        Assert.True(result.IsArtistSearch);
    }
}
