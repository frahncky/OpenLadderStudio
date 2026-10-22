$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$plcPath = Join-Path (Get-Location) 'PLCPlatform.build.cs'
$physicalPath = Join-Path (Get-Location) 'TP02PhysicalValidationForm.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $plcPath)) { throw 'PLCPlatform.build.cs nao encontrado.' }
if (-not (Test-Path $physicalPath)) { throw 'TP02PhysicalValidationForm.cs nao encontrado.' }

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($text.Contains($needleCrLf)) { return $text.Replace($needleCrLf, $replacementCrLf) }
    if ($text.Contains($needleLf)) { return $text.Replace($needleLf, $replacementLf) }
    throw "Ancora nao encontrada ($label)."
}

function Replace-First([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $i = $text.IndexOf($needle, [System.StringComparison]::Ordinal)
    if ($i -lt 0) { throw "Ancora nao encontrada ($label)." }
    return $text.Substring(0, $i) + $replacement + $text.Substring($i + $needle.Length)
}

$shell = [System.IO.File]::ReadAllText($shellPath)

# As revisoes de UI podem renomear ou mover o item. ShowReader() e a ancora
# semantica estavel para localizar a entrada de leitura TP02.
$menuPattern = '(?m)^(?<indent>[ \t]*)(?<owner>[A-Za-z_][A-Za-z0-9_]*)\.DropDownItems\.Add\(DropItem\("[^"\r\n]*",\s*delegate\s*\{\s*ShowReader\(\);\s*\}\)\);\s*$'
$menuMatch = [System.Text.RegularExpressions.Regex]::Match($shell, $menuPattern)
if (-not $menuMatch.Success) { throw 'Ancora semantica ShowReader() nao encontrada em nenhum menu.' }
$indent = $menuMatch.Groups['indent'].Value
$owner = $menuMatch.Groups['owner'].Value
$menuReplacement = $indent + $owner + '.DropDownItems.Add(DropItem("Transferir programa TP02...", delegate { ShowTp02ProgramTransfer(); }));' + "`r`n" +
                   $indent + $owner + '.DropDownItems.Add(DropItem("Validacao fisica final TP02...", delegate { ShowTp02PhysicalValidation(); }));' + "`r`n" +
                   $indent + $owner + '.DropDownItems.Add(DropItem("Leitor PG experimental...", delegate { ShowReader(); }));'
$shell = $shell.Substring(0, $menuMatch.Index) + $menuReplacement + $shell.Substring($menuMatch.Index + $menuMatch.Length)

# Se existir um botao de toolbar apontando para ShowReader(), ele vira a entrada
# principal de transferencia. A validacao final permanece no menu PLC.
$toolbarPattern = '(?m)^(?<indent>[ \t]*)AddToolButton\(bar,\s*"[^"\r\n]*",\s*StudioIcon\.[A-Za-z0-9_]+,\s*(?:true|false),\s*delegate\s*\{\s*ShowReader\(\);\s*\}\);\s*$'
$toolbarMatch = [System.Text.RegularExpressions.Regex]::Match($shell, $toolbarPattern)
if ($toolbarMatch.Success) {
    $toolbarReplacement = $toolbarMatch.Groups['indent'].Value + 'AddToolButton(bar, "Transferir TP02", StudioIcon.Download, false, delegate { ShowTp02ProgramTransfer(); });'
    $shell = $shell.Substring(0, $toolbarMatch.Index) + $toolbarReplacement + $shell.Substring($toolbarMatch.Index + $toolbarMatch.Length)
}

$methodNeedle = '        private void ShowReader()'
$methodInsert = @'
        private void ShowTp02PhysicalValidation()
        {
            RefreshProfileUi();
            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Selecione o controlador WEG TP02-60MR antes da validacao fisica.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ladderForm == null || ladderForm.IsDisposed) ShowLadder();
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                MessageBox.Show(this, "O editor Ladder nao esta disponivel.", "OpenLadder Studio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (TP02PhysicalValidationForm dialog = new TP02PhysicalValidationForm(currentProfile, ladderForm))
            {
                dialog.ShowDialog(this);
            }
            statusText.Text = "Validacao fisica TP02 encerrada";
        }

        private void ShowTp02ProgramTransfer()
        {
            RefreshProfileUi();
            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Selecione o controlador WEG TP02-60MR antes de abrir a transferencia de programa.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ladderForm == null || ladderForm.IsDisposed) ShowLadder();
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                MessageBox.Show(this, "O editor Ladder nao esta disponivel.", "OpenLadder Studio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (TP02ProgramTransferForm dialog = new TP02ProgramTransferForm(currentProfile, ladderForm))
            {
                dialog.ShowDialog(this);
            }
            statusText.Text = "Transferencia TP02 encerrada";
        }

'@
$shell = Replace-First $shell $methodNeedle ($methodInsert + $methodNeedle) 'metodos TP02 transfer/validation'

# O Build.bat ja compila UniversalStudioShell.build.cs. Mantemos a lista de fontes
# estavel incorporando a classe de validacao nesse arquivo temporario de build.
$physical = [System.IO.File]::ReadAllText($physicalPath)
$physicalLines = $physical -split "`r?`n"
$usingLines = New-Object System.Collections.Generic.List[string]
$bodyLines = New-Object System.Collections.Generic.List[string]
foreach ($line in $physicalLines) {
    if ($line -match '^using\s+.+;\s*$') { [void]$usingLines.Add($line) }
    else { [void]$bodyLines.Add($line) }
}
$prefix = [string]::Join("`r`n", $usingLines.ToArray()) + "`r`n"
$body = [string]::Join("`r`n", $bodyLines.ToArray()).Trim()
$shell = $prefix + $shell.TrimStart([char]0xFEFF) + "`r`n`r`n" + $body + "`r`n"

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

$plc = [System.IO.File]::ReadAllText($plcPath)
$start = $plc.IndexOf('    internal sealed class WegTp02Driver', [System.StringComparison]::Ordinal)
$end = $plc.IndexOf('    internal sealed class GenericModbusRtuDriver', $start, [System.StringComparison]::Ordinal)
if ($start -lt 0 -or $end -lt 0) { throw 'Secao WegTp02Driver nao encontrada.' }
$before = $plc.Substring(0, $start)
$section = $plc.Substring($start, $end - $start)
$after = $plc.Substring($end)

$capNeedle = @'
            c.ReadRegisters = true;
            c.ReadProgram = true;
            return c;
'@
$capReplacement = @'
            c.ReadRegisters = true;
            c.ReadProgram = true;
            c.UploadProgram = true;
            c.DownloadProgram = true;
            return c;
'@
if ($section.Contains($capNeedle.Trim())) {
    $section = Replace-Required $section $capNeedle.Trim() $capReplacement.Trim() 'capacidades TP02'
}

$descStart = $section.IndexOf('            return "Serial TP02:', [System.StringComparison]::Ordinal)
if ($descStart -lt 0) { $descStart = $section.IndexOf('            return "WEG TP02:', [System.StringComparison]::Ordinal) }
if ($descStart -lt 0) { throw 'Descricao WegTp02Driver nao encontrada.' }
$descEnd = $section.IndexOf('";', $descStart, [System.StringComparison]::Ordinal)
if ($descEnd -lt 0) { throw 'Fim da descricao WegTp02Driver nao encontrado.' }
$descEnd += 2
$newDescription = '            return "WEG TP02: PG/PC12 em 19200 8O1; transferencia moderna pela MMI em Computer Link 19200 7N1. RBP le o programa; WBP grava somente em STOP com backup e verificacao por releitura.";'
$section = $section.Substring(0, $descStart) + $newDescription + $section.Substring($descEnd)

$plc = $before + $section + $after
[System.IO.File]::WriteAllText($plcPath, $plc, [System.Text.Encoding]::UTF8)

# A validacao fisica ja foi incorporada ao shell; agora aplicamos o auto-retry
# de inicializacao do TP-232PG diretamente sobre a fonte temporaria compilada.
& (Join-Path (Get-Location) 'PrepareTp02PgRecoveryV93.ps1')

Write-Host 'TP02 Program Transfer V90 + Physical Validation + PG Recovery V93 aplicada ao Studio.'
