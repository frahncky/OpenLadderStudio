@echo off
setlocal
cd /d "%~dp0"

call "%~dp0BUILD_INTERFACE_MODERNA.bat"

if exist "%~dp0TP02_RBP_Reader.exe" (
    start "" "%~dp0TP02_RBP_Reader.exe"
    exit /b 0
)

echo Nao foi possivel iniciar o leitor RBP.
pause
exit /b 1
