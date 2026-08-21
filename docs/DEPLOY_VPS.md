# Развёртывание на Ubuntu VPS

## 1. Требования

Ubuntu 24.04 LTS, 2 vCPU, минимум 4 GB RAM, 30 GB SSD, домен, SSH с ключами. Для реальных данных предпочтительнее отдельный encrypted disk/volume и off-site backup target.

## 2. Docker и firewall

```bash
sudo apt update && sudo apt install -y ca-certificates curl git ufw certbot
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

Перезайдите в SSH после добавления в группу Docker.

## 3. Код и production environment

```bash
git clone <REPOSITORY_URL> /opt/kasanie
cd /opt/kasanie
cp .env.example .env
chmod 600 .env
```

В `.env` задайте уникальные `POSTGRES_PASSWORD`, одинаковый пароль внутри `ConnectionStrings__DefaultConnection`, `ASPNETCORE_ENVIRONMENT=Production`, `COOKIE_SECURE=true`, `APP_DOMAIN=kasanie.example.ru`, threshold аналитики. Для первого запуска задайте уникальные `BootstrapAdmin__Email` и случайный `BootstrapAdmin__Password` (16+ символов). Не копируйте development password в production и не включайте Development.

## 4. DNS и HTTPS

Направьте A/AAAA записи домена на VPS. До запуска production-конфига получите сертификат standalone (порт 80 должен быть свободен):

```bash
sudo certbot certonly --standalone -d kasanie.example.ru
sudo mkdir -p /opt/kasanie/certbot/certs
sudo cp -L /etc/letsencrypt/live/kasanie.example.ru/fullchain.pem /opt/kasanie/certbot/certs/fullchain.pem
sudo cp -L /etc/letsencrypt/live/kasanie.example.ru/privkey.pem /opt/kasanie/certbot/certs/privkey.pem
sudo chown -R "$USER":"$USER" /opt/kasanie/certbot
chmod 600 /opt/kasanie/certbot/certs/privkey.pem
```

Скрипт `scripts/renew-certificates.sh` копирует актуальные сертификаты и без остановки сайта reload’ит Nginx. Разрешите его выполнение и подключите как deploy hook:

```bash
chmod 750 scripts/*.sh
sudo certbot renew --dry-run --deploy-hook 'cd /opt/kasanie && APP_DOMAIN=kasanie.example.ru ./scripts/renew-certificates.sh'
```

## 5. Build, migrations и старт

```bash
cd /opt/kasanie
chmod 750 scripts/*.sh
./scripts/preflight-production.sh
docker compose -f compose.yaml -f compose.production.yaml config
docker compose -f compose.yaml -f compose.production.yaml build --pull
docker compose -f compose.yaml -f compose.production.yaml up -d
docker compose -f compose.yaml -f compose.production.yaml ps
curl --fail https://kasanie.example.ru/health
```

API применяет committed EF migrations при старте до readiness. Проверка: `docker compose logs api | grep -i migration`. Production не создаёт demo users.

После первого успешного входа Admin очистите `BootstrapAdmin__Email` и `BootstrapAdmin__Password` в `.env` и выполните `docker compose -f compose.yaml -f compose.production.yaml up -d --force-recreate api`. Bootstrap не содержит default credentials и повторно существующего пользователя не создаёт.

## 6. Логи, restart и обновление

```bash
docker compose logs -f --tail=200 api web db
docker compose restart api

git pull --ff-only
./scripts/backup-db.sh
docker compose -f compose.yaml -f compose.production.yaml build --pull
docker compose -f compose.yaml -f compose.production.yaml up -d --remove-orphans
curl --fail https://kasanie.example.ru/health
```

Перед обновлением прочитайте migration notes и обеспечьте достаточно места для старого и нового image.

## 7. Backup

```bash
BACKUP_DIR=/srv/kasanie-backups ./scripts/backup-db.sh
```

Скрипт создаёт рядом SHA-256 manifest и проверяет читаемость dump через `pg_restore --list` до сообщения об успехе.

Храните минимум ежедневные 14 дней, еженедельные 8 недель и ежемесячные 12 месяцев; шифруйте и отправляйте копию в другой failure domain. Backup внутри DB container недопустим. Ежемесячно проверяйте restore на изолированной машине.

## 8. Restore

Остановите входящий трафик/включите maintenance page, сохраните аварийный backup, затем:

```bash
./scripts/restore-db.sh /srv/kasanie-backups/kasanie-YYYYMMDDTHHMMSSZ.dump
curl --fail https://kasanie.example.ru/health
```

Скрипт требует явного ввода `RESTORE`, пересоздаёт только configured database и рестартует API.

## 9. Проверка состояния и мониторинг

Быстрая проверка приложения и всех трёх контейнеров:

```bash
KASANIE_BASE_URL=https://kasanie.example.ru ./scripts/check-health.sh
```

Запускайте её cron/systemd timer каждые 5 минут и направляйте ненулевой exit code в ваш мониторинг. Перед реальными данными настройте alert на HTTP health, срок TLS-сертификата, дисковое место, restart containers и неуспешный backup.
