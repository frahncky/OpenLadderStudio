@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
  echo .NET Framework 4.0 nao encontrado.
  exit /b 1
)

if not exist "TP02FullProtocolCapture.cs" exit /b 1
if not exist "..\OpenLadderStudio.Core\Tp02PgProtocol.cs" exit /b 1
if not exist "..\OpenLadderStudio.Core\Tp02PgMemoryProtocol.cs" exit /b 1
if not exist "..\OpenLadderStudio.Core\Tp02Pg34Pager.cs" exit /b 1

"%CSC%" /nologo /target:exe /optimize+ /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02FullProtocolCaptureProgram /out:"OpenLadderTP02FullCapture.exe" /reference:System.dll "StudioDiagnostics.cs" "..\OpenLadderStudio.Core\Tp02PgProtocol.cs" "..\OpenLadderStudio.Core\Tp02PgMemoryProtocol.cs" "..\OpenLadderStudio.Core\Tp02Pg34Pager.cs" "TP02FullProtocolCapture.cs"
if errorlevel 1 exit /b 1

".\OpenLadderTP02FullCapture.exe" --self-test
if errorlevel 1 exit /b 1

echo OpenLadderTP02FullCapture.exe criado e autoteste offline aprovado.
exit /b 0
