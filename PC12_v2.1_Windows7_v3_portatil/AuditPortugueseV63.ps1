$ErrorActionPreference = 'Stop'
$root = Get-Location
$utf8 = New-Object System.Text.UTF8Encoding($false)

# A normalizacao PT-BR atua sobre literais C# e, historicamente, acabou atingindo
# tokens que nao sao texto de interface. Esta etapa roda imediatamente depois da
# normalizacao e restaura por PADRAO os identificadores de API, URLs e caminhos
# tecnicos antes da compilacao.
function Repair-TechnicalTokens([string]$text) {
    $fixed = $text

    # Campo JSON da API do GitHub. Aceita qualquer variante que tenha sido criada
    # pela traducao entre browser_ e _url.
    $fixed = [regex]::Replace(
        $fixed,
        'browser_[^"''\s\\]*?_url',
        'browser_download_url',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    # Endpoint de assets de release do GitHub. O segmento correto e sempre download.
    $fixed = [regex]::Replace(
        $fixed,
        '(?i)(https://github\.com/frahncky/OpenLadderStudio/releases/)[^/"''\\]+/',
        '$1download/')

    # Caminho tecnico usado no fallback de version.txt. Nomes de caminho nao recebem acento.
    $fixed = [regex]::Replace(
        $fixed,
        '(?i)PC12_v2\.1_Windows7_v3_port[^/"''\\]*',
        'PC12_v2.1_Windows7_v3_portatil')

    return $fixed
}

$repairCount = 0
$sourceFiles = Get-ChildItem -Path $root -Filter '*.cs' -File | Sort-Object Name
foreach ($file in $sourceFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $fixed = Repair-TechnicalTokens $text
    if ($fixed -cne $text) {
        [System.IO.File]::WriteAllText($file.FullName, $fixed, $utf8)
        $repairCount++
        Write-Host ('Tokens tecnicos restaurados: {0}' -f $file.Name) -ForegroundColor Cyan
    }
}

# O updater gerado e o contrato principal desta auditoria. Nao permitimos compilar
# se os nomes esperados pela API ou os endpoints estiverem alterados.
$updaterBuild = Join-Path $root 'PC12Updater.build.cs'
if (-not (Test-Path $updaterBuild)) {
    throw 'PC12Updater.build.cs nao encontrado para auditoria tecnica.'
}

$updaterText = [System.IO.File]::ReadAllText($updaterBuild)
$requiredTechnicalTokens = @(
    'browser_download_url',
    '/releases/download/',
    'PC12_v2.1_Windows7_v3_portatil/version.txt'
)
foreach ($token in $requiredTechnicalTokens) {
    if (-not $updaterText.Contains($token)) {
        $browser = [regex]::Match($updaterText, 'browser_.{0,80}?_url', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        $release = [regex]::Match($updaterText, 'releases/.{0,80}?/', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        Write-Host ('Diagnostico browser: {0}' -f $browser.Value) -ForegroundColor Yellow
        Write-Host ('Diagnostico release: {0}' -f $release.Value) -ForegroundColor Yellow
        throw "Updater sem token tecnico obrigatorio apos normalizacao: $token"
    }
}

# O caminho da pasta portatil aparece com e sem barra final; so e corrupcao
# quando 'port' nao e seguido de 'atil' terminando o token.
$forbiddenPatterns = @(
    'browser_(?!download_url)[^"''\s\\]*?_url',
    '(?i)github\.com/frahncky/OpenLadderStudio/releases/(?!download/)[^/"''\\]+/',
    '(?i)PC12_v2\.1_Windows7_v3_port(?!atil(?![A-Za-z]))[^/"''\\]*'
)
foreach ($rx in $forbiddenPatterns) {
    if ([regex]::IsMatch($updaterText, $rx)) {
        throw "Updater contem token tecnico corrompido: $rx"
    }
}

# A corrupcao nao e exclusiva do updater: qualquer URL ou identificador de API
# traduzido quebra o produto em silencio. Desde que o normalizador passou a
# pular literais tecnicos isso nao deve mais acontecer, e esta varredura existe
# para que uma regressao apareca no build em vez de chegar ao usuario.
foreach ($file in $sourceFiles) {
    $sourceText = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($rx in $forbiddenPatterns) {
        if ([regex]::IsMatch($sourceText, $rx)) {
            throw ('{0} contem token tecnico corrompido: {1}' -f $file.Name, $rx)
        }
    }
}

# Auditoria textual enxuta. Mantemos a deteccao de mojibake e dos problemas de
# linguagem mais recorrentes, mas ignoramos literais tecnicos e URLs.
$stringPattern = '@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"'
function Is-TechnicalLiteral([string]$value) {
    if ($value -match 'https?://') { return $true }
    if ($value -match '(?i)browser_download_url|tag_name') { return $true }
    if ($value -match '(?i)OpenLadder-Studio-Setup\.exe(?:\.sha256)?') { return $true }
    if ($value -match '(?i)PC12_v2\.1_Windows7_v3_portatil') { return $true }
    return $false
}
function Has-Mojibake([string]$value) {
    if ($value.IndexOf([char]0xFFFD) -ge 0) { return $true }
    if ([regex]::IsMatch($value, '\u00C3(?=[\u0080-\u00BF\u0192])')) { return $true }
    if ([regex]::IsMatch($value, '\u00C2(?=[\u0080-\u00BF\u2000-\u206F])')) { return $true }
    if ([regex]::IsMatch($value, '\u00E2(?=[\u0080-\u00BF\u2000-\u206F])')) { return $true }
    return $false
}

$findings = New-Object System.Collections.Generic.List[string]
foreach ($file in $sourceFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($m in [regex]::Matches($text, $stringPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $value = $m.Value
        if (Is-TechnicalLiteral $value) { continue }
        if (Has-Mojibake $value) {
            $line = 1 + ([regex]::Matches($text.Substring(0, $m.Index), "`n")).Count
            $compact = ($value -replace "`r|`n", ' ')
            if ($compact.Length -gt 220) { $compact = $compact.Substring(0, 217) + '...' }
            $findings.Add(('{0}:{1}: {2}' -f $file.Name, $line, $compact))
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host ''
    Write-Host '=== AUDITORIA PT-BR: codificacao suspeita ===' -ForegroundColor Yellow
    $findings | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
    throw 'A auditoria de portugues encontrou texto com codificacao corrompida.'
}

Write-Host ('Auditoria PT-BR concluida. Tokens tecnicos restaurados em {0} arquivo(s).' -f $repairCount)