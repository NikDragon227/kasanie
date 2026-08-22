# Аудит объектной авторизации API

Дата: 22 августа 2026. Проверены все маршруты `Kasanie.Api/Endpoints`, ролевые политики, запросы EF Core и негативные сценарии с подменой идентификаторов.

## Результат

| ID | Риск | До исправления | Решение |
|---|---|---|---|
| AUTH-01 | Критический | `RegionalAnalyst` видел агрегаты всей базы: пользователь не был связан с регионом | Регион хранится в серверной Identity claim `kasanie:analytics-region`; endpoint фильтрует игроков по claim и возвращает 403 при её отсутствии |
| AUTH-02 | Высокий | Верхнеуровневые метрики и дневной тренд могли раскрывать выборку меньше порога | Метрики подавляются по числу уникальных игроков; регион целиком скрывается ниже порога; разрез целиком скрывается, если остаток позволяет вывести малую группу |
| TEST-01 | Высокий | `dotnet test` молча пропускал проект без `IsTestProject` | Включён реальный test discovery; 33 теста выполняются, включая негативные HTTP-сценарии |

Прямых IDOR в маршрутах игрока, тренера и родителя не обнаружено. Идентификаторы ресурсов проверяются через владельца профиля либо активную связь с игроком.

## Матрица маршрутов

| Маршрут | Роль | Объектная проверка | Итог |
|---|---|---|---|
| `GET /api/auth/csrf` | Анонимно | Неприменимо | Допустимо |
| `POST /api/auth/register` | Анонимно | Создаёт только новый профиль текущей регистрации | Допустимо, rate limit |
| `POST /api/auth/login` | Анонимно | Только Identity account по email | Допустимо, rate limit и lockout |
| `POST /api/auth/resend-confirmation` | Анонимно | Нейтральный ответ независимо от наличия аккаунта | Допустимо, rate limit |
| `POST /api/auth/confirm-email` | Анонимно | Identity token привязан к user ID | Допустимо, rate limit |
| `POST /api/auth/forgot-password` | Анонимно | Нейтральный ответ; token отправляется владельцу email | Допустимо, rate limit |
| `POST /api/auth/reset-password` | Анонимно | Identity token привязан к аккаунту | Допустимо, rate limit |
| `POST /api/auth/change-password` | Любой вошедший | `UserManager.GetUserAsync(principal)`, проверка текущего пароля, обновление собственной cookie | Допустимо |
| `POST /api/auth/logout` | Любой вошедший | Только текущая cookie-сессия | Допустимо |
| `GET /api/me` | Любой вошедший | `UserManager.GetUserAsync(principal)` | Допустимо |
| `GET /api/reference/cities` | Анонимно | Публичный справочник без PII | Допустимо |
| `GET /api/player/dashboard` | Player | `OwnPlayerAsync` по `NameIdentifier` | Допустимо |
| `GET /api/player/profile` | Player | `OwnPlayerAsync` | Допустимо |
| `PUT /api/player/profile` | Player | Изменяется только `OwnPlayerAsync` | Допустимо |
| `GET /api/player/progress` | Player | Все запросы фильтруются по собственному `PlayerId` | Допустимо |
| `GET /api/player/development` | Player | `OwnPlayerAsync` по `NameIdentifier` | Допустимо |
| `GET /api/assessments/current` | Player | Сессия по собственному `PlayerId` | Допустимо |
| `PUT /api/assessments/draft` | Player | Draft по собственному `PlayerId` | Допустимо |
| `POST /api/assessments/submit` | Player | Сессия, snapshot и план создаются для собственного `PlayerId` | Допустимо |
| `GET /api/assessments/history` | Player | История по собственному `PlayerId` | Допустимо |
| `GET /api/training/plan` | Player | План и сессии по собственному `PlayerId` | Допустимо |
| `POST /api/training/days/{dayId}/start` | Player | День должен принадлежать плану собственного игрока | Допустимо |
| `GET /api/training/sessions/{sessionId}` | Player | Сессия должна иметь собственный `PlayerId` | Допустимо; чужая сессия даёт 404 |
| `PUT /api/training/sessions/{sessionId}/exercises/{trainingExerciseId}` | Player | Сессия принадлежит игроку, упражнение — дню этой сессии | Допустимо |
| `POST /api/training/sessions/{sessionId}/complete` | Player | Сессия фильтруется по собственному `PlayerId` | Допустимо |
| `GET /api/coach/catalog` | Coach | Общий неперсональный справочник | Допустимо |
| `GET /api/coach/players` | Coach | Только активные игроки команд, назначенных текущему тренеру | Допустимо |
| `GET /api/coach/players/{playerId}` | Coach | `CoachCanAccessAsync`, активная команда и школа | Допустимо; чужой игрок даёт 403 |
| `GET /api/coach/players/{playerId}/development` | Coach | `CoachCanAccessAsync`, активная команда и школа | Допустимо; чужой игрок даёт 403 |
| `GET/POST/PUT /api/coach/team-trainings[...]` | Coach | Каждая операция проверяет `TeamCoach`; player/exercise ID сверяются со снимком занятия | Допустимо; чужая команда даёт 403 |
| `/api/school/{schoolId}/...` | SchoolOwner/SchoolAdmin | Активное управляющее членство именно в указанной школе | Допустимо; чужая школа даёт 403 |
| `/api/admin/schools[...]` | Admin | Глобальное создание и блокировка школ, операции аудируются | Допустимо |
| `POST /api/coach/players/{playerId}/notes` | Coach | `CoachCanAccessAsync`; автор заметки берётся из текущего пользователя | Допустимо |
| `POST /api/coach/players/{playerId}/plan/exercises` | Coach | Активная связь + день активного плана указанного игрока | Допустимо |
| `PUT /api/coach/players/{playerId}/plan/exercises` | Coach | Активная связь + упражнение активного плана указанного игрока | Допустимо |
| `POST /api/coach/players/{playerId}/program` | Coach | Активная связь + активный план указанного игрока | Допустимо |
| `GET /api/parent/children` | Parent | Только `ParentPlayerLinks` текущего родителя | Допустимо |
| `POST /api/parent/children` | Parent | Новый ребёнок сразу связывается с текущим `ParentProfile` | Допустимо |
| `GET /api/parent/children/{playerId}` | Parent | `ParentCanAccessAsync` | Допустимо; чужой ребёнок даёт 403 |
| `GET /api/parent/children/{playerId}/development` | Parent | `ParentCanAccessAsync` по явной связи родитель–ребёнок | Допустимо; чужой ребёнок даёт 403 |
| `PUT /api/parent/children/{playerId}/consent` | Parent | `ParentCanAccessAsync`, меняется конкретная связь текущего родителя | Допустимо |
| `GET /api/analytics/overview` | RegionalAnalyst | Обязательная серверная claim региона + фильтр `Municipality.Region` + suppression | Исправлено AUTH-01/02 |
| `GET /api/admin/summary` | Admin | Глобальный доступ — назначение роли | Допустимо |
| `GET/POST/PUT/DELETE /api/admin/exercises[/{id}]` | Admin | Глобальный справочник — назначение роли, изменения аудируются | Допустимо |
| `GET/POST/PUT/DELETE /api/admin/assessments[/{id}]` | Admin | Глобальный справочник — назначение роли, изменения аудируются | Допустимо |
| `GET/POST/PUT/DELETE /api/admin/programs[/{id}]` | Admin | Глобальный справочник — назначение роли, изменения аудируются | Допустимо |
| `GET/POST/PUT/DELETE /api/admin/municipalities[/{id}]` | Admin | Глобальный справочник — назначение роли, изменения аудируются | Допустимо |
| `GET /api/admin/users` | Admin | Глобальное управление пользователями — назначение роли | Допустимо |
| `POST /api/admin/users` | Admin | Создаёт аккаунт без пароля, профиль/region claim по роли и 24-часовую Identity-ссылку; токен не аудируется | Добавлено |
| `POST /api/admin/users/{id}/invite` | Admin | Перевыпускает приглашение или создаёт ссылку сброса действующего незаблокированного аккаунта; события различаются, токен не аудируется | Добавлено |
| `PUT /api/admin/users/{id}/lock` | Admin | Блокирует без удаления и инвалидирует активную cookie; самоблокировка запрещена | Добавлено |
| `PUT /api/admin/users/{id}/roles` | Admin | Глобальное управление ролями; удаление роли аналитика удаляет region claim и инвалидирует cookie | Допустимо |
| `PUT /api/admin/users/{id}/analytics-region` | Admin | Регион только из активного справочника и только для RegionalAnalyst; старая cookie инвалидируется | Добавлено |
| `GET/POST /api/admin/coach-links` | Admin | Глобальное управление связями — назначение роли, изменения аудируются | Допустимо |

## Негативные HTTP-тесты

- тренер получает 403 при прямом URL несвязанного игрока;
- родитель получает 403 при прямом URL чужого ребёнка;
- игрок получает 404 при запросе чужой тренировочной сессии;
- аналитик без region claim получает 403;
- аналитик видит только игроков региона из claim;
- региональная выборка ниже privacy threshold возвращается полностью подавленной.

## Эксплуатационное требование

После назначения роли `RegionalAnalyst` администратор обязан вызвать `PUT /api/admin/users/{id}/analytics-region`. Изменение claim обновляет security stamp и завершает старую cookie-сессию; аналитик входит заново уже с новым регионом. API работает fail-closed: без claim аналитика не пустят в отчёт.
