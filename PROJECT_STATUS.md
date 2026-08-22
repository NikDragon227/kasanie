# Статус проекта Kasanie

Обновлено: 22 августа 2026.

## Фаза 21 — быстрый журнал командной тренировки

- Тренер создаёт занятие для назначенной команды и выбирает от 1 до 8 упражнений из каталога.
- Состав фиксируется снимком на момент создания, поэтому история не зависит от последующих переводов игроков.
- Перед занятием тренер одним нажатием отмечает всех присутствующими и меняет только опоздавших/отсутствующих.
- После занятия открывается матрица «сделал / понял» по каждому присутствующему игроку и упражнению; положительные отметки выставлены по умолчанию, тренер фиксирует только исключения.
- Незавершённые данные сохраняются черновиком; завершение невозможно без полной посещаемости и всех обязательных отметок.
- Добавлены сводка по присутствующим и игрокам, требующим внимания, аудит и строгая проверка принадлежности команды тренеру.
- Миграция `AddTeamTrainingJournal` применена, Docker stack и новый интерфейс проверены без console errors.

## Фаза 20 — школы и команды

- Добавлены школы, членство с ролями владельца/администратора/тренера, команды, тренеры команд и составы игроков.
- Платформенный администратор создаёт школу и владельца по одноразовому приглашению, видит показатели и может заблокировать школу.
- Владелец школы управляет настройками, командами, приглашениями тренеров, назначениями и игроками в собственном кабинете.
- Кабинет тренера строится по назначенным командам; legacy-связь `CoachPlayerLink` больше не даёт доступ к игроку.
- Миграция `AddSchoolsAndTeams` сохраняет старые данные в школе «Касание Demo»; Development seed добавляет `owner@kasanie.local`.
- Docker stack пересобран, миграция применена к существующему volume без удаления данных; новые кабинеты проверены в браузере без console errors.
- Проверена изоляция двух школ: чужой владелец получает 403, тренер видит только состав своей команды.

## Фаза 11 — стабилизация MVP

- Docker stack пересобран и запущен: `db`, `api` и `web` имеют статус `healthy`.
- Проверена и сохранена корректировка EF Core для повторного сохранения/завершения тестирования: зависимые `AssessmentResult` удаляются через `DbSet.RemoveRange`, а существующие результаты обновляются на месте.
- Radar chart использует расширенный SVG `viewBox`; подписи шести навыков не накладываются на саму диаграмму.
- Пользователь вводит только город. Добавлен поиск по мере ввода, подсказки показывают город и регион, а в форму передаётся только выбранный город; муниципалитет и регион определяются сервером.
- Справочник Development seed расширен крупными городами России; endpoint `GET /api/reference/cities?q=...` возвращает не более 12 подходящих вариантов и не выгружает весь каталог для каждого ввода.
- Добавлен frontend unit-сценарий поиска города и выбора «Казань — Республика Татарстан».

## Фаза 12 — управление контентом для пилота

- Администратор может создавать, редактировать и деактивировать города/регионы, тренировочные программы и тесты с DEMO-нормами.
- Операции защищены Admin policy, валидируются на сервере и записываются в audit log.
- Деактивация используется вместо удаления: существующие профили, планы и результаты не теряют ссылочную целостность.
- В семейной форме создания ребёнка используется тот же поиск города, что и при регистрации и редактировании профиля.
- Добавлен маршрут `/admin/municipalities` и расширен браузерный сценарий управления городами и программами.

## Фаза 13 — восстановление доступа

- Регистрация создаёт неподтверждённый аккаунт и отправляет ссылку подтверждения email; вход до подтверждения заблокирован.
- Реализованы повторная отправка подтверждения, нейтральный запрос восстановления пароля и безопасная смена пароля по одноразовому Identity token.
- В Development ссылки пишутся только в API log при пустом `SMTP_HOST`; в production используется SMTP-конфигурация из environment variables.
- Добавлены страницы `/confirm-email`, `/forgot-password`, `/reset-password`.

## Фаза 14 — production operational readiness

- Backup создаёт SHA-256 manifest и проверяется `pg_restore --list` до объявления успешным.
- Restore проверяет checksum (если manifest есть) и читаемость dump до явного destructive confirmation.
- Добавлены `scripts/check-health.sh` для внешнего мониторинга и `scripts/renew-certificates.sh` для certbot deploy hook без остановки Nginx.
- VPS guide дополнен TLS renewal, проверкой backup и минимальным мониторингом.

## Фаза 15 — production preflight

- Добавлен `scripts/preflight-production.sh`: без вывода секретов проверяет обязательные environment variables, HTTPS URL, SMTP, production cookie, пароль БД, bootstrap Admin, TLS-файлы и итоговую Compose-конфигурацию.

## Последнее исправление запуска

- Устранено расхождение пароля существующего PostgreSQL volume и нового локального `.env`.
- `.env.example` теперь использует согласованный Development-only пароль `kasanie-dev`.
- `start-dev.ps1` автоматически обновляет старое значение шаблона `change-me-in-production` только в локальном `.env`.
- Данные и named volume сохранены; удаление/переинициализация БД не выполнялись.
- Повторная проверка: сайт HTTP 200, `/health` HTTP 200, `db`, `api` и `web` healthy.

## Завершено

- Полная структура React 19 / .NET 10 / PostgreSQL 18 / Nginx / Docker Compose.
- Identity cookie auth, CSRF, lockout, rate limit, роли и resource-level authorization.
- EF Core domain и committed migration `InitialCreate`; Development-only demo seed.
- Игрок: dashboard, профиль, продолжение теста, scoring, план, workout и progress.
- Тренер: связанные игроки, detail, notes, добавление/замена упражнения, программа.
- Родитель: связанные дети, создание профиля до 14 лет, просмотр и consent state.
- Регион: только агрегаты с подавлением малых групп; Admin exercise CRUD и reference views.
- Документация архитектуры, БД, алгоритма, безопасности, тестов и VPS deployment.
- Backup/restore scripts и Windows starters.
- Собраны Docker images, поднят stack, применена migration и заполнен seed.

## В работе

- Ручное развёртывание на конкретном VPS: домен/DNS, SMTP provider, сертификат и off-site backup target. Требуются внешние реквизиты и доступ к серверу.

## Осталось после MVP

- Научная валидация тестов и нагрузок.
- SMTP email confirmation/recovery и MFA.
- Полный CRUD UI для всех вторичных справочников (API и read views уже есть; exercise CRUD готов полностью).
- Formal security/legal review, external monitoring/SIEM и production admin bootstrap procedure.
- Подключение официального полного справочника населённых пунктов перед production-запуском (текущий расширенный каталог предназначен для MVP/demo).

## Известные вопросы

- Утверждённый `Kasanie_MVP.html` не был предоставлен; точное визуальное сравнение невозможно.
- На хосте установлен SDK .NET 9; .NET 10 build/test выполнены в официальном SDK container.
- Seed-нормы — DEMO и не научный стандарт.
- Каталог городов не является официальным реестром и не покрывает все населённые пункты.

## Выполненные команды

- `npm install --cache D:\AI_Workspace\.npm-cache`
- `npm run lint` — успешно.
- `npm run test` — 4/4 успешно.
- `npm run build` — успешно.
- .NET 10 container `dotnet test ... -c Release` — 10/10 успешно.
- `docker compose config` — успешно.
- `docker compose build` — успешно (API и web).
- `docker compose up -d --build` — успешно; db/api healthy, web started.
- HTTP smoke: `/health` 200, `/` 200, Identity login, `/api/me`, player dashboard.
- Persistence smoke: workout session 1, 4 exercises completed; API restarted; new login reports 1 completed session.
- Authorization smoke: unrelated coach player 403; unrelated parent child 403; Player→Admin 403.
- Analytics smoke: no `email` or `playerId` properties, threshold 3.
- Admin smoke: exercise id 13 created and read back.
- Under-14 direct registration: 422 parent-required behavior.
- `npm run e2e` — 5/5 Chromium scenarios passed after fixing the login-navigation race found by the first run.
- Фаза 11: `npm run lint` — успешно; `npm run test` — 6/6 успешно; `npm run build` — успешно; `npm run e2e` — 5/5 Chromium scenarios успешно.
- Фаза 11: `docker compose config --quiet`, `docker compose up -d --build`, `/health` и поиск городов (`Каз`, `Мос`) — успешно; все три контейнера healthy.
- Фаза 12: `npm run lint`, `npm run test` (6/6), `npm run build`, .NET xUnit (11/11), Docker rebuild и Playwright E2E (6/6) — успешно.
- Фаза 13: `npm run lint`, `npm run test` (6/6), `npm run build`, Docker rebuild, базовый E2E (6/6) и сценарий восстановления пароля (1/1) — успешно.
- Фаза 14: production Compose config — успешно; `/health` — HTTP 200; `db`, `api`, `web` healthy; shell-синтаксис operational scripts проверен в Alpine Linux container.
- Фаза 15: `preflight-production.sh` и остальные operational scripts прошли shell-проверку в Alpine; production Compose config — успешно. Текущий development `.env` корректно отклоняется preflight-проверкой.
- Фаза 16: разделены liveness и readiness endpoints; Docker API healthcheck использует `/health/live`, внешняя проверка — `/health/ready`. Docker rebuild, оба endpoint и все контейнеры healthy — успешно.
- Фаза 17: игрок может сохранить субъективную сложность и комментарий к каждому упражнению, а также общий комментарий по тренировке. `npm run lint`, Vitest (6/6), production frontend build и Docker rebuild — успешно.
- Фаза 18: тренер видит самооценку игрока в карточке — общий комментарий, комментарии к упражнениям и сложность. Vitest (7/7), backend xUnit (11/11), Docker build, production Compose config, оба health endpoint и Playwright E2E (8/8) — успешно.
- Фаза 19: E2E-фикстуры больше не загрязняют справочники: старые `E2E …` и `Smoke CRUD …` деактивированы, а новые тесты создают и деактивируют свои записи автоматически. Очистка через админ-интерфейс — успешно.

## Статус тестов

- Frontend lint: PASS.
- Frontend Vitest: PASS, 7 tests.
- Frontend build: PASS.
- Backend xUnit: PASS, 31 tests.
- Docker config/build/start: PASS.
- API/runtime smoke and persistence: PASS.
- Playwright E2E: PASS, 9 tests (fixture cleanup, Player persistence and feedback, Coach feedback view, Parent, RegionalAnalyst, Admin CRUD with cleanup, password recovery).
- Frontend city search: PASS, 1 дополнительный unit test.
- Admin content CRUD: PASS, Playwright создаёт город и программу через UI.
- Email/password recovery: PASS, Playwright verifies neutral recovery response; SMTP delivery itself requires production credentials.
