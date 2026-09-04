$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'ModbusMonitorV15.build.cs'
$outputPath = Join-Path (Get-Location) 'ModbusMonitorV17.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$text = $text.Replace('internal static class ModbusMonitorProgramV15', 'internal static class ModbusMonitorProgramV17')

$fieldNeedle = '        private Panel tcpPanel;'
$fieldInsert = @'
        private Panel tcpPanel;
        private Timer onlineTimer;
        private ComboBox intervalCombo;
        private Button onlineButton;
        private Label onlineStateLabel;
        private bool onlineEnabled;
        private bool readInProgress;
        private int onlineCycle;
'@
if (-not $text.Contains($fieldNeedle)) { throw 'Campo tcpPanel não encontrado no monitor v0.15.' }
$text = $text.Replace($fieldNeedle, $fieldInsert.TrimEnd())

$ctorNeedle = @'
            LoadConnectionSettings();
            LoadMemoryMap(false);
        }
'@
$ctorInsert = @'
            LoadConnectionSettings();
            LoadMemoryMap(false);
            InitializeOnlineMonitor();
        }
'@
if (-not $text.Contains($ctorNeedle.Trim())) { throw 'Construtor do monitor não encontrado.' }
$text = $text.Replace($ctorNeedle.Trim(), $ctorInsert.Trim())

$uiNeedle = @'
            Button save = ActionButton("SALVAR PERFIL", 228, y, 114, PanelColor);
            save.FlatAppearance.BorderColor = Border;
            save.Click += delegate { SaveConnectionSettings(true); };
            left.Controls.Add(save);
            y += 50;

            statusLabel = NewLabel("Pronto", 8.7f, false, Muted);
'@
$uiInsert = @'
            Button save = ActionButton("SALVAR PERFIL", 228, y, 114, PanelColor);
            save.FlatAppearance.BorderColor = Border;
            save.Click += delegate { SaveConnectionSettings(true); };
            left.Controls.Add(save);
            y += 50;

            AddDivider(left, y); y += 18;
            AddCaption(left, "MONITORAMENTO ONLINE", y); y += 24;

            AddSmallLabel(left, "Intervalo de atualização", 16, y);
            intervalCombo = NewCombo(left, 16, y + 18, 146);
            intervalCombo.Items.AddRange(new object[] { "250 ms", "500 ms", "1 s", "2 s", "5 s" });
            intervalCombo.SelectedIndex = 2;
            intervalCombo.SelectedIndexChanged += delegate { UpdateOnlineInterval(); };

            onlineButton = ActionButton("INICIAR ONLINE", 170, y + 18, 172, PanelColor);
            onlineButton.FlatAppearance.BorderColor = Border;
            onlineButton.Click += ToggleOnlineMonitor;
            left.Controls.Add(onlineButton);
            y += 62;

            onlineStateLabel = NewLabel("Offline • atualização manual", 8.0f, false, Muted);
            onlineStateLabel.Location = new Point(16, y);
            onlineStateLabel.MaximumSize = new Size(326, 36);
            left.Controls.Add(onlineStateLabel);
            y += 38;

            statusLabel = NewLabel("Pronto", 8.7f, false, Muted);
'@
if (-not $text.Contains($uiNeedle.Trim())) { throw 'Bloco de botões do monitor não encontrado.' }
$text = $text.Replace($uiNeedle.Trim(), $uiInsert.Trim())

$loadNeedle = '            quantityBox.Value = Clamp(s.Quantity, (int)quantityBox.Minimum, (int)quantityBox.Maximum);'
$loadInsert = @'
            quantityBox.Value = Clamp(s.Quantity, (int)quantityBox.Minimum, (int)quantityBox.Maximum);
            SelectMonitorInterval(s.MonitorIntervalMs);
'@
if (-not $text.Contains($loadNeedle)) { throw 'Carga de quantidade não encontrada.' }
$text = $text.Replace($loadNeedle, $loadInsert.TrimEnd())

$saveNeedle = '            s.Quantity = (int)quantityBox.Value;'
$saveInsert = @'
            s.Quantity = (int)quantityBox.Value;
            s.MonitorIntervalMs = SelectedMonitorInterval();
'@
if (-not $text.Contains($saveNeedle)) { throw 'Salvamento de quantidade não encontrado.' }
$text = $text.Replace($saveNeedle, $saveInsert.TrimEnd())

$readNeedle = @'
        private void ReadDevice(object sender, EventArgs e)
        {
            resultGrid.Rows.Clear();
'@
$readInsert = @'
        private void ReadDevice(object sender, EventArgs e)
        {
            if (readInProgress) return;
            readInProgress = true;
            resultGrid.Rows.Clear();
'@
if (-not $text.Contains($readNeedle.Trim())) { throw 'ReadDevice não encontrado.' }
$text = $text.Replace($readNeedle.Trim(), $readInsert.Trim())

$finallyNeedle = @'
            finally
            {
                Cursor = Cursors.Default;
            }
        }
'@
$finallyInsert = @'
            finally
            {
                Cursor = Cursors.Default;
                readInProgress = false;
            }
        }
'@
if (-not $text.Contains($finallyNeedle.Trim())) { throw 'Finally de ReadDevice não encontrado.' }
$text = $text.Replace($finallyNeedle.Trim(), $finallyInsert.Trim())

$editNeedle = @'
        private void EditMemoryMap(object sender, EventArgs e)
        {
            using (PlcMemoryMapManagerForm dialog = new PlcMemoryMapManagerForm())
'@
$editInsert = @'
        private void EditMemoryMap(object sender, EventArgs e)
        {
            StopOnlineMonitor(false);
            using (PlcMemoryMapManagerForm dialog = new PlcMemoryMapManagerForm())
'@
if ($text.Contains($editNeedle.Trim())) {
    $text = $text.Replace($editNeedle.Trim(), $editInsert.Trim())
}

$methodNeedle = '        private void UpdateTransportUi()'
$methods = @'
        private void InitializeOnlineMonitor()
        {
            onlineTimer = new Timer();
            onlineTimer.Tick += OnlineTimerTick;
            UpdateOnlineInterval();
            FormClosing += delegate { StopOnlineMonitor(false); };
            UpdateOnlineUi();
        }

        private int SelectedMonitorInterval()
        {
            if (intervalCombo == null) return 1000;
            if (intervalCombo.SelectedIndex == 0) return 250;
            if (intervalCombo.SelectedIndex == 1) return 500;
            if (intervalCombo.SelectedIndex == 3) return 2000;
            if (intervalCombo.SelectedIndex == 4) return 5000;
            return 1000;
        }

        private void SelectMonitorInterval(int milliseconds)
        {
            if (intervalCombo == null) return;
            if (milliseconds <= 250) intervalCombo.SelectedIndex = 0;
            else if (milliseconds <= 500) intervalCombo.SelectedIndex = 1;
            else if (milliseconds <= 1000) intervalCombo.SelectedIndex = 2;
            else if (milliseconds <= 2000) intervalCombo.SelectedIndex = 3;
            else intervalCombo.SelectedIndex = 4;
        }

        private void UpdateOnlineInterval()
        {
            if (onlineTimer != null) onlineTimer.Interval = SelectedMonitorInterval();
            if (onlineEnabled && onlineStateLabel != null)
                onlineStateLabel.Text = "Online • intervalo " + SelectedMonitorInterval().ToString() + " ms • ciclo " + onlineCycle.ToString();
        }

        private void ToggleOnlineMonitor(object sender, EventArgs e)
        {
            if (onlineEnabled)
            {
                StopOnlineMonitor(true);
                return;
            }

            SaveConnectionSettings(false);
            onlineEnabled = true;
            onlineCycle = 0;
            UpdateOnlineInterval();
            onlineTimer.Start();
            UpdateOnlineUi();
            statusLabel.Text = "Monitoramento online iniciado. Leituras são somente de consulta.";
            statusLabel.ForeColor = Accent;
            OnlineTimerTick(null, EventArgs.Empty);
        }

        private void StopOnlineMonitor(bool notify)
        {
            onlineEnabled = false;
            if (onlineTimer != null) onlineTimer.Stop();
            UpdateOnlineUi();
            if (notify && statusLabel != null)
            {
                statusLabel.Text = "Monitoramento online interrompido.";
                statusLabel.ForeColor = Muted;
            }
        }

        private void UpdateOnlineUi()
        {
            if (onlineButton != null)
            {
                onlineButton.Text = onlineEnabled ? "PARAR ONLINE" : "INICIAR ONLINE";
                onlineButton.BackColor = onlineEnabled ? Color.FromArgb(153, 75, 75) : PanelColor;
                onlineButton.FlatAppearance.BorderColor = onlineEnabled ? Color.FromArgb(180, 92, 92) : Border;
            }
            if (onlineStateLabel != null)
            {
                onlineStateLabel.Text = onlineEnabled
                    ? "Online • intervalo " + SelectedMonitorInterval().ToString() + " ms • aguardando leitura"
                    : "Offline • atualização manual";
                onlineStateLabel.ForeColor = onlineEnabled ? Accent : Muted;
            }
        }

        private void OnlineTimerTick(object sender, EventArgs e)
        {
            if (!onlineEnabled || readInProgress) return;
            if (onlineTimer != null) onlineTimer.Stop();
            try
            {
                ReadDevice(null, EventArgs.Empty);
                onlineCycle++;
                if (onlineStateLabel != null)
                {
                    onlineStateLabel.Text = "Online • ciclo " + onlineCycle.ToString() + " • " + DateTime.Now.ToString("HH:mm:ss") + " • " + SelectedMonitorInterval().ToString() + " ms";
                    onlineStateLabel.ForeColor = statusLabel != null && statusLabel.ForeColor == ErrorColor ? ErrorColor : Accent;
                }
            }
            finally
            {
                if (onlineEnabled && onlineTimer != null)
                {
                    UpdateOnlineInterval();
                    onlineTimer.Start();
                }
            }
        }

'@
if (-not $text.Contains($methodNeedle)) { throw 'UpdateTransportUi não encontrado.' }
$text = $text.Replace($methodNeedle, $methods + $methodNeedle)

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
