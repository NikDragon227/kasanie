# Касание / Kasanie

Production-oriented MVP веб-платформы персонального футбольного развития: диагностика шести навыков, детерминированный недельный план, выполнение тренировок и прогресс. Игрок, тренер, родитель, региональный аналитик и администратор работают с одной серверной моделью и разными границами доступа.

> Нормативы в seed — демонстрационные и не являются научно валидированными. Перед обработкой реальных, особенно детских, персональных данных нужны формальная юридическая и ИБ-экспертиза.

## Архитектура

- React 19 + TypeScript + Vite + React Router;
- .NET 10 ASP.NET Core Minimal API, Identity cookie, EF Core;
- PostgreSQL 18;
- Nginx, Docker Compose, один Linux VPS;
- browser → Nginx → React и `/api` → ASP.NET Core → PostgreSQL.

Подробности: [архитектура](docs/ARCHITECTURE.md), [БД](docs/DATABASE.md), [безопасность](docs/SECURITY.md).

## Самый быстрый запуск

Требуется Docker Desktop (Windows) или Docker Engine + Compose (Linux).

```powershell
cd D:\AI_Workspace\01_Project\Kasanie
Copy-Item .env.example .env
docker compose up --build
```

Либо запустите `start-dev.cmd` / `./start-dev.ps1`. Откройте [http://localhost](http://localhost). Первый запуск скачает images, применит migration и заполнит Development demo data. Проверка: `curl.exe --fail http://localhost/health/ready`.

Если порт 80 занят, задайте `HTTP_PORT=8088` в `.env` и откройте `http://localhost:8088`.

## Demo users — только Development

Общий пароль: `Kasanie-Demo-2026!`

| Роль | Email |
|---|---|
| Игрок | `player@kasanie.local` |
| Тренер | `coach@kasanie.local` |
| Родитель | `parent@kasanie.local` |
| Региональный аналитик | `analyst@kasanie.local` |
| Администратор | `admin@kasanie.local` |

Эти predictable accounts создаются исключительно при `ASPNETCORE_ENVIRONMENT=Development`. В Production seed не запускается и default admin не существует. Для первого запуска задайте уникальные `BootstrapAdmin__Email` и `BootstrapAdmin__Password`, дождитесь создания администратора, затем немедленно удалите обе переменные и повторно примените Compose.

## Подтверждение email и восстановление пароля

Новые аккаунты подтверждают email до первого входа. В локальном `Development`, если `SMTP_HOST` пуст, ссылки подтверждения и восстановления безопасно выводятся только в лог API: `docker compose logs api`. В production заполните `APP_BASE_URL`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM` и при необходимости `SMTP_USE_SSL` в `.env`.

Тренеров, родителей, аналитиков и администраторов создают через «Администрирование → Пользователи»: система выдаёт одноразовую ссылку на самостоятельную установку пароля. Пока пароль не задан, администратор может выпустить новую ссылку; для заблокированной учётной записи перевыпуск запрещён. Ссылка действует 24 часа и содержит секретный Identity token — передавайте её только адресату. Для аналитика регион выбирается при приглашении. Пользователи не удаляются физически: администратор блокирует или разблокирует учётную запись.

## Разработка без Docker для приложения

Нужны Node.js 24+, npm и .NET 10 SDK; PostgreSQL можно оставить в Docker.

```powershell
docker compose up -d db

cd backend\Kasanie.Api
$env:ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=kasanie;Username=kasanie;Password=kasanie-dev'
dotnet restore
dotnet run

cd ..\..\frontend
npm install
npm run dev
```

Vite на `http://localhost:5173` проксирует `/api` и `/health` на `http://localhost:5080`.

## Миграции

Initial migration committed в `backend/Kasanie.Api/Infrastructure/Migrations`. API вызывает `MigrateAsync` при старте.

```powershell
cd backend\Kasanie.Api
dotnet ef migrations add FeatureName --output-dir Infrastructure/Migrations
dotnet ef database update
```

В production перед миграцией делайте backup.

## Тесты и сборка

```powershell
cd frontend
npm run lint
npm run test
npm run build

cd ..\backend
dotnet restore
dotnet build -c Release
dotnet test -c Release

cd ..
docker compose config
docker compose build
docker compose up -d
curl.exe --fail http://localhost/health
```

Полный E2E при работающем stack:

```powershell
cd frontend
npx playwright install chromium
npm run e2e
```

Фактические результаты последнего прогона: [PROJECT_STATUS.md](PROJECT_STATUS.md). Матрица: [docs/TESTING.md](docs/TESTING.md).

## Docker и данные

```powershell
docker compose ps
docker compose logs -f --tail=200
docker compose restart api
docker compose down          # volume и данные сохраняются
docker compose down -v       # удаляет БД: использовать только осознанно
```

PostgreSQL не публикует порт наружу. Named volumes `kasanie_pgdata` и `kasanie_keys` сохраняют БД и ключи cookie между рестартами.

## Конфигурация

Скопируйте `.env.example` в `.env`; `.env` исключён из Git.

- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` — БД;
- `ConnectionStrings__DefaultConnection` — direct/non-Compose workflow; Compose формирует строку из PostgreSQL variables;
- `ASPNETCORE_ENVIRONMENT` — `Development` или `Production`;
- `APP_DOMAIN`, `COOKIE_SECURE` — домен и secure-cookie;
- `ANALYTICS_MINIMUM_GROUP_SIZE` — порог подавления малых групп;
- `APP_BASE_URL`, `SMTP_*` — подтверждение email и восстановление пароля; без SMTP в Development ссылки пишутся в логи API.
- `BootstrapAdmin__Email`, `BootstrapAdmin__Password` — одноразовое production-создание первого Admin; оставить пустыми после bootstrap.

Не коммитьте `.env`, private keys, dumps и production credentials.

## Health checks

- `/health/live` — API-процесс запущен; применяется Docker для проверки самого контейнера.
- `/health/ready` — API и PostgreSQL готовы к работе; используйте для внешнего мониторинга.
- `/health` оставлен как совместимый alias readiness-проверки.

## Структура

```text
frontend/          React SPA, unit tests, Playwright
backend/
  Kasanie.Api/     API, domain, EF, Identity, migrations
  Kasanie.Tests/   xUnit
nginx/             local и TLS production configs
scripts/           pg_dump / pg_restore
docs/              architecture, DB, algorithm, security, deployment
compose*.yaml      local и production overlays
```

## Production

Пошаговая инструкция для Ubuntu, DNS, TLS, update и backup/restore: [docs/DEPLOY_VPS.md](docs/DEPLOY_VPS.md).
