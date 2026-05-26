# TIDAL Monochrome Proxy Mode Design

**Date:** 2026-05-26

**Goal:** Add a personal-testing mode for Tubifarry's TIDAL indexer and download client that uses a Monochrome-compatible HiFi API endpoint instead of requiring TIDAL application credentials.

## Context

Tubifarry currently communicates with TIDAL OpenAPI v2 directly. The indexer generates `/v2/searchResults/...` JSON:API requests and the download client fetches `/v2/albums/...` and `/v2/trackManifests/...`, all authenticated with a client-credentials bearer token supplied through Lidarr settings.

Monochrome does not expose a TIDAL end-user login flow. Its proxy/API path provides catalog and playback-related endpoints without Tubifarry supplying a TIDAL client ID or client secret.

Live endpoint checks performed on 2026-05-26 found:

- `https://api.monochrome.tf` returns HTTP 503 (`Service Suspended`) and is not a usable test endpoint.
- `https://monochrome-api.samidy.com` returns catalog results, but its current API version does not expose `/trackManifests/`.
- `https://us-west.monochrome.tf` returns catalog results and serves `/trackManifests/`, so it is the selected default test endpoint.
- A probed manifest from `https://us-west.monochrome.tf` had `trackPresentation: PREVIEW` and `previewReason: FULL_REQUIRES_SUBSCRIPTION`. Proxy mode must therefore report preview/full-stream limitations accurately rather than promising lossless full-track downloads.

## Chosen Approach

Introduce an explicit Monochrome proxy mode within the existing TIDAL provider surfaces. Direct TIDAL OpenAPI operation remains available; proxy mode is used for personal testing and defaults its proxy URL to `https://us-west.monochrome.tf`.

This avoids embedding shared TIDAL credentials in Tubifarry while allowing live Lidarr testing of catalog resolution and available manifest/download behavior through Monochrome.

## Configuration

Both the TIDAL indexer settings and TIDAL download client settings will gain a connection-mode choice:

- `Direct TIDAL OpenAPI`: current behavior; requires `Client ID` and `Client Secret`.
- `Monochrome Proxy`: uses `Monochrome API Base URL`; client credentials are not required.

Proxy mode uses an editable absolute URL setting with default value `https://us-west.monochrome.tf`. Existing direct-mode settings are retained so this experiment does not remove the secure credential-based path.

Because Lidarr field visibility may not support dynamic mode-based hiding cleanly in this plugin, credentials may remain displayed but will be documented as required only for direct mode. Validation is conditional on the selected mode.

## Indexer Flow

In proxy mode:

1. The TIDAL indexer requests album catalog data using:

   `/search/?al=<escaped artist and album query>`

2. No bearer authorization header is added.
3. A proxy-aware parser reads the HiFi API response (`data.albums.items`) and maps results into Tubifarry `ReleaseInfo` entries using the existing `tidal://album/<id>` download URLs.
4. The Lidarr connection test exercises this proxy catalog query, so unavailable or malformed proxy services fail visibly.

In direct mode, the existing authenticated OpenAPI request and parser behavior remain unchanged.

## Download Flow

In proxy mode:

1. Album metadata and tracks are requested through:

   `/album/?id=<albumId>`

2. Each track manifest is requested through:

   `/trackManifests/?id=<trackId>&quality=<quality>&adaptive=false&formats=<mapped format>`

3. The proxy's nested manifest response is unwrapped into the manifest URI already consumed by Tubifarry's DASH downloader.
4. If the manifest reports preview-only playback, the client logs this condition clearly. A preview result is not misreported as successful full-lossless availability.

Direct mode continues to use TIDAL OpenAPI with credential-derived bearer authorization.

## Error Handling

- Invalid proxy URLs fail settings validation.
- Connection tests fail when the configured proxy cannot perform catalog search.
- Unsupported or malformed proxy album/manifest responses produce actionable exceptions naming the failed proxy operation.
- Preview-only manifest responses remain usable for personal workflow verification, while logs identify the entitlement limitation.

## Tests

Implementation follows a failing-test-first sequence:

1. Settings tests proving proxy mode validates without TIDAL credentials and direct mode still requires credentials.
2. Request-generator tests proving proxy mode creates `/search/?al=` requests without an authorization header.
3. Parser tests proving a representative Monochrome album-search payload maps to a TIDAL release.
4. Download response tests for proxy album metadata and nested manifest unwrapping, including preview presentation handling where testable without network calls.
5. Full plugin test suite and live Lidarr validation after the compatible plugin assembly is deployed.

## Deployment And Personal Test

After local verification, deploy the plugin build targeting the live Lidarr assembly version on Unraid. Configure the live TIDAL indexer and client for Monochrome proxy mode using `https://us-west.monochrome.tf`, then validate:

- Lidarr can save/test the proxy-backed provider settings.
- A known album search such as Daft Punk / Discovery returns a TIDAL release.
- A selected download attempt reaches the proxy manifest path and records whether the available response is preview-only or usable for the intended file workflow.

This test establishes integration viability. It does not claim the public Monochrome service provides full subscription-quality audio for arbitrary catalog items.
