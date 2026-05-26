using NzbDrone.Core.Annotations;

namespace Tubifarry.Indexers.Tidal
{
    public enum TidalConnectionMode
    {
        [FieldOption("Direct TIDAL OpenAPI")]
        DirectOpenApi = 0,

        [FieldOption("Monochrome Proxy")]
        MonochromeProxy = 1
    }
}
