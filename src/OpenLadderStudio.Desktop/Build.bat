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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareUniversalStudioV20.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareUniversalStudioV21.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02DryRunV83.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02ControlV30.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02ControlV31.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareStudioUiV20.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareStudioUiV21.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0NormalizeStudioIconEnumV51.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareUiV51.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareMemoryMapV15.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareModbusMonitorV15.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareModbusMonitorV17.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareModbusMonitorV18.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareAppBranding.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareAutoUpdaterV36.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareUpdateResumeV50.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareUpdateNotification.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLinkV38.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PreparePgLinkV39.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02ProgramTransferV90.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02Pg33ProbeV111.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02Pg33ProbeV113.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02Pg33ProbeV117.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02Pg33ChangeRestoreV118.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02Pg33LinkRecoveryV119.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02Pg33ProjectWriteV123.ps1"
if errorlevel 1 goto :erro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PrepareTp02Pg33VerifyV124.ps1"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /out:"OpenLadderUpdater.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "PC12Updater.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.DeviceManagerProgram /out:"OpenLadderDeviceManager.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCDeviceManagerV16.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.MemoryMapManagerProgram /out:"OpenLadderMemoryMap.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCMemoryMapV15.cs" "PLCMemoryMapManagerV15.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.ModbusMonitorProgramV18 /out:"OpenLadderModbus.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCConnectionSettings.cs" "PLCMemoryMapV15.cs" "PLCMemoryMapManagerV15.build.cs" "ModbusCore.cs" "ModbusBulkReader.cs" "ModbusTrendHistory.cs" "ModbusMonitorV18.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:exe /optimize+ /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.SimulationSelfTest /out:"OpenLadderSimTest.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "LadderSimulation.cs" "ProcessSimulation.cs" "SimulatedPlants.cs" "SimulationSelfTest.cs"
if errorlevel 1 goto :erro

".\OpenLadderSimTest.exe"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:exe /optimize+ /win32manifest:"OpenLadderStudio.manifest" /main:OpenLadderStudio.Core.Tests.LadderProjectCodecSelfTest /out:"OpenLadderCoreTest.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "..\..\src\OpenLadderStudio.Core\LadderProject.cs" "..\..\tests\OpenLadderStudio.Core.Tests\LadderProjectCodecSelfTest.cs"
if errorlevel 1 goto :erro

".\OpenLadderCoreTest.exe"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:exe /optimize+ /win32manifest:"OpenLadderStudio.manifest" /main:OpenLadderStudio.Core.Tests.Tp02ComputerLinkProgramCodecSelfTest /out:"OpenLadderTP02ComputerLinkTest.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "StudioDiagnostics.cs" "..\..\src\OpenLadderStudio.Core\Tp02TargetCompiler.cs" "..\..\src\OpenLadderStudio.Core\Tp02ComputerLinkProgramCodec.cs" "..\..\tests\OpenLadderStudio.Core.Tests\Tp02ComputerLinkProgramCodecSelfTest.cs"
if errorlevel 1 goto :erro

".\OpenLadderTP02ComputerLinkTest.exe"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:exe /optimize+ /win32manifest:"OpenLadderStudio.manifest" /main:Tp02PgProtocolSelfTest /out:"OpenLadderTP02PgProtocolTest.exe" /reference:System.dll "StudioDiagnostics.cs" "..\..\src\OpenLadderStudio.Core\Tp02PgProtocol.cs" "..\..\tests\OpenLadderStudio.Core.Tests\Tp02PgProtocolSelfTest.cs"
if errorlevel 1 goto :erro

".\OpenLadderTP02PgProtocolTest.exe"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.SimulatorProgram /out:"OpenLadderSimulator.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "LadderSimulation.cs" "ProcessSimulation.cs" "SimulatedPlants.cs" "LadderSimulator.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.LadderProgram /out:"OpenLadderEditor.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "..\..\src\OpenLadderStudio.Core\LadderProject.cs" "LadderEditor.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02PgLinkV39Program /out:"OpenLadderTP02PgLink.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "DockOrder.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCConnectionSettings.cs" "TP02PgFrameParserV33.cs" "TP02PgLinkV39.build.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.TP02SerialCaptureProgram /out:"OpenLadderTP02Capture.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "DockOrder.cs" "TP02SerialCapture.cs"
if errorlevel 1 goto :erro

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"OpenLadderStudio.ico" /win32manifest:"OpenLadderStudio.manifest" /main:ModernPC12.UniversalStudioProgram /out:"OpenLadderStudio.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "AppBranding.cs" "StudioDiagnostics.cs" "..\..\src\OpenLadderStudio.Core\LadderProject.cs" "..\..\src\OpenLadderStudio.Core\Tp02TargetCompiler.cs" "..\..\src\OpenLadderStudio.Core\Tp02LadderTargetCompiler.cs" "..\..\src\OpenLadderStudio.Core\Tp02ComputerLinkProgramCodec.cs" "..\..\src\OpenLadderStudio.Core\Tp02PgProtocol.cs" "UniversalStudioShell.build.cs" "DockOrder.cs" "StudioUi.build.cs" "UniversalLadderAdapter.cs" "PC12Studio.cs" "ModernPC12.cs" "LadderEditor.build.cs" "Tp02DryRunPreview.cs" "TP02ProgramTransferForm.cs" "TP02BridgeLab.cs" "TP02Control.cs" "TP02ControlV31.build.cs" "TP02PgCommandCatalog.cs" "TP02PgFrameParserV33.cs" "TP02PgLinkV34.cs" "TP02PgLinkV35.cs" "TP02PgLinkV37.cs" "TP02ProgramReader.cs" "TP02MachineDecoder.cs" "TP02OpcodeCalibration.cs" "TP02CalibrationCampaign.cs" "TP02AutoDecoder.cs" "TP02IlToLadder.cs" "PC12Updater.cs" "PLCPlatform.build.cs" "PLCCustomProfiles.cs" "PLCDeviceManagerV16.cs" "PLCConnectionSettings.cs" "PLCMemoryMapV15.cs" "PLCMemoryMapManagerV15.build.cs" "ModbusCore.cs" "ModbusBulkReader.cs" "ModbusTrendHistory.cs" "ModbusMonitorV18.build.cs" "LadderSimulation.cs" "ProcessSimulation.cs" "SimulatedPlants.cs" "LadderSimulator.cs"
if errorlevel 1 goto :erro

del /q "LadderEditor.build.cs" >nul 2>&1
del /q "PC12Updater.build.cs" >nul 2>&1
del /q "PLCDeviceManagerV16.build.cs" >nul 2>&1
del /q "PLCPlatform.build.cs" >nul 2>&1
del /q "UniversalStudioShell.build.cs" >nul 2>&1
del /q "TP02ControlV31.build.cs" >nul 2>&1
del /q "TP02PgLinkV38.build.cs" >nul 2>&1
del /q "TP02PgLinkV39.build.cs" >nul 2>&1
del /q "StudioUi.build.cs" >nul 2>&1
del /q "PLCMemoryMapManagerV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV17.build.cs" >nul 2>&1
del /q "ModbusMonitorV18.build.cs" >nul 2>&1
exit /b 0

:erro
del /q "LadderEditor.build.cs" >nul 2>&1
del /q "PC12Updater.build.cs" >nul 2>&1
del /q "PLCDeviceManagerV16.build.cs" >nul 2>&1
del /q "PLCPlatform.build.cs" >nul 2>&1
del /q "UniversalStudioShell.build.cs" >nul 2>&1
del /q "TP02ControlV31.build.cs" >nul 2>&1
del /q "TP02PgLinkV38.build.cs" >nul 2>&1
del /q "TP02PgLinkV39.build.cs" >nul 2>&1
del /q "StudioUi.build.cs" >nul 2>&1
del /q "PLCMemoryMapManagerV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV15.build.cs" >nul 2>&1
del /q "ModbusMonitorV17.build.cs" >nul 2>&1
del /q "ModbusMonitorV18.build.cs" >nul 2>&1
exit /b 1
