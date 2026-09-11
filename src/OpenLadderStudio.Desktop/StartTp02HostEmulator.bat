@echo off
setlocal
cd /d "%~dp0"

set "EMU=OpenLadderTP02HostEmulator"

if not exist "%EMU%.exe" (
    call BuildTp02WbpWriter.bat
    if errorlevel 1 exit /b 1
)

if "%~1"=="" goto :uso

".\%EMU%.exe" --port=%~1
exit /b %ERRORLEVEL%

:uso
echo Uso:
echo   StartTp02HostEmulator.bat COM11
echo.
echo Conecte COM11 a outra COM virtual, por exemplo COM10.
echo Use o reader/writer na COM10. Nao conecte o emulador ao PLC fisico.
exit /b 2
