#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DATA_DIR="${BIGSAMGL_LOCAL_DB_DATA:-$ROOT_DIR/.local/postgres-data}"
LOG_FILE="${BIGSAMGL_LOCAL_DB_LOG:-$ROOT_DIR/.local/postgres.log}"
PORT="${BIGSAMGL_LOCAL_DB_PORT:-55432}"
DB_USER="${BIGSAMGL_LOCAL_DB_USER:-bigsamgl}"
DB_NAME="${BIGSAMGL_LOCAL_DB_NAME:-bigsamgl}"
ACTION="${1:-status}"

[[ "$PORT" =~ ^[0-9]{2,5}$ ]] || {
  echo "BIGSAMGL_LOCAL_DB_PORT must be a valid local port." >&2
  exit 1
}
[[ "$DB_USER" =~ ^[A-Za-z_][A-Za-z0-9_-]{0,62}$ ]] || {
  echo "BIGSAMGL_LOCAL_DB_USER contains unsupported characters." >&2
  exit 1
}
[[ "$DB_NAME" =~ ^[A-Za-z_][A-Za-z0-9_-]{0,62}$ ]] || {
  echo "BIGSAMGL_LOCAL_DB_NAME contains unsupported characters." >&2
  exit 1
}

for tool in pg_isready pg_ctl initdb psql createdb; do
  command -v "$tool" >/dev/null 2>&1 || {
    echo "$tool is required. Install PostgreSQL, then retry." >&2
    exit 1
  }
done

ready() {
  pg_isready --host=127.0.0.1 --port="$PORT" --username="$DB_USER" \
    >/dev/null 2>&1
}

status() {
  if ready; then
    echo "BiGSAM local PostgreSQL is ready on 127.0.0.1:$PORT."
    return 0
  fi
  echo "BiGSAM local PostgreSQL is not running on 127.0.0.1:$PORT."
  return 1
}

start() {
  if ready; then
    status
    return
  fi

  mkdir -p "$ROOT_DIR/.local"
  if [[ ! -f "$DATA_DIR/PG_VERSION" ]]; then
    echo "Creating the isolated BiGSAM local database cluster..."
    initdb \
      --pgdata="$DATA_DIR" \
      --username="$DB_USER" \
      --auth=trust \
      --encoding=UTF8 \
      --locale=C
  fi

  pg_ctl \
    --pgdata="$DATA_DIR" \
    --log="$LOG_FILE" \
    --options="-p $PORT -h 127.0.0.1 -k $DATA_DIR -c max_connections=100" \
    --wait \
    start

  if [[ "$(psql --host=127.0.0.1 --port="$PORT" --username="$DB_USER" \
      --dbname=postgres --tuples-only --no-align \
      --command="SELECT 1 FROM pg_database WHERE datname = '$DB_NAME';")" != "1" ]]; then
    createdb --host=127.0.0.1 --port="$PORT" --username="$DB_USER" "$DB_NAME"
  fi

  status
}

stop() {
  if [[ ! -f "$DATA_DIR/PG_VERSION" ]]; then
    echo "No BiGSAM local database cluster exists."
    return
  fi
  pg_ctl --pgdata="$DATA_DIR" --wait stop --mode=fast
  echo "BiGSAM local PostgreSQL stopped."
}

case "$ACTION" in
  start) start ;;
  stop) stop ;;
  status) status ;;
  *)
    echo "Use: $0 start | stop | status" >&2
    exit 2
    ;;
esac
