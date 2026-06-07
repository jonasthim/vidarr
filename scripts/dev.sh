#!/usr/bin/env bash
# Starts the Vidarr backend and the Vite dev server together in one terminal.
# Ctrl-C kills both. Logs are prefixed [host] / [web] so they're greppable.
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

# ---- Config knobs -----------------------------------------------------------
# Both backend port and frontend port are overridable via env so the same
# script works when the defaults clash with something else on the machine.
export VIDARR_DEV_BACKEND_PORT="${VIDARR_DEV_BACKEND_PORT:-5027}"
export VIDARR_DEV_FRONTEND_PORT="${VIDARR_DEV_FRONTEND_PORT:-5173}"

# Lock the API key to a stable dev value so the Vite-injected
# window.VIDARR_API_KEY matches what the backend expects.
export VIDARR_API_KEY="${VIDARR_API_KEY:-dev-key}"

# Keep dev DB/backups/incomplete out of the way of production paths.
export VIDARR_SQLITE_PATH="${VIDARR_SQLITE_PATH:-data/dev/vidarr.db}"
export VIDARR_BACKUP_FOLDER="${VIDARR_BACKUP_FOLDER:-data/dev/backups}"
export VIDARR_INCOMPLETE="${VIDARR_INCOMPLETE:-data/dev/incomplete}"

# ---- Helpers ----------------------------------------------------------------
host_pid=""
web_pid=""

prefix_stream() {
    local label="$1"
    while IFS= read -r line; do
        printf '%s %s\n' "$label" "$line"
    done
}

cleanup() {
    trap '' INT TERM
    [[ -n "$host_pid" ]] && kill -TERM "$host_pid" 2>/dev/null || true
    [[ -n "$web_pid"  ]] && kill -TERM "$web_pid"  2>/dev/null || true
    wait 2>/dev/null || true
    printf '\n[dev] both processes stopped.\n'
}
trap cleanup INT TERM EXIT

# ---- Run --------------------------------------------------------------------
mkdir -p data/dev

printf '[dev] Vidarr dev launcher\n'
printf '[dev]   backend  : http://localhost:%s\n' "$VIDARR_DEV_BACKEND_PORT"
printf '[dev]   frontend : http://localhost:%s\n' "$VIDARR_DEV_FRONTEND_PORT"
printf '[dev]   api key  : %s\n' "$VIDARR_API_KEY"
printf '[dev] starting (Ctrl-C stops both)...\n\n'

# Backend
(
    cd "$ROOT" && \
    ASPNETCORE_URLS="http://localhost:${VIDARR_DEV_BACKEND_PORT}" \
        dotnet run --project src/Vidarr.Host --launch-profile http 2>&1
) | prefix_stream "[host]" &
host_pid=$!

# Frontend (Vite picks up VIDARR_DEV_BACKEND_PORT + VIDARR_API_KEY from env)
(
    cd "$ROOT/src/Vidarr.Web" && \
        npm run dev -- --port "$VIDARR_DEV_FRONTEND_PORT" --strictPort 2>&1
) | prefix_stream "[web]" &
web_pid=$!

# Wait for either to die, then cleanup tears down the other.
wait -n "$host_pid" "$web_pid"
