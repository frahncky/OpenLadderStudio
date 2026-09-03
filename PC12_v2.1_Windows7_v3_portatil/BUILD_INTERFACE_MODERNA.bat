@echo off
setlocal
cd /d "%~dp0"

echo ================================================
echo  PC12 Modern - compilacao das interfaces
echo ================================================
echo.

set "CSC="

if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"

if not defined CSC (
    echo ERRO: compilador C# do .NET Framework nao encontrado.
    echo.
    echo O PC12 original ainda pode ser iniciado normalmente pelo pc12.exe.
    echo Para usar as interfaces modernas, instale o .NET Framework 4.x no Windows 7.
    echo.
    pause
    exit /b 1
)

echo [1/2] Compilando central PC12 Modern...
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Moderno.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "ModernPC12.cs"
if errorlevel 1 goto :erro

echo [2/2] Compilando PC12 Ladder Studio...
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'LadderEditor.cs') -replace 'internal sealed class LadderCanvas : Control','internal sealed class LadderCanvas : ScrollableControl' | Set-Content 'LadderEditor.build.cs'"
if errorlevel 1 goto :erro
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Ladder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "LadderEditor.build.cs"
set "BUILDERR=%ERRORLEVEL%"
del /q "LadderEditor.build.cs" >nul 2>&1
if not "%BUILDERR%"=="0" goto :erro

echo.
echo Interfaces criadas com sucesso:
echo   PC12_Moderno.exe
echo   PC12_Ladder.exe
echo.
exit /b 0

:erro
del /q "LadderEditor.build.cs" >nul 2>&1
echo.
echo ERRO: nao foi possivel compilar uma das interfaces modernas.
pause
exit /b 1
