@echo off
setlocal
cd /d "%~dp0"

if not exist "OpenLadderTP02Emulator.exe" (
    call BuildTp02Emulator.bat
    if errorlevel 1 exit /b 1
)

OpenLadderTP02Emulator.exe %*

if errorlevel 1 (
    echo.
    echo O emulador terminou com erro.
    pause
)
