param(
    [string]$Path,
    [string]$Compare
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function New-ObjectCompat {
    param([hashtable]$Properties)
    $o = New-Object PSObject
    foreach ($key in $Properties.Keys) {
        $o | Add-Member NoteProperty $key $Properties[$key]
    }
    return $o
}

function New-FrameObject {
    param(
        [int]$Seq,
        [int]$Offset,
        [string]$Cmd,
        [int]$Len,
        [int]$Total,
        [string]$Checksum,
        [string]$Kind,
        [string]$Hex,
        [string]$Pg33Start = '-',
        [int]$Pg33Words = -1,
        [bool]$W1A = $false,
        [string]$WordsHex = ''
    )
    return (New-ObjectCompat @{
        Seq = $Seq
        Offset = $Offset
        Cmd = $Cmd
        Len = $Len
        Total = $Total
        Checksum = $Checksum
        Kind = $Kind
        Pg33Start = $Pg33Start
        Pg33Words = $Pg33Words
        W1A = $W1A
        WordsHex = $WordsHex
        Hex = $Hex
    })
}

function New-AnalysisObject {
    param([string]$ResolvedPath, [int]$Bytes, [object[]]$Frames)
    return (New-ObjectCompat @{
        Path = $ResolvedPath
        Bytes = $Bytes
        Frames = $Frames
    })
}

function Get-LatestCapture {
    $dir = Join-Path $ScriptDir 'tp02-emulator-captures'
    if (-not (Test-Path $dir)) { return $null }
    $file = Get-ChildItem -Path $dir -Filter '*-raw.bin' |
        Where-Object { -not $_.PSIsContainer } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($file -eq $null) { return $null }
    return $file.FullName
}

function Test-HelloAt {
    param([byte[]]$Data, [int]$Offset)
    [byte[]]$hello = 0x43,0x4F,0x4E,0x2D,0x49,0x43,0x42,0x0D
    if (($Offset + $hello.Length) -gt $Data.Length) { return $false }
    for ($i = 0; $i -lt $hello.Length; $i++) {
        if ($Data[$Offset + $i] -ne $hello[$i]) { return $false }
    }
    return $true
}

function Test-SumFF {
    param([byte[]]$Data, [int]$Offset, [int]$Length)
    $sum = 0
    for ($i = 0; $i -lt $Length; $i++) {
        $sum = ($sum + [int]$Data[$Offset + $i]) -band 0xFF
    }
    return ($sum -eq 0xFF)
}

function Format-Hex {
    param([byte[]]$Data, [int]$Offset, [int]$Length, [int]$Max = 96)
    $n = [Math]::Min($Length, $Max)
    $parts = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $n; $i++) {
        $parts.Add($Data[$Offset + $i].ToString('X2'))
    }
    $text = [string]::Join(' ', $parts.ToArray())
    if ($Length -gt $Max) { $text += ' ...' }
    return $text
}

function Get-KnownName {
    param([byte]$Cmd)
    switch ($Cmd) {
        0x33 { return '33 Write Program Data (confirmado offline PC12)' }
        0xF0 { return 'F0 preflight/status' }
        0x38 { return '38 preambulo leitura' }
        0x34 { return '34 leitura de programa' }
        0x0A { return '0A leitura de memoria' }
        0x14 { return '14 consulta auxiliar' }
        0x0F { return '0F candidato destrutivo/clear - NAO usar em PLC fisico' }
        default { return 'DESCONHECIDO' }
    }
}

function Decode-Pg33 {
    param([byte[]]$Data, [int]$Offset, [int]$Total)

    $bad = {
        param([string]$Reason)
        return (New-ObjectCompat @{
            Valid = $false
            Reason = $Reason
            Start = 0
            WordCount = 0
            WordsHex = ''
            W1A = $false
        })
    }

    if ($Total -lt 10) { return (& $bad 'quadro curto') }
    if ($Data[$Offset] -ne 0x33) { return (& $bad 'opcode != 33') }
    if ($Data[$Offset + 2] -ne 0x00) { return (& $bad 'TX[2] != 00') }

    $hlBytes = [int]$Data[$Offset + 5]
    if ($hlBytes -le 0 -or (($hlBytes -band 1) -ne 0)) {
        return (& $bad 'TX[5] nao e 2*W par')
    }

    $wordCount = [int]($hlBytes / 2)
    if ($wordCount -lt 1 -or $wordCount -gt 80) {
        return (& $bad 'W fora de 1..80')
    }

    $expectedLen = (3 * $wordCount) + 4
    if ([int]$Data[$Offset + 1] -ne $expectedLen) {
        return (& $bad ('LEN={0}, esperado={1}' -f [int]$Data[$Offset + 1],$expectedLen))
    }

    if ($Total -ne ($expectedLen + 3)) {
        return (& $bad 'comprimento total inconsistente')
    }

    $start = ([int]$Data[$Offset + 3] * 256) + [int]$Data[$Offset + 4]
    if ($start -lt 0 -or $start -ge 4000 -or ($start + $wordCount) -gt 4000) {
        return (& $bad 'faixa de passos fora de 0..3999')
    }

    $hlStart = $Offset + 6
    $exStart = $hlStart + $hlBytes
    $checksumIndex = $Offset + $Total - 1
    if (($exStart + $wordCount) -ne $checksumIndex) {
        return (& $bad 'planos HL/EX nao fecham antes do checksum')
    }

    $words = New-Object System.Collections.Generic.List[string]
    $w1aBytes = New-Object System.Collections.Generic.List[int]

    for ($i = 0; $i -lt $wordCount; $i++) {
        $h = [int]$Data[$hlStart + (2 * $i)]
        $l = [int]$Data[$hlStart + (2 * $i) + 1]
        $e = [int]$Data[$exStart + $i]
        $words.Add(('{0:X2} {1:X2} {2:X2}' -f $h,$l,$e))
        if ($i -lt 3) {
            $w1aBytes.Add($h)
            $w1aBytes.Add($l)
            $w1aBytes.Add($e)
        }
    }

    $isW1A = $false
    if ($wordCount -ge 3 -and $w1aBytes.Count -ge 9) {
        [int[]]$expected = 0x00,0x10,0x00,0x20,0x40,0x00,0x00,0x70,0x00
        $isW1A = $true
        for ($j = 0; $j -lt 9; $j++) {
            if ($w1aBytes[$j] -ne $expected[$j]) {
                $isW1A = $false
                break
            }
        }
    }

    return (New-ObjectCompat @{
        Valid = $true
        Reason = ''
        Start = $start
        WordCount = $wordCount
        WordsHex = [string]::Join(' | ', $words.ToArray())
        W1A = $isW1A
    })
}

function Parse-Capture {
    param([string]$CapturePath)

    if (-not (Test-Path $CapturePath)) {
        throw "Captura nao encontrada: $CapturePath"
    }

    $resolved = (Resolve-Path $CapturePath).Path
    [byte[]]$data = [System.IO.File]::ReadAllBytes($resolved)
    $frames = New-Object System.Collections.Generic.List[object]
    $i = 0
    $seq = 0

    while ($i -lt $data.Length) {
        if (Test-HelloAt -Data $data -Offset $i) {
            $seq++
            $frames.Add((New-FrameObject -Seq $seq -Offset $i -Cmd 'ASCII' -Len 8 -Total 8 -Checksum '-' -Kind 'HELLO CON-ICB' -Hex (Format-Hex -Data $data -Offset $i -Length 8)))
            $i += 8
            continue
        }

        if (($i + 2) -lt $data.Length) {
            $payloadLen = [int]$data[$i + 1]
            $total = $payloadLen + 3
            if ($total -ge 3 -and ($i + $total) -le $data.Length) {
                if (Test-SumFF -Data $data -Offset $i -Length $total) {
                    $seq++
                    $cmd = [byte]$data[$i]
                    $kind = Get-KnownName -Cmd $cmd
                    $pgStart = '-'
                    $pgWords = -1
                    $w1a = $false
                    $wordsHex = ''

                    if ($cmd -eq 0x33) {
                        $pg = Decode-Pg33 -Data $data -Offset $i -Total $total
                        if ($pg.Valid) {
                            $pgStart = ('0x{0:X4}' -f $pg.Start)
                            $pgWords = $pg.WordCount
                            $w1a = $pg.W1A
                            $wordsHex = $pg.WordsHex
                            if ($w1a) { $kind += ' | W1A reconhecido' }
                        }
                        else {
                            $kind += ' | GEOMETRIA INVALIDA: ' + $pg.Reason
                        }
                    }

                    $frames.Add((New-FrameObject `
                        -Seq $seq `
                        -Offset $i `
                        -Cmd ('0x{0:X2}' -f $cmd) `
                        -Len $payloadLen `
                        -Total $total `
                        -Checksum 'FF OK' `
                        -Kind $kind `
                        -Hex (Format-Hex -Data $data -Offset $i -Length $total) `
                        -Pg33Start $pgStart `
                        -Pg33Words $pgWords `
                        -W1A $w1a `
                        -WordsHex $wordsHex))
                    $i += $total
                    continue
                }
            }
        }

        $i++
    }

    return (New-AnalysisObject -ResolvedPath $resolved -Bytes $data.Length -Frames $frames.ToArray())
}

function Show-Analysis {
    param($Result)

    Write-Host ''
    Write-Host '============================================================'
    Write-Host ' TP02 PG - ANALISE DE CAPTURA PC12 -> EMULADOR'
    Write-Host '============================================================'
    Write-Host ('Arquivo : {0}' -f $Result.Path)
    Write-Host ('Bytes   : {0}' -f $Result.Bytes)
    Write-Host ('Frames  : {0}' -f $Result.Frames.Count)
    Write-Host ''

    if ($Result.Frames.Count -eq 0) {
        Write-Host 'Nenhum frame com soma modulo 256 = FF foi encontrado.' -ForegroundColor Yellow
        return
    }

    $Result.Frames | Format-Table Seq,Offset,Cmd,Len,Total,Checksum,Pg33Start,Pg33Words,W1A,Kind -AutoSize

    Write-Host ''
    Write-Host 'Write Program Data / PG33:'
    $pg33 = @($Result.Frames | Where-Object { $_.Cmd -eq '0x33' })
    if ($pg33.Count -eq 0) {
        Write-Host '  nenhum quadro 0x33 encontrado.' -ForegroundColor Yellow
    }
    else {
        foreach ($f in $pg33) {
            Write-Host ('  #{0} start={1} words={2} W1A={3}' -f $f.Seq,$f.Pg33Start,$f.Pg33Words,$f.W1A) -ForegroundColor Green
            if (-not [string]::IsNullOrWhiteSpace($f.WordsHex)) {
                Write-Host ('     words: {0}' -f $f.WordsHex)
            }
        }
    }

    Write-Host ''
    Write-Host 'Frames desconhecidos:'
    $unknown = @($Result.Frames | Where-Object { $_.Kind -eq 'DESCONHECIDO' })
    if ($unknown.Count -eq 0) {
        Write-Host '  nenhum' -ForegroundColor Green
    }
    else {
        foreach ($f in $unknown) {
            Write-Host ('  #{0} offset={1} cmd={2} len={3}  {4}' -f $f.Seq,$f.Offset,$f.Cmd,$f.Len,$f.Hex) -ForegroundColor Cyan
        }
    }

    Write-Host ''
    Write-Host 'Resumo por opcode:'
    $Result.Frames |
        Where-Object { $_.Cmd -ne 'ASCII' } |
        Group-Object Cmd |
        Sort-Object Name |
        ForEach-Object {
            Write-Host ('  {0}: {1} frame(s)' -f $_.Name,$_.Count)
        }
}

function Compare-Analysis {
    param($A, $B)

    Write-Host ''
    Write-Host '============================================================'
    Write-Host ' COMPARACAO DE DUAS CAPTURAS'
    Write-Host '============================================================'
    Write-Host ('A: {0}' -f $A.Path)
    Write-Host ('B: {0}' -f $B.Path)
    Write-Host ''

    $max = [Math]::Max($A.Frames.Count, $B.Frames.Count)
    for ($i = 0; $i -lt $max; $i++) {
        $fa = if ($i -lt $A.Frames.Count) { $A.Frames[$i] } else { $null }
        $fb = if ($i -lt $B.Frames.Count) { $B.Frames[$i] } else { $null }

        if ($fa -eq $null) {
            Write-Host ('+ somente B #{0}: {1}' -f ($i + 1), $fb.Hex) -ForegroundColor Green
            continue
        }
        if ($fb -eq $null) {
            Write-Host ('- somente A #{0}: {1}' -f ($i + 1), $fa.Hex) -ForegroundColor Yellow
            continue
        }

        if ($fa.Hex -ne $fb.Hex) {
            Write-Host ('* frame #{0} mudou' -f ($i + 1)) -ForegroundColor Cyan
            Write-Host ('  A {0} {1}' -f $fa.Cmd,$fa.Kind)
            if ($fa.Cmd -eq '0x33') {
                Write-Host ('    start={0} words={1}: {2}' -f $fa.Pg33Start,$fa.Pg33Words,$fa.WordsHex)
            } else {
                Write-Host ('    {0}' -f $fa.Hex)
            }
            Write-Host ('  B {0} {1}' -f $fb.Cmd,$fb.Kind)
            if ($fb.Cmd -eq '0x33') {
                Write-Host ('    start={0} words={1}: {2}' -f $fb.Pg33Start,$fb.Pg33Words,$fb.WordsHex)
            } else {
                Write-Host ('    {0}' -f $fb.Hex)
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($Path)) {
    $Path = Get-LatestCapture
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'Informe -Path ou gere uma captura em tp02-emulator-captures.'
    }
}

$result = Parse-Capture -CapturePath $Path
Show-Analysis -Result $result

$csv = [System.IO.Path]::ChangeExtension($result.Path, '.frames.csv')
$result.Frames | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8
Write-Host ''
Write-Host ('CSV: {0}' -f $csv)

if (-not [string]::IsNullOrWhiteSpace($Compare)) {
    $resultB = Parse-Capture -CapturePath $Compare
    Compare-Analysis -A $result -B $resultB
}
