#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPORT="$(
  dotnet list "$ROOT_DIR/LogisticsSystem.sln" package \
    --vulnerable \
    --include-transitive \
    --format json
)"

printf '%s\n' "$REPORT"
if grep -q '"severity"' <<<"$REPORT"; then
  echo "NuGet vulnerability gate failed." >&2
  exit 1
fi
echo "NuGet vulnerability gate passed."
