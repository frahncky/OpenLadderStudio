@echo off
setlocal
cd /d "%~dp0"

if not exist "OpenLadderTP02PgLink.exe" (
    echo OpenLadderTP02PgLink.exe nao encontrado.
    echo Execute BUILD_LINK_PG_V038.bat primeiro.
    pause
    exit /b 1
)

start "" "OpenLadderTP02PgLink.exe"
exit /b 0
