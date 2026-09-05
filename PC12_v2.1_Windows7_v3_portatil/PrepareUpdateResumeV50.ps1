$ErrorActionPreference = 'Stop'

$root = Get-Location
$updaterPath = Join-Path $root 'PC12Updater.build.cs'
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'

if (-not (Test-Path $updaterPath)) { throw 'PC12Updater.build.cs nao encontrado.' }
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}

function Replace-First([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $i = $text.IndexOf($needle)
    if ($i -lt 0) { throw "Ancora nao encontrada ($label)." }
    return $text.Substring(0, $i) + $replacement + $text.Substring($i + $needle.Length)
}

# -----------------------------------------------------------------------------
# Shell: salva a sessao ao fechar e restaura somente quando a atualizacao deixa
# o marcador resume-after-update.flag. O snapshot inclui o Ladder serializado,
# inclusive alteracoes ainda nao salvas, o caminho do projeto e a ultima area
# de trabalho aberta antes da tela de atualizacao.
# -----------------------------------------------------------------------------
$shell = [System.IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")

$buildNeedle = '            BuildUi();'
$buildReplacement = @'
            BuildUi();
            FormClosing += delegate { SaveUpdateResumeState(); };
            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { RestoreUpdateResumeState(); })); };
'@
$buildReplacement = $buildReplacement.Replace("`r`n", "`n")
$shell = Replace-First $shell $buildNeedle $buildReplacement.TrimEnd() 'eventos da sessao'

$sessionMethods = @'
        private static string UpdateResumeDirectory()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(root, "OpenLadder Studio");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static string UpdateResumeStatePath()
        {
            return Path.Combine(UpdateResumeDirectory(), "update-session.dat");
        }

        private static string UpdateResumeMarkerPath()
        {
            return Path.Combine(UpdateResumeDirectory(), "resume-after-update.flag");
        }

        private static string SessionEncode(string value)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string SessionDecode(string value)
        {
            try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty)); }
            catch { return string.Empty; }
        }

        private string ResolveResumeTabKey()
        {
            try
            {
                if (tabStrip != null)
                {
                    StudioTab selected = tabStrip.Selected;
                    if (selected != null && !string.IsNullOrEmpty(selected.Key)
                        && !string.Equals(selected.Key, "UPD", StringComparison.OrdinalIgnoreCase))
                        return selected.Key;

                    List<StudioTab> tabs = tabStrip.Tabs;
                    if (tabs != null)
                    {
                        for (int i = tabs.Count - 1; i >= 0; i--)
                        {
                            StudioTab tab = tabs[i];
                            if (tab != null && !string.IsNullOrEmpty(tab.Key)
                                && !string.Equals(tab.Key, "UPD", StringComparison.OrdinalIgnoreCase))
                                return tab.Key;
                        }
                    }
                }
            }
            catch { }
            return "LD";
        }

        private void SaveUpdateResumeState()
        {
            try
            {
                string projectFile = string.Empty;
                string ladderSnapshot = string.Empty;
                bool ladderDirty = false;
                string resumeTab = ResolveResumeTabKey();

                if (ladderForm != null && !ladderForm.IsDisposed)
                {
                    BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    FieldInfo fileField = typeof(LadderEditorForm).GetField("currentFile", flags);
                    FieldInfo dirtyField = typeof(LadderEditorForm).GetField("dirty", flags);
                    MethodInfo serialize = typeof(LadderEditorForm).GetMethod("SerializeProject", flags);

                    if (fileField != null)
                    {
                        object value = fileField.GetValue(ladderForm);
                        projectFile = value == null ? string.Empty : value.ToString();
                    }
                    if (dirtyField != null)
                    {
                        object value = dirtyField.GetValue(ladderForm);
                        ladderDirty = value is bool && (bool)value;
                    }
                    if (serialize != null)
                    {
                        object value = serialize.Invoke(ladderForm, null);
                        ladderSnapshot = value == null ? string.Empty : value.ToString();
                    }
                }

                System.Text.StringBuilder data = new System.Text.StringBuilder();
                data.AppendLine("OPENLADDER-UPDATE-SESSION-1");
                data.AppendLine("tab=" + SessionEncode(resumeTab));
                data.AppendLine("file=" + SessionEncode(projectFile));
                data.AppendLine("dirty=" + (ladderDirty ? "1" : "0"));
                data.AppendLine("ladder=" + SessionEncode(ladderSnapshot));
                File.WriteAllText(UpdateResumeStatePath(), data.ToString(), System.Text.Encoding.UTF8);
            }
            catch
            {
                // A persistencia da sessao nunca deve impedir o fechamento do Studio.
            }
        }

        private void RestoreUpdateResumeState()
        {
            string marker = UpdateResumeMarkerPath();
            if (!File.Exists(marker)) return;

            try
            {
                // Remove primeiro para evitar um ciclo de restauracao caso a aplicacao
                // seja encerrada inesperadamente durante a propria restauracao.
                try { File.Delete(marker); } catch { }

                string statePath = UpdateResumeStatePath();
                if (!File.Exists(statePath)) return;
                string[] lines = File.ReadAllLines(statePath, System.Text.Encoding.UTF8);
                if (lines.Length == 0 || lines[0].Trim() != "OPENLADDER-UPDATE-SESSION-1") return;

                string tab = "LD";
                string projectFile = string.Empty;
                string ladderSnapshot = string.Empty;
                bool ladderDirty = false;

                for (int i = 1; i < lines.Length; i++)
                {
                    int eq = lines[i].IndexOf('=');
                    if (eq <= 0) continue;
                    string key = lines[i].Substring(0, eq).Trim();
                    string value = lines[i].Substring(eq + 1);
                    if (key == "tab") tab = SessionDecode(value);
                    else if (key == "file") projectFile = SessionDecode(value);
                    else if (key == "dirty") ladderDirty = value.Trim() == "1";
                    else if (key == "ladder") ladderSnapshot = SessionDecode(value);
                }

                ShowLadder();

                if (ladderForm != null && !ladderForm.IsDisposed && !string.IsNullOrEmpty(ladderSnapshot))
                {
                    BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    MethodInfo deserialize = typeof(LadderEditorForm).GetMethod("DeserializeProject", flags);
                    FieldInfo fileField = typeof(LadderEditorForm).GetField("currentFile", flags);
                    FieldInfo dirtyField = typeof(LadderEditorForm).GetField("dirty", flags);
                    FieldInfo canvasField = typeof(LadderEditorForm).GetField("canvas", flags);
                    MethodInfo updateLabel = typeof(LadderEditorForm).GetMethod("UpdateProjectLabel", flags);

                    if (deserialize != null) deserialize.Invoke(ladderForm, new object[] { ladderSnapshot });
                    if (fileField != null) fileField.SetValue(ladderForm, projectFile ?? string.Empty);
                    if (dirtyField != null) dirtyField.SetValue(ladderForm, ladderDirty);
                    if (updateLabel != null) updateLabel.Invoke(ladderForm, null);
                    if (canvasField != null)
                    {
                        Control canvasControl = canvasField.GetValue(ladderForm) as Control;
                        if (canvasControl != null) canvasControl.Invalidate();
                    }
                    UpdateProjectName();
                }

                RestoreWorkArea(tab);
                if (statusText != null) statusText.Text = "Sessao restaurada apos a atualizacao.";
            }
            catch
            {
                // Em caso de snapshot antigo/incompativel, abre normalmente no Ladder.
                try { ShowLadder(); } catch { }
            }
        }

        private void RestoreWorkArea(string key)
        {
            string k = string.IsNullOrEmpty(key) ? "LD" : key.ToUpperInvariant();
            if (k == "PLC") { ShowCommunication(); return; }
            if (k == "MON") { ShowMonitor(); return; }
            if (k == "RBP") { ShowReader(); return; }
            if (k == "DEC") { ShowDecoder(); return; }
            if (k == "CAL") { ShowCalibration(); return; }
            if (k == "IL") { ShowIl(); return; }
            ShowLadder();
        }

'@
$sessionMethods = $sessionMethods.Replace("`r`n", "`n")
$sessionAnchor = '        private void StartAutomaticUpdater()'
if (-not $shell.Contains($sessionAnchor)) { throw 'StartAutomaticUpdater nao encontrado no shell.' }
$shell = $shell.Replace($sessionAnchor, $sessionMethods + $sessionAnchor)

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Updater: antes de iniciar o instalador marca que a proxima abertura deve
# restaurar a sessao. O instalador existente ja usa CLOSEAPPLICATIONS e
# RESTARTAPPLICATIONS para fechar e reabrir o Studio.
# -----------------------------------------------------------------------------
$updater = [System.IO.File]::ReadAllText($updaterPath).Replace("`r`n", "`n")

$launchNeedle = @'
                progress.Value = 100;
                ProcessStartInfo psi = new ProcessStartInfo();
'@
$launchReplacement = @'
                progress.Value = 100;
                statusLabel.ForeColor = Accent;
                statusLabel.Text = "Fechando o OpenLadder Studio para atualizar...";
                PrepareResumeAfterUpdate();
                Application.DoEvents();

                ProcessStartInfo psi = new ProcessStartInfo();
'@
$launchNeedle = $launchNeedle.Replace("`r`n", "`n").TrimEnd()
$launchReplacement = $launchReplacement.Replace("`r`n", "`n").TrimEnd()
$updater = Replace-Required $updater $launchNeedle $launchReplacement 'marcador antes do instalador'

$updaterMethods = @'
        private static void PrepareResumeAfterUpdate()
        {
            try
            {
                // So cria o marcador se o Studio estiver realmente aberto. Assim,
                // executar o updater isoladamente nao restaura uma sessao antiga.
                Process[] studios = Process.GetProcessesByName("OpenLadderStudio");
                if (studios == null || studios.Length == 0) return;

                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string dir = Path.Combine(root, "OpenLadder Studio");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "resume-after-update.flag"),
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), Encoding.UTF8);
            }
            catch
            {
                // O update ainda pode prosseguir; apenas nao havera restauracao fina.
            }
        }

'@
$updaterMethods = $updaterMethods.Replace("`r`n", "`n")
$readAnchor = '        private string ReadCurrentVersion()'
if (-not $updater.Contains($readAnchor)) { throw 'ReadCurrentVersion nao encontrado no updater.' }
$updater = $updater.Replace($readAnchor, $updaterMethods + $readAnchor)

[System.IO.File]::WriteAllText($updaterPath, $updater, [System.Text.Encoding]::UTF8)
Write-Host 'Retomada automatica apos atualizacao V50 aplicada.'
