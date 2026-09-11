@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" goto :uso

set "PORT=%~1"
set "OUT=%~2"

if not exist "OpenLadderTP02Rbp.exe" (
    echo OpenLadderTP02Rbp.exe nao encontrado. Execute BuildTp02WbpWriter.bat.
    exit /b 1
)

if "%OUT%"=="" (
    ".\OpenLadderTP02Rbp.exe" --port=%PORT%
) else (
    ".\OpenLadderTP02Rbp.exe" --port=%PORT% --out="%OUT%"
)
exit /b %ERRORLEVEL%

:uso
echo Uso:
echo   TP02ReadProgram.bat COM3
echo   TP02ReadProgram.bat COM3 programa.hex
echo.
echo Le via Computer Link/RBP ate F-00 END ou passo 4000.
echo Pela MMI, PG/COM precisa estar LOW: pino 4 ligado ao pino 5.
exit /b 2