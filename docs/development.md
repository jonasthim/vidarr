# Vidarr — Development guide

This is the long-form companion to the README's quick-start. It covers everything you need to run Vidarr locally, with the backend and the React SPA wired up end-to-end.

## Prerequisites

| Tool          | Min version | Used for                                  |
| ------------- | ----------- | ----------------------------------------- |
| .NET SDK      | 10.0        | Backend build, test, run                  |
| Node          | 20+         | Vite dev server, SPA bundle               |
| npm           | bundled     | Frontend deps                             |
| ffmpeg        | any recent  | Chapter split + media inspection          |
| yt-dlp        | any recent  | YouTube download client + version check   |
| curl          | any         | `make seed` and ad-hoc API probes         |

Install snippets:

```sh
# Linux (Debian/Ubuntu/Arch-family)
sudo apt install ffmpeg curl   ||   sudo pacman -S ffmpeg curl
pipx install yt-dlp            # or  pip install --user yt-dlp
# .NET 10 SDK: https://dotnet.microsoft.com/download
# Node 20+   : https://nodejs.org  or  fnm install 20  /  nvm install 20

# macOS
brew install ffmpeg yt-dlp node dotnet@10

# Windows (winget)
winget install Microsoft.DotNet.SDK.10
winget install OpenJS.NodeJS.LTS
winget install Gyan.FFmpeg
winget install yt-dlp.yt-dlp
```

Verify with:

```sh
make check-tools
```

## Port layout

| Process                   | Default port    | Set in                                          |
| ------------------------- | --------------- | ----------------------------------------------- |
| `Vidarr.Host` (http)      | `5027`          | `src/Vidarr.Host/Properties/launchSettings.json` |
| `Vidarr.Host` (https)     | `7182`          | `src/Vidarr.Host/Properties/launchSettings.json` |
| Vite dev server           | `5173`          | `src/Vidarr.Web/vite.config.ts`                 |
| Vite → backend proxy      | `/api → 5027`   | `src/Vidarr.Web/vite.config.ts`                 |

Override the backend port if `5027` clashes:

```sh
VIDARR_DEV_BACKEND_PORT=5500 make dev
```

Vite picks up the same env var via `vite.config.ts` so the proxy follows.

## Quick start

```sh
make dev
```

Opens two coordinated processes in one terminal:
- `[host]` — `dotnet run --project src/Vidarr.Host --launch-profile http`
- `[web ]` — `npm --prefix src/Vidarr.Web run dev`

Logs are prefix-tagged so they're greppable. Ctrl-C tears down both.

Then browse `http://localhost:5173`. Network tab should show `/api/v1/*` returning 200, not 401 (see API key below).

## Two-terminal flow (fallback)

If you'd rather run them yourself:

```sh
# Terminal 1
dotnet run --project src/Vidarr.Host --launch-profile http
# Watch for: "Vidarr API key: <key>"  — note it, or set VIDARR_API_KEY=dev-key

# Terminal 2
cd src/Vidarr.Web
VIDARR_API_KEY=dev-key npm run dev
```

## API auth

The REST API key is **persisted in the DB** and auto-generated on first boot (Sonarr/Radarr parity). It's exposed in the web UI under **Settings → Security**, where it can be revealed, copied, or regenerated. Setting `VIDARR_API_KEY` in the environment (or `Vidarr:ApiKey` in appsettings) pins the value at boot and disables the Regenerate button.

### How the SPA gets the key

The SPA reads `window.VIDARR_API_KEY` on boot (`src/Vidarr.Web/src/api.ts:1`). It's injected into `index.html` in two different ways:

- **Production** — the .NET host serves `index.html` through `IndexHtmlHandler`, which reads the live key from `IApiKeyService` and substitutes `%VIDARR_API_KEY%` at request time. Rotations take effect immediately on the next page load.
- **Dev** — `vite.config.ts` substitutes the placeholder at Vite-dev-server start with `process.env.VIDARR_API_KEY` (defaults to `dev-key`).

For `make dev`, the dev appsettings locks the backend's key to `dev-key`, and `scripts/dev.sh` exports the same value to both processes, so they always agree without any per-machine setup. Set `VIDARR_API_KEY` in your shell to override.

## Sample data

With `make dev` running:

```sh
make seed
```

`scripts/seed.sh` POSTs:
- A root folder at `/tmp/vidarr-library` (`/api/v1/rootfolder`)
- Three artists referencing canned IMVDb provider IDs (`/api/v1/artist`)

Quality profiles are already seeded by `Vidarr.Catalog.Seeding.IDataSeeder` at startup, so this script doesn't re-add them.

The IMVDb provider ids are deliberately placeholder — on a real IMVDb backend those will 404, the artist is rejected, and the script reports the status code. For a richer fixture, supply real IMVDb ids in `scripts/seed.sh` (or use the in-browser "Add artist" flow once `make dev` is up).

## Common targets

```
make help        # list every target with one-line descriptions
make dev         # backend + Vite together (Ctrl-C stops both)
make build       # dotnet build
make test        # full test suite, Release config (parity with CI)
make format      # dotnet format --verify-no-changes
make coverage    # full coverage run + HTML report (opens in browser)
make seed        # sample data via REST
make check-tools # detect missing prerequisites
make clean       # wipe data/, TestResults/, bin/, obj/
```

## IDE setup

### VS Code

`.vscode/launch.json` defines three configs:

- **Host (debug)** — launches the backend under the .NET debugger; URLs surface in `Now listening on:` line, `serverReadyAction` auto-opens in the browser.
- **Web (Vite, Chrome)** — opens Chrome at `http://localhost:5173` with sourcemap mapping into `src/Vidarr.Web/src/`.
- **Full dev** — compound that starts the Vite dev server (`tasks.json` → `vite-dev`) then the host with the debugger attached. `stopAll: true` shuts both down on stop.

Hit F5 and pick **Full dev** for a one-shortcut launch.

### JetBrains Rider / Visual Studio

`launchSettings.json` profiles (`http`, `https`) are picked up automatically. Open `Vidarr.slnx`, select the `Vidarr.Host: http` profile, and press Run. For the Vite side, run `npm run dev` from the integrated terminal under `src/Vidarr.Web`.

## Resetting state

Local dev state lives entirely under `data/dev/` (set in `appsettings.Development.json`):

```sh
rm -rf data/dev/          # nuke DB + backups + incomplete folder
make clean                # nuke data/, TestResults/, every bin/obj
```

The DB is recreated on the next `dotnet run` via `EnsureCreatedAsync()`.

## Troubleshooting

| Symptom                                                    | Cause / fix                                                                 |
| ---------------------------------------------------------- | --------------------------------------------------------------------------- |
| `make dev` errors with "tool MISSING"                      | Install whichever tool — `scripts/check-tools.sh` told you which            |
| `/api/v1/*` returns 401 in the browser                     | `VIDARR_API_KEY` mismatch — easiest fix: kill, then `make dev` again        |
| `npm run dev` proxies but every request 502s               | Backend isn't running, or backend port doesn't match `VIDARR_DEV_BACKEND_PORT` |
| `SqliteException: database is locked`                      | A stale debugger or process is holding `data/dev/vidarr.db` open. Kill it.  |
| Port 5027 / 5173 already in use                            | `VIDARR_DEV_BACKEND_PORT=NNNN VIDARR_DEV_FRONTEND_PORT=MMMM make dev`       |
| `dotnet test` crashes on `Vidarr.Tests.Common.dll`         | Already fixed — that csproj has `<IsTestProject>false</IsTestProject>`      |
| Build takes forever because the SPA rebuilds during `dotnet test` | Pass `-p:SkipWebBuild=true` (already in the `make test` recipe)      |
| `make coverage` complains about reportgenerator            | It auto-installs into `~/.dotnet/tools`; ensure that's on `PATH`            |
| yt-dlp version check raises a Health issue                 | Install / upgrade `yt-dlp`; or set `VIDARR_YTDLP_PATH` to a working binary  |

## Architecture pointer

For the full design — module list, decision pipeline, event bus, data model — see [`docs/superpowers/specs/2026-06-06-vidarr-design.md`](superpowers/specs/2026-06-06-vidarr-design.md). The phased implementation history is at [`docs/superpowers/plans/2026-06-06-vidarr-implementation-plan.md`](superpowers/plans/2026-06-06-vidarr-implementation-plan.md).
