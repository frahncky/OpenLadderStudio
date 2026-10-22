@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" goto :uso

set "PROJECT=%~1"
set "PORT=%~2"
set "MODE=%~3"
set "HEX=%TEMP%\OpenLadder-TP02-%RANDOM%-%RANDOM%.hex"

if not exist "OpenLadderTP02ProjectExport.exe" (
    echo OpenLadderTP02ProjectExport.exe nao encontrado. Execute BuildTp02WbpWriter.bat.
    exit /b 1
)
if not exist "OpenLadderTP02Wbp.exe" (
    echo OpenLadderTP02Wbp.exe nao encontrado. Execute BuildTp02WbpWriter.bat.
    exit /b 1
)

".\OpenLadderTP02ProjectExport.exe" "%PROJECT%" "%HEX%"
if errorlevel 1 goto :erro

if /I "%MODE%"=="WRITE" (
    if "%PORT%"=="" (
        echo Para WRITE informe a porta COM no segundo argumento.
        goto :erro
    )
    echo.
    echo === ESCRITA REAL SOLICITADA ===
    echo Porta: %PORT%
    ".\OpenLadderTP02Wbp.exe" --file="%HEX%" --port=%PORT% --write
    set "RC=%ERRORLEVEL%"
) else (
    echo.
    echo === DRY-RUN ===
    ".\OpenLadderTP02Wbp.exe" --file="%HEX%"
    set "RC=%ERRORLEVEL%"
)

del /q "%HEX%" >nul 2>&1
exit /b %RC%

:erro
del /q "%HEX%" >nul 2>&1
exit /b 1

:uso
echo Uso dry-run:
echo   TP02WriteProject.bat projeto.pladder
echo.
echo Uso escrita real:
echo   TP02WriteProject.bat projeto.pladder COM3 WRITE
echo.
echo A escrita real exige PSR=STOP e verifica cada bloco com RBP.
exit /b 2
