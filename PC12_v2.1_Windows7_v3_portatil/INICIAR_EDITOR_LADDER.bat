@echo off
setlocal
cd /d "%~dp0"

echo Atualizando OpenLadder Studio - Editor Ladder...
call "BUILD_INTERFACE_MODERNA.bat"
if errorlevel 1 (
    echo.
    echo Nao foi possivel atualizar o Editor Ladder.
    if exist "OpenLadderEditor.exe" (
        echo Abrindo a ultima versao compilada disponivel.
        start "" "%~dp0OpenLadderEditor.exe"
        exit /b 0
    )
    exit /b 1
)

start "" "%~dp0OpenLadderEditor.exe"
