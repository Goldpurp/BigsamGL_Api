#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/.env.local"
PROJECT="$ROOT_DIR/Api/Api.csproj"

[[ -f "$ENV_FILE" ]] || {
  echo "Missing $ENV_FILE. Copy .env.local.example and fill the local values." >&2
  exit 1
}

set -a
source "$ENV_FILE"
set +a
export ASPNETCORE_ENVIRONMENT=Local

"$ROOT_DIR/scripts/local-db.sh" start

dotnet build "$PROJECT" \
  --configuration Release \
  --disable-build-servers \
  -m:1 \
  -p:UseSharedCompilation=false

exec dotnet run \
  --project "$PROJECT" \
  --configuration Release \
  --no-build \
  --launch-profile BiGSAM.Local
