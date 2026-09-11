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

"%CSC%" /nologo /target:exe /optimize+ /out:"OpenLadderTP02Emulator.exe" /reference:System.dll "TP02PgEmulator.cs"
if errorlevel 1 exit /b 1

echo OpenLadderTP02Emulator.exe criado com sucesso.
echo Use uma porta COM virtual pareada com a porta configurada no PC12.
exit /b 0
