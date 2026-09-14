$ErrorActionPreference = 'Stop'

# V1.54 - corrige a ordem dos bytes START_H/START_L no readback PG34 multipagina.
#
# Evidencia fisica da v1.53 mostrou que o leitor gerado podia transmitir o
# endereco inicial com os bytes trocados em paginas acima de 0x00FF, embora o
# checksum continuasse valido. O codec canonico Tp02Pg34Pager ja implementa a
# ordem confirmada pelo PC12: 34 03 [step_hi] [step_lo] A0 chk.
#
# Esta correcao e estritamente de LEITURA. Nao adiciona PG33, restore, RUN,
# STOP remoto, Clear All, 0x09 ou WBP.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V154: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

$needle = '                byte[] request34 = Build34Request(startStep);'
$replacement = '                byte[] request34 = OpenLadderStudio.Core.Tp02Pg34Pager.BuildReadRequest(startStep);'
$count = [System.Text.RegularExpressions.Regex]::Matches(
    $shell, [System.Text.RegularExpressions.Regex]::Escape($needle)).Count
if ($count -ne 1) {
    throw "V154: chamada PG34 da v1.53 esperada exatamente uma vez; encontrado: $count."
}
$shell = $shell.Replace($needle, $replacement)

# Atualiza somente os marcadores de diagnostico da rotina multipagina.
$shell = $shell.Replace(' V153 PG34 ', ' V154 PG34 ')
$shell = $shell.Replace('PG34 PAGED READBACK v1.53', 'PG34 PAGED READBACK v1.54')
$shell = $shell.Replace('-pg34-paged-v153.txt', '-pg34-paged-v154.txt')
$shell = $shell.Replace('V153: porta PG fechada antes do readback multipagina.',
    'V154: porta PG fechada antes do readback multipagina.')

# Guarda de build: a rotina corrigida deve delegar a montagem do quadro ao
# pager canonico, que possui testes para 0, 80, 160, 240, 320 e fronteiras 16-bit.
$guard = 'OpenLadderStudio.Core.Tp02Pg34Pager.BuildReadRequest(startStep)'
$guardCount = [System.Text.RegularExpressions.Regex]::Matches(
    $shell, [System.Text.RegularExpressions.Regex]::Escape($guard)).Count
if ($guardCount -lt 1) {
    throw 'V154: guarda falhou; pager canonico PG34 nao ficou ligado ao readback.'
}

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG34 Endian Fix V154 aplicado: START_H/START_L via Tp02Pg34Pager; VERIFY permanece read-only.'
