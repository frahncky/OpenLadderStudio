@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC (
    echo .NET Framework 4.0 nao encontrado.
    exit /b 1
)

set "CORE=..\OpenLadderStudio.Core"
set "TESTS=..\..\tests\OpenLadderStudio.Core.Tests"

if not exist "%CORE%\LadderProject.cs" goto :faltando
if not exist "%CORE%\Tp02TargetCompiler.cs" goto :faltando
if not exist "%CORE%\Tp02LadderTargetCompiler.cs" goto :faltando
if not exist "%CORE%\Tp02ComputerLinkProgramCodec.cs" goto :faltando
if not exist "TP02WbpWriter.cs" goto :faltando
if not exist "TP02ProjectToWbpHex.cs" goto :faltando
if not exist "%TESTS%\Tp02ComputerLinkProgramCodecSelfTest.cs" goto :faltando

"%CSC%" /nologo /target:exe /optimize+ /out:"OpenLadderTP02WbpTest.exe" /reference:System.dll "%CORE%\Tp02TargetCompiler.cs" "%CORE%\Tp02ComputerLinkProgramCodec.cs" "%TESTS%\Tp02ComputerLinkProgramCodecSelfTest.cs"
if errorlevel 1 goto :erro

".\OpenLadderTP02WbpTest.exe"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:exe /optimize+ /main:ModernPC12.TP02WbpWriterProgram /out:"OpenLadderTP02Wbp.exe" /reference:System.dll "%CORE%\Tp02TargetCompiler.cs" "%CORE%\Tp02ComputerLinkProgramCodec.cs" "TP02WbpWriter.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:exe /optimize+ /main:ModernPC12.TP02ProjectToWbpHexProgram /out:"OpenLadderTP02ProjectExport.exe" /reference:System.dll "%CORE%\LadderProject.cs" "%CORE%\Tp02TargetCompiler.cs" "%CORE%\Tp02LadderTargetCompiler.cs" "TP02ProjectToWbpHex.cs"
if errorlevel 1 goto :erro

echo.
echo OpenLadderTP02Wbp.exe criado com sucesso.
echo OpenLadderTP02ProjectExport.exe criado com sucesso.
echo Padrao: DRY-RUN. Escrita real exige --write e PSR=STOP.
exit /b 0

:faltando
echo Arquivo fonte necessario nao encontrado.
exit /b 1

:erro
echo Falha no build/autoteste TP02 WBP.
exit /b 1
