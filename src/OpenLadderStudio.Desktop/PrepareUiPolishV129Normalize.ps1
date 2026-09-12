$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V129 normalize: UniversalStudioShell.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)
$literal = [char]96 + 'r' + [char]96 + 'n'
$shell = $shell.Replace($literal, [Environment]::NewLine)
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'UI V129 normalize: quebras de linha normalizadas no shell gerado.'
