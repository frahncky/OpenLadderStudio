@echo off
setlocal
cd /d "%~dp0"

rem Interface unificada atual
if exist "%~dp0PC12_Studio.exe" (
    start "" "%~dp0PC12_Studio.exe"
    exit /b 0
)

rem Primeira execucao: compila automaticamente
if exist "%~dp0BUILD_INTERFACE_MODERNA.bat" (
    call "%~dp0BUILD_INTERFACE_MODERNA.bat"
)

if exist "%~dp0PC12_Studio.exe" (
    start "" "%~dp0PC12_Studio.exe"
    exit /b 0
)

rem Fallback para a central moderna anterior
if exist "%~dp0PC12_Moderno.exe" (
    start "" "%~dp0PC12_Moderno.exe"
    exit /b 0
)

rem Fallback final: software legado
start "" "%~dp0pc12.exe"
