#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT_DIR/.env.local"

[[ -f "$ENV_FILE" ]] || {
  echo "Missing $ENV_FILE. Copy .env.local.example first." >&2
  exit 1
}

set -a
source "$ENV_FILE"
set +a
export ASPNETCORE_ENVIRONMENT=Local

dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project "$ROOT_DIR/Infrastructure/Infrastructure.csproj" \
  --startup-project "$ROOT_DIR/Api/Api.csproj"
