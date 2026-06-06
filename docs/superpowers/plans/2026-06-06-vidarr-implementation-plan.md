# Vidarr v1 — Phased Implementation Plan

**Spec:** [`docs/superpowers/specs/2026-06-06-vidarr-design.md`](../specs/2026-06-06-vidarr-design.md)
**Stack:** .NET 10 (LTS) / C# / ASP.NET Core, EF Core 10 (SQLite, WAL), React 18 + TS (Vite), Serilog, xUnit
**Doctrine:** strict TDD, ~100% line coverage (95% floor), *arr-stack architecture, every external boundary behind an interface
**Date:** 2026-06-06

> Confirmed against spec: Release Profiles are NOT a separate entity (spec §1: "Custom Formats subsume what older *arr versions split between Release Profiles and Custom Formats"). No Release Profile work appears in any phase below.

---

## Solution layout (final state, established incrementally)

```
/Vidarr.sln
/src/
  Vidarr.Contracts/           (interfaces, DTOs shared across modules, events)
  Vidarr.Catalog/             (EF Core DbContext, repositories, migrations)
  Vidarr.Metadata/            (IMetadataProvider + ImvdbMetadataProvider)
  Vidarr.Indexers/            (IIndexer + Newznab/Torznab/YouTube)
  Vidarr.Decision/            (Parser, Specs, CustomFormatEngine, Comparer)
  Vidarr.DownloadClients/     (IDownloadClient + 6 impls)
  Vidarr.Importer/            (Import pipeline)
  Vidarr.ChapterSplit/        (IMediaInspector, IChapterSplitter)
  Vidarr.Naming/              (INamingService, token engine)
  Vidarr.Scheduler/           (IHostedService jobs + Channel<T> queues)
  Vidarr.Rules/               (DiscoveryRuleSet engine)
  Vidarr.Notifications/       (INotification + 4 impls)
  Vidarr.EventBus/            (in-proc Publish/Subscribe)
  Vidarr.Health/              (health check specs)
  Vidarr.Api/                 (controllers, FluentValidation)
  Vidarr.Web/                 (React+TS SPA built by Vite)
  Vidarr.Host/                (composition root, Program.cs, DI, hosts SPA)
  Vidarr.Infrastructure/      (IHttpClient, IFileSystem, IProcessRunner, ISystemClock, IRandom, IEnvironment defaults)
/tests/
  Vidarr.<Module>.Tests/      (one per src module)
  Vidarr.Tests.Common/        (shared fakes: FakeFileSystem, FakeHttpClient, FakeProcessRunner, FakeClock)
  Vidarr.IntegrationTests/    (WebApplicationFactory, EF Core real SQLite)
  Vidarr.ContractTests/       (Testcontainers-backed)
  Vidarr.PropertyTests/       (FsCheck — Parser, Naming)
/docker/
/build/                       (coverlet.runsettings, ReportGenerator config)
.github/workflows/
```

**Boundary-interface rule (apply in every phase):**
`System.Net.Http.HttpClient`, `System.IO.File*`, `System.Diagnostics.Process`, `DateTime.UtcNow`, `Random`, `Environment.*` are NEVER referenced from production code outside `Vidarr.Infrastructure`. All consumers depend on `IHttpClient`, `IFileSystem`, `IProcessRunner`, `ISystemClock`, `IRandom`, `IEnvironment`. CI grep gate enforces this.

---

## Phase 0 — Documentation Discovery & Allowed-APIs cheat sheet

**Scope (no code yet):**
- Establish the doc-sources implementers must consult and the recurring API cheat sheet that every later phase references.
- Stand up no projects; produce no commits beyond `README.md`, this plan, and any spec annotations.

**End-of-phase deliverable:**
An implementer (or Claude session) starting any later phase can answer "where do I look first" without guessing.

**Allowed APIs / docs to read first (recurring — every later phase implicitly cites this):**

| Concern | Authoritative source (read before writing code) |
|---|---|
| ASP.NET Core 10 minimal APIs, IHostedService, hosting model | https://learn.microsoft.com/aspnet/core/fundamentals/?view=aspnetcore-10.0 |
| ASP.NET Core static files / SPA hosting | https://learn.microsoft.com/aspnet/core/fundamentals/static-files |
| `System.Threading.Channels` (`Channel<T>`) | https://learn.microsoft.com/dotnet/core/extensions/channels |
| `System.Text.Json` source-gen + options | https://learn.microsoft.com/dotnet/standard/serialization/system-text-json |
| EF Core 10 SQLite provider | https://learn.microsoft.com/ef/core/providers/sqlite/ |
| EF Core 10 migrations | https://learn.microsoft.com/ef/core/managing-schemas/migrations/ |
| EF Core 10 Owned types & value conversions | https://learn.microsoft.com/ef/core/modeling/owned-entities + https://learn.microsoft.com/ef/core/modeling/value-conversions |
| xUnit | https://xunit.net/docs/getting-started/v2/netcore/cmdline |
| FluentAssertions | https://fluentassertions.com/introduction |
| NSubstitute | https://nsubstitute.github.io/help/getting-started/ |
| Coverlet + runsettings + threshold | https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/MSBuildIntegration.md |
| ReportGenerator | https://github.com/danielpalme/ReportGenerator |
| FsCheck (property-based) | https://fscheck.github.io/FsCheck/ |
| FluentValidation w/ ASP.NET Core | https://docs.fluentvalidation.net/en/latest/aspnet.html |
| Serilog + sinks (Console, File) | https://github.com/serilog/serilog/wiki + https://github.com/serilog/serilog-sinks-file |
| Vite + React + TS | https://vitejs.dev/guide/ + https://react.dev/learn + https://www.typescriptlang.org/docs/ |
| TanStack Query (suggested poll lib) | https://tanstack.com/query/latest/docs/framework/react/overview |
| IMVDb API | https://imvdb.com/developers/api |
| NewzNab API spec | https://newznab.readthedocs.io/en/latest/misc/api/ |
| Torznab spec (Jackett docs) | https://github.com/Jackett/Jackett/wiki/Migration-Notes#torznab |
| YouTube Data API v3 | https://developers.google.com/youtube/v3/docs |
| YouTube channel RSS | `https://www.youtube.com/feeds/videos.xml?channel_id=<UC...>` |
| yt-dlp `--dump-json` + flags | https://github.com/yt-dlp/yt-dlp#usage-and-options |
| qBittorrent WebUI API v2 | https://github.com/qbittorrent/qBittorrent/wiki/WebUI-API-(qBittorrent-4.1) |
| Transmission RPC | https://github.com/transmission/transmission/blob/main/docs/rpc-spec.md |
| Deluge JSON-RPC (WebUI) | https://deluge.readthedocs.io/en/latest/reference/api.html |
| SABnzbd API | https://sabnzbd.org/wiki/configuration/4.3/api |
| NZBGet JSON-RPC | https://nzbget.com/documentation/api/ |
| Plex library refresh | https://www.plexopedia.com/plex-media-server/api/library/refresh-library/ (PUT `/library/sections/{id}/refresh`) |
| Jellyfin/Emby library scan | https://api.jellyfin.org/ (POST `/Library/Refresh`) |
| Discord webhooks | https://discord.com/developers/docs/resources/webhook |
| ffmpeg chapters + stream copy | https://ffmpeg.org/ffmpeg.html (`-map_chapters`, `-c copy`, `-ss/-to`) |
| ffprobe JSON output | https://ffmpeg.org/ffprobe.html (`-show_format -show_streams -show_chapters -of json`) |
| Testcontainers for .NET | https://dotnet.testcontainers.org/ |

**Anti-patterns to avoid:**
- Quoting API surface from memory. Every later phase MUST open the linked doc before writing code that touches that surface.
- Adopting third-party "helper" libraries (e.g. an existing qBittorrent .NET wrapper) — the spec demands native impls behind `IHttpClient`.

**Dependencies:** none.
**Relative size:** S.

---

## Phase 1 — Walking Skeleton (vertical end-to-end slice)

ONE of everything: IMVDb metadata, YouTube indexer (yt-dlp scrape path only — no API key path, no RSS), YtDlp download client, Webhook notification. Minimum viable Catalog + Decision + Importer + Naming wired to a tiny REST API and a placeholder React shell. **Full TDD + coverage gate live from this phase.**

### Scope — projects & files produced

- **Solution & build infra**
  - `Vidarr.sln`
  - `Directory.Build.props` (TreatWarningsAsErrors, Nullable enable, ImplicitUsings, LangVersion latest)
  - `Directory.Packages.props` (central package management)
  - `build/coverlet.runsettings` — `Threshold=95`, `ThresholdType=line`, `ThresholdStat=total`
  - `.editorconfig`
  - `.github/workflows/ci.yml` (lint, build, test, coverage gate; tag-Docker is later)
- **Production projects (slim slices):**
  - `Vidarr.Contracts`
    - Boundary interfaces: `IHttpClient`, `IFileSystem`, `IProcessRunner`, `ISystemClock`, `IRandom`, `IEnvironment`
    - Domain interfaces: `IMetadataProvider`, `IIndexer`, `IDownloadClient`, `INotification`, `IEventBus`
    - Shared records: `ArtistSearchResult`, `ArtistDetails`, `MusicVideoDetails`, `ReleaseInfo`, `IndexerSearchCriteria`, `RemoteRelease`, `DownloadClientItem`, `DownloadClientItemId`, `GrabEvent`, `ImportEvent`
    - Enums: `DownloadProtocol`, `MonitorMode`, `NotificationEventType`, `Source`, `Resolution`
  - `Vidarr.Infrastructure`
    - Concrete `HttpClientAdapter` (wraps `System.Net.Http.HttpClient` via `IHttpClientFactory`), `FileSystem`, `ProcessRunner`, `SystemClock`, `RandomAdapter`, `EnvironmentAdapter`
  - `Vidarr.Catalog`
    - `VidarrDbContext` (SQLite)
    - Entities: `Artist`, `MusicVideo`, `MusicVideoFile`, `Quality` (system seed), `RootFolder`
    - Repositories: `IArtistRepository`, `IMusicVideoRepository`, `IMusicVideoFileRepository`, `IRootFolderRepository`
    - First EF migration `InitialCreate`
  - `Vidarr.Metadata` — `ImvdbMetadataProvider` (search, get artist, get videos, get single video) via `IHttpClient`
  - `Vidarr.Indexers` — `YouTubeIndexer` with yt-dlp scrape path only (`Fetch` calls `yt-dlp --dump-json "ytsearch10:<query>"` via `IProcessRunner`)
  - `Vidarr.Decision` — minimal Parser (regex pipeline) + minimum specs: `AlreadyImportedSpec`, `QualityAllowedSpec`, `MinSizeSpec`, `MaxSizeSpec`; trivial Comparer ordered by (QualityRank, Size). CustomFormat engine stub returns score=0 (real engine arrives Phase 8).
  - `Vidarr.DownloadClients` — `YtDlpDownloadClient` (subprocess; progress parsed from stdout)
  - `Vidarr.Naming` — `INamingService` with the default template and the Phase-1 token subset: `{Artist Name}`, `{Title}`, `{Year}`, `{Quality Full}`
  - `Vidarr.Importer` — minimal path: yt-dlp produces one file → parser → match → build `MusicVideoFile` → naming → move via `IFileSystem` → mark `HasFile`
  - `Vidarr.EventBus` — in-proc `EventBus` (`Channel<T>` per type or simple `Subscribe`/`Publish`)
  - `Vidarr.Notifications` — `WebhookNotifier` (POST JSON)
  - `Vidarr.Scheduler` — `IHostedService` + a single `DownloadStatusPollJob` so we can observe yt-dlp progress to completion
  - `Vidarr.Api` — minimal endpoints (versioned `/api/v1`, `X-Api-Key` auth middleware):
    - `POST /api/v1/artist/lookup`, `POST /api/v1/artist` (add by IMVDb id), `GET /api/v1/artist/{id}`
    - `POST /api/v1/command` with single command `ArtistSearch` (kicks YouTube search → decision → grab)
    - `GET /api/v1/queue`
    - `GET /api/v1/musicvideo?artistId=`
  - `Vidarr.Web` — Vite + React + TS shell with two pages: "Add Artist" (search box → list → add) and "Library" (artist list + videos). Polls every 5 s via TanStack Query.
  - `Vidarr.Host` — composition root; serves SPA; registers DI; Serilog console+rolling-file; reads `appsettings.json` + env vars
- **Test projects** (one per src project, plus shared):
  - `Vidarr.Tests.Common` — `FakeHttpClient`, `FakeFileSystem`, `FakeProcessRunner` (records invocations, returns canned stdout/exit), `FakeClock`, `FakeRandom`, `FakeEnvironment`, EF-Core SQLite test fixture helper
  - `Vidarr.PropertyTests` — first FsCheck suite for Parser (round-trip + invariant properties) and Naming (token substitution invariants)
  - `Vidarr.IntegrationTests` — `WebApplicationFactory<Program>` with all external impls swapped for fakes; an end-to-end test for the slice below

### End-of-phase deliverable (user-observable)

From a clean checkout, `docker run` (or `dotnet run`) the app, browse to the SPA, search for an artist (IMVDb is hit through `FakeHttpClient` in tests; real in dev), add the artist, click "Search" on a video → yt-dlp subprocess (fake in tests, real on host in dev) downloads → file is named per template, placed under `<RootFolder>/<Artist>/...mkv` → webhook POST fires with the import payload. Database state and history reflect grab + import. **Coverage gate at 95% passes in CI.**

### Tests required

- **Unit (every concrete class):**
  - Parser: ~30 explicit cases + FsCheck properties (idempotence on parse-then-stringify of canonical components; invariance to leading/trailing whitespace; resolution/source tokens recognized in any order)
  - Naming: FsCheck properties — output never contains template braces, never contains illegal filename characters, deterministic given same input, never empty
  - Each Spec: 2-cell truth (pass / reject) min
  - Comparer: ordered-by tests
  - WebhookNotifier: posts correct JSON, handles non-2xx as logged failure
- **Module:**
  - `ImvdbMetadataProvider` against `FakeHttpClient` with recorded IMVDb fixtures
  - `YouTubeIndexer` against `FakeProcessRunner` with recorded `yt-dlp --dump-json` fixtures
  - `YtDlpDownloadClient` against `FakeProcessRunner` (progress parsing, exit codes)
  - `Importer` with `FakeFileSystem` + fake catalog
  - `WantedSearchService` / grab flow with all deps faked
- **Integration:**
  - `WebApplicationFactory` end-to-end: POST add artist → command search → poll queue → assert file placed via `FakeFileSystem` → assert webhook captured by fake → assert `HasFile=true` and history row
  - Real EF Core SQLite (temp file) — migrations apply cleanly
- **Coverage:**
  - Threshold=95% enforced in CI via `coverlet.runsettings`. `[ExcludeFromCodeCoverage]` allowed only on `Program.cs`, generated `*Migration*.cs`, plain DTO records (justified file by file).

### Allowed APIs / docs to read first

- ASP.NET Core minimal APIs, `IHostedService`, Static-files + SPA fallback (Phase 0 links)
- EF Core SQLite + migrations + value conversions for `Quality` and JSON columns
- `System.Threading.Channels`
- xUnit, FluentAssertions, NSubstitute, Coverlet, ReportGenerator
- FluentValidation (basic)
- Serilog Console + File sinks
- IMVDb API (only the endpoints used: `search/entities`, `artists/<id>`, `artists/<id>/videos`, `videos/<id>`)
- yt-dlp `--dump-json`, `--no-warnings`, `--ignore-config`, `-f`, `--newline`, `--print-to-file`
- Discord webhook payload format (used by `WebhookNotifier` smoke)
- Vite + React 18 + TS, TanStack Query polling

### Anti-patterns to avoid

- Adding `System.Net.Http.HttpClient`, `System.IO.File`, `System.Diagnostics.Process`, `DateTime.UtcNow`, `Random`, `Environment.*` references anywhere outside `Vidarr.Infrastructure`. Use the interfaces.
- Skipping the property-based tests for Parser/Naming — they must land in Phase 1 because they prevent entire bug classes from accruing through Phases 2–10.
- Inventing EF Core 10 APIs from memory — open the docs, especially for JSON column mapping and Owned types decisions.
- Coverage gate "to be added later" — coverlet runsettings and CI threshold land in this phase or never.
- Writing the React SPA against `fetch` directly without a typed API client — generate or hand-author TS types from `Vidarr.Contracts` DTOs.
- Letting yt-dlp's variable stdout format leak into multiple consumers — wrap in a `YtDlpProgressParser` that `YtDlpDownloadClient` and tests share.
- Returning live `HttpClient` from a `IHttpClientFactory` in production code — wrap in the `IHttpClient` adapter.

### Dependencies

- Requires Phase 0 only.
- Blocks every subsequent phase (the architectural mold is set here).

**Relative size:** XL.

---

## Phase 2 — Catalog completion, Quality model, RootFolders, Tags, Blocklist, History

### Scope

- `Vidarr.Catalog` adds: `Tag`, `BlocklistEntry`, `HistoryEvent`, `DownloadClient` (entity), `Indexer` (entity), `Notification` (entity), `QualityProfile` (entity, including `FormatItems[]`, `MinFormatScore`), `CustomFormat` (entity, including `Specifications[]`), `DiscoveryRuleSet` (entity), `ApplicationConfig`
- Migrations: `AddQualityProfiles`, `AddCustomFormats`, `AddTagsAndBlocklist`, `AddHistory`, `AddDiscoveryRuleSets`, `AddConfig`
- Quality seeding (12 system qualities per spec §8) via `IDataSeeder` invoked at startup
- Repositories for every new entity
- `Vidarr.Api` extends with full CRUD for `qualityprofile`, `customformat` (definition only — engine still stubbed), `rootfolder`, `tag`, `blocklist`, `history`, `config/host`, `config/naming`, `config/mediamanagement`
- React: Settings → Media Management, Profiles, Root Folders, Tags pages

### End-of-phase deliverable

User can manage Quality Profiles, Custom Format definitions (no scoring yet), Root Folders, Tags, view History and Blocklist, and configure naming/media-management — all through the SPA and REST.

### Tests required

- Module: repositories CRUD; quality seed idempotency; JSON column round-trip for `Specifications`, `FormatItems`, `Conditions`
- Integration: full REST CRUD round-trip per resource via `WebApplicationFactory`
- FluentValidation: error-shape contract test (matches *arr `{ errors: [...] }` shape)
- Coverage: 95% maintained

### Allowed APIs / docs to read first

- EF Core JSON columns (SQLite) and value converters
- FluentValidation rule sets
- *arr JSON error shape (Sonarr v4 source reference for parity)

### Anti-patterns to avoid

- Storing `FormatItems` / `Specifications` as separate tables (over-modelling); JSON column is fine and matches Sonarr's approach
- Returning EF entities from controllers — DTOs in `Vidarr.Contracts` only
- Mutating seeded `Quality` rows from migrations (treat seed as runtime, not DDL)

### Dependencies

- Requires Phase 1.

**Relative size:** L.

---

## Phase 3 — NewzNab & Torznab indexers + ReleaseSearchService

### Scope

- `Vidarr.Indexers`: `NewznabIndexer` (XML/RSS, music-video category 6030 default but configurable, min/max age), `TorznabIndexer` (extends Newznab with `seeders`, `peers` torznab attrs)
- `ReleaseSearchService` — fans out across enabled indexers in parallel (TPL `Task.WhenAll` with per-indexer timeout and `CancellationToken`), aggregates results, dedupes by GUID+title
- API: `GET /api/v1/release?artistId=&musicVideoId=` (interactive search), `POST /api/v1/release` (grab — Phase 4 wires the grab path properly for non-YouTube)
- `/api/v1/indexer` CRUD + `test` + `schema`
- React: Settings → Indexers (add Newznab/Torznab; test connection)

### End-of-phase deliverable

Through the SPA, user adds a Newznab/Torznab indexer, runs an interactive search on a wanted video, sees the merged result set with parsed quality and rejection reasons (decision engine already exists from Phase 1 — augmented with `IndexerEnabledSpec`).

### Tests required

- Unit: NewznabIndexer/TorznabIndexer parse correctness against recorded XML fixtures (Jackett-style and raw Newznab samples; include malformed payload cases)
- Module: `ReleaseSearchService` with multiple fake indexers, asserting parallelism, timeout, partial-failure behavior
- Contract (lightweight here — full nightly later): one mocked HTTP fixture per protocol
- Property: Parser augmented with NewzNab-typical title patterns (release-group, source, resolution tokens)

### Allowed APIs / docs to read first

- NewzNab API spec + category numbers
- Torznab Jackett wiki (especially the extra torznab attrs)
- HttpClient timeouts via `IHttpClient`

### Anti-patterns to avoid

- Using `XmlSerializer` reflection magic — prefer `XDocument`/`XmlReader` for fixture-friendly tests
- Letting indexer-specific failure types crash the whole fan-out — each indexer's failure is an isolated `Result`

### Dependencies

- Requires Phase 1, 2.
- Can run in parallel with Phase 4 if you had help.

**Relative size:** M.

---

## Phase 4 — Download clients: qBittorrent, Transmission, Deluge

### Scope

- `QBittorrent`, `Transmission`, `Deluge` implementations of `IDownloadClient`
- Each implementation uses `IHttpClient`; auth/session handling (qBit cookie auth; Deluge JSON-RPC session; Transmission `X-Transmission-Session-Id` 409 dance)
- API: `/api/v1/downloadclient` CRUD + `test` + `schema`
- `Vidarr.Scheduler.DownloadStatusPollJob` extended to multi-client polling; routes new completed items into `Importer`
- React: Settings → Download Clients

### End-of-phase deliverable

User adds a qBittorrent (or Transmission, or Deluge) client, runs interactive search on a Newznab/Torznab indexer (Phase 3), grabs a torrent, watches it download, sees it imported.

### Tests required

- Module: each client against `FakeHttpClient` with recorded HTTP fixtures covering: add torrent (magnet + .torrent file), session/auth refresh, list, remove (with and without data), status mapping
- Contract: Testcontainers-backed nightly tests against official qBittorrent / Transmission / Deluge images (one test per protocol primitive)
- Edge cases: Transmission 409 session re-auth; qBit pre-2.x not supported (assert error); Deluge auth expiry

### Allowed APIs / docs to read first

- qBittorrent WebUI API v2 (specifically `/api/v2/auth/login`, `/api/v2/torrents/add`, `/info`, `/delete`)
- Transmission RPC (note `tag` IDs and the session-id header protocol)
- Deluge JSON-RPC (Web UI auth, `core.add_torrent_magnet`, `core.get_torrents_status`)
- Testcontainers .NET — `IAsyncLifetime` patterns

### Anti-patterns to avoid

- Holding session state in static fields — store per-instance, refresh on 401/409
- Using `HttpClient` directly — use `IHttpClient` with cookie support
- Mapping torrent state strings from memory — use the documented enums

### Dependencies

- Requires Phase 1, 2, 3.
- Sub-phases (`qBit` / `Transmission` / `Deluge`) parallelizable with help.

**Relative size:** L.

---

## Phase 5 — Usenet clients: SABnzbd & NZBGet

### Scope

- `SABnzbd`, `NZBGet` implementations of `IDownloadClient`
- Usenet-specific `DownloadProtocol.Usenet` handling in `ReleaseSearchService` & decision (`ProtocolPreference` is already in the comparer — wire actual logic)
- Comparer breaks ties for Usenet by `Age`, for Torrent by `Seeders`
- React: settings handle Usenet-specific fields (category, priority)

### End-of-phase deliverable

User adds SABnzbd or NZBGet, grabs an NZB via Newznab → import.

### Tests required

- Module: each client against `FakeHttpClient` fixtures (SAB `mode=addurl`/`addfile`/`queue`/`history`; NZBGet JSON-RPC `append`, `listgroups`, `editqueue`)
- Contract: Testcontainers nightly (`linuxserver/sabnzbd`, `linuxserver/nzbget`)

### Allowed APIs / docs to read first

- SABnzbd API (note multipart vs URL-add; API-key vs NZB-key)
- NZBGet JSON-RPC

### Anti-patterns to avoid

- Forgetting the `mode=` query-string idiom in SAB
- Confusing NZBGet's category vs script vs priority fields

### Dependencies

- Requires Phase 1, 2, 3, 4 (for `Importer` Usenet path).
- Parallelizable with Phase 6.

**Relative size:** M.

---

## Phase 6 — YouTube indexer: full hybrid (Data API + RSS + channel resolution)

### Scope

- Augment `YouTubeIndexer` with:
  - YouTube Data API v3 path (search.list, search by channelId, videos.list for hi-res metadata)
  - Quota-aware switching: when configured, prefer API; on 403/`quotaExceeded`, downgrade to yt-dlp scrape; emit health issue
  - Channel RSS subscription path: `RssSync()` reads `https://www.youtube.com/feeds/videos.xml?channel_id=<UC...>` for each monitored artist's `YouTubeChannelIds[]`
  - Channel ID resolution: populate `Artist.YouTubeChannelIds[]` from IMVDb social links (Phase 1's `ImvdbMetadataProvider` extended); UI override per-artist; fallback to text search
- React: Artist page → "YouTube Channels" multi-input
- Quality mapping per spec §8 (height → WEBDL-Xp) lives in a dedicated `IYouTubeQualityMapper`

### End-of-phase deliverable

User configures a Data API key (optional). For monitored artists with known channel IDs, RSS sync surfaces new uploads within 15 minutes; for unknown artists, text-search fallback works; if quota exhausted, scrape path takes over and a health issue is raised.

### Tests required

- Unit: `IYouTubeQualityMapper` (boundary cases at 720/1080/2160)
- Module: API-vs-scrape switching on `FakeHttpClient` returning quota-exceeded; RSS parsing fixtures (real-world feeds, with and without `media:group`)
- Module: channel-ID resolution from a recorded IMVDb fixture with social links

### Allowed APIs / docs to read first

- YouTube Data API v3 (specifically `search.list` quota cost — note 100 units per call; `videos.list` for `contentDetails.duration`)
- YouTube channel RSS feed format (`media:group`, `yt:videoId`, `published`)
- IMVDb social-link shape in artist payload

### Anti-patterns to avoid

- Treating yt-dlp scrape and Data API paths as separate indexers — they're one indexer with two strategies
- Spamming Data API without quota accounting — count expected units per call, log running total

### Dependencies

- Requires Phase 1.
- Parallelizable with Phases 3–5.

**Relative size:** L.

---

## Phase 7 — Scheduler full job suite + EventBus completion

### Scope

- All scheduled jobs per spec §4 / §5:
  - `ArtistRefreshJob` (staggered hourly per artist)
  - `RssSyncJob` (15 min default)
  - `WantedVideoSearchJob` (daily)
  - `RuleSetEvaluationJob` (nightly — engine lands Phase 9)
  - `DownloadStatusPollJob` (30 s; extended from Phase 1/4)
  - `BackupJob` (weekly default — handler lands Phase 13)
  - `HealthCheckJob` (15 min — checks land Phase 12)
- `Vidarr.Scheduler` exposes a command queue (`Channel<ICommand>`); REST `POST /api/v1/command` enqueues; each job is also triggerable on-demand by command
- `Vidarr.EventBus` formalized: typed Publish/Subscribe, exception isolation, structured logging of every event
- React: Activity → Queue, Activity → History pages polished; "Run now" buttons for each command

### End-of-phase deliverable

Hourly artist refresh discovers new IMVDb videos and adds them to Wanted; RSS sync grabs them automatically via configured indexers + clients; user can trigger any job from the UI.

### Tests required

- Unit: each job's pure step using fake repositories and `FakeClock`
- Module: `Scheduler` orchestration with `FakeClock` advancing time
- Integration: end-to-end "new video appears in IMVDb → next refresh imports it" with all externals faked

### Allowed APIs / docs to read first

- `System.Threading.Channels`, `BackgroundService`, `PeriodicTimer` (.NET 10)
- Sonarr command-queue conventions (for endpoint parity)

### Anti-patterns to avoid

- `Task.Delay` in production loops without `CancellationToken`
- Storing `DateTime.UtcNow` instead of `ISystemClock.UtcNow`
- Letting one failed job block others — each job runs in its own try/catch with structured log

### Dependencies

- Requires Phase 1, 2, 3, 4 or 5, 6.

**Relative size:** L.

---

## Phase 8 — Custom Formats engine + full Decision pipeline + Comparer

### Scope

- Real `CustomFormatEngine`:
  - Spec types per spec §3: `ReleaseTitleSpecification`, `ReleaseGroupSpecification`, `IndexerFlagSpecification`, `SourceSpecification`, `ResolutionSpecification`, `LanguageSpecification`, `SizeSpecification`, `YouTubeChannelSpecification`
  - Each spec is a class with one test file
  - Match rule per spec §3 (all Required match AND all non-Required non-Negate match → format matches); `Negate` inverts; profile score = sum across matched formats
- Decision adds remaining specs from spec §4: `BlocklistedSpec`, `CustomFormatRequiredSpec`, `MinFormatScoreSpec`, `UpgradeAllowedSpec`, `CutoffMetSpec`
- Comparer becomes full: `(QualityRank ASC, CustomFormatScore DESC, ProtocolPreference, SeederTorrentOrAgeUsenetTie, IndexerPriority, Size)`
- React: Settings → Custom Formats (full editor: add spec, configure fields, Negate / Required, drag to assign scores in a Quality Profile)

### End-of-phase deliverable

User defines a custom format ("prefer VEVO uploads", "ban x265") and a quality profile that scores it; grabbing picks the highest-scored acceptable release; the rejection-reasons UI shows the full chain (which specs rejected which release).

### Tests required

- Unit: every spec class — truth tables
- Property: comparer sort stability across permuted inputs
- Module: end-to-end Decision with a portfolio of releases, custom formats, profiles
- Reuse the Sonarr CF behavior matrix as a reference set of test cases

### Allowed APIs / docs to read first

- Spec §3 (CustomFormat) and §4 (Decision / Comparer)
- Sonarr's CustomFormat docs (for behavior reference)

### Anti-patterns to avoid

- Re-introducing a `ReleaseProfile` entity (spec is explicit: subsumed by CustomFormat)
- Hand-rolling spec evaluation order — keep evaluation pure and order-independent
- Letting `Negate` and `Required` interact via implicit precedence; document and test the matrix explicitly

### Dependencies

- Requires Phase 1, 2, 3, 4 or 5.

**Relative size:** L.

---

## Phase 9 — Discovery Rules engine + manual interactive search + manual import + blocklist UX

### Scope

- `Vidarr.Rules` engine: iterate `DiscoveryRuleSet`s, evaluate conditions (`Genre IN`, `Year >= / <=`, `Decade =`, `Type IN`, `Country IN`), apply actions (monitor, profile, root folder, tags, monitor mode). Runs nightly via `RuleSetEvaluationJob` (already scheduled Phase 7); on-demand `POST /api/v1/discoveryrule/evaluate/{id}`
- Manual interactive search UI: search → results table with rejection reasons + score → user picks → grab bypasses Decision acceptance but routes through standard download flow
- Manual import UI: scan an arbitrary folder → suggested matches → user confirms → produce `MusicVideoFile` rows (file-op-aware: hardlink/copy/move; do-not-move option)
- Blocklist UI: list / add / remove; on Failed download with `?blocklist=true` query, add to blocklist
- React: pages — Discovery Rules, Interactive Search modal, Manual Import wizard, Blocklist

### End-of-phase deliverable

User creates a rule "Country=Sweden AND Year>=2020 → monitor, Quality Profile X" → matching videos appear in Wanted → autodownload. User manually searches and grabs a specific release. User imports an existing folder of music videos.

### Tests required

- Unit: each Rule condition (truth table); Action application (idempotency)
- Module: Rule engine over a fake catalog
- Module: manual-import file walker against `FakeFileSystem`
- Integration: full UI-driven manual import flow

### Allowed APIs / docs to read first

- Spec §4 `Vidarr.Rules`
- Spec §5 "Manual interactive search" and "Manual import" subsections (§7 too — existing-library import tool)

### Anti-patterns to avoid

- Treating manual grab as a full Decision bypass — it skips acceptance but still goes through download client and import
- Letting manual import double-move files when "do not move" is selected

### Dependencies

- Requires Phase 1, 2, 8 (manual grab UI shows scores).

**Relative size:** L.

---

## Phase 10 — ChapterSplit (single-file concert MKV handling)

### Scope

- `Vidarr.ChapterSplit`:
  - `IMediaInspector` wraps `ffprobe -show_chapters -show_streams -show_format -of json` via `IProcessRunner`
  - `IChapterSplitter` wraps `ffmpeg -ss <start> -to <end> -map 0 -c copy -map_chapters -1 -avoid_negative_ts make_zero` per chapter
  - Chapter-title → catalog-MusicVideo fuzzy matcher (token-set ratio with artist-context bias)
- `Vidarr.Importer` integration:
  - If single-file with chapters AND release matched to multiple MusicVideos → split → per-chapter match → per-chapter import
  - Unmatched chapters → manual-import queue (UI carryover from Phase 9)
- React: import preview shows chapter splits and matches; user can override each chapter's MusicVideo target

### End-of-phase deliverable

User grabs a 90-minute live-concert MKV with 18 chapters → import produces 18 named files under the artist folder (or fewer + manual queue if some chapters didn't match) — stream-copied, no re-encode.

### Tests required

- Unit: chapter matcher (truth table over realistic chapter titles)
- Module: `MediaInspector` with `FakeProcessRunner` returning recorded ffprobe JSON
- Module: `ChapterSplitter` with `FakeProcessRunner` asserting exact ffmpeg argv
- Integration (nightly contract): real ffmpeg against a small Creative-Commons test MKV with chapters; assert chapter count and approximate durations

### Allowed APIs / docs to read first

- ffprobe JSON output schema (`format`, `streams`, `chapters`)
- ffmpeg `-ss`/`-to` placement (BEFORE `-i` for fast seek, AFTER for accurate cut — pick one and test)
- ffmpeg stream-copy chapter remux best practice (`-c copy -map 0 -map_chapters -1`)
- yt-dlp's chapter output for parity (chapters become MKV chapters when post-processed with `--add-chapters`)

### Anti-patterns to avoid

- Re-encoding instead of stream-copying — must be lossless
- Shelling out to `ffmpeg` via `Process.Start` directly — go through `IProcessRunner`
- Using floating-point timestamp arithmetic without millisecond rounding
- Parsing ffprobe's plain-text output instead of JSON

### Dependencies

- Requires Phase 1, 9 (manual-import queue exists for fall-through)

**Relative size:** L.

---

## Phase 11 — Remaining notifications (Plex, Jellyfin, Discord) + notification fan-out hardening

### Scope

- `PlexNotifier` (PUT `/library/sections/{id}/refresh`, `X-Plex-Token`)
- `JellyfinNotifier` (POST `/Library/Refresh`, `X-Emby-Token`)
- `DiscordNotifier` (rich embed: artist art, video title, quality, indexer)
- Notification subscription model honored: each notifier subscribes only to its `SubscribedEvents`; failures logged, raise `HealthIssueRaised`, never block other subscribers
- Test notification button per notifier
- React: Settings → Notifications

### End-of-phase deliverable

On import, Plex/Jellyfin refresh the music-video library section, Discord posts the embed, webhook continues to fire — independently and even if one fails.

### Tests required

- Module: each notifier against `FakeHttpClient` fixtures
- Module: dispatch fan-out: one notifier throws → others still invoked → health issue raised
- Contract (nightly): real Plex/Jellyfin/Discord webhook only (Plex/Jellyfin via Testcontainers)

### Allowed APIs / docs to read first

- Plex library refresh endpoint + auth
- Jellyfin/Emby `/Library/Refresh`
- Discord webhook JSON shape + rate-limit headers

### Anti-patterns to avoid

- Synchronous `await` chain through all notifiers (one slow → all slow); fire-and-forget with bounded concurrency, but capture exceptions

### Dependencies

- Requires Phase 1, 7 (EventBus completed).

**Relative size:** M.

---

## Phase 12 — Health checks + auth completion + API-key & forms login

### Scope

- `Vidarr.Health` checks per spec §4: `DiskSpaceCheck`, `IndexerReachableCheck`, `DownloadClientReachableCheck`, `RootFolderAccessibleCheck`, `YtDlpVersionCheck`
- Issue lifecycle: raise on failure, resolve on next success; events published to `EventBus`
- `/api/v1/health` endpoint
- Auth: API key (already since Phase 1 for `X-Api-Key` and `?apikey=`) + optional forms login (Sonarr-style cookie auth) toggle in `ApplicationConfig`
- React: Health page (active issues), Login page (when forms-auth enabled)

### End-of-phase deliverable

User sees a health page that reports broken indexers/clients/root folders/yt-dlp; optional forms login protects the SPA while API key keeps automation tooling working.

### Tests required

- Unit: each check spec (pass/fail truth)
- Module: lifecycle (raise once, idempotent on repeated fail; clear on success)
- Integration: `WebApplicationFactory` with forms-auth on/off; API-key with/without

### Allowed APIs / docs to read first

- ASP.NET Core cookie auth handler
- Sonarr's auth-config endpoints (for shape parity)

### Anti-patterns to avoid

- Adding a third auth mode — spec is explicit on two
- Caching health results in static state — store in EF or in-memory keyed by check id

### Dependencies

- Requires Phase 1, 7, 11.

**Relative size:** M.

---

## Phase 13 — Backup/restore + yt-dlp updater + Docker + Logging polish + Existing-library import polish

### Scope

- Backup: zip `config.json` + `vidarr.db` (after `PRAGMA wal_checkpoint(TRUNCATE)`) to `<config>/backups/yyyyMMdd-HHmmss.zip`; configurable retention. `BackupJob` (Phase 7) wired to handler.
- Restore: upload-zip endpoint, validates contents, stages, requires restart
- yt-dlp updater job: HEAD/GET against yt-dlp releases, compare versions, download to temp, atomic swap, emit health-resolution event. Opt-in. Docker compose docs show separate `yt-dlp` volume.
- Existing-library import (carryover): scan tool, parsed-name preview, batch confirm
- Logging polish: per-module Serilog levels via config, log enrichers (artist id, video id, indexer name)
- **Docker**: multi-stage Dockerfile based on `mcr.microsoft.com/dotnet/sdk:10.0` → publish → `mcr.microsoft.com/dotnet/aspnet:10.0` + ffmpeg + yt-dlp; sample `docker-compose.yml` with three volumes (config, library, downloads)
- **CI**: extend `.github/workflows/ci.yml`: `dotnet format` lint, build, test, coverage gate (already there), Docker image build (PR + tag)

### End-of-phase deliverable

`docker compose up` from a clean machine yields a running Vidarr with persistent config, library, downloads. Backups roll. yt-dlp updates on opt-in. CI is green and produces images on tag.

### Tests required

- Module: backup roundtrip (write → read → restore yields same data); retention policy
- Integration: Dockerfile smoke (build, run, hit `/api/v1/system/status`) in CI
- Contract: yt-dlp updater swap atomicity (with `FakeFileSystem` + an integration test gated nightly)

### Allowed APIs / docs to read first

- SQLite `PRAGMA wal_checkpoint`
- `mcr.microsoft.com/dotnet/aspnet:10.0` image surface
- Docker multi-stage build best practices for .NET
- GitHub Actions matrix + `docker/build-push-action`

### Anti-patterns to avoid

- Zipping the SQLite file while WAL is hot — checkpoint first
- Replacing yt-dlp without a chmod + atomic rename
- Baking yt-dlp into the image without a way to override (use a volume)

### Dependencies

- Requires Phase 7, 12.

**Relative size:** L.

---

## Phase 14 — Verification phase (smoke + coverage + image)

### Scope

- **End-to-end smoke** (one script, runs in CI nightly + on release):
  - `docker compose up` brings up: Vidarr, qBittorrent (`linuxserver/qbittorrent`), and a Vidarr-side fake IMVDb backed by a recorded fixture
  - Add a known artist (one whose YouTube channel has a Creative-Commons video)
  - Trigger artist search via REST
  - For one wanted video, run interactive search → grab via YtDlp client (downloads a small CC-licensed video)
  - Assert: file under root folder, naming matches template, webhook fired, history rows present, queue empty
  - Second path: same artist via Newznab fixture-server → grab via qBittorrent against a small test torrent → assert import
- **Coverage gate verification**: full run with `coverlet` on the entire solution; threshold 95%; ReportGenerator HTML uploaded as artifact; gate fails the build below threshold
- **Docker image build & run**: production image built from `Dockerfile`, run `docker run` smoke against `/api/v1/system/status`
- **Cross-platform check**: smoke on linux/amd64 and linux/arm64 in CI (matrix)

### End-of-phase deliverable

A green CI run that proves: end-to-end vertical works against real yt-dlp + real qBittorrent, coverage gate holds, image builds and starts.

### Tests required

- This phase IS the test. Its artifacts:
  - `tests/Vidarr.SmokeTests/` (a dedicated project)
  - Nightly workflow `.github/workflows/nightly.yml`

### Allowed APIs / docs to read first

- Testcontainers .NET (qBit image)
- yt-dlp's Creative-Commons-friendly test URLs (use `youtube` test ID or a public CC channel)
- GitHub Actions Docker matrix

### Anti-patterns to avoid

- Smoke test depending on a fragile public URL — pin to a CC-licensed asset under your control if possible
- Bypassing the coverage gate to ship — the gate is the deliverable

### Dependencies

- Requires every prior phase.

**Relative size:** M.

---

## Dependency graph

```
P0 ──> P1 ──┬──> P2 ──┬──> P3 ──┬──> P4 ──┬──> P5 ──┐
            │         │         │         │         │
            │         │         │         └────────►┼──> P8 ──> P9 ──> P10 ──┐
            │         │         │                   │                         │
            │         │         └──────────────────►┘                         │
            │         │                                                       │
            │         └──> P6 ──────────────────────────────────────────────► │
            │                                                                 │
            └──> P7 (uses P3..P6) ──> P11 ──> P12 ──> P13 ──> P14 (verify)   │
                                                                              │
                                              ◄───────────────────────────────┘
```

**Parallelizable (if more hands available):**
- P3 // P4 // P5 // P6 — all build on P1+P2 and don't depend on each other
- P8 // P9 partial — CF engine can land while manual-import UI is built (UI shows scores, so P8 ends before P9 finishes)
- P11 // P12 — independent

**Strictly sequential:**
- P0 → P1 → P2 (foundation)
- P10 needs P9 (manual-import queue receives unmatched chapters)
- P14 is last

---

## Open questions (to resolve before/during the relevant phase)

1. **Quality definitions: user-extensible vs system-seed only.** Spec §8 says "System-seeded; user-extensible" but the entity in §3 lists `Quality` as a system table without explicit `IsUserDefined`. Are user-added qualities first-class (their own rows with IDs > 100), or do users only re-order / rename existing rows? Affects Phase 2.

2. **Quality profile `Items[]` "grouping support".** Spec §3 says "ordered allowed qualities with grouping support". Does v1 ship grouping in the UI or just the data model? Affects Phase 2.

3. **Manual-grab Decision bypass scope.** Spec §5 says manual grab "bypasses Decision acceptance but routes through normal download flow." Does it still write rejection-reason history (informational), or skip History entirely? Affects Phase 9.

4. **YouTube quality cutoff/upgrade semantics.** A YouTube upload doesn't have an obvious "release group" — `YouTubeChannelSpecification` exists for CF matching, but is the "channel" treated as the release group for the comparer's tie-break, or always blank? Affects Phase 8.

5. **Forms login: cookie scope vs base-URL prefix.** Spec mentions `URL base` in `ApplicationConfig` (reverse-proxy support). Cookie path strategy when `URL base = /vidarr`? Affects Phase 12.

6. **IMVDb rate limits & caching.** IMVDb has no documented hard limits but is politeness-oriented. Desired caching TTL for artist/video lookups (24h? per `LastInfoSync`?). Affects Phase 1 (correctness) and Phase 7 (refresh cadence).

7. **YouTube channel RSS deduplication.** RSS feeds give `published`. Dedup against `MusicVideo.external_ids.youtube` for already-known videos, or against a separate RSS-seen cache? Affects Phase 6.

8. **ChapterSplit container choice.** Spec says stream-copy remux. Output containers always `.mkv`, regardless of source container, or preserve source? Affects Phase 10 default + the Naming token `{ext}`.

9. **Existing-library import tool location.** Lives inside Vidarr (HTTP-driven wizard) only, or is there ALSO a CLI subcommand on `Vidarr.Host`? Phase 9 assumes HTTP-only.

10. **CI test-matrix budget.** Coverlet 95% line gate + nightly Testcontainers contract tests on linux/amd64 assumed. Is arm64 nightly required for v1? (Docker Phase 13 / Verification Phase 14 build arm64; running contract tests on arm64 is significantly slower and more flaky.)

---

## Critical files for Phase 1 (load-bearing — get these right, the rest extends a known pattern)

- `Vidarr.sln`
- `Directory.Packages.props`
- `build/coverlet.runsettings`
- `src/Vidarr.Contracts/IHttpClient.cs` (and the rest of the boundary interfaces colocated in this project)
- `src/Vidarr.Host/Program.cs`
- `.github/workflows/ci.yml`

---

*End of plan.*
