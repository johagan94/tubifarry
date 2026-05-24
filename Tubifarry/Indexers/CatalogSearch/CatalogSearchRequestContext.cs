using System.Text.Json;

namespace Tubifarry.Indexers.CatalogSearch
{
    public sealed record CatalogSearchRequestContext(
        string? SearchArtist,
        IReadOnlyList<string> ArtistAliases,
        IReadOnlyList<string> ExpectedAlbums,
        bool IsArtistSearch)
    {
        public static CatalogSearchRequestContext Empty { get; } = new(null, [], [], false);

        public static CatalogSearchRequestContext FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Empty;

            try
            {
                return JsonSerializer.Deserialize<CatalogSearchRequestContext>(json) ?? Empty;
            }
            catch
            {
                return Empty;
            }
        }
    }
}
