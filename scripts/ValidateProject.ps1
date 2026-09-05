$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$portable = Join-Path $repoRoot 'PC12_v2.1_Windows7_v3_portatil'
$versionPath = Join-Path $portable 'version.txt'
$changeLogPath = Join-Path $repoRoot 'CHANGELOG.md'
$installer = Join-Path $repoRoot 'installer\PC12Studio.iss'
$universalPrep = Join-Path $portable 'PrepareUniversalStudioV20.ps1'
$uiPrep = Join-Path $portable 'PrepareStudioUiV20.ps1'
$iconPrep = Join-Path $portable 'GenerateOpenLadderIcon.ps1'
$scanEngine = Join-Path $portable 'LadderSimulation.cs'
$processModel = Join-Path $portable 'ProcessSimulation.cs'
$simulatorUi = Join-Path $portable 'LadderSimulator.cs'
$simulatorTest = Join-Path $portable 'SimulationSelfTest.cs'

$architectureValidation = Join-Path $PSScriptRoot 'ValidateArchitecture.ps1'
if (-not (Test-Path $architectureValidation)) { throw "Validador arquitetural ausente: $architectureValidation" }
& $architectureValidation

$required = @($versionPath, $changeLogPath, $installer, $universalPrep, $uiPrep, $iconPrep, $scanEngine, $processModel, $simulatorUi, $simulatorTest)
foreach ($path in $required) {
    if (-not (Test-Path $path)) { throw "Arquivo obrigatório ausente: $path" }
}

$version = [System.IO.File]::ReadAllText($versionPath).Trim()
if ($version -notmatch '^\d+\.\d+(\.\d+)?

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

# O nucleo da simulacao pertence ao dominio e nao pode depender de WinForms.
foreach ($core in @($scanEngine, $processModel)) {
    $coreText = [System.IO.File]::ReadAllText($core)
    if ($coreText.Contains('System.Windows.Forms')) {
        throw "O nucleo da simulacao nao pode depender de WinForms: $core"
    }
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

foreach ($file in Get-ChildItem -Path $portable -Filter '*.cs') {
    $code = [System.IO.File]::ReadAllText($file.FullName)
    if ($code -notmatch '(?m)class\s+\w+\s*:\s*Form\b') { continue }
    if ($code -notmatch 'AutoScaleMode') {
        throw "$($file.Name) declara formulário sem AutoScaleMode. Com o manifesto de DPI ativo, a janela deixa de ser escalada e aparece menor do que deveria."
    }
    if ($code -notmatch 'AutoScaleDimensions') {
        throw "$($file.Name) define AutoScaleMode sem AutoScaleDimensions. Sem a dimensão de referência o fator de escala do WinForms é 1 e o AutoScaleMode não tem efeito algum."
    }
}

foreach ($file in Get-ChildItem -Path $portable -Filter '*.cs') {
    $code = [System.IO.File]::ReadAllText($file.FullName)
    if ($code -notmatch 'new DataGridView\(\)') { continue }
    if ($code -notmatch 'ColumnHeadersHeightSizeMode') {
        throw "$($file.Name) tem DataGridView sem ColumnHeadersHeightSizeMode. A faixa de cabeçalho fica com altura fixa e a fonte, em pontos, invade a primeira linha em telas com escala."
    }
}

foreach ($file in Get-ChildItem -Path $portable -Filter '*.cs') {
    $code = [System.IO.File]::ReadAllText($file.FullName)
    if ($code -notmatch '(?m)class\s+\w+\s*:\s*Form\b') { continue }
    foreach ($hit in [Regex]::Matches($code, '(\w+)\.Dock\s*=\s*DockStyle\.(Top|Bottom|Left|Right)\s*;')) {
        $bar = $hit.Groups[1].Value
        if ($code -match ('(?m)^\s*' + [Regex]::Escape($bar) + '\.BringToFront\(\);')) {
            throw "$($file.Name) chama BringToFront em '$bar', que e uma barra ancorada. A ancoragem resolve do ultimo filho para o primeiro: so o painel Fill pode ir para a frente, senao as barras passam a ser desenhadas por cima do conteudo."
        }
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
) { throw "version.txt inválido: $version" }

$changeLogText = [System.IO.File]::ReadAllText($changeLogPath)
$escapedVersion = [Regex]::Escape($version)
$changeLogEntry = [Regex]::Match($changeLogText, "(?ms)^## \[$escapedVersion\][^\r\n]*\r?\n(.*?)(?=^## \[|\z)")
if (-not $changeLogEntry.Success -or [string]::IsNullOrWhiteSpace($changeLogEntry.Groups[1].Value)) {
    throw "O CHANGELOG não possui notas para a versão $version."
}

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

# O nucleo da simulacao pertence ao dominio e nao pode depender de WinForms.
foreach ($core in @($scanEngine, $processModel)) {
    $coreText = [System.IO.File]::ReadAllText($core)
    if ($coreText.Contains('System.Windows.Forms')) {
        throw "O nucleo da simulacao nao pode depender de WinForms: $core"
    }
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

foreach ($file in Get-ChildItem -Path $portable -Filter '*.cs') {
    $code = [System.IO.File]::ReadAllText($file.FullName)
    if ($code -notmatch '(?m)class\s+\w+\s*:\s*Form\b') { continue }
    if ($code -notmatch 'AutoScaleMode') {
        throw "$($file.Name) declara formulário sem AutoScaleMode. Com o manifesto de DPI ativo, a janela deixa de ser escalada e aparece menor do que deveria."
    }
    if ($code -notmatch 'AutoScaleDimensions') {
        throw "$($file.Name) define AutoScaleMode sem AutoScaleDimensions. Sem a dimensão de referência o fator de escala do WinForms é 1 e o AutoScaleMode não tem efeito algum."
    }
}

foreach ($file in Get-ChildItem -Path $portable -Filter '*.cs') {
    $code = [System.IO.File]::ReadAllText($file.FullName)
    if ($code -notmatch 'new DataGridView\(\)') { continue }
    if ($code -notmatch 'ColumnHeadersHeightSizeMode') {
        throw "$($file.Name) tem DataGridView sem ColumnHeadersHeightSizeMode. A faixa de cabeçalho fica com altura fixa e a fonte, em pontos, invade a primeira linha em telas com escala."
    }
}

foreach ($file in Get-ChildItem -Path $portable -Filter '*.cs') {
    $code = [System.IO.File]::ReadAllText($file.FullName)
    if ($code -notmatch '(?m)class\s+\w+\s*:\s*Form\b') { continue }
    foreach ($hit in [Regex]::Matches($code, '(\w+)\.Dock\s*=\s*DockStyle\.(Top|Bottom|Left|Right)\s*;')) {
        $bar = $hit.Groups[1].Value
        if ($code -match ('(?m)^\s*' + [Regex]::Escape($bar) + '\.BringToFront\(\);')) {
            throw "$($file.Name) chama BringToFront em '$bar', que e uma barra ancorada. A ancoragem resolve do ultimo filho para o primeiro: so o painel Fill pode ir para a frente, senao as barras passam a ser desenhadas por cima do conteudo."
        }
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
