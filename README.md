# Vidarr

A self-hosted, *arr-style application that maintains a library of music videos.

[![CI](https://github.com/jonasthim/vidarr/actions/workflows/ci.yml/badge.svg)](https://github.com/jonasthim/vidarr/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Status: v0.1 — initial release.** All 14 implementation phases shipped (~683 tests, 96.9% line coverage), but the app has not yet been baked in a real-world deployment. Expect rough edges.

## What it does

Follow artists, or define genre/year/decade auto-rules. Vidarr discovers matching music videos and downloads them — then organizes the files on disk and tells your media server about them.

- **Discovery:** YouTube (Data API when configured, yt-dlp scraping otherwise, channel RSS for monitored artists), NewzNab (Usenet), Torznab (torrents).
- **Download clients:** qBittorrent, Transmission, Deluge, SABnzbd, NZBGet, yt-dlp.
- **Decision pipeline:** Sonarr-style parser → specs → Custom Format engine → comparer.
- **Quality model:** Quality Profiles + Custom Formats (Sonarr v4 style).
- **Concert MKVs:** ffmpeg-based chapter-split + per-chapter matching against the catalog.
- **Notifications:** Plex (library refresh), Jellyfin/Emby, Discord, generic webhook.
- **Health checks:** disk space, indexer reachability, download-client reachability, root-folder accessibility, yt-dlp version.
- **Backup/restore:** zipped DB + config; opt-in yt-dlp auto-updater.

Sonarr/Radarr/Lidarr conventions are preserved deliberately — modules, terminology, REST shape, decision pipeline — so existing *arr users feel at home.

## Quick start (Docker)

```sh
docker compose up -d
```

Then open `http://localhost:8989`. The API key is auto-generated on first start and printed to the container log; set it explicitly with the `VIDARR_API_KEY` env var (see [docker-compose.yml](docker-compose.yml)).

Three named volumes are mounted: `/config` (SQLite DB + backups), `/downloads` (incomplete files), `/library` (final files).

## Configuration

Every setting is overridable by env var. The fallback chain is `env var → appsettings.json key → built-in default`.

| Env var                  | `appsettings.json` key       | Default              | Purpose                          |
| ------------------------ | ---------------------------- | -------------------- | -------------------------------- |
| `VIDARR_API_KEY`         | `Vidarr:ApiKey`              | auto on first boot   | REST API key (`X-Api-Key` header) — see Auth below |
| `VIDARR_SQLITE_PATH`     | `Vidarr:Sqlite:Path`         | `data/vidarr.db`     | Path to the SQLite file          |
| `VIDARR_BACKUP_FOLDER`   | `Vidarr:Backup:Folder`       | `data/backups`       | Directory for zipped backups     |
| —                        | `Vidarr:Backup:Retention`    | `10`                 | Number of backups to keep        |
| `VIDARR_INCOMPLETE`      | `Vidarr:IncompleteFolder`    | `data/incomplete`    | yt-dlp working directory         |
| `VIDARR_IMVDB_KEY`       | `Vidarr:Imvdb:ApiKey`        | (none)               | IMVDb API key (optional)         |
| `VIDARR_YTDLP_PATH`      | `Vidarr:YtDlp:Path`          | `yt-dlp`             | Path to the yt-dlp binary        |

Per-namespace log levels live under `Serilog:MinimumLevel:Override` in [`src/Vidarr.Host/appsettings.json`](src/Vidarr.Host/appsettings.json).

### Auth

The REST API key is generated on first boot and persisted in the SQLite DB (Sonarr/Radarr parity). It survives restarts and can be regenerated from **Settings → Security** in the web UI. Setting `VIDARR_API_KEY` env var (or `Vidarr:ApiKey` in appsettings) pins the value at boot and disables UI rotation — useful when the operator wants the value locked in by their orchestration. Optional forms login (username + password) is a parallel mechanism configured via `PUT /api/v1/auth/config`.

## Architecture

Vidarr is a single .NET process with an in-memory event bus, EF Core 10 + SQLite (WAL), and a React + TypeScript SPA served from `wwwroot/`.

| Module               | Responsibility                                            |
| -------------------- | --------------------------------------------------------- |
| `Vidarr.Contracts`   | Boundary interfaces, domain models, events                |
| `Vidarr.Catalog`     | EF Core entities, repositories, seeding                   |
| `Vidarr.Metadata`    | IMVDb provider (pluggable `IMetadataProvider`)            |
| `Vidarr.Indexers`    | NewzNab, Torznab, YouTube (hybrid Data API / yt-dlp)      |
| `Vidarr.Decision`    | Release parser, specs, Custom Format engine, comparer     |
| `Vidarr.DownloadClients` | qBittorrent / Transmission / Deluge / SABnzbd / NZBGet / yt-dlp |
| `Vidarr.Importer`    | Chapter-aware import pipeline                             |
| `Vidarr.ChapterSplit`| ffmpeg/ffprobe wrapper, chapter detection + title match   |
| `Vidarr.Rules`       | Discovery rules (genre / year / decade)                   |
| `Vidarr.Notifications`| Plex, Jellyfin/Emby, Discord, generic webhook            |
| `Vidarr.Health`      | Health checks + monitor + yt-dlp updater                  |
| `Vidarr.Backup`      | Backup / restore (with `PRAGMA wal_checkpoint(TRUNCATE)`) |
| `Vidarr.Scheduler`   | Command queue, recurring-job hosted service               |
| `Vidarr.EventBus`    | In-process pub/sub                                        |
| `Vidarr.Naming`      | Token-driven filename/folder rendering                    |
| `Vidarr.Api`         | ASP.NET Core minimal APIs, forms-auth, API-key middleware |
| `Vidarr.Host`        | Composition root, Serilog, hosted services                |
| `Vidarr.Web`         | React + TypeScript SPA, TanStack Query                    |

Full design: [`docs/superpowers/specs/2026-06-06-vidarr-design.md`](docs/superpowers/specs/2026-06-06-vidarr-design.md). Phased plan: [`docs/superpowers/plans/2026-06-06-vidarr-implementation-plan.md`](docs/superpowers/plans/2026-06-06-vidarr-implementation-plan.md).

## Development

Prerequisites: .NET 10 SDK, Node 20+, ffmpeg + yt-dlp on `PATH`. Run `make check-tools` to verify.

```sh
make dev      # backend + Vite dev server in one terminal (Ctrl-C stops both)
make test     # full suite (Release config, parity with CI)
make help     # list every target
```

See [`docs/development.md`](docs/development.md) for the full local-dev guide — port layout, API-key flow, sample-data seeding, IDE setup, troubleshooting. Test discipline is strict TDD with a 95% line-coverage gate enforced in CI; see [`CLAUDE.md`](CLAUDE.md) for the conventions every change must follow.

## Project layout

```
src/
  Vidarr.{Contracts, Catalog, Metadata, Indexers, Decision,
          DownloadClients, Importer, ChapterSplit, Rules,
          Notifications, Health, Backup, Scheduler, EventBus,
          Naming, Infrastructure, Api, Host, Web}/
tests/
  Vidarr.{<each module>}.Tests/        # unit tests, one per module
  Vidarr.IntegrationTests/             # WebApplicationFactory boot
  Vidarr.SmokeTests/                   # golden vertical + connectivity
  Vidarr.Tests.Common/                 # FakeHttpClient, FakeFileSystem, ...
```

## CI / image distribution

- **Every PR + push to `main`:** [`ci.yml`](.github/workflows/ci.yml) runs build, format check, the full test suite, the 95% coverage gate, then builds a `linux/amd64` Docker image and smoke-tests `/api/v1/system/status` against the running container.
- **Tag pushes (`v*`):** the same job builds `linux/amd64 + linux/arm64` and pushes to `ghcr.io/jonasthim/vidarr`.
- **Nightly:** [`nightly.yml`](.github/workflows/nightly.yml) runs the full suite + smoke tests + docker image smoke across both architectures.

## Status / roadmap

Shipped vs. deferred is tracked phase-by-phase in [`docs/superpowers/plans/2026-06-06-vidarr-implementation-plan.md`](docs/superpowers/plans/2026-06-06-vidarr-implementation-plan.md). The explicit v1 non-goals (Lidarr-linked mode, plugin host, calendar view, i18n, mobile apps, …) are listed in the [spec §1](docs/superpowers/specs/2026-06-06-vidarr-design.md).

## License

MIT. See [LICENSE](LICENSE).
