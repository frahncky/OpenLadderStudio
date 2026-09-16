# Windows 7 / PowerShell 2.0 compatible. Metadata only: does not open COM or contact PLC.
param(
    [string]$Port = 'COM1',
    [string]$Out = ''
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$portMatch = [regex]::Match($Port, '^COM([1-9][0-9]{0,2})$')
if (-not $portMatch.Success) { throw 'Porta invalida: use COM1, COM2, ... COM256.' }
if ([int]$portMatch.Groups[1].Value -gt 256) { throw 'Porta fora do intervalo COM1..COM256.' }
if ([string]::IsNullOrEmpty($Out)) {
    $Out = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) ('TP02-Porta-' + $Port + '-diagnostico.txt')
}

function SafeText($value) {
    if ($null -eq $value) { return 'NAO DISPONIVEL' }
    $s = [string]$value
    if ([string]::IsNullOrEmpty($s.Trim())) { return 'NAO DISPONIVEL' }
    return ($s -replace '[\r\n\t]', ' ').Trim()
}

# Consultas WMI de metadados do Windows. Nao cria SerialPort e nao altera configuracoes.
$devices = @(Get-WmiObject -Class Win32_PnPEntity -ErrorAction Stop |
    Where-Object { $_.Name -match ('\(' + [regex]::Escape($Port) + '\)\s*$') })
$serialPorts = @(Get-WmiObject -Class Win32_SerialPort -ErrorAction SilentlyContinue |
    Where-Object { $_.DeviceID -eq $Port })
$drivers = @(Get-WmiObject -Class Win32_PnPSignedDriver -ErrorAction SilentlyContinue)
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('TP02 - DIAGNOSTICO SOMENTE METADADOS (SEM ACESSO A COM/PLC)')
$lines.Add('Porta=' + $Port)
$lines.Add('DispositivosEncontrados=' + $devices.Count)
$lines.Add('PortasSeriaisEncontradas=' + $serialPorts.Count)
foreach ($device in $devices) {
    # O PNPDeviceID e usado apenas em memoria para associar o driver; nao e exportado.
    $pnpId = [string]$device.PNPDeviceID
    $transport = 'NAO IDENTIFICADO'
    if ($pnpId -match '^USB\\') { $transport = 'USB (indicio por enumeracao PnP)' }
    elseif ($pnpId -match '^PCI\\') { $transport = 'PCI/PCIe (indicio por enumeracao PnP)' }
    elseif ($pnpId -match '^ACPI\\') { $transport = 'ACPI/porta integrada (indicio por enumeracao PnP)' }
    elseif ($pnpId -match '^FTDIBUS\\') { $transport = 'FTDI/USB (indicio por enumeracao PnP)' }
    $driver = $null
    foreach ($candidate in $drivers) {
        if ([string]::Equals([string]$candidate.DeviceID, $pnpId, [StringComparison]::OrdinalIgnoreCase)) {
            $driver = $candidate
            break
        }
    }
    $lines.Add('---')
    $lines.Add('Dispositivo=' + (SafeText $device.Name))
    $lines.Add('Fabricante=' + (SafeText $device.Manufacturer))
    $lines.Add('TransporteProvavel=' + $transport)
    $lines.Add('ServicoDriver=' + (SafeText $device.Service))
    $lines.Add('StatusPnP=' + (SafeText $device.Status))
    if ($null -ne $driver) {
        $lines.Add('FornecedorDriver=' + (SafeText $driver.DriverProviderName))
        $lines.Add('VersaoDriver=' + (SafeText $driver.DriverVersion))
        $lines.Add('DataDriver=' + (SafeText $driver.DriverDate))
        $lines.Add('Assinado=' + (SafeText $driver.IsSigned))
    }
    else {
        $lines.Add('Driver=NAO LOCALIZADO POR WMI')
    }
}
foreach ($serial in $serialPorts) {
    $lines.Add('SerialNome=' + (SafeText $serial.Name))
    $lines.Add('SerialStatus=' + (SafeText $serial.Status))
}
$lines.Add('Aviso=Isto nao confirma pinagem, integridade do cabo ou comportamento do PLC.')
$lines.Add('Privacidade=IDs PnP, numeros de serie e conteudo da memoria nao sao exportados.')
[IO.File]::WriteAllLines($Out, [string[]]$lines.ToArray(), (New-Object Text.UTF8Encoding($false)))
Write-Host ('Diagnostico de metadados salvo em: ' + $Out)
if ($devices.Count -eq 0 -and $serialPorts.Count -eq 0) {
    Write-Warning ('A porta ' + $Port + ' nao foi encontrada por WMI; nao prova que ela inexiste.')
}
