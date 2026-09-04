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
    echo PC12 Modern nao encontrado. Tentando compilar interface...
    call "%~dp0BUILD_INTERFACE_MODERNA.bat"
    echo.
)
else (
    echo BUILD_INTERFACE_MODERNA.bat nao foi encontrado.
    echo.
)

if exist "%~dp0PC12_Moderno.exe" (
    start "" "%~dp0PC12_Moderno.exe"
    exit /b 0
)

rem Fallback seguro: abre o software legado diretamente
if exist "%~dp0pc12.exe" (
    echo Interface moderna indisponivel. Iniciando pc12.exe...
    start "" "%~dp0pc12.exe"
    exit /b 0
)

echo ERRO: nem PC12_Moderno.exe nem pc12.exe foram encontrados nesta pasta.
pause
exit /b 4
