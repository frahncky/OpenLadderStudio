@echo off
setlocal
cd /d "%~dp0"

echo.
echo ============================================================
echo  OpenLadder - Laboratorio TP02 100%% OFFLINE
echo ============================================================
echo  Este roteiro NAO abre porta COM e NAO usa PLC fisico.
echo  Ele compila o emulador e executa a matriz de protocolo.
echo ============================================================
echo.

call BuildTp02Emulator.bat
if errorlevel 1 goto :erro

OpenLadderTP02Emulator.exe --self-test-all
if errorlevel 1 goto :erro

echo.
echo Laboratorio offline concluido sem acesso serial.
exit /b 0

:erro
echo.
echo Falha no laboratorio offline. Nenhuma porta COM foi aberta pelo roteiro.
exit /b 1
