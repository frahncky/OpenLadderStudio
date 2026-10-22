param(
    [Parameter(Mandatory=$true)][string]$Cmd,
    [Parameter(Mandatory=$true)][string]$Response,
    [int]$DelayMs = 150,
    [string]$Label = 'captura PC12'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$rules = Join-Path $base 'TP02PgEmulatorRules.txt'

$cmdNorm = $Cmd.Trim().ToUpperInvariant()
if ($cmdNorm.StartsWith('0X')) { $cmdNorm = $cmdNorm.Substring(2) }
if ($cmdNorm.Length -ne 2) { throw 'CMD deve ter 2 digitos hexadecimais.' }
[byte]$cmdByte = [Convert]::ToByte($cmdNorm,16)
if (@('F0','38','34','0A','14') -contains $cmdNorm) { throw 'Esse comando ja e tratado nativamente.' }
if ($DelayMs -lt 0 -or $DelayMs -gt 10000) { throw 'DelayMs fora da faixa 0..10000.' }

$clean = ($Response -replace '[^0-9A-Fa-f]',' ').Trim()
if ([string]::IsNullOrWhiteSpace($clean)) { throw 'Resposta vazia.' }
$parts = $clean -split '\s+'
$sum = 0
$normParts = @()
foreach ($p in $parts) {
    if ($p.Length -ne 2) { throw "Byte hexadecimal invalido: $p" }
    $b = [Convert]::ToByte($p,16)
    $sum = ($sum + [int]$b) -band 0xFF
    $normParts += $b.ToString('X2')
}
if ($sum -ne 0xFF) { throw ("A resposta nao fecha soma modulo 256 em FF; soma=0x{0:X2}" -f $sum) }

if (-not (Test-Path $rules)) { throw "Arquivo nao encontrado: $rules" }
$existing = [IO.File]::ReadAllLines($rules)
foreach ($line in $existing) {
    $trim = $line.Trim()
    if ($trim.StartsWith('#') -or $trim.Length -eq 0) { continue }
    $first = $trim.Split('|')[0].Trim().ToUpperInvariant()
    if ($first.StartsWith('0X')) { $first = $first.Substring(2) }
    if ($first -eq $cmdNorm) { throw "Ja existe regra para CMD $cmdNorm. Edite TP02PgEmulatorRules.txt para substituir." }
}

$safeLabel = $Label.Replace('|','/').Trim()
$entry = '{0}|{1}|{2}|{3}' -f $cmdNorm,$DelayMs,([string]::Join(' ',$normParts)),$safeLabel
[IO.File]::AppendAllText($rules, $entry + [Environment]::NewLine, [Text.Encoding]::UTF8)
Write-Host "Regra adicionada: $entry"
Write-Host 'Execute BuildTp02Emulator.bat para recompilar com a nova resposta.'
