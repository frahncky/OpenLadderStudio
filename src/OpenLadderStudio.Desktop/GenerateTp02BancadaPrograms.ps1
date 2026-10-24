param(
    [string]$OutputDirectory,
    [string]$ExportExe
)

# Gera os programas de teste PB, PC, PD e PE do roteiro
# docs/tp02-bancada-firmware-2026-10-24.md (fase P0.3).
# Offline: nao abre porta COM e nao transmite nenhum byte ao PLC.
# Nenhum programa referencia saida fisica Y.
#
# Com -ExportExe (OpenLadderTP02ProjectExport.exe), compila cada .pladder para
# <nome>.tp02.hex (uma palavra HHLLEE por linha) e confere a contagem de palavras.

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $base '..\..\docs\data\tp02-bancada-2026-10-26'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

$empty = 'EMPTY~EMPTY'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function New-Rung([string]$first, [string]$last) {
    $cells = @($empty, $empty, $empty, $empty, $empty, $empty, $empty, $empty)
    if ($first) { $cells[0] = $first + '~EMPTY' }
    if ($last) { $cells[7] = $last + '~EMPTY' }
    return 'RUNG|' + ($cells -join '|')
}

function Write-Project([string]$name, [System.Collections.Generic.List[string]]$rows) {
    $path = Join-Path $OutputDirectory $name
    [System.IO.File]::WriteAllText($path, (($rows -join "`r`n") + "`r`n"), $utf8NoBom)
    return $path
}

function Format-C([int]$n) { return 'C' + $n.ToString('0000') }
function Format-X([int]$n) { return 'X' + $n.ToString('0000') }

# PB: 2 passos = STR X0001 / F-00 END.
$pb = New-Object 'System.Collections.Generic.List[string]'
$pb.Add('PC12-LADDER|2')
$pb.Add((New-Rung 'NO:X0001' 'END'))
$pbPath = Write-Project 'PB-2-passos.pladder' $pb

# PC: 91 passos = 18 x (STR Xn + F-13w ADD D0002,D0001,10) + END
# (docs/tp02-pg-pagination-34-v119.md).
$pc = New-Object 'System.Collections.Generic.List[string]'
$pc.Add('PC12-LADDER|2')
for ($i = 1; $i -le 18; $i++) {
    $pc.Add((New-Rung ('NO:' + (Format-X $i)) 'FUN:F-13w:D0002,D0001,10'))
}
$pc.Add((New-Rung $null 'END'))
$pcPath = Write-Project 'PC-91-passos.pladder' $pc

# PD: 323 passos, gerador historico (161 x STR X0001 / OUT C0001 + END).
$pdPath = Join-Path $OutputDirectory 'PD-323-passos.pladder'
& (Join-Path $base 'GenerateTp02Pg34BoundaryProject.ps1') -OutputPath $pdPath | Out-Null

# PE: instrucao de 2 palavras (F-23 SET C0040) nos slots 79/80.
#   palavras 0..77 : 39 x (STR X0001 / OUT C0001..C0039)
#   palavra  78    : STR X0001
#   palavras 79,80 : F-23 SET C0040 (17 71 / D7 84)
#   palavra  81    : F-00 END
# F-43 JMP + F-42 LB foi descartado: o compilador OpenLadder recusa LB sem
# condicao de entrada. As bobinas C sao distintas para evitar "Double OUT".
$pe = New-Object 'System.Collections.Generic.List[string]'
$pe.Add('PC12-LADDER|2')
for ($i = 1; $i -le 39; $i++) {
    $pe.Add((New-Rung 'NO:X0001' ('COIL:' + (Format-C $i))))
}
$pe.Add((New-Rung 'NO:X0001' 'SET:C0040'))
$pe.Add((New-Rung $null 'END'))
$pePath = Write-Project 'PE-82-passos-2w-slot79.pladder' $pe

$expected = [ordered]@{
    'PB-2-passos.pladder' = 2
    'PC-91-passos.pladder' = 91
    'PD-323-passos.pladder' = 323
    'PE-82-passos-2w-slot79.pladder' = 82
}

if (-not [string]::IsNullOrWhiteSpace($ExportExe)) {
    if (-not (Test-Path $ExportExe)) { throw "Exportador nao encontrado: $ExportExe" }
    foreach ($name in $expected.Keys) {
        $project = Join-Path $OutputDirectory $name
        $hex = [System.IO.Path]::ChangeExtension($project, '.tp02.hex')
        & $ExportExe $project $hex | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Compilacao TP02 falhou: $name" }
        $words = @([System.IO.File]::ReadAllLines($hex) | Where-Object { $_.Trim().Length -gt 0 })
        if ($words.Count -ne $expected[$name]) {
            throw ("{0}: {1} palavras, esperado {2}" -f $name, $words.Count, $expected[$name])
        }
        if ($words[$words.Count - 1].Trim().ToUpperInvariant() -ne '007000') {
            throw "${name}: ultima palavra nao e F-00 END (007000)"
        }
        Write-Host ("{0}: {1} palavras, END={2:X4}" -f $name, $words.Count, ($words.Count - 1))
    }
}

Write-Host ('Programas gerados em ' + $OutputDirectory)
Write-Host 'Nenhuma porta COM foi aberta; nenhum byte foi transmitido.'
