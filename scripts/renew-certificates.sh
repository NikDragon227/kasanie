#!/usr/bin/env sh
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
DOMAIN=${APP_DOMAIN:?Set APP_DOMAIN before renewing certificates}
LIVE_DIR=${CERTBOT_LIVE_DIR:-"/etc/letsencrypt/live/$DOMAIN"}
TARGET_DIR="$ROOT_DIR/certbot/certs"

test -r "$LIVE_DIR/fullchain.pem"
test -r "$LIVE_DIR/privkey.pem"
mkdir -p "$TARGET_DIR"
cp -L "$LIVE_DIR/fullchain.pem" "$TARGET_DIR/fullchain.pem"
cp -L "$LIVE_DIR/privkey.pem" "$TARGET_DIR/privkey.pem"
chmod 600 "$TARGET_DIR/privkey.pem"
cd "$ROOT_DIR"
docker compose -f compose.yaml -f compose.production.yaml exec -T web nginx -s reload
echo "TLS certificate reloaded for $DOMAIN."
