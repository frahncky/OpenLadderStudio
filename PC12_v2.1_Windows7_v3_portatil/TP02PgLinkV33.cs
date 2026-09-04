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
    /// Link PG do WEG TP02 reproduzindo a sequencia inicial do PC12 2.1.
    /// A v0.33 separa eco serial e quadro util antes da validacao do checksum.
    /// Esta tela continua somente leitura/handshake.
    /// </summary>
    internal sealed class TP02PgLinkV33Form : Form
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

        private static readonly byte[] Pc12Probe = new byte[]
        {
            0xF0, 0x00, 0x0F
        };

        public TP02PgLinkV33Form()
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
            header.Controls.Add(LabelAt("Handshake PC12 com separação de eco e enquadramento binário", 9.0f, FontStyle.Regular, TextSecondary, 24, 44));

            stateLabel = new Label();
            stateLabel.Text = "●  NÃO TESTADO";
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 300;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            stateLabel.ForeColor = TextSecondary;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 180;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Comunicação PG", 11.0f, FontStyle.Bold, TextPrimary, 18, 12));
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
            profileLabel.Text = "PC12: 19200 bps · 8 bits · 1 stop · ODD/NONE";
            profileLabel.AutoSize = true;
            profileLabel.Location = new Point(274, 68);
            profileLabel.ForeColor = TextSecondary;
            profileLabel.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            config.Controls.Add(profileLabel);

            testButton = ButtonAt("TESTAR LINK PG (PC12)", 790, 56, 250, true);
            testButton.Click += delegate { StartTest(); };
            config.Controls.Add(testButton);

            config.Controls.Add(LabelAt("Hello PC12: 43 4F 4E 2D 49 43 42 0D = CON-ICB<CR> · Probe: F0 00 0F", 8.5f, FontStyle.Bold, Navy, 18, 108));

            Label safety = new Label();
            safety.Text = "TESTE SOMENTE DE LINK. RUN, STOP, escrita, download e apagamento continuam bloqueados.\r\nA v0.33 não considera o burst inteiro como quadro: remove apenas eco exato e valida o quadro PG isolado.";
            safety.AutoSize = false;
            safety.Location = new Point(18, 132);
            safety.Size = new Size(1080, 44);
            safety.ForeColor = Danger;
            safety.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            config.Controls.Add(safety);

            Panel info = new Panel();
            info.Dock = DockStyle.Top;
            info.Height = 96;
            info.BackColor = Canvas;
            Controls.Add(info);
            info.Controls.Add(LabelAt("Validação v0.33", 10.0f, FontStyle.Bold, TextPrimary, 18, 13));

            Label explanation = new Label();
            explanation.Text = "1. Envia o handshake do PC12.  2. Registra RX bruto.  3. Remove somente ecos exatos de F0 00 0F.  4. Procura quadro contíguo com pelo menos 4 bytes e soma módulo 256 = FF.  5. Exibe quadro útil isolado.";
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
            Log("INFO", "COM/cabo/modo PG já foram confirmados fisicamente na v0.32.");
            Log("INFO", "A v0.33 separa eco serial e resposta útil antes do checksum.");
            Log("INFO", "Nenhum comando que altera o PLC será enviado.");

            Thread worker = new Thread(new ThreadStart(delegate { TestWorker(portName); }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void TestWorker(string portName)
        {
            PgSerialProfile[] profiles = new PgSerialProfile[]
            {
                new PgSerialProfile("19200 8O1 · DTR/RTS off", Parity.Odd, false, false),
                new PgSerialProfile("19200 8O1 · DTR/RTS on", Parity.Odd, true, true),
                new PgSerialProfile("19200 8N1 · DTR/RTS off", Parity.None, false, false),
                new PgSerialProfile("19200 8N1 · DTR/RTS on", Parity.None, true, true)
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
                    LogSafe("PG", "Checksum do quadro isolado = FF.");
                    if (usefulFrame != null && usefulFrame.Length > 0)
                    {
                        LogSafe("PG", "Primeiro byte = 0x" + usefulFrame[0].ToString("X2", CultureInfo.InvariantCulture)
                            + "; bit 0x40 = " + ((usefulFrame[0] & 0x40) != 0 ? "1" : "0") + ".");
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

                Thread.Sleep(80);
                byte[] helloReply = new byte[0];
                for (int attempt = 1; attempt <= 4; attempt++)
                {
                    port.DiscardInBuffer();
                    LogSafe("TX HELLO", "tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + "  " + ToHex(Pc12Hello));
                    port.Write(Pc12Hello, 0, Pc12Hello.Length);
                    helloReply = ReadBurst(port, 1000);
                    if (helloReply.Length > 0)
                    {
                        sawAnyByte = true;
                        LogSafe("RX HELLO", ToHex(helloReply) + "  ASCII=" + ToPrintable(helloReply));
                        break;
                    }
                    Thread.Sleep(80);
                }

                if (helloReply.Length == 0)
                {
                    LogSafe("SEM RX", "CON-ICB não recebeu nenhum byte neste perfil.");
                    return false;
                }

                Thread.Sleep(120);
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    port.DiscardInBuffer();
                    LogSafe("TX PG", "tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + "  " + ToHex(Pc12Probe));
                    port.Write(Pc12Probe, 0, Pc12Probe.Length);
                    byte[] raw = ReadBurst(port, 1200);
                    if (raw.Length == 0)
                    {
                        LogSafe("RX PG", "[]");
                        Thread.Sleep(100);
                        continue;
                    }

                    sawAnyByte = true;
                    TP02PgFrameParserV33.ParseResult parsed = TP02PgFrameParserV33.Parse(raw, Pc12Probe);
                    LogSafe("RX PG RAW", ToHex(parsed.Raw) + "  soma=0x" + parsed.RawSum.ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("ECO", "quantidade=" + parsed.EchoCount.ToString(CultureInfo.InvariantCulture));
                    LogSafe("RX SEM ECO", ToHex(parsed.WithoutEcho) + "  soma=0x" + parsed.WithoutEchoSum.ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("PARSER", parsed.Detail);

                    if (parsed.IsValid)
                    {
                        usefulFrame = parsed.Frame;
                        return true;
                    }

                    LogSafe("CHECKSUM", "A resposta chegou, mas ainda não foi possível isolar quadro PG >=4 bytes com soma FF.");
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
                Log("RESULTADO", "Handshake, resposta binária e enquadramento PG foram confirmados.");
                Log("RESULTADO", "RUN/STOP/escrita continuam bloqueados até decodificarmos os comandos PG correspondentes.");
            }
            else if (sawAnyByte)
            {
                SetState("●  PG RESPONDEU · FRAME A DECODIFICAR", Warning);
                Log("RESULTADO", "COM/cabo/modo PG estão confirmados; a resposta binária foi preservada sem mascarar o burst bruto.");
                Log("RESULTADO", "Use as linhas RX PG RAW / RX SEM ECO / PARSER para a próxima decodificação.");
            }
            else
            {
                SetState("●  SEM RESPOSTA PG", Danger);
                Log("RESULTADO", "Nenhum byte retornou ao handshake PG neste teste.");
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

        private static string ToPrintable(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "''";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b == 13) sb.Append("<CR>");
                else if (b == 10) sb.Append("<LF>");
                else if (b >= 32 && b <= 126) sb.Append((char)b);
                else sb.Append("<" + b.ToString("X2", CultureInfo.InvariantCulture) + ">");
            }
            return "'" + sb.ToString() + "'";
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
