param(
    [ValidateSet('STOP','RUN','BOTH')]
    [string]$Action = 'BOTH',
    [string]$Port = '',
    [int]$TimeoutSeconds = 120,
    [switch]$Rebuild
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $base

function Write-Banner([string]$text) {
    Write-Host ''
    Write-Host '============================================================'
    Write-Host (' TP02 PG - ' + $text)
    Write-Host '============================================================'
}

function Get-Hex([byte[]]$bytes) {
    if ($null -eq $bytes -or $bytes.Length -eq 0) { return '' }
    return (($bytes | ForEach-Object { $_.ToString('X2') }) -join ' ')
}

function Get-FrameInfo([string]$path, [string]$label) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 3) { throw "Quadro curto demais em $path" }

    $sum = 0
    foreach ($b in $bytes) { $sum = ($sum + [int]$b) -band 0xFF }

    $payloadLength = [int]$bytes[1]
    $expectedTotal = $payloadLength + 3

    return New-Object PSObject -Property @{
        Label = $label
        Path = $path
        Command = [int]$bytes[0]
        PayloadLength = $payloadLength
        TotalLength = $bytes.Length
        ExpectedTotalLength = $expectedTotal
        ChecksumOk = ($sum -eq 0xFF)
        Hex = Get-Hex $bytes
        Bytes = $bytes
    }
}

function Get-UnknownSnapshot([string]$captureDir) {
    $seen = @{}
    if (Test-Path $captureDir) {
        Get-ChildItem -LiteralPath $captureDir -Filter '*-unknown-*.bin' -ErrorAction SilentlyContinue | ForEach-Object {
            $seen[$_.FullName.ToLowerInvariant()] = $true
        }
    }
    return $seen
}

function Wait-NewUnknown([string]$captureDir, [hashtable]$before, [int]$timeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $captureDir) {
            $candidate = Get-ChildItem -LiteralPath $captureDir -Filter '*-unknown-*.bin' -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Where-Object { -not $before.ContainsKey($_.FullName.ToLowerInvariant()) } |
                Select-Object -First 1
            if ($null -ne $candidate) { return $candidate.FullName }
        }
        Start-Sleep -Milliseconds 200
    }
    return $null
}

function Capture-Action([string]$label, [string]$portName, [string]$emulatorExe, [string]$captureDir, [int]$timeoutSeconds) {
    Write-Banner ("CAPTURA " + $label)
    Write-Host ('PC12 deve estar configurado na outra ponta do par virtual. Emulador: ' + $portName)
    Write-Host ('Agora, no PC12 original, execute SOMENTE o comando ' + $label + '.')
    Write-Host 'Nao execute READ, WRITE ou outro comando durante esta fase.'
    Write-Host 'O emulador NAO respondera a opcode desconhecido; ele apenas capturara o quadro.'
    Write-Host ''

    $before = Get-UnknownSnapshot $captureDir
    $args = @($portName, '--no-auto-ack', '--pg33-ack')
    $process = Start-Process -FilePath $emulatorExe -ArgumentList $args -PassThru -NoNewWindow

    try {
        $path = Wait-NewUnknown $captureDir $before $timeoutSeconds
        if ($null -eq $path) {
            throw ("Nenhum quadro desconhecido foi capturado em {0} s durante {1}." -f $timeoutSeconds,$label)
        }

        Start-Sleep -Milliseconds 350
        try {
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        } catch { }

        $info = Get-FrameInfo $path $label
        Write-Host ''
        Write-Host ('CAPTURADO ' + $label + ':')
        Write-Host ('  CMD          : 0x{0:X2}' -f $info.Command)
        Write-Host ('  LEN          : {0}' -f $info.PayloadLength)
        Write-Host ('  Total        : {0} byte(s)' -f $info.TotalLength)
        Write-Host ('  Checksum FF  : {0}' -f $(if ($info.ChecksumOk) { 'OK' } else { 'ERRO' }))
        Write-Host ('  HEX          : ' + $info.Hex)
        return $info
    }
    finally {
        try {
            if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        } catch { }
        Start-Sleep -Milliseconds 300
    }
}

if ($TimeoutSeconds -lt 10 -or $TimeoutSeconds -gt 600) {
    throw 'TimeoutSeconds deve estar entre 10 e 600.'
}

$build = Join-Path $base 'BuildTp02Emulator.bat'
$emulator = Join-Path $base 'OpenLadderTP02Emulator.exe'
$captureDir = Join-Path $base 'tp02-emulator-captures'

if (-not (Test-Path $build)) { throw "BuildTp02Emulator.bat nao encontrado em $base" }

if ($Rebuild -or -not (Test-Path $emulator)) {
    Write-Host 'Compilando emulador TP02...'
    & $build
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar OpenLadderTP02Emulator.exe.' }
}

if (-not (Test-Path $emulator)) { throw 'OpenLadderTP02Emulator.exe nao foi encontrado apos o build.' }

if ([string]::IsNullOrWhiteSpace($Port)) {
    $Port = (Read-Host 'Informe a porta COM VIRTUAL usada pelo emulador, por exemplo COM11').Trim()
}
if ([string]::IsNullOrWhiteSpace($Port)) { throw 'Porta COM nao informada.' }

Directory::CreateDirectory($captureDir) | Out-Null

$results = @()
if ($Action -eq 'STOP' -or $Action -eq 'BOTH') {
    $results += Capture-Action 'STOP' $Port $emulator $captureDir $TimeoutSeconds
}
if ($Action -eq 'BOTH') {
    Write-Host ''
    Write-Host 'Prepare o PC12 para uma nova conexao com a mesma COM virtual.'
    Write-Host 'Pressione ENTER quando estiver pronto para a captura de RUN.'
    [void](Read-Host)
}
if ($Action -eq 'RUN' -or $Action -eq 'BOTH') {
    $results += Capture-Action 'RUN' $Port $emulator $captureDir $TimeoutSeconds
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportPath = Join-Path $captureDir ("TP02-PG-RUNSTOP-DISCOVERY-" + $stamp + '.txt')
$report = New-Object Text.StringBuilder
[void]$report.AppendLine('TP02 PG - descoberta controlada de RUN/STOP')
[void]$report.AppendLine(('Data: ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')))
[void]$report.AppendLine(('Porta do emulador: ' + $Port))
[void]$report.AppendLine('Metodo: PC12 original -> COM virtual -> OpenLadder TP02 Emulator')
[void]$report.AppendLine('Seguranca: nenhum quadro desta rotina foi enviado ao PLC fisico.')
[void]$report.AppendLine('Resposta para opcode desconhecido: DESABILITADA (--no-auto-ack).')
[void]$report.AppendLine('')

foreach ($r in $results) {
    [void]$report.AppendLine(('[' + $r.Label + ']'))
    [void]$report.AppendLine(('CMD=0x{0:X2}' -f $r.Command))
    [void]$report.AppendLine(('LEN=' + $r.PayloadLength))
    [void]$report.AppendLine(('TOTAL=' + $r.TotalLength))
    [void]$report.AppendLine(('EXPECTED_TOTAL=' + $r.ExpectedTotalLength))
    [void]$report.AppendLine(('CHECKSUM_FF=' + $(if ($r.ChecksumOk) { 'OK' } else { 'ERRO' })))
    [void]$report.AppendLine(('HEX=' + $r.Hex))
    [void]$report.AppendLine(('FILE=' + $r.Path))
    [void]$report.AppendLine('')
}

if ($results.Count -eq 2) {
    $stop = $results | Where-Object { $_.Label -eq 'STOP' } | Select-Object -First 1
    $run = $results | Where-Object { $_.Label -eq 'RUN' } | Select-Object -First 1
    if ($null -ne $stop -and $null -ne $run) {
        [void]$report.AppendLine('[COMPARACAO]')
        [void]$report.AppendLine(('CMD_STOP=0x{0:X2}' -f $stop.Command))
        [void]$report.AppendLine(('CMD_RUN=0x{0:X2}' -f $run.Command))
        [void]$report.AppendLine(('OPCODE_IGUAL=' + $(if ($stop.Command -eq $run.Command) { 'SIM' } else { 'NAO' })))
        [void]$report.AppendLine(('FRAME_IGUAL=' + $(if ($stop.Hex -eq $run.Hex) { 'SIM' } else { 'NAO' })))
        if ($stop.Command -eq $run.Command -and $stop.Hex -ne $run.Hex) {
            [void]$report.AppendLine('OBS=RUN e STOP usam o mesmo opcode; a diferenca esta no payload/quadro.')
        }
        elseif ($stop.Command -ne $run.Command) {
            [void]$report.AppendLine('OBS=RUN e STOP aparentam usar opcodes distintos.')
        }
        else {
            [void]$report.AppendLine('OBS=Frames identicos; a captura precisa ser repetida antes de qualquer implementacao.')
        }
        [void]$report.AppendLine('')
    }
}

[void]$report.AppendLine('[PROXIMO PASSO]')
[void]$report.AppendLine('Nao adicionar regra de resposta nem implementar no PLC fisico apenas com esta captura.')
[void]$report.AppendLine('Primeiro confirmar o quadro e a resposta esperada em nova captura/analise do PC12; depois validar uma vez no TP02 fisico.')

[IO.File]::WriteAllText($reportPath, $report.ToString(), [Text.Encoding]::UTF8)

Write-Banner 'CAPTURA CONCLUIDA'
Write-Host ('Relatorio: ' + $reportPath)
Write-Host ''
Write-Host $report.ToString()
