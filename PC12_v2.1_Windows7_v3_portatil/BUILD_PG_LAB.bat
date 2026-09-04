@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
    echo .NET Framework 4.0 nao encontrado. O Laboratorio PG requer .NET 4.0 ou superior.
    exit /b 1
)

if not exist "TP02PgLabSource.txt" (
    echo TP02PgLabSource.txt nao encontrado.
    exit /b 1
)
if not exist "TP02-PG-Tests.json" (
    echo TP02-PG-Tests.json nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabCampaignV11.ps1" (
    echo PreparePgLabCampaignV11.ps1 nao encontrado.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateOpenLadderIcon.ps1"
if errorlevel 1 exit /b 1
if not exist "OpenLadderStudio.ico" exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -Command "$t=[IO.File]::ReadAllText('TP02PgLabSource.txt'); $t=$t.Replace('            config.BringToFront();','').Replace('            header.BringToFront();',''); [IO.File]::WriteAllText('TP02PgLab.build.cs',$t,[Text.Encoding]::UTF8)"
if errorlevel 1 exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabCampaignV11.ps1"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02PgLabProgram /out:"OpenLadderTP02PgLab.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "StudioDiagnostics.cs" "TP02PgLab.build.cs"
if errorlevel 1 goto :erro

del /q "TP02PgLab.build.cs" >nul 2>&1
echo OpenLadderTP02PgLab.exe criado com sucesso - motor PG Lab 1.1.
exit /b 0

:erro
del /q "TP02PgLab.build.cs" >nul 2>&1
exit /b 1
