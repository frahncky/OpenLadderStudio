$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")
$old = '            AddToolButton(bar, "Conectar", StudioIcon.Plug, true, delegate { ShowCommunication(); });'
$new = '            AddToolButton(bar, "Conectar", StudioIcon.Plug, false, delegate { ShowCommunication(); });'
if (-not $shell.Contains($old)) { throw 'Ancora do botao Conectar nao encontrada.' }
$shell = $shell.Replace($old, $new)

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'UI V60 aplicada: Conectar sem destaque permanente; destaque fica reservado a estados reais.'
