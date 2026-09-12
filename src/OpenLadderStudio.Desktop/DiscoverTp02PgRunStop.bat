@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo  OpenLadder - descoberta TP02 PG RUN / STOP
echo ============================================================
echo.
echo Esta rotina usa SOMENTE porta COM virtual com o emulador.
echo Nao conecte esta rotina diretamente ao PLC fisico.
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0DiscoverTp02PgRunStop.ps1" -Action BOTH %*
set "RC=%ERRORLEVEL%"

echo.
if not "%RC%"=="0" (
  echo Falha na captura RUN/STOP. Codigo %RC%.
) else (
  echo Captura RUN/STOP concluida.
)
echo.
pause
exit /b %RC%
