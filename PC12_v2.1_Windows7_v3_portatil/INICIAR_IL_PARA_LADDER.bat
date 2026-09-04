@echo off
setlocal
cd /d "%~dp0"
call BUILD_INTERFACE_MODERNA.bat
if errorlevel 1 exit /b 1
start "" "TP02_IL_to_Ladder.exe"
