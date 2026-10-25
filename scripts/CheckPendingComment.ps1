# Confere, numa janela de verdade, que o menu Arquivo e a barra de ferramentas nao
# perdem um comentario de linha ainda pendente no painel PROPRIEDADES.
# Compila scripts/CheckPendingComment.cs num diretorio temporario (nenhum .exe fica
# no repositorio) e o roda contra o OpenLadderStudio.exe ja compilado.
# Uso: powershell -ExecutionPolicy Bypass -File scripts/CheckPendingComment.ps1
# Saida 0 = "Todas as verificacoes passaram". Precisa de sessao de desktop; nao roda no CI.
param(
    [string]$Exe
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Exe) { $Exe = Join-Path $here '..\src\OpenLadderStudio.Desktop\OpenLadderStudio.exe' }
if (-not (Test-Path -LiteralPath $Exe)) { throw "Executavel nao encontrado: $Exe. Rode o Build.bat antes." }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw "csc.exe nao encontrado: $csc" }

$work = Join-Path ([IO.Path]::GetTempPath()) ('openladder-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $out = Join-Path $work 'CheckPendingComment.exe'
    & $csc /nologo /target:exe /out:$out /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $here 'CheckPendingComment.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar CheckPendingComment.cs.' }

    & $out (Resolve-Path -LiteralPath $Exe).Path
    $code = $LASTEXITCODE
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
exit $code
