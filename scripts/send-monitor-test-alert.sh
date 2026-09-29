#!/usr/bin/env sh
# Отправляет одно тестовое Telegram-уведомление. Запускать только вручную после настройки.
set -eu

ENV_FILE=${ENV_FILE:-/etc/kasanie-monitor.env}
test -r "$ENV_FILE" || { echo "Monitor environment file is not readable: $ENV_FILE" >&2; exit 1; }

# shellcheck disable=SC1090
. "$ENV_FILE"

if [ -n "${KASANIE_TELEGRAM_RELAY_URL:-}" ] && [ -n "${KASANIE_TELEGRAM_RELAY_SHARED_SECRET:-}" ]; then
  curl --fail --silent --show-error --max-time 15 \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer ${KASANIE_TELEGRAM_RELAY_SHARED_SECRET}" \
    --data '{"text":"Kasanie: monitoring is connected. Test alert."}' \
    "${KASANIE_TELEGRAM_RELAY_URL%/}/v1/notify"
  echo "Telegram relay test alert sent."
  exit 0
fi

: "${KASANIE_TELEGRAM_BOT_TOKEN:?Configure the Cloudflare relay or KASANIE_TELEGRAM_BOT_TOKEN}"
: "${KASANIE_TELEGRAM_CHAT_ID:?KASANIE_TELEGRAM_CHAT_ID must be set}"

curl --fail --silent --show-error --max-time 15 \
  --data-urlencode "chat_id=${KASANIE_TELEGRAM_CHAT_ID}" \
  --data-urlencode "text=Kasanie: monitoring is connected. Test alert." \
  "https://api.telegram.org/bot${KASANIE_TELEGRAM_BOT_TOKEN}/sendMessage"
echo "Telegram test alert sent."
