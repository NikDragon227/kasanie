#!/usr/bin/env sh
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
BACKUP_DIR=${BACKUP_DIR:-"$ROOT_DIR/backups"}
mkdir -p "$BACKUP_DIR"
STAMP=$(date -u +%Y%m%dT%H%M%SZ)
TARGET="$BACKUP_DIR/kasanie-$STAMP.dump"

cd "$ROOT_DIR"
docker compose exec -T db sh -c 'pg_dump --format=custom --no-owner --no-acl --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' > "$TARGET"
chmod 600 "$TARGET"
docker compose exec -T db pg_restore --list < "$TARGET" >/dev/null
sha256sum "$TARGET" > "$TARGET.sha256"
chmod 600 "$TARGET.sha256"
echo "Backup created and verified: $TARGET"
echo "Checksum: $TARGET.sha256"
