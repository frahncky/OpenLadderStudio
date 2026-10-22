@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo  OpenLadder - TP02 PG Command Discovery
echo ============================================================
echo  SOMENTE PC12 original + par COM virtual.
echo  NAO conecte esta rotina ao PLC fisico.
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0DiscoverTp02PgCommands.ps1" -Rebuild
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo.
  echo Falha na descoberta. Codigo=%RC%
  exit /b %RC%
)

echo.
echo Descoberta concluida.
exit /b 0
