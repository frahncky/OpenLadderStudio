$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path $path)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    # A ancora vem de um here-string e herda o fim de linha deste script; o alvo
    # gerado pode estar em LF, CRLF ou misto. Procurar so numa das formas falha
    # em todas as ancoras multilinha de uma vez, culpando a primeira da fila.
    $ancoraLf = $needle.Replace("`r`n", "`n")
    $ancoraCrLf = $ancoraLf.Replace("`n", "`r`n")
    $novoLf = $replacement.Replace("`r`n", "`n")
    $novoCrLf = $novoLf.Replace("`n", "`r`n")
    if ($haystack.Contains($ancoraCrLf)) { return $haystack.Replace($ancoraCrLf, $novoCrLf) }
    if ($haystack.Contains($ancoraLf)) { return $haystack.Replace($ancoraLf, $novoLf) }
    throw "Ancora nao encontrada ($label)."
}

$menuNeedle = '            plc.DropDownItems.Add(DropItem("Ler programa", delegate { ShowReader(); }));'
$menuInsert = @'
            plc.DropDownItems.Add(DropItem("Ler programa", delegate { ShowReader(); }));
            plc.DropDownItems.Add(DropItem("Pre-compilar TP02 (dry-run)", delegate { ShowTp02DryRun(); }));
'@
$text = Replace-Required $text $menuNeedle $menuInsert.TrimEnd() 'menu dry-run TP02'

$methodNeedle = '        private void ShowCommunication()'
$methodInsert = @'
        private void ShowTp02DryRun()
        {
            if (ladderForm == null || ladderForm.IsDisposed) ShowLadder();
            if (currentProfile == null || !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Selecione um controlador WEG TP02 antes de usar a pre-compilacao.\r\n\r\nEsta ferramenta e somente offline e nao transmite WBP.",
                    "Pre-compilacao TP02", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (Tp02DryRunPreviewForm dialog = new Tp02DryRunPreviewForm(ladderForm, currentProfile))
            {
                dialog.ShowDialog(this);
            }
        }

'@
$text = Replace-Required $text $methodNeedle ($methodInsert + $methodNeedle) 'ShowTp02DryRun'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'Pre-compilacao TP02 dry-run integrada ao shell.'
