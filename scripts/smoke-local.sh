#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BIGSAMGL_LOCAL_API_URL:-http://127.0.0.1:5120}"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

for command in curl jq; do
  command -v "$command" >/dev/null 2>&1 || {
    echo "Required command '$command' is not installed." >&2
    exit 1
  }
done

request() {
  local name="$1"
  shift
  curl --silent --show-error --dump-header "$TMP_DIR/$name.headers" \
    --output "$TMP_DIR/$name.body" --write-out '%{http_code}' "$@"
}

assert_status() {
  [[ "$2" == "$1" ]] || {
    echo "FAIL: $3 returned HTTP $2; expected $1." >&2
    exit 1
  }
  echo "PASS: $3"
}

status="$(request live "$BASE_URL/health/live")"
assert_status 200 "$status" "liveness"
jq -e '.status == "Healthy" and .environment == "local"' "$TMP_DIR/live.body" >/dev/null
grep -Eiq '^X-BiGSAM-API-Environment:[[:space:]]*local' "$TMP_DIR/live.headers"

status="$(request ready "$BASE_URL/health/ready")"
assert_status 200 "$status" "database readiness"
jq -e '.status == "Ready" and .database == "Connected"' "$TMP_DIR/ready.body" >/dev/null

status="$(request swagger "$BASE_URL/swagger/v1/swagger.json")"
assert_status 200 "$status" "OpenAPI document"
jq -e '(.paths | length) > 0' "$TMP_DIR/swagger.body" >/dev/null

status="$(request allowed-cors -H 'Origin: http://localhost:3001' "$BASE_URL/health/live")"
assert_status 200 "$status" "customer website CORS"
grep -Eiq '^Access-Control-Allow-Origin:[[:space:]]*http://localhost:3001' \
  "$TMP_DIR/allowed-cors.headers"

status="$(request rejected-cors -H 'Origin: https://attacker.invalid' "$BASE_URL/health/live")"
assert_status 200 "$status" "untrusted origin response"
if grep -Eiq '^Access-Control-Allow-Origin:' "$TMP_DIR/rejected-cors.headers"; then
  echo "FAIL: untrusted origin received a CORS allow header." >&2
  exit 1
fi

status="$(request wrong-environment \
  -H 'X-BiGSAM-App-Environment: production' "$BASE_URL/health/live")"
assert_status 409 "$status" "cross-environment protection"

status="$(request bad-host -H 'Host: attacker.invalid' "$BASE_URL/health/live")"
assert_status 400 "$status" "host header protection"

echo "All BiGSAM local smoke checks passed."
