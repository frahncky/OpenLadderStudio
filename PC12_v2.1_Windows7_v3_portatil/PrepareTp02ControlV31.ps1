$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'TP02ControlV31.cs'
$outputPath = Join-Path (Get-Location) 'TP02ControlV31.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$needle = 'probeThread = new Thread(delegate { AutoDetectWorker(portName, preferredStation, current); });'
$replacement = 'probeThread = new Thread(new ThreadStart(delegate { AutoDetectWorker(portName, preferredStation, current); }));'
if (-not $text.Contains($needle)) { throw 'Construtor Thread da autodeteccao TP02 nao encontrado.' }
$text = $text.Replace($needle, $replacement)

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
