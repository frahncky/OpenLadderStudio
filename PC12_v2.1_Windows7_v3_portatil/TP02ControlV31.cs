using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ModernPC12
{
    internal sealed class TP02ControlV31Form : Form
    {
        private sealed class ProbeProfile
        {
            public string Name;
            public int Baud;
            public int DataBits;
            public Parity Parity;
            public StopBits StopBits;
            public int ResponseCode;
            public bool Dtr;
            public bool Rts;

            public ProbeProfile(string name, int baud, int dataBits, Parity parity, StopBits stopBits, int responseCode, bool dtr, bool rts)
            {
                Name = name;
                Baud = baud;
                DataBits = dataBits;
                Parity = parity;
                StopBits = stopBits;
                ResponseCode = responseCode;
                Dtr = dtr;
                Rts = rts;
            }
        }

        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Danger = Color.FromArgb(183, 54, 54);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 120, 20);
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
        private CheckBox dtrCheck;
        private CheckBox rtsCheck;
        private TextBox bitAddressBox;
        private ComboBox bitStateCombo;
        private TextBox wordAddressBox;
        private TextBox wordValueBox;
        private Label stateLabel;
        private Label modeLabel;
        private TextBox logBox;
        private Button autoButton;
        private Button runButton;
        private Button stopButton;
        private Button writeBitButton;
        private Button writeWordButton;

        private Thread probeThread;
        private volatile bool probeCancel;
        private volatile bool linkConfirmed;

        public TP02ControlV31Form()
        {
            Text = "OpenLadder Studio - Controle TP02";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 720);
            Size = new Size(1260, 830);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            LoadDefaults();
            FormClosing += delegate { probeCancel = true; };
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 78;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label title = LabelAt("CONTROLE ONLINE - WEG TP02", 15.0f, FontStyle.Bold, Navy, 22, 11);
            header.Controls.Add(title);
            Label sub = LabelAt("Computer Link na porta MMI: leitura, escrita e RUN/STOP", 8.8f, FontStyle.Regular, TextSecondary, 24, 42);
            header.Controls.Add(sub);

            modeLabel = new Label();
            modeLabel.Text = "MMI COMPUTER LINK";
            modeLabel.AutoSize = false;
            modeLabel.TextAlign = ContentAlignment.MiddleRight;
            modeLabel.Location = new Point(650, 14);
            modeLabel.Size = new Size(320, 22);
            modeLabel.ForeColor = TextSecondary;
            modeLabel.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            header.Controls.Add(modeLabel);

            stateLabel = new Label();
            stateLabel.Text = "●  NÃO TESTADO";
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 260;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            stateLabel.ForeColor = TextSecondary;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 178;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Conexão serial", 11.0f, FontStyle.Bold, TextPrimary, 18, 10));

            AddFieldLabel(config, "Porta", 18, 40);
            portCombo = ComboAt(18, 60, 100);
            config.Controls.Add(portCombo);
            Button refresh = ButtonAt("ATUALIZAR", 126, 58, 96, false);
            refresh.Click += delegate { RefreshPorts(); };
            config.Controls.Add(refresh);

            AddFieldLabel(config, "Baud", 238, 40);
            baudCombo = ComboAt(238, 60, 92);
            baudCombo.Items.AddRange(new object[] { "38400", "19200", "9600", "4800", "2400", "1200", "600", "300" });
            config.Controls.Add(baudCombo);

            AddFieldLabel(config, "Paridade", 344, 40);
            parityCombo = ComboAt(344, 60, 92);
            parityCombo.Items.AddRange(new object[] { "None", "Even", "Odd" });
            config.Controls.Add(parityCombo);

            AddFieldLabel(config, "Bits", 450, 40);
            dataBitsCombo = ComboAt(450, 60, 64);
            dataBitsCombo.Items.AddRange(new object[] { "7", "8" });
            config.Controls.Add(dataBitsCombo);

            AddFieldLabel(config, "Stop", 528, 40);
            stopBitsCombo = ComboAt(528, 60, 64);
            stopBitsCombo.Items.AddRange(new object[] { "1", "2" });
            config.Controls.Add(stopBitsCombo);

            AddFieldLabel(config, "Estação", 606, 40);
            stationBox = NumericAt(606, 60, 66, 1, 99, 1);
            config.Controls.Add(stationBox);

            AddFieldLabel(config, "Resposta", 686, 40);
            responseTimeBox = NumericAt(686, 60, 66, 0, 15, 4);
            config.Controls.Add(responseTimeBox);

            dtrCheck = CheckAt("DTR", 770, 61, false);
            rtsCheck = CheckAt("RTS", 830, 61, false);
            config.Controls.Add(dtrCheck);
            config.Controls.Add(rtsCheck);

            Button test = ButtonAt("TESTAR CONFIGURAÇÃO", 910, 56, 170, true);
            test.Click += delegate { TestCurrentConfiguration(); };
            config.Controls.Add(test);

            autoButton = ButtonAt("AUTO-DETECTAR TP02", 1090, 56, 150, false);
            autoButton.Click += delegate { ToggleAutoDetect(); };
            config.Controls.Add(autoButton);

            Label p1 = LabelAt("Quadro usado: ::NN?R<comando><checksum><CR>. O prefixo :: é fixado nesta versão.", 8.2f, FontStyle.Regular, TextSecondary, 18, 100);
            config.Controls.Add(p1);

            Label p2 = LabelAt("IMPORTANTE: na porta MMI, Computer Link exige PG/COM baixo: pino 4 ligado ao pino 5. Cabo em modo PG (pino 4 aberto) não responde a estes comandos.", 8.3f, FontStyle.Bold, Danger, 18, 124);
            p2.MaximumSize = new Size(1190, 42);
            config.Controls.Add(p2);

            Panel operations = new Panel();
            operations.Dock = DockStyle.Top;
            operations.Height = 230;
            operations.BackColor = Canvas;
            Controls.Add(operations);

            operations.Controls.Add(LabelAt("Estado do PLC", 10.0f, FontStyle.Bold, TextPrimary, 18, 13));
            stopButton = ButtonAt("■  STOP", 18, 38, 126, false);
            stopButton.ForeColor = Danger;
            stopButton.Click += delegate { ChangeRunState("STP"); };
            operations.Controls.Add(stopButton);
            runButton = ButtonAt("▶  RUN", 154, 38, 126, false);
            runButton.ForeColor = Success;
            runButton.Click += delegate { ChangeRunState("RUN"); };
            operations.Controls.Add(runButton);

            operations.Controls.Add(LabelAt("Bobina / relé", 10.0f, FontStyle.Bold, TextPrimary, 316, 13));
            bitAddressBox = TextAt("Y0001", 316, 40, 96);
            operations.Controls.Add(bitAddressBox);
            bitStateCombo = ComboAt(420, 40, 80);
            bitStateCombo.Items.AddRange(new object[] { "ON", "OFF" });
            bitStateCombo.SelectedIndex = 0;
            operations.Controls.Add(bitStateCombo);
            Button readBit = ButtonAt("LER MCR", 508, 37, 112, false);
            readBit.Click += delegate { ReadBit(); };
            operations.Controls.Add(readBit);
            writeBitButton = ButtonAt("ESCREVER SCS", 628, 37, 132, false);
            writeBitButton.Click += delegate { WriteBit(); };
            operations.Controls.Add(writeBitButton);

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
            writeWordButton = ButtonAt("ESCREVER WRV", 350, 126, 132, false);
            writeWordButton.Click += delegate { WriteWord(); };
            operations.Controls.Add(writeWordButton);

            Label safety = new Label();
            safety.Text = "RUN, STOP, SCS e WRV só são habilitados depois que o TP02 responder validamente ao PSR.\r\nCLR, WBP, ROM e apagamento de memória continuam bloqueados.";
            safety.AutoSize = false;
            safety.Location = new Point(520, 110);
            safety.Size = new Size(700, 74);
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
            SetDangerousEnabled(false);
        }

        private void LoadDefaults()
        {
            RefreshPorts();
            SelectOrAdd(baudCombo, "19200");
            SelectOrAdd(dataBitsCombo, "7");
            SelectOrAdd(parityCombo, "None");
            SelectOrAdd(stopBitsCombo, "1");
            stationBox.Value = 1;
            responseTimeBox.Value = 4;
            dtrCheck.Checked = false;
            rtsCheck.Checked = false;

            try
            {
                PlcDeviceProfile profile = PlcProfileStore.Load();
                PlcConnectionSettings s = PlcConnectionSettingsStore.Load(profile);
                if (!string.IsNullOrEmpty(s.PortName)) SelectOrAdd(portCombo, s.PortName);
            }
            catch { }
        }

        private void SaveDetectedSettings()
        {
            try
            {
                PlcDeviceProfile profile = PlcProfileStore.Load();
                PlcConnectionSettings s = PlcConnectionSettingsStore.Load(profile);
                if (portCombo.SelectedItem != null) s.PortName = portCombo.SelectedItem.ToString();
                s.BaudRate = ParseSelectedInt(baudCombo, 19200);
                s.DataBits = ParseSelectedInt(dataBitsCombo, 7);
                s.Parity = parityCombo.SelectedItem == null ? "None" : parityCombo.SelectedItem.ToString();
                s.StopBits = ParseSelectedInt(stopBitsCombo, 1);
                s.UnitId = (int)stationBox.Value;
                s.TimeoutMs = 2500;
                PlcConnectionSettingsStore.Save(profile, s);
            }
            catch { }
        }

        private void TestCurrentConfiguration()
        {
            if (ProbeBusy()) return;
            string response;
            bool ok = ExecuteCommand("PSR", string.Empty, out response);
            if (!ok && response.Length == 0)
                Log("DIAGNÓSTICO", "Zero bytes recebidos. Se a COM estiver correta, confirme que a porta MMI está em COMPUTER LINK (pinos 4 e 5 ligados), não em PG.");
        }

        private bool ProbeBusy()
        {
            if (probeThread != null && probeThread.IsAlive)
            {
                MessageBox.Show(this, "A auto-detecção está em andamento. Pare-a antes de enviar comandos.", "TP02", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            return false;
        }

        private void ToggleAutoDetect()
        {
            if (probeThread != null && probeThread.IsAlive)
            {
                probeCancel = true;
                Log("AUTO", "Cancelamento solicitado.");
                return;
            }
            if (portCombo.SelectedItem == null)
            {
                Warn("Nenhuma porta COM selecionada.");
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            int preferredStation = (int)stationBox.Value;
            ProbeProfile current = CurrentProbeProfile("Configuração atual");

            probeCancel = false;
            linkConfirmed = false;
            SetDangerousEnabled(false);
            autoButton.Text = "PARAR DETECÇÃO";
            SetState("●  PROCURANDO...", Warning);
            Log("AUTO", "Iniciando detecção em " + portName + ". Primeiro quadro: prefixo :: e comando PSR.");
            Log("AUTO", "A estação é varrida de 01 a 99 porque o manual informa que estação divergente produz silêncio total.");

            probeThread = new Thread(delegate { AutoDetectWorker(portName, preferredStation, current); });
            probeThread.IsBackground = true;
            probeThread.Start();
        }

        private void AutoDetectWorker(string portName, int preferredStation, ProbeProfile current)
        {
            List<ProbeProfile> profiles = new List<ProbeProfile>();
            AddProfileUnique(profiles, new ProbeProfile(current.Name, current.Baud, current.DataBits, current.Parity, current.StopBits, current.ResponseCode, current.Dtr, current.Rts));
            AddProfileUnique(profiles, new ProbeProfile("19200 7N1", 19200, 7, Parity.None, StopBits.One, 4, false, false));
            AddProfileUnique(profiles, new ProbeProfile("19200 7E2", 19200, 7, Parity.Even, StopBits.Two, 5, false, false));
            AddProfileUnique(profiles, new ProbeProfile("19200 8N1", 19200, 8, Parity.None, StopBits.One, 4, false, false));
            AddProfileUnique(profiles, new ProbeProfile("9600 8N1", 9600, 8, Parity.None, StopBits.One, 4, false, false));
            AddProfileUnique(profiles, new ProbeProfile("9600 7E2", 9600, 7, Parity.Even, StopBits.Two, 5, false, false));

            bool anyBytes = false;
            string lastSerialError = string.Empty;

            for (int p = 0; p < profiles.Count && !probeCancel; p++)
            {
                ProbeProfile profile = profiles[p];
                LogSafe("AUTO", "Testando " + profile.Name + ", DTR=" + OnOff(profile.Dtr) + ", RTS=" + OnOff(profile.Rts) + ".");
                string error;
                int station;
                string response;
                bool sawBytes;
                bool ok = ScanStations(portName, profile, preferredStation, out station, out response, out sawBytes, out error);
                if (sawBytes) anyBytes = true;
                if (error.Length > 0) lastSerialError = error;
                if (ok)
                {
                    LogSafe("ACHOU", "TP02 respondeu na estação " + station.ToString("00", CultureInfo.InvariantCulture) + " com " + profile.Name + ".");
                    LogSafe("RX", EscapeFrame(response) + "   " + ToHex(response));
                    ApplyDetectedSafe(profile, station);
                    FinishProbeSafe(true, anyBytes, string.Empty);
                    return;
                }
            }

            if (!probeCancel && !anyBytes)
            {
                ProbeProfile[] powered = new ProbeProfile[]
                {
                    new ProbeProfile("19200 7N1 + DTR/RTS", 19200, 7, Parity.None, StopBits.One, 4, true, true),
                    new ProbeProfile("19200 7E2 + DTR/RTS", 19200, 7, Parity.Even, StopBits.Two, 5, true, true)
                };
                for (int i = 0; i < powered.Length && !probeCancel; i++)
                {
                    string error;
                    int station;
                    string response;
                    bool sawBytes;
                    bool ok = ScanStations(portName, powered[i], preferredStation, out station, out response, out sawBytes, out error);
                    if (sawBytes) anyBytes = true;
                    if (error.Length > 0) lastSerialError = error;
                    if (ok)
                    {
                        LogSafe("ACHOU", "TP02 respondeu na estação " + station.ToString("00", CultureInfo.InvariantCulture) + " com " + powered[i].Name + ".");
                        ApplyDetectedSafe(powered[i], station);
                        FinishProbeSafe(true, anyBytes, string.Empty);
                        return;
                    }
                }
            }

            FinishProbeSafe(false, anyBytes, lastSerialError);
        }

        private static void AddProfileUnique(List<ProbeProfile> list, ProbeProfile candidate)
        {
            for (int i = 0; i < list.Count; i++)
            {
                ProbeProfile x = list[i];
                if (x.Baud == candidate.Baud && x.DataBits == candidate.DataBits && x.Parity == candidate.Parity
                    && x.StopBits == candidate.StopBits && x.ResponseCode == candidate.ResponseCode
                    && x.Dtr == candidate.Dtr && x.Rts == candidate.Rts) return;
            }
            list.Add(candidate);
        }

        private bool ScanStations(string portName, ProbeProfile profile, int preferredStation,
            out int foundStation, out string foundResponse, out bool sawAnyBytes, out string serialError)
        {
            foundStation = 0;
            foundResponse = string.Empty;
            sawAnyBytes = false;
            serialError = string.Empty;
            SerialPort port = null;

            try
            {
                port = new SerialPort(portName);
                port.BaudRate = profile.Baud;
                port.DataBits = profile.DataBits;
                port.Parity = profile.Parity;
                port.StopBits = profile.StopBits;
                port.Encoding = Encoding.ASCII;
                port.WriteTimeout = 800;
                port.DtrEnable = profile.Dtr;
                port.RtsEnable = profile.Rts;
                port.Handshake = Handshake.None;
                port.Open();

                List<int> order = BuildStationOrder(preferredStation);
                for (int i = 0; i < order.Count && !probeCancel; i++)
                {
                    int station = order[i];
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();
                    string frame = BuildFrameFor(station, profile.ResponseCode, "PSR", string.Empty);
                    port.Write(frame);

                    bool complete;
                    string response = ReadUntilCarriageReturn(port, 140, out complete);
                    if (response.Length > 0) sawAnyBytes = true;
                    if (complete && IsValidPsrResponse(response, station))
                    {
                        foundStation = station;
                        foundResponse = response;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                serialError = ex.Message;
                LogSafe("ERRO", profile.Name + ": " + ex.Message);
            }
            finally
            {
                if (port != null)
                {
                    try { if (port.IsOpen) port.Close(); } catch { }
                    port.Dispose();
                }
            }
            return false;
        }

        private static List<int> BuildStationOrder(int preferred)
        {
            List<int> result = new List<int>();
            if (preferred >= 1 && preferred <= 99) result.Add(preferred);
            if (preferred != 1) result.Add(1);
            for (int i = 2; i <= 99; i++)
                if (i != preferred) result.Add(i);
            return result;
        }

        private static bool IsValidPsrResponse(string response, int station)
        {
            string clean = StripPrefixAndTerminator(response);
            if (clean.Length < 8 || clean.IndexOf('%') >= 0) return false;
            string stationText = station.ToString("00", CultureInfo.InvariantCulture);
            if (!clean.StartsWith(stationText, StringComparison.OrdinalIgnoreCase)) return false;
            int marker = clean.IndexOf('#');
            if (marker < 0) return false;
            int cmd = clean.IndexOf("PSR", marker, StringComparison.OrdinalIgnoreCase);
            if (cmd < 0 || cmd + 3 >= clean.Length) return false;
            char state = clean[cmd + 3];
            if (state != '0' && state != '1' && state != '2') return false;
            return VerifyChecksum(clean);
        }

        private void ApplyDetectedSafe(ProbeProfile profile, int station)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { ApplyDetectedSafe(profile, station); }));
                return;
            }

            SelectOrAdd(baudCombo, profile.Baud.ToString(CultureInfo.InvariantCulture));
            SelectOrAdd(dataBitsCombo, profile.DataBits.ToString(CultureInfo.InvariantCulture));
            SelectOrAdd(parityCombo, profile.Parity.ToString());
            SelectOrAdd(stopBitsCombo, profile.StopBits == StopBits.Two ? "2" : "1");
            responseTimeBox.Value = profile.ResponseCode;
            stationBox.Value = station;
            dtrCheck.Checked = profile.Dtr;
            rtsCheck.Checked = profile.Rts;
            linkConfirmed = true;
            SaveDetectedSettings();
            SetDangerousEnabled(true);
            SetState("●  TP02 CONECTADO", Success);
            Log("AUTO", "Parâmetros aplicados. Escrita e RUN/STOP foram habilitados.");
        }

        private void FinishProbeSafe(bool found, bool anyBytes, string serialError)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { FinishProbeSafe(found, anyBytes, serialError); }));
                return;
            }

            autoButton.Text = "AUTO-DETECTAR TP02";
            if (probeCancel)
            {
                SetState("●  DETECÇÃO PARADA", Warning);
                Log("AUTO", "Detecção cancelada.");
                return;
            }
            if (found) return;

            linkConfirmed = false;
            SetDangerousEnabled(false);

            if (serialError.Length > 0 && !anyBytes)
            {
                SetState("●  ERRO NA PORTA", Danger);
                Log("DIAGNÓSTICO", "A porta serial não pôde ser usada corretamente: " + serialError);
                return;
            }

            if (anyBytes)
            {
                SetState("●  SINAL SEM PROTOCOLO", Warning);
                Log("DIAGNÓSTICO", "Chegaram bytes, mas nenhum quadro PSR válido. O elo físico está vivo; ainda há divergência de parâmetros ou enquadramento.");
            }
            else
            {
                SetState("●  ZERO BYTES", Danger);
                Log("DIAGNÓSTICO", "Nenhuma configuração e nenhuma estação 01-99 retornou um único byte.");
                Log("DIAGNÓSTICO", "Com a COM correta, isso é compatível com cabo/conversor inadequado ou MMI em MODO PG.");
                Log("DIAGNÓSTICO", "Para Computer Link na MMI do TP02, o manual exige PG/COM baixo: pino 4 ligado ao pino 5. Cabo de programação PC12 pode deixar o pino 4 aberto (PG).");
                Log("DIAGNÓSTICO", "Não serão enviados SCS, WRV, RUN ou STOP enquanto um PSR válido não for recebido.");
            }
        }

        private ProbeProfile CurrentProbeProfile(string name)
        {
            return new ProbeProfile(name, ParseSelectedInt(baudCombo, 19200), ParseSelectedInt(dataBitsCombo, 7), ParseParity(),
                ParseSelectedInt(stopBitsCombo, 1) == 2 ? StopBits.Two : StopBits.One, (int)responseTimeBox.Value,
                dtrCheck.Checked, rtsCheck.Checked);
        }

        private void ReadBit()
        {
            if (ProbeBusy()) return;
            string address = NormalizeBitAddress(bitAddressBox.Text, true);
            if (address == null) { Warn("Endereço inválido. Use X, Y, C ou SC."); return; }
            bitAddressBox.Text = address;
            string response;
            ExecuteCommand("MCR", address, out response);
        }

        private void WriteBit()
        {
            if (!RequireConfirmedLink()) return;
            string address = NormalizeBitAddress(bitAddressBox.Text, false);
            if (address == null) { Warn("SCS aceita Y, C ou SC; X é somente leitura."); return; }
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
            if (ProbeBusy()) return;
            string address = NormalizeWordAddress(wordAddressBox.Text);
            if (address == null) { Warn("Endereço inválido. Use V, D, WS, WC ou F."); return; }
            wordAddressBox.Text = address;
            string response;
            ExecuteCommand("MRV", address + "01", out response);
        }

        private void WriteWord()
        {
            if (!RequireConfirmedLink()) return;
            string address = NormalizeWordAddress(wordAddressBox.Text);
            if (address == null) { Warn("Endereço inválido. Use V, D, WS, WC ou F."); return; }

            int value;
            string text = (wordValueBox.Text ?? string.Empty).Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) || value < 0 || value > 65535)
            {
                Warn("Valor inválido. Informe 0000 a FFFF.");
                return;
            }

            string hex = value.ToString("X4", CultureInfo.InvariantCulture);
            wordAddressBox.Text = address;
            wordValueBox.Text = hex;
            if (!ConfirmDanger("ESCREVER REGISTRADOR", "Endereço: " + address + "\r\nNovo valor: 0x" + hex)) return;

            string response;
            if (ExecuteCommand("WRV", address + "01" + hex, out response))
            {
                Thread.Sleep(80);
                ExecuteCommand("MRV", address + "01", out response);
            }
        }

        private void ChangeRunState(string command)
        {
            if (!RequireConfirmedLink()) return;
            string action = command == "RUN" ? "COLOCAR O PLC EM RUN" : "COLOCAR O PLC EM STOP";
            string consequence = command == "RUN" ? "O programa começará a executar e poderá energizar saídas." : "A execução do programa será interrompida.";
            if (!ConfirmDanger(action, consequence)) return;

            string response;
            if (ExecuteCommand(command, string.Empty, out response))
            {
                Thread.Sleep(120);
                ExecuteCommand("PSR", string.Empty, out response);
            }
        }

        private bool RequireConfirmedLink()
        {
            if (ProbeBusy()) return false;
            if (linkConfirmed) return true;
            Warn("Primeiro obtenha uma resposta PSR válida com TESTAR CONFIGURAÇÃO ou AUTO-DETECTAR TP02. Escrita e RUN/STOP ficam bloqueados sem confirmação de comunicação.");
            return false;
        }

        private bool ExecuteCommand(string command, string payload, out string response)
        {
            response = string.Empty;
            if (portCombo.SelectedItem == null) { Warn("Nenhuma porta COM selecionada."); return false; }

            string frame = BuildFrameFor((int)stationBox.Value, (int)responseTimeBox.Value, command, payload);
            SerialPort port = null;
            try
            {
                port = new SerialPort(portCombo.SelectedItem.ToString());
                port.BaudRate = ParseSelectedInt(baudCombo, 19200);
                port.DataBits = ParseSelectedInt(dataBitsCombo, 7);
                port.Parity = ParseParity();
                port.StopBits = ParseSelectedInt(stopBitsCombo, 1) == 2 ? StopBits.Two : StopBits.One;
                port.Encoding = Encoding.ASCII;
                port.ReadTimeout = 2500;
                port.WriteTimeout = 1500;
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
                    if (response.Length > 0)
                    {
                        Log("RX", EscapeFrame(response) + "   " + ToHex(response));
                        Log("ERRO", "Resposta incompleta, sem <CR>.");
                        SetState("●  RESPOSTA INCOMPLETA", Warning);
                    }
                    else
                    {
                        Log("ERRO", "Timeout: zero bytes. Em MMI, confirme Computer Link (pino 4 ligado ao 5), COM, estação e RS-422.");
                        SetState("●  ZERO BYTES", Danger);
                    }
                    if (command == "PSR") { linkConfirmed = false; SetDangerousEnabled(false); }
                    return false;
                }

                Log("RX", EscapeFrame(response) + "   " + ToHex(response));
                string detail;
                bool ok = DecodeResponse(command, response, out detail);
                Log(ok ? "OK" : "ERRO", detail);

                if (command == "PSR")
                {
                    linkConfirmed = ok;
                    SetDangerousEnabled(ok);
                    if (ok) { ApplyStatusFromResponse(response); SaveDetectedSettings(); }
                    else SetState("●  QUADRO INVÁLIDO", Danger);
                }
                return ok;
            }
            catch (Exception ex)
            {
                Log("ERRO", ex.Message);
                SetState("●  FALHA DE COMUNICAÇÃO", Danger);
                if (command == "PSR") { linkConfirmed = false; SetDangerousEnabled(false); }
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

        private static string BuildFrameFor(int station, int responseCode, string command, string payload)
        {
            const string responseCodes = "0123456789ABCDEF";
            if (responseCode < 0) responseCode = 0;
            if (responseCode > 15) responseCode = 15;
            string core = station.ToString("00", CultureInfo.InvariantCulture) + "?" + responseCodes[responseCode] + command + payload;
            return "::" + core + Checksum(core) + "\r";
        }

        private static string Checksum(string core)
        {
            int sum = 0;
            for (int i = 0; i < core.Length; i++) sum = (sum + (byte)core[i]) & 0xFF;
            return (((~sum) + 1) & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static bool DecodeResponse(string command, string response, out string detail)
        {
            string clean = StripPrefixAndTerminator(response);
            if (clean.Length < 6) { detail = "Resposta curta demais."; return false; }

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
            if (marker < 0) { detail = "Resposta sem marcador #. Checksum " + (checksumOk ? "OK" : "não validado") + "."; return false; }
            int cmd = clean.IndexOf(command, marker, StringComparison.OrdinalIgnoreCase);
            if (cmd < 0) { detail = "Resposta recebida, mas o eco de " + command + " não foi localizado."; return false; }

            int dataStart = cmd + command.Length;
            int dataLength = clean.Length - dataStart - 2;
            if (dataLength < 0) dataLength = 0;
            string data = clean.Substring(dataStart, dataLength);

            if (command == "PSR")
            {
                string state = data.Length > 0 ? data.Substring(0, 1) : "?";
                string meaning = state == "0" ? "STOP/PROGRAM" : state == "1" ? "RUN" : state == "2" ? "ERROR" : "desconhecido";
                detail = "PLC = " + meaning + ". Checksum " + (checksumOk ? "OK" : "não validado") + ".";
                return checksumOk && (state == "0" || state == "1" || state == "2");
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
            string clean = StripPrefixAndTerminator(response);
            int marker = clean.IndexOf('#');
            int cmd = marker < 0 ? -1 : clean.IndexOf("PSR", marker, StringComparison.OrdinalIgnoreCase);
            if (cmd < 0 || cmd + 3 >= clean.Length) return;
            char state = clean[cmd + 3];
            if (state == '1') SetState("●  RUN", Success);
            else if (state == '0') SetState("●  STOP / PROGRAM", Warning);
            else if (state == '2') SetState("●  ERRO NO PLC", Danger);
            else SetState("●  COMUNICANDO", Success);
        }

        private static string StripPrefixAndTerminator(string response)
        {
            string clean = (response ?? string.Empty).TrimEnd('\r', '\n');
            while (clean.StartsWith(":")) clean = clean.Substring(1);
            return clean;
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

        private void SetDangerousEnabled(bool enabled)
        {
            if (runButton != null) runButton.Enabled = enabled;
            if (stopButton != null) stopButton.Enabled = enabled;
            if (writeBitButton != null) writeBitButton.Enabled = enabled;
            if (writeWordButton != null) writeWordButton.Enabled = enabled;
        }

        private void RefreshPorts()
        {
            string previous = portCombo == null || portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            portCombo.Items.Clear();
            portCombo.Items.AddRange(ports);
            if (previous.Length > 0 && portCombo.Items.IndexOf(previous) < 0) portCombo.Items.Add(previous);
            if (portCombo.Items.Count > 0)
            {
                int idx = previous.Length > 0 ? portCombo.Items.IndexOf(previous) : 0;
                portCombo.SelectedIndex = idx >= 0 ? idx : 0;
            }
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
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 1 || n > max) return null;
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
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) || n < 1 || n > max) return null;
            return prefix + n.ToString(new string('0', width), CultureInfo.InvariantCulture);
        }

        private static string ReadUntilCarriageReturn(SerialPort port, int timeoutMs, out bool complete)
        {
            StringBuilder received = new StringBuilder();
            complete = false;
            port.ReadTimeout = Math.Min(80, Math.Max(20, timeoutMs));
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

        private void Log(string kind, string message)
        {
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + kind + "  " + message + Environment.NewLine);
        }

        private void LogSafe(string kind, string message)
        {
            if (logBox == null || logBox.IsDisposed) return;
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new MethodInvoker(delegate { LogSafe(kind, message); }));
                return;
            }
            Log(kind, message);
        }

        private void SetState(string text, Color color)
        {
            if (stateLabel == null || stateLabel.IsDisposed) return;
            if (stateLabel.InvokeRequired)
            {
                stateLabel.BeginInvoke(new MethodInvoker(delegate { SetState(text, color); }));
                return;
            }
            stateLabel.Text = text;
            stateLabel.ForeColor = color;
        }

        private static string DescribePort(SerialPort port)
        {
            string p = port.Parity == Parity.Even ? "E" : port.Parity == Parity.Odd ? "O" : "N";
            return port.PortName + "  " + port.BaudRate.ToString(CultureInfo.InvariantCulture)
                + " " + port.DataBits.ToString(CultureInfo.InvariantCulture) + p
                + (port.StopBits == StopBits.Two ? "2" : "1")
                + "  DTR=" + OnOff(port.DtrEnable) + " RTS=" + OnOff(port.RtsEnable);
        }

        private static string OnOff(bool value) { return value ? "on" : "off"; }
        private static string EscapeFrame(string frame) { return (frame ?? string.Empty).Replace("\r", "<CR>").Replace("\n", "<LF>"); }

        private static string ToHex(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return "[" + sb.ToString() + "]";
        }

        private void Warn(string text) { MessageBox.Show(this, text, "OpenLadder Studio - TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        private Parity ParseParity()
        {
            string text = parityCombo.SelectedItem == null ? "None" : parityCombo.SelectedItem.ToString();
            try { return (Parity)Enum.Parse(typeof(Parity), text, true); }
            catch { return Parity.None; }
        }

        private static int ParseSelectedInt(ComboBox box, int fallback)
        {
            if (box == null || box.SelectedItem == null) return fallback;
            int value;
            return int.TryParse(box.SelectedItem.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static void SelectOrAdd(ComboBox box, string value)
        {
            if (box.Items.IndexOf(value) < 0) box.Items.Add(value);
            box.SelectedItem = value;
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
            b.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private TextBox TextAt(string text, int left, int top, int width)
        {
            TextBox t = new TextBox();
            t.Text = text;
            t.Location = new Point(left, top);
            t.Size = new Size(width, 25);
            t.Font = new Font("Consolas", 10.0f, FontStyle.Bold);
            t.CharacterCasing = CharacterCasing.Upper;
            return t;
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
            c.Location = new Point(left, top);
            c.AutoSize = true;
            c.Checked = value;
            c.ForeColor = TextSecondary;
            return c;
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
    }
}
