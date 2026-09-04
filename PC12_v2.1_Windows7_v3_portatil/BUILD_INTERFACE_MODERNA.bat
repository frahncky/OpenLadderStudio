@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
if not defined CSC exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'LadderEditor.cs') -replace 'internal sealed class LadderCanvas : Control','internal sealed class LadderCanvas : ScrollableControl' -replace 'PC12 Ladder Studio','OpenLadder Studio' -replace 'PC12 LADDER STUDIO','OPENLADDER STUDIO' | Set-Content 'LadderEditor.build.cs'"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareUniversalStudioV13.ps1"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /out:"OpenLadderUpdater.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PC12Updater.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.DeviceManagerProgram /out:"OpenLadderDeviceManager.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PLCPlatform.cs" "PLCDeviceManager.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.MemoryMapManagerProgram /out:"OpenLadderMemoryMap.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PLCPlatform.cs" "PLCMemoryMap.cs" "PLCMemoryMapManager.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.ModbusMonitorProgramV13 /out:"OpenLadderModbus.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PLCPlatform.cs" "PLCConnectionSettings.cs" "ModbusCore.cs" "ModbusMonitorV13.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.LadderProgram /out:"OpenLadderEditor.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "LadderEditor.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.UniversalStudioProgram /out:"OpenLadderStudio.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "UniversalStudioShell.build.cs" "UniversalLadderAdapter.cs" "PC12Studio.cs" "ModernPC12.cs" "LadderEditor.build.cs" "TP02BridgeLab.cs" "TP02ProgramReader.cs" "TP02MachineDecoder.cs" "TP02OpcodeCalibration.cs" "TP02CalibrationCampaign.cs" "TP02AutoDecoder.cs" "TP02IlToLadder.cs" "PC12Updater.cs" "PLCPlatform.cs" "PLCDeviceManager.cs" "PLCConnectionSettings.cs" "PLCMemoryMap.cs" "PLCMemoryMapManager.cs" "ModbusCore.cs" "ModbusMonitorV13.cs"
if errorlevel 1 goto :erro

del /q "LadderEditor.build.cs" >nul 2>&1
del /q "UniversalStudioShell.build.cs" >nul 2>&1
exit /b 0

:erro
del /q "LadderEditor.build.cs" >nul 2>&1
del /q "UniversalStudioShell.build.cs" >nul 2>&1
exit /b 1
