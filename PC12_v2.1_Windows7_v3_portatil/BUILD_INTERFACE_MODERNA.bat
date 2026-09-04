@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"

if not defined CSC (
    echo ERRO: compilador C# do .NET Framework nao encontrado.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'LadderEditor.cs') -replace 'internal sealed class LadderCanvas : Control','internal sealed class LadderCanvas : ScrollableControl' | Set-Content 'LadderEditor.build.cs'"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Moderno.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "ModernPC12.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Ladder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "LadderEditor.build.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Bridge_Lab.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02BridgeLab.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_RBP_Reader.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02ProgramReader.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Machine_Decoder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02MachineDecoder.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Opcode_Calibration.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02OpcodeCalibration.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Calibration_Campaign.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02CalibrationCampaign.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.TP02AutoDecoderProgram /out:"TP02_Auto_Decoder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02MachineDecoder.cs" "TP02AutoDecoder.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_IL_to_Ladder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02IlToLadder.cs"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Updater.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PC12Updater.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.DirectStudioProgram /out:"PC12_Studio.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PC12DirectStudio.cs" "PC12Studio.cs" "ModernPC12.cs" "LadderEditor.build.cs" "TP02BridgeLab.cs" "TP02ProgramReader.cs" "TP02MachineDecoder.cs" "TP02OpcodeCalibration.cs" "TP02CalibrationCampaign.cs" "TP02AutoDecoder.cs" "TP02IlToLadder.cs" "PC12Updater.cs"
if errorlevel 1 goto :erro

del /q "LadderEditor.build.cs" >nul 2>&1
exit /b 0

:erro
del /q "LadderEditor.build.cs" >nul 2>&1
exit /b 1
