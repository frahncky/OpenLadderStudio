param(
    [string[]]$Path,
    [ValidateSet('auto', 'PA', 'PB', 'PC', 'PD', 'PE')]
    [string]$Programa = 'auto',
    [string]$ProgramsDir,
    [string]$Out,
    [switch]$SelfTest,
    [string]$EnsaioDir
)

# Analisador das capturas do roteiro docs/tp02-bancada-firmware-2026-10-24.md.
# Lê logs do validador físico, da PG Lab, da captura completa/em ponte, do
# emulador TP02 e arquivos chave=valor; classifica cada resposta do roteiro como
# ESPERADA, ALTERNATIVA PREVISTA (qual) ou INESPERADA, agrupada por pendência.
# Offline: não abre porta COM e não transmite nada.

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ProgramsDir)) {
    $ProgramsDir = Join-Path $ScriptDir '..\..\docs\data\tp02-bancada-2026-10-26'
}

$ProgramTable = [ordered]@{
    'PA' = @{ Words = 23;  File = $null }
    'PB' = @{ Words = 2;   File = 'PB-2-passos.tp02.hex' }
    'PC' = @{ Words = 91;  File = 'PC-91-passos.tp02.hex' }
    'PD' = @{ Words = 323; File = 'PD-323-passos.tp02.hex' }
    'PE' = @{ Words = 82;  File = 'PE-82-passos-2w-slot79.tp02.hex' }
}

$Pend = @{
    A1  = 'A1 - sessão base (HELLO STOP / F0)'
    P3  = 'Pendência 3 - generalidade de 38 = 2*(N-1)'
    P5  = 'Pendência 5 - F0 bit6 com seletor em RUN (A3)'
    P67 = 'Pendências 6 e 7 - paginação do 34 além de 80 (B1)'
    P4  = 'Pendência 4 - 34 com START fora de múltiplo de 0x50 (B2)'
    P1  = 'Pendência 1 - firmware exige 03 00 FC antes do 33? (C1)'
    P2  = 'Pendência 2 - NAK do 33 com checksum inválido (C2)'
    C3  = 'C3 - 03 00 FC (Clear Program)'
    RO  = 'Leituras 0A (fora das fases A/B)'
    SEG = 'SEGURANÇA - quadros fora do roteiro'
}

# ------------------------------------------------------------------ util

function ConvertFrom-Hex([string]$text) {
    if ($null -eq $text) { return ,([byte[]]@()) }
    $clean = ($text -replace '[^0-9A-Fa-f]', ' ').Trim()
    if ($clean.Length -eq 0) { return ,([byte[]]@()) }
    $parts = $clean -split '\s+'
    $list = New-Object 'System.Collections.Generic.List[byte]'
    foreach ($p in $parts) {
        if ($p.Length -ne 2) { return ,([byte[]]@()) }
        $list.Add([Convert]::ToByte($p, 16))
    }
    return ,($list.ToArray())
}

function ConvertTo-HexText([byte[]]$b, [int]$max = 0) {
    if ($null -eq $b -or $b.Length -eq 0) { return '[]' }
    $n = $b.Length
    if ($max -gt 0 -and $n -gt $max) { $n = $max }
    $s = (($b[0..($n - 1)] | ForEach-Object { $_.ToString('X2') }) -join ' ')
    if ($n -lt $b.Length) { $s += (' ... (' + $b.Length + ' bytes)') }
    return $s
}

function Get-Sum8([byte[]]$b) {
    $s = 0
    foreach ($x in $b) { $s = ($s + [int]$x) -band 0xFF }
    return $s
}

function Test-Same([byte[]]$a, [byte[]]$b) {
    if ($null -eq $a -or $null -eq $b -or $a.Length -ne $b.Length) { return $false }
    for ($i = 0; $i -lt $a.Length; $i++) { if ($a[$i] -ne $b[$i]) { return $false } }
    return $true
}

function Test-Contains([byte[]]$raw, [byte[]]$seq) {
    if ($null -eq $raw -or $raw.Length -lt $seq.Length) { return $false }
    for ($i = 0; $i -le $raw.Length - $seq.Length; $i++) {
        $ok = $true
        for ($j = 0; $j -lt $seq.Length; $j++) { if ($raw[$i + $j] -ne $seq[$j]) { $ok = $false; break } }
        if ($ok) { return $true }
    }
    return $false
}

# Localiza o primeiro quadro S LEN payload CHK com soma FF e LEN dado (ou qualquer LEN se -1).
function Find-Frame([byte[]]$raw, [int]$len) {
    if ($null -eq $raw) { return $null }
    for ($i = 0; $i -le $raw.Length - 3; $i++) {
        $l = [int]$raw[$i + 1]
        if ($len -ge 0 -and $l -ne $len) { continue }
        $total = $l + 3
        if ($i + $total -gt $raw.Length) { continue }
        $s = 0
        for ($j = 0; $j -lt $total; $j++) { $s = ($s + [int]$raw[$i + $j]) -band 0xFF }
        if ($s -ne 0xFF) { continue }
        $f = New-Object byte[] $total
        [Array]::Copy($raw, $i, $f, 0, $total)
        return ,$f
    }
    return $null
}

function Find-Nak([byte[]]$raw) {
    $f = Find-Frame $raw -1
    if ($null -ne $f -and ($f[0] -band 0x80) -ne 0) { return ,$f }
    return $null
}

function New-Frame([byte[]]$body) {
    $f = New-Object byte[] ($body.Length + 1)
    [Array]::Copy($body, $f, $body.Length)
    $f[$body.Length] = [byte]((0xFF - (Get-Sum8 $body)) -band 0xFF)
    return ,$f
}

function Get-Braw([int]$h, [int]$l) {
    return [byte]((($h -shr 4) + ($h -band 0x0F) + ($l -shr 4) + ($l -band 0x0F)) -band 0x0F)
}

# ------------------------------------------------------------------ programas

function Read-ProgramWords([string]$name) {
    if (-not $ProgramTable.Contains($name)) { return $null }
    $file = $ProgramTable[$name].File
    if ($null -eq $file) { return $null }
    $p = Join-Path $ProgramsDir $file
    if (-not (Test-Path $p)) { return $null }
    $words = New-Object 'System.Collections.Generic.List[byte[]]'
    foreach ($line in [IO.File]::ReadAllLines($p)) {
        $t = $line.Trim()
        if ($t.Length -eq 0) { continue }
        $words.Add([byte[]]@([Convert]::ToByte($t.Substring(0, 2), 16), [Convert]::ToByte($t.Substring(2, 2), 16)))
    }
    return ,($words.ToArray())
}

# Página 34 prevista pelo modelo por palavra (00 F0 + 160 HIGH/LOW + 80 BRAW + CHK).
function Get-ModelPage($words, [int]$start) {
    $body = New-Object byte[] 242
    $body[0] = 0x00; $body[1] = 0xF0
    for ($i = 0; $i -lt 80; $i++) {
        $step = $start + $i
        if ($step -ge $words.Length) { break }
        $h = [int]$words[$step][0]; $l = [int]$words[$step][1]
        $body[2 + 2 * $i] = [byte]$h
        $body[3 + 2 * $i] = [byte]$l
        $body[2 + 160 + $i] = Get-Braw $h $l
    }
    return ,(New-Frame $body)
}

# Página @004F prevista a partir de páginas @0000 e @0050 capturadas no mesmo log.
function Get-ShiftedPage([byte[]]$p0, [byte[]]$p50) {
    $body = New-Object byte[] 242
    $body[0] = 0x00; $body[1] = 0xF0
    $body[2] = $p0[2 + 158]; $body[3] = $p0[2 + 159]; $body[162] = $p0[2 + 160 + 79]
    for ($i = 1; $i -lt 80; $i++) {
        $body[2 + 2 * $i] = $p50[2 + 2 * ($i - 1)]
        $body[3 + 2 * $i] = $p50[3 + 2 * ($i - 1)]
        $body[162 + $i] = $p50[162 + ($i - 1)]
    }
    return ,(New-Frame $body)
}

function Test-BrawConsistent([byte[]]$page) {
    for ($i = 0; $i -lt 80; $i++) {
        if ($page[162 + $i] -ne (Get-Braw ([int]$page[2 + 2 * $i]) ([int]$page[3 + 2 * $i]))) { return $false }
    }
    return $true
}

# ------------------------------------------------------------------ leitura dos logs

function New-Exchange([string]$label, [byte[]]$tx, [int]$line, [string]$file) {
    $o = New-Object PSObject
    $o | Add-Member NoteProperty Label $label
    $o | Add-Member NoteProperty Tx $tx
    $o | Add-Member NoteProperty Rx ([byte[]]@())
    $o | Add-Member NoteProperty RxSeen $false
    $o | Add-Member NoteProperty Line $line
    $o | Add-Member NoteProperty File $file
    return $o
}

function Read-Exchanges([string]$file) {
    $list = New-Object 'System.Collections.Generic.List[object]'
    $blocked = New-Object 'System.Collections.Generic.List[string]'
    $program = $null
    $current = $null
    $n = 0
    foreach ($line in [IO.File]::ReadAllLines($file)) {
        $n++
        if ($line -match 'Programa:\s*(P[A-E])-') { $program = $Matches[1] }
        if ($line -match 'BLOQUEIO DE SEGURAN') { $blocked.Add($line.Trim()); continue }
        if ($line -match 'PC12 RAW') { continue }

        # Captura completa/ponte (protocol-report): "| rótulo | ...ms | TX=.. | RX=.. |"
        if ($line -match '\|\s*(?<label>[^|]+?)\s*\|[^|]*\|\s*TX=(?<tx>[0-9A-Fa-f ]*)\|\s*RX=(?<rx>[^|]*)\|') {
            $e = New-Exchange $Matches['label'] (ConvertFrom-Hex $Matches['tx']) $n $file
            $e.Rx = ConvertFrom-Hex $Matches['rx']; $e.RxSeen = $true
            $list.Add($e); $current = $null; continue
        }
        # Validador / captura completa: "ROTULO TX: hex" e "ROTULO RX(12ms): hex|[]"
        if ($line -match '(?<label>[A-Za-z0-9_\-]+) TX: (?<hex>[0-9A-Fa-f ]+)\s*$') {
            $current = New-Exchange $Matches['label'] (ConvertFrom-Hex $Matches['hex']) $n $file
            $list.Add($current); continue
        }
        if ($line -match '(?<label>[A-Za-z0-9_\-]+) RX(\([^)]*\))?: (?<hex>\[\]|[0-9A-Fa-f ]+)') {
            if ($null -ne $current) { $current.Rx = ConvertFrom-Hex $Matches['hex']; $current.RxSeen = $true; $current = $null }
            continue
        }
        # PG Lab: "[hh:mm:ss] TX          passo  [hex] soma=.." / "RX RAW" / "RX SEM ECO"
        if ($line -match '\]\s+TX\s+(?<label>.*?)\s+\[(?<hex>[0-9A-Fa-f ]+)\]') {
            $current = New-Exchange ($Matches['label'].Trim()) (ConvertFrom-Hex $Matches['hex']) $n $file
            $list.Add($current); continue
        }
        if ($line -match '\]\s+RX (RAW|SEM ECO)(\s+[^\[]*)?(\[(?<hex>[0-9A-Fa-f ]*)\])?') {
            if ($null -ne $current) { $current.Rx = ConvertFrom-Hex $Matches['hex']; $current.RxSeen = $true }
            continue
        }
        # Emulador TP02: "PC12 -> EMU ... [hex]" / "EMU -> PC12 ... [hex]"
        if ($line -match 'PC12 -> EMU(?<label>[^\[]*)\[(?<hex>[0-9A-Fa-f ]+)\]') {
            $current = New-Exchange ('EMU ' + $Matches['label'].Trim()) (ConvertFrom-Hex $Matches['hex']) $n $file
            $current.RxSeen = $true  # o emulador registra tudo o que envia; sem linha EMU -> PC12 = silêncio
            $list.Add($current); continue
        }
        if ($line -match 'EMU -> PC12[^\[]*\[(?<hex>[0-9A-Fa-f ]+)\]') {
            if ($null -ne $current) { $current.Rx = ConvertFrom-Hex $Matches['hex']; $current.RxSeen = $true; $current = $null }
            continue
        }
        # Chave=valor (sessão física arquivada): NOME_TX=hex / NOME_RX=hex
        if ($line -match '^HELLO_RX=(?<hex>[0-9A-Fa-f ]+)\s*$') {
            $e = New-Exchange 'HELLO' ([byte[]](0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D)) $n $file
            $e.Rx = ConvertFrom-Hex $Matches['hex']; $e.RxSeen = $true
            $list.Add($e); $current = $null; continue
        }
        if ($line -match '^(?<label>[A-Za-z0-9]+(_[A-Za-z0-9]+)*)_TX=(?<hex>[0-9A-Fa-f ]+)\s*$') {
            $current = New-Exchange $Matches['label'] (ConvertFrom-Hex $Matches['hex']) $n $file
            $list.Add($current); continue
        }
        if ($line -match '^(?<label>[A-Za-z0-9]+(_[A-Za-z0-9]+)*)_RX=(?<hex>[0-9A-Fa-f ]*)\s*$') {
            if ($null -ne $current) { $current.Rx = ConvertFrom-Hex $Matches['hex']; $current.RxSeen = $true; $current = $null }
            continue
        }
    }
    $r = New-Object PSObject
    $r | Add-Member NoteProperty Exchanges $list
    $r | Add-Member NoteProperty Blocked $blocked
    $r | Add-Member NoteProperty Program $program
    return $r
}

# ------------------------------------------------------------------ classificação

$HelloTx = [byte[]](0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D)
$HelloStop = [byte[]](0x80, 0x01, 0x09, 0x75)
$HelloRun = [byte[]](0xC0, 0x01, 0x09, 0x35)
$F0Tx = [byte[]](0xF0, 0x00, 0x0F)
$F0Stop = [byte[]](0x00, 0x02, 0x10, 0x22, 0xCB)
$F0Bit6 = [byte[]](0x40, 0x02, 0x10, 0x22, 0x8B)
$Tx38 = [byte[]](0x38, 0x00, 0xC7)
$Pg33Sentinel = ConvertFrom-Hex '33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 D9'
$Pg33BadChk = ConvertFrom-Hex '33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 DA'
$Clear03 = [byte[]](0x03, 0x00, 0xFC)
$Ack = [byte[]](0x00, 0x00, 0xFF)

function New-Result($e, [string]$pendTitle, [string]$cls, [string]$detail) {
    $o = New-Object PSObject
    $o | Add-Member NoteProperty Pendencia $pendTitle
    $o | Add-Member NoteProperty Classe $cls
    $o | Add-Member NoteProperty Detalhe $detail
    $o | Add-Member NoteProperty Label $e.Label
    $o | Add-Member NoteProperty Tx (ConvertTo-HexText $e.Tx)
    $o | Add-Member NoteProperty Rx (ConvertTo-HexText $e.Rx 24)
    $o | Add-Member NoteProperty Origem ((Split-Path -Leaf $e.File) + ':' + $e.Line)
    return $o
}

function Get-38Values([int]$n) {
    $total = 2 * ($n - 1)
    $page = 2 * ([Math]::Min($n, 80) - 1)
    return @($total, $page)
}

function Format-38([int]$v) {
    return (ConvertTo-HexText (New-Frame ([byte[]](0x00, 0x02, (($v -shr 8) -band 0xFF), ($v -band 0xFF)))))
}

function Invoke-Classify($parsed, [string]$prog) {
    $results = New-Object 'System.Collections.Generic.List[object]'
    $words = $null
    $n = -1
    if ($prog -and $ProgramTable.Contains($prog)) {
        $n = $ProgramTable[$prog].Words
        $words = Read-ProgramWords $prog
    }
    $inRun = $false
    $pages = @{}
    $backup = $null

    foreach ($e in $parsed.Exchanges) {
        $tx = $e.Tx; $rx = $e.Rx
        if (-not $e.RxSeen) { $results.Add((New-Result $e $Pend.SEG 'INESPERADA' 'RX bruto ausente no log (não é silêncio): arquivar o RX completo')); continue }
        $isF0 = Test-Same $tx $F0Tx

        if (Test-Same $tx $HelloTx) {
            $hasStop = Test-Contains $rx $HelloStop
            $hasRun = Test-Contains $rx $HelloRun
            if ($hasStop -and $hasRun) { $results.Add((New-Result $e $Pend.A1 'INESPERADA' 'HELLO ambíguo: STOP e RUN na mesma resposta')); continue }
            if ($hasStop) { $inRun = $false; $results.Add((New-Result $e $Pend.A1 'ESPERADA' 'HELLO 80 01 09 75 (STOP)')); continue }
            if ($hasRun) { $inRun = $true; $results.Add((New-Result $e $Pend.P5 'ESPERADA' 'HELLO C0 01 09 35 (RUN): pré-condição de A3; só F0 pode sair')); continue }
            if ($rx.Length -eq 0) { $results.Add((New-Result $e $Pend.A1 'ALTERNATIVA PREVISTA' 'HELLO sem resposta (tentativa; o validador repete até 2 ciclos)')); continue }
            $results.Add((New-Result $e $Pend.A1 'INESPERADA' 'HELLO com resposta desconhecida')); continue
        }

        if ($inRun -and -not $isF0) {
            $results.Add((New-Result $e $Pend.SEG 'INESPERADA' 'VIOLAÇÃO: quadro diferente de F0 transmitido com HELLO RUN')); continue
        }

        if ($isF0) {
            if ($inRun) {
                if (Test-Contains $rx $F0Bit6) { $results.Add((New-Result $e $Pend.P5 'ESPERADA' '40 02 10 22 8B: bit6 de S reflete RUN')); continue }
                if (Test-Contains $rx $F0Stop) { $results.Add((New-Result $e $Pend.P5 'ALTERNATIVA PREVISTA' '00 02 10 22 CB: bit6 NÃO reflete RUN no F0')); continue }
                if ($rx.Length -eq 0) { $results.Add((New-Result $e $Pend.P5 'ALTERNATIVA PREVISTA' 'silêncio: F0 não responde em RUN')); continue }
                $nak = Find-Nak $rx
                if ($null -ne $nak) { $results.Add((New-Result $e $Pend.P5 'ALTERNATIVA PREVISTA' ('NAK em RUN, código rx[2]=' + $nak[2].ToString('X2')))); continue }
                $results.Add((New-Result $e $Pend.P5 'INESPERADA' 'F0 em RUN com resposta fora das previstas')); continue
            }
            if (Test-Contains $rx $F0Stop) { $results.Add((New-Result $e $Pend.A1 'ESPERADA' '00 02 10 22 CB')); continue }
            $results.Add((New-Result $e $Pend.A1 'INESPERADA' 'F0 em STOP diferente de 00 02 10 22 CB (parada de A1)')); continue
        }

        if (Test-Same $tx $Tx38) {
            $f = Find-Frame $rx 2
            if ($null -eq $f) { $results.Add((New-Result $e $Pend.P3 'INESPERADA' 'sem quadro LEN=02 com soma FF (parada de A2)')); continue }
            $v = ([int]$f[2] -shl 8) -bor [int]$f[3]
            $stepsFromV = ($v / 2) + 1
            if ($n -lt 1) {
                $results.Add((New-Result $e $Pend.P3 'ESPERADA' ('estrutural; programa não informado (-Programa). Valor 0x' + $v.ToString('X4') + ' => N=' + $stepsFromV))); continue
            }
            $vals = Get-38Values $n
            if ($v -eq $vals[0]) { $results.Add((New-Result $e $Pend.P3 'ESPERADA' ($prog + ': 2*(N-1) com N=' + $n + ' (' + (Format-38 $vals[0]) + ')'))); continue }
            if ($v -eq $vals[1]) { $results.Add((New-Result $e $Pend.P3 'ALTERNATIVA PREVISTA' ($prog + ': limitado à página de 80 (' + (Format-38 $vals[1]) + ')'))); continue }
            $results.Add((New-Result $e $Pend.P3 'INESPERADA' ($prog + ': valor 0x' + $v.ToString('X4') + '; previsto ' + (Format-38 $vals[0]) + ' ou ' + (Format-38 $vals[1])))); continue
        }

        if ($tx.Length -eq 6 -and $tx[0] -eq 0x34 -and $tx[1] -eq 0x03 -and (Get-Sum8 $tx) -eq 0xFF) {
            $start = ([int]$tx[2] -shl 8) -bor [int]$tx[3]
            $aligned = ($start % 0x50) -eq 0
            $p34Pend = $Pend.P67
            if (-not $aligned) { $p34Pend = $Pend.P4 }
            $page = Find-Frame $rx 0xF0
            if ($null -eq $page) {
                if ($rx.Length -eq 0) {
                    if ($aligned) { $results.Add((New-Result $e $p34Pend 'INESPERADA' ('START 0x' + $start.ToString('X4') + ': silêncio (parada de B1, sem repetir)'))) }
                    else { $results.Add((New-Result $e $p34Pend 'ALTERNATIVA PREVISTA' 'silêncio: START inválido descartado')) }
                    continue
                }
                $nak = Find-Nak $rx
                if ($null -ne $nak -and -not $aligned) { $results.Add((New-Result $e $p34Pend 'ALTERNATIVA PREVISTA' ('NAK: firmware exige múltiplo de 80; código rx[2]=' + $nak[2].ToString('X2')))); continue }
                $results.Add((New-Result $e $p34Pend 'INESPERADA' ('START 0x' + $start.ToString('X4') + ': sem página LEN=F0 com soma FF'))); continue
            }
            $pages[$start] = $page
            if ($start -eq 0 -and $null -eq $backup) { $backup = $page }
            $braw = Test-BrawConsistent $page
            $brawNote = ''
            if (-not $braw) { $brawNote = '; BRAW diverge da soma de nibbles' }

            if ($aligned) {
                if ($null -eq $words) {
                    $cls = 'ESPERADA'; if (-not $braw) { $cls = 'INESPERADA' }
                    $results.Add((New-Result $e $p34Pend $cls ('START 0x' + $start.ToString('X4') + ': página estrutural válida; sem programa de referência' + $brawNote))); continue
                }
                $model = Get-ModelPage $words $start
                if (-not $braw) { $results.Add((New-Result $e $p34Pend 'INESPERADA' ($prog + ' START 0x' + $start.ToString('X4') + ': BRAW diverge da soma de nibbles'))); continue }
                if (Test-Same $page $model) { $results.Add((New-Result $e $p34Pend 'ESPERADA' ($prog + ' START 0x' + $start.ToString('X4') + ': igual ao modelo por palavra'))); continue }
                # Mesmo conteúdo até o END, mas palavras não nulas depois dele: resíduo.
                $endLocal = ($words.Length - 1) - $start
                $prefixOk = $true
                $lim = [Math]::Min(79, $endLocal)
                for ($i = 0; $i -le $lim; $i++) {
                    if ($page[2 + 2 * $i] -ne $model[2 + 2 * $i] -or $page[3 + 2 * $i] -ne $model[3 + 2 * $i]) { $prefixOk = $false; break }
                }
                if ($prefixOk -and $endLocal -ge 0 -and $endLocal -lt 79) {
                    $results.Add((New-Result $e $p34Pend 'ALTERNATIVA PREVISTA' ($prog + ' START 0x' + $start.ToString('X4') + ': igual até o END; palavras antigas depois do END (resíduo)' + $brawNote))); continue
                }
                $diff = 0
                for ($i = 0; $i -lt 243; $i++) { if ($page[$i] -ne $model[$i]) { $diff++ } }
                $results.Add((New-Result $e $p34Pend 'INESPERADA' ($prog + ' START 0x' + $start.ToString('X4') + ': ' + $diff + ' byte(s) diferentes do modelo' + $brawNote))); continue
            }

            # START fora de múltiplo de 0x50 (B2).
            $mWord = $null; $m50 = $null; $m00 = $null
            $floorStart = [int]([Math]::Floor($start / 0x50) * 0x50)
            $ceilStart = $floorStart + 0x50
            if ($null -ne $words) {
                $mWord = Get-ModelPage $words $start
                $m50 = Get-ModelPage $words $ceilStart
                $m00 = Get-ModelPage $words $floorStart
            } else {
                if ($pages.ContainsKey($floorStart) -and $pages.ContainsKey($ceilStart) -and ($start -eq $ceilStart - 1)) {
                    $mWord = Get-ShiftedPage $pages[$floorStart] $pages[$ceilStart]
                }
                if ($pages.ContainsKey($ceilStart)) { $m50 = $pages[$ceilStart] }
                if ($pages.ContainsKey($floorStart)) { $m00 = $pages[$floorStart] }
            }
            if ($null -ne $mWord -and (Test-Same $page $mWord)) { $results.Add((New-Result $e $p34Pend 'ESPERADA' ('START 0x' + $start.ToString('X4') + ': página por palavra (par 0 = palavra ' + $start + ')'))); continue }
            if ($null -ne $m50 -and (Test-Same $page $m50)) { $results.Add((New-Result $e $p34Pend 'ALTERNATIVA PREVISTA' ('firmware arredonda o START para 0x' + $ceilStart.ToString('X4')))); continue }
            if ($null -ne $m00 -and (Test-Same $page $m00)) { $results.Add((New-Result $e $p34Pend 'ALTERNATIVA PREVISTA' ('firmware arredonda o START para 0x' + $floorStart.ToString('X4')))); continue }
            if ($null -eq $mWord -and $null -eq $m50 -and $null -eq $m00) {
                $results.Add((New-Result $e $p34Pend 'INESPERADA' 'sem referência: informe -Programa ou capture 34 @0000 e @0050 no mesmo log')); continue
            }
            $results.Add((New-Result $e $p34Pend 'INESPERADA' ('START 0x' + $start.ToString('X4') + ': página não bate com nenhum modelo previsto' + $brawNote))); continue
        }

        if (Test-Same $tx $Pg33Sentinel) {
            if (Test-Same (Find-Frame $rx 0) $Ack) { $results.Add((New-Result $e $Pend.P1 'ESPERADA' 'ACK 00 00 FF; conferir readback 34 @0000 (pares 3..22)')); continue }
            $results.Add((New-Result $e $Pend.P1 'INESPERADA' 'ACK diferente de 00 00 FF ou silêncio: NÃO restaurar às cegas; ler 34 @0000')); continue
        }
        if (Test-Same $tx $Pg33BadChk) {
            if ($rx.Length -eq 0) { $results.Add((New-Result $e $Pend.P2 'ESPERADA' 'silêncio: firmware descarta quadro com soma errada')); continue }
            $nak = Find-Nak $rx
            if ($null -ne $nak) { $results.Add((New-Result $e $Pend.P2 'ESPERADA' ('NAK código rx[2]=' + $nak[2].ToString('X2')))); continue }
            if (Test-Same (Find-Frame $rx 0) $Ack) { $results.Add((New-Result $e $Pend.P2 'ALTERNATIVA PREVISTA' 'ACK 00 00 FF: firmware NÃO valida a soma; restaurar e encerrar C')); continue }
            $results.Add((New-Result $e $Pend.P2 'INESPERADA' 'resposta fora das previstas')); continue
        }
        if (Test-Same $tx $Clear03) {
            if (Test-Same (Find-Frame $rx 0) $Ack) { $results.Add((New-Result $e $Pend.C3 'ESPERADA' 'ACK 00 00 FF')); continue }
            $results.Add((New-Result $e $Pend.C3 'INESPERADA' 'ACK diferente de 00 00 FF ou silêncio')); continue
        }
        if ($tx.Length -eq 6 -and $tx[0] -eq 0x0A) {
            $f = Find-Frame $rx ([int]$tx[4])
            if ($null -ne $f) { $results.Add((New-Result $e $Pend.RO 'ESPERADA' ('quadro LEN=' + $tx[4].ToString('X2') + ' válido'))) }
            else { $results.Add((New-Result $e $Pend.RO 'INESPERADA' ('sem quadro LEN=' + $tx[4].ToString('X2') + ' com soma FF'))) }
            continue
        }
        $results.Add((New-Result $e $Pend.SEG 'INESPERADA' 'quadro fora do roteiro (fases A/B só leem)'))
    }
    return ,$results
}

function Format-Report($results, $parsed, [string]$source, [string]$prog) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('TP02 - ANÁLISE DA BANCADA DE FIRMWARE (roteiro 2026-10-24)')
    [void]$sb.AppendLine('Fonte: ' + $source)
    $progText = 'não informado'
    if ($prog) { $progText = $prog }
    [void]$sb.AppendLine('Programa de referência: ' + $progText)
    [void]$sb.AppendLine('Nota: ESPERADA = previsão do PC12/emulador; nada aqui é fato de firmware sem captura física arquivada.')
    [void]$sb.AppendLine()
    foreach ($key in @('A1', 'P3', 'P5', 'P67', 'P4', 'P1', 'P2', 'C3', 'RO', 'SEG')) {
        $title = $Pend[$key]
        $items = @($results | Where-Object { $_.Pendencia -eq $title })
        if ($items.Count -eq 0) { continue }
        [void]$sb.AppendLine('== ' + $title)
        foreach ($r in $items) {
            [void]$sb.AppendLine(('  [{0}] {1}: {2}' -f $r.Classe, $r.Label, $r.Detalhe))
            [void]$sb.AppendLine(('      TX {0} | RX {1} | {2}' -f $r.Tx, $r.Rx, $r.Origem))
        }
        [void]$sb.AppendLine()
    }
    if ($parsed.Blocked.Count -gt 0) {
        [void]$sb.AppendLine('== Quadros barrados pela allowlist (não transmitidos)')
        foreach ($b in $parsed.Blocked) { [void]$sb.AppendLine('  ' + ($b -replace '^\[[^\]]*\]\s*', '')) }
        [void]$sb.AppendLine()
    }
    $esp = @($results | Where-Object { $_.Classe -eq 'ESPERADA' }).Count
    $alt = @($results | Where-Object { $_.Classe -eq 'ALTERNATIVA PREVISTA' }).Count
    $ine = @($results | Where-Object { $_.Classe -eq 'INESPERADA' }).Count
    [void]$sb.AppendLine(('Resumo: ESPERADA={0} ALTERNATIVA PREVISTA={1} INESPERADA={2}' -f $esp, $alt, $ine))
    return $sb.ToString()
}

function Invoke-Analysis([string]$file, [string]$prog) {
    $parsed = Read-Exchanges $file
    $p = $prog
    if ($p -eq 'auto' -or [string]::IsNullOrEmpty($p)) {
        $p = $parsed.Program
        if (-not $p) {
            $leaf = Split-Path -Leaf $file
            if ($leaf -cmatch '(^|[^A-Za-z])(P[A-E])([^A-Za-z]|$)') { $p = $Matches[2] }
        }
    }
    $results = Invoke-Classify $parsed $p
    $o = New-Object PSObject
    $o | Add-Member NoteProperty Results $results
    $o | Add-Member NoteProperty Parsed $parsed
    $o | Add-Member NoteProperty Program $p
    $o | Add-Member NoteProperty Report (Format-Report $results $parsed $file $p)
    return $o
}

# ------------------------------------------------------------------ autoteste

$script:checks = 0
$script:fails = 0
function Assert-Class($analysis, [string]$label, [string]$cls, [string]$detailLike, [string]$ctx) {
    $script:checks++
    $hit = @($analysis.Results | Where-Object { $_.Label -eq $label -and $_.Classe -eq $cls -and ($_.Detalhe -like $detailLike) })
    if ($hit.Count -eq 0) {
        $script:fails++
        $got = @($analysis.Results | Where-Object { $_.Label -eq $label } | ForEach-Object { $_.Classe + ' / ' + $_.Detalhe }) -join ' || '
        Write-Host ('FALHA: ' + $ctx + ' ' + $label + ' esperado [' + $cls + '] ~ "' + $detailLike + '"; obtido: ' + $got)
    }
}

function Assert-True([bool]$cond, [string]$msg) {
    $script:checks++
    if (-not $cond) { $script:fails++; Write-Host ('FALHA: ' + $msg) }
}

function Write-Fabricated([string]$dir, [string]$name, [string[]]$lines) {
    $p = Join-Path $dir $name
    [IO.File]::WriteAllLines($p, $lines, (New-Object System.Text.UTF8Encoding($true)))
    return $p
}

function Get-PageHex($words, [int]$start) { return (ConvertTo-HexText (Get-ModelPage $words $start)) }

function Invoke-SelfTest {
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ('tp02-bancada-analyzer-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    try {
        # Checksums do roteiro.
        Assert-True ((Format-38 0x2C) -eq '00 02 00 2C D1') 'PA 38'
        Assert-True ((Format-38 0x02) -eq '00 02 00 02 FB') 'PB 38'
        Assert-True ((Format-38 0xB4) -eq '00 02 00 B4 49') 'PC 38 total'
        Assert-True ((Format-38 0x284) -eq '00 02 02 84 77') 'PD 38 total'
        Assert-True ((Format-38 0x9E) -eq '00 02 00 9E 5F') '38 limitado'
        Assert-True ((Format-38 0xA2) -eq '00 02 00 A2 5B') 'PE 38 total'

        foreach ($k in @('PB', 'PC', 'PD', 'PE')) {
            $w = Read-ProgramWords $k
            Assert-True ($null -ne $w -and $w.Length -eq $ProgramTable[$k].Words) ('programa ' + $k + ' com ' + $ProgramTable[$k].Words + ' palavras')
            if ($null -ne $w) { Assert-True ($w[$w.Length - 1][0] -eq 0 -and $w[$w.Length - 1][1] -eq 0x70) ('programa ' + $k + ' termina em END') }
        }
        $pe = Read-ProgramWords 'PE'
        $pd = Read-ProgramWords 'PD'

        # 1. Logs do ensaio offline (validador + emulador em processo).
        if (-not [string]::IsNullOrWhiteSpace($EnsaioDir)) {
            $expect = @(
                @('A1-sessao-base', 'HELLO', 'ESPERADA', '*STOP*'),
                @('A1-sessao-base', 'F0', 'ESPERADA', '*CB*'),
                @('A2-38-PB-pagina', '38', 'ESPERADA', 'PB*'),
                @('A2-38-PB-total16', '38', 'ESPERADA', 'PB*'),
                @('A2-38-PC-pagina', '38', 'ALTERNATIVA PREVISTA', '*limitado*'),
                @('A2-38-PC-total16', '38', 'ESPERADA', '*00 B4 49*'),
                @('A2-38-PD-pagina', '38', 'ALTERNATIVA PREVISTA', '*limitado*'),
                @('A2-38-PD-total16', '38', 'ESPERADA', '*02 84 77*'),
                @('A2-38-PE-pagina', '38', 'ALTERNATIVA PREVISTA', '*limitado*'),
                @('A2-38-PE-total16', '38', 'ESPERADA', '*00 A2 5B*'),
                @('A3-F0-RUN-bit6', 'HELLO', 'ESPERADA', '*RUN*'),
                @('A3-F0-RUN-bit6', 'F0-RUN', 'ESPERADA', '40 02 10 22 8B*'),
                @('A3-F0-RUN-stop', 'F0-RUN', 'ALTERNATIVA PREVISTA', '*NÃO reflete*'),
                @('A3-F0-RUN-silent', 'F0-RUN', 'ALTERNATIVA PREVISTA', 'silêncio*'),
                @('A3-F0-RUN-nak', 'F0-RUN', 'ALTERNATIVA PREVISTA', 'NAK*10'),
                @('B1-paginacao-PC', 'PG34-0050', 'ESPERADA', '*modelo*'),
                @('B1-paginacao-PD', 'PG34-00A0', 'ESPERADA', '*modelo*'),
                @('B1-paginacao-PD', 'PG34-00F0', 'ESPERADA', '*modelo*'),
                @('B1-paginacao-PD', 'PG34-0140', 'ESPERADA', '*modelo*'),
                @('B2-START-004F-word', 'PG34-004F', 'ESPERADA', '*por palavra*'),
                @('B2-START-004F-align50', 'PG34-004F', 'ALTERNATIVA PREVISTA', '*0x0050'),
                @('B2-START-004F-align00', 'PG34-004F', 'ALTERNATIVA PREVISTA', '*0x0000'),
                @('B2-START-004F-nak', 'PG34-004F', 'ALTERNATIVA PREVISTA', 'NAK*'),
                @('B2-START-004F-silent', 'PG34-004F', 'ALTERNATIVA PREVISTA', 'silêncio*')
            )
            foreach ($x in $expect) {
                $file = Join-Path $EnsaioDir ($x[0] + '.log')
                Assert-True (Test-Path $file) ('log do ensaio ausente: ' + $x[0])
                if (-not (Test-Path $file)) { continue }
                $a = Invoke-Analysis $file 'auto'
                Assert-Class $a $x[1] $x[2] $x[3] $x[0]
            }
            foreach ($f in Get-ChildItem -Path $EnsaioDir -Filter '*.log') {
                $a = Invoke-Analysis $f.FullName 'auto'
                $bad = @($a.Results | Where-Object { $_.Classe -eq 'INESPERADA' })
                Assert-True ($bad.Count -eq 0) ('ensaio ' + $f.Name + ' não deveria ter INESPERADA')
                if ($f.Name -like 'A3-*') {
                    Assert-True ($a.Parsed.Blocked.Count -eq 7) ('ensaio ' + $f.Name + ': 7 quadros barrados em RUN')
                    Assert-True ($a.Parsed.Exchanges.Count -eq 2) ('ensaio ' + $f.Name + ': só HELLO e F0 transmitidos')
                }
                [IO.File]::WriteAllText(([IO.Path]::ChangeExtension($f.FullName, '.analise.txt')), $a.Report, (New-Object System.Text.UTF8Encoding($true)))
            }
        }

        # 2. Logs fabricados das alternativas, um por formato de ferramenta.
        $hello = '43 4F 4E 2D 49 43 42 0D'
        $p0 = Get-PageHex $pe 0
        $p50 = Get-PageHex $pe 0x50
        $p4f = Get-PageHex $pe 0x4F

        # 2a. Validador: B2 sem programa (modelo derivado das páginas do próprio log).
        $f = Write-Fabricated $tmp 'validador-b2.log' @(
            ('[10:00:00.000] HELLO-C1-1 TX: ' + $hello), '[10:00:00.100] HELLO-C1-1 RX: 80 01 09 75',
            '[10:00:00.200] F0 TX: F0 00 0F', '[10:00:00.300] F0 RX: 00 02 10 22 CB',
            '[10:00:00.400] PG34-P0 TX: 34 03 00 00 A0 28', ('[10:00:00.500] PG34-P0 RX: ' + $p0),
            '[10:00:00.600] PG34-P80 TX: 34 03 00 50 A0 D8', ('[10:00:00.700] PG34-P80 RX: ' + $p50),
            '[10:00:00.800] PG34-4F TX: 34 03 00 4F A0 D9', ('[10:00:00.900] PG34-4F RX: ' + $p4f))
        $a = Invoke-Analysis $f 'auto'
        Assert-Class $a 'PG34-4F' 'ESPERADA' '*por palavra*' 'validador sem programa'
        Assert-Class $a 'PG34-P0' 'ESPERADA' '*estrutural*' 'validador sem programa'
        $a = Invoke-Analysis $f 'PE'
        Assert-Class $a 'PG34-4F' 'ESPERADA' '*por palavra*' 'validador PE'

        # 2b. Captura completa (RX com latência): 004F arredondado para 0050 e página com BRAW errado.
        $bad50 = Get-ModelPage $pe 0x50
        $bad50[162] = [byte](($bad50[162] + 1) -band 0x0F)
        $bad50 = New-Frame ($bad50[0..241])
        $f = Write-Fabricated $tmp 'fullcapture-PE.log' @(
            ('[10:00:00.000] HELLO TX: ' + $hello), '[10:00:00.100] HELLO RX(240ms): 80 01 09 75',
            '[10:00:01.000] PG34-0050 TX: 34 03 00 50 A0 D8', ('[10:00:01.100] PG34-0050 RX(300ms): ' + (ConvertTo-HexText $bad50) + ' | frames=1'),
            '[10:00:02.000] PG34-004F TX: 34 03 00 4F A0 D9', ('[10:00:02.100] PG34-004F RX(300ms): ' + $p50 + ' | frames=1'))
        $a = Invoke-Analysis $f 'auto'
        Assert-True ($a.Program -eq 'PE') 'programa inferido do nome do arquivo'
        Assert-Class $a 'PG34-004F' 'ALTERNATIVA PREVISTA' '*0x0050' 'fullcapture'
        Assert-Class $a 'PG34-0050' 'INESPERADA' '*BRAW*' 'fullcapture BRAW'

        # 2c. PG Lab: 38 de PA/PC/PD e alternativas de C1/C2/03.
        $f = Write-Fabricated $tmp 'pglab.log' @(
            ('[10:00:00.000] TX          HANDSHAKE tentativa 1  [' + $hello + '] soma=0x9F'),
            '[10:00:00.100] RX RAW      resposta ao HANDSHAKE  [80 01 09 75] soma=0xFF',
            '[10:00:00.200] TX          38  [38 00 C7] soma=0xFF',
            '[10:00:00.300] RX RAW      38  [00 02 00 2C D1] soma=0xFF',
            '[10:00:00.400] TX          C1 sentinela  [33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 D9] soma=0xFF',
            '[10:00:00.500] RX RAW      C1 sentinela  [00 00 FF] soma=0xFF',
            '[10:00:00.600] TX          C2 chk errado  [33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 DA] soma=0x00',
            '[10:00:00.700] RX RAW      C2 chk errado  [00 00 FF] soma=0xFF',
            '[10:00:00.800] TX          clear  [03 00 FC] soma=0xFF',
            '[10:00:00.900] RX RAW      clear  [80 01 12 6C] soma=0xFF')
        $a = Invoke-Analysis $f 'PA'
        Assert-Class $a '38' 'ESPERADA' 'PA*00 2C D1*' 'pglab PA'
        Assert-Class $a 'C1 sentinela' 'ESPERADA' 'ACK*' 'pglab C1'
        Assert-Class $a 'C2 chk errado' 'ALTERNATIVA PREVISTA' '*NÃO valida*' 'pglab C2'
        Assert-Class $a 'clear' 'INESPERADA' '*' 'pglab 03'
        $a = Invoke-Analysis $f 'PC'
        Assert-Class $a '38' 'INESPERADA' 'PC: valor 0x002C*' 'pglab PC'

        # 2d. Emulador: RUN com quadro proibido transmitido (violação) e 38 silencioso.
        $f = Write-Fabricated $tmp 'emulador.txt' @(
            ('[10:00:00.000] PC12 RAW                         [' + $hello + ']'),
            ('[10:00:00.000] PC12 -> EMU HELLO                [' + $hello + ']'),
            '[10:00:00.200] EMU -> PC12 HELLO                [C0 01 09 35] soma=0xFF',
            '[10:00:00.300] PC12 -> EMU #1 CMD=0xF0 LEN=0 CHK=OK [F0 00 0F] soma=0xFF',
            '[10:00:00.500] EMU -> PC12 F0 RUN (bit6)        [40 02 10 22 8B] soma=0xFF',
            '[10:00:00.600] PC12 -> EMU #2 CMD=0x34 LEN=3 CHK=OK [34 03 00 00 A0 28] soma=0xFF')
        $a = Invoke-Analysis $f 'PD'
        Assert-Class $a 'EMU HELLO' 'ESPERADA' '*RUN*' 'emulador'
        Assert-Class $a 'EMU #1 CMD=0xF0 LEN=0 CHK=OK' 'ESPERADA' '40 02*' 'emulador'
        Assert-Class $a 'EMU #2 CMD=0x34 LEN=3 CHK=OK' 'INESPERADA' 'VIOLAÇÃO*' 'emulador'

        # 2e. Chave=valor (formato da sessão física arquivada) e protocol-report.
        $f = Write-Fabricated $tmp 'sessao-PD.txt' @(
            'HELLO_RX=80 01 09 75', 'F0_TX=F0 00 0F', 'F0_RX=00 02 10 22 CB',
            '38_TX=38 00 C7', '38_RX=00 02 00 9E 5F',
            'PG34_0140_TX=34 03 01 40 A0 E7', 'PG34_0140_RX=')
        $a = Invoke-Analysis $f 'auto'
        Assert-Class $a 'HELLO' 'ESPERADA' '*STOP*' 'chave=valor'
        Assert-Class $a '38' 'ALTERNATIVA PREVISTA' '*limitado*' 'chave=valor'
        Assert-Class $a 'PG34_0140' 'INESPERADA' '*silêncio*' 'chave=valor'

        # 2f. RX ausente no log (não confundir com silêncio) e silêncios explícitos.
        $f = Write-Fabricated $tmp 'sessao-sem-rx.txt' @(
            'PG34_0000_TX=34 03 00 00 A0 28', 'PG34_0000_RX_STATUS=00', 'PG34_0000_RX_LEN=F0')
        $a = Invoke-Analysis $f 'PB'
        Assert-Class $a 'PG34_0000' 'INESPERADA' 'RX bruto ausente*' 'RX ausente'
        $f = Write-Fabricated $tmp 'emulador-silencio.txt' @(
            ('[10:00:00.000] PC12 -> EMU HELLO                [' + $hello + ']'),
            '[10:00:00.200] EMU -> PC12 HELLO                [80 01 09 75] soma=0xFF',
            '[10:00:00.300] PC12 -> EMU #1 CMD=0x34 LEN=3 CHK=OK [34 03 00 4F A0 D9] soma=0xFF',
            '[10:00:00.400] BANCADA 34 SILENCIOSO            START fora de multiplo de 0x50 descartado (SINTETICO/HIPOTESE).')
        $a = Invoke-Analysis $f 'PE'
        Assert-Class $a 'EMU #1 CMD=0x34 LEN=3 CHK=OK' 'ALTERNATIVA PREVISTA' 'silêncio*' 'emulador silêncio'
        $f = Write-Fabricated $tmp 'pglab-silencio.log' @(
            '[10:00:00.000] TX          F0 RUN  [F0 00 0F] soma=0xFF',
            '[10:00:00.100] RX RAW      F0 RUN')
        $a = Invoke-Analysis $f 'PD'
        Assert-Class $a 'F0 RUN' 'INESPERADA' '*00 02 10 22 CB*' 'pglab F0 vazio em STOP'

        # PB com resíduo do PA depois do END (resposta à pendência 1 / P0.1).
        $pb = Read-ProgramWords 'PB'
        $res = Get-ModelPage $pb 0
        $res[6] = 0x20; $res[7] = 0x41; $res[162 + 2] = Get-Braw 0x20 0x41
        $res = New-Frame ($res[0..241])
        $f = Write-Fabricated $tmp 'protocol-report.txt' @(
            'TP02 FULL PROTOCOL CAPTURE',
            ('001 | HELLO | 240ms | TX=' + $hello + ' | RX=80 01 09 75 | frames=1 | context='),
            ('002 | PG34-0000 | 300ms | TX=34 03 00 00 A0 28 | RX=' + (ConvertTo-HexText $res) + ' | frames=1 | context='))
        $a = Invoke-Analysis $f 'PB'
        Assert-Class $a 'PG34-0000' 'ALTERNATIVA PREVISTA' '*resíduo*' 'protocol-report'
        Assert-True ($a.Report -like '*Pendências 6 e 7*') 'relatório agrupa por pendência'
    }
    finally {
        Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
    }
    if ($script:fails -gt 0) {
        Write-Host ('AnalyzeTp02BancadaFirmware self-test: ' + $script:fails + ' falha(s) em ' + $script:checks + ' verificações.')
        return 1
    }
    Write-Host ('AnalyzeTp02BancadaFirmware self-test: PASS (' + $script:checks + ' verificações; COM=NÃO ABERTA)')
    return 0
}

# ------------------------------------------------------------------ principal

if ($SelfTest) { exit (Invoke-SelfTest) }

if ($null -eq $Path -or $Path.Count -eq 0) {
    Write-Host 'Uso: AnalyzeTp02BancadaFirmware.ps1 -Path <log|pasta> [-Programa PA|PB|PC|PD|PE] [-Out relatório.txt]'
    Write-Host '     AnalyzeTp02BancadaFirmware.ps1 -SelfTest [-EnsaioDir <pasta do RunTp02BancadaEnsaio.bat>]'
    exit 2
}

$files = New-Object 'System.Collections.Generic.List[string]'
foreach ($p in $Path) {
    if (Test-Path $p -PathType Container) {
        foreach ($f in Get-ChildItem -Path $p -Recurse -File | Where-Object { $_.Extension -in @('.log', '.txt', '.csv') -and $_.Name -notlike '*.analise.txt' }) { $files.Add($f.FullName) }
    } else { $files.Add((Resolve-Path $p).Path) }
}

$all = New-Object System.Text.StringBuilder
$unexpected = 0
foreach ($f in $files) {
    $a = Invoke-Analysis $f $Programa
    if ($a.Results.Count -eq 0 -and $a.Parsed.Blocked.Count -eq 0) { continue }
    [void]$all.AppendLine($a.Report)
    $unexpected += @($a.Results | Where-Object { $_.Classe -eq 'INESPERADA' }).Count
}
$text = $all.ToString()
if ($text.Length -eq 0) { $text = 'Nenhuma troca TX/RX reconhecida nos arquivos informados.' }
Write-Host $text
if (-not [string]::IsNullOrWhiteSpace($Out)) {
    [IO.File]::WriteAllText($Out, $text, (New-Object System.Text.UTF8Encoding($true)))
    Write-Host ('Relatório: ' + $Out)
}
if ($unexpected -gt 0) { exit 3 }
exit 0
