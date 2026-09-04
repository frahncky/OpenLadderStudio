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
    /// Link PG do WEG TP02 baseado na resposta real observada no PLC fisico.
    /// A v0.34 considera a resposta valida ao CON-ICB como confirmacao do Link PG.
    /// Nenhum comando que altera o PLC e enviado nesta tela.
    /// </summary>
    internal sealed class TP02PgLinkV34Form : Form
    {
        private sealed class PgSerialProfile
        {
            public string Name;
            public Parity Parity;
            public bool Dtr;
            public bool Rts;

            public PgSerialProfile(string name, Parity parity, bool dtr, bool rts)
            {
                Name = name;
                Parity = parity;
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
        private Label stateLabel;
        private Label profileLabel;
        private TextBox logBox;
        private Button testButton;
        private volatile bool running;

        private static readonly byte[] Pc12Hello = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };

        public TP02PgLinkV34Form()
        {
            Text = "OpenLadder Studio - Link PG TP02";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 670);
            Size = new Size(1160, 770);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            RefreshPorts();
            LoadSavedPort();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 82;
            header.BackColor = Color.White;
            Controls.Add(header);

            header.Controls.Add(LabelAt("LINK PG - WEG TP02", 15.5f, FontStyle.Bold, Navy, 22, 11));
            header.Controls.Add(LabelAt("Handshake PC12 validado no PLC fisico", 9.0f, FontStyle.Regular, TextSecondary, 24, 44));

            stateLabel = new Label();
            stateLabel.Text = "●  NAO TESTADO";
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 300;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            stateLabel.ForeColor = TextSecondary;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 190;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Comunicacao PG", 11.0f, FontStyle.Bold, TextPrimary, 18, 12));
            AddFieldLabel(config, "Porta COM", 18, 45);

            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Location = new Point(18, 65);
            portCombo.Size = new Size(120, 25);
            config.Controls.Add(portCombo);

            Button refresh = ButtonAt("ATUALIZAR", 148, 62, 100, false);
            refresh.Click += delegate { RefreshPorts(); };
            config.Controls.Add(refresh);

            profileLabel = new Label();
            profileLabel.Text = "Prioridade: 19200 8N1 · DTR/RTS off";
            profileLabel.AutoSize = true;
            profileLabel.Location = new Point(274, 68);
            profileLabel.ForeColor = TextSecondary;
            profileLabel.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            config.Controls.Add(profileLabel);

            testButton = ButtonAt("TESTAR LINK PG (PC12)", 790, 56, 250, true);
            testButton.Click += delegate { StartTest(); };
            config.Controls.Add(testButton);

            config.Controls.Add(LabelAt("Hello PC12: 43 4F 4E 2D 49 43 42 0D = CON-ICB<CR>", 8.5f, FontStyle.Bold, Navy, 18, 108));

            Label observed = new Label();
            observed.Text = "Resposta observada no PLC: C0 01 09 35. Soma modulo 256 = FF, portanto quadro PG valido.";
            observed.AutoSize = false;
            observed.Location = new Point(18, 132);
            observed.Size = new Size(1080, 24);
            observed.ForeColor = Success;
            observed.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            config.Controls.Add(observed);

            Label safety = new Label();
            safety.Text = "A v0.34 NAO envia automaticamente F0 00 0F. RUN, STOP, escrita, download e apagamento permanecem bloqueados ate a proxima etapa do protocolo PG ser confirmada.";
            safety.AutoSize = false;
            safety.Location = new Point(18, 158);
            safety.Size = new Size(1080, 28);
            safety.ForeColor = Danger;
            safety.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            config.Controls.Add(safety);

            Panel info = new Panel();
            info.Dock = DockStyle.Top;
            info.Height = 96;
            info.BackColor = Canvas;
            Controls.Add(info);

            info.Controls.Add(LabelAt("Validacao v0.34", 10.0f, FontStyle.Bold, TextPrimary, 18, 13));
            Label explanation = new Label();
            explanation.Text = "1. Abre 19200/8N1 primeiro.  2. Envia somente CON-ICB<CR>.  3. Registra RX bruto.  4. Remove apenas eco exato do hello.  5. Confirma o Link quando um quadro contiguo >=4 bytes fecha soma FF.";
            explanation.AutoSize = false;
            explanation.Location = new Point(18, 40);
            explanation.Size = new Size(1080, 45);
            explanation.ForeColor = TextSecondary;
            info.Controls.Add(explanation);

            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.ReadOnly = true;
            logBox.WordWrap = false;
            logBox.BackColor = Color.FromArgb(20, 28, 36);
            logBox.ForeColor = Color.FromArgb(218, 232, 245);
            logBox.Font = new Font("Consolas", 9.2f);
            Controls.Add(logBox);

            DockOrder.Apply(this, logBox, info, config, header);
        }

        private void LoadSavedPort()
        {
            try
            {
                PlcDeviceProfile profile = PlcProfileStore.Load();
                PlcConnectionSettings settings = PlcConnectionSettingsStore.Load(profile);
                if (!string.IsNullOrEmpty(settings.PortName) && portCombo.Items.IndexOf(settings.PortName) >= 0)
                    portCombo.SelectedItem = settings.PortName;
            }
            catch { }
        }

        private void SavePort(string portName)
        {
            try
            {
                PlcDeviceProfile profile = PlcProfileStore.Load();
                PlcConnectionSettings settings = PlcConnectionSettingsStore.Load(profile);
                settings.PortName = portName;
                settings.BaudRate = 19200;
                settings.DataBits = 8;
                settings.Parity = "None";
                settings.StopBits = 1;
                settings.TimeoutMs = 1800;
                PlcConnectionSettingsStore.Save(profile, settings);
            }
            catch { }
        }

        private void StartTest()
        {
            if (running) return;
            if (portCombo.SelectedItem == null)
            {
                Warn("Selecione a porta COM usada pelo PC12.");
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            SavePort(portName);
            running = true;
            testButton.Enabled = false;
            logBox.Clear();
            SetState("●  TESTANDO LINK PG...", Warning);
            Log("INFO", "Cabo, COM e modo PG ja foram confirmados no PLC fisico.");
            Log("INFO", "A v0.34 valida a resposta ao CON-ICB e nao envia F0 00 0F automaticamente.");
            Log("INFO", "Nenhum comando que altera o PLC sera enviado.");

            Thread worker = new Thread(new ThreadStart(delegate { TestWorker(portName); }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void TestWorker(string portName)
        {
            PgSerialProfile[] profiles = new PgSerialProfile[]
            {
                new PgSerialProfile("19200 8N1 · DTR/RTS off", Parity.None, false, false),
                new PgSerialProfile("19200 8N1 · DTR/RTS on", Parity.None, true, true),
                new PgSerialProfile("19200 8O1 · DTR/RTS off", Parity.Odd, false, false),
                new PgSerialProfile("19200 8O1 · DTR/RTS on", Parity.Odd, true, true)
            };

            bool sawAnyByte = false;
            string lastError = string.Empty;

            for (int i = 0; i < profiles.Length; i++)
            {
                PgSerialProfile profile = profiles[i];
                LogSafe("PERFIL", profile.Name);

                string error;
                byte[] usefulFrame;
                bool gotBytes;
                bool ok = TryProfile(portName, profile, out usefulFrame, out gotBytes, out error);
                if (gotBytes) sawAnyByte = true;
                if (!string.IsNullOrEmpty(error)) lastError = error;

                if (ok)
                {
                    LogSafe("SUCESSO", "LINK PG confirmado com " + profile.Name + ".");
                    LogSafe("PG FRAME", ToHex(usefulFrame));
                    LogSafe("CHECKSUM", "soma modulo 256 = 0xFF");
                    if (usefulFrame.Length >= 4)
                    {
                        LogSafe("DECODE", "byte0=0x" + usefulFrame[0].ToString("X2", CultureInfo.InvariantCulture)
                            + " byte1=0x" + usefulFrame[1].ToString("X2", CultureInfo.InvariantCulture)
                            + " byte2=0x" + usefulFrame[2].ToString("X2", CultureInfo.InvariantCulture)
                            + " byte3=0x" + usefulFrame[3].ToString("X2", CultureInfo.InvariantCulture));
                    }
                    FinishSafe(true, profile.Name, sawAnyByte, string.Empty);
                    return;
                }
            }

            FinishSafe(false, string.Empty, sawAnyByte, lastError);
        }

        private bool TryProfile(string portName, PgSerialProfile profile, out byte[] usefulFrame,
            out bool sawAnyByte, out string error)
        {
            usefulFrame = new byte[0];
            sawAnyByte = false;
            error = string.Empty;
            SerialPort port = null;

            try
            {
                port = new SerialPort(portName, 19200, profile.Parity, 8, StopBits.One);
                port.Handshake = Handshake.None;
                port.DtrEnable = profile.Dtr;
                port.RtsEnable = profile.Rts;
                port.ReadTimeout = 80;
                port.WriteTimeout = 1000;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                Thread.Sleep(100);
                for (int attempt = 1; attempt <= 4; attempt++)
                {
                    port.DiscardInBuffer();
                    LogSafe("TX HELLO", "tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + "  " + ToHex(Pc12Hello));
                    port.Write(Pc12Hello, 0, Pc12Hello.Length);
                    byte[] raw = ReadBurst(port, 1200);

                    if (raw.Length == 0)
                    {
                        LogSafe("RX HELLO", "[]");
                        Thread.Sleep(100);
                        continue;
                    }

                    sawAnyByte = true;
                    TP02PgFrameParserV33.ParseResult parsed = TP02PgFrameParserV33.Parse(raw, Pc12Hello);
                    LogSafe("RX HELLO RAW", ToHex(parsed.Raw) + "  soma=0x" + parsed.RawSum.ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("ECO", "quantidade=" + parsed.EchoCount.ToString(CultureInfo.InvariantCulture));
                    LogSafe("RX HELLO SEM ECO", ToHex(parsed.WithoutEcho) + "  soma=0x" + parsed.WithoutEchoSum.ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("PARSER", parsed.Detail);

                    if (parsed.IsValid)
                    {
                        usefulFrame = parsed.Frame;
                        return true;
                    }

                    LogSafe("CHECKSUM", "Resposta recebida, mas ainda nao forma quadro PG valido com soma FF.");
                    Thread.Sleep(100);
                }

                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LogSafe("ERRO", profile.Name + ": " + ex.Message);
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

        private static byte[] ReadBurst(SerialPort port, int totalTimeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(totalTimeoutMs);
            DateTime lastByte = DateTime.MinValue;
            System.Collections.Generic.List<byte> bytes = new System.Collections.Generic.List<byte>();

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    int value = port.ReadByte();
                    if (value >= 0)
                    {
                        bytes.Add((byte)value);
                        lastByte = DateTime.UtcNow;
                    }
                }
                catch (TimeoutException)
                {
                    if (bytes.Count > 0 && lastByte != DateTime.MinValue
                        && (DateTime.UtcNow - lastByte).TotalMilliseconds >= 180) break;
                }
            }
            return bytes.ToArray();
        }

        private void FinishSafe(bool success, string profile, bool sawAnyByte, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { FinishSafe(success, profile, sawAnyByte, error); }));
                return;
            }

            running = false;
            testButton.Enabled = true;

            if (success)
            {
                profileLabel.Text = "LINK confirmado: " + profile;
                profileLabel.ForeColor = Success;
                SetState("●  LINK PG CONFIRMADO", Success);
                Log("RESULTADO", "O TP02 respondeu ao hello do PC12 com quadro PG de checksum valido.");
                Log("RESULTADO", "A proxima etapa e decodificar o significado dos bytes do quadro e a sequencia posterior do PC12.");
                Log("RESULTADO", "RUN/STOP/escrita continuam bloqueados.");
            }
            else if (sawAnyByte)
            {
                SetState("●  PG RESPONDEU · QUADRO A DECODIFICAR", Warning);
                Log("RESULTADO", "O PLC respondeu ao hello, mas nenhum quadro com soma FF foi isolado neste teste.");
            }
            else
            {
                SetState("●  SEM RESPOSTA PG", Danger);
                Log("RESULTADO", "Nenhum byte retornou ao hello PG.");
                if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
            }
        }

        private void RefreshPorts()
        {
            string previous = portCombo == null || portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            portCombo.Items.Clear();
            portCombo.Items.AddRange(ports);
            if (portCombo.Items.Count > 0)
            {
                int index = previous.Length > 0 ? portCombo.Items.IndexOf(previous) : -1;
                portCombo.SelectedIndex = index >= 0 ? index : 0;
            }
        }

        private void Log(string kind, string text)
        {
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + kind + "  " + text + Environment.NewLine);
        }

        private void LogSafe(string kind, string text)
        {
            if (logBox == null || logBox.IsDisposed) return;
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new MethodInvoker(delegate { LogSafe(kind, text); }));
                return;
            }
            Log(kind, text);
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

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "[]";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return "[" + sb.ToString() + "]";
        }

        private void Warn(string text)
        {
            MessageBox.Show(this, text, "OpenLadder Studio - TP02 PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
