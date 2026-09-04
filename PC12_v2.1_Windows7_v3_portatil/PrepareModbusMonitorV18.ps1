$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'ModbusMonitorV17.build.cs'
$outputPath = Join-Path (Get-Location) 'ModbusMonitorV18.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$text = $text.Replace('internal static class ModbusMonitorProgramV17', 'internal static class ModbusMonitorProgramV18')

$fieldNeedle = '        private int onlineCycle;'
$fieldInsert = @'
        private int onlineCycle;
        private ModbusTrendHistory trendHistory;
        private ModbusTrendForm trendForm;
'@
if (-not $text.Contains($fieldNeedle)) { throw 'Campos do monitor online v0.17 não encontrados.' }
$text = $text.Replace($fieldNeedle, $fieldInsert.TrimEnd())

$ctorNeedle = @'
            InitializeOnlineMonitor();
        }
'@
$ctorInsert = @'
            InitializeOnlineMonitor();
            InitializeTrendHistory();
        }
'@
if (-not $text.Contains($ctorNeedle.Trim())) { throw 'Inicialização online não encontrada.' }
$text = $text.Replace($ctorNeedle.Trim(), $ctorInsert.Trim())

$uiNeedle = @'
            onlineStateLabel = NewLabel("Offline • atualização manual", 8.0f, false, Muted);
            onlineStateLabel.Location = new Point(16, y);
            onlineStateLabel.MaximumSize = new Size(326, 36);
            left.Controls.Add(onlineStateLabel);
            y += 38;

            statusLabel = NewLabel("Pronto", 8.7f, false, Muted);
'@
$uiInsert = @'
            onlineStateLabel = NewLabel("Offline • atualização manual", 8.0f, false, Muted);
            onlineStateLabel.Location = new Point(16, y);
            onlineStateLabel.MaximumSize = new Size(326, 36);
            left.Controls.Add(onlineStateLabel);
            y += 38;

            Button trackSignal = ActionButton("RASTREAR LINHA", 16, y, 158, PanelColor);
            trackSignal.FlatAppearance.BorderColor = Border;
            trackSignal.Click += TrackSelectedSignal;
            left.Controls.Add(trackSignal);

            Button trends = ActionButton("GRÁFICOS", 182, y, 160, PanelColor);
            trends.FlatAppearance.BorderColor = Border;
            trends.Click += ShowTrendWindow;
            left.Controls.Add(trends);
            y += 48;

            statusLabel = NewLabel("Pronto", 8.7f, false, Muted);
'@
if (-not $text.Contains($uiNeedle.Trim())) { throw 'Bloco do monitoramento online não encontrado.' }
$text = $text.Replace($uiNeedle.Trim(), $uiInsert.Trim())

$captureNeedle = @'
                if (!bulk.Success)
                {
'@
$captureInsert = @'
                CaptureTrendSamples(function, address, bulk);

                if (!bulk.Success)
                {
'@
if (-not $text.Contains($captureNeedle.Trim())) { throw 'Ponto de captura após a leitura não encontrado.' }
$text = $text.Replace($captureNeedle.Trim(), $captureInsert.Trim())

$methodNeedle = '        private void InitializeOnlineMonitor()'
$methods = @'
        private void InitializeTrendHistory()
        {
            trendHistory = new ModbusTrendHistory();
            FormClosing += delegate
            {
                if (trendForm != null && !trendForm.IsDisposed) trendForm.Close();
            };
        }

        private void TrackSelectedSignal(object sender, EventArgs e)
        {
            if (trendHistory == null) InitializeTrendHistory();
            if (resultGrid == null || resultGrid.CurrentRow == null)
            {
                statusLabel.Text = "Selecione uma linha da tabela de dados antes de rastrear um sinal.";
                statusLabel.ForeColor = WarningColor;
                return;
            }

            object indexValue = resultGrid.CurrentRow.Cells["index"].Value;
            int index;
            if (indexValue == null || !int.TryParse(indexValue.ToString(), out index))
            {
                statusLabel.Text = "Não foi possível identificar o endereço da linha selecionada.";
                statusLabel.ForeColor = WarningColor;
                return;
            }

            ModbusFunction function = (ModbusFunction)(functionCombo.SelectedIndex + 1);
            int absoluteAddress = (int)addressBox.Value + index;
            bool isBit = function == ModbusFunction.ReadCoils || function == ModbusFunction.ReadDiscreteInputs;
            object addressValue = resultGrid.CurrentRow.Cells["address"].Value;
            string displayAddress = addressValue == null ? absoluteAddress.ToString() : addressValue.ToString();
            PlcMemoryArea selectedArea = SelectedMemoryArea();
            string displayName = selectedArea == null ? displayAddress : selectedArea.Name + " • " + displayAddress;

            string message;
            bool ok = trendHistory.Track(function, absoluteAddress, displayName, isBit, out message);
            statusLabel.Text = message;
            statusLabel.ForeColor = ok ? Accent : WarningColor;

            if (trendForm != null && !trendForm.IsDisposed) trendForm.NotifySignalsChanged();
        }

        private void ShowTrendWindow(object sender, EventArgs e)
        {
            if (trendHistory == null) InitializeTrendHistory();
            if (trendForm == null || trendForm.IsDisposed)
            {
                trendForm = new ModbusTrendForm(trendHistory);
                trendForm.FormClosed += delegate { trendForm = null; };
                trendForm.Show(this);
            }
            else
            {
                trendForm.NotifySignalsChanged();
                trendForm.BringToFront();
                trendForm.Activate();
            }
        }

        private void CaptureTrendSamples(ModbusFunction function, int startAddress, ModbusBulkReadResult bulk)
        {
            if (trendHistory == null || bulk == null) return;
            trendHistory.Capture(function, startAddress, bulk.Bits, bulk.Registers, DateTime.Now);
        }

'@
if (-not $text.Contains($methodNeedle)) { throw 'InitializeOnlineMonitor não encontrado.' }
$text = $text.Replace($methodNeedle, $methods + $methodNeedle)

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
