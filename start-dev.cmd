@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-dev.ps1"
if errorlevel 1 (
  echo.
  echo Kasanie failed to start. See the error above.
  pause
)
