@echo off
setlocal
cd /d "%~dp0"

if not exist "AnalyzeTp02Capture.ps1" (
    echo AnalyzeTp02Capture.ps1 nao encontrado.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0AnalyzeTp02Capture.ps1" %*
set "ERR=%ERRORLEVEL%"

echo.
if not "%ERR%"=="0" echo Falha na analise. Codigo: %ERR%
if "%ERR%"=="0" echo Analise concluida.
pause
exit /b %ERR%
