$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

function Test-DockerEngine {
  # `docker info` закономерно завершается с ненулевым кодом до старта Desktop.
  # Запускаем его через cmd, чтобы PowerShell с ErrorActionPreference=Stop не
  # превращал этот ожидаемый результат в исключение до запуска Docker Desktop.
  & cmd.exe /d /c "docker info >nul 2>&1"
  return $LASTEXITCODE -eq 0
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  throw 'Docker CLI не найден. Установите Docker Desktop и повторите запуск.'
}

if (-not (Test-DockerEngine)) {
  $dockerDesktopPath = 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
  if (-not (Test-Path -LiteralPath $dockerDesktopPath)) {
    throw 'Docker Engine не запущен, а Docker Desktop не найден в стандартной папке.'
  }

  Write-Host 'Docker Desktop не запущен. Запускаю и жду готовности...' -ForegroundColor Yellow
  Start-Process -FilePath $dockerDesktopPath -WindowStyle Hidden

  $dockerReady = $false
  for ($attempt = 1; $attempt -le 45; $attempt++) {
    Start-Sleep -Seconds 2
    if (Test-DockerEngine) {
      $dockerReady = $true
      break
    }
  }

  if (-not $dockerReady) {
    throw 'Docker Desktop не запустился за 90 секунд. Откройте его вручную и повторите запуск.'
  }
}

if (-not (Test-Path -LiteralPath '.env')) {
  Copy-Item -LiteralPath '.env.example' -Destination '.env'
  Write-Host 'Создан .env из примера. Значения предназначены только для локальной разработки.' -ForegroundColor Yellow
}
if ((Get-Content -LiteralPath '.env' -Raw) -match 'POSTGRES_PASSWORD=change-me-in-production') {
  Write-Host 'Обновляю старый демонстрационный пароль в локальном .env для совместимости с существующей БД.' -ForegroundColor Yellow
  $environmentText = (Get-Content -LiteralPath '.env' -Raw).Replace('POSTGRES_PASSWORD=change-me-in-production', 'POSTGRES_PASSWORD=kasanie-dev').Replace('Password=change-me-in-production', 'Password=kasanie-dev')
  Set-Content -LiteralPath '.env' -Value $environmentText -Encoding utf8
}

Write-Host 'Собираю и запускаю Kasanie...' -ForegroundColor Cyan
& docker compose up --detach --build
if ($LASTEXITCODE -ne 0) {
  throw "Docker Compose завершился с кодом $LASTEXITCODE."
}

Write-Host ''
Write-Host 'Kasanie запущен: http://localhost' -ForegroundColor Green
Write-Host 'Остановить проект: docker compose down' -ForegroundColor DarkGray
