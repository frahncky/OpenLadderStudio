param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $root = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'OpenLadder Studio\TP02 PG34 Boundary Test'
    $OutputPath = Join-Path $root 'pg34-boundary-323.pladder'
}

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$rows = New-Object 'System.Collections.Generic.List[string]'
$rows.Add('PC12-LADDER|2')
$empty = 'EMPTY~EMPTY'

# 161 rungs x 2 machine words = 322 words.
# Each rung uses only X0001 and internal relay C0001; no physical Y output is referenced.
for ($rung = 1; $rung -le 161; $rung++) {
    $cells = @($empty, $empty, $empty, $empty, $empty, $empty, $empty, $empty)
    $cells[0] = 'NO:X0001~EMPTY'
    $cells[7] = 'COIL:C0001~EMPTY'
    $rows.Add('RUNG|' + ($cells -join '|'))
}

# Final word: F-00 END at global step 322 (0x0142).
$endCells = @($empty, $empty, $empty, $empty, $empty, $empty, $empty, $empty)
$endCells[7] = 'END~EMPTY'
$rows.Add('RUNG|' + ($endCells -join '|'))

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$text = ($rows -join "`r`n") + "`r`n"
[System.IO.File]::WriteAllText($OutputPath, $text, $utf8NoBom)

$expectedPath = [System.IO.Path]::ChangeExtension($OutputPath, '.expected.txt')
$expected = @'
TP02 PG34 BOUNDARY TEST — EXPECTED
===================================
Projeto: 161 rungs STR X0001 -> OUT C0001 + F-00 END
Saidas fisicas Y: nenhuma
Machine words esperadas: 323
END global: 322 decimal = 0x0142

Padrao de words:
- passos pares 0..320 : 00 10 / BRAW 01 => 001001  (STR X0001)
- passos impares 1..321: 40 40 / BRAW 08 => 404008  (OUT C0001)
- passo 322           : 00 70 / BRAW 07 => 007007  (F-00 END)

Paginas PG34 esperadas:
start 0   / 0x0000 -> 34 03 00 00 A0 28
start 80  / 0x0050 -> 34 03 00 50 A0 D8
start 160 / 0x00A0 -> 34 03 00 A0 A0 88
start 240 / 0x00F0 -> 34 03 00 F0 A0 38
start 320 / 0x0140 -> 34 03 01 40 A0 E7

Criterios principais:
1. page-0000.hex ... page-0320.hex validos, LEN=F0 e checksum FF.
2. A transicao start 0x0000 -> 0x0050 prova a segunda pagina.
3. A transicao start 0x00F0 -> 0x0140 prova a troca do byte alto START_H.
4. read-summary.txt deve indicar 5 paginas, 323 passos, END 0322, BRAW 0 e UNKNOWN 0.

SEGURANCA
O gerador e offline e nao transmite nada. Gravar este projeto no PLC substitui temporariamente
o programa existente; faca backup antes, mantenha o equipamento em condicao segura/STOP e restaure
o programa original ao final. O projeto nao referencia saidas Y, mas nao deve ser colocado em RUN
em uma maquina/processo real sem isolamento adequado.
'@
[System.IO.File]::WriteAllText($expectedPath, $expected.TrimStart() + "`r`n", $utf8NoBom)

Write-Host ('Projeto criado: ' + $OutputPath)
Write-Host ('Expectativa: ' + $expectedPath)
Write-Host '323 words: paginas 0, 80, 160, 240 e 320; END no passo 322.'
Write-Host 'O script nao abre COM e nao transmite qualquer byte ao PLC.'
