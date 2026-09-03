@echo off
setlocal
cd /d "%~dp0"

rem Tenta recompilar para garantir que a interface corresponde ao codigo atual.
rem Se a compilacao falhar, ainda usamos o ultimo executavel valido existente.
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
