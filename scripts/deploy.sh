#!/usr/bin/env bash
# Деплой Касания на прод. Кладёшь в /opt/kasanie/scripts/deploy.sh
# Делаешь исполняемым один раз:  chmod +x scripts/deploy.sh
# Запускаешь из /opt/kasanie:    ./scripts/deploy.sh
set -euo pipefail

cd /opt/kasanie

COMPOSE="docker compose -f compose.yaml -f compose.production.yaml"

echo "==> 1/6 Бэкап базы"
$COMPOSE exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > ~/kasanie-backup-$(date +%F-%H%M).sql
ls -lh ~/kasanie-backup-$(date +%F)*.sql | tail -1

echo "==> 2/6 Обновление кода"
git pull
echo "Текущий коммит: $(git log -1 --oneline)"

echo "==> 3/6 Сборка api и web"
$COMPOSE build api web

echo "==> 4/6 Перезапуск (принудительное пересоздание контейнеров на свежих образах)"
$COMPOSE up -d --force-recreate api web

echo "==> 5/6 Очистка висячих образов"
docker image prune -f

echo "==> 6/6 Статус и проверка здоровья"
$COMPOSE ps
sleep 3
code=$(curl -fsS -o /dev/null -w '%{http_code}' http://localhost/health/ready || true)
if [ "$code" = "200" ]; then
  echo "health/ready: 200 OK"
else
  echo "!!! ВНИМАНИЕ: http://localhost/health/ready вернул '$code' — смотри логи api"
fi

echo ""
echo "Готово. Логи api (выход — Ctrl+C):"
echo "  $COMPOSE logs -f api"
echo "Проверь глазами: https://prokasanie.ru"
