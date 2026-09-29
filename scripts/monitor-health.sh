#!/usr/bin/env sh
# Запускается по cron/systemd timer на VPS. Проверяет доступность приложения,
# срок TLS, свободное место и свежесть последнего backup. При сбое отправляет
# короткое уведомление на webhook или в Telegram.
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
BASE_URL=${KASANIE_BASE_URL:-https://prokasanie.ru}
ALERT_WEBHOOK_URL=${KASANIE_ALERT_WEBHOOK_URL:-}
TELEGRAM_RELAY_URL=${KASANIE_TELEGRAM_RELAY_URL:-}
TELEGRAM_RELAY_SHARED_SECRET=${KASANIE_TELEGRAM_RELAY_SHARED_SECRET:-}
TELEGRAM_BOT_TOKEN=${KASANIE_TELEGRAM_BOT_TOKEN:-}
TELEGRAM_CHAT_ID=${KASANIE_TELEGRAM_CHAT_ID:-}
TLS_CERT_PATH=${KASANIE_TLS_CERT_PATH:-"$ROOT_DIR/certbot/certs/fullchain.pem"}
TLS_MIN_REMAINING_DAYS=${KASANIE_TLS_MIN_REMAINING_DAYS:-14}
DISK_PATH=${KASANIE_DISK_PATH:-/}
DISK_MAX_USED_PERCENT=${KASANIE_DISK_MAX_USED_PERCENT:-85}
BACKUP_DIR=${KASANIE_BACKUP_DIR:-"$ROOT_DIR/backups"}
BACKUP_MAX_AGE_HOURS=${KASANIE_BACKUP_MAX_AGE_HOURS:-48}
OUTPUT_FILE=$(mktemp)
trap 'rm -f "$OUTPUT_FILE"' EXIT
FAILURES=''

add_failure() {
  if [ -n "$FAILURES" ]; then
    FAILURES="$FAILURES; $1"
  else
    FAILURES=$1
  fi
}

if ! KASANIE_BASE_URL="$BASE_URL" "$ROOT_DIR/scripts/check-health.sh" >"$OUTPUT_FILE" 2>&1; then
  cat "$OUTPUT_FILE" >&2
  add_failure 'health check failed'
fi

case "$TLS_MIN_REMAINING_DAYS" in
  ''|*[!0-9]*) add_failure 'TLS threshold is invalid' ;;
  *)
    TLS_MIN_REMAINING_SECONDS=$((TLS_MIN_REMAINING_DAYS * 86400))
    if ! command -v openssl >/dev/null 2>&1; then
      add_failure 'openssl is unavailable for TLS check'
    elif [ ! -r "$TLS_CERT_PATH" ]; then
      add_failure 'TLS certificate file is unavailable'
    elif ! openssl x509 -checkend "$TLS_MIN_REMAINING_SECONDS" -noout -in "$TLS_CERT_PATH" >/dev/null 2>&1; then
      add_failure "TLS certificate expires in under ${TLS_MIN_REMAINING_DAYS} days"
    fi
    ;;
esac

case "$DISK_MAX_USED_PERCENT" in
  ''|*[!0-9]*) add_failure 'disk threshold is invalid' ;;
  *)
    DISK_USED_PERCENT=$(df -P "$DISK_PATH" 2>/dev/null | awk 'NR == 2 { gsub(/%/, "", $5); print $5 }')
    case "$DISK_USED_PERCENT" in
      ''|*[!0-9]*) add_failure 'disk usage is unavailable' ;;
      *)
        if [ "$DISK_USED_PERCENT" -ge "$DISK_MAX_USED_PERCENT" ]; then
          add_failure "disk ${DISK_PATH} is ${DISK_USED_PERCENT}% full"
        fi
        ;;
    esac
    ;;
esac

case "$BACKUP_MAX_AGE_HOURS" in
  ''|*[!0-9]*) add_failure 'backup age threshold is invalid' ;;
  *)
    LATEST_BACKUP=$(find "$BACKUP_DIR" -maxdepth 1 -type f \( -name 'kasanie-*.dump' -o -name 'kasanie-*.dump.enc' \) -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -n 1 || true)
    if [ -z "$LATEST_BACKUP" ]; then
      add_failure 'no database backup found'
    else
      BACKUP_EPOCH=${LATEST_BACKUP%% *}
      BACKUP_EPOCH=${BACKUP_EPOCH%%.*}
      BACKUP_AGE_HOURS=$((($(date +%s) - BACKUP_EPOCH) / 3600))
      if [ "$BACKUP_AGE_HOURS" -gt "$BACKUP_MAX_AGE_HOURS" ]; then
        add_failure "database backup is ${BACKUP_AGE_HOURS}h old"
      fi
    fi
    ;;
esac

[ -n "$FAILURES" ] || exit 0
ALERT_TEXT="Kasanie: ${FAILURES} for ${BASE_URL}. Check the VPS logs."

if [ -n "$ALERT_WEBHOOK_URL" ]; then
  # Подходит для webhook, который принимает JSON с полем text (Slack/Make и аналоги).
  # Не передаём логи или персональные данные.
  curl --fail --silent --show-error --max-time 15 \
    -H 'Content-Type: application/json' \
    --data "{\"text\":\"${ALERT_TEXT}\"}" \
    "$ALERT_WEBHOOK_URL" || true
fi

if [ -n "$TELEGRAM_RELAY_URL" ] && [ -n "$TELEGRAM_RELAY_SHARED_SECRET" ]; then
  # Cloudflare Worker stores the BotFather token and chat ID as Worker Secrets.
  # The VPS receives an unrelated, route-specific relay secret only.
  curl --fail --silent --show-error --max-time 15 \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer ${TELEGRAM_RELAY_SHARED_SECRET}" \
    --data "{\"text\":\"${ALERT_TEXT}\"}" \
    "${TELEGRAM_RELAY_URL%/}/v1/notify" || true
elif [ -n "$TELEGRAM_RELAY_URL" ] || [ -n "$TELEGRAM_RELAY_SHARED_SECRET" ]; then
  echo "Cloudflare Telegram relay requires both KASANIE_TELEGRAM_RELAY_URL and KASANIE_TELEGRAM_RELAY_SHARED_SECRET." >&2
elif [ -n "$TELEGRAM_BOT_TOKEN" ] && [ -n "$TELEGRAM_CHAT_ID" ]; then
  # Не передаём логи, персональные данные или конфигурацию — только факт сбоя.
  curl --fail --silent --show-error --max-time 15 \
    --data-urlencode "chat_id=${TELEGRAM_CHAT_ID}" \
    --data-urlencode "text=${ALERT_TEXT}" \
    "https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/sendMessage" || true
elif [ -n "$TELEGRAM_BOT_TOKEN" ] || [ -n "$TELEGRAM_CHAT_ID" ]; then
  echo "Telegram alerts require both KASANIE_TELEGRAM_BOT_TOKEN and KASANIE_TELEGRAM_CHAT_ID." >&2
fi
exit 1
