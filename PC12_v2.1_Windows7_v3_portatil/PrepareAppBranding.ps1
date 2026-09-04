$ErrorActionPreference = 'Stop'

function Add-BrandingInstall([string]$path) {
    if (-not (Test-Path $path)) { throw "Arquivo não encontrado: $path" }
    $text = [System.IO.File]::ReadAllText($path)
    $needle = '            Application.SetCompatibleTextRenderingDefault(false);'
    $replacement = "            Application.SetCompatibleTextRenderingDefault(false);`r`n            AppBranding.Install();"
    if (-not $text.Contains($needle)) { throw "Ponto de inicialização não encontrado em $path" }
    if (-not $text.Contains('AppBranding.Install();')) {
        $text = $text.Replace($needle, $replacement)
    }
    [System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
}

$root = Get-Location

$copies = @(
    @{ Source = 'PC12Updater.cs'; Target = 'PC12Updater.build.cs' },
    @{ Source = 'PLCDeviceManagerV16.cs'; Target = 'PLCDeviceManagerV16.build.cs' }
)

foreach ($item in $copies) {
    $source = Join-Path $root $item.Source
    $target = Join-Path $root $item.Target
    [System.IO.File]::WriteAllText($target, [System.IO.File]::ReadAllText($source), [System.Text.Encoding]::UTF8)
    Add-BrandingInstall $target
}

$generated = @(
    'LadderEditor.build.cs',
    'PLCMemoryMapManagerV15.build.cs',
    'ModbusMonitorV18.build.cs',
    'UniversalStudioShell.build.cs'
)

foreach ($name in $generated) {
    Add-BrandingInstall (Join-Path $root $name)
}
