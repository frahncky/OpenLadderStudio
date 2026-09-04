@echo off
setlocal
cd /d "%~dp0"

if exist "%~dp0BUILD_INTERFACE_MODERNA.bat" call "%~dp0BUILD_INTERFACE_MODERNA.bat"

if not exist "%~dp0PC12_Studio.exe" (
    echo ERRO: PC12_Studio.exe nao foi gerado.
    pause
    exit /b 1
)

start "" "%~dp0PC12_Studio.exe"
