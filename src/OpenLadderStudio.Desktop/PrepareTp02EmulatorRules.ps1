param(
    [string]$Source = 'TP02PgEmulator.cs',
    [string]$Rules = 'TP02PgEmulatorRules.txt',
    [string]$Output = 'TP02PgEmulator.build.cs'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-Local([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $base $p)
}

function Parse-HexBytes([string]$text) {
    $clean = ($text -replace '[^0-9A-Fa-f]',' ').Trim()
    if ([string]::IsNullOrWhiteSpace($clean)) { return @() }
    $parts = $clean -split '\s+'
    $bytes = @()
    foreach ($p in $parts) {
        if ($p.Length -ne 2) { throw "Byte hexadecimal invalido: $p" }
        $bytes += [Convert]::ToByte($p,16)
    }
    return ,$bytes
}

$sourcePath = Resolve-Local $Source
$rulesPath = Resolve-Local $Rules
$outputPath = Resolve-Local $Output

if (-not (Test-Path $sourcePath)) { throw "Fonte nao encontrada: $sourcePath" }
if (-not (Test-Path $rulesPath)) { throw "Regras nao encontradas: $rulesPath" }

$text = [IO.File]::ReadAllText($sourcePath)
$marker = '                default:'
$idx = $text.IndexOf($marker, [StringComparison]::Ordinal)
if ($idx -lt 0) { throw 'Nao foi encontrado o bloco default do switch do emulador.' }

# 33 e Write PLC Program confirmado offline; nao pode ser sobrescrito por regra externa.
$known = @('F0','38','34','33','0A','14')
$cases = New-Object Text.StringBuilder
$count = 0

foreach ($raw in [IO.File]::ReadAllLines($rulesPath)) {
    $line = $raw.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
    $parts = $line.Split('|')
    if ($parts.Length -lt 3) { throw "Regra invalida: $line" }

    $cmd = $parts[0].Trim().ToUpperInvariant()
    if ($cmd.StartsWith('0X')) { $cmd = $cmd.Substring(2) }
    if ($cmd.Length -ne 2) { throw "CMD invalido: $cmd" }
    [byte]$cmdByte = [Convert]::ToByte($cmd,16)
    if ($known -contains $cmd) { throw "CMD $cmd ja e tratado nativamente." }

    [int]$delay = 0
    if (-not [Int32]::TryParse($parts[1].Trim(), [ref]$delay)) { throw "DELAY invalido: $($parts[1])" }
    if ($delay -lt 0 -or $delay -gt 10000) { throw "DELAY fora da faixa: $delay" }

    $resp = Parse-HexBytes $parts[2]
    if ($resp.Count -eq 0) { throw "Resposta vazia para CMD $cmd" }

    $sum = 0
    foreach ($b in $resp) { $sum = ($sum + [int]$b) -band 0xFF }
    if ($sum -ne 0xFF) { throw ("Resposta do CMD {0} nao fecha soma FF; soma=0x{1:X2}" -f $cmd,$sum) }

    $label = if ($parts.Length -ge 4) { $parts[3].Trim() } else { 'regra externa' }
    $safeLabel = $label.Replace('\\','\\\\').Replace('"','\\"')
    $bytesCs = ($resp | ForEach-Object { '0x' + $_.ToString('X2') }) -join ', '

    [void]$cases.AppendLine(('                case 0x{0}:' -f $cmd))
    [void]$cases.AppendLine(('                    SleepFor({0});' -f $delay))
    [void]$cases.AppendLine(('                    Send("EMU -> PC12 REGRA CMD=0x{0} {1}", new byte[] {{ {2} }});' -f $cmd,$safeLabel,$bytesCs))
    [void]$cases.AppendLine('                    break;')
    [void]$cases.AppendLine('')
    $count++
}

$out = $text.Insert($idx, $cases.ToString())
[IO.File]::WriteAllText($outputPath, $out, [Text.Encoding]::UTF8)
Write-Host ("TP02 emulator build source preparado: {0} regra(s) externa(s)." -f $count)
