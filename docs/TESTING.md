# Тестирование

## Команды

```bash
cd frontend
npm run lint
npm run test
npm run build

cd ../backend
dotnet restore
dotnet build -c Release
dotnet test -c Release

cd ..
docker compose config
docker compose build
docker compose up -d
curl --fail http://localhost/health
```

Если на Windows нет .NET 10 SDK, backend suite можно выполнить в контейнере:

```powershell
docker run --rm -v "${PWD}:/src" -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 `
  dotnet test Kasanie.Tests/Kasanie.Tests.csproj -c Release
```

## Покрытые сценарии

- xUnit (33 теста): normalizer в обоих направлениях, границы score, приоритет плана, возраст, роли, admin policy, отсутствие PII, ресурсные проверки, региональная изоляция, privacy threshold, приглашения и парольные сценарии, создание школы с владельцем, изоляция двух школ, полный цикл командного журнала, расчёт профиля развития для трёх ролей и запрет доступа к чужой динамике;
- Vitest/RTL: валидация входа, поиск города с выбором подсказки, диапазон теста, загрузка dashboard, отметка и завершение тренировки, сохранение сложности и комментария к упражнению;
- Playwright: очистка технических E2E-фикстур, вход игрока, тесты/план/workout/reload, передача обратной связи от игрока тренеру, dashboards тренера/родителя/аналитика, создание и последующая деактивация упражнения, города и программы администратором, нейтральный запрос восстановления пароля.

E2E требуют работающего Docker stack и Chromium (`npx playwright install chromium`). В финальном локальном прогоне прошли все 9 сценариев. Результат конкретного прогона записан в `PROJECT_STATUS.md`.
