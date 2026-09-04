@echo off
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
if not defined CSC exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0GenerateOpenLadderIcon.ps1"
if errorlevel 1 goto :erro
if not exist "OpenLadderStudio.ico" goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'LadderEditor.cs') -replace 'internal sealed class LadderCanvas : Control','internal sealed class LadderCanvas : ScrollableControl' -replace 'PC12 Ladder Studio','OpenLadder Studio' -replace 'PC12 LADDER STUDIO','OPENLADDER STUDIO' | Set-Content 'LadderEditor.build.cs'"
if errorlevel 1 goto :erro

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePLCPlatformV16.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareUniversalStudioV18.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareMemoryMapV15.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareModbusMonitorV15.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareModbusMonitorV17.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareModbusMonitorV18.ps1"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /out:"OpenLadderUpdater.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PC12Updater.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /main:ModernPC12.DeviceManagerProgram /out:"OpenLadderDeviceManager.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCDeviceManagerV16.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /main:ModernPC12.MemoryMapManagerProgram /out:"OpenLadderMemoryMap.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCMemoryMapV15.cs" "PLCMemoryMapManagerV15.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /main:ModernPC12.ModbusMonitorProgramV18 /out:"OpenLadderModbus.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCConnectionSettings.cs" "PLCMemoryMapV15.cs" "PLCMemoryMapManagerV15.build.cs" "ModbusCore.cs" "ModbusBulkReader.cs" "ModbusTrendHistory.cs" "ModbusMonitorV18.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /main:ModernPC12.LadderProgram /out:"OpenLadderEditor.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "LadderEditor.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /main:ModernPC12.UniversalStudioProgram /out:"OpenLadderStudio.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "UniversalStudioShell.build.cs" "DockOrder.cs" "UniversalLadderAdapter.cs" "PC12Studio.cs" "ModernPC12.cs" "LadderEditor.build.cs" "TP02BridgeLab.cs" "TP02ProgramReader.cs" "TP02MachineDecoder.cs" "TP02OpcodeCalibration.cs" "TP02CalibrationCampaign.cs" "TP02AutoDecoder.cs" "TP02IlToLadder.cs" "PC12Updater.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCDeviceManagerV16.cs" "PLCConnectionSettings.cs" "PLCMemoryMapV15.cs" "PLCMemoryMapManagerV15.build.cs" "ModbusCore.cs" "ModbusBulkReader.cs" "ModbusTrendHistory.cs" "ModbusMonitorV18.build.cs"
if errorlevel 1 goto :erro

del /q "LadderEditor.build.cs" >nul 2>&1
del /q "PLCPlatform.build.cs" >nul 2>&1
del /q "UniversalStudioShell.build.cs" >nul 2>&1
del /q "PLCMemoryMapManagerV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV17.build.cs" >nul 2>&1
del /q "ModbusMonitorV18.build.cs" >nul 2>&1
exit /b 0

:erro
del /q "LadderEditor.build.cs" >nul 2>&1
del /q "PLCPlatform.build.cs" >nul 2>&1
del /q "UniversalStudioShell.build.cs" >nul 2>&1
del /q "PLCMemoryMapManagerV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV17.build.cs" >nul 2>&1
del /q "ModbusMonitorV18.build.cs" >nul 2>&1
exit /b 1
