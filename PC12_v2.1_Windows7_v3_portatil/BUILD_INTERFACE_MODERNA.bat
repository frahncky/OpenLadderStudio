@echo off
setlocal
cd /d "%~dp0"

echo ================================================
echo  PC12 Modern - compilacao da interface
echo ================================================
echo.

set "CSC="

if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"

if not defined CSC (
    echo ERRO: compilador C# do .NET Framework nao encontrado.
    echo.
    echo O PC12 original ainda pode ser iniciado normalmente pelo pc12.exe.
    echo Para usar a interface moderna, instale o .NET Framework 4.x no Windows 7.
    echo.
    pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /out:"PC12_Moderno.exe" /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "ModernPC12.cs"

if errorlevel 1 (
    echo.
    echo ERRO: nao foi possivel compilar a interface moderna.
    pause
    exit /b 1
)

echo.
echo Interface criada com sucesso: PC12_Moderno.exe
echo.
exit /b 0
