# Резервное копирование БД

Дамп PostgreSQL в формате `custom`, проверяется `pg_restore --list` сразу после создания. Опционально: шифрование AES-256 и выгрузка в off-site хранилище с ротацией.

## Быстро (локально, без off-site)

```sh
./scripts/backup-db.sh                       # создаёт backups/kasanie-<UTC>.dump + .sha256
./scripts/restore-db.sh backups/kasanie-<UTC>.dump   # спросит подтверждение RESTORE
```

Без настройки скрипт хранит последние `BACKUP_KEEP_LOCAL` (по умолчанию 7) наборов, остальное локально подчищает.

## Off-site (прод)

### 1. rclone — Яндекс.Диск, бэкенд `yandex`

WebDAV Яндекса на бесплатном тарифе отключён (`402 WebDAV is not available for the free tariff`).
Используем **родной бэкенд `yandex`** (REST API Диска) — работает и на бесплатном аккаунте.
OAuth-токен получаем на машине с браузером и вписываем в конфиг на сервере руками
(`rclone config create ... config_token=` в rclone 1.75 уходит в интерактивную auth-машину и на headless не годится).

**На машине с браузером** (Windows: `winget install Rclone.Rclone`, затем новый терминал):

```sh
rclone authorize "yandex"
```

Вход под аккаунтом Диска → «Разрешить» → скопировать блок `{"access_token":...,"refresh_token":...,"expiry":...}`
между `--->` и `<---End paste`.

**На проде** — записать конфиг напрямую в файл:

```sh
apt install -y rclone
mkdir -p ~/.config/rclone
cat >> ~/.config/rclone/rclone.conf <<'EOF'

[yadisk]
type = yandex
token = {"access_token":"...","token_type":"bearer","refresh_token":"...","expiry":"..."}
EOF

rclone mkdir yadisk:kasanie-backups
rclone lsd yadisk:                       # каталог kasanie-backups виден, без 401/402
```

`refresh_token` rclone обновляет сам — cron не сломается. При компрометации токена: отозвать
в Яндекс ID → «Безопасность», заново `rclone authorize "yandex"`, заменить `token = ...` в конфиге.

### 2. Настройки

```sh
cp /opt/kasanie/scripts/backup-db.env.example ~/.kasanie-backup.env
openssl rand -base64 36          # → это значение BACKUP_PASSPHRASE, сразу в менеджер паролей
nano ~/.kasanie-backup.env       # BACKUP_PASSPHRASE=<из openssl>, BACKUP_REMOTE=yadisk:kasanie-backups
chmod 600 ~/.kasanie-backup.env
```

**`BACKUP_PASSPHRASE` храни отдельно от сервера** (менеджер паролей). Без неё зашифрованный бэкап не восстановить.

### 3. Проверка

```sh
/opt/kasanie/scripts/backup-db.sh
rclone ls <remote>:<путь>       # должен появиться kasanie-<UTC>.dump.enc + .sha256
```

### 4. Расписание (cron)

```sh
crontab -e
```
```
0 3 * * * cd /opt/kasanie && ./scripts/backup-db.sh >> /var/log/kasanie-backup.log 2>&1
```
Пароль и remote скрипт берёт из `~/.kasanie-backup.env` — в crontab секретов нет.

## Восстановление из зашифрованного

```sh
rclone copy <remote>:<путь>/kasanie-<UTC>.dump.enc ./restore/
rclone copy <remote>:<путь>/kasanie-<UTC>.dump.enc.sha256 ./restore/
# BACKUP_PASSPHRASE берётся из ~/.kasanie-backup.env
./scripts/restore-db.sh ./restore/kasanie-<UTC>.dump.enc
```

## Учебное восстановление (раз в месяц)

1. Скачать свежий off-site бэкап на отдельную машину / отдельный compose-стек (не прод).
2. `restore-db.sh` в тестовую БД, дождаться `Restore complete`.
3. `curl --fail http://localhost/health/ready`, зайти в интерфейс, проверить данные.
4. Записать дату и результат.

Резервная копия, которую ни разу не восстанавливали, не считается рабочей.
