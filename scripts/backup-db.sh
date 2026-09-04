#!/usr/bin/env sh
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)

# Необязательный env-файл с настройками off-site (BACKUP_PASSPHRASE / BACKUP_REMOTE и пр.).
# На проде: скопировать scripts/backup-db.env.example в ~/.kasanie-backup.env, chmod 600.
BACKUP_ENV_FILE=${BACKUP_ENV_FILE:-"$HOME/.kasanie-backup.env"}
if [ -f "$BACKUP_ENV_FILE" ]; then
  # set -a: значения из env-файла попадают в окружение (openssl -pass env: читает оттуда).
  set -a
  # shellcheck disable=SC1090
  . "$BACKUP_ENV_FILE"
  set +a
fi

BACKUP_DIR=${BACKUP_DIR:-"$ROOT_DIR/backups"}
KEEP_LOCAL=${BACKUP_KEEP_LOCAL:-7}
KEEP_REMOTE_DAYS=${BACKUP_KEEP_REMOTE_DAYS:-30}
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

UPLOAD="$TARGET"

# Шифрование (если задан BACKUP_PASSPHRASE) — AES-256 через openssl.
# Локально после этого остаётся только зашифрованная копия.
if [ -n "${BACKUP_PASSPHRASE:-}" ]; then
  command -v openssl >/dev/null 2>&1 || { echo "openssl not found — cannot encrypt" >&2; exit 1; }
  ENC="$TARGET.enc"
  openssl enc -aes-256-cbc -pbkdf2 -salt -pass env:BACKUP_PASSPHRASE -in "$TARGET" -out "$ENC"
  chmod 600 "$ENC"
  sha256sum "$ENC" > "$ENC.sha256"
  chmod 600 "$ENC.sha256"
  rm -f "$TARGET" "$TARGET.sha256"
  UPLOAD="$ENC"
  echo "Encrypted: $ENC"
fi

# Off-site (если задан BACKUP_REMOTE — rclone-путь, напр. b2:kasanie-backups).
# Нужен установленный и настроенный rclone (apt install rclone && rclone config).
if [ -n "${BACKUP_REMOTE:-}" ]; then
  if command -v rclone >/dev/null 2>&1; then
    rclone copy --quiet "$UPLOAD" "$BACKUP_REMOTE/"
    rclone copy --quiet "$UPLOAD.sha256" "$BACKUP_REMOTE/"
    rclone delete --quiet --min-age "${KEEP_REMOTE_DAYS}d" "$BACKUP_REMOTE/" || true
    echo "Uploaded to: $BACKUP_REMOTE (remote retention: ${KEEP_REMOTE_DAYS}d)"
  else
    echo "rclone not found — off-site upload skipped" >&2
  fi
fi

# Локальная ротация: оставляем KEEP_LOCAL самых свежих наборов.
find "$BACKUP_DIR" -maxdepth 1 -type f \( -name 'kasanie-*.dump' -o -name 'kasanie-*.dump.enc' \) -printf '%T@\t%p\n' 2>/dev/null \
  | sort -rn | sed -n "$((KEEP_LOCAL + 1)),\$p" | cut -f2- \
  | while IFS= read -r old; do rm -f "$old" "$old.sha256"; echo "Pruned local: $old"; done

echo "Done. Checksum: $UPLOAD.sha256"
