#!/usr/bin/env sh
# Финальная техническая проверка перед тихим запуском.
# Не изменяет данные и не отправляет писем: только проверяет готовность окружения.
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
ENV_FILE=${ENV_FILE:-"$ROOT_DIR/.env"}
BASE_URL=${KASANIE_BASE_URL:-}

fail() { echo "Launch readiness failed: $1" >&2; exit 1; }
value() { sed -n "s/^$1=//p" "$ENV_FILE" | tail -n 1; }

test -f "$ENV_FILE" || fail ".env is missing"
if [ -z "$BASE_URL" ]; then
  BASE_URL=$(value APP_BASE_URL)
fi
case "$BASE_URL" in https://*) ;; *) fail "KASANIE_BASE_URL or APP_BASE_URL must use https://" ;; esac

ENV_FILE="$ENV_FILE" "$ROOT_DIR/scripts/preflight-production.sh"
KASANIE_BASE_URL="$BASE_URL" "$ROOT_DIR/scripts/check-health.sh"

METRIKA_ID=$(value VITE_YANDEX_METRIKA_ID)
case "$METRIKA_ID" in
  '') echo "Yandex Metrica: disabled" ;;
  *[!0-9]*) fail "VITE_YANDEX_METRIKA_ID must contain only digits" ;;
  *)
    curl --fail --silent --show-error --head "$BASE_URL/" | grep -qi 'content-security-policy:.*mc.yandex.ru' \
      || fail "CSP does not allow mc.yandex.ru"
    echo "Yandex Metrica: configured"
    ;;
esac

if command -v systemctl >/dev/null 2>&1; then
  systemctl is-enabled --quiet kasanie-monitor.timer \
    || fail "kasanie-monitor.timer is not enabled; run scripts/install-monitor-timer.sh"
  systemctl is-active --quiet kasanie-monitor.timer \
    || fail "kasanie-monitor.timer is not active"
fi

echo "Launch readiness passed."
