@echo off
setlocal
cd /d "%~dp0"
echo TP02 - diagnostico somente de metadados do Windows ^(nao abre COM1^).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tp02DriverProbe.ps1" -Port COM1
if errorlevel 1 echo Falha ao consultar WMI. Envie apenas a mensagem de erro, sem IDs do dispositivo.
echo.
echo Se gerado, envie o arquivo TP02-Porta-COM1-diagnostico.txt.
pause
