@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
    echo .NET Framework 4.0 nao encontrado. O Laboratorio PG requer .NET 4.0 ou superior.
    exit /b 1
)

if not exist "Tp02PgLab.cs.in" (
    echo Tp02PgLab.cs.in nao encontrado.
    exit /b 1
)
if not exist "Tp02OpenAiAgent.cs" (
    echo Tp02OpenAiAgent.cs nao encontrado.
    exit /b 1
)
if not exist "Tp02PersistentOpenAiAgent.cs" (
    echo Tp02PersistentOpenAiAgent.cs nao encontrado.
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
if not exist "PreparePgLabSafetyV12.ps1" (
    echo PreparePgLabSafetyV12.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabCandidateV13.ps1" (
    echo PreparePgLabCandidateV13.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabRecoveryV14.ps1" (
    echo PreparePgLabRecoveryV14.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabMatrixV15.ps1" (
    echo PreparePgLabMatrixV15.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabUnifiedV16.ps1" (
    echo PreparePgLabUnifiedV16.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabPostHandshakeV17.ps1" (
    echo PreparePgLabPostHandshakeV17.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabAdaptiveAgentV18.ps1" (
    echo PreparePgLabAdaptiveAgentV18.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabContinuousResearchV19.ps1" (
    echo PreparePgLabContinuousResearchV19.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabF0RetryV20.ps1" (
    echo PreparePgLabF0RetryV20.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabReadSweepV21.ps1" (
    echo PreparePgLabReadSweepV21.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabDecodeV22.ps1" (
    echo PreparePgLabDecodeV22.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabCleanSessionV23.ps1" (
    echo PreparePgLabCleanSessionV23.ps1 nao encontrado.
    exit /b 1
)
if not exist "PreparePgLabHelloRetryV24.ps1" (
    echo PreparePgLabHelloRetryV24.ps1 nao encontrado.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateOpenLadderIcon.ps1"
if errorlevel 1 exit /b 1
if not exist "OpenLadderStudio.ico" exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -Command "$t=[IO.File]::ReadAllText('Tp02PgLab.cs.in'); $t=$t.Replace('            config.BringToFront();','').Replace('            header.BringToFront();',''); [IO.File]::WriteAllText('TP02PgLab.build.cs',$t,[Text.Encoding]::UTF8)"
if errorlevel 1 exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabCampaignV11.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabSafetyV12.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabCandidateV13.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabRecoveryV14.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabMatrixV15.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabUnifiedV16.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabPostHandshakeV17.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabAdaptiveAgentV18.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabContinuousResearchV19.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabF0RetryV20.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabReadSweepV21.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabDecodeV22.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabCleanSessionV23.ps1"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLabHelloRetryV24.ps1"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02PgLabProgram /out:"OpenLadderTP02PgLab.exe" /reference:System.dll /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "StudioDiagnostics.cs" "Tp02OpenAiAgent.cs" "Tp02PersistentOpenAiAgent.cs" "TP02PgLab.build.cs"
if errorlevel 1 goto :erro

del /q "TP02PgLab.build.cs" >nul 2>&1
echo OpenLadderTP02PgLab.exe criado com sucesso - motor PG Lab 1.14 com ate 6 HELLOs por sessao e um unico F0 por abertura da COM.
exit /b 0

:erro
del /q "TP02PgLab.build.cs" >nul 2>&1
exit /b 1
