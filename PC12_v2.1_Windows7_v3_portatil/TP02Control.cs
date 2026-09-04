using System;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Controle operacional do WEG TP02 pelo protocolo ASCII Computer Link.
    /// Operacoes destrutivas de memoria/programa (CLR/WBP/ROM) nao fazem parte desta tela.
    /// </summary>
    internal sealed class TP02ControlForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Danger = Color.FromArgb(183, 54, 54);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);

        private ComboBox portCombo;
        private ComboBox baudCombo;
        private ComboBox parityCombo;
        private ComboBox dataBitsCombo;
        private ComboBox stopBitsCombo;
        private NumericUpDown stationBox;
        private NumericUpDown responseTimeBox;
        private CheckBox doubleColonCheck;
        private CheckBox dtrCheck;
        private CheckBox rtsCheck;
        private TextBox bitAddressBox;
        private ComboBox bitStateCombo;
        private TextBox wordAddressBox;
        private TextBox wordValueBox;
        private Label stateLabel;
        private TextBox logBox;

        public TP02ControlForm()
        {
            Text = "OpenLadder Studio - Controle TP02";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1050, 700);
            Size = new Size(1220, 800);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            LoadConnectionDefaults();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 72;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label title = LabelAt("CONTROLE ONLINE - WEG TP02", 15.0f, FontStyle.Bold, Navy, 22, 12);
            header.Controls.Add(title);
            Label sub = LabelAt("Ler, escrever e comandar RUN/STOP pelo protocolo TP02 ASCII", 8.8f, FontStyle.Regular, TextSecondary, 24, 43);
            header.Controls.Add(sub);

            stateLabel = new Label();
            stateLabel.Text = "●  NÃO TESTADO";
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 250;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            stateLabel.ForeColor = TextSecondary;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 142;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Conexão serial", 11.0f, FontStyle.Bold, TextPrimary, 18, 12));
            AddFieldLabel(config, "Porta", 18, 43);
            portCombo = ComboAt(18, 64, 100);
            config.Controls.Add(portCombo);
            Button refresh = ButtonAt("ATUALIZAR", 126, 62, 96, false);
            refresh.Click += delegate { RefreshPorts(); };
            config.Controls.Add(refresh);

            AddFieldLabel(config, "Baud", 238, 43);
            baudCombo = ComboAt(238, 64, 92);
            baudCombo.Items.AddRange(new object[] { "38400", "19200", "9600", "4800", "2400", "1200", "600", "300" });
            config.Controls.Add(baudCombo);

            AddFieldLabel(config, "Paridade", 344, 43);
            parityCombo = ComboAt(344, 64, 92);
            parityCombo.Items.AddRange(new object[] { "Even", "Odd", "None" });
            config.Controls.Add(parityCombo);

            AddFieldLabel(config, "Bits", 450, 43);
            dataBitsCombo = ComboAt(450, 64, 64);
            dataBitsCombo.Items.AddRange(new object[] { "7", "8" });
            config.Controls.Add(dataBitsCombo);

            AddFieldLabel(config, "Stop", 528, 43);
            stopBitsCombo = ComboAt(528, 64, 64);
            stopBitsCombo.Items.AddRange(new object[] { "2", "1" });
            config.Controls.Add(stopBitsCombo);

            AddFieldLabel(config, "Estação", 606, 43);
            stationBox = NumericAt(606, 64, 66, 1, 99, 1);
            config.Controls.Add(stationBox);

            AddFieldLabel(config, "Resposta", 686, 43);
            responseTimeBox = NumericAt(686, 64, 66, 0, 15, 5);
            config.Controls.Add(responseTimeBox);

            doubleColonCheck = CheckAt("Prefixo ::", 774, 64, false);
            dtrCheck = CheckAt("DTR", 774, 88, true);
            rtsCheck = CheckAt("RTS", 834, 88, true);
            config.Controls.Add(doubleColonCheck);
            config.Controls.Add(dtrCheck);
            config.Controls.Add(rtsCheck);

            Button test = ButtonAt("TESTAR / LER STATUS", 925, 58, 200, true);
            test.Click += delegate { ReadStatus(); };
            config.Controls.Add(test);

            Label note = LabelAt("Padrão TP02: 19200, 7E2, estação 01. Se não responder, use Monitor online > VARRER PARÂMETROS.", 8.3f, FontStyle.Regular, TextSecondary, 18, 108);
            config.Controls.Add(note);

            Panel operations = new Panel();
            operations.Dock = DockStyle.Top;
            operations.Height = 228;
            operations.BackColor = Canvas;
            Controls.Add(operations);

            operations.Controls.Add(LabelAt("Estado do PLC", 10.0f, FontStyle.Bold, TextPrimary, 18, 14));
            Button stop = ButtonAt("■  STOP", 18, 38, 126, false);
            stop.ForeColor = Danger;
            stop.Click += delegate { ChangeRunState("STP"); };
            operations.Controls.Add(stop);
            Button run = ButtonAt("▶  RUN", 154, 38, 126, false);
            run.ForeColor = Success;
            run.Click += delegate { ChangeRunState("RUN"); };
            operations.Controls.Add(run);

            operations.Controls.Add(LabelAt("Bobina / relé", 10.0f, FontStyle.Bold, TextPrimary, 316, 14));
            bitAddressBox = TextAt("Y0001", 316, 40, 96);
            operations.Controls.Add(bitAddressBox);
            bitStateCombo = ComboAt(420, 40, 80);
            bitStateCombo.Items.AddRange(new object[] { "ON", "OFF" });
            bitStateCombo.SelectedIndex = 0;
            operations.Controls.Add(bitStateCombo);
            Button readBit = ButtonAt("LER MCR", 508, 37, 112, false);
            readBit.Click += delegate { ReadBit(); };
            operations.Controls.Add(readBit);
            Button writeBit = ButtonAt("ESCREVER SCS", 628, 37, 132, false);
            writeBit.Click += delegate { WriteBit(); };
            operations.Controls.Add(writeBit);

            operations.Controls.Add(LabelAt("Registrador", 10.0f, FontStyle.Bold, TextPrimary, 18, 103));
            wordAddressBox = TextAt("D0001", 18, 129, 100);
            operations.Controls.Add(wordAddressBox);
            wordValueBox = TextAt("0000", 126, 129, 92);
            wordValueBox.CharacterCasing = CharacterCasing.Upper;
            operations.Controls.Add(wordValueBox);
            Label hex = LabelAt("valor HEX", 8.0f, FontStyle.Regular, TextSecondary, 128, 157);
            operations.Controls.Add(hex);
            Button readWord = ButtonAt("LER MRV", 230, 126, 112, false);
            readWord.Click += delegate { ReadWord(); };
            operations.Controls.Add(readWord);
            Button writeWord = ButtonAt("ESCREVER WRV", 350, 126, 132, false);
            writeWord.Click += delegate { WriteWord(); };
            operations.Controls.Add(writeWord);

            Label safety = new Label();
            safety.Text = "ATENÇÃO: SCS, WRV, RUN e STOP alteram o PLC real. Confirme que a máquina está em condição segura.\r\nCLR, WBP, ROM e comandos de apagamento/download permanecem desabilitados nesta tela.";
            safety.AutoSize = false;
            safety.Location = new Point(520, 110);
            safety.Size = new Size(650, 76);
            safety.ForeColor = Danger;
            safety.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            operations.Controls.Add(safety);

            Button clear = ButtonAt("LIMPAR LOG", 18, 188, 120, false);
            clear.Click += delegate { logBox.Clear(); };
            operations.Controls.Add(clear);

            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.ReadOnly = true;
            logBox.WordWrap = false;
            logBox.Font = new Font("Consolas", 9.2f);
            logBox.BackColor = Color.FromArgb(20, 28, 36);
            logBox.ForeColor = Color.FromArgb(218, 232, 245);
            Controls.Add(logBox);
            DockOrder.Apply(this, logBox, operations, config, header);
        }

        private void LoadConnectionDefaults()
        {
            RefreshPorts();
            PlcDeviceProfile profile = PlcProfileStore.Load();
            PlcConnectionSettings s = PlcConnectionSettingsStore.Load(profile);
            SelectOrAdd(portCombo, s.PortName);
            SelectOrAdd(baudCombo, s.BaudRate.ToString(CultureInfo.InvariantCulture));
            SelectOrAdd(dataBitsCombo, s.DataBits.ToString(CultureInfo.InvariantCulture));
            SelectOrAdd(parityCombo, s.Parity);
            SelectOrAdd(stopBitsCombo, s.StopBits.ToString(CultureInfo.InvariantCulture));
            if (s.UnitId >= 1 && s.UnitId <= 99) stationBox.Value = s.UnitId;
            responseTimeBox.Value = 5;
        }

        private void SaveConnectionDefaults()
        {
            try
            {
                PlcDeviceProfile profile = PlcProfileStore.Load();
                PlcConnectionSettings s = PlcConnectionSettingsStore.Load(profile);
                if (portCombo.SelectedItem != null) s.PortName = portCombo.SelectedItem.ToString();
                s.BaudRate = ParseSelectedInt(baudCombo, 19200);
                s.DataBits = ParseSelectedInt(dataBitsCombo, 7);
                s.Parity = parityCombo.SelectedItem == null ? "Even" : parityCombo.SelectedItem.ToString();
                s.StopBits = ParseSelectedInt(stopBitsCombo, 2);
                s.UnitId = (int)stationBox.Value;
                s.TimeoutMs = 2500;
                PlcConnectionSettingsStore.Save(profile, s);
            }
            catch { }
        }

        private void ReadStatus()
        {
            string response;
            ExecuteCommand("PSR", string.Empty, out response);
        }

        private void ReadBit()
        {
            string address = NormalizeBitAddress(bitAddressBox.Text, true);
            if (address == null)
            {
                Warn("Endereço inválido. Use X, Y, C ou SC, por exemplo Y0001 ou C0001.");
                return;
            }
            bitAddressBox.Text = address;
            string response;
            ExecuteCommand("MCR", address, out response);
        }

        private void WriteBit()
        {
            string address = NormalizeBitAddress(bitAddressBox.Text, false);
            if (address == null)
            {
                Warn("Endereço de escrita inválido. SCS aceita Y, C ou SC; entradas X não podem ser escritas.");
                return;
            }
            bitAddressBox.Text = address;
            string value = bitStateCombo.SelectedIndex == 1 ? "0" : "1";
            string state = value == "1" ? "ON" : "OFF";
            if (!ConfirmDanger("ESCREVER BOBINA", "Endereço: " + address + "\r\nNovo estado: " + state)) return;
            string response;
            if (ExecuteCommand("SCS", address + value, out response))
            {
                Thread.Sleep(80);
                ExecuteCommand("MCR", address, out response);
            }
        }

        private void ReadWord()
        {
            string address = NormalizeWordAddress(wordAddressBox.Text);
            if (address == null)
            {
                Warn("Endereço inválido. Use V, D, WS, WC ou F, por exemplo D0001.");
                return;
            }
            wordAddressBox.Text = address;
            string response;
            ExecuteCommand("MRV", address + "01", out response);
        }

        private void WriteWord()
        {
            string address = NormalizeWordAddress(wordAddressBox.Text);
            if (address == null)
            {
                Warn("Endereço inválido. Use V, D, WS, WC ou F, por exemplo D0001.");
                return;
            }
            int value;
            string text = (wordValueBox.Text ?? string.Empty).Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) || value < 0 || value > 65535)
            {
                Warn("Valor inválido. Informe uma palavra hexadecimal entre 0000 e FFFF.");
                return;
            }
            string hex = value.ToString("X4", CultureInfo.InvariantCulture);
            wordAddressBox.Text = address;
            wordValueBox.Text = hex;
            if (!ConfirmDanger("ESCREVER REGISTRADOR", "Endereço: " + address + "\r\nNovo valor: 0x" + hex + " (" + value.ToString(CultureInfo.InvariantCulture) + ")")) return;
            string response;
            if (ExecuteCommand("WRV", address + "01" + hex, out response))
            {
                Thread.Sleep(80);
                ExecuteCommand("MRV", address + "01", out response);
            }
        }

        private void ChangeRunState(string command)
        {
            string before;
            ExecuteCommand("PSR", string.Empty, out before);
            string action = command == "RUN" ? "COLOCAR O PLC EM RUN" : "COLOCAR O PLC EM STOP";
            string consequence = command == "RUN"
                ? "O programa do PLC começará a executar e poderá energizar saídas."
                : "A execução do programa será interrompida.";
            if (!ConfirmDanger(action, consequence)) return;

            string response;
            if (ExecuteCommand(command, string.Empty, out response))
            {
                Thread.Sleep(120);
                ExecuteCommand("PSR", string.Empty, out response);
            }
        }

        private bool ExecuteCommand(string command, string payload, out string response)
        {
            response = string.Empty;
            if (portCombo.SelectedItem == null)
            {
                Warn("Nenhuma porta COM selecionada.");
                return false;
            }

            SaveConnectionDefaults();
            string frame = BuildFrame(command, payload);
            SerialPort port = null;
            try
            {
                port = new SerialPort(portCombo.SelectedItem.ToString());
                port.BaudRate = ParseSelectedInt(baudCombo, 19200);
                port.DataBits = ParseSelectedInt(dataBitsCombo, 7);
                port.Parity = ParseParity();
                port.StopBits = ParseSelectedInt(stopBitsCombo, 2) == 2 ? StopBits.Two : StopBits.One;
                port.Encoding = Encoding.ASCII;
                port.ReadTimeout = 2500;
                port.WriteTimeout = 1500;
                port.NewLine = "\r";
                port.DtrEnable = dtrCheck.Checked;
                port.RtsEnable = rtsCheck.Checked;
                port.Handshake = Handshake.None;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                Log("PORTA", DescribePort(port));
                Log("TX", EscapeFrame(frame) + "   " + ToHex(frame));
                port.Write(frame);

                bool complete;
                response = ReadUntilCarriageReturn(port, 2500, out complete);
                if (!complete)
                {
                    if (response.Length > 0) Log("ERRO", "Resposta incompleta: " + EscapeFrame(response));
                    else Log("ERRO", "Timeout: nenhum byte recebido. Confirme COM, estação, cabo/conversor e parâmetros seriais.");
                    SetState("●  SEM RESPOSTA", Danger);
                    return false;
                }

                Log("RX", EscapeFrame(response) + "   " + ToHex(response));
                string detail;
                bool ok = DecodeResponse(command, response, out detail);
                Log(ok ? "OK" : "ERRO", detail);
                if (command == "PSR" && ok) ApplyStatusFromResponse(response);
                else if (ok && stateLabel.Text.IndexOf("NÃO TESTADO", StringComparison.OrdinalIgnoreCase) >= 0) SetState("●  COMUNICANDO", Success);
                return ok;
            }
            catch (Exception ex)
            {
                Log("ERRO", ex.Message);
                SetState("●  FALHA DE COMUNICAÇÃO", Danger);
                return false;
            }
            finally
            {
                if (port != null)
                {
                    try { if (port.IsOpen) port.Close(); } catch { }
                    port.Dispose();
                }
            }
        }

        private string BuildFrame(string command, string payload)
        {
            string station = ((int)stationBox.Value).ToString("00", CultureInfo.InvariantCulture);
            const string responseCodes = "0123456789ABCDEF";
            char responseCode = responseCodes[(int)responseTimeBox.Value];
            string core = station + "?" + responseCode + command + payload;
            string prefix = doubleColonCheck.Checked ? "::" : ":";
            return prefix + core + Checksum(core) + "\r";
        }

        private static string Checksum(string core)
        {
            int sum = 0;
            for (int i = 0; i < core.Length; i++) sum = (sum + (byte)core[i]) & 0xFF;
            return (((~sum) + 1) & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static bool DecodeResponse(string command, string response, out string detail)
        {
            string clean = (response ?? string.Empty).TrimEnd('\r', '\n');
            while (clean.StartsWith(":")) clean = clean.Substring(1);
            if (clean.Length < 6)
            {
                detail = "Resposta curta demais.";
                return false;
            }

            bool checksumOk = VerifyChecksum(clean);
            int error = clean.IndexOf('%');
            if (error >= 0)
            {
                string code = clean.Length >= 4 ? clean.Substring(clean.Length - 4, 2) : string.Empty;
                detail = "TP02 retornou erro" + (code.Length == 2 ? " " + code + " (" + ErrorText(code) + ")" : string.Empty)
                    + ". Checksum " + (checksumOk ? "OK" : "não validado") + ".";
                return false;
            }

            int marker = clean.IndexOf('#');
            if (marker < 0)
            {
                detail = "Resposta sem marcador #. Checksum " + (checksumOk ? "OK" : "não validado") + ".";
                return false;
            }

            int cmd = clean.IndexOf(command, marker, StringComparison.OrdinalIgnoreCase);
            if (cmd < 0)
            {
                detail = "Resposta normal recebida, mas o eco de " + command + " não foi localizado.";
                return false;
            }

            int dataStart = cmd + command.Length;
            int dataLength = clean.Length - dataStart - 2;
            if (dataLength < 0) dataLength = 0;
            string data = clean.Substring(dataStart, dataLength);

            if (command == "PSR")
            {
                string state = data.Length > 0 ? data.Substring(0, 1) : "?";
                string meaning = state == "0" ? "STOP/PROGRAM" : state == "1" ? "RUN" : state == "2" ? "ERROR" : "desconhecido";
                detail = "PLC = " + meaning + ". Checksum " + (checksumOk ? "OK" : "não validado") + ".";
                return checksumOk;
            }
            if (command == "MCR")
            {
                string state = data.Length > 0 ? data.Substring(0, 1) : "?";
                detail = "Bobina = " + (state == "1" ? "ON" : state == "0" ? "OFF" : state) + ". Checksum " + (checksumOk ? "OK" : "não validado") + ".";
                return checksumOk;
            }
            if (command == "MRV")
            {
                if (data.Length >= 4)
                {
                    int value;
                    string hex = data.Substring(0, 4);
                    if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                        detail = "Registrador = 0x" + hex + " (" + value.ToString(CultureInfo.InvariantCulture) + "). Checksum " + (checksumOk ? "OK" : "não validado") + ".";
                    else detail = "Dados = " + data + ".";
                }
                else detail = "Dados = " + data + ".";
                return checksumOk;
            }

            detail = command + " confirmado pelo TP02. Checksum " + (checksumOk ? "OK" : "não validado") + ".";
            return checksumOk;
        }

        private void ApplyStatusFromResponse(string response)
        {
            string clean = (response ?? string.Empty).TrimEnd('\r', '\n');
            while (clean.StartsWith(":")) clean = clean.Substring(1);
            int marker = clean.IndexOf('#');
            int cmd = marker < 0 ? -1 : clean.IndexOf("PSR", marker, StringComparison.OrdinalIgnoreCase);
            if (cmd < 0 || cmd + 3 >= clean.Length) return;
            char state = clean[cmd + 3];
            if (state == '1') SetState("●  RUN", Success);
            else if (state == '0') SetState("●  STOP / PROGRAM", Color.FromArgb(190, 120, 20));
            else if (state == '2') SetState("●  ERRO NO PLC", Danger);
            else SetState("●  COMUNICANDO", Success);
        }

        private static bool VerifyChecksum(string clean)
        {
            if (clean.Length < 3) return false;
            int checksum;
            if (!int.TryParse(clean.Substring(clean.Length - 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out checksum)) return false;
            int sum = 0;
            for (int i = 0; i < clean.Length - 2; i++) sum = (sum + (byte)clean[i]) & 0xFF;
            return ((sum + checksum) & 0xFF) == 0;
        }

        private static string ErrorText(string code)
        {
            if (code == "01") return "erro de quadro";
            if (code == "02") return "operação bloqueada em RUN";
            if (code == "03") return "checksum incorreto";
            if (code == "04") return "endereço/intervalo fora da faixa";
            if (code == "05") return "falha de EEPROM";
            if (code == "06") return "senha ativa";
            return "erro não identificado";
        }

        private bool ConfirmDanger(string action, string detail)
        {
            string message = action + "\r\n\r\n" + detail
                + "\r\n\r\nEste comando será enviado ao PLC físico e pode alterar saídas ou o estado da máquina."
                + "\r\nConfirme que a instalação está em condição segura.\r\n\r\nDeseja continuar?";
            return MessageBox.Show(this, message, "Confirmar comando TP02", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void RefreshPorts()
        {
            string previous = portCombo == null || portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            portCombo.Items.Clear();
            portCombo.Items.AddRange(ports);
            if (previous.Length > 0 && portCombo.Items.IndexOf(previous) < 0) portCombo.Items.Add(previous);
            if (portCombo.Items.Count > 0) portCombo.SelectedItem = previous.Length > 0 ? (object)previous : portCombo.Items[0];
        }

        private static string NormalizeBitAddress(string value, bool allowInput)
        {
            string v = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
            string prefix;
            string digits;
            int width;
            int max;
            if (v.StartsWith("SC")) { prefix = "SC"; digits = v.Substring(2); width = 3; max = 128; }
            else if (v.StartsWith("X")) { if (!allowInput) return null; prefix = "X"; digits = v.Substring(1); width = 4; max = 384; }
            else if (v.StartsWith("Y")) { prefix = "Y"; digits = v.Substring(1); width = 4; max = 384; }
            else if (v.StartsWith("C")) { prefix = "C"; digits = v.Substring(1); width = 4; max = 2048; }
            else return null;
            int n;
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 0 || n > max) return null;
            return prefix + n.ToString(new string('0', width), CultureInfo.InvariantCulture);
        }

        private static string NormalizeWordAddress(string value)
        {
            string v = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
            string prefix;
            string digits;
            int width;
            int max;
            if (v.StartsWith("WS")) { prefix = "WS"; digits = v.Substring(2); width = 3; max = 128; }
            else if (v.StartsWith("WC")) { prefix = "WC"; digits = v.Substring(2); width = 3; max = 912; }
            else if (v.StartsWith("V")) { prefix = "V"; digits = v.Substring(1); width = 4; max = 1024; }
            else if (v.StartsWith("D")) { prefix = "D"; digits = v.Substring(1); width = 4; max = 2048; }
            else if (v.StartsWith("F")) { prefix = "F"; digits = v.Substring(1); width = 4; max = 130; }
            else return null;
            int n;
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 0 || n > max) return null;
            return prefix + n.ToString(new string('0', width), CultureInfo.InvariantCulture);
        }

        private static string ReadUntilCarriageReturn(SerialPort port, int timeoutMs, out bool complete)
        {
            StringBuilder received = new StringBuilder();
            complete = false;
            port.ReadTimeout = 150;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int value;
                try { value = port.ReadByte(); }
                catch (TimeoutException) { continue; }
                if (value < 0) continue;
                received.Append((char)value);
                if (value == 13) { complete = true; break; }
            }
            return received.ToString();
        }

        private void SetState(string text, Color color)
        {
            stateLabel.Text = text;
            stateLabel.ForeColor = color;
        }

        private void Log(string kind, string message)
        {
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + kind + "  " + message + Environment.NewLine);
        }

        private void Warn(string text)
        {
            MessageBox.Show(this, text, "OpenLadder Studio - TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string DescribePort(SerialPort port)
        {
            string p = port.Parity == Parity.Even ? "E" : port.Parity == Parity.Odd ? "O" : "N";
            return port.PortName + "  " + port.BaudRate.ToString(CultureInfo.InvariantCulture) + " "
                + port.DataBits.ToString(CultureInfo.InvariantCulture) + p + (port.StopBits == StopBits.Two ? "2" : "1")
                + "  DTR=" + (port.DtrEnable ? "on" : "off") + "  RTS=" + (port.RtsEnable ? "on" : "off");
        }

        private static string EscapeFrame(string frame)
        {
            return frame.Replace("\r", "<CR>").Replace("\n", "<LF>");
        }

        private static string ToHex(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return "[" + sb.ToString() + "]";
        }

        private Parity ParseParity()
        {
            string p = parityCombo.SelectedItem == null ? "Even" : parityCombo.SelectedItem.ToString();
            if (p == "Odd") return Parity.Odd;
            if (p == "None") return Parity.None;
            return Parity.Even;
        }

        private static int ParseSelectedInt(ComboBox combo, int fallback)
        {
            int value;
            return combo != null && combo.SelectedItem != null
                && int.TryParse(combo.SelectedItem.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value : fallback;
        }

        private static void SelectOrAdd(ComboBox combo, string value)
        {
            if (combo == null || string.IsNullOrEmpty(value)) return;
            if (combo.Items.IndexOf(value) < 0) combo.Items.Add(value);
            combo.SelectedItem = value;
        }

        private Button ButtonAt(string text, int left, int top, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(194, 205, 216);
            b.BackColor = primary ? Accent : Color.White;
            b.ForeColor = primary ? Color.White : Navy;
            b.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            if (primary) b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private Label LabelAt(string text, float size, FontStyle style, Color color, int left, int top)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.Location = new Point(left, top);
            return l;
        }

        private void AddFieldLabel(Control parent, string text, int left, int top)
        {
            parent.Controls.Add(LabelAt(text, 8.1f, FontStyle.Bold, TextSecondary, left, top));
        }

        private ComboBox ComboAt(int left, int top, int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(left, top);
            c.Size = new Size(width, 25);
            return c;
        }

        private NumericUpDown NumericAt(int left, int top, int width, int min, int max, int value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(left, top);
            n.Size = new Size(width, 25);
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            return n;
        }

        private CheckBox CheckAt(string text, int left, int top, bool value)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Checked = value;
            c.Location = new Point(left, top);
            c.ForeColor = TextSecondary;
            return c;
        }

        private TextBox TextAt(string text, int left, int top, int width)
        {
            TextBox box = new TextBox();
            box.Text = text;
            box.CharacterCasing = CharacterCasing.Upper;
            box.Font = new Font("Consolas", 10.0f, FontStyle.Bold);
            box.Location = new Point(left, top);
            box.Size = new Size(width, 25);
            return box;
        }
    }
}
