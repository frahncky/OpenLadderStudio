$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$formPath = Join-Path (Get-Location) 'TP02PgProgramReadForm.cs'
$coreRoot = (Resolve-Path (Join-Path (Get-Location) '..\OpenLadderStudio.Core')).Path
$pagerPath = Join-Path $coreRoot 'Tp02Pg34Pager.cs'
$decoderPath = Join-Path $coreRoot 'Tp02Pg34Decoder.cs'

foreach ($p in @($shellPath, $formPath, $pagerPath, $decoderPath)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Arquivo necessario nao encontrado: $p" }
}

$text = [System.IO.File]::ReadAllText($shellPath)

# Adiciona item de menu logo apos a validacao fisica final, preservando o owner real.
$menuPattern = '(?m)^(?<indent>[ \t]*)(?<owner>[A-Za-z_][A-Za-z0-9_]*)\.DropDownItems\.Add\(DropItem\("Validacao fisica final TP02\.\.\."\s*,\s*delegate\s*\{\s*ShowTp02PhysicalValidation\(\);\s*\}\)\);\s*$'
$menuMatch = [System.Text.RegularExpressions.Regex]::Match($text, $menuPattern)
if (-not $menuMatch.Success) { throw 'Ancora do menu Validacao fisica final TP02 nao encontrada.' }
$line = $menuMatch.Value
$indent = $menuMatch.Groups['indent'].Value
$owner = $menuMatch.Groups['owner'].Value
$insertMenu = $line + "`r`n" + $indent + $owner + '.DropDownItems.Add(DropItem("Ler programa TP02 via PG...", delegate { ShowTp02PgProgramRead(); }));'
$text = $text.Substring(0, $menuMatch.Index) + $insertMenu + $text.Substring($menuMatch.Index + $menuMatch.Length)

# Adiciona o metodo de abertura da ferramenta READ-ONLY.
$methodAnchor = '        private void ShowTp02PhysicalValidation()'
$methodIndex = $text.IndexOf($methodAnchor, [System.StringComparison]::Ordinal)
if ($methodIndex -lt 0) { throw 'Metodo ShowTp02PhysicalValidation nao encontrado.' }
$method = @'
        private void ShowTp02PgProgramRead()
        {
            RefreshProfileUi();
            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Selecione o controlador WEG TP02-60MR antes da leitura PG.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (TP02PgProgramReadForm dialog = new TP02PgProgramReadForm(currentProfile))
            {
                dialog.ShowDialog(this);
            }
            statusText.Text = "Leitura PG do TP02 encerrada";
        }

'@
$text = $text.Substring(0, $methodIndex) + $method + $text.Substring($methodIndex)

function Append-Source([string]$target, [string]$sourcePath) {
    $source = [System.IO.File]::ReadAllText($sourcePath).TrimStart([char]0xFEFF)
    $lines = $source -split "`r?`n"
    $usings = New-Object System.Collections.Generic.List[string]
    $body = New-Object System.Collections.Generic.List[string]
    foreach ($l in $lines) {
        if ($l -match '^using\s+.+;\s*$') { [void]$usings.Add($l) }
        else { [void]$body.Add($l) }
    }
    $prefix = [string]::Join("`r`n", $usings.ToArray())
    if ($prefix.Length -gt 0) { $prefix += "`r`n" }
    $bodyText = [string]::Join("`r`n", $body.ToArray()).Trim()
    return $prefix + $target.TrimStart([char]0xFEFF) + "`r`n`r`n" + $bodyText + "`r`n"
}

# O build legado usa uma lista fixa de fontes. Incorporamos as classes READ-ONLY
# e o decoder/pager ao shell temporario para nao alterar a linha gigante do csc.
$text = Append-Source $text $pagerPath
$text = Append-Source $text $decoderPath
$text = Append-Source $text $formPath

[System.IO.File]::WriteAllText($shellPath, $text, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG Program Read V95 aplicado: HELLO -> F0 -> 38 -> 34 READ-ONLY integrado.'
