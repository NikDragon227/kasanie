#!/usr/bin/env sh
# Устанавливает systemd timer мониторинга на VPS. Запускать от root.
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)

test "$(id -u)" = "0" || { echo "Run this script as root." >&2; exit 1; }
test -f /etc/kasanie-monitor.env || {
  echo "Create /etc/kasanie-monitor.env first (see docs/DEPLOY_VPS.md)." >&2
  exit 1
}

install -m 0644 "$ROOT_DIR/scripts/systemd/kasanie-monitor.service" /etc/systemd/system/kasanie-monitor.service
install -m 0644 "$ROOT_DIR/scripts/systemd/kasanie-monitor.timer" /etc/systemd/system/kasanie-monitor.timer
systemctl daemon-reload
systemctl enable --now kasanie-monitor.timer
systemctl is-enabled --quiet kasanie-monitor.timer
systemctl is-active --quiet kasanie-monitor.timer
echo "Kasanie monitor timer is enabled."
