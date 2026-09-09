$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02PgLab.build.cs'
if (-not (Test-Path $path)) { throw 'TP02PgLab.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$source, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $source.Contains($needle)) { throw "$label nao encontrado." }
    return $source.Replace($needle, $replacement)
}

# O motor 1.1 bloqueava F0 00 0F por precaucao enquanto sua semantica era desconhecida.
# A analise estatica do PC12 fechou o uso de F0 00 0F como consulta de status/preflight
# de conexao. Ele so pode ser transmitido pela classe READ_ONLY_VERIFIED, que exige:
# 1) autorizacao manual do operador; e
# 2) presenca explicita na readOnlyAllowlist do pacote.
# O comando 0F 00 F0 (Clear All Memory) permanece bloqueado internamente.
$text = Replace-Required $text `
    '            return n == "0F 00 F0" || n == "F0 00 0F";' `
    '            return n == "0F 00 F0";' `
    'Bloqueio interno F0/0F'

$text = Replace-Required $text `
    '        private const string EngineVersion = "1.1";' `
    '        private const string EngineVersion = "1.2";' `
    'EngineVersion 1.1'

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab safety gate 1.2 aplicado: F0 somente via READ_ONLY_VERIFIED; 0F permanece bloqueado.'
