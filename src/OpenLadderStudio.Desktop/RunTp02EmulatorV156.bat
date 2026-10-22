@echo off
setlocal
cd /d "%~dp0"

if not exist "OpenLadderTP02Emulator.exe" (
    call BuildTp02Emulator.bat
    if errorlevel 1 exit /b 1
)

set "PORT=%~1"
if not defined PORT (
    echo.
    echo Informe a porta VIRTUAL ligada ao OpenLadder, por exemplo COM11.
    set /p PORT=Porta virtual: 
)
if not defined PORT exit /b 1

echo.
echo ============================================================
echo  TP02 EMULATOR - CENARIO v1.56
echo ============================================================
echo  Seed inicial : 323 words, END=0322
echo  HELLO       : resposta somente na 5a tentativa
echo  F0          : resposta somente na 4a tentativa
echo  PG33 ACK    : 00 00 FF emulado
echo  Unknown ACK : OFF
echo  ATENCAO     : use SOMENTE par de COM virtual.
echo ============================================================
echo.

OpenLadderTP02Emulator.exe "%PORT%" --scenario=v156 --pg33-ack --no-auto-ack
exit /b %ERRORLEVEL%
