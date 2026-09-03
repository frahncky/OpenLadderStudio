@echo off
setlocal
cd /d "%~dp0"

echo ================================================
echo  PC12 Studio TP02 - compilacao das interfaces
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

echo Preparando fonte compatível do Ladder...
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content 'LadderEditor.cs') -replace 'internal sealed class LadderCanvas : Control','internal sealed class LadderCanvas : ScrollableControl' | Set-Content 'LadderEditor.build.cs'"
if errorlevel 1 goto :erro

echo [1/4] Compilando central PC12 Modern...
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Moderno.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "ModernPC12.cs"
if errorlevel 1 goto :erro

echo [2/4] Compilando PC12 Ladder Studio...
"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Ladder.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "LadderEditor.build.cs"
if errorlevel 1 goto :erro

echo [3/4] Compilando TP02 Bridge Lab...
"%CSC%" /nologo /target:winexe /optimize+ /out:"TP02_Bridge_Lab.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "TP02BridgeLab.cs"
if errorlevel 1 goto :erro

echo [4/4] Compilando PC12 Studio unificado...
"%CSC%" /nologo /target:winexe /optimize+ /main:ModernPC12.UnifiedProgram /out:"PC12_Studio.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "PC12Studio.cs" "ModernPC12.cs" "LadderEditor.build.cs" "TP02BridgeLab.cs"
if errorlevel 1 goto :erro

del /q "LadderEditor.build.cs" >nul 2>&1

echo.
echo Interfaces criadas com sucesso:
echo   PC12_Studio.exe       ^(interface principal^)
echo   PC12_Moderno.exe      ^(central anterior^)
echo   PC12_Ladder.exe       ^(editor separado^)
echo   TP02_Bridge_Lab.exe   ^(bridge separado^)
echo.
exit /b 0

:erro
del /q "LadderEditor.build.cs" >nul 2>&1
echo.
echo ERRO: nao foi possivel compilar uma das interfaces modernas.
pause
exit /b 1
