$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'ModbusMonitorV14.cs'
$outputPath = Join-Path (Get-Location) 'ModbusMonitorV15.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$text = $text.Replace('internal static class ModbusMonitorProgramV14', 'internal static class ModbusMonitorProgramV15')
$text = $text.Replace('Área com " + requested.ToString() + " pontos; a leitura atual foi limitada a " + effective.ToString() + " por requisição Modbus.', 'Área com " + requested.ToString() + " pontos; será lida automaticamente em " + ModbusBulkReader.BlockCount((ModbusFunction)(functionIndex + 1), requested).ToString() + " bloco(s) de até " + effective.ToString() + " ponto(s).')
$text = $text.Replace('Área carregada com limite de " + effective.ToString() + " pontos por requisição.', 'Área extensa carregada: leitura automática em blocos habilitada.')

$newMethod = @'
        private void ReadDevice(object sender, EventArgs e)
        {
            resultGrid.Rows.Clear();
            rawBox.Clear();
            statusLabel.Text = "Lendo...";
            statusLabel.ForeColor = Muted;
            Cursor = Cursors.WaitCursor;

            try
            {
                SaveConnectionSettings(false);
                byte unit = (byte)unitBox.Value;
                ushort address = (ushort)addressBox.Value;
                ModbusFunction function = (ModbusFunction)(functionCombo.SelectedIndex + 1);
                PlcMemoryArea selected = SelectedMemoryArea();
                int totalQuantity = selected == null ? (int)quantityBox.Value : Math.Max(1, selected.Length);

                if ((long)address + (long)totalQuantity > 65536L)
                    throw new InvalidOperationException("A área solicitada ultrapassa o endereço Modbus 65535.");

                int totalBlocks = ModbusBulkReader.BlockCount(function, totalQuantity);
                Action<int, int, int> progress = delegate(int block, int blocks, int completed)
                {
                    statusLabel.Text = "Lendo bloco " + block.ToString() + "/" + blocks.ToString() + " • " + completed.ToString() + "/" + totalQuantity.ToString() + " ponto(s)...";
                    statusLabel.ForeColor = Muted;
                    Application.DoEvents();
                };

                ModbusBulkReadResult bulk;
                if (transportCombo.SelectedIndex == 0)
                {
                    if (portCombo.SelectedItem == null) throw new InvalidOperationException("Nenhuma porta COM selecionada.");
                    ModbusRtuClient client = new ModbusRtuClient();
                    client.PortName = portCombo.SelectedItem.ToString();
                    client.BaudRate = ParseInt(baudCombo.SelectedItem, 9600);
                    client.DataBits = (int)dataBitsBox.Value;
                    client.Parity = ParseParity(parityCombo.SelectedItem == null ? "None" : parityCombo.SelectedItem.ToString());
                    client.StopBits = ParseInt(stopCombo.SelectedItem, 1) == 2 ? StopBits.Two : StopBits.One;
                    client.TimeoutMs = (int)timeoutBox.Value;
                    bulk = ModbusBulkReader.ReadRtu(client, unit, function, address, totalQuantity, progress);
                }
                else
                {
                    if (string.IsNullOrEmpty(hostBox.Text.Trim())) throw new InvalidOperationException("Informe o endereço IP ou host.");
                    ModbusTcpClient client = new ModbusTcpClient();
                    client.Host = hostBox.Text.Trim();
                    client.Port = (int)tcpPortBox.Value;
                    client.TimeoutMs = (int)timeoutBox.Value;
                    bulk = ModbusBulkReader.ReadTcp(client, unit, function, address, totalQuantity, progress);
                }

                rawBox.Text = ModbusBulkReader.FormatRawFrames(bulk.Frames);

                if (bulk.Bits != null && bulk.Bits.Length > 0)
                {
                    for (int i = 0; i < bulk.Bits.Length; i++)
                        resultGrid.Rows.Add(i.ToString(), FormatDisplayAddress(address + i), bulk.Bits[i] ? "ON" : "OFF", bulk.Bits[i] ? "1" : "0");
                }
                else if (bulk.Registers != null)
                {
                    for (int i = 0; i < bulk.Registers.Length; i++)
                    {
                        ushort value = bulk.Registers[i];
                        resultGrid.Rows.Add(i.ToString(), FormatDisplayAddress(address + i), value.ToString(), "0x" + value.ToString("X4"));
                    }
                }

                if (!bulk.Success)
                {
                    statusLabel.Text = "Leitura parcial: " + bulk.CompletedQuantity.ToString() + "/" + totalQuantity.ToString() + " ponto(s). " + bulk.Error;
                    statusLabel.ForeColor = ErrorColor;
                    return;
                }

                string areaText = selected == null ? string.Empty : " • Área: " + selected.Name;
                string blockText = totalBlocks > 1 ? " em " + totalBlocks.ToString() + " blocos" : string.Empty;
                statusLabel.Text = "Leitura concluída: " + totalQuantity.ToString() + " ponto(s)" + blockText + areaText + ". Perfil salvo automaticamente.";
                statusLabel.ForeColor = Accent;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erro: " + ex.Message;
                statusLabel.ForeColor = ErrorColor;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
'@

$pattern = '(?s)        private void ReadDevice\(object sender, EventArgs e\)\s*\{.*?\n        \}\s*\n\s*        private Parity ParseParity'
$replacement = $newMethod.TrimEnd() + "`r`n`r`n        private Parity ParseParity"
$updated = [System.Text.RegularExpressions.Regex]::Replace($text, $pattern, $replacement, 1)
if ($updated -eq $text) { throw 'Não foi possível substituir ReadDevice em ModbusMonitorV14.cs.' }
$text = $updated

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
