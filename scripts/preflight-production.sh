#!/usr/bin/env sh
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
ENV_FILE=${ENV_FILE:-"$ROOT_DIR/.env"}
fail() { echo "Production preflight failed: $1" >&2; exit 1; }
value() { sed -n "s/^$1=//p" "$ENV_FILE" | tail -n 1; }

test -f "$ENV_FILE" || fail ".env is missing"
for KEY in POSTGRES_PASSWORD APP_DOMAIN APP_BASE_URL SMTP_HOST SMTP_USERNAME SMTP_PASSWORD SMTP_FROM; do
  VALUE=$(value "$KEY")
  test -n "$VALUE" || fail "$KEY must be set"
done

DB_PASSWORD=$(value POSTGRES_PASSWORD)

test "$(value ASPNETCORE_ENVIRONMENT)" = "Production" || fail "ASPNETCORE_ENVIRONMENT must be Production"
test "$(value COOKIE_SECURE)" = "true" || fail "COOKIE_SECURE must be true"
case "$(value APP_BASE_URL)" in https://*) ;; *) fail "APP_BASE_URL must use https://" ;; esac
test "$(value APP_DOMAIN)" != "localhost" || fail "APP_DOMAIN cannot be localhost"
test "$DB_PASSWORD" != "kasanie-dev" || fail "POSTGRES_PASSWORD uses the development value"
test ${#DB_PASSWORD} -ge 16 || fail "POSTGRES_PASSWORD must be at least 16 characters"

BOOTSTRAP_EMAIL=$(value BootstrapAdmin__Email)
BOOTSTRAP_PASSWORD=$(value BootstrapAdmin__Password)
if { test -n "$BOOTSTRAP_EMAIL" && test -z "$BOOTSTRAP_PASSWORD"; } || { test -z "$BOOTSTRAP_EMAIL" && test -n "$BOOTSTRAP_PASSWORD"; }; then
  fail "BootstrapAdmin email and password must be set together or both empty"
fi

test -r "$ROOT_DIR/certbot/certs/fullchain.pem" || fail "TLS fullchain.pem is missing"
test -r "$ROOT_DIR/certbot/certs/privkey.pem" || fail "TLS privkey.pem is missing"
cd "$ROOT_DIR"
docker compose -f compose.yaml -f compose.production.yaml config --quiet
echo "Production preflight passed."
