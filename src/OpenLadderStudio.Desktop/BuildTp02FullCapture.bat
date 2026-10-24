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
if not exist "PrepareTp02FullCaptureV159.ps1" exit /b 1
if not exist "PrepareTp02FullCaptureV160.ps1" exit /b 1
if not exist "PrepareTp02FullCaptureV161.ps1" exit /b 1
if not exist "PrepareTp02FullCaptureV162.ps1" exit /b 1
if not exist "..\OpenLadderStudio.Core\Tp02PgProtocol.cs" exit /b 1
if not exist "..\OpenLadderStudio.Core\Tp02PgMemoryProtocol.cs" exit /b 1
if not exist "..\OpenLadderStudio.Core\Tp02Pg34Pager.cs" exit /b 1

rem Build.bat pode ter normalizado este fonte no working tree. Para o Full Capture,
rem restauramos a versao imutavel do commit antes de aplicar os patches v1.59-v1.62.
where git >nul 2>&1
if not errorlevel 1 (
  echo ===== V162 DIAG: git show AcquirePort =====
  git show HEAD:src/OpenLadderStudio.Desktop/TP02FullProtocolCapture.cs | findstr /n /c:"AcquirePort" /c:"CaptureF0" /c:"Frame38"
  git show HEAD:src/OpenLadderStudio.Desktop/TP02FullProtocolCapture.cs > "TP02FullProtocolCapture.original.cs" 2>nul
  if not errorlevel 1 if exist "TP02FullProtocolCapture.original.cs" copy /y "TP02FullProtocolCapture.original.cs" "TP02FullProtocolCapture.cs" >nul
)

echo ===== V162 DIAG: working file after restore =====
findstr /n /c:"AcquirePort" /c:"CaptureF0" /c:"Frame38" "TP02FullProtocolCapture.cs"

echo ===== V162 DIAG: source hashes =====
certutil -hashfile "TP02FullProtocolCapture.cs" SHA256 | findstr /v /c:"hash" /c:"CertUtil"
if exist "TP02FullProtocolCapture.original.cs" certutil -hashfile "TP02FullProtocolCapture.original.cs" SHA256 | findstr /v /c:"hash" /c:"CertUtil"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02FullCaptureV159.ps1"
if errorlevel 1 goto :erro
if not exist "TP02FullProtocolCapture.build.cs" goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02FullCaptureV160.ps1"
if errorlevel 1 goto :erro
if not exist "TP02FullProtocolCapture.build.cs" goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02FullCaptureV161.ps1"
if errorlevel 1 goto :erro
if not exist "TP02FullProtocolCapture.build.cs" goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02FullCaptureV162.ps1"
if errorlevel 1 goto :erro
if not exist "TP02FullProtocolCapture.build.cs" goto :erro

"%CSC%" /nologo /target:exe /optimize+ /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02FullProtocolCaptureProgram /out:"OpenLadderTP02FullCapture.exe" /reference:System.dll /reference:System.Windows.Forms.dll "StudioDiagnostics.cs" "..\OpenLadderStudio.Core\Tp02PgProtocol.cs" "..\OpenLadderStudio.Core\Tp02PgMemoryProtocol.cs" "..\OpenLadderStudio.Core\Tp02Pg34Pager.cs" "TP02FullProtocolCapture.build.cs"
if errorlevel 1 goto :erro

".\OpenLadderTP02FullCapture.exe" --self-test
if errorlevel 1 goto :erro

del /q "TP02FullProtocolCapture.build.cs" >nul 2>&1
del /q "TP02FullProtocolCapture.original.cs" >nul 2>&1
echo OpenLadderTP02FullCapture.exe v1.62 criado e autoteste offline aprovado.
exit /b 0

:erro
del /q "TP02FullProtocolCapture.build.cs" >nul 2>&1
del /q "TP02FullProtocolCapture.original.cs" >nul 2>&1
exit /b 1
