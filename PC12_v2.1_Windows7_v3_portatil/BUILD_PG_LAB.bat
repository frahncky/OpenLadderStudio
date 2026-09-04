@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
    echo .NET Framework 4.0 nao encontrado. O Laboratorio PG requer .NET 4.0 ou superior.
    exit /b 1
)

if not exist "TP02PgLab.cs" (
    echo TP02PgLab.cs nao encontrado.
    exit /b 1
)
if not exist "TP02-PG-Tests.json" (
    echo TP02-PG-Tests.json nao encontrado.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateOpenLadderIcon.ps1"
if errorlevel 1 exit /b 1
if not exist "OpenLadderStudio.ico" exit /b 1

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02PgLabProgram /out:"OpenLadderTP02PgLab.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "StudioDiagnostics.cs" "TP02PgLab.cs"
if errorlevel 1 exit /b 1

echo OpenLadderTP02PgLab.exe criado com sucesso.
exit /b 0
