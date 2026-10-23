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
$emulatorExe = Join-Path $scriptRoot 'OpenLadderTP02Emulator.exe'
$buildScript = Join-Path $scriptRoot 'BuildTp02Emulator.bat'
$captureDirectory = Join-Path $scriptRoot 'tp02-emulator-captures'

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
  public static class WindowApi {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
  }
}
'@
}

Assert-VirtualPair
if (-not (Test-Path -LiteralPath $pc12Exe)) { throw "PC12 nao encontrado: $pc12Exe" }
if (-not (Test-Path -LiteralPath $buildScript)) { throw "Build do emulador nao encontrado: $buildScript" }

Get-Process -Name 'OpenLadderTP02Emulator' -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Milliseconds 500

& $buildScript
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o emulador TP02.' }

$emulator = Start-Process -FilePath $emulatorExe -ArgumentList $emulatorPort, '--scenario=v156', '--seed=semantic323', '--pg33-ack', '--no-auto-ack', '--pg38=total16' -WorkingDirectory $scriptRoot -WindowStyle Hidden -PassThru
Start-Sleep -Milliseconds 750

$pc12 = Get-Process -Name pc12 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -ne $pc12) {
    if (-not $DiscardUnsavedPc12Changes) {
        throw 'PC12 ja esta aberto. Para o modo sem intervencao, use -DiscardUnsavedPc12Changes; isso descarta alteracoes nao salvas.'
    }
    Stop-Process -Id $pc12.Id -Force
    Start-Sleep -Milliseconds 750
}

$pc12 = Start-Process -FilePath $pc12Exe -WorkingDirectory (Split-Path -Parent $pc12Exe) -PassThru
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

# PLC > Com Port (0x133) abre o seletor do PC12. O controle inicial do
# dialogo aceita a porta digitada; COM3 e fixo para este laboratorio fechado.
[void][Pc12VirtualRead.WindowApi]::SendMessage($pc12.MainWindowHandle, 0x0111, [IntPtr]0x133, [IntPtr]::Zero)
Start-Sleep -Milliseconds 500
$shell = New-Object -ComObject WScript.Shell
if (-not $shell.AppActivate($pc12.Id)) { throw 'Nao foi possivel ativar o PC12 para configurar COM3.' }
$shell.SendKeys('COM3{ENTER}')
Start-Sleep -Milliseconds 750

# A primeira chamada abre/inicializa a COM no PC12. Depois da sondagem F0
# qualificada, a segunda chamada abaixo inicia a leitura efetiva.
[void][Pc12VirtualRead.WindowApi]::SendMessage($pc12.MainWindowHandle, 0x0111, [IntPtr]0x12E, [IntPtr]::Zero)

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
[void][Pc12VirtualRead.WindowApi]::SendMessage($pc12.MainWindowHandle, 0x0111, [IntPtr]0x12E, [IntPtr]::Zero)

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
            exit 0
        }
    }
} while ((Get-Date) -lt $deadline)

throw "Timeout: PLC/Read nao gerou quadro PG34 em $TimeoutSeconds segundos. O emulador continua somente no par virtual."
