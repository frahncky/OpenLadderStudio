@echo off
setlocal
cd /d "%~dp0"

if not exist "OpenLadderTP02HostEmulator.exe" (
    echo OpenLadderTP02HostEmulator.exe nao encontrado. Execute BuildTp02WbpWriter.bat.
    exit /b 1
)

if "%~1"=="" goto :uso

".\OpenLadderTP02HostEmulator.exe" --port=%~1
exit /b %ERRORLEVEL%

:uso
echo Uso:
echo   StartTp02HostEmulator.bat COM11
echo.
echo Conecte COM11 a outra COM virtual, por exemplo COM10.
echo Use o reader/writer na COM10. Nao conecte o emulador ao PLC fisico.
exit /b 2