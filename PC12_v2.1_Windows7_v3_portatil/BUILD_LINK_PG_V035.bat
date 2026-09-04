@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
if not defined CSC (
    echo Compilador C# do .NET Framework nao encontrado.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateOpenLadderIcon.ps1"
if errorlevel 1 goto :erro
if not exist "OpenLadderStudio.ico" goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePLCPlatformV16.ps1"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02PgLinkV35Program /out:"OpenLadderTP02PgLink.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "DockOrder.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCConnectionSettings.cs" "TP02PgFrameParserV33.cs" "TP02PgLinkV35.cs"
if errorlevel 1 goto :erro

del /q "PLCPlatform.build.cs" >nul 2>&1
echo.
echo OpenLadderTP02PgLink.exe criado com sucesso.
echo Execute INICIAR_LINK_PG_V035.bat para testar no TP02.
exit /b 0

:erro
del /q "PLCPlatform.build.cs" >nul 2>&1
echo.
echo Falha ao compilar o Link PG v0.35.
pause
exit /b 1
