#!/usr/bin/env sh
# Запускается по cron/systemd timer на VPS. При сбое отправляет короткое
# уведомление на webhook или в Telegram при сбое healthcheck.
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
BASE_URL=${KASANIE_BASE_URL:-https://prokasanie.ru}
ALERT_WEBHOOK_URL=${KASANIE_ALERT_WEBHOOK_URL:-}
TELEGRAM_RELAY_URL=${KASANIE_TELEGRAM_RELAY_URL:-}
TELEGRAM_RELAY_SHARED_SECRET=${KASANIE_TELEGRAM_RELAY_SHARED_SECRET:-}
TELEGRAM_BOT_TOKEN=${KASANIE_TELEGRAM_BOT_TOKEN:-}
TELEGRAM_CHAT_ID=${KASANIE_TELEGRAM_CHAT_ID:-}
OUTPUT_FILE=$(mktemp)
trap 'rm -f "$OUTPUT_FILE"' EXIT

if KASANIE_BASE_URL="$BASE_URL" "$ROOT_DIR/scripts/check-health.sh" >"$OUTPUT_FILE" 2>&1; then
  exit 0
fi

cat "$OUTPUT_FILE" >&2
if [ -n "$ALERT_WEBHOOK_URL" ]; then
  # Подходит для webhook, который принимает JSON с полем text (Slack/Make и аналоги).
  # Не передаём логи или персональные данные.
  curl --fail --silent --show-error --max-time 15 \
    -H 'Content-Type: application/json' \
    --data "{\"text\":\"Kasanie: health check failed for ${BASE_URL}. Check the VPS logs.\"}" \
    "$ALERT_WEBHOOK_URL" || true
fi

if [ -n "$TELEGRAM_RELAY_URL" ] && [ -n "$TELEGRAM_RELAY_SHARED_SECRET" ]; then
  # Cloudflare Worker stores the BotFather token and chat ID as Worker Secrets.
  # The VPS receives an unrelated, route-specific relay secret only.
  curl --fail --silent --show-error --max-time 15 \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer ${TELEGRAM_RELAY_SHARED_SECRET}" \
    --data "{\"text\":\"Kasanie: health check failed for ${BASE_URL}. Check the VPS logs.\"}" \
    "${TELEGRAM_RELAY_URL%/}/v1/notify" || true
elif [ -n "$TELEGRAM_RELAY_URL" ] || [ -n "$TELEGRAM_RELAY_SHARED_SECRET" ]; then
  echo "Cloudflare Telegram relay requires both KASANIE_TELEGRAM_RELAY_URL and KASANIE_TELEGRAM_RELAY_SHARED_SECRET." >&2
elif [ -n "$TELEGRAM_BOT_TOKEN" ] && [ -n "$TELEGRAM_CHAT_ID" ]; then
  # Не передаём логи, персональные данные или конфигурацию — только факт сбоя.
  curl --fail --silent --show-error --max-time 15 \
    --data-urlencode "chat_id=${TELEGRAM_CHAT_ID}" \
    --data-urlencode "text=Kasanie: health check failed for ${BASE_URL}. Check the VPS logs." \
    "https://api.telegram.org/bot${TELEGRAM_BOT_TOKEN}/sendMessage" || true
elif [ -n "$TELEGRAM_BOT_TOKEN" ] || [ -n "$TELEGRAM_CHAT_ID" ]; then
  echo "Telegram alerts require both KASANIE_TELEGRAM_BOT_TOKEN and KASANIE_TELEGRAM_CHAT_ID." >&2
fi
exit 1
