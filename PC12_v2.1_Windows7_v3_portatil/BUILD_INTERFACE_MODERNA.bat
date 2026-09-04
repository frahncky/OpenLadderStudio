@echo off
setlocal
cd /d "%~dp0"

echo ================================================
echo  PC12 Studio TP02 - compilacao das interfaces
echo ================================================
echo.

set "CSC="

if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"

if not defined CSC (
    echo ERRO: compilador C# do .NET Framework nao encontrado.
    echo.
    echo O PC12 original ainda pode ser iniciado normalmente pelo pc12.exe.
    echo Para usar as interfaces modernas, instale o .NET Framework 4.x no Windows 7.
    echo.
    pause
    exit /b 1
)

echo Preparando fonte compatível do Ladder...
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'LadderEditor.cs') -replace 'internal sealed class LadderCanvas : Control','internal sealed class LadderCanvas : ScrollableControl' | Set-Content 'LadderEditor.build.cs'"
if errorlevel 1 goto :erro

echo [1/8] Compilando central PC12 Modern...
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Moderno.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "ModernPC12.cs"
if errorlevel 1 goto :erro

echo [2/8] Compilando PC12 Ladder Studio...
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Ladder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "LadderEditor.build.cs"
if errorlevel 1 goto :erro

echo [3/8] Compilando TP02 Bridge Lab...
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Bridge_Lab.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02BridgeLab.cs"
if errorlevel 1 goto :erro

echo [4/8] Compilando leitor RBP...
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_RBP_Reader.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02ProgramReader.cs"
if errorlevel 1 goto :erro

echo [5/8] Compilando decodificador RBP para IL...
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Machine_Decoder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02MachineDecoder.cs"
if errorlevel 1 goto :erro

echo [6/8] Compilando calibracao automatica de opcodes...
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Opcode_Calibration.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02OpcodeCalibration.cs"
if errorlevel 1 goto :erro

echo [7/8] Compilando campanha guiada de calibracao...
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Calibration_Campaign.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02CalibrationCampaign.cs"
if errorlevel 1 goto :erro

echo [8/8] Compilando PC12 Studio unificado...
"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.UnifiedProgram /out:"PC12_Studio.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PC12Studio.cs" "ModernPC12.cs" "LadderEditor.build.cs" "TP02BridgeLab.cs" "TP02ProgramReader.cs" "TP02MachineDecoder.cs" "TP02OpcodeCalibration.cs" "TP02CalibrationCampaign.cs"
if errorlevel 1 goto :erro

del /q "LadderEditor.build.cs" >nul 2>&1

echo.
echo Interfaces criadas com sucesso:
echo   PC12_Studio.exe               ^(interface principal^)
echo   PC12_Moderno.exe              ^(central anterior^)
echo   PC12_Ladder.exe               ^(editor separado^)
echo   TP02_Bridge_Lab.exe           ^(bridge separado^)
echo   TP02_RBP_Reader.exe           ^(leitor de programa^)
echo   TP02_Machine_Decoder.exe      ^(decodificacao RBP para IL^)
echo   TP02_Opcode_Calibration.exe   ^(calibracao automatica de opcodes^)
echo   TP02_Calibration_Campaign.exe ^(campanha guiada de testes^)
echo.
exit /b 0

:erro
del /q "LadderEditor.build.cs" >nul 2>&1
echo.
echo ERRO: nao foi possivel compilar uma das interfaces modernas.
pause
exit /b 1
