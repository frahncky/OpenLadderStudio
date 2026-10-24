@echo off
setlocal
cd /d "%~dp0"

echo.
echo ============================================================
echo  OpenLadder - Ensaio OFFLINE do roteiro de bancada TP02
echo ============================================================
echo  Nao abre porta COM e NAO usa PLC fisico. Cada quadro passa
echo  pela allowlist do validador fisico e e respondido em processo
echo  pelo emulador TP02 PG. Depois o analisador classifica os logs.
echo ============================================================
echo.

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
    echo .NET Framework 4.0 nao encontrado.
    exit /b 1
)

set "PROGRAMS=%~dp0..\..\docs\data\tp02-bancada-2026-10-26"
set "OUTDIR=%~dp0tp02-emulator-captures\bancada-ensaio"
if not "%~1"=="" set "OUTDIR=%~1"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02EmulatorRules.ps1" -Output "TP02PgEmulator.ensaio.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:exe /optimize+ /main:ModernPC12.TP02BancadaEnsaioProgram /out:"OpenLadderTP02BancadaEnsaio.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "..\OpenLadderStudio.Core\Tp02PgProtocol.cs" "..\OpenLadderStudio.Core\Tp02PgMemoryProtocol.cs" "..\OpenLadderStudio.Core\Tp02Pg34Pager.cs" "TP02PhysicalValidator.cs" "TP02PgEmulator.ensaio.build.cs" "TP02PgReadback.cs" "TP02PgV156Scenario.cs" "TP02PgReadbackSelfTest.cs" "TP02PgComprehensiveSelfTest.cs" "TP02BancadaEnsaio.cs"
if errorlevel 1 goto :erro
del /q "TP02PgEmulator.ensaio.build.cs" >nul 2>&1

if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
".\OpenLadderTP02BancadaEnsaio.exe" "--programs=%PROGRAMS%" "--out=%OUTDIR%"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0AnalyzeTp02BancadaFirmware.ps1" -SelfTest -EnsaioDir "%OUTDIR%" -ProgramsDir "%PROGRAMS%"
if errorlevel 1 goto :erro

echo.
echo Ensaio offline concluido. Logs: %OUTDIR%
exit /b 0

:erro
del /q "TP02PgEmulator.ensaio.build.cs" >nul 2>&1
echo.
echo Falha no ensaio offline. Nenhuma porta COM foi aberta.
exit /b 1
