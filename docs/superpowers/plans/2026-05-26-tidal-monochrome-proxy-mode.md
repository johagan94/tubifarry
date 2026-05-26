# TIDAL Monochrome Proxy Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a personal-testing TIDAL mode that routes Tubifarry catalog and manifest work through a Monochrome-compatible HiFi API endpoint without requiring TIDAL client credentials.

**Architecture:** Add explicit connection-mode settings shared by the TIDAL indexer and download client. In direct mode, preserve current OpenAPI authenticated behavior; in Monochrome proxy mode, generate HiFi API requests, parse HiFi search/album/manifest JSON, and leave preview-only limitations visible in logs.

**Tech Stack:** C# / .NET 8, Lidarr plugin APIs, FluentValidation, xUnit.

---

### Task 1: TIDAL Connection Mode Settings

**Files:**
- Modify: `Tubifarry/Indexers/Tidal/TidalIndexerSettings.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalProviderSettings.cs`
- Test: `Tubifarry.Tests/Indexers/Tidal/TidalRequestGeneratorFixture.cs`

- [ ] **Step 1: Write failing settings tests**

Add tests showing Monochrome proxy mode validates with an empty client ID/secret when a valid proxy URL is supplied, and direct mode still requires client credentials.

- [ ] **Step 2: Run focused tests and verify red**

Run:

```powershell
$env:MSBuildSDKsPath=$null
$lidarrSrc = (Resolve-Path 'Submodules/Lidarr/src').Path + '\'
dotnet test Tubifarry.Tests/Tubifarry.Tests.csproj -c Debug -p:SolutionDir="$lidarrSrc" --filter TidalRequestGeneratorFixture
```

Expected: compilation failure because the connection mode and proxy URL properties do not exist.

- [ ] **Step 3: Implement conditional settings validation**

Add `TidalConnectionMode` with values `DirectOpenApi = 0` and `MonochromeProxy = 1`. Add `ConnectionMode` and `MonochromeBaseUrl` to both TIDAL settings classes. Require credentials only for direct mode and require a well-formed absolute Monochrome URL for proxy mode.

- [ ] **Step 4: Run focused tests and verify green**

Run the same focused test command. Expected: focused tests pass.

### Task 2: Proxy Search Request And Parser

**Files:**
- Modify: `Tubifarry/Indexers/Tidal/TidalRequestGenerator.cs`
- Modify: `Tubifarry/Indexers/Tidal/TidalParser.cs`
- Modify: `Tubifarry/Indexers/Tidal/TidalRecords.cs`
- Test: `Tubifarry.Tests/Indexers/Tidal/TidalRequestGeneratorFixture.cs`
- Create: `Tubifarry.Tests/Indexers/Tidal/TidalParserFixture.cs`

- [ ] **Step 1: Write failing request-generation and parser tests**

Add a test proving proxy mode creates `/search/?al=` requests against `https://us-west.monochrome.tf`, with no authorization header. Add a parser test with a representative Monochrome payload containing `data.albums.items[0]` for Daft Punk / Discovery and assert it returns a `tidal://album/1550545` release.

- [ ] **Step 2: Run focused tests and verify red**

Run:

```powershell
$env:MSBuildSDKsPath=$null
$lidarrSrc = (Resolve-Path 'Submodules/Lidarr/src').Path + '\'
dotnet test Tubifarry.Tests/Tubifarry.Tests.csproj -c Debug -p:SolutionDir="$lidarrSrc" --filter "TidalRequestGeneratorFixture|TidalParserFixture"
```

Expected: tests fail because proxy search and parser support are not implemented.

- [ ] **Step 3: Implement proxy request and parser support**

In `TidalRequestGenerator`, branch on connection mode. Proxy mode builds `/search/?al=<query>` URLs from the configured Monochrome base URL and skips token acquisition. In `TidalParser`, detect Monochrome responses and map `data.albums.items` into `ReleaseInfo` using album title, first artist, release date, track count, audio quality, cover, and album ID.

- [ ] **Step 4: Run focused tests and verify green**

Run the same focused test command. Expected: request and parser tests pass.

### Task 3: Proxy Download Album And Manifest Handling

**Files:**
- Modify: `Tubifarry/Download/Clients/Tidal/TidalDownloadManager.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalDownloadOptions.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalDownloadRequest.cs`
- Modify: `Tubifarry/Indexers/Tidal/TidalRecords.cs`
- Test: `Tubifarry.Tests/Indexers/Tidal/TidalRequestGeneratorFixture.cs`

- [ ] **Step 1: Write failing helper tests where practical**

Add test coverage for quality-to-format URL generation or use existing focused tests to force compilation against new download option properties. The production change remains small and is verified with full build plus live test because `TidalDownloadRequest` is integration-heavy.

- [ ] **Step 2: Implement proxy download options**

Pass connection mode and Monochrome base URL from `TidalClient` settings into `TidalDownloadOptions`.

- [ ] **Step 3: Implement proxy album and manifest calls**

In proxy mode, skip `TidalAuthHelper`, request `/album/?id=<albumId>`, parse `data.items[*].item` into the existing track flow, request `/trackManifests/?id=<trackId>&quality=<quality>&adaptive=false&formats=<format>`, unwrap nested `data.data.attributes.uri`, and log preview-only `trackPresentation` and `previewReason`.

- [ ] **Step 4: Run full tests**

Run:

```powershell
$env:MSBuildSDKsPath=$null
$lidarrSrc = (Resolve-Path 'Submodules/Lidarr/src').Path + '\'
dotnet test Tubifarry.Tests/Tubifarry.Tests.csproj -c Debug -p:SolutionDir="$lidarrSrc"
```

Expected: all tests pass.

### Task 4: Build, Deploy, And Live Test On Unraid Lidarr

**Files:**
- Modify only source/test files above.

- [ ] **Step 1: Build against live Lidarr assembly version**

Run:

```powershell
$env:MSBuildSDKsPath=$null
$lidarrSrc = (Resolve-Path 'Submodules/Lidarr/src').Path + '\'
dotnet build Tubifarry.Tests/Tubifarry.Tests.csproj -c Debug -p:SolutionDir="$lidarrSrc" -p:AssemblyVersion=3.1.3.4956 -p:AssemblyConfiguration=Debug-live-unraid -t:Rebuild
```

Expected: build succeeds and produces plugin DLLs compatible with live Lidarr `3.1.3.4956`.

- [ ] **Step 2: Run tests against the live-compatible build**

Run:

```powershell
$env:MSBuildSDKsPath=$null
$lidarrSrc = (Resolve-Path 'Submodules/Lidarr/src').Path + '\'
dotnet test Tubifarry.Tests/Tubifarry.Tests.csproj -c Debug -p:SolutionDir="$lidarrSrc" -p:AssemblyVersion=3.1.3.4956 -p:AssemblyConfiguration=Debug-live-unraid --no-build
```

Expected: all tests pass.

- [ ] **Step 3: Deploy plugin to Unraid Lidarr**

Copy the live-compatible plugin output into `/mnt/cache/appdata/lidarr/plugins/johagan/Tubifarry`, restart the Lidarr Docker container, and verify no new plugin-load errors appear in logs.

- [ ] **Step 4: Configure and test provider schemas**

Using Lidarr API, verify the TIDAL indexer and TIDAL download client expose Monochrome proxy settings. Configure them for Monochrome proxy mode with `https://us-west.monochrome.tf`.

- [ ] **Step 5: Live search/download validation**

Run a known Daft Punk / Discovery search. If a download is started, record whether the manifest is preview-only or full-useable and whether Lidarr keeps the plugin loaded cleanly after the attempt.
