$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$updaterPath = Join-Path $root 'PC12Updater.build.cs'
foreach ($p in @($shellPath, $updaterPath)) {
    if (-not (Test-Path $p)) { throw "Arquivo de build não encontrado: $p" }
}

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Início não encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim não encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$updater = LF ([System.IO.File]::ReadAllText($updaterPath))
$automaticNew = @'
            if (automatic)
            {
                Text = "OpenLadder Studio - Atualização automática";
                ShowInTaskbar = true;
                Opacity = 1.0;
                WindowState = FormWindowState.Normal;
                Shown += delegate
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        try
                        {
                            statusLabel.Text = "Verificando a nova versão...";
                            Application.DoEvents();
                            CheckForUpdates();
                            if (updateButton.Enabled
                                && string.Equals(updateButton.Text, "ATUALIZAR", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrEmpty(setupUrl))
                            {
                                statusLabel.Text = "Atualização encontrada. Iniciando o download...";
                                Application.DoEvents();
                                DownloadAndInstall();
                            }
                        }
                        catch (Exception ex)
                        {
                            statusLabel.ForeColor = Warning;
                            statusLabel.Text = "Não foi possível concluir a atualização: " + ex.Message;
                            checkButton.Enabled = true;
                        }
                    }));
                };
            }
        }

'@
$updater = Replace-Section $updater '            if (automatic)' '        private void BuildUi()' $automaticNew 'modo automático visível'
$updater = $updater.Replace('statusLabel.Text = "Baixando...";', 'statusLabel.Text = "Baixando a atualização...";')
$updater = $updater.Replace('statusLabel.Text = "Baixando... " + value.ToString(CultureInfo.InvariantCulture) + "%";', 'statusLabel.Text = "Baixando a atualização... " + value.ToString(CultureInfo.InvariantCulture) + "%";')
$updater = $updater.Replace('statusLabel.Text = "Verificando...";', 'statusLabel.Text = "Verificando o arquivo baixado...";')
$updater = $updater.Replace('statusLabel.Text = "Fechando o OpenLadder Studio...";', 'statusLabel.Text = "Fechando o OpenLadder Studio para instalar a atualização...";')
[System.IO.File]::WriteAllText($updaterPath, $updater, [System.Text.Encoding]::UTF8)

$shell = LF ([System.IO.File]::ReadAllText($shellPath))
$shell = $shell.Replace(
    '"A versão v" + latestVersion + " do OpenLadder Studio está disponível.\r\n\r\nDeseja baixar e instalar agora?",',
    '"A versão v" + latestVersion + " do OpenLadder Studio está disponível.\r\n\r\nDeseja baixar e instalar agora?\r\n\r\nO programa será fechado e reaberto automaticamente durante a atualização.",')

$noticeLine = '            if (updateNotice != null) updateNotice.Enabled = false;'
if ($shell.Contains($noticeLine)) {
    $shell = $shell.Replace($noticeLine, @'
            if (updateNotice != null)
            {
                updateNotice.Text = "● PREPARANDO ATUALIZAÇÃO...";
                updateNotice.Enabled = false;
            }
'@.TrimEnd())
}
$shell = $shell.Replace('                statusText.Text = "Preparando a atualização...";',
                        '                statusText.Text = "O atualizador foi iniciado. O progresso será exibido em uma janela própria.";')
$shell = $shell.Replace('Monitor online', 'Monitor on-line')
$shell = $shell.Replace('Verificador de atualizações aberto.', 'Janela de atualização aberta.')
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

Write-Host 'V62 aplicada: atualização automática visível, progresso claro e textos revisados.'
