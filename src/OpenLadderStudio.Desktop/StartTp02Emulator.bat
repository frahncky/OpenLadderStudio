@echo off
setlocal
cd /d "%~dp0"

set "EMU=OpenLadderTP02Emulator"

if not exist "%EMU%.exe" (
    call BuildTp02Emulator.bat
    if errorlevel 1 exit /b 1
)

"%EMU%.exe" %*

if errorlevel 1 (
    echo.
    echo O emulador terminou com erro.
    pause
)
