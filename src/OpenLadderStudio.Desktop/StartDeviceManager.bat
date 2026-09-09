@echo off
setlocal
cd /d "%~dp0"

if exist "%~dp0Build.bat" call "%~dp0Build.bat"

if not exist "%~dp0OpenLadderDeviceManager.exe" (
    echo ERRO: OpenLadderDeviceManager.exe nao foi gerado.
    pause
    exit /b 1
)

start "" "%~dp0OpenLadderDeviceManager.exe"
