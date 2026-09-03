@echo off
setlocal
cd /d "%~dp0"

call "BUILD_INTERFACE_MODERNA.bat"
if errorlevel 1 exit /b 1

start "" "%~dp0TP02_Bridge_Lab.exe"
