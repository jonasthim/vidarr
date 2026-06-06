# Vidarr — Design

**Status:** Draft (post-brainstorm)
**Date:** 2026-06-06
**Author:** brainstorming session with the user
**Stack:** .NET 8 / C# backend, React + TypeScript frontend, SQLite via EF Core, ffmpeg + yt-dlp on host

---

## 1. Goals, non-goals, v1 scope

### Goal

A self-hosted, *arr-style application that maintains a library of music videos. Users follow artists and/or define genre/year auto-rules; Vidarr discovers and downloads matching videos from YouTube (yt-dlp) or NewzNab / Torznab indexers + download clients; then organizes files on disk and notifies media servers (Plex, Jellyfin/Emby).

Vidarr follows *arr-stack architecture deliberately (modules, terminology, REST API shape, decision pipeline) so existing *arr users feel at home.

### v1 in scope

- **Domain:** Artists, MusicVideos, MusicVideoFiles, Releases, Downloads, Quality Profiles, Custom Formats, Discovery Rules, Root Folders, Tags, History, Blocklist, Notifications, Indexers, Download Clients
- **Monitoring strategies:** followed-artists, manual add, genre / year / decade auto-rules
- **Metadata:** pluggable provider interface with **IMVDb** implementation (others deferred)
- **Sources:**
  - **YouTube indexer** with hybrid discovery (YouTube Data API v3 when an API key is configured; yt-dlp scraping fallback; YouTube channel RSS subscription for monitored artists)
  - **NewzNab** (Usenet) and **Torznab** (torrents) protocol handlers, implemented natively
- **Download clients:** qBittorrent, Transmission, Deluge, SABnzbd, NZBGet, and the YtDlp client (yt-dlp subprocess)
- **Decision pipeline:** Sonarr-style — Parser → specs → Custom Format engine → comparer
- **Quality model:** Quality Profiles (allowed list + cutoff + upgrade) + Custom Formats (Sonarr v4 style). Custom Formats subsume what older *arr versions split between Release Profiles and Custom Formats.
- **Compilations:**
  - Multi-file: per-file matcher + per-video import
  - Single-file concert (one MKV w/ chapters): ffmpeg-based chapter-split + per-chapter match
- **Web UI:** React + TypeScript SPA, talks to the REST API, polled (no SignalR in v1)
- **REST API:** `/api/v1/...`, API-key auth, standard *arr conventions
- **Auth:** Sonarr-style — optional forms login + API key
- **Notifications:** Plex, Jellyfin/Emby, Discord, generic webhook
- **Health checks** (disk, indexer, download-client, root-folder, yt-dlp version)
- **Backup / restore** of config + DB
- **Logging:** structured (Serilog), rolling file + stdout
- **Test discipline:** strict TDD + ~100% line coverage gate in CI (95% floor with small exclusion budget for composition / DTOs)
- **Deployment:** Docker first; bare-metal supported

### v1 explicit non-goals (deferred)

- Music-library linked mode (Lidarr / Jellyfin track → video sync)
- Additional metadata providers beyond IMVDb (interface ships, no extra impls)
- SignalR / WebSocket real-time UI (poll only in v1)
- Multi-user with roles
- Calendar / upcoming view
- Plugin host with separate-assembly extensibility
- Mobile-optimized UI / native apps
- i18n / translations
- Sponsorblock-style intro/outro trimming
- Auto-update of Vidarr itself

---

## 2. System overview

```
                ┌───────────────────────────────────────────────┐
                │                Vidarr (single process)         │
                │                                                │
  React SPA ───▶│  REST API (ASP.NET Core)                       │
  (Vite build)  │       │                                        │
                │       ▼                                        │
                │  Application Services (DI)                     │
                │   ├── Catalog        ├── Importer              │
                │   ├── Metadata       ├── Notifier              │
                │   ├── IndexerSearch  ├── Scheduler (IHosted)   │
                │   ├── DownloadClient ├── EventBus (in-proc)    │
                │   ├── Decision       ├── ChapterSplit          │
                │   ├── RuleEngine     ├── Naming                │
                │   └── Health                                   │
                │       │                                        │
                │       ▼                                        │
                │  Repositories ──▶ SQLite (EF Core, WAL)        │
                └────────────┬─────────────────────────┬─────────┘
                             │                         │
                ┌────────────▼──────┐    ┌────────────▼─────────┐
                │ yt-dlp subprocess │    │ Plex / Jellyfin /     │
                │ ffmpeg/ffprobe    │    │ Discord / webhooks    │
                │ download clients  │    └───────────────────────┘
                │ indexers (HTTP)   │
                │ metadata (HTTP)   │
                └───────────────────┘
```

- One process, one SQLite DB, one Docker image.
- All external integrations behind interfaces injected via DI; every external boundary has a fake.
- Internal event bus is used **only** for fan-out events (notifications, history, UI updates), not as a general comms substrate.
- Scheduler is `IHostedService` + `Channel<T>` queues (no Hangfire dependency) — simpler and easier to test.

---

## 3. Domain model

Names follow Sonarr / Lidarr conventions so *arr-familiar developers can navigate quickly.

### Artist

- `Id`
- External IDs per provider: `{ imvdb, musicbrainz, ... }`
- `Name`, `SortName`, `Disambiguation`, `Aliases[]`
- `Genres[]`, `Country`, `YearsActive`
- `Images[]` (poster / fanart / banner — cached locally)
- `Monitored`, `MonitorMode` (`all` | `new-only` | `none`)
- `QualityProfileId`, `RootFolderPath`, `Tags[]`
- `YouTubeChannelIds[]` (multi — main + side / VEVO + topic channels; see §4 YouTube indexer)
- `Added`, `LastInfoSync`, `LastSearch`

### MusicVideo (analogous to Sonarr Episode)

- `Id`, `ArtistId`, external IDs per provider
- `Title`, `AlternateTitles[]`, `Year`, `ReleaseDate`
- `Type`: `Official` | `Live` | `Lyric` | `Acoustic` | `Alternative` | `Cover` | `Remix`
- `Director`, `ProductionCompany`, `Runtime`
- `Genres[]`, `ThumbnailUrl`
- `Monitored`, `HasFile`, `FileId?`
- `LastSearch`

### MusicVideoFile (analogous to EpisodeFile)

- `Id`, `MusicVideoId`, `RelativePath`, `Size`, `DateAdded`
- `Quality` (canonical Quality), `MediaInfo` (resolution, codec, container, audio codec / bitrate)
- `SourceLabel` (release group / YouTube channel)
- Originating `Indexer`, `ReleaseTitle`

### Release (in-memory; from indexer search)

- `Title`, `Url` / `Magnet` / `YoutubeUrl`, `Size`, `PublishedDate`, `Age` (Usenet), `Seeders` / `Leechers` (torrent)
- Parsed `Quality`, `ReleaseGroup`, matched `ArtistId` / `MusicVideoIds[]` (1 for single, N for compilations)
- `Indexer`, `DownloadClientHint`
- `Score` (custom-format score), `RejectionReasons[]`

### Download

- `Id`, `DownloadClientId`, `DownloadClientItemId`
- `MusicVideoIds[]`, `Indexer`, `ReleaseTitle`, `Quality`
- `Status`: `Queued` | `Downloading` | `CompletedReadyToImport` | `Importing` | `Imported` | `Failed` | `Removed`
- `Progress`, `EtaSeconds`, `OutputPath`

### QualityProfile

- `Id`, `Name`
- `Items[]`: ordered allowed qualities with grouping support
- `Cutoff`: quality at which upgrades stop
- `UpgradeAllowed`
- `MinFormatScore`
- `FormatItems[]`: ordered list of `{ customFormatId, score }`

### Quality (system table)

- `Id`, `Name` (e.g. `WEBDL-2160p`, `WEBDL-1080p`, `BluRay-1080p`, `HDTV-720p`, `Unknown`)
- `Resolution`, `Source` (`WEBDL` | `BluRay` | `HDTV` | `DVD` | `Raw` | `Unknown`)

See section 8 for the seeded definitions.

### CustomFormat

- `Id`, `Name`, `IncludeCustomFormatWhenRenaming`
- `Specifications[]`: typed conditions. Each spec has `Name`, `Implementation`, `Negate`, `Required`, `Fields`
- Initial spec types:
  - `ReleaseTitleSpecification` (regex on release title)
  - `ReleaseGroupSpecification` (regex on release group)
  - `IndexerFlagSpecification` (freeleech, halfleech, etc.)
  - `SourceSpecification` (WEBDL | BluRay | HDTV | DVD | Raw)
  - `ResolutionSpecification` (480p | 720p | 1080p | 2160p)
  - `LanguageSpecification`
  - `SizeSpecification` (min / max bytes)
  - `YouTubeChannelSpecification` (YouTube indexer only — match channel ID / name, e.g. VEVO)

A CustomFormat **matches** when (all `Required` specs match) AND (all non-required specs that are not `Negate` match). Profile score = sum of `score` for each matched format in `FormatItems`.

### Indexer

- `Id`, `Name`, `Implementation` (`Newznab` | `Torznab` | `YouTube`)
- `Settings` (impl-specific JSON: URL / API key / category map for NewzNab and Torznab; channel monitoring + optional Data-API key + optional cookie path for YouTube)
- `Priority`, `EnableRss`, `EnableInteractiveSearch`, `EnableAutomaticSearch`
- `Tags[]`, preferred `DownloadClientId?`

### DownloadClient

- `Id`, `Name`, `Implementation` (`qBittorrent` | `Transmission` | `Deluge` | `SABnzbd` | `NZBGet` | `YtDlp`)
- `Settings`, `Priority`, `Tags[]`, `Enable`
- `Category`, `RemovesCompletedDownloads`

### RootFolder, Tag, BlocklistEntry

- `RootFolder`: `Id`, `Path`, `Accessible`, `FreeSpace`, `TotalSpace`
- `Tag`: `Id`, `Label`
- `BlocklistEntry`: `ArtistId`, `MusicVideoId`, `ReleaseTitle`, `Indexer`, `Reason`, `Date`

### DiscoveryRuleSet (genre / year / decade auto-add)

- `Id`, `Name`, `Enabled`
- `Conditions[]`: `Genre IN`, `Year >= / <=`, `Decade =`, `Type IN`, `Country IN`
- `Actions`: `QualityProfileId`, `RootFolder`, `Tags[]`, `MonitorMode`
- `LastRun`

### HistoryEvent

- `EventType`: `Grabbed` | `Imported` | `Upgraded` | `Failed` | `Deleted` | `Renamed` | `Ignored`
- `Date`, `ArtistId`, `MusicVideoId`, `ReleaseTitle`, `Indexer`, `DownloadClient`, `Quality`
- `Data` (JSON blob)

### Notification

- `Id`, `Name`, `Implementation` (`Plex` | `Jellyfin` | `Discord` | `Webhook`)
- `Settings`, `Tags[]`
- `SubscribedEvents`: `OnGrab` | `OnImport` | `OnUpgrade` | `OnDelete` | `OnHealthIssue` | `OnApplicationUpdate` | `OnTest`

### ApplicationConfig

- Site name, port, URL base, API key, log level, paths, hardlink toggle, etc.

---

## 4. Modules / components

Each module is its own .NET project (`Vidarr.<Module>`). Public interfaces live in a thin `Vidarr.Contracts` project. Everything DI-registered; nothing static.

### Vidarr.Host

ASP.NET Core composition root. Serves React SPA + REST API. Hosts background services. Owns DI registrations.

### Vidarr.Catalog

EF Core repositories for Artists, MusicVideos, MusicVideoFiles, Tags, RootFolders, Blocklist, History. Persistence + query primitives only — no business logic.

### Vidarr.Metadata

```csharp
interface IMetadataProvider {
    string Id { get; }                                        // "imvdb"
    Task<IReadOnlyList<ArtistSearchResult>> SearchArtists(string query, CancellationToken ct);
    Task<ArtistDetails> GetArtist(string providerId, CancellationToken ct);
    Task<IReadOnlyList<MusicVideoDetails>> GetArtistVideos(string providerId, CancellationToken ct);
    Task<MusicVideoDetails> GetVideo(string providerId, CancellationToken ct);
}
```

v1 ships `ImvdbMetadataProvider`. Cross-provider identity merging via MusicBrainz IDs when present (future-proofing the interface).

### Vidarr.Indexers

```csharp
interface IIndexer {
    int Id { get; }
    bool SupportsRss { get; }
    Task<IReadOnlyList<ReleaseInfo>> Fetch(IndexerSearchCriteria criteria, CancellationToken ct);
    Task<IReadOnlyList<ReleaseInfo>> RssSync(CancellationToken ct);
}
```

Implementations:

- `NewznabIndexer` — XML / RSS API; music-video category (6030 typical); min / max age
- `TorznabIndexer` — Newznab variant with seed / leech fields
- `YouTubeIndexer` — Hybrid:
  - YouTube Data API v3 if API key configured (search + channel uploads)
  - yt-dlp scraping fallback (`yt-dlp --dump-json` against search URLs / channel pages)
  - Channel RSS subscription for monitored artists with a known YouTube channel ID (lightweight new-upload signal)
  - **Channel ID resolution:** `Artist.YouTubeChannelIds[]` is populated from metadata-provider social links (IMVDb's site links, future MusicBrainz URL relationships), with a per-artist UI override. An artist can have multiple channels (e.g. main + VEVO + Topic). When unknown, the indexer falls back to text search (`"<artist> - <title>"`).

A `ReleaseSearchService` fans out across enabled indexers in parallel, aggregates results, passes to Decision Engine.

### Vidarr.Decision (pure logic)

Given `ReleaseInfo` + catalog state, returns `Accepted(score)` or `Rejected(reasons[])`.

- **Parser** — release title → `{ ArtistName, Title, Year, Resolution, Source, ReleaseGroup, MultiVideoHints }`. Music-video-specific parser; heavily tested with property-based tests.
- **Specs** — predicates, each one class, one test:
  - `AlreadyImportedSpec`, `BlocklistedSpec`, `MinSizeSpec`, `MaxSizeSpec`,
  - `QualityAllowedSpec`, `CustomFormatRequiredSpec`, `MinFormatScoreSpec`,
  - `IndexerEnabledSpec`,
  - `UpgradeAllowedSpec`, `CutoffMetSpec`
- **CustomFormatEngine** — evaluates Custom Format conditions against a Release; sums scores; produces format-match list.
- **Comparer** — sorts accepted releases by `(QualityRank, CustomFormatScore, ProtocolPreference, SeederTorrentOrAgeUsenetTie, IndexerPriority, Size)`.

### Vidarr.DownloadClients

```csharp
interface IDownloadClient {
    int Id { get; }
    DownloadProtocol Protocol { get; }                        // Torrent | Usenet | Streaming
    Task<DownloadClientItemId> Download(RemoteRelease release, CancellationToken ct);
    Task<IReadOnlyList<DownloadClientItem>> GetItems(CancellationToken ct);
    Task Remove(DownloadClientItemId id, bool deleteData, CancellationToken ct);
    Task TestConnection(CancellationToken ct);
}
```

Implementations: `QBittorrent`, `Transmission`, `Deluge`, `SABnzbd`, `NZBGet`, `YtDlp`. The `YtDlp` client invokes the yt-dlp binary via `IProcessRunner` and reports progress by parsing yt-dlp's stdout.

### Vidarr.Importer

Watches each download client's completed items + manual-import folder. For each completed download:

1. Inspect files via `IMediaInspector` (ffprobe).
2. If single-file with chapters & part of a multi-video release → `Vidarr.ChapterSplit` → demux per chapter (stream copy, no re-encode) → produce N files.
3. Match each file to a MusicVideo (Parser + best-match heuristics over the catalog).
4. Build `MusicVideoFile` entity; copy / move / hardlink (per config) into the artist's folder under the root folder per Naming template.
5. Emit `ImportCompleted` / `UpgradeImported` events. Unmatched files → manual-import queue.

### Vidarr.ChapterSplit

ffmpeg-based. `IMediaInspector` wraps ffprobe; `IChapterSplitter` wraps ffmpeg remux. Both fakeable for tests. Heuristics for matching chapter titles to catalog videos (fuzzy match on track-title with artist context).

### Vidarr.Naming

Token-based template engine. `INamingService.BuildFilePath(MusicVideoFile, NamingConfig) → RelativePath`. Pure function. Tokens listed in section 7.

### Vidarr.Scheduler

`IHostedService` running recurring jobs with configurable cadence; each job is triggerable on-demand via the `command` API:

- `ArtistRefreshJob` (per-artist staggered hourly)
- `RssSyncJob` (15 min default)
- `WantedVideoSearchJob` (daily)
- `RuleSetEvaluationJob` (nightly)
- `DownloadStatusPollJob` (30 s)
- `BackupJob` (weekly default)
- `HealthCheckJob` (15 min)

### Vidarr.Rules

Iterates each enabled `DiscoveryRuleSet`; walks catalog of unmonitored MusicVideos; evaluates conditions; applies actions (monitor + assign profile / root folder / tags).

### Vidarr.Notifications

```csharp
interface INotification {
    int Id { get; }
    IReadOnlySet<NotificationEventType> SupportedEvents { get; }
    Task OnGrab(GrabEvent evt, CancellationToken ct);
    Task OnImport(ImportEvent evt, CancellationToken ct);
    Task OnUpgrade(UpgradeEvent evt, CancellationToken ct);
    Task OnDelete(DeleteEvent evt, CancellationToken ct);
    Task OnHealthIssue(HealthIssueEvent evt, CancellationToken ct);
    Task OnTest(CancellationToken ct);
}
```

Implementations: `PlexNotifier`, `JellyfinNotifier`, `DiscordNotifier`, `WebhookNotifier`. Each subscribes only to the events declared in `SubscribedEvents`; failures logged + raise health issues but don't block other subscribers.

### Vidarr.EventBus

Tiny in-process dispatcher. `Publish<T>(T evt)` / `Subscribe<T>(Func<T, Task>)`. Events:

- `ReleaseGrabbed`, `DownloadCompleted`, `ImportCompleted`, `UpgradeImported`, `DownloadFailed`, `HealthIssueRaised`, `HealthIssueResolved`

### Vidarr.Health

Runs health-check specs (`DiskSpaceCheck`, `IndexerReachableCheck`, `DownloadClientReachableCheck`, `RootFolderAccessibleCheck`, `YtDlpVersionCheck`). Raises / clears `HealthIssue` events.

### Vidarr.Api

REST controllers + DTOs + validation (FluentValidation). Endpoints in section 6.

### Vidarr.Web

React + TypeScript SPA, built with Vite. Served as static files by `Vidarr.Host`. Polls REST endpoints (no SignalR in v1).

---

## 5. Key workflows

### Followed-artist refresh

Hourly, per artist, staggered:

1. `ArtistRefreshJob` picks artists due for refresh.
2. For each: `IMetadataProvider.GetArtist` + `GetArtistVideos`.
3. Diff vs catalog → add new MusicVideos; mark monitored per artist's `MonitorMode`.
4. Newly-monitored videos go on the Wanted list.

### Discovery rule evaluation

Nightly:

1. `RuleSetEvaluationJob` iterates each enabled `DiscoveryRuleSet`.
2. Walks catalog of unmonitored MusicVideos; evaluates Conditions.
3. Matching videos get monitored + assigned QualityProfile / RootFolder / Tags per Actions; Wanted list updated.

### Search & grab — RSS sync

Every 15 minutes:

1. `RssSyncJob` calls `RssSync()` on every enabled indexer.
2. Each `ReleaseInfo` → Parser → match to MusicVideo(s) (1 for single, N for compilations).
3. Each candidate → Decision Engine (specs + Custom Formats + comparer).
4. Best accepted release per MusicVideo (respecting profile cutoff / upgrade rules) → `IDownloadClient.Download()`.
5. Emit `ReleaseGrabbed`; write History row.

### Search & grab — Wanted

Scheduled + on-demand: targeted `Fetch(criteria)` across indexers for each Wanted MusicVideo; same Decision + grab path.

### Manual interactive search

1. User invokes search for a specific video; API runs parallel `Fetch`.
2. UI shows all results with rejection reasons / scores; user picks one.
3. Chosen release → `Download` (bypasses Decision acceptance but routes through normal download flow).

### Download status polling

Every 30 seconds: `DownloadStatusPollJob` polls each enabled `IDownloadClient.GetItems()`; updates `Download.Status` / progress; on `CompletedReadyToImport` → enqueue `Importer.Process()`.

### Import

1. Inspect files via `IMediaInspector`.
2. Multi-file → match each file to a MusicVideo. Single-file w/ chapters → ChapterSplit → N files → match.
3. For each matched file: build `MusicVideoFile`, run `INamingService` → destination path under RootFolder.
4. Perform file op (hardlink / copy / move per config).
5. Persist `MusicVideoFile`; mark MusicVideo `HasFile`; mark Download `Imported`; History event; publish `ImportCompleted` or `UpgradeImported`.
6. Notifications subscribe → Plex / Jellyfin refresh; Discord / webhook posted.
7. Unmatched files → manual-import queue.

### Notification fan-out

Notifications subscribe to events they care about. Dispatcher invokes subscribers; failures are logged + raise health issues but don't block other subscribers. Each notification's `OnX` is a separate testable method.

### Health checks

Run on schedule + on relevant config changes. Active issues exposed via `/health`. Issues raise `HealthIssueRaised` events (notifiers can subscribe).

---

## 6. REST API surface

Standard *arr conventions. `/api/v1/...`, `X-Api-Key` header (or `?apikey=`), pagination via `page`, `pageSize`, `sortKey`, `sortDirection`.

Resources (CRUD where applicable):

- `artist` — list / get / add / update / delete; `lookup?term=` for metadata search; `refresh/{id}` command
- `musicvideo` — list (filter by artist, monitored, hasFile), get, monitor toggle
- `musicvideofile` — list, delete, get media-info
- `release` — `?artistId=&musicVideoId=` interactive search; `POST /release` to grab a chosen release
- `queue` — list active downloads; `DELETE /queue/{id}?blocklist=true`; `POST /queue/grab` manual force-grab
- `history` — list with filters; mark-failed
- `blocklist` — list, add, remove
- `wanted/missing`, `wanted/cutoff` — paginated lists
- `qualityprofile`, `customformat`, `qualitydefinition` — full CRUD
- `indexer` — list / get / add / update / delete; `test`; `schema` returns supported implementations and their settings shape
- `downloadclient` — same shape as indexer
- `notification` — same shape as indexer
- `rootfolder` — list / add / delete; `freespace`
- `tag` — list / add / delete; details endpoint shows usages
- `discoveryrule` — full CRUD; `evaluate/{id}` command
- `command` — `POST` to trigger background jobs (refresh, RssSync, etc.) — Sonarr-style command queue
- `health` — current health issues
- `system/status`, `system/backup` (list / create / restore), `system/log`, `config/host`, `config/naming`, `config/mediamanagement`

DTOs versioned per endpoint. Validation via FluentValidation. Errors follow *arr JSON shape: `{ errors: [{ propertyName, errorMessage }] }`.

---

## 7. File layout & naming

Default layout (configurable templates):

```
<RootFolder>/
  <Artist Name>/
    <Artist Name> - <Title> (<Year>) [<Quality Full>][<Source>].<ext>
```

Token registry (initial set):

- **Artist:** `{Artist Name}`, `{Artist NameThe}`, `{Artist Disambiguation}`, `{Artist Country}`
- **Video:** `{Title}`, `{Title CleanTitle}`, `{Year}`, `{Type}`, `{Director}`
- **Media:** `{Quality Full}`, `{Quality Title}`, `{MediaInfo Simple}`, `{MediaInfo Full}`, `{MediaInfo VideoCodec}`, `{MediaInfo AudioCodec}`, `{MediaInfo Resolution}`
- **Release:** `{Release Group}`, `{Source}`, `{Indexer}`
- **Custom:** `{Custom Formats}`

File operations on import: `Move` (default) | `Copy` | `Hardlink with fallback to copy`. Cross-FS hardlinks degrade to copy with a health-issue warning.

Existing-library import tool: scans a chosen folder, parses, suggests matches, user confirms; produces `MusicVideoFile` entries pointing at existing files without moving them.

---

## 8. Quality definitions

System-seeded; user-extensible.

| Id  | Name          | Resolution | Source  |
| --- | ------------- | ---------- | ------- |
| 1   | Unknown       | —          | Unknown |
| 2   | WEBDL-480p    | 480p       | WEBDL   |
| 3   | WEBDL-720p    | 720p       | WEBDL   |
| 4   | WEBDL-1080p   | 1080p      | WEBDL   |
| 5   | WEBDL-2160p   | 2160p      | WEBDL   |
| 6   | HDTV-720p     | 720p       | HDTV    |
| 7   | HDTV-1080p    | 1080p      | HDTV    |
| 8   | DVD           | 480p       | DVD     |
| 9   | BluRay-720p   | 720p       | BluRay  |
| 10  | BluRay-1080p  | 1080p      | BluRay  |
| 11  | BluRay-2160p  | 2160p      | BluRay  |
| 12  | Raw-HD        | —          | Raw     |

YouTube uploads map deterministically onto WEBDL qualities by yt-dlp's reported max format height: `≥ 2160` → `WEBDL-2160p`, `1080…2159` → `WEBDL-1080p`, `720…1079` → `WEBDL-720p`, `< 720` → `WEBDL-480p`. Codec is informational (`MediaInfo VideoCodec` token) but does not influence quality ranking.

Profile decision priority: `(QualityRank ASC by profile order, CustomFormatScore DESC, ProtocolPreference, SeederTorrentOrAgeUsenetTie, IndexerPriority, Size)`.

---

## 9. Testing approach

**Strict TDD + ~100% line coverage gate in CI** (95% floor, small `[ExcludeFromCodeCoverage]` budget for `Program.cs` composition + plain DTOs).

### Tooling

- xUnit + FluentAssertions + NSubstitute (mocks) + Coverlet (coverage) + ReportGenerator
- FsCheck or CsCheck for property-based tests (Parser, Naming engine)
- testcontainers-dotnet for contract-test fixtures (qBittorrent, Transmission, Deluge, SABnzbd, NZBGet have official images)

### Test layers

- **Unit** — pure logic: Parser, Decision specs, Custom Format engine, Comparer, Naming token engine, Rule conditions, Quality comparisons. No I/O. Fast.
- **Module** — services with immediate deps faked. E.g. `ImporterTests` with fake `IMediaInspector`, `IChapterSplitter`, `IFileSystem`, `ICatalog`.
- **Integration** — real EF Core + SQLite (in-memory or temp file), real `WebApplicationFactory` for HTTP. External services (yt-dlp, indexer HTTP, download-client HTTP) still faked.
- **Contract** — for each `IDownloadClient` / `IIndexer` / `INotification` implementation: contract tests against recorded fixtures or docker-compose instances in CI.

### Boundaries

Every external surface is behind an interface:

- `IHttpClient`, `IFileSystem`, `IProcessRunner`, `ISystemClock`, `IRandom`, `IEnvironment`

Tests use deterministic fakes. CI matrix runs unit + module + integration on every PR; contract tests on nightly + on release branches.

---

## 10. Deployment & ops

### Docker first

Multi-stage Dockerfile. Final image based on `mcr.microsoft.com/dotnet/aspnet:8.0` + ffmpeg + yt-dlp + the published app. Compose example with three volumes: config, library, downloads.

### Bare metal

`dotnet publish` self-contained binary; system needs ffmpeg + yt-dlp on PATH.

### Storage

SQLite database at `<config>/vidarr.db` (WAL mode). Backups dump DB + config to a timestamped zip in `<config>/backups/` with configurable rotation.

### Config

Env vars override `config.json`: `VIDARR_PORT`, `VIDARR_URL_BASE`, `VIDARR_API_KEY`, `VIDARR_LOG_LEVEL`, …. API key auto-generated on first run if not set.

### Logging

Serilog → rolling file + stdout (Docker-friendly). Per-module log level.

### yt-dlp updater

Scheduled job checks for a newer yt-dlp; downloads + atomically swaps the binary. Disabled by default; opt-in toggle. Docker: separate `yt-dlp` volume so updates persist across image rebuilds.

### CI

GitHub Actions: lint (`dotnet format`), build, test, coverage gate, Docker image build on tag.

---

## 11. Explicit deferrals (post-v1)

- Music-library linked mode (Lidarr / Jellyfin track → video sync)
- Additional metadata providers (MusicBrainz, TheAudioDB) — interface ships v1
- SignalR real-time UI
- Multi-user with roles
- Calendar / upcoming view
- Plugin host with separate-assembly extensibility
- Mobile / native apps
- i18n / translations
- Additional indexer types beyond NewzNab + Torznab + YouTube
- Sponsorblock-style intro / outro trimming
- Auto-update of Vidarr itself

---

## 12. Open risks

- **yt-dlp brittleness:** YouTube changes break yt-dlp regularly. Hybrid indexer + opt-in auto-update mitigates; CI smoke-test against a known-public-domain channel.
- **YouTube Data API quota:** 10k units/day default. A heavy library can blow through this quickly. Falling back to yt-dlp scraping is the mitigation; design must make the fallback path real, not aspirational.
- **Music-video metadata coverage:** IMVDb is the best public source but coverage is patchy (older artists, non-Western artists). v1 ships with IMVDb only; this will produce real gaps.
- **Chapter-split matching:** matching chapter titles to catalog music videos is fuzzy. Heuristic-based matcher will misfire; UI must support manual override per chapter.
- **Coverage gate vs. velocity:** 95% line coverage on a system with this much external integration is real work. Contract tests with docker-compose'd clients are slow. CI design must keep the fast loop fast (unit + module on every push; integration on PR; contract nightly).
- **Custom Format scoring edge cases:** Sonarr has shipped many subtle fixes to its scoring engine over the years. Expect to inherit some of that work.

---

*End of design.*
