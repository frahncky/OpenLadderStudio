$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$updaterPath = Join-Path $root 'PC12Updater.build.cs'
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
foreach ($p in @($shellPath, $updaterPath, $ladderPath)) {
    if (-not (Test-Path $p)) { throw "Arquivo de build nao encontrado: $p" }
}

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# -----------------------------------------------------------------------------
# Shell: o aviso de nova versao instala de verdade pelo atualizador externo.
# O mesmo executavel tambem e usado pelo comando Atualizar, evitando dois fluxos.
# -----------------------------------------------------------------------------
$shell = LF ([System.IO.File]::ReadAllText($shellPath))
$shell = $shell.Replace('            updateNotice.Click += delegate { ShowUpdater(); };',
                        '            updateNotice.Click += delegate { StartUpdateNow(); };')

$checkMethod = @'
        private void CheckForUpdatesInBackground()
        {
            Thread worker = new Thread(delegate()
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    string latestVersion;
                    bool available = PC12UpdateChecker.TryGetAvailableVersion(out latestVersion);
                    if (IsDisposed) return;

                    if (!string.IsNullOrEmpty(latestVersion))
                    {
                        if (!available) return;
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                if (IsDisposed || updateNotice == null || updateNotice.Visible) return;
                                updateNotice.Text = "● NOVA VERSÃO v" + latestVersion + " — INSTALAR";
                                updateNotice.Width = 300;
                                updateNotice.LinkColor = Color.FromArgb(251, 191, 36);
                                updateNotice.ActiveLinkColor = Color.White;
                                updateNotice.Visible = true;
                                if (statusText != null)
                                {
                                    statusText.Text = "Nova versão v" + latestVersion + " disponível.";
                                    statusText.ForeColor = Color.FromArgb(251, 191, 36);
                                }

                                DialogResult answer = MessageBox.Show(this,
                                    "A versão v" + latestVersion + " do OpenLadder Studio está disponível.\r\n\r\nDeseja baixar e instalar agora?",
                                    "Atualização disponível", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                                if (answer == DialogResult.Yes) StartUpdateNow();
                            });
                        }
                        catch (InvalidOperationException) { }
                        return;
                    }

                    if (attempt < 2) Thread.Sleep(6000);
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

'@
$shell = Replace-Section $shell '        private void CheckForUpdatesInBackground()' '        private void RefreshProfileUi()' $checkMethod 'aviso com instalacao direta'

$updateMethods = @'
        private bool LaunchExternalUpdater(string arguments)
        {
            try
            {
                string updater = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenLadderUpdater.exe");
                if (!File.Exists(updater))
                {
                    MessageBox.Show(this,
                        "O componente de atualização não foi encontrado. Reinstale o OpenLadder Studio.",
                        "Atualização", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = updater;
                psi.Arguments = arguments ?? string.Empty;
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Não foi possível iniciar a atualização.\r\n\r\n" + ex.Message,
                    "Atualização", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void StartUpdateNow()
        {
            SaveUpdateResumeState();
            if (!LaunchExternalUpdater("/AUTO")) return;

            if (updateNotice != null) updateNotice.Enabled = false;
            if (statusText != null)
            {
                statusText.Text = "Preparando a atualização...";
                statusText.ForeColor = Color.FromArgb(251, 191, 36);
            }
        }

        private void ShowUpdater()
        {
            if (LaunchExternalUpdater(string.Empty) && statusText != null)
                statusText.Text = "Verificador de atualizações aberto.";
        }

'@
$shell = Replace-Section $shell '        private void ShowUpdater()' '        /// <summary>' $updateMethods 'atualizador externo unico'

# Revisao de portugues dos textos finais gerados por etapas anteriores.
$portugueseShell = @{
    'Sessao restaurada apos a atualizacao.' = 'Sessão restaurada após a atualização.'
    'Atualizacoes do OpenLadder Studio' = 'Atualizações do OpenLadder Studio'
    'Verificacao concluida' = 'Verificação concluída'
    'Nao foi possivel' = 'Não foi possível'
}
foreach ($key in $portugueseShell.Keys) { $shell = $shell.Replace($key, $portugueseShell[$key]) }
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Updater: salva marcador, fecha o processo principal de forma graciosa,
# espera a saida e so entao inicia o instalador. Kill e apenas fallback.
# -----------------------------------------------------------------------------
$updater = LF ([System.IO.File]::ReadAllText($updaterPath))

$closeMethod = @'
        private static void CloseRunningStudio()
        {
            Process current = Process.GetCurrentProcess();
            Process[] studios = Process.GetProcessesByName("OpenLadderStudio");
            if (studios == null) return;

            for (int i = 0; i < studios.Length; i++)
            {
                Process studio = studios[i];
                try
                {
                    if (studio == null || studio.Id == current.Id || studio.HasExited) continue;

                    try { studio.CloseMainWindow(); } catch { }
                    bool closed = false;
                    try { closed = studio.WaitForExit(6000); } catch { }

                    if (!closed)
                    {
                        try { studio.Kill(); } catch { }
                        try { studio.WaitForExit(2500); } catch { }
                    }
                }
                catch { }
                finally
                {
                    try { if (studio != null) studio.Dispose(); } catch { }
                }
            }
        }

'@
if (-not $updater.Contains('private static void CloseRunningStudio()')) {
    $readAnchor = '        private string ReadCurrentVersion()'
    if (-not $updater.Contains($readAnchor)) { throw 'ReadCurrentVersion nao encontrado no updater.' }
    $updater = $updater.Replace($readAnchor, $closeMethod + $readAnchor)
}

$launchOld = @'
                PrepareResumeAfterUpdate();
                psi.FileName = downloadedSetup;
'@
$launchNew = @'
                statusLabel.Text = "Fechando o OpenLadder Studio...";
                Application.DoEvents();
                PrepareResumeAfterUpdate();
                CloseRunningStudio();
                psi.FileName = downloadedSetup;
'@
$updater = Replace-Required $updater $launchOld.TrimEnd() $launchNew.TrimEnd() 'fechamento antes do instalador'

$portugueseUpdater = @{
    'card.Controls.Add(NewLabel("Instalada",' = 'card.Controls.Add(NewLabel("Versão instalada",'
    'card.Controls.Add(NewLabel("Disponível",' = 'card.Controls.Add(NewLabel("Nova versão",'
    'statusLabel.Text = "Versão atualizada.";' = 'statusLabel.Text = "Você já está usando a versão mais recente.";'
    '"Nenhuma release publicada."' = '"Nenhuma versão publicada."'
}
foreach ($key in $portugueseUpdater.Keys) { $updater = $updater.Replace($key, $portugueseUpdater[$key]) }
[System.IO.File]::WriteAllText($updaterPath, $updater, [System.Text.Encoding]::UTF8)

# Pequena revisao textual adicional no editor Ladder.
$ladder = LF ([System.IO.File]::ReadAllText($ladderPath))
$ladder = $ladder.Replace('"Programa compativel.', '"Programa compatível.')
$ladder = $ladder.Replace('"Parametro', '"Parâmetro')
$ladder = $ladder.Replace('"Nenhuma alteracao', '"Nenhuma alteração')
[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)

Write-Host 'V61 aplicada: portugues revisado, aviso instala diretamente e Studio fecha antes do instalador.'
