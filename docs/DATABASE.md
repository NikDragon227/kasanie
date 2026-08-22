# База данных

PostgreSQL 18 — единственное постоянное хранилище. Схема развивается committed-миграциями из `backend/Kasanie.Api/Infrastructure/Migrations`.

Основные группы таблиц:

- ASP.NET Identity: пользователи, роли, claims и связи;
- организации: `Schools`, `SchoolMemberships`, `Teams`, `TeamCoaches`, `TeamPlayers`;
- командный журнал: `TeamTrainings`, `TeamTrainingExercises`, `TeamTrainingAttendances`, `TeamTrainingPlayerResults`;
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

Seed идемпотентно создаёт школу «Касание Demo», владельца, основную команду, тренера, состав, пользователей, муниципалитеты, 6 тестов, DEMO-нормы, 12 упражнений, программу и историю. Миграция `AddSchoolsAndTeams` помещает ранее существовавших тренеров и игроков в демо-школу. Production seed не запускается.

## Хранение и восстановление

Named volume монтируется в `/var/lib/postgresql`, как требуется официальным образом PostgreSQL 18. Backup создаётся снаружи контейнера в custom format `pg_dump`; см. `scripts/backup-db.sh` и `scripts/restore-db.sh`.
