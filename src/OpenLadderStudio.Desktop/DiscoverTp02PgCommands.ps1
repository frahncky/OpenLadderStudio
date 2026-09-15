param(
    [string]$Port = '',
    [string[]]$Actions = @('STOP','RUN','MONITOR_START','MONITOR_STOP','READ_PROGRAM','WRITE_PROGRAM','CLEAR_SYSTEM','CLEAR_DATA','CLEAR_PROGRAM','CLEAR_ALL_MEMORY'),
    [ValidateSet('SILENT','GENERIC')][string]$AckMode = 'GENERIC',
    [int]$CaptureSeconds = 8,
    [switch]$Rebuild,
    [switch]$VirtualOnlyConfirmed,
    [switch]$SelfTest
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $base

function Hex([byte[]]$data) {
    return (($data | ForEach-Object { $_.ToString('X2') }) -join ' ')
}

function Sum8([byte[]]$data) {
    $sum = 0
    if ($null -ne $data) {
        foreach ($x in $data) { $sum = ($sum + [int]$x) -band 0xFF }
    }
    return $sum
}

function Get-StaticClassification([byte[]]$data) {
    if ($null -eq $data -or $data.Length -ne 3) { return 'UNCLASSIFIED' }
    $hex = Hex $data
    switch ($hex) {
        '01 00 FE' { return 'STOP_PROGRAM_MODE_STATIC_CONFIRMED' }
        '02 00 FD' { return 'RUN_STATIC_CONFIRMED' }
        '03 00 FC' { return 'CLEAR_PROGRAM_STATIC_CONFIRMED' }
        '04 00 FB' { return 'CLEAR_SYSTEM_STATIC_CONFIRMED' }
        '0F 00 F0' { return 'CLEAR_ALL_MEMORY_STATIC_CONFIRMED_DESTRUCTIVE' }
        '11 00 EE' { return 'CLEAR_DATA_STATIC_CONFIRMED' }
        default { return 'UNCLASSIFIED' }
    }
}

function Decode([string]$path, [string]$label) {
    [byte[]]$b = [IO.File]::ReadAllBytes($path)
    if ($b.Length -lt 3) { return $null }
    return New-Object PSObject -Property @{
        Action = $label
        Path = $path
        Command = [int]$b[0]
        PayloadLength = [int]$b[1]
        TotalLength = $b.Length
        ChecksumOk = ((Sum8 $b) -eq 0xFF)
        Hex = Hex $b
        Classification = Get-StaticClassification $b
    }
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

function Capture-Action([string]$label, [string]$portName, [string]$exe, [string]$dir) {
    Write-Host ''
    Write-Host '============================================================'
    Write-Host (' ACAO: ' + $label)
    Write-Host '============================================================'
    Write-Host ('Emulador: ' + $portName + ' | ACK desconhecido: ' + $AckMode)
    Write-Host ('No PC12 original, prepare a acao ' + $label + '.')
    [void](Read-Host 'Pressione ENTER e execute a acao no PC12 imediatamente')

    $before = Snapshot $dir
    $args = @($portName, '--pg33-ack')
    if ($AckMode -eq 'GENERIC') { $args += '--auto-ack' } else { $args += '--no-auto-ack' }

    $p = Start-Process -FilePath $exe -ArgumentList $args -PassThru -NoNewWindow
    try {
        $deadline = (Get-Date).AddSeconds($CaptureSeconds)
        while ((Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    }
    finally {
        try { if ($null -ne $p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } } catch { }
        Start-Sleep -Milliseconds 250
    }

    $rows = @()
    Get-ChildItem -LiteralPath $dir -Filter '*-unknown-*.bin' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime |
        Where-Object { -not $before.ContainsKey($_.FullName.ToLowerInvariant()) } |
        ForEach-Object {
            $r = Decode $_.FullName $label
            if ($null -ne $r) { $rows += $r }
        }

    if ($rows.Count -eq 0) {
        Write-Host 'Nenhum opcode desconhecido novo apareceu nessa janela.'
        return @()
    }

    $unique = @{}
    $dedup = @()
    foreach ($r in $rows) {
        $key = $r.Hex
        if (-not $unique.ContainsKey($key)) {
            $unique[$key] = $true
            $dedup += $r
            Write-Host ('CMD=0x{0:X2} LEN={1} CHK={2} CLASS={3} HEX={4}' -f $r.Command,$r.PayloadLength,$(if ($r.ChecksumOk) {'OK'} else {'ERRO'}),$r.Classification,$r.Hex)
        }
    }
    return $dedup
}

function Run-SelfTest {
    $cases = @(
        @{ Hex='01 00 FE'; Name='STOP_PROGRAM_MODE_STATIC_CONFIRMED' },
        @{ Hex='02 00 FD'; Name='RUN_STATIC_CONFIRMED' },
        @{ Hex='03 00 FC'; Name='CLEAR_PROGRAM_STATIC_CONFIRMED' },
        @{ Hex='04 00 FB'; Name='CLEAR_SYSTEM_STATIC_CONFIRMED' },
        @{ Hex='0F 00 F0'; Name='CLEAR_ALL_MEMORY_STATIC_CONFIRMED_DESTRUCTIVE' },
        @{ Hex='11 00 EE'; Name='CLEAR_DATA_STATIC_CONFIRMED' }
    )
    $checks = 0
    foreach ($c in $cases) {
        [byte[]]$b = $c.Hex.Split(' ') | ForEach-Object { [Convert]::ToByte($_,16) }
        if ((Sum8 $b) -ne 0xFF) { throw ('Checksum falhou: ' + $c.Hex) }
        if ((Get-StaticClassification $b) -ne $c.Name) { throw ('Classificacao falhou: ' + $c.Hex) }
        $checks += 2
    }
    [byte[]]$unknown = 0x7E,0x00,0x81
    if ((Sum8 $unknown) -ne 0xFF) { throw 'Fixture desconhecido nao fecha FF.' }
    if ((Get-StaticClassification $unknown) -ne 'UNCLASSIFIED') { throw 'Fixture desconhecido foi classificado indevidamente.' }
    $checks += 2
    Write-Host ('TP02 PG command discovery self-test: PASS checks=' + $checks)
    Write-Host 'static=01_STOP_PROGRAM+02_RUN+03_CLEAR_PROGRAM+04_CLEAR_SYSTEM+0F_CLEAR_ALL+11_CLEAR_DATA; physical_plc=NAO UTILIZADO'
}

if ($SelfTest) {
    Run-SelfTest
    exit 0
}

if ($CaptureSeconds -lt 2 -or $CaptureSeconds -gt 120) { throw 'CaptureSeconds deve estar entre 2 e 120.' }
if ($Actions.Count -eq 0) { throw 'Informe pelo menos uma acao.' }

if (-not $VirtualOnlyConfirmed) {
    Write-Host 'ATENCAO: esta rotina e SOMENTE para par COM virtual + PC12 original.'
    Write-Host 'Nao conecte o emulador a uma COM fisica do PLC.'
    $confirm = (Read-Host 'Digite VIRTUAL para continuar').Trim().ToUpperInvariant()
    if ($confirm -ne 'VIRTUAL') { throw 'Execucao cancelada: bancada virtual nao confirmada.' }
}

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
foreach ($action in $Actions) {
    $label = ($action.Trim().ToUpperInvariant() -replace '[^A-Z0-9_-]','_')
    if ($label.Length -eq 0) { continue }
    $results += Capture-Action $label $Port $exe $dir
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$txtPath = Join-Path $dir ('TP02-PG-COMMAND-DISCOVERY-' + $stamp + '.txt')
$csvPath = Join-Path $dir ('TP02-PG-COMMAND-DISCOVERY-' + $stamp + '.csv')

$report = New-Object Text.StringBuilder
[void]$report.AppendLine('TP02 PG - command discovery matrix')
[void]$report.AppendLine(('Data=' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')))
[void]$report.AppendLine(('EmulatorPort=' + $Port))
[void]$report.AppendLine(('UnknownAck=' + $AckMode))
[void]$report.AppendLine('PhysicalPLC=NO')
[void]$report.AppendLine('STATIC_CONFIRMED_REQUESTS=01 00 FE STOP/PROGRAM; 02 00 FD RUN; 03 00 FC CLEAR_PROGRAM; 04 00 FB CLEAR_SYSTEM; 0F 00 F0 CLEAR_ALL_MEMORY; 11 00 EE CLEAR_DATA')
[void]$report.AppendLine('SYNTHETIC_EMPTY_SUCCESS=00 00 FF (parser-compatible; physical response not yet captured)')
[void]$report.AppendLine('WARNING=03/04/0F/11 sao comandos destrutivos; esta rotina e exclusivamente para COM virtual.')
[void]$report.AppendLine('')

foreach ($r in $results) {
    [void]$report.AppendLine(('[{0}] CMD=0x{1:X2} LEN={2} TOTAL={3} CHECKSUM_FF={4} CLASS={5}' -f $r.Action,$r.Command,$r.PayloadLength,$r.TotalLength,$(if ($r.ChecksumOk) {'OK'} else {'ERRO'}),$r.Classification))
    [void]$report.AppendLine(('HEX=' + $r.Hex))
    [void]$report.AppendLine(('FILE=' + $r.Path))
    [void]$report.AppendLine('')
}

if ($results.Count -eq 0) { [void]$report.AppendLine('RESULT=nenhum quadro desconhecido novo capturado.') }
[void]$report.AppendLine('NEXT=usar a bancada virtual para observar sequencias posteriores; manter 00 00 FF como ACK sintetico ate captura da resposta real do TP02.')

[IO.File]::WriteAllText($txtPath,$report.ToString(),[Text.Encoding]::UTF8)
if ($results.Count -gt 0) {
    $results | Select-Object Action,@{N='CommandHex';E={'0x{0:X2}' -f $_.Command}},PayloadLength,TotalLength,ChecksumOk,Classification,Hex,Path |
        Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
} else {
    'Action,CommandHex,PayloadLength,TotalLength,ChecksumOk,Classification,Hex,Path' | Set-Content -LiteralPath $csvPath -Encoding UTF8
}

Write-Host ''
Write-Host '============================================================'
Write-Host ' CONCLUIDO'
Write-Host '============================================================'
Write-Host ('TXT: ' + $txtPath)
Write-Host ('CSV: ' + $csvPath)
Write-Host $report.ToString()
