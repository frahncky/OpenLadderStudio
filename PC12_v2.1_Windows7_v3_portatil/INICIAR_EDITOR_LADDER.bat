@echo off
setlocal
cd /d "%~dp0"

echo Atualizando PC12 Ladder Studio...
call "BUILD_INTERFACE_MODERNA.bat"
if errorlevel 1 (
    echo.
    echo Nao foi possivel atualizar o Ladder Studio.
    if exist "PC12_Ladder.exe" (
        echo Abrindo a ultima versao compilada disponivel.
        start "" "%~dp0PC12_Ladder.exe"
        exit /b 0
    )
    exit /b 1
)

start "" "%~dp0PC12_Ladder.exe"
