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

function Replace-Block([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# Estado de conexao da barra principal.
$fieldNeedle = @'
        private PlcDeviceProfile currentProfile;
        private IPlcDriver currentDriver;
'@
$fieldReplacement = @'
        private PlcDeviceProfile currentProfile;
        private IPlcDriver currentDriver;

        // v1.28: fluxo igual ao software original: CONECTAR estabelece a sessao.
        // Os comandos permanecem visiveis/clicaveis; sem conexao eles apenas
        // sinalizam SEM CONEXAO e nao transmitem nada ao PLC.
        private bool tp02HomeConnectedV128;
        private string tp02HomePortV128 = string.Empty;
        private string tp02HomeStateV128 = string.Empty;
        private string tp02HomeModelV128 = string.Empty;
        private Button tp02ConnectButtonV128;
        private Button tp02ReadButtonV128;
        private Button tp02WriteButtonV128;
        private Button tp02VerifyButtonV128;
        private Button tp02StopButtonV128;
        private Button tp02RunButtonV128;
        private Label tp02ConnectionLabelV128;
'@
$shell = Replace-Required $shell $fieldNeedle $fieldReplacement 'campos conexao TP02 v1.28'

# Reconstroi a barra TP02 da tela principal com CONECTAR primeiro.
$barStart = '        private Control BuildTp02HomeBarV126()'
$barEnd = '        private Button NewTp02HomeButtonV126'
$barReplacement = @'
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
            title.Location = new Point(14, 8);
            title.Size = new Size(126, 30);
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.ForeColor = Fore;
            title.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            bar.Controls.Add(title);

            tp02ConnectButtonV128 = NewTp02HomeButtonV126("CONECTAR", 145, 112, true,
                delegate { ToggleTp02HomeConnectionV128(); });
            bar.Controls.Add(tp02ConnectButtonV128);

            tp02ConnectionLabelV128 = new Label();
            tp02ConnectionLabelV128.Text = "DESCONECTADO";
            tp02ConnectionLabelV128.AutoSize = false;
            tp02ConnectionLabelV128.Location = new Point(264, 8);
            tp02ConnectionLabelV128.Size = new Size(205, 30);
            tp02ConnectionLabelV128.TextAlign = ContentAlignment.MiddleLeft;
            tp02ConnectionLabelV128.ForeColor = Muted;
            tp02ConnectionLabelV128.Font = new Font("Segoe UI Semibold", 8.1f, FontStyle.Bold);
            bar.Controls.Add(tp02ConnectionLabelV128);

            int x = 475;
            tp02ReadButtonV128 = NewTp02HomeButtonV126("LER", x, 86, false,
                delegate { ExecuteTp02HomeCommandV126("READ"); });
            bar.Controls.Add(tp02ReadButtonV128);
            x += 94;

            tp02WriteButtonV128 = NewTp02HomeButtonV126("ESCREVER", x, 104, true,
                delegate { ExecuteTp02HomeCommandV126("WRITE"); });
            bar.Controls.Add(tp02WriteButtonV128);
            x += 112;

            tp02VerifyButtonV128 = NewTp02HomeButtonV126("VERIFICAR", x, 108, false,
                delegate { ExecuteTp02HomeCommandV126("VERIFY"); });
            bar.Controls.Add(tp02VerifyButtonV128);
            x += 116;

            tp02StopButtonV128 = NewTp02HomeButtonV126("STOP", x, 88, false,
                delegate { ExecuteTp02HomeCommandV126("STOP"); });
            tp02StopButtonV128.ForeColor = StudioTheme.Warning;
            bar.Controls.Add(tp02StopButtonV128);
            x += 96;

            tp02RunButtonV128 = NewTp02HomeButtonV126("RUN", x, 88, false,
                delegate { ExecuteTp02HomeCommandV126("RUN"); });
            tp02RunButtonV128.ForeColor = Accent;
            bar.Controls.Add(tp02RunButtonV128);

            UpdateTp02HomeButtonsV128();
            return bar;
        }

        private void ToggleTp02HomeConnectionV128()
        {
            if (tp02HomeConnectedV128)
            {
                ResetTp02HomeConnectionV128("Desconectado pelo usuario.");
                return;
            }

            RefreshProfileUi();
            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Selecione o controlador WEG TP02-60MR antes de conectar.",
                    "OpenLadder Studio - TP02", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ladderForm == null || ladderForm.IsDisposed) ShowLadder();
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                MessageBox.Show(this, "O editor Ladder nao esta disponivel.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (tp02ConnectButtonV128 != null) tp02ConnectButtonV128.Enabled = false;
            if (tp02ConnectionLabelV128 != null)
            {
                tp02ConnectionLabelV128.Text = "CONECTANDO...";
                tp02ConnectionLabelV128.ForeColor = StudioTheme.Warning;
            }
            statusText.Text = "TP02: conectando pelo TP-232PG...";
            Application.DoEvents();

            string state;
            string portName;
            string detail;
            string error;
            bool connected;
            using (TP02Pg33NoOpProbeForm dialog = new TP02Pg33NoOpProbeForm(currentProfile, ladderForm))
            {
                connected = dialog.TryHomeConnectV128(out state, out portName, out detail, out error);
            }

            if (!connected)
            {
                tp02HomeConnectedV128 = false;
                tp02HomePortV128 = string.Empty;
                tp02HomeStateV128 = string.Empty;
                UpdateTp02HomeButtonsV128();
                statusText.Text = "TP02: SEM CONEXAO";
                MessageBox.Show(this,
                    "SEM CONEXAO COM O TP02.\r\n\r\n" + error,
                    "OpenLadder Studio - TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            tp02HomeConnectedV128 = true;
            tp02HomePortV128 = portName ?? string.Empty;
            tp02HomeStateV128 = state ?? string.Empty;
            tp02HomeModelV128 = currentProfile.Model ?? string.Empty;
            UpdateTp02HomeButtonsV128();

            if (connectionValue != null)
                connectionValue.Text = "Conectado " + tp02HomePortV128 + " / " + tp02HomeStateV128;
            if (modeText != null)
                modeText.Text = currentProfile.Model + "    |    " + currentProfile.Protocol
                    + "    |    ON-LINE " + tp02HomeStateV128 + "    |    v1.28";
            statusText.Text = "TP02 conectado em " + tp02HomePortV128 + " / estado " + tp02HomeStateV128;
        }

        private void ResetTp02HomeConnectionV128(string reason)
        {
            tp02HomeConnectedV128 = false;
            tp02HomePortV128 = string.Empty;
            tp02HomeStateV128 = string.Empty;
            tp02HomeModelV128 = string.Empty;
            UpdateTp02HomeButtonsV128();
            if (connectionValue != null) connectionValue.Text = "Desconectado";
            if (modeText != null)
            {
                string model = currentProfile == null ? "SEM PLC" : currentProfile.Model;
                string protocol = currentProfile == null ? "-" : currentProfile.Protocol;
                modeText.Text = model + "    |    " + protocol + "    |    OFF-LINE    |    v1.28";
            }
            if (!string.IsNullOrEmpty(reason)) statusText.Text = reason;
        }

        private void UpdateTp02HomeButtonsV128()
        {
            bool connected = tp02HomeConnectedV128;

            if (tp02ConnectButtonV128 != null)
            {
                tp02ConnectButtonV128.Enabled = true;
                tp02ConnectButtonV128.Text = connected ? "DESCONECTAR" : "CONECTAR";
            }

            // Igual ao software original: os comandos nao somem nem ficam cinza.
            // Sem conexao, o clique apenas sinaliza SEM CONEXAO e nao transmite bytes.
            if (tp02ReadButtonV128 != null) tp02ReadButtonV128.Enabled = true;
            if (tp02WriteButtonV128 != null) tp02WriteButtonV128.Enabled = true;
            if (tp02VerifyButtonV128 != null) tp02VerifyButtonV128.Enabled = true;
            if (tp02StopButtonV128 != null) tp02StopButtonV128.Enabled = true;
            if (tp02RunButtonV128 != null) tp02RunButtonV128.Enabled = true;

            if (tp02ConnectionLabelV128 != null)
            {
                if (!connected)
                {
                    tp02ConnectionLabelV128.Text = "DESCONECTADO";
                    tp02ConnectionLabelV128.ForeColor = Muted;
                }
                else
                {
                    tp02ConnectionLabelV128.Text = "CONECTADO  " + tp02HomePortV128 + "  •  " + tp02HomeStateV128;
                    tp02ConnectionLabelV128.ForeColor = Accent;
                }
            }
        }

'@
$shell = Replace-Block $shell $barStart $barEnd $barReplacement 'barra connect-first v1.28'

# Os comandos da tela principal exigem conexao previa e reutilizam a COM
# identificada na etapa CONECTAR.
$cmdStart = '        private void ExecuteTp02HomeCommandV126(string command)'
$cmdEnd = '        private MenuStrip BuildMenu()'
$cmdBlock = $shell.Substring($shell.IndexOf($cmdStart, [System.StringComparison]::Ordinal))
$cmdEndIndexRelative = $cmdBlock.IndexOf($cmdEnd, [System.StringComparison]::Ordinal)
if ($cmdEndIndexRelative -lt 0) { throw 'Fim ExecuteTp02HomeCommandV126 nao encontrado.' }
$cmdReplacement = @'
        private void ExecuteTp02HomeCommandV126(string command)
        {
            string normalized = (command ?? string.Empty).Trim().ToUpperInvariant();
            RefreshProfileUi();

            if (!tp02HomeConnectedV128)
            {
                statusText.Text = "TP02: SEM CONEXAO";
                if (connectionValue != null) connectionValue.Text = "Sem conexao";
                MessageBox.Show(this,
                    "SEM CONEXAO COM O TP02.\r\n\r\nClique em CONECTAR para estabelecer a comunicacao antes de executar "
                    + normalized + ".\r\n\r\nNenhum byte foi transmitido.",
                    "OpenLadder Studio - SEM CONEXAO",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentProfile.Model ?? string.Empty, tp02HomeModelV128, StringComparison.OrdinalIgnoreCase))
            {
                ResetTp02HomeConnectionV128("Controlador alterado; conecte novamente.");
                MessageBox.Show(this,
                    "SEM CONEXAO COM O TP02.\r\n\r\nO controlador ativo mudou. Clique em CONECTAR novamente.",
                    "OpenLadder Studio - SEM CONEXAO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if ((string.Equals(normalized, "WRITE", StringComparison.Ordinal) ||
                 string.Equals(normalized, "VERIFY", StringComparison.Ordinal)) &&
                !string.Equals(tp02HomeStateV128, "STOP", StringComparison.Ordinal))
            {
                MessageBox.Show(this,
                    normalized + " exige o TP02 em STOP.\r\n\r\nEstado atual detectado: " + tp02HomeStateV128 + ".",
                    "OpenLadder Studio - TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                dialog.UseHomePortV128(tp02HomePortV128);
                dialog.QueueHomeCommandV126(normalized);
                dialog.ShowDialog(this);
            }
            statusText.Text = "TP02 conectado em " + tp02HomePortV128 + " / " + tp02HomeStateV128;
        }

'@
$cmdAbsStart = $shell.IndexOf($cmdStart, [System.StringComparison]::Ordinal)
$cmdAbsEnd = $shell.IndexOf($cmdEnd, $cmdAbsStart, [System.StringComparison]::Ordinal)
if ($cmdAbsStart -lt 0 -or $cmdAbsEnd -lt 0) { throw 'Bloco comando home nao encontrado.' }
$shell = $shell.Substring(0, $cmdAbsStart) + $cmdReplacement + $shell.Substring($cmdAbsEnd)

# Ao selecionar outro controlador, a conexao anterior deixa de valer.
$deviceNeedle = @'
            RefreshProfileUi();
            statusText.Text = currentProfile == null ? "Nenhum controlador selecionado" : "Controlador ativo: " + currentProfile.Manufacturer + " " + currentProfile.Model;
'@
$deviceReplacement = @'
            RefreshProfileUi();
            ResetTp02HomeConnectionV128(currentProfile == null
                ? "Nenhum controlador selecionado"
                : "Controlador ativo: " + currentProfile.Manufacturer + " " + currentProfile.Model + " / desconectado");
'@
$shell = Replace-Required $shell $deviceNeedle $deviceReplacement 'reset conexao ao trocar PLC'

# API interna da janela PG para validar a conexao sem escrever no PLC e para
# manter a mesma porta escolhida nas operacoes seguintes.
$readAnchor = '        internal void QueueHomeCommandV126(string command)'
$readIndex = $shell.IndexOf($readAnchor, [System.StringComparison]::Ordinal)
if ($readIndex -lt 0) { throw 'QueueHomeCommandV126 nao encontrado para V128.' }
$dialogMethods = @'
        internal bool TryHomeConnectV128(out string state, out string portName,
            out string detail, out string error)
        {
            state = string.Empty;
            portName = string.Empty;
            detail = string.Empty;
            error = string.Empty;

            if (portCombo == null || portCombo.SelectedItem == null)
            {
                error = "Nenhuma porta COM foi selecionada para o TP-232PG.";
                return false;
            }

            portName = portCombo.SelectedItem.ToString();
            SerialPort port = null;
            try
            {
                string acquisition;
                port = AcquireStablePgPortV93(portName, 1, out state, out acquisition);
                if (port == null || !port.IsOpen)
                    throw new IOException("A porta nao permaneceu aberta apos a qualificacao PG.");
                detail = acquisition;
                return string.Equals(state, "STOP", StringComparison.Ordinal)
                    || string.Equals(state, "RUN", StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                ClosePort(port);
            }
        }

        internal void UseHomePortV128(string portName)
        {
            if (portCombo == null || string.IsNullOrEmpty(portName)) return;
            if (!portCombo.Items.Contains(portName)) portCombo.Items.Add(portName);
            portCombo.SelectedItem = portName;
        }

'@
$shell = $shell.Substring(0, $readIndex) + $dialogMethods + $shell.Substring($readIndex)

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 Connect-First V128 aplicado: comandos sempre clicaveis; sem conexao sinalizam e nao transmitem bytes.'
