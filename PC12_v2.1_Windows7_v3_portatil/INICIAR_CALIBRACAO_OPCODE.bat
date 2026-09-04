@echo off
setlocal
cd /d "%~dp0"
call "%~dp0BUILD_INTERFACE_MODERNA.bat"
if exist "%~dp0TP02_Opcode_Calibration.exe" start "" "%~dp0TP02_Opcode_Calibration.exe"
