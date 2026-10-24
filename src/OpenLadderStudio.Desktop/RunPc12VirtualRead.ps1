param(
    [int]$TimeoutSeconds = 45,
    [switch]$DiscardUnsavedPc12Changes
)

$ErrorActionPreference = 'Stop'

# Laboratorio fechado: PC12 em COM3, emulador em COM4. Nao ha parametro de porta
# para impedir uso acidental de adaptador/PLC fisico.
$pc12Port = 'COM3'
$emulatorPort = 'COM4'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$pc12Exe = Join-Path $workspaceRoot 'PC12_v2.1_Windows7_v3_portatil\PC12_v2.1_Windows7_v3_portatil\pc12.exe'
$pc12Project = Join-Path (Split-Path -Parent $pc12Exe) 'pc12.lad'
$buildScript = Join-Path $scriptRoot 'BuildTp02Emulator.bat'
$captureDirectory = Join-Path $scriptRoot 'tp02-emulator-captures'
$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$emulatorExe = Join-Path $captureDirectory ('OpenLadderTP02Emulator.VirtualRun-' + $runStamp + '.exe')
$emulatorProcessName = [System.IO.Path]::GetFileNameWithoutExtension($emulatorExe)
$runLog = Join-Path $captureDirectory ('PC12-VirtualRead-' + $runStamp + '.log')

function Write-RunLog([string]$message) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss.fff'), $message
    Write-Host $line
    Add-Content -LiteralPath $runLog -Value $line
}

function Assert-VirtualPair {
    $map = Get-ItemProperty 'HKLM:\HARDWARE\DEVICEMAP\SERIALCOMM'
    $items = @($map.PSObject.Properties | Where-Object { $_.Value -in $pc12Port, $emulatorPort })
    if ($items.Count -ne 2 -or ($items.Value -notcontains $pc12Port) -or ($items.Value -notcontains $emulatorPort) -or (($items.Name -join '|') -notmatch 'VSerial')) {
        throw "Recusado: $pc12Port e $emulatorPort devem ser o par VSerial virtual. Nenhuma porta fisica e aceita."
    }
}

function Add-NativeWindowApi {
    if ('Pc12VirtualRead.WindowApi' -as [type]) { return }
    Add-Type @'
using System;
using System.Runtime.InteropServices;
namespace Pc12VirtualRead {
  [StructLayout(LayoutKind.Sequential)]
  public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
  public static class WindowApi {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr FindWindow(string className, string windowName);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
  }
}
'@
}

New-Item -ItemType Directory -Force -Path $captureDirectory | Out-Null
Write-RunLog "Inicio. Laboratorio virtual fixo: $pc12Port <-> $emulatorPort."
$env:TP02_EMULATOR_OUT = $emulatorExe

try {
Assert-VirtualPair
if (-not (Test-Path -LiteralPath $pc12Exe)) { throw "PC12 nao encontrado: $pc12Exe" }
if (-not (Test-Path -LiteralPath $pc12Project)) { throw "Projeto local PC12 nao encontrado: $pc12Project" }
if (-not (Test-Path -LiteralPath $buildScript)) { throw "Build do emulador nao encontrado: $buildScript" }

Get-Process -Name @('OpenLadderTP02Emulator', $emulatorProcessName) -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Milliseconds 500

& $buildScript
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o emulador TP02.' }

$emulator = Start-Process -FilePath $emulatorExe -ArgumentList $emulatorPort, '--scenario=v156', '--seed=semantic323', '--pg33-ack', '--no-auto-ack', '--pg38=total16' -WorkingDirectory $scriptRoot -WindowStyle Hidden -PassThru
Write-RunLog "Emulador iniciado (PID $($emulator.Id))."
Start-Sleep -Milliseconds 750

$pc12 = Get-Process -Name pc12 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -ne $pc12) {
    if (-not $DiscardUnsavedPc12Changes) {
        throw 'PC12 ja esta aberto. Para o modo sem intervencao, use -DiscardUnsavedPc12Changes; isso descarta alteracoes nao salvas.'
    }
    Stop-Process -Id $pc12.Id -Force
    Start-Sleep -Milliseconds 750
}

$pc12 = Start-Process -FilePath $pc12Exe -ArgumentList $pc12Project -WorkingDirectory (Split-Path -Parent $pc12Exe) -PassThru
Write-RunLog "PC12 iniciado (PID $($pc12.Id))."
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    Start-Sleep -Milliseconds 500
    $pc12 = Get-Process -Id $pc12.Id -ErrorAction Stop
    if ($pc12.MainWindowHandle -ne [IntPtr]::Zero) { break }
}

Add-NativeWindowApi
if ($pc12.MainWindowHandle -eq [IntPtr]::Zero) {
    $pc12 = Get-Process -Id $pc12.Id
}
if ($pc12.MainWindowHandle -eq [IntPtr]::Zero) { throw 'A janela principal do PC12 nao ficou disponivel.' }

# PLC > Com Port (0x133) abre o seletor. A ordem de foco observada no PC12
# e COM1..COM4, TP02/Modem, Link/Unlink. A sequencia abaixo seleciona COM3,
# TP02 e Link; Enter isolado acionaria o botao Unlink padrao.
if (-not [Pc12VirtualRead.WindowApi]::PostMessage($pc12.MainWindowHandle, 0x0111, [IntPtr]0x133, [IntPtr]::Zero)) {
    throw 'Nao foi possivel abrir o seletor de comunicacao do PC12.'
}
Start-Sleep -Milliseconds 500
$dialog = [IntPtr]::Zero
for ($attempt = 0; $attempt -lt 10 -and $dialog -eq [IntPtr]::Zero; $attempt++) {
    $dialog = [Pc12VirtualRead.WindowApi]::FindWindow($null, 'Communication Port')
    if ($dialog -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 200 }
}
if ($dialog -eq [IntPtr]::Zero) { throw 'O seletor Communication Port do PC12 nao apareceu.' }
$rect = New-Object Pc12VirtualRead.Rect
if (-not [Pc12VirtualRead.WindowApi]::GetWindowRect($dialog, [ref]$rect)) { throw 'Nao foi possivel localizar os controles do seletor de porta.' }
function Invoke-Pc12DialogClick([int]$x, [int]$y) {
    [void][Pc12VirtualRead.WindowApi]::SetCursorPos($x, $y)
    [Pc12VirtualRead.WindowApi]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Pc12VirtualRead.WindowApi]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
}
Invoke-Pc12DialogClick ($rect.Left + 32) ($rect.Top + 109) # COM3
Invoke-Pc12DialogClick ($rect.Left + 32) ($rect.Top + 181) # TP02
Invoke-Pc12DialogClick ($rect.Left + 63) ($rect.Top + 220) # Link
Write-RunLog 'COM3 configurada com protocolo TP02 e Link ativado.'
Start-Sleep -Milliseconds 750

# A primeira chamada inicia a comunicacao e a sondagem F0.
if (-not [Pc12VirtualRead.WindowApi]::PostMessage($pc12.MainWindowHandle, 0x0111, [IntPtr]0x12E, [IntPtr]::Zero)) {
    throw 'Nao foi possivel solicitar a primeira leitura PLC no PC12.'
}
Write-RunLog 'Solicitada primeira leitura PLC (inicializacao F0).'

$handshakeDeadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Milliseconds 500
    $latest = Get-ChildItem -LiteralPath $captureDirectory -Filter '*.txt' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest -and $latest.LastWriteTime -ge $emulator.StartTime) {
        $f0Ready = Select-String -LiteralPath $latest.FullName -Pattern 'EMU -> PC12 F0' -Quiet -ErrorAction SilentlyContinue
        if ($f0Ready) { break }
    }
} while ((Get-Date) -lt $handshakeDeadline)

if (-not $f0Ready) { throw "Timeout: PC12 nao qualificou F0 em $TimeoutSeconds segundos." }

# WM_COMMAND / 0x12E foi enumerado da instalacao PC12 2.1: PLC > Read.
if (-not [Pc12VirtualRead.WindowApi]::PostMessage($pc12.MainWindowHandle, 0x0111, [IntPtr]0x12E, [IntPtr]::Zero)) {
    throw 'Nao foi possivel solicitar a leitura PLC no PC12.'
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Milliseconds 500
    $latest = Get-ChildItem -LiteralPath $captureDirectory -Filter '*.txt' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest -and $latest.LastWriteTime -ge $emulator.StartTime) {
        $requests = @(Select-String -LiteralPath $latest.FullName -Pattern 'CMD=0x34' -ErrorAction SilentlyContinue)
        if ($requests.Count -gt 0) {
            Write-Host "PASS: PLC/Read automatico iniciou $($requests.Count) pagina(s) PG34 no par virtual $pc12Port <-> $emulatorPort."
            Write-Host "Captura: $($latest.FullName)"
            Write-RunLog 'PASS: PG34 recebido no emulador virtual.'
            return
        }
    }
} while ((Get-Date) -lt $deadline)

throw "Timeout: PLC/Read nao gerou quadro PG34 em $TimeoutSeconds segundos. O emulador continua somente no par virtual."
}
catch {
    Write-RunLog ("FALHA: " + $_.Exception.Message)
    throw
}
finally {
    if ($null -ne $emulator -and -not $emulator.HasExited) {
        Stop-Process -Id $emulator.Id -Force -ErrorAction SilentlyContinue
        Write-RunLog "Emulador PID $($emulator.Id) encerrado."
    }
    Remove-Item Env:TP02_EMULATOR_OUT -ErrorAction SilentlyContinue
    Write-RunLog "Log da execucao: $runLog"
}
