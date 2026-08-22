# Архитектура Kasanie

Kasanie — модульный монолит для одного Linux VPS. Публичный origin обслуживает Nginx: статический React SPA и `/api/*`, проксируемый в ASP.NET Core. API хранит все прикладные данные в PostgreSQL; браузер не является источником истины.

```text
браузер → Nginx :80/:443 → React
                         ↘ ASP.NET Core API :8080 → PostgreSQL :5432
```

## Backend

- `Domain` — сущности, перечисления и роли;
- `Infrastructure` — EF Core context, миграции, Development-seed;
- `Application` — подсчёт оценки, генератор плана, проверки ресурсного доступа и аудит;
- `Endpoints` — DTO-oriented minimal REST API по контурам;
- `Kasanie.Tests` — xUnit для алгоритмов, возраста, ролей и границ school/coach/parent.

Аутентификация — ASP.NET Core Identity application cookie. Для меняющих запросов нужен antiforgery token. Ролевые политики дополняются ресурсной проверкой членства в школе и состава команды. Владелец управляет только своей школой; тренер получает игрока только через `TeamCoach → Team → TeamPlayer`. `DevelopmentSeeder` запускается только при `ASPNETCORE_ENVIRONMENT=Development`.

## Frontend

React 19 + TypeScript + React Router. `api.ts` всегда передаёт cookie и автоматически получает CSRF token. Route guards отвечают только за UX; источником решения о доступе остаётся API. Данные страниц загружаются заново с сервера и не сохраняются в localStorage.

## Эксплуатация

Контейнер API сам применяет EF migrations до начала приёма запросов. PostgreSQL и Data Protection keys размещены в named volumes. Горизонтальное масштабирование не входит в MVP; для одного VPS это уменьшает сложность эксплуатации.
