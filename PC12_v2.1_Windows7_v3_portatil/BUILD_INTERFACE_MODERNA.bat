@echo off
setlocal
cd /d "%~dp0"

echo ================================================
echo  PC12 Modern - compilacao da interface
echo ================================================
echo.

set "CSC="
set "SOURCE=ModernPC12.cs"

if not exist "%SOURCE%" (
    echo ERRO: arquivo-fonte %SOURCE% nao encontrado nesta pasta.
    echo.
    exit /b 2
)

call :setCompiler "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
call :setCompiler "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
call :setCompiler "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
call :setCompiler "%WINDIR%\Microsoft.NET\Framework\v2.0.50727\csc.exe"

if not defined CSC (
    echo ERRO: compilador C# do .NET Framework nao encontrado.
    echo.
    echo O PC12 original ainda pode ser iniciado normalmente pelo pc12.exe.
    echo Para usar a interface moderna, instale o .NET Framework 4.x no Windows 7.
    echo.
    pause
    exit /b 1
)

echo Compilador localizado:
echo %CSC%
echo.

"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Moderno.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "%SOURCE%"

if errorlevel 1 (
    echo.
    echo ERRO: nao foi possivel compilar a interface moderna.
    echo O iniciador principal ainda pode usar o pc12.exe como fallback.
    pause
    exit /b 3
)

echo.
echo Interface criada com sucesso: PC12_Moderno.exe
echo.
exit /b 0

:setCompiler
if defined CSC exit /b 0
if exist "%~1" set "CSC=%~1"
exit /b 0
