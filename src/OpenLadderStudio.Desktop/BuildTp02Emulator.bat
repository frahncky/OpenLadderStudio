@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
    echo .NET Framework 4.0 nao encontrado.
    exit /b 1
)

if not exist "TP02PgEmulator.cs" (
    echo TP02PgEmulator.cs nao encontrado.
    exit /b 1
)
if not exist "PrepareTp02EmulatorRules.ps1" (
    echo PrepareTp02EmulatorRules.ps1 nao encontrado.
    exit /b 1
)
if not exist "TP02PgEmulatorRules.txt" (
    echo TP02PgEmulatorRules.txt nao encontrado.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02EmulatorRules.ps1"
if errorlevel 1 goto :erro
if not exist "TP02PgEmulator.build.cs" goto :erro

"%CSC%" /nologo /target:exe /optimize+ /out:"OpenLadderTP02Emulator.exe" /reference:System.dll "TP02PgEmulator.build.cs"
if errorlevel 1 goto :erro

del /q "TP02PgEmulator.build.cs" >nul 2>&1
echo OpenLadderTP02Emulator.exe criado com sucesso.
echo Regras externas: TP02PgEmulatorRules.txt
echo Use uma porta COM virtual pareada com a porta configurada no PC12.
exit /b 0

:erro
del /q "TP02PgEmulator.build.cs" >nul 2>&1
echo Falha ao preparar ou compilar o emulador TP02.
exit /b 1
