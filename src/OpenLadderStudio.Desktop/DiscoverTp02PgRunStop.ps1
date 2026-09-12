param(
    [ValidateSet('STOP','RUN','BOTH')][string]$Action = 'BOTH',
    [string]$Port = '',
    [int]$TimeoutSeconds = 120,
    [switch]$Rebuild
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $base

function Banner([string]$text) {
    Write-Host ''
    Write-Host '============================================================'
    Write-Host (' TP02 PG - ' + $text)
    Write-Host '============================================================'
}

function Hex([byte[]]$data) {
    return (($data | ForEach-Object { $_.ToString('X2') }) -join ' ')
}

function Snapshot([string]$dir) {
    $h = @{}
    if (Test-Path $dir) {
        Get-ChildItem -LiteralPath $dir -Filter '*-unknown-*.bin' -ErrorAction SilentlyContinue | ForEach-Object {
            $h[$_.FullName.ToLowerInvariant()] = $true
        }
    }
    return $h
}

function Decode([string]$path, [string]$label) {
    [byte[]]$b = [IO.File]::ReadAllBytes($path)
    if ($b.Length -lt 3) { throw "Quadro curto: $path" }
    $sum = 0
    foreach ($x in $b) { $sum = ($sum + [int]$x) -band 0xFF }
    return New-Object PSObject -Property @{
        Label = $label
        Path = $path
        Command = [int]$b[0]
        PayloadLength = [int]$b[1]
        TotalLength = $b.Length
        ChecksumOk = ($sum -eq 0xFF)
        Hex = Hex $b
    }
}

function Capture([string]$label, [string]$portName, [string]$exe, [string]$dir) {
    Banner ('CAPTURA ' + $label)
    Write-Host ('Emulador na ' + $portName + '; PC12 na outra ponta do par virtual.')
    Write-Host ('No PC12 original, execute SOMENTE ' + $label + ' agora.')
    Write-Host 'Nenhum opcode desconhecido recebera ACK nesta captura.'

    $before = Snapshot $dir
    $p = Start-Process -FilePath $exe -ArgumentList @($portName,'--no-auto-ack','--pg33-ack') -PassThru -NoNewWindow
    try {
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $found = $null
        while ((Get-Date) -lt $deadline -and $null -eq $found) {
            $found = Get-ChildItem -LiteralPath $dir -Filter '*-unknown-*.bin' -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Where-Object { -not $before.ContainsKey($_.FullName.ToLowerInvariant()) } |
                Select-Object -First 1
            if ($null -eq $found) { Start-Sleep -Milliseconds 200 }
        }
        if ($null -eq $found) { throw ("Timeout aguardando quadro desconhecido de " + $label) }
        Start-Sleep -Milliseconds 250
        $r = Decode $found.FullName $label
        Write-Host ('CMD=0x{0:X2} LEN={1} CHECKSUM={2}' -f $r.Command,$r.PayloadLength,$(if ($r.ChecksumOk) {'OK'} else {'ERRO'}))
        Write-Host ('HEX=' + $r.Hex)
        return $r
    }
    finally {
        try { if ($null -ne $p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } } catch { }
        Start-Sleep -Milliseconds 250
    }
}

if ($TimeoutSeconds -lt 10 -or $TimeoutSeconds -gt 600) { throw 'TimeoutSeconds deve estar entre 10 e 600.' }

$build = Join-Path $base 'BuildTp02Emulator.bat'
$exe = Join-Path $base 'OpenLadderTP02Emulator.exe'
$dir = Join-Path $base 'tp02-emulator-captures'
[IO.Directory]::CreateDirectory($dir) | Out-Null

if ($Rebuild -or -not (Test-Path $exe)) {
    & $build
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o emulador.' }
}
if (-not (Test-Path $exe)) { throw 'OpenLadderTP02Emulator.exe nao encontrado.' }

if ([string]::IsNullOrWhiteSpace($Port)) {
    $Port = (Read-Host 'COM VIRTUAL usada pelo emulador (ex.: COM11)').Trim()
}
if ([string]::IsNullOrWhiteSpace($Port)) { throw 'Porta COM nao informada.' }

$results = @()
if ($Action -eq 'STOP' -or $Action -eq 'BOTH') { $results += Capture 'STOP' $Port $exe $dir }
if ($Action -eq 'BOTH') {
    Write-Host ''
    Write-Host 'Reabra/reconecte o PC12 na mesma porta virtual se necessario.'
    [void](Read-Host 'Pressione ENTER para iniciar a fase RUN')
}
if ($Action -eq 'RUN' -or $Action -eq 'BOTH') { $results += Capture 'RUN' $Port $exe $dir }

$report = New-Object Text.StringBuilder
[void]$report.AppendLine('TP02 PG - RUN/STOP discovery')
[void]$report.AppendLine(('Data=' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')))
[void]$report.AppendLine(('EmulatorPort=' + $Port))
[void]$report.AppendLine('PhysicalPLC=NO')
[void]$report.AppendLine('UnknownAck=OFF')
[void]$report.AppendLine('')
foreach ($r in $results) {
    [void]$report.AppendLine(('[' + $r.Label + ']'))
    [void]$report.AppendLine(('CMD=0x{0:X2}' -f $r.Command))
    [void]$report.AppendLine(('LEN=' + $r.PayloadLength))
    [void]$report.AppendLine(('TOTAL=' + $r.TotalLength))
    [void]$report.AppendLine(('CHECKSUM_FF=' + $(if ($r.ChecksumOk) {'OK'} else {'ERRO'})))
    [void]$report.AppendLine(('HEX=' + $r.Hex))
    [void]$report.AppendLine(('FILE=' + $r.Path))
    [void]$report.AppendLine('')
}

if ($results.Count -eq 2) {
    $s = $results | Where-Object { $_.Label -eq 'STOP' } | Select-Object -First 1
    $r = $results | Where-Object { $_.Label -eq 'RUN' } | Select-Object -First 1
    [void]$report.AppendLine('[COMPARE]')
    [void]$report.AppendLine(('CMD_STOP=0x{0:X2}' -f $s.Command))
    [void]$report.AppendLine(('CMD_RUN=0x{0:X2}' -f $r.Command))
    [void]$report.AppendLine(('SAME_OPCODE=' + $(if ($s.Command -eq $r.Command) {'YES'} else {'NO'})))
    [void]$report.AppendLine(('SAME_FRAME=' + $(if ($s.Hex -eq $r.Hex) {'YES'} else {'NO'})))
    if ($s.Command -eq $r.Command -and $s.Hex -ne $r.Hex) {
        [void]$report.AppendLine('NOTE=Mesmo opcode; RUN/STOP diferem no payload.')
    } elseif ($s.Command -ne $r.Command) {
        [void]$report.AppendLine('NOTE=RUN/STOP aparentam usar opcodes distintos.')
    } else {
        [void]$report.AppendLine('NOTE=Frames identicos; repetir captura antes de implementar.')
    }
    [void]$report.AppendLine('')
}

[void]$report.AppendLine('NEXT=Nao enviar estes quadros ao PLC fisico ainda. Primeiro confirmar resposta/semantica; depois implementar no OpenLadder e validar uma vez em bancada.')
$reportPath = Join-Path $dir ('TP02-PG-RUNSTOP-DISCOVERY-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')
[IO.File]::WriteAllText($reportPath,$report.ToString(),[Text.Encoding]::UTF8)

Banner 'CONCLUIDO'
Write-Host ('Relatorio: ' + $reportPath)
Write-Host $report.ToString()
