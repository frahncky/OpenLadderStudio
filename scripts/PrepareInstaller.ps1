$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionPath = Join-Path $repoRoot 'src\OpenLadderStudio.Desktop\version.txt'
$templatePath = Join-Path $repoRoot 'installer\OpenLadderStudio.iss'
$outputPath = Join-Path $repoRoot 'installer\OpenLadderStudio.build.iss'
$prereqDir = Join-Path $repoRoot 'installer\prerequisites'
$dotNetPath = Join-Path $prereqDir 'dotNetFx40_Full_x86_x64.exe'
$dotNetUrl = 'https://download.microsoft.com/download/9/5/a/95a9616b-7a37-4af6-bc36-d6ea96c8daae/dotNetFx40_Full_x86_x64.exe'

if (-not (Test-Path $versionPath)) { throw 'version.txt não encontrado.' }
if (-not (Test-Path $templatePath)) { throw 'Template do instalador não encontrado.' }

$version = [System.IO.File]::ReadAllText($versionPath).Trim()
if ($version -notmatch '^\d+\.\d+(\.\d+)?$') { throw "Versão inválida: $version" }

# O Studio é compilado para CLR 4. O Windows 7 original não traz .NET 4,
# portanto o redistribuível oficial é incorporado ao instalador e só é
# executado no computador de destino quando necessário.
New-Item -ItemType Directory -Path $prereqDir -Force | Out-Null
$needDownload = (-not (Test-Path $dotNetPath)) -or ((Get-Item $dotNetPath).Length -lt 50000000)
if ($needDownload) {
    Write-Host 'Baixando pré-requisito oficial Microsoft .NET Framework 4...'
    Invoke-WebRequest -UseBasicParsing -Uri $dotNetUrl -OutFile $dotNetPath
}

$dotNetFile = Get-Item $dotNetPath
if ($dotNetFile.Length -lt 50000000) {
    throw "Redistribuível .NET Framework 4 incompleto: $($dotNetFile.Length) bytes."
}

$sig = Get-AuthenticodeSignature -FilePath $dotNetPath
if (($sig.Status -ne 'Valid') -or ($null -eq $sig.SignerCertificate) -or ($sig.SignerCertificate.Subject -notmatch 'Microsoft')) {
    throw "Assinatura Authenticode inválida no redistribuível .NET Framework 4: $($sig.Status)."
}
Write-Host "Pré-requisito .NET 4 validado: $([Math]::Round($dotNetFile.Length / 1MB, 1)) MB"

$template = [System.IO.File]::ReadAllText($templatePath)
$token = '@OPENLADDER_VERSION@'
if (-not $template.Contains($token)) { throw 'Token de versão do instalador não encontrado.' }

$content = $template.Replace($token, $version)
[System.IO.File]::WriteAllText($outputPath, $content, [System.Text.Encoding]::UTF8)
Write-Host "Instalador preparado para OpenLadder Studio v$version"
