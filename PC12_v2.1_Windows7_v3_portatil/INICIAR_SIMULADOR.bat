@echo off
setlocal
cd /d "%~dp0"

echo Atualizando OpenLadder Studio - Simulacao de processo...
call "BUILD_INTERFACE_MODERNA.bat"
if errorlevel 1 (
    echo.
    echo Nao foi possivel atualizar o simulador.
    if exist "OpenLadderSimulator.exe" (
        echo Abrindo a ultima versao compilada disponivel.
        start "" "%~dp0OpenLadderSimulator.exe"
        exit /b 0
    )
    exit /b 1
)

start "" "%~dp0OpenLadderSimulator.exe"
