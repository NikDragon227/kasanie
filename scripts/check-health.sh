#!/usr/bin/env sh
set -eu

BASE_URL=${KASANIE_BASE_URL:-http://localhost}
ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)

for PATH in /health/live /health/ready; do
  curl --fail --silent --show-error "$BASE_URL$PATH"
  echo
done
cd "$ROOT_DIR"
for SERVICE in db api web; do
  docker compose ps --status running --services | grep -qx "$SERVICE" || {
    echo "Service is not running: $SERVICE" >&2
    exit 1
  }
done
echo "Kasanie health check passed."
