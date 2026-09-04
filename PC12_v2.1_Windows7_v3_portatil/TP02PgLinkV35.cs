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
    /// <summary>
    /// Link PG v0.35 do WEG TP02.
    /// Mantem a descoberta da v0.34 (varredura de perfis 8 bits) e, quando
    /// um quadro PG valido e encontrado, preserva a mesma porta aberta para
    /// uma captura passiva posterior. Nenhum comando de escrita, RUN, STOP,
    /// download ou apagamento e enviado por esta ferramenta.
    /// </summary>
    internal sealed class TP02PgLinkV35Form : Form
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
        private volatile bool cancelRequested;

        private const int HelloTimeoutMs = 1600;
        private const int PostLinkCaptureMs = 5000;

        private static readonly byte[] Pc12Hello = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };

        private static readonly byte[] KnownResponse = new byte[]
        {
            0xC0, 0x01, 0x09, 0x35
        };

        public TP02PgLinkV35Form()
        {
            Text = "OpenLadder Studio - Link PG TP02 v0.35";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 690);
            Size = new Size(1180, 790);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            RefreshPorts();
            LoadSavedPort();
            FormClosing += delegate { cancelRequested = true; };
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 82;
            header.BackColor = Color.White;
            Controls.Add(header);

            header.Controls.Add(LabelAt("LINK PG - WEG TP02", 15.5f, FontStyle.Bold, Navy, 22, 11));
            header.Controls.Add(LabelAt("v0.35 - auto-deteccao segura + captura pos-handshake", 9.0f, FontStyle.Regular, TextSecondary, 24, 44));

            stateLabel = new Label();
            stateLabel.Text = "●  NAO TESTADO";
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 330;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            stateLabel.ForeColor = TextSecondary;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 210;
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
            profileLabel.Text = "AUTO: 19200 / 8 bits / N-O-E / DTR-RTS";
            profileLabel.AutoSize = true;
            profileLabel.Location = new Point(274, 68);
            profileLabel.ForeColor = TextSecondary;
            profileLabel.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            config.Controls.Add(profileLabel);

            testButton = ButtonAt("DETECTAR LINK E CAPTURAR", 760, 56, 280, true);
            testButton.Click += delegate { StartTest(); };
            config.Controls.Add(testButton);

            Button clear = ButtonAt("LIMPAR", 1050, 56, 92, false);
            clear.Click += delegate { if (!running && logBox != null) logBox.Clear(); };
            config.Controls.Add(clear);

            config.Controls.Add(LabelAt("HELLO PC12: 43 4F 4E 2D 49 43 42 0D = CON-ICB<CR>", 8.5f, FontStyle.Bold, Navy, 18, 108));

            Label observed = new Label();
            observed.Text = "Quadro conhecido: C0 01 09 35 (soma FF). A v0.35 tambem aceita outro quadro PG valido apos remover o eco exato do HELLO.";
            observed.AutoSize = false;
            observed.Location = new Point(18, 132);
            observed.Size = new Size(1110, 28);
            observed.ForeColor = Success;
            observed.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            config.Controls.Add(observed);

            Label safety = new Label();
            safety.Text = "MODO SEGURO: somente CON-ICB<CR> e enviado. A varredura altera apenas paridade e sinais DTR/RTS da porta serial. RUN, STOP, escrita, download e apagamento continuam bloqueados.";
            safety.AutoSize = false;
            safety.Location = new Point(18, 164);
            safety.Size = new Size(1110, 38);
            safety.ForeColor = Danger;
            safety.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            config.Controls.Add(safety);

            Panel info = new Panel();
            info.Dock = DockStyle.Top;
            info.Height = 108;
            info.BackColor = Canvas;
            Controls.Add(info);

            info.Controls.Add(LabelAt("Diagnostico v0.35", 10.0f, FontStyle.Bold, TextPrimary, 18, 13));
            Label explanation = new Label();
            explanation.Text = "1. Testa primeiro 8O1 com DTR/RTS on.  2. Se falhar, repete os perfis seguros da v0.34 e inclui 8E1.  3. Registra RX bruto e remove somente o eco exato.  4. Ao detectar quadro PG valido, salva o perfil.  5. Mantem a porta aberta e escuta por 5 s sem transmitir mais nada.";
            explanation.AutoSize = false;
            explanation.Location = new Point(18, 40);
            explanation.Size = new Size(1110, 56);
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

        private void SaveDetected(string portName, PgSerialProfile detected)
        {
            try
            {
                PlcDeviceProfile profile = PlcProfileStore.Load();
                PlcConnectionSettings settings = PlcConnectionSettingsStore.Load(profile);
                settings.PortName = portName;
                settings.BaudRate = 19200;
                settings.DataBits = 8;
                settings.Parity = detected.Parity == Parity.Odd ? "Odd" : detected.Parity == Parity.Even ? "Even" : "None";
                settings.StopBits = 1;
                settings.TimeoutMs = HelloTimeoutMs;
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
            cancelRequested = false;
            running = true;
            testButton.Enabled = false;
            logBox.Clear();
            profileLabel.Text = "AUTO-DETECTANDO...";
            profileLabel.ForeColor = Warning;
            SetState("●  PROCURANDO LINK PG...", Warning);

            Log("INFO", "A v0.35 voltou a varrer os perfis da v0.34; nao depende mais de 8O1 fixo.");
            Log("INFO", "Somente CON-ICB<CR> sera transmitido em cada tentativa.");

            Thread worker = new Thread(new ThreadStart(delegate { TestWorker(portName); }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void TestWorker(string portName)
        {
            PgSerialProfile[] profiles = new PgSerialProfile[]
            {
                new PgSerialProfile("19200 8O1 · DTR/RTS on", Parity.Odd, true, true),
                new PgSerialProfile("19200 8N1 · DTR/RTS off", Parity.None, false, false),
                new PgSerialProfile("19200 8N1 · DTR/RTS on", Parity.None, true, true),
                new PgSerialProfile("19200 8O1 · DTR/RTS off", Parity.Odd, false, false),
                new PgSerialProfile("19200 8E1 · DTR/RTS on", Parity.Even, true, true),
                new PgSerialProfile("19200 8E1 · DTR/RTS off", Parity.Even, false, false)
            };

            bool sawAnyByte = false;
            string lastError = string.Empty;

            for (int i = 0; i < profiles.Length && !cancelRequested; i++)
            {
                PgSerialProfile p = profiles[i];
                SerialPort port = null;
                try
                {
                    LogSafe("PERFIL", p.Name);
                    port = new SerialPort(portName, 19200, p.Parity, 8, StopBits.One);
                    port.Handshake = Handshake.None;
                    port.DtrEnable = p.Dtr;
                    port.RtsEnable = p.Rts;
                    port.ReadTimeout = 70;
                    port.WriteTimeout = 1000;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();
                    Thread.Sleep(120);

                    for (int attempt = 1; attempt <= 3 && !cancelRequested; attempt++)
                    {
                        port.DiscardInBuffer();
                        LogSafe("PG HELLO TX", "tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + "  " + ToHex(Pc12Hello));
                        port.Write(Pc12Hello, 0, Pc12Hello.Length);

                        byte[] raw = ReadBurst(port, HelloTimeoutMs, 220);
                        if (raw.Length == 0)
                        {
                            LogSafe("PG HELLO RX", "[]");
                            Thread.Sleep(120);
                            continue;
                        }

                        sawAnyByte = true;
                        TP02PgFrameParserV33.ParseResult parsed = TP02PgFrameParserV33.Parse(raw, Pc12Hello);
                        LogSafe("PG HELLO RX RAW", ToHex(parsed.Raw) + "  soma=0x" + parsed.RawSum.ToString("X2", CultureInfo.InvariantCulture));
                        LogSafe("PG ECO", "quantidade=" + parsed.EchoCount.ToString(CultureInfo.InvariantCulture));
                        LogSafe("PG RX SEM ECO", ToHex(parsed.WithoutEcho) + "  soma=0x" + parsed.WithoutEchoSum.ToString("X2", CultureInfo.InvariantCulture));
                        LogSafe("PG PARSER", parsed.Detail);

                        int knownIndex = IndexOfSequence(parsed.WithoutEcho, KnownResponse);
                        bool exactKnown = knownIndex >= 0;
                        bool validFrame = parsed.IsValid;

                        if (exactKnown || validFrame)
                        {
                            byte[] frame = exactKnown ? KnownResponse : parsed.Frame;
                            LogSafe("PG FRAME", ToHex(frame));
                            LogSafe("PG CHECKSUM", "soma modulo 256 = 0x" + TP02PgFrameParserV33.Sum8(frame).ToString("X2", CultureInfo.InvariantCulture));
                            LogSafe("PG LINK", exactKnown ? "ESTABLISHED - C0 01 09 35 confirmado." : "ESTABLISHED - quadro PG checksum FF confirmado.");
                            SaveDetected(portName, p);
                            SetDetectingProfileSafe("CONFIRMADO: " + p.Name, Success);
                            SetState("●  LINK PG CONFIRMADO · CAPTURANDO...", Success);

                            if (exactKnown)
                            {
                                int inlineOffset = knownIndex + KnownResponse.Length;
                                if (inlineOffset < parsed.WithoutEcho.Length)
                                {
                                    byte[] inline = Slice(parsed.WithoutEcho, inlineOffset, parsed.WithoutEcho.Length - inlineOffset);
                                    if (inline.Length > 0) LogSafe("PG POST INLINE", ToHex(inline));
                                }
                            }

                            int postBursts = CapturePostLink(port);
                            FinishSafe(true, sawAnyByte, postBursts, p.Name, string.Empty);
                            return;
                        }

                        LogSafe("PG DIAG", "bytes recebidos, mas sem quadro PG valido neste perfil/tentativa.");
                        Thread.Sleep(120);
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    LogSafe("ERRO", p.Name + ": " + ex.Message);
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

            if (cancelRequested) FinishCancelled();
            else FinishSafe(false, sawAnyByte, 0, string.Empty, lastError);
        }

        private int CapturePostLink(SerialPort port)
        {
            int bursts = 0;
            DateTime start = DateTime.UtcNow;
            DateTime deadline = start.AddMilliseconds(PostLinkCaptureMs);
            LogSafe("PG CAPTURE", "escuta passiva iniciada; nenhum byte adicional sera transmitido.");

            while (DateTime.UtcNow < deadline && !cancelRequested)
            {
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0) break;
                int window = remaining > 500 ? 500 : remaining;
                if (window < 80) window = 80;

                byte[] burst = ReadBurst(port, window, 150);
                if (burst.Length == 0) continue;

                bursts++;
                int elapsed = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                int sum = TP02PgFrameParserV33.Sum8(burst);
                LogSafe("PG POST RX", "+" + elapsed.ToString(CultureInfo.InvariantCulture) + " ms  " + ToHex(burst) + "  soma=0x" + sum.ToString("X2", CultureInfo.InvariantCulture));
            }

            if (bursts == 0)
                LogSafe("PG CAPTURE", "nenhum byte adicional em " + PostLinkCaptureMs.ToString(CultureInfo.InvariantCulture) + " ms; o PLC provavelmente aguarda o proximo TX do PC12.");
            else
                LogSafe("PG CAPTURE", bursts.ToString(CultureInfo.InvariantCulture) + " burst(s) posterior(es) registrado(s).");
            return bursts;
        }

        private static byte[] ReadBurst(SerialPort port, int totalTimeoutMs, int quietGapMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(totalTimeoutMs);
            DateTime lastByte = DateTime.MinValue;
            List<byte> bytes = new List<byte>();

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
                        && (DateTime.UtcNow - lastByte).TotalMilliseconds >= quietGapMs) break;
                }
            }
            return bytes.ToArray();
        }

        private void FinishSafe(bool success, bool sawAnyByte, int postBursts, string profile, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { FinishSafe(success, sawAnyByte, postBursts, profile, error); }));
                return;
            }

            running = false;
            testButton.Enabled = true;

            if (success)
            {
                SetState("●  LINK PG CONFIRMADO", Success);
                Log("RESULTADO", "Link PG confirmado com " + profile + ".");
                if (postBursts > 0)
                    Log("RESULTADO", "Ha dados posteriores ao handshake no log.");
                else
                    Log("RESULTADO", "Nao houve RX espontaneo apos o handshake; o proximo TX do PC12 ainda precisa ser capturado.");
                Log("RESULTADO", "Nenhum comando posterior ao HELLO foi transmitido.");
            }
            else if (sawAnyByte)
            {
                SetState("●  PG RESPONDEU · AINDA NAO VALIDADO", Warning);
                profileLabel.Text = "Bytes recebidos; revisar log";
                profileLabel.ForeColor = Warning;
                Log("RESULTADO", "O TP02 devolveu bytes, mas nenhum quadro PG checksum FF foi isolado. Envie uma foto do log desta tela.");
                if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
            }
            else
            {
                SetState("●  SEM RESPOSTA PG", Danger);
                profileLabel.Text = "Nenhum perfil respondeu";
                profileLabel.ForeColor = Danger;
                Log("RESULTADO", "Nenhum byte retornou ao CON-ICB em nenhum perfil 19200/8 bits testado.");
                Log("RESULTADO", "Confirme a COM e feche o PC12 original antes de testar, pois duas aplicacoes nao podem usar a mesma porta simultaneamente.");
                if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
            }
        }

        private void FinishCancelled()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { FinishCancelled(); }));
                return;
            }
            running = false;
            testButton.Enabled = true;
            SetState("●  CANCELADO", TextSecondary);
        }

        private void RefreshPorts()
        {
            if (portCombo == null) return;
            string previous = portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            portCombo.Items.Clear();
            portCombo.Items.AddRange(ports);
            if (ports.Length > 0)
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

        private void SetDetectingProfileSafe(string text, Color color)
        {
            if (profileLabel == null || profileLabel.IsDisposed) return;
            if (profileLabel.InvokeRequired)
            {
                profileLabel.BeginInvoke(new MethodInvoker(delegate { SetDetectingProfileSafe(text, color); }));
                return;
            }
            profileLabel.Text = text;
            profileLabel.ForeColor = color;
        }

        private static int IndexOfSequence(byte[] source, byte[] pattern)
        {
            if (source == null || pattern == null || pattern.Length == 0 || source.Length < pattern.Length) return -1;
            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j]) { ok = false; break; }
                }
                if (ok) return i;
            }
            return -1;
        }

        private static byte[] Slice(byte[] source, int offset, int count)
        {
            if (source == null || count <= 0 || offset < 0 || offset >= source.Length) return new byte[0];
            if (offset + count > source.Length) count = source.Length - offset;
            byte[] output = new byte[count];
            Buffer.BlockCopy(source, offset, output, 0, count);
            return output;
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

    internal static class TP02PgLinkV35Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02PgLinkV35Form());
        }
    }
}
