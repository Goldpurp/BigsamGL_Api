#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_CONNECTION="${BIGSAMGL_TEST_POSTGRES:-Host=127.0.0.1;Port=55432;Database=postgres;Username=bigsamgl;Password=bigsamgl-test-only;SSL Mode=Disable}"

dotnet restore "$ROOT_DIR/LogisticsSystem.sln"
dotnet build "$ROOT_DIR/LogisticsSystem.sln" \
  --configuration Release \
  --no-restore \
  --disable-build-servers \
  -m:1 \
  -p:UseSharedCompilation=false

BIGSAMGL_TEST_POSTGRES="$TEST_CONNECTION" dotnet test \
  "$ROOT_DIR/Tests/Tests.csproj" \
  --configuration Release \
  --no-build \
  --logger "console;verbosity=normal"

"$ROOT_DIR/scripts/check-nuget-vulnerabilities.sh"
dotnet tool restore
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project "$ROOT_DIR/Infrastructure/Infrastructure.csproj" \
  --startup-project "$ROOT_DIR/Api/Api.csproj" \
  --configuration Release \
  --no-build

dotnet publish "$ROOT_DIR/Api/Api.csproj" \
  --configuration Release \
  --no-build \
  --output /tmp/bigsamgl-api-publish
