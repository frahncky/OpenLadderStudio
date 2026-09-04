$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionPath = Join-Path $repoRoot 'PC12_v2.1_Windows7_v3_portatil\version.txt'
$templatePath = Join-Path $repoRoot 'installer\PC12Studio.iss'
$outputPath = Join-Path $repoRoot 'installer\PC12Studio.build.iss'

if (-not (Test-Path $versionPath)) { throw 'version.txt não encontrado.' }
if (-not (Test-Path $templatePath)) { throw 'Template do instalador não encontrado.' }

$version = [System.IO.File]::ReadAllText($versionPath).Trim()
if ($version -notmatch '^\d+\.\d+(\.\d+)?$') { throw "Versão inválida: $version" }

$template = [System.IO.File]::ReadAllText($templatePath)
$token = '@OPENLADDER_VERSION@'
if (-not $template.Contains($token)) { throw 'Token de versão do instalador não encontrado.' }

$content = $template.Replace($token, $version)
[System.IO.File]::WriteAllText($outputPath, $content, [System.Text.Encoding]::UTF8)
Write-Host "Instalador preparado para OpenLadder Studio v$version"
