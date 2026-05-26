using System.Text.Json.Serialization;
using Tubifarry.Core.Utilities;

namespace Tubifarry.Indexers.Tidal
{
    #region Search & API Response Models

    public record TidalRequestData(
        [property: JsonPropertyName("baseUrl")] string BaseUrl,
        [property: JsonPropertyName("searchType")] string SearchType,
        [property: JsonPropertyName("limit")] int Limit);

    public record TidalSearchResponse(
        [property: JsonPropertyName("data")] TidalSearchData? Data,
        [property: JsonPropertyName("included")] List<TidalIncludedItem>? Included,
        [property: JsonPropertyName("meta")] TidalSearchMeta? Meta);

    public record TidalSearchData(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("attributes")] TidalSearchDataAttributes? Attributes,
        [property: JsonPropertyName("relationships")] TidalSearchRelationships? Relationships,
        [property: JsonPropertyName("albums")] TidalMonochromeAlbumSection? Albums);

    public record TidalMonochromeAlbumSection(
        [property: JsonPropertyName("items")] List<TidalMonochromeAlbum>? Items);

    public record TidalMonochromeAlbumEnvelope(
        [property: JsonPropertyName("data")] TidalMonochromeAlbum? Data);

    public record TidalMonochromeAlbum(
        [property: JsonPropertyName("id"), JsonConverter(typeof(StringConverter))] string Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("duration"), JsonConverter(typeof(NullableDoubleConverter))] double? Duration,
        [property: JsonPropertyName("numberOfTracks")] int? NumberOfTracks,
        [property: JsonPropertyName("releaseDate")] string? ReleaseDate,
        [property: JsonPropertyName("audioQuality")] string? AudioQuality,
        [property: JsonPropertyName("cover")] string? Cover,
        [property: JsonPropertyName("explicit")] bool? Explicit,
        [property: JsonPropertyName("artist")] TidalMonochromeArtist? Artist,
        [property: JsonPropertyName("artists")] List<TidalMonochromeArtist>? Artists,
        [property: JsonPropertyName("items")] List<TidalMonochromeTrackItem>? Items);

    public record TidalMonochromeTrackItem(
        [property: JsonPropertyName("item")] TidalMonochromeTrack? Item,
        [property: JsonPropertyName("type")] string? Type);

    public record TidalMonochromeTrack(
        [property: JsonPropertyName("id"), JsonConverter(typeof(StringConverter))] string Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("duration"), JsonConverter(typeof(NullableDoubleConverter))] double? Duration,
        [property: JsonPropertyName("trackNumber")] int? TrackNumber,
        [property: JsonPropertyName("releaseDate")] string? ReleaseDate,
        [property: JsonPropertyName("audioQuality")] string? AudioQuality,
        [property: JsonPropertyName("cover")] string? Cover,
        [property: JsonPropertyName("explicit")] bool? Explicit,
        [property: JsonPropertyName("artist")] TidalMonochromeArtist? Artist,
        [property: JsonPropertyName("artists")] List<TidalMonochromeArtist>? Artists,
        [property: JsonPropertyName("album")] TidalMonochromeAlbum? Album);

    public record TidalMonochromeArtist(
        [property: JsonPropertyName("id"), JsonConverter(typeof(NullableStringConverter))] string? Id,
        [property: JsonPropertyName("name")] string? Name);

    public record TidalSearchDataAttributes(
        [property: JsonPropertyName("totalResults")] int? TotalResults);

    public record TidalSearchItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("attributes")] TidalSearchAttributes? Attributes,
        [property: JsonPropertyName("relationships")] TidalSearchRelationships? Relationships);

    public record TidalSearchAttributes(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("artist")] string? Artist,
        [property: JsonPropertyName("trackNumber")] int? TrackNumber,
        [property: JsonPropertyName("duration"), JsonConverter(typeof(NullableDoubleConverter))] double? Duration,
        [property: JsonPropertyName("numberOfTracks")] int? NumberOfTracks,
        [property: JsonPropertyName("releaseDate")] string? ReleaseDate,
        [property: JsonPropertyName("audioQuality")] string? AudioQuality,
        [property: JsonPropertyName("isrc")] string? Isrc,
        [property: JsonPropertyName("popularity")] int? Popularity,
        [property: JsonPropertyName("explicit")] bool? Explicit,
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("copyright"), JsonConverter(typeof(NullableStringConverter))] string? Copyright,
        [property: JsonPropertyName("url")] string? Url);

    public record TidalSearchRelationships(
        [property: JsonPropertyName("artists")] TidalRelationshipData? Artists,
        [property: JsonPropertyName("albums")] TidalRelationshipData? Albums,
        [property: JsonPropertyName("tracks")] TidalRelationshipData? Tracks);

    public record TidalRelationshipData(
        [property: JsonPropertyName("data")] List<TidalIdItem>? Data);

    public record TidalIdItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type);

    public record TidalIncludedItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("attributes")] TidalIncludedAttributes? Attributes,
        [property: JsonPropertyName("relationships")] TidalSearchRelationships? Relationships);

    public record TidalIncludedAttributes(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("imageUrl")] string? ImageUrl,
        [property: JsonPropertyName("cover")] string? Cover,
        [property: JsonPropertyName("releaseDate")] string? ReleaseDate,
        [property: JsonPropertyName("numberOfTracks")] int? NumberOfTracks,
        [property: JsonPropertyName("numberOfItems")] int? NumberOfItems,
        [property: JsonPropertyName("trackNumber")] int? TrackNumber,
        [property: JsonPropertyName("duration"), JsonConverter(typeof(NullableDoubleConverter))] double? Duration,
        [property: JsonPropertyName("audioQuality")] string? AudioQuality,
        [property: JsonPropertyName("explicit")] bool? Explicit,
        [property: JsonPropertyName("copyright"), JsonConverter(typeof(NullableStringConverter))] string? Copyright);

    public record TidalSearchMeta(
        [property: JsonPropertyName("totalResults")] int TotalResults);

    #endregion Search & API Response Models

    #region Authentication Models

    public record TidalAuthResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    #endregion Authentication Models

    #region Track Manifest Models

    public record TidalManifestResponse(
        [property: JsonPropertyName("data")] TidalManifestData? Data);

    public record TidalManifestData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("attributes")] TidalManifestAttributes? Attributes,
        [property: JsonPropertyName("data")] TidalManifestData? Data);

    public record TidalManifestAttributes(
        [property: JsonPropertyName("uri")] string? Uri,
        [property: JsonPropertyName("formats")] List<string>? Formats,
        [property: JsonPropertyName("trackPresentation")] string? TrackPresentation,
        [property: JsonPropertyName("previewReason")] string? PreviewReason,
        [property: JsonPropertyName("hash")] string? Hash);

    #endregion Track Manifest Models

    #region Track Metadata Models

    public record TidalTrackResponse(
        [property: JsonPropertyName("data")] TidalTrackData? Data);

    public record TidalTrackData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("attributes")] TidalTrackAttributes? Attributes);

    public record TidalTrackAttributes(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("trackNumber")] int? TrackNumber,
        [property: JsonPropertyName("duration"), JsonConverter(typeof(NullableDoubleConverter))] double? Duration,
        [property: JsonPropertyName("isrc")] string? Isrc,
        [property: JsonPropertyName("audioQuality")] string? AudioQuality,
        [property: JsonPropertyName("explicit")] bool? Explicit,
        [property: JsonPropertyName("copyright"), JsonConverter(typeof(NullableStringConverter))] string? Copyright,
        [property: JsonPropertyName("version")] string? Version);

    #endregion Track Metadata Models

    #region Album Metadata Models

    public record TidalAlbumResponse(
        [property: JsonPropertyName("data")] TidalAlbumData? Data,
        [property: JsonPropertyName("included")] List<TidalIncludedItem>? Included);

    public record TidalAlbumData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("attributes")] TidalAlbumAttributes? Attributes,
        [property: JsonPropertyName("relationships")] TidalSearchRelationships? Relationships);

    public record TidalAlbumAttributes(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("releaseDate")] string? ReleaseDate,
        [property: JsonPropertyName("numberOfTracks")] int? NumberOfTracks,
        [property: JsonPropertyName("audioQuality")] string? AudioQuality,
        [property: JsonPropertyName("copyright"), JsonConverter(typeof(NullableStringConverter))] string? Copyright,
        [property: JsonPropertyName("imageUrl")] string? ImageUrl,
        [property: JsonPropertyName("cover")] string? Cover,
        [property: JsonPropertyName("explicit")] bool? Explicit);

    #endregion Album Metadata Models
}
