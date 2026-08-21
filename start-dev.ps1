$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not (Test-Path -LiteralPath '.env')) {
  Copy-Item -LiteralPath '.env.example' -Destination '.env'
  Write-Host 'Создан .env из примера. Значения предназначены только для локальной разработки.' -ForegroundColor Yellow
}
if ((Get-Content -LiteralPath '.env' -Raw) -match 'POSTGRES_PASSWORD=change-me-in-production') {
  Write-Host 'Обновляю старый демонстрационный пароль в локальном .env для совместимости с существующей БД.' -ForegroundColor Yellow
  $environmentText = (Get-Content -LiteralPath '.env' -Raw).Replace('POSTGRES_PASSWORD=change-me-in-production', 'POSTGRES_PASSWORD=kasanie-dev').Replace('Password=change-me-in-production', 'Password=kasanie-dev')
  Set-Content -LiteralPath '.env' -Value $environmentText -Encoding utf8
}
docker compose up --build
