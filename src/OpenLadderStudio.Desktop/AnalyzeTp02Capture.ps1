param(
    [string]$Path,
    [string]$Compare
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-LatestCapture {
    $dir = Join-Path $PSScriptRoot 'tp02-emulator-captures'
    if (-not (Test-Path $dir)) { return $null }
    $file = Get-ChildItem -Path $dir -Filter '*-raw.bin' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
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
        0xF0 { return 'F0 preflight/status' }
        0x38 { return '38 preambulo leitura' }
        0x34 { return '34 leitura de programa' }
        0x0A { return '0A leitura de memoria' }
        0x14 { return '14 consulta auxiliar' }
        0x0F { return '0F candidato destrutivo/clear - NAO usar em PLC fisico' }
        default { return 'DESCONHECIDO' }
    }
}

function Parse-Capture {
    param([string]$CapturePath)

    if (-not (Test-Path $CapturePath)) {
        throw "Captura nao encontrada: $CapturePath"
    }

    [byte[]]$data = [System.IO.File]::ReadAllBytes((Resolve-Path $CapturePath))
    $frames = New-Object System.Collections.Generic.List[object]
    $i = 0
    $seq = 0

    while ($i -lt $data.Length) {
        if (Test-HelloAt -Data $data -Offset $i) {
            $seq++
            $frames.Add([pscustomobject]@{
                Seq = $seq
                Offset = $i
                Cmd = 'ASCII'
                Len = 8
                Total = 8
                Checksum = '-'
                Kind = 'HELLO CON-ICB'
                Hex = Format-Hex -Data $data -Offset $i -Length 8
            })
            $i += 8
            continue
        }

        if (($i + 2) -lt $data.Length) {
            $payloadLen = [int]$data[$i + 1]
            $total = $payloadLen + 3
            if ($total -ge 3 -and ($i + $total) -le $data.Length) {
                if (Test-SumFF -Data $data -Offset $i -Length $total) {
                    $seq++
                    $cmd = $data[$i]
                    $frames.Add([pscustomobject]@{
                        Seq = $seq
                        Offset = $i
                        Cmd = ('0x{0:X2}' -f $cmd)
                        Len = $payloadLen
                        Total = $total
                        Checksum = 'FF OK'
                        Kind = Get-KnownName -Cmd $cmd
                        Hex = Format-Hex -Data $data -Offset $i -Length $total
                    })
                    $i += $total
                    continue
                }
            }
        }

        $i++
    }

    return [pscustomobject]@{
        Path = (Resolve-Path $CapturePath).Path
        Bytes = $data.Length
        Frames = $frames.ToArray()
    }
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

    $Result.Frames | Format-Table Seq,Offset,Cmd,Len,Total,Checksum,Kind -AutoSize

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
            Write-Host ('  A {0} {1}  {2}' -f $fa.Cmd,$fa.Kind,$fa.Hex)
            Write-Host ('  B {0} {1}  {2}' -f $fb.Cmd,$fb.Kind,$fb.Hex)
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
