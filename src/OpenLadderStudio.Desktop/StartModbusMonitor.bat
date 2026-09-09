@echo off
setlocal
cd /d "%~dp0"

if exist "%~dp0Build.bat" call "%~dp0Build.bat"

if not exist "%~dp0OpenLadderModbus.exe" (
    echo ERRO: OpenLadderModbus.exe nao foi gerado.
    pause
    exit /b 1
)

start "" "%~dp0OpenLadderModbus.exe"
