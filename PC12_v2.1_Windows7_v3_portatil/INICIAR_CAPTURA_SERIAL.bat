@echo off
setlocal
cd /d "%~dp0"

echo Preparando o capturador serial PC12 / TP02...
call "BUILD_INTERFACE_MODERNA.bat"
if errorlevel 1 (
    echo.
    echo Nao foi possivel preparar o capturador.
    if exist "OpenLadderTP02Capture.exe" (
        echo Abrindo a ultima versao compilada disponivel.
        start "" "%~dp0OpenLadderTP02Capture.exe"
        exit /b 0
    )
    exit /b 1
)

start "" "%~dp0OpenLadderTP02Capture.exe"
