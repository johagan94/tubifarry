# SquidQobuz Recovery And TIDAL Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recover stranded SquidQobuz completed downloads after restart and make TIDAL health checks authenticate without embedded credentials.

**Architecture:** Add an original SquidQobuz completed-item recovery component that reconstructs only Lidarr-grabbed output folders using download history, then merge recovered items into client polling. Refactor TIDAL token acquisition to require configured credentials and have indexer testing flow through an authenticated catalog request handled by Lidarr's standard test pipeline.

**Tech Stack:** C# / .NET 8, Lidarr plugin APIs, xUnit, Moq.

---

### Task 1: SquidQobuz Completed Folder Recovery

**Files:**
- Create: `Tubifarry/Download/Clients/SquidQobuz/SquidQobuzCompletedItemRecovery.cs`
- Modify: `Tubifarry/Download/Clients/SquidQobuz/SquidQobuzClient.cs`
- Test: `Tubifarry.Tests/Download/Clients/SquidQobuz/SquidQobuzCompletedItemRecoveryFixture.cs`

- [ ] **Step 1: Write the failing recovery/filtering tests**

Test a configured output directory with one folder whose full path has a matching `DownloadGrabbed` history record and one unrelated folder. Assert only the grabbed folder is returned as `Completed`, with the recorded title and directory-path download ID.

- [ ] **Step 2: Run the focused tests and verify red**

Run:

```powershell
$env:MSBuildSDKsPath=$null
$lidarrSrc = (Resolve-Path 'Submodules/Lidarr/src').Path + '\'
dotnet test Tubifarry.Tests/Tubifarry.Tests.csproj -c Debug -p:SolutionDir="$lidarrSrc" --filter SquidQobuzCompletedItemRecoveryFixture
```

Expected: compilation/test failure because recovery behavior has not been implemented.

- [ ] **Step 3: Implement original recovery and client polling integration**

Create a recovery service using `IDiskProvider` and `IDownloadHistoryService`. Scan immediate album directories, require `GetLatestGrab(folderPath)` before yielding a `DownloadClientItem`, sum file sizes, and surface the item as movable/completed. Merge it into `SquidQobuzClient.GetItems()` without returning duplicate download IDs already present in memory.

- [ ] **Step 4: Run focused and full tests**

Expected: the new recovery tests and existing suite pass.

### Task 2: SquidQobuz Configuration Guidance

**Files:**
- Modify: `Tubifarry/Download/Clients/SquidQobuz/SquidQobuzProviderSettings.cs`
- Modify: `Tubifarry/Indexers/SquidQobuz/SquidQobuzIndexerSettings.cs`

- [ ] **Step 1: Replace the dead regional endpoint placeholder**

Use `https://qobuz.squid.wtf/api`, matching the tested working default endpoint.

- [ ] **Step 2: Verify build and tests remain green**

### Task 3: TIDAL Authenticated Indexer Diagnostic

**Files:**
- Modify: `Tubifarry/Indexers/Tidal/TidalRequestGenerator.cs`
- Modify: `Tubifarry/Indexers/Tidal/TidalIndexer.cs`
- Modify: `Tubifarry/Indexers/Tidal/TidalIndexerSettings.cs`
- Create: `Tubifarry.Tests/Indexers/Tidal/TidalRequestGeneratorFixture.cs`

- [ ] **Step 1: Write a failing request-generation test**

Supply a fake token provider, request the TIDAL recent/test chain, and assert that it includes a bearer-authenticated catalog search request.

- [ ] **Step 2: Route indexer test through the authenticated request generator**

Generate a small authenticated catalog query for `GetRecentRequests()` and remove the endpoint-reachability-only override so Lidarr handles unauthorized responses as test failures.

- [ ] **Step 3: Verify focused and full tests**

### Task 4: Remove Embedded TIDAL Credentials

**Files:**
- Modify: `Tubifarry/Download/Clients/Tidal/TidalAuthHelper.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalClient.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalDownloadManager.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalDownloadOptions.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalDownloadRequest.cs`
- Modify: `Tubifarry/Download/Clients/Tidal/TidalProviderSettings.cs`
- Modify: `Tubifarry/Indexers/Tidal/TidalIndexerSettings.cs`

- [ ] **Step 1: Require configured client credentials**

Add password-masked settings fields and validation for TIDAL client ID and secret in both provider configuration surfaces. Change token acquisition to receive those values as inputs rather than storing a credential in source.

- [ ] **Step 2: Verify the repository no longer contains the embedded credential**

Search TIDAL source for hard-coded auth constants and confirm only the public token endpoint remains.

- [ ] **Step 3: Run full tests**

Expected: test suite passes under the required Lidarr `SolutionDir` build property; any pre-existing reference warnings are recorded.

### Task 5: Milestone Hygiene And Publication

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Ignore generated smoke runtime output**

Add `/_temp/lidarr-smoke/` so local deployment checks do not become staged source changes again.

- [ ] **Step 2: Inspect diff and run final verification**

- [ ] **Step 3: Commit the milestone branch**

Commit only intended source, test, plan, and ignore-file changes. Publish only to an appropriate repository after confirming no credentials are present.
