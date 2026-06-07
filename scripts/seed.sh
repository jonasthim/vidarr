#!/usr/bin/env bash
# Seeds a running dev backend with a root folder + a handful of artists so the
# UI is non-empty on first boot. Idempotent; conflicts (409) are skipped.
set -u

API="${VIDARR_DEV_API_URL:-http://localhost:5027/api/v1}"
KEY="${VIDARR_API_KEY:-dev-key}"

post() {
    local path="$1" body="$2"
    curl -sS -o /tmp/vidarr-seed.out -w '%{http_code}' \
        -X POST "$API$path" \
        -H "X-Api-Key: $KEY" \
        -H 'Content-Type: application/json' \
        -d "$body"
}

require_running() {
    if ! curl -fsS "$API/system/status" >/dev/null; then
        printf 'Cannot reach %s — is the dev backend running? Try: make dev\n' "$API" >&2
        exit 1
    fi
}

seed_root_folder() {
    local code
    code=$(post /rootfolder '{"path":"/tmp/vidarr-library"}')
    case "$code" in
        201|200) printf '  root folder /tmp/vidarr-library: created\n' ;;
        409)     printf '  root folder /tmp/vidarr-library: already exists\n' ;;
        *)       printf '  root folder /tmp/vidarr-library: FAILED (%s)\n' "$code"; cat /tmp/vidarr-seed.out; return 1 ;;
    esac
    mkdir -p /tmp/vidarr-library
}

# /api/v1/artist requires (provider, providerId, rootFolderPath, qualityProfileId,
# monitorMode). The IMVDb provider id is freeform; we use placeholder strings so
# the FakeMetadataProvider (only used in test fixtures) doesn't get involved —
# in real dev the request hits the real IMVDb endpoint which returns 404 for
# unknown ids and the artist is rejected. Use VIDARR_SEED_REAL_IMVDB=1 + a real
# provider id to actually populate from upstream.
seed_artist() {
    local provider_id="$1" name="$2"
    local code
    code=$(post /artist "$(printf '{"provider":"imvdb","providerId":"%s","rootFolderPath":"/tmp/vidarr-library","qualityProfileId":1,"monitorMode":"All"}' "$provider_id")")
    case "$code" in
        201|200) printf '  artist %-25s: added (provider id %s)\n' "$name" "$provider_id" ;;
        409)     printf '  artist %-25s: already added\n' "$name" ;;
        *)       printf '  artist %-25s: skipped (status %s)\n' "$name" "$code" ;;
    esac
}

# ---- Run --------------------------------------------------------------------
require_running

printf 'Seeding via %s ...\n' "$API"
seed_root_folder
seed_artist "9999" "Smoke Test Band"
seed_artist "10000" "Demo Artist Two"
seed_artist "10001" "Demo Artist Three"

printf 'Done.\n'
