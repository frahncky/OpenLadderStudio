$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

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

$menuNeedle = '            plc.DropDownItems.Add(DropItem("Transferir programa TP02...", delegate { ShowTp02ProgramTransfer(); }));'
$menuReplacement = @'
            plc.DropDownItems.Add(DropItem("Transferir programa TP02...", delegate { ShowTp02ProgramTransfer(); }));
            plc.DropDownItems.Add(DropItem("Validacao fisica final TP02...", delegate { ShowTp02PhysicalValidation(); }));
'@
$shell = Replace-Required $shell $menuNeedle $menuReplacement.TrimEnd() 'menu Validacao fisica TP02'

$methodNeedle = '        private void ShowTp02ProgramTransfer()'
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

'@
$shell = Replace-First $shell $methodNeedle ($methodInsert + $methodNeedle) 'metodo ShowTp02PhysicalValidation'

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 Physical Validation V91 aplicada ao Studio.'
