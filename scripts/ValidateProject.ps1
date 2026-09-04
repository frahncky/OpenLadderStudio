$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$portable = Join-Path $repoRoot 'PC12_v2.1_Windows7_v3_portatil'
$versionPath = Join-Path $portable 'version.txt'
$installer = Join-Path $repoRoot 'installer\PC12Studio.iss'
$universalPrep = Join-Path $portable 'PrepareUniversalStudioV20.ps1'
$uiPrep = Join-Path $portable 'PrepareStudioUiV20.ps1'
$iconPrep = Join-Path $portable 'GenerateOpenLadderIcon.ps1'

$required = @($versionPath, $installer, $universalPrep, $uiPrep, $iconPrep)
foreach ($path in $required) {
    if (-not (Test-Path $path)) { throw "Arquivo obrigatório ausente: $path" }
}

$version = [System.IO.File]::ReadAllText($versionPath).Trim()
if ($version -notmatch '^\d+\.\d+(\.\d+)?$') { throw "version.txt inválido: $version" }

$installerText = [System.IO.File]::ReadAllText($installer)
if (-not $installerText.Contains('@OPENLADDER_VERSION@')) {
    throw 'O instalador deve usar o token @OPENLADDER_VERSION@ em vez de uma versão fixa.'
}

$universalText = [System.IO.File]::ReadAllText($universalPrep)
if (-not $universalText.Contains("version.txt")) {
    throw 'A preparação do shell deve ler a versão de version.txt.'
}
if ($universalText -match "'v0\.2[0-9]'") {
    throw 'Foi encontrada uma versão fixa no script do shell universal.'
}

$uiText = [System.IO.File]::ReadAllText($uiPrep)
if (-not $uiText.Contains('StudioIconPalette')) {
    throw 'A interface deve usar a paleta semântica central de ícones.'
}

$iconText = [System.IO.File]::ReadAllText($iconPrep)
foreach ($size in @('16', '24', '32', '48', '64', '128', '256')) {
    if (-not $iconText.Contains($size)) { throw "Tamanho $size ausente do pipeline de ícone." }
}

$buildScript = Join-Path $portable 'BUILD_INTERFACE_MODERNA.bat'
if (-not (Test-Path $buildScript)) { throw "Arquivo obrigatório ausente: $buildScript" }

$buildText = [System.IO.File]::ReadAllText($buildScript)
$produced = @()
foreach ($hit in [Regex]::Matches($buildText, '/out:"([A-Za-z0-9_]+\.exe)"')) {
    $produced += $hit.Groups[1].Value
}
if ($produced.Count -eq 0) { throw 'Nenhum executável encontrado em BUILD_INTERFACE_MODERNA.bat.' }

$manifest = Join-Path $portable 'OpenLadderStudio.manifest'
if (-not (Test-Path $manifest)) { throw "Arquivo obrigatório ausente: $manifest" }

$manifestText = [System.IO.File]::ReadAllText($manifest)
if ($manifestText -notmatch '<dpiAware[^>]*>\s*true\s*</dpiAware>') {
    throw 'O manifesto deve declarar dpiAware = true, senão a interface fica borrada em telas com escala.'
}

$compileLines = @([Regex]::Matches($buildText, '(?m)^.*?/out:"[A-Za-z0-9_]+\.exe".*$'))
if ($compileLines.Count -eq 0) { throw 'Nenhuma invocação do compilador encontrada em BUILD_INTERFACE_MODERNA.bat.' }
foreach ($line in $compileLines) {
    if ($line.Value -notmatch '/win32manifest:') {
        throw 'Toda invocação do compilador deve usar /win32manifest para embutir o reconhecimento de DPI.'
    }
    if ($line.Value -notmatch 'StudioDiagnostics\.cs') {
        throw 'Toda invocação do compilador deve incluir StudioDiagnostics.cs para o tratamento global de erros.'
    }
}

foreach ($launcher in Get-ChildItem -Path $portable -Filter 'INICIAR_*.bat') {
    $launcherText = [System.IO.File]::ReadAllText($launcher.FullName).Replace('%~dp0', '')
    foreach ($hit in [Regex]::Matches($launcherText, '([A-Za-z0-9_]+\.exe)')) {
        $exe = $hit.Groups[1].Value
        if ($produced -contains $exe) { continue }
        if (Test-Path (Join-Path $portable $exe)) { continue }
        throw "$($launcher.Name) chama $exe, que não é gerado pelo build nem existe no repositório."
    }
}

$generated = @(
    (Join-Path $repoRoot 'installer\PC12Studio.build.iss'),
    (Join-Path $portable 'StudioUi.build.cs'),
    (Join-Path $portable 'UniversalStudioShell.build.cs')
)
foreach ($path in $generated) {
    if (Test-Path $path) { Write-Host "Arquivo gerado presente no workspace: $path" }
}

Write-Host "OpenLadder Studio v${version}: metadados e estrutura validados."
