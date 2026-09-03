@echo off
setlocal
cd /d "%~dp0"

if not exist "PC12_Ladder.exe" (
    echo PC12 Ladder Studio ainda nao foi compilado.
    echo Compilando agora...
    call "BUILD_INTERFACE_MODERNA.bat"
    if errorlevel 1 exit /b 1
)

start "" "%~dp0PC12_Ladder.exe"
