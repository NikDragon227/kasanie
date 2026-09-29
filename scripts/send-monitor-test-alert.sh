#!/usr/bin/env sh
# Отправляет одно тестовое Telegram-уведомление. Запускать только вручную после настройки.
set -eu

ENV_FILE=${ENV_FILE:-/etc/kasanie-monitor.env}
test -r "$ENV_FILE" || { echo "Monitor environment file is not readable: $ENV_FILE" >&2; exit 1; }

# shellcheck disable=SC1090
. "$ENV_FILE"

: "${KASANIE_TELEGRAM_BOT_TOKEN:?KASANIE_TELEGRAM_BOT_TOKEN must be set}"
: "${KASANIE_TELEGRAM_CHAT_ID:?KASANIE_TELEGRAM_CHAT_ID must be set}"

curl --fail --silent --show-error --max-time 15 \
  --data-urlencode "chat_id=${KASANIE_TELEGRAM_CHAT_ID}" \
  --data-urlencode "text=Kasanie: monitoring is connected. Test alert." \
  "https://api.telegram.org/bot${KASANIE_TELEGRAM_BOT_TOKEN}/sendMessage"
echo "Telegram test alert sent."
