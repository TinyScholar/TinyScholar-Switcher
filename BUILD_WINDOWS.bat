@echo off
setlocal
cd /d "%~dp0"
dotnet build Switcher.csproj -c Release -o dist
if errorlevel 1 (
  echo Build failed. Keep this window open to read the error.
  pause
  exit /b 1
)
start "" "dist"
