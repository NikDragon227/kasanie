#!/usr/bin/env bash
# Деплой Касания на прод. Кладёшь в /opt/kasanie/scripts/deploy.sh
# Делаешь исполняемым один раз:  chmod +x scripts/deploy.sh
# Запускаешь из /opt/kasanie:    ./scripts/deploy.sh
set -euo pipefail

cd /opt/kasanie

COMPOSE="docker compose -f compose.yaml -f compose.production.yaml"

echo "==> 1/5 Бэкап базы"
$COMPOSE exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > ~/kasanie-backup-$(date +%F-%H%M).sql
ls -lh ~/kasanie-backup-$(date +%F)*.sql | tail -1

echo "==> 2/5 Обновление кода"
git pull
echo "Текущий коммит: $(git log -1 --oneline)"

echo "==> 3/5 Сборка api и web"
$COMPOSE build api web

echo "==> 4/5 Перезапуск"
$COMPOSE up -d

echo "==> 5/5 Статус"
$COMPOSE ps

echo ""
echo "Готово. Логи api (выход — Ctrl+C):"
echo "  $COMPOSE logs -f api"
echo "Проверь глазами: https://prokasanie.ru — вход и новые разделы."
