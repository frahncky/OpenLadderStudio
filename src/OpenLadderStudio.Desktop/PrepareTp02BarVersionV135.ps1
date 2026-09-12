$ErrorActionPreference = 'Stop'

# O sufixo de versao da barra TP02 era mantido por Replace encadeado: o V128
# injeta "v1.28" nos dois estados de modeText (ON-LINE e OFF-LINE) e cada versao
# seguinte trocava o literal anterior pelo seu. A corrente arrebentou:
#
#   V128 injeta v1.28
#   V131 troca v1.28 -> v1.31   aplicou
#   V132 troca v1.31 -> v1.32   aplicou
#   V133 troca v1.31 -> v1.33   NO-OP, o V132 ja havia mudado para v1.32
#   V134 troca v1.33 -> v1.34   NO-OP, v1.33 nunca existiu no texto
#
# Resultado: os binarios v1.33 e v1.34 mostram "v1.32" na barra TP02, e a v1.35
# mostraria o mesmo. Trocar mais um literal fixo apenas reporia a armadilha para
# a proxima versao, entao este passo passa a derivar o rotulo de version.txt,
# que o ValidateProject.ps1 ja define como fonte unica da versao.
#
# A expressao casa qualquer "|    vX.Y" seguido de fecha-string, independente de
# qual versao os passos anteriores deixaram ali, e a contagem e conferida: zero
# ocorrencia significa que a ancora do modeText mudou de forma, e o build para em
# vez de publicar um rotulo errado em silencio.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V135: UniversalStudioShell.build.cs nao encontrado.' }

$versionPath = Join-Path (Get-Location) 'version.txt'
if (-not (Test-Path -LiteralPath $versionPath)) { throw 'V135: version.txt nao encontrado.' }
$version = [System.IO.File]::ReadAllText($versionPath).Trim()
if ($version -notmatch '^\d+\.\d+(\.\d+)?$') { throw "V135: version.txt invalido: $version" }

$shell = [System.IO.File]::ReadAllText($shellPath)

$pattern = '(\s\|\s+)v\d+\.\d+(?:\.\d+)?(";)'
$matches = [System.Text.RegularExpressions.Regex]::Matches($shell, $pattern)
if ($matches.Count -lt 1) {
    throw 'V135: nenhum sufixo de versao encontrado no modeText da barra TP02. A ancora mudou; ajuste PrepareTp02BarVersionV135.ps1.'
}

$shell = [System.Text.RegularExpressions.Regex]::Replace($shell, $pattern, ('${1}v' + $version + '${2}'))

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("TP02 Bar Version V135 aplicado: " + $matches.Count.ToString() + " sufixo(s) de versao derivado(s) de version.txt = v" + $version + ".") -ForegroundColor Cyan
