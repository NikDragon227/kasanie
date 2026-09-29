#!/usr/bin/env sh
# Запускается по cron/systemd timer на VPS. При сбое отправляет короткое
# уведомление на webhook, если KASANIE_ALERT_WEBHOOK_URL задан в окружении.
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
BASE_URL=${KASANIE_BASE_URL:-https://prokasanie.ru}
ALERT_WEBHOOK_URL=${KASANIE_ALERT_WEBHOOK_URL:-}
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
exit 1
