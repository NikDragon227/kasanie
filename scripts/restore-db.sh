#!/usr/bin/env sh
set -eu

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 /absolute/path/to/kasanie-backup.dump[.enc]" >&2
  exit 2
fi

BACKUP_FILE=$1
if [ ! -f "$BACKUP_FILE" ]; then
  echo "Backup not found: $BACKUP_FILE" >&2
  exit 2
fi

# env-файл (BACKUP_PASSPHRASE для .enc).
BACKUP_ENV_FILE=${BACKUP_ENV_FILE:-"$HOME/.kasanie-backup.env"}
if [ -f "$BACKUP_ENV_FILE" ]; then
  # set -a: значения из env-файла попадают в окружение (openssl -pass env: читает оттуда).
  set -a
  # shellcheck disable=SC1090
  . "$BACKUP_ENV_FILE"
  set +a
fi

if [ -f "$BACKUP_FILE.sha256" ]; then
  (cd "$(dirname -- "$BACKUP_FILE")" && sha256sum -c "$(basename -- "$BACKUP_FILE").sha256")
fi

# Зашифрованный бэкап (.enc) — расшифровываем во временный файл.
WORK_FILE="$BACKUP_FILE"
case "$BACKUP_FILE" in
  *.enc)
    [ -n "${BACKUP_PASSPHRASE:-}" ] || { echo "Encrypted backup — set BACKUP_PASSPHRASE (or ~/.kasanie-backup.env)" >&2; exit 2; }
    command -v openssl >/dev/null 2>&1 || { echo "openssl not found" >&2; exit 2; }
    WORK_FILE=$(mktemp)
    trap 'rm -f "$WORK_FILE"' EXIT
    openssl enc -d -aes-256-cbc -pbkdf2 -pass env:BACKUP_PASSPHRASE -in "$BACKUP_FILE" -out "$WORK_FILE"
    ;;
esac

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT_DIR"
docker compose exec -T db pg_restore --list < "$WORK_FILE" >/dev/null
echo "This replaces all data in the configured Kasanie database. Type RESTORE to continue:"
read -r CONFIRM
[ "$CONFIRM" = "RESTORE" ] || { echo "Cancelled"; exit 1; }

docker compose exec -T db sh -c 'dropdb --if-exists --force --username="$POSTGRES_USER" "$POSTGRES_DB" && createdb --username="$POSTGRES_USER" "$POSTGRES_DB"'
docker compose exec -T db sh -c 'pg_restore --exit-on-error --no-owner --no-acl --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' < "$WORK_FILE"
docker compose restart api
echo "Restore complete. Verify with: curl --fail http://localhost/health"
