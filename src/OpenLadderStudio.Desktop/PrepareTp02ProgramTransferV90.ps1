$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$plcPath = Join-Path (Get-Location) 'PLCPlatform.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $plcPath)) { throw 'PLCPlatform.build.cs nao encontrado.' }

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

$menuNeedle = '            plc.DropDownItems.Add(DropItem("Ler programa", delegate { ShowReader(); }));'
$menuReplacement = @'
            plc.DropDownItems.Add(DropItem("Transferir programa TP02...", delegate { ShowTp02ProgramTransfer(); }));
            plc.DropDownItems.Add(DropItem("Leitor PG experimental...", delegate { ShowReader(); }));
'@
$shell = Replace-Required $shell $menuNeedle $menuReplacement.TrimEnd() 'menu PLC TP02'

$toolbarNeedle = '            AddToolButton(bar, "Ler PLC", StudioIcon.Download, false, delegate { ShowReader(); });'
$toolbarReplacement = '            AddToolButton(bar, "Transferir TP02", StudioIcon.Download, false, delegate { ShowTp02ProgramTransfer(); });'
$shell = Replace-Required $shell $toolbarNeedle $toolbarReplacement 'toolbar TP02'

$methodNeedle = '        private void ShowReader()'
$methodInsert = @'
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
$shell = Replace-First $shell $methodNeedle ($methodInsert + $methodNeedle) 'metodo ShowTp02ProgramTransfer'

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
$section = Replace-Required $section $capNeedle.Trim() $capReplacement.Trim() 'capacidades TP02'

$oldDescription = '            return "Serial TP02: 19200 bps, 8O1, estação configurável. É o perfil que o PC12 original força ao abrir a porta. Operações modernas atuais em modo seguro de leitura.";'
$newDescription = '            return "WEG TP02: PG/PC12 em 19200 8O1; transferencia moderna pela MMI em Computer Link 19200 7N1. RBP le o programa; WBP grava somente em STOP com backup e verificacao por releitura.";'
$section = Replace-Required $section $oldDescription $newDescription 'descricao driver TP02'

$plc = $before + $section + $after
[System.IO.File]::WriteAllText($plcPath, $plc, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 Program Transfer V90 aplicada ao Studio.'
