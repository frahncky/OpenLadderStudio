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
    /// Link PG v0.37 do WEG TP02.
    /// Etapa controlada de engenharia reversa: confirma primeiro o HELLO conhecido
    /// CON-ICB<CR> -> C0 01 09 35 e somente entao transmite uma unica vez o segundo
    /// quadro observado no PC12, F0 00 0F. Depois disso apenas registra RX bruto.
    /// RUN, STOP, escrita, download e apagamento permanecem bloqueados.
    /// </summary>
    internal sealed class TP02PgLinkV37Form : Form
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

        private const int HelloTimeoutMs = 1700;
        private const int Stage2TimeoutMs = 3200;
        private const int PostStage2CaptureMs = 5000;

        private static readonly byte[] Pc12Hello = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };

        private static readonly byte[] KnownHelloResponse = new byte[]
        {
            0xC0, 0x01, 0x09, 0x35
        };

        private static readonly byte[] Pc12Stage2 = new byte[]
        {
            0xF0, 0x00, 0x0F
        };

        public TP02PgLinkV37Form()
        {
            Text = "OpenLadder Studio - Link PG TP02 v0.37";
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
            header.Controls.Add(LabelAt("v0.37 - segundo estagio PG controlado", 9.0f, FontStyle.Regular, TextSecondary, 24, 44));

            stateLabel = new Label();
            stateLabel.Text = "●  NAO TESTADO";
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 350;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            stateLabel.ForeColor = TextSecondary;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 226;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Comunicacao PG validada fisicamente", 11.0f, FontStyle.Bold, TextPrimary, 18, 12));
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
            profileLabel.Text = "CONFIRMADO: 19200 8O1 · DTR/RTS on";
            profileLabel.AutoSize = true;
            profileLabel.Location = new Point(274, 68);
            profileLabel.ForeColor = Success;
            profileLabel.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            config.Controls.Add(profileLabel);

            testButton = ButtonAt("TESTAR ETAPA 2 PG", 790, 56, 250, true);
            testButton.Click += delegate { StartTest(); };
            config.Controls.Add(testButton);

            Button clear = ButtonAt("LIMPAR", 1050, 56, 92, false);
            clear.Click += delegate { if (!running && logBox != null) logBox.Clear(); };
            config.Controls.Add(clear);

            config.Controls.Add(LabelAt("1º TX: 43 4F 4E 2D 49 43 42 0D = CON-ICB<CR>", 8.5f, FontStyle.Bold, Navy, 18, 108));
            config.Controls.Add(LabelAt("RX exigido antes de avancar: C0 01 09 35 · soma modulo 256 = FF", 8.5f, FontStyle.Bold, Success, 18, 132));
            config.Controls.Add(LabelAt("2º TX: F0 00 0F · soma modulo 256 = FF · enviado UMA unica vez apos o handshake exato", 8.5f, FontStyle.Bold, Navy, 18, 156));

            Label safety = new Label();
            safety.Text = "MODO SEGURO: se C0 01 09 35 nao for confirmado, F0 00 0F NAO e enviado. Depois de F0 00 0F, a ferramenta somente escuta e registra bytes. RUN, STOP, escrita, download e apagamento continuam bloqueados.";
            safety.AutoSize = false;
            safety.Location = new Point(18, 184);
            safety.Size = new Size(1110, 38);
            safety.ForeColor = Danger;
            safety.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            config.Controls.Add(safety);

            Panel info = new Panel();
            info.Dock = DockStyle.Top;
            info.Height = 110;
            info.BackColor = Canvas;
            Controls.Add(info);

            info.Controls.Add(LabelAt("Diagnostico v0.37", 10.0f, FontStyle.Bold, TextPrimary, 18, 13));
            Label explanation = new Label();
            explanation.Text = "1. Abre COM em 19200/8O1 com DTR/RTS on.  2. Confirma o HELLO conhecido.  3. Somente apos C0 01 09 35 envia F0 00 0F uma vez.  4. Registra RX bruto, remove apenas eco exato de F0 00 0F e calcula a soma.  5. Mantem escuta passiva por mais 5 s sem qualquer novo TX.";
            explanation.AutoSize = false;
            explanation.Location = new Point(18, 40);
            explanation.Size = new Size(1110, 58);
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

        private void StartTest()
        {
            if (running) return;
            if (portCombo.SelectedItem == null)
            {
                Warn("Selecione a porta COM usada pelo TP02.");
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            cancelRequested = false;
            running = true;
            testButton.Enabled = false;
            logBox.Clear();
            SetState("●  CONFIRMANDO LINK PG...", Warning);

            Log("INFO", "Perfil fisicamente confirmado: 19200 8O1 · DTR/RTS on.");
            Log("INFO", "F0 00 0F so sera enviado se C0 01 09 35 for recebido nesta execucao.");
            Log("INFO", "Depois do segundo quadro nenhum outro TX sera realizado.");

            Thread worker = new Thread(new ThreadStart(delegate { TestWorker(portName); }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void TestWorker(string portName)
        {
            SerialPort port = null;
            bool handshakeConfirmed = false;
            bool stage2Sent = false;
            bool stage2Received = false;
            string lastError = string.Empty;

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
                Thread.Sleep(120);

                LogSafe("PORTA", portName + "  19200 8O1  DTR=on  RTS=on");

                for (int attempt = 1; attempt <= 4 && !cancelRequested; attempt++)
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

                    TP02PgFrameParserV33.ParseResult parsed = TP02PgFrameParserV33.Parse(raw, Pc12Hello);
                    LogSafe("PG HELLO RX RAW", ToHex(parsed.Raw) + "  soma=0x" + parsed.RawSum.ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("PG ECO", "quantidade=" + parsed.EchoCount.ToString(CultureInfo.InvariantCulture));
                    LogSafe("PG RX SEM ECO", ToHex(parsed.WithoutEcho) + "  soma=0x" + parsed.WithoutEchoSum.ToString("X2", CultureInfo.InvariantCulture));

                    int knownIndex = IndexOfSequence(parsed.WithoutEcho, KnownHelloResponse);
                    if (knownIndex < 0)
                    {
                        LogSafe("PG BLOQUEIO", "C0 01 09 35 nao foi localizado; F0 00 0F permanece bloqueado.");
                        Thread.Sleep(120);
                        continue;
                    }

                    handshakeConfirmed = true;
                    LogSafe("PG FRAME", ToHex(KnownHelloResponse));
                    LogSafe("PG CHECKSUM", "HELLO RX soma modulo 256 = 0x" + Sum8(KnownHelloResponse).ToString("X2", CultureInfo.InvariantCulture));
                    LogSafe("PG LINK", "ESTABLISHED - C0 01 09 35 confirmado nesta execucao.");
                    SaveProfile(portName);
                    SetState("●  LINK CONFIRMADO · ENVIANDO ETAPA 2...", Success);

                    int inlineOffset = knownIndex + KnownHelloResponse.Length;
                    if (inlineOffset < parsed.WithoutEcho.Length)
                    {
                        byte[] inline = Slice(parsed.WithoutEcho, inlineOffset, parsed.WithoutEcho.Length - inlineOffset);
                        if (inline.Length > 0)
                            LogSafe("PG HELLO EXTRA", ToHex(inline) + "  soma=0x" + Sum8(inline).ToString("X2", CultureInfo.InvariantCulture));
                    }

                    Thread.Sleep(140);
                    LogSafe("PG STAGE2 TX", ToHex(Pc12Stage2) + "  soma=0x" + Sum8(Pc12Stage2).ToString("X2", CultureInfo.InvariantCulture));
                    port.Write(Pc12Stage2, 0, Pc12Stage2.Length);
                    stage2Sent = true;
                    SetState("●  ETAPA 2 ENVIADA · CAPTURANDO RX...", Success);

                    byte[] stage2Raw = ReadBurst(port, Stage2TimeoutMs, 240);
                    if (stage2Raw.Length == 0)
                    {
                        LogSafe("PG STAGE2 RX", "[]");
                        LogSafe("PG DIAG", "nenhum byte retornou imediatamente apos F0 00 0F.");
                    }
                    else
                    {
                        stage2Received = true;
                        int echoCount;
                        byte[] withoutEcho = RemoveLeadingExactEcho(stage2Raw, Pc12Stage2, out echoCount);
                        LogSafe("PG STAGE2 RX RAW", ToHex(stage2Raw) + "  soma=0x" + Sum8(stage2Raw).ToString("X2", CultureInfo.InvariantCulture));
                        LogSafe("PG STAGE2 ECO", "quantidade=" + echoCount.ToString(CultureInfo.InvariantCulture));
                        LogSafe("PG STAGE2 SEM ECO", ToHex(withoutEcho) + "  soma=0x" + Sum8(withoutEcho).ToString("X2", CultureInfo.InvariantCulture));
                        if (withoutEcho.Length > 0 && Sum8(withoutEcho) == 0xFF)
                            LogSafe("PG STAGE2 FRAME?", "o bloco sem eco fecha soma FF; manter interpretacao em aberto ate comparar com o PC12.");
                        else if (withoutEcho.Length > 0)
                            LogSafe("PG STAGE2 FRAME?", "RX registrado sem assumir enquadramento; pode conter um ou mais quadros/bytes de estado.");
                    }

                    int postBursts = CaptureAfterStage2(port);
                    FinishSafe(handshakeConfirmed, stage2Sent, stage2Received, postBursts, string.Empty);
                    return;
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

            if (cancelRequested) FinishCancelled();
            else FinishSafe(handshakeConfirmed, stage2Sent, stage2Received, 0, lastError);
        }

        private int CaptureAfterStage2(SerialPort port)
        {
            int bursts = 0;
            DateTime start = DateTime.UtcNow;
            DateTime deadline = start.AddMilliseconds(PostStage2CaptureMs);
            LogSafe("PG CAPTURE", "escuta passiva pos-F0 iniciada; nenhum novo byte sera transmitido.");

            while (DateTime.UtcNow < deadline && !cancelRequested)
            {
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0) break;
                int window = remaining > 500 ? 500 : remaining;
                if (window < 80) window = 80;

                byte[] burst = ReadBurst(port, window, 160);
                if (burst.Length == 0) continue;

                bursts++;
                int elapsed = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                LogSafe("PG POST-F0 RX", "+" + elapsed.ToString(CultureInfo.InvariantCulture) + " ms  " + ToHex(burst) + "  soma=0x" + Sum8(burst).ToString("X2", CultureInfo.InvariantCulture));
            }

            if (bursts == 0)
                LogSafe("PG CAPTURE", "nenhum byte adicional nos " + PostStage2CaptureMs.ToString(CultureInfo.InvariantCulture) + " ms posteriores.");
            else
                LogSafe("PG CAPTURE", bursts.ToString(CultureInfo.InvariantCulture) + " burst(s) adicional(is) registrado(s).");
            return bursts;
        }

        private void FinishSafe(bool handshakeConfirmed, bool stage2Sent, bool stage2Received, int postBursts, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { FinishSafe(handshakeConfirmed, stage2Sent, stage2Received, postBursts, error); }));
                return;
            }

            running = false;
            testButton.Enabled = true;

            if (!handshakeConfirmed)
            {
                SetState("●  HANDSHAKE NAO CONFIRMADO", Danger);
                Log("RESULTADO", "C0 01 09 35 nao foi confirmado nesta execucao; F0 00 0F NAO foi enviado.");
                if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
                return;
            }

            if (!stage2Sent)
            {
                SetState("●  LINK OK · ETAPA 2 NAO ENVIADA", Warning);
                Log("RESULTADO", "Link PG confirmado, mas a etapa 2 nao chegou a ser transmitida.");
                if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
                return;
            }

            SetState("●  ETAPA 2 PG CONCLUIDA", Success);
            Log("RESULTADO", "HELLO confirmado e F0 00 0F transmitido exatamente uma vez.");
            if (stage2Received)
                Log("RESULTADO", "Ha RX posterior a F0 00 0F no log; este e o dado principal para a proxima decodificacao.");
            else
                Log("RESULTADO", "Nao houve RX imediato apos F0 00 0F.");
            if (postBursts > 0)
                Log("RESULTADO", "Tambem houve " + postBursts.ToString(CultureInfo.InvariantCulture) + " burst(s) durante a escuta passiva posterior.");
            Log("RESULTADO", "Nenhum terceiro comando foi transmitido. RUN/STOP/escrita/download/apagamento continuam bloqueados.");
            if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
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

        private void SaveProfile(string portName)
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

        private static byte[] RemoveLeadingExactEcho(byte[] raw, byte[] command, out int echoCount)
        {
            echoCount = 0;
            if (raw == null || raw.Length == 0 || command == null || command.Length == 0) return raw ?? new byte[0];

            int offset = 0;
            while (offset + command.Length <= raw.Length)
            {
                bool same = true;
                for (int i = 0; i < command.Length; i++)
                {
                    if (raw[offset + i] != command[i]) { same = false; break; }
                }
                if (!same) break;
                echoCount++;
                offset += command.Length;
            }

            return Slice(raw, offset, raw.Length - offset);
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

        private static int Sum8(byte[] bytes)
        {
            int sum = 0;
            if (bytes == null) return 0;
            for (int i = 0; i < bytes.Length; i++) sum = (sum + bytes[i]) & 0xFF;
            return sum;
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

    internal static class TP02PgLinkV37Program
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppBranding.Install();
            Application.Run(new TP02PgLinkV37Form());
        }
    }
}
