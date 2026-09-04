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
    /// Validacao PG v0.35 baseada no quadro real observado no WEG TP02.
    /// Perfil confirmado em bancada: 19200 bps, 8O1, DTR/RTS ligados.
    /// A ferramenta envia somente CON-ICB<CR> e, apos confirmar C0 01 09 35,
    /// permanece apenas escutando a linha para registrar bytes posteriores.
    /// RUN, STOP, escrita, download e apagamento nao fazem parte desta etapa.
    /// </summary>
    internal sealed class TP02PgLinkV35Form : Form
    {
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

        private const int HelloTimeoutMs = 1500;
        private const int PostLinkCaptureMs = 5000;

        private static readonly byte[] Pc12Hello = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };

        private static readonly byte[] ExpectedHelloResponse = new byte[]
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
            header.Controls.Add(LabelAt("v0.35 - perfil real confirmado e captura pos-handshake", 9.0f, FontStyle.Regular, TextSecondary, 24, 44));

            stateLabel = new Label();
            stateLabel.Text = "●  NAO TESTADO";
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 320;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            stateLabel.ForeColor = TextSecondary;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 204;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Comunicacao PG validada", 11.0f, FontStyle.Bold, TextPrimary, 18, 12));
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
            profileLabel.Text = "FIXO: 19200 8O1 · DTR/RTS on";
            profileLabel.AutoSize = true;
            profileLabel.Location = new Point(274, 68);
            profileLabel.ForeColor = Success;
            profileLabel.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            config.Controls.Add(profileLabel);

            testButton = ButtonAt("VALIDAR E CAPTURAR", 790, 56, 250, true);
            testButton.Click += delegate { StartTest(); };
            config.Controls.Add(testButton);

            Button clear = ButtonAt("LIMPAR", 1050, 56, 92, false);
            clear.Click += delegate { if (!running && logBox != null) logBox.Clear(); };
            config.Controls.Add(clear);

            config.Controls.Add(LabelAt("HELLO PC12: 43 4F 4E 2D 49 43 42 0D = CON-ICB<CR>", 8.5f, FontStyle.Bold, Navy, 18, 108));

            Label observed = new Label();
            observed.Text = "Resposta esperada: C0 01 09 35. Checksum: (C0 + 01 + 09 + 35) mod 256 = FF.";
            observed.AutoSize = false;
            observed.Location = new Point(18, 132);
            observed.Size = new Size(1090, 24);
            observed.ForeColor = Success;
            observed.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            config.Controls.Add(observed);

            Label safety = new Label();
            safety.Text = "MODO SEGURO: a v0.35 envia somente CON-ICB<CR>. Depois do handshake, apenas escuta por 5 s. RUN, STOP, escrita, download e apagamento permanecem bloqueados.";
            safety.AutoSize = false;
            safety.Location = new Point(18, 160);
            safety.Size = new Size(1110, 36);
            safety.ForeColor = Danger;
            safety.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            config.Controls.Add(safety);

            Panel info = new Panel();
            info.Dock = DockStyle.Top;
            info.Height = 108;
            info.BackColor = Canvas;
            Controls.Add(info);

            info.Controls.Add(LabelAt("Validacao v0.35", 10.0f, FontStyle.Bold, TextPrimary, 18, 13));
            Label explanation = new Label();
            explanation.Text = "1. Abre 19200/8O1 com DTR/RTS ligados.  2. Envia somente CON-ICB<CR>.  3. Confirma o quadro exato C0 01 09 35.  4. Mantem a mesma porta aberta e registra RX bruto por 5 s.  5. Nao transmite nenhum comando adicional.";
            explanation.AutoSize = false;
            explanation.Location = new Point(18, 40);
            explanation.Size = new Size(1110, 54);
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
                settings.Parity = "Odd";
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
            SavePort(portName);
            cancelRequested = false;
            running = true;
            testButton.Enabled = false;
            logBox.Clear();
            SetState("●  VALIDANDO LINK PG...", Warning);

            Log("INFO", "Perfil fixado pelo teste real: 19200 8O1 · DTR/RTS on.");
            Log("INFO", "A v0.35 envia somente CON-ICB<CR> e nao envia F0 00 0F.");
            Log("INFO", "Depois do handshake, a porta fica somente em escuta por " + PostLinkCaptureMs.ToString(CultureInfo.InvariantCulture) + " ms.");

            Thread worker = new Thread(new ThreadStart(delegate { TestWorker(portName); }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void TestWorker(string portName)
        {
            SerialPort port = null;
            bool sawAnyByte = false;
            string lastError = string.Empty;
            int postBursts = 0;

            try
            {
                port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                port.Handshake = Handshake.None;
                port.DtrEnable = true;
                port.RtsEnable = true;
                port.ReadTimeout = 70;
                port.WriteTimeout = 1000;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                LogSafe("PORTA", portName + "  19200 8O1  DTR=on  RTS=on");
                Thread.Sleep(120);

                for (int attempt = 1; attempt <= 4 && !cancelRequested; attempt++)
                {
                    port.DiscardInBuffer();
                    LogSafe("PG HELLO TX", "tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + "  " + ToHex(Pc12Hello));
                    port.Write(Pc12Hello, 0, Pc12Hello.Length);

                    byte[] raw = ReadBurst(port, HelloTimeoutMs, 180);
                    if (raw.Length == 0)
                    {
                        LogSafe("PG HELLO RX", "[]");
                        Thread.Sleep(100);
                        continue;
                    }

                    sawAnyByte = true;
                    TP02PgFrameParserV33.ParseResult parsed = TP02PgFrameParserV33.Parse(raw, Pc12Hello);
                    LogSafe("PG HELLO RX RAW", ToHex(parsed.Raw) + "  soma=0x" + parsed.RawSum.ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("PG ECO", "quantidade=" + parsed.EchoCount.ToString(CultureInfo.InvariantCulture));
                    LogSafe("PG RX SEM ECO", ToHex(parsed.WithoutEcho) + "  soma=0x" + parsed.WithoutEchoSum.ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("PG PARSER", parsed.Detail);

                    int responseIndex = IndexOfSequence(parsed.WithoutEcho, ExpectedHelloResponse);
                    if (responseIndex >= 0)
                    {
                        LogSafe("PG FRAME", ToHex(ExpectedHelloResponse));
                        LogSafe("PG CHECKSUM", "soma modulo 256 = 0x" + TP02PgFrameParserV33.Sum8(ExpectedHelloResponse).ToString("X2", CultureInfo.InvariantCulture));
                        LogSafe("PG DECODE", "byte0=0xC0  byte1=0x01  byte2=0x09  byte3=0x35(checksum)");
                        LogSafe("PG LINK", "ESTABLISHED - resposta exata observada no PLC confirmada.");
                        SetState("●  LINK PG CONFIRMADO · CAPTURANDO...", Success);

                        int inlineOffset = responseIndex + ExpectedHelloResponse.Length;
                        if (inlineOffset < parsed.WithoutEcho.Length)
                        {
                            byte[] inline = Slice(parsed.WithoutEcho, inlineOffset, parsed.WithoutEcho.Length - inlineOffset);
                            if (inline.Length > 0)
                            {
                                postBursts++;
                                LogSafe("PG POST INLINE", ToHex(inline) + "  soma=0x" + TP02PgFrameParserV33.Sum8(inline).ToString("X2", CultureInfo.InvariantCulture));
                            }
                        }

                        postBursts += CapturePostLink(port);
                        FinishSafe(true, sawAnyByte, postBursts, string.Empty);
                        return;
                    }

                    if (parsed.IsValid)
                    {
                        LogSafe("PG CANDIDATO", "checksum FF valido, mas quadro diferente de C0 01 09 35: " + ToHex(parsed.Frame));
                    }
                    else
                    {
                        LogSafe("PG CHECKSUM", "Resposta recebida, mas o handshake conhecido ainda nao foi encontrado.");
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                LogSafe("ERRO", ex.Message);
            }
            finally
            {
                if (port != null)
                {
                    try { if (port.IsOpen) port.Close(); } catch { }
                    port.Dispose();
                }
            }

            if (cancelRequested)
                FinishCancelled();
            else
                FinishSafe(false, sawAnyByte, postBursts, lastError);
        }

        private int CapturePostLink(SerialPort port)
        {
            int bursts = 0;
            DateTime start = DateTime.UtcNow;
            DateTime deadline = start.AddMilliseconds(PostLinkCaptureMs);
            LogSafe("PG CAPTURE", "inicio da escuta passiva; nenhum byte sera transmitido.");

            while (DateTime.UtcNow < deadline && !cancelRequested)
            {
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0) break;
                int window = remaining > 450 ? 450 : remaining;
                if (window < 70) window = 70;

                byte[] burst = ReadBurst(port, window, 120);
                if (burst.Length == 0) continue;

                bursts++;
                int elapsed = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                int sum = TP02PgFrameParserV33.Sum8(burst);
                LogSafe("PG POST RX", "+" + elapsed.ToString(CultureInfo.InvariantCulture) + " ms  " + ToHex(burst) + "  soma=0x" + sum.ToString("X2", CultureInfo.InvariantCulture));
                if (burst.Length >= 4 && sum == 0xFF)
                    LogSafe("PG POST FRAME?", "burst fecha checksum FF; manter bruto para correlacao com a sequencia do PC12.");
            }

            if (bursts == 0)
            {
                LogSafe("PG CAPTURE", "nenhum byte adicional em " + PostLinkCaptureMs.ToString(CultureInfo.InvariantCulture) + " ms.");
                LogSafe("PG HIPOTESE", "o TP02 provavelmente aguarda o proximo comando do PC12; esta versao nao inventa nem transmite esse comando.");
            }
            else
            {
                LogSafe("PG CAPTURE", bursts.ToString(CultureInfo.InvariantCulture) + " burst(s) posterior(es) registrado(s).");
            }
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

        private void FinishSafe(bool success, bool sawAnyByte, int postBursts, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { FinishSafe(success, sawAnyByte, postBursts, error); }));
                return;
            }

            running = false;
            testButton.Enabled = true;

            if (success)
            {
                profileLabel.Text = "CONFIRMADO: 19200 8O1 · DTR/RTS on";
                profileLabel.ForeColor = Success;
                SetState("●  LINK PG CONFIRMADO", Success);
                Log("RESULTADO", "Handshake exato confirmado: C0 01 09 35; checksum FF.");
                if (postBursts > 0)
                    Log("RESULTADO", "Ha dados posteriores ao handshake no log. A proxima etapa e correlacionar esses bytes com a sequencia do PC12.");
                else
                    Log("RESULTADO", "Nao houve RX espontaneo depois do handshake. O proximo TX do PC12 continua desconhecido e precisa ser capturado no PC12 original/sniffer.");
                Log("RESULTADO", "Nenhum comando posterior ao HELLO foi transmitido. RUN/STOP/escrita continuam bloqueados.");
            }
            else if (sawAnyByte)
            {
                SetState("●  PG RESPONDEU · HANDSHAKE DIVERGENTE", Warning);
                Log("RESULTADO", "Foram recebidos bytes, mas C0 01 09 35 nao foi localizado nesta execucao.");
                if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
            }
            else
            {
                SetState("●  SEM RESPOSTA PG", Danger);
                Log("RESULTADO", "Nenhum byte retornou ao CON-ICB no perfil confirmado 19200 8O1 com DTR/RTS ligados.");
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
            SetState("●  CANCELADO", Warning);
            Log("RESULTADO", "Captura encerrada.");
        }

        private void RefreshPorts()
        {
            if (portCombo == null) return;
            string previous = portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
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

        private static int IndexOfSequence(byte[] source, byte[] pattern)
        {
            if (source == null || pattern == null || pattern.Length == 0 || source.Length < pattern.Length) return -1;
            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool equal = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        equal = false;
                        break;
                    }
                }
                if (equal) return i;
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
