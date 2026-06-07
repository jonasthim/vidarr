#!/usr/bin/env bash
# Runs the full test suite with coverlet, generates a Cobertura + HTML report,
# and opens it in a browser if xdg-open / open is available.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

OUT="$ROOT/TestResults"
rm -rf "$OUT"

dotnet test \
    --configuration Release \
    --settings build/coverlet.runsettings \
    --collect:"XPlat Code Coverage" \
    --results-directory "$OUT" \
    -p:SkipWebBuild=true

# Install / locate reportgenerator. We don't pin the version here — the CI
# workflow drives that — but the latest tool talks to any coverlet output.
if ! command -v reportgenerator >/dev/null 2>&1; then
    if [[ -x "$HOME/.dotnet/tools/reportgenerator" ]]; then
        export PATH="$PATH:$HOME/.dotnet/tools"
    else
        dotnet tool install --global dotnet-reportgenerator-globaltool >/dev/null
        export PATH="$PATH:$HOME/.dotnet/tools"
    fi
fi

reportgenerator \
    -reports:"$OUT/**/coverage.cobertura.xml" \
    -targetdir:"$OUT/coverage-report" \
    -reporttypes:'Html;TextSummary' \
    -assemblyfilters:'+Vidarr.*;-*Tests*' \
    -classfilters:'-*Migrations*;-*Program' \
    -filefilters:'-**/Migrations/**;-**/Program.cs'

printf '\n%s\n' "----- Coverage summary -----"
grep -E 'Line coverage|Branch coverage|Method coverage' "$OUT/coverage-report/Summary.txt"
printf '\nHTML report: %s/coverage-report/index.html\n' "$OUT"

# Best-effort open
if command -v xdg-open >/dev/null 2>&1; then
    xdg-open "$OUT/coverage-report/index.html" >/dev/null 2>&1 &
elif command -v open >/dev/null 2>&1; then
    open "$OUT/coverage-report/index.html" >/dev/null 2>&1 &
fi
