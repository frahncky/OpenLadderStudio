@echo off
setlocal
cd /d "%~dp0"

rem Interface moderna ja compilada
if exist "%~dp0PC12_Moderno.exe" (
    start "" "%~dp0PC12_Moderno.exe"
    exit /b 0
)

rem Primeira execucao: tenta compilar automaticamente
if exist "%~dp0BUILD_INTERFACE_MODERNA.bat" (
    call "%~dp0BUILD_INTERFACE_MODERNA.bat"
)

if exist "%~dp0PC12_Moderno.exe" (
    start "" "%~dp0PC12_Moderno.exe"
    exit /b 0
)

rem Fallback seguro: abre o software legado diretamente
start "" "%~dp0pc12.exe"
