#!/usr/bin/env sh
set -eu

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 /absolute/path/to/kasanie-backup.dump" >&2
  exit 2
fi

BACKUP_FILE=$1
if [ ! -f "$BACKUP_FILE" ]; then
  echo "Backup not found: $BACKUP_FILE" >&2
  exit 2
fi

if [ -f "$BACKUP_FILE.sha256" ]; then
  (cd "$(dirname -- "$BACKUP_FILE")" && sha256sum -c "$(basename -- "$BACKUP_FILE").sha256")
fi

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT_DIR"
docker compose exec -T db pg_restore --list < "$BACKUP_FILE" >/dev/null
echo "This replaces all data in the configured Kasanie database. Type RESTORE to continue:"
read -r CONFIRM
[ "$CONFIRM" = "RESTORE" ] || { echo "Cancelled"; exit 1; }

docker compose exec -T db sh -c 'dropdb --if-exists --force --username="$POSTGRES_USER" "$POSTGRES_DB" && createdb --username="$POSTGRES_USER" "$POSTGRES_DB"'
docker compose exec -T db sh -c 'pg_restore --exit-on-error --no-owner --no-acl --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' < "$BACKUP_FILE"
docker compose restart api
echo "Restore complete. Verify with: curl --fail http://localhost/health"
