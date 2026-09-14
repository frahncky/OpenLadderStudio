param(
    [Parameter(Mandatory = $true)]
    [string]$SessionDirectory
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SessionDirectory -PathType Container)) {
    throw "Pasta de sessao nao encontrada: $SessionDirectory"
}

$sessionLogPath = Join-Path $SessionDirectory 'session.log'
if (-not (Test-Path -LiteralPath $sessionLogPath)) {
    throw "session.log nao encontrado em: $SessionDirectory"
}

$log = [System.IO.File]::ReadAllText($sessionLogPath)
$report = New-Object 'System.Collections.Generic.List[string]'
$report.Add('TP02 PG34 LONG PAGING — PHYSICAL SESSION AUDIT')
$report.Add('================================================')
$report.Add('Sessao: ' + $SessionDirectory)
$report.Add('Data auditoria: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$report.Add('')

function Convert-HexFileToBytes([string]$Path) {
    $text = [System.IO.File]::ReadAllText($Path).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { return ,([byte[]]@()) }
    $tokens = $text -split '\s+'
    $bytes = New-Object byte[] $tokens.Count
    for ($i = 0; $i -lt $tokens.Count; $i++) {
        $bytes[$i] = [Convert]::ToByte($tokens[$i], 16)
    }
    return ,$bytes
}

function Test-Pg34Frame([byte[]]$Bytes) {
    if ($null -eq $Bytes -or $Bytes.Length -ne 0xF3) { return $false }
    if ($Bytes[0] -ne 0x00 -or $Bytes[1] -ne 0xF0) { return $false }
    $sum = 0
    foreach ($b in $Bytes) { $sum = ($sum + [int]$b) -band 0xFF }
    return ($sum -eq 0xFF)
}

$helloOk = ($log -match 'HELLO confirmado:\s+(80 01 09 75|C0 01 09 35)')
$f0Ok = ($log -match 'F0 #\d+ RX RAW:\s+00 02 10 22 CB')
$frame38Ok = ($log -match '38 FRAME OK:')

$report.Add(('HELLO: ' + $(if ($helloOk) { 'PASS' } else { 'FAIL/NAO LOCALIZADO' })))
$report.Add(('F0:    ' + $(if ($f0Ok) { 'PASS' } else { 'FAIL/NAO LOCALIZADO' })))
$report.Add(('38:    ' + $(if ($frame38Ok) { 'PASS' } else { 'FAIL/NAO LOCALIZADO' })))
$report.Add('')

$pages = @(
    @{ Start = 0;   Label = '0000'; Hex = '0000'; Tx = '34 03 00 00 A0 28' },
    @{ Start = 80;  Label = '0080'; Hex = '0050'; Tx = '34 03 00 50 A0 D8' },
    @{ Start = 160; Label = '0160'; Hex = '00A0'; Tx = '34 03 00 A0 A0 88' },
    @{ Start = 240; Label = '0240'; Hex = '00F0'; Tx = '34 03 00 F0 A0 38' },
    @{ Start = 320; Label = '0320'; Hex = '0140'; Tx = '34 03 01 40 A0 E7' }
)

$pageStatus = @{}
$report.Add('PAGINAS PG34')
$report.Add('------------')
foreach ($p in $pages) {
    $txPattern = '34@' + $p.Label + ' #\d+ TX:\s+' + [regex]::Escape($p.Tx)
    $txSeen = ($log -match $txPattern)
    $pagePath = Join-Path $SessionDirectory ('page-' + $p.Label + '.hex')
    $frameOk = $false
    if (Test-Path -LiteralPath $pagePath) {
        try {
            $bytes = Convert-HexFileToBytes $pagePath
            $frameOk = Test-Pg34Frame $bytes
        }
        catch { $frameOk = $false }
    }
    $ok = $txSeen -and $frameOk
    $pageStatus[$p.Start] = $ok
    $report.Add(('start={0,3} / 0x{1}: {2} | TX={3} | RX-frame={4}' -f $p.Start, $p.Hex,
        $(if ($ok) { 'PASS' } else { 'PENDENTE/FAIL' }),
        $(if ($txSeen) { 'OK' } else { 'NAO' }),
        $(if ($frameOk) { 'OK' } else { 'NAO' })))
}
$report.Add('')

$boundary80 = [bool]$pageStatus[0] -and [bool]$pageStatus[80]
$boundaryHigh = [bool]$pageStatus[240] -and [bool]$pageStatus[320]
$report.Add('FRONTEIRAS')
$report.Add('----------')
$report.Add(('0000 -> 0050 (0 -> 80):   ' + $(if ($boundary80) { 'PASS' } else { 'PENDENTE' })))
$report.Add(('00F0 -> 0140 (240 -> 320): ' + $(if ($boundaryHigh) { 'PASS — START_H fisicamente comprovado' } else { 'PENDENTE' })))
$report.Add('')

$summaryPath = Join-Path $SessionDirectory 'read-summary.txt'
$summaryExact = $false
if (Test-Path -LiteralPath $summaryPath) {
    $summary = [System.IO.File]::ReadAllText($summaryPath)
    $summaryExact = ($summary -match 'Paginas 34:\s*5') -and
                    ($summary -match 'Passos:\s*323') -and
                    ($summary -match 'END:\s*0322') -and
                    ($summary -match 'BRAW divergentes:\s*0') -and
                    ($summary -match 'UNKNOWN:\s*0')
}

$wordsPath = Join-Path $SessionDirectory 'program-words.txt'
$patternExact = $false
if (Test-Path -LiteralPath $wordsPath) {
    $words = @(Get-Content -LiteralPath $wordsPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($words.Count -eq 323) {
        $patternExact = $true
        for ($i = 0; $i -lt 322; $i++) {
            $expected = if (($i % 2) -eq 0) { '001001' } else { '404008' }
            if ($words[$i].Trim().ToUpperInvariant() -ne $expected) {
                $patternExact = $false
                break
            }
        }
        if ($patternExact -and $words[322].Trim().ToUpperInvariant() -ne '007007') {
            $patternExact = $false
        }
    }
}

$report.Add('PROJETO-TESTE 323 WORDS')
$report.Add('-------------------------')
$report.Add(('Resumo esperado (5 paginas / 323 / END 0322 / BRAW 0 / UNKNOWN 0): ' + $(if ($summaryExact) { 'PASS' } else { 'NAO CONFIRMADO' })))
$report.Add(('Padrao 161x [STR X0001, OUT C0001] + END: ' + $(if ($patternExact) { 'PASS' } else { 'NAO CONFIRMADO / OUTRO PROGRAMA' })))
$report.Add('')

if ($boundaryHigh) {
    $overall = 'PASS'
    $detail = 'Paginacao PG34 longa fisicamente comprovada inclusive na troca START_H 00->01.'
}
elseif ($boundary80) {
    $overall = 'PARCIAL'
    $detail = 'Segunda pagina comprovada; falta programa/resposta suficiente para 0x00F0 -> 0x0140.'
}
elseif ([bool]$pageStatus[0]) {
    $overall = 'PARCIAL'
    $detail = 'Primeira pagina comprovada; faltam paginas posteriores.'
}
else {
    $overall = 'FAIL'
    $detail = 'Nenhum quadro PG34 base completo foi confirmado nesta sessao.'
}

$report.Add('RESULTADO GLOBAL: ' + $overall)
$report.Add($detail)
$report.Add('')
$report.Add('Seguranca: este analisador e offline; nao abre COM nem transmite qualquer byte ao PLC.')

$reportPath = Join-Path $SessionDirectory 'pg34-boundary-validation.txt'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($reportPath, $report.ToArray(), $utf8NoBom)
$report | ForEach-Object { Write-Host $_ }
Write-Host ''
Write-Host ('Relatorio salvo em: ' + $reportPath)

if ($overall -eq 'PASS') { exit 0 }
if ($overall -eq 'PARCIAL') { exit 2 }
exit 1
