# База данных

PostgreSQL 18 — единственное постоянное хранилище. Схема создаётся миграцией `InitialCreate` из `backend/Kasanie.Api/Infrastructure/Migrations`.

Основные группы таблиц:

- ASP.NET Identity: пользователи, роли, claims и связи;
- профили: `Players`, `ParentProfiles`, `CoachProfiles`, `ParentPlayerLinks`, `CoachPlayerLinks`, `Municipalities`;
- диагностика: определения, DEMO-нормы, сессии, результаты и исторические skill snapshots;
- тренировки: упражнения, программы, планы, дни, задания, сессии и результаты;
- достижения, заметки тренера и audit log.

## Миграции

Создать новую миграцию при установленном .NET 10 SDK:

```bash
cd backend/Kasanie.Api
dotnet ef migrations add MeaningfulName --output-dir Infrastructure/Migrations
```

Применение выполняется API при старте. Для контролируемого production rollout сначала сделайте backup и запустите новый контейнер API отдельно, проверив его логи.

## Данные разработки

Seed идемпотентно создаёт пользователей, муниципалитеты, 6 тестов, DEMO-нормы, 12 упражнений, программу, связи и историю. Production seed не запускается. DEMO-нормы нельзя трактовать как спортивный стандарт.

## Хранение и восстановление

Named volume монтируется в `/var/lib/postgresql`, как требуется официальным образом PostgreSQL 18. Backup создаётся снаружи контейнера в custom format `pg_dump`; см. `scripts/backup-db.sh` и `scripts/restore-db.sh`.

