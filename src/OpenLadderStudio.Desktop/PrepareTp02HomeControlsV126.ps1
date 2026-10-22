$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($text.Contains($needleCrLf)) { return $text.Replace($needleCrLf, $replacementCrLf) }
    if ($text.Contains($needleLf)) { return $text.Replace($needleLf, $replacementLf) }
    throw "Ancora nao encontrada ($label)."
}

# v1.26: os comandos operacionais do TP02 passam a ficar permanentemente
# visiveis na tela principal do OpenLadder, logo abaixo da barra principal.
# READ/WRITE/VERIFY disparam os mesmos fluxos da tela PG v1.25.
# STOP/RUN continuam protegidos e nao transmitem quadro desconhecido.
$uiNeedle = @'
            Control status = BuildStatusBar();
            Controls.Add(status);

            Control toolbar = BuildToolbar();
            Controls.Add(toolbar);
'@
$uiReplacement = @'
            Control status = BuildStatusBar();
            Controls.Add(status);

            Control tp02HomeBar = BuildTp02HomeBarV126();
            Controls.Add(tp02HomeBar);

            Control toolbar = BuildToolbar();
            Controls.Add(toolbar);
'@
$shell = Replace-Required $shell $uiNeedle $uiReplacement 'barra TP02 na tela principal'

$menuAnchor = '        private MenuStrip BuildMenu()'
$menuIndex = $shell.IndexOf($menuAnchor, [System.StringComparison]::Ordinal)
if ($menuIndex -lt 0) { throw 'BuildMenu nao encontrado para inserir barra TP02 v1.26.' }

$homeMethods = @'
        private Control BuildTp02HomeBarV126()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 47;
            bar.Fill = Color.FromArgb(31, 34, 38);
            bar.BottomLine = Border;

            Label title = new Label();
            title.Text = "TP02 / TP-232PG";
            title.AutoSize = false;
            title.Location = new Point(16, 8);
            title.Size = new Size(135, 30);
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.ForeColor = Fore;
            title.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            bar.Controls.Add(title);

            int x = 156;
            bar.Controls.Add(NewTp02HomeButtonV126("READ", x, 96, false, delegate { ExecuteTp02HomeCommandV126("READ"); }));
            x += 104;
            bar.Controls.Add(NewTp02HomeButtonV126("WRITE", x, 104, true, delegate { ExecuteTp02HomeCommandV126("WRITE"); }));
            x += 112;
            bar.Controls.Add(NewTp02HomeButtonV126("VERIFY", x, 108, false, delegate { ExecuteTp02HomeCommandV126("VERIFY"); }));
            x += 116;
            bar.Controls.Add(NewTp02HomeButtonV126("STOP", x, 92, false, delegate { ExecuteTp02HomeCommandV126("STOP"); }));
            x += 100;
            bar.Controls.Add(NewTp02HomeButtonV126("RUN", x, 92, false, delegate { ExecuteTp02HomeCommandV126("RUN"); }));

            Label safety = new Label();
            safety.Text = "STOP / RUN protegidos ate validacao fisica";
            safety.AutoSize = true;
            safety.Location = new Point(x + 108, 16);
            safety.ForeColor = StudioTheme.Warning;
            safety.Font = StudioTheme.Small;
            bar.Controls.Add(safety);

            return bar;
        }

        private Button NewTp02HomeButtonV126(string text, int left, int width, bool primary, EventHandler action)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(left, 7);
            button.Size = new Size(width, 32);
            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.TabStop = false;
            button.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            button.BackColor = primary ? Accent : ChromeLight;
            button.ForeColor = primary ? Color.White : Fore;
            button.FlatAppearance.BorderColor = primary ? AccentDark : Border;
            button.FlatAppearance.BorderSize = 1;
            if (action != null) button.Click += action;
            return button;
        }

        private void ExecuteTp02HomeCommandV126(string command)
        {
            string normalized = (command ?? string.Empty).Trim().ToUpperInvariant();
            RefreshProfileUi();
            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Selecione o controlador WEG TP02-60MR antes de usar " + normalized + ".",
                    "OpenLadder Studio - TP02", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ladderForm == null || ladderForm.IsDisposed) ShowLadder();
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                MessageBox.Show(this, "O editor Ladder nao esta disponivel.", "OpenLadder Studio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (TP02Pg33NoOpProbeForm dialog = new TP02Pg33NoOpProbeForm(currentProfile, ladderForm))
            {
                dialog.QueueHomeCommandV126(normalized);
                dialog.ShowDialog(this);
            }
            statusText.Text = "TP02 PG: " + normalized + " encerrado";
        }

'@
$shell = $shell.Substring(0, $menuIndex) + $homeMethods + $shell.Substring($menuIndex)

# Permite que um botao da tela principal abra a mesma janela PG e execute
# automaticamente a operacao escolhida depois que a janela estiver carregada.
$readAnchor = '        private void StartPgReadV125()'
$readIndex = $shell.IndexOf($readAnchor, [System.StringComparison]::Ordinal)
if ($readIndex -lt 0) { throw 'StartPgReadV125 nao encontrado para comando da tela principal.' }

$queueMethod = @'
        internal void QueueHomeCommandV126(string command)
        {
            string normalized = (command ?? string.Empty).Trim().ToUpperInvariant();
            bool executed = false;
            Shown += delegate
            {
                if (executed) return;
                executed = true;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (string.Equals(normalized, "READ", StringComparison.Ordinal))
                        StartPgReadV125();
                    else if (string.Equals(normalized, "WRITE", StringComparison.Ordinal))
                        StartProjectWriteV123();
                    else if (string.Equals(normalized, "VERIFY", StringComparison.Ordinal))
                        StartProjectVerifyV124();
                    else if (string.Equals(normalized, "STOP", StringComparison.Ordinal))
                        ShowPgRunStopPendingV125("STOP");
                    else if (string.Equals(normalized, "RUN", StringComparison.Ordinal))
                        ShowPgRunStopPendingV125("RUN");
                }));
            };
        }

'@
$shell = $shell.Substring(0, $readIndex) + $queueMethod + $shell.Substring($readIndex)

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 Home Controls V126 aplicado: READ/WRITE/VERIFY/STOP/RUN visiveis na tela principal.'
