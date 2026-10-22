$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02PgLab.build.cs'
if (-not (Test-Path $path)) { throw 'TP02PgLab.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)

# v1.18: a captura física TP02-PG-Lab-20260910-183546.txt confirmou no mesmo
# quadro 34 TMR, CNT, constantes, OUT C, F-23 SET, F-24 RST, F-13w ADD,
# operandos Y/D e F-00 END. A mesma regra de BRAW (soma de nibbles mod 16)
# fechou nos 23 passos ativos. O 38=2C coincidiu com 2*(23-1).
# Esta etapa apenas atualiza a apresentação do decoder já compilado no Core.
# Nenhum TX, escrita, RUN/STOP, download, erase ou firmware é adicionado.

if (-not $text.Contains('        private const string EngineVersion = "1.17";')) {
    throw 'EngineVersion 1.17 nao encontrado apos Decode34V27.'
}
$text = $text.Replace(
    '        private const string EngineVersion = "1.17";',
    '        private const string EngineVersion = "1.18";')

if (-not $text.Contains('"BRAW booleano OK "')) {
    throw 'Texto BRAW booleano do Decode34V27 nao encontrado.'
}
$text = $text.Replace('"BRAW booleano OK "', '"BRAW OK "')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.18 aplicado: decoder misto do 34 e BRAW geral nos tipos fisicamente confirmados; nenhum novo TX.'
