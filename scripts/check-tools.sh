#!/usr/bin/env bash
# Verifies that every tool Vidarr needs for local development is on PATH.
# Used standalone (`scripts/check-tools.sh`) and as a prerequisite of `make dev`.
set -u

red()    { printf '\033[31m%s\033[0m' "$1"; }
green()  { printf '\033[32m%s\033[0m' "$1"; }
yellow() { printf '\033[33m%s\033[0m' "$1"; }

missing=0

check() {
    local name="$1" min="$2" want_cmd="$3" version_cmd="$4"
    if ! command -v "$want_cmd" >/dev/null 2>&1; then
        printf '  %s  %-8s  not found on PATH\n' "$(red MISSING)" "$name"
        missing=$((missing + 1))
        return
    fi
    local actual
    actual="$(eval "$version_cmd" 2>/dev/null | head -1)"
    printf '  %s  %-8s  %s' "$(green OK)" "$name" "$actual"
    if [[ -n "$min" ]]; then
        printf ' (need >= %s)' "$min"
    fi
    printf '\n'
}

printf '\nChecking required tools for Vidarr local dev:\n\n'

check "dotnet"  "10.0" "dotnet"  "dotnet --version"
check "node"    "20"   "node"    "node --version"
check "npm"     ""     "npm"     "npm --version"
check "ffmpeg"  ""     "ffmpeg"  "ffmpeg -version | head -1"
check "yt-dlp"  ""     "yt-dlp"  "yt-dlp --version"
check "curl"    ""     "curl"    "curl --version | head -1"

printf '\n'
if [[ "$missing" -gt 0 ]]; then
    printf '%s tools missing. Install them, then re-run.\n' "$(red "$missing")"
    printf 'Linux:   apt install ffmpeg curl  &&  pipx install yt-dlp\n'
    printf '         .NET 10 SDK from https://dotnet.microsoft.com/download\n'
    printf '         Node 20+ from https://nodejs.org or your package manager\n'
    printf 'macOS:   brew install ffmpeg yt-dlp node dotnet@10\n'
    exit 1
fi
printf '%s.\n' "$(green "All required tools present")"
