using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    /// <summary>
    /// Leitura READ-ONLY do programa do WEG TP02 pelo protocolo PG/PC12.
    /// Fluxo fisicamente confirmado: HELLO -> F0 -> 38 -> 34.
    /// Nenhum comando de escrita, RUN, STOP ou limpeza existe nesta classe.
    /// </summary>
    internal sealed class TP02PgProgramReadForm : Form
    {
        private sealed class PgReadResult
        {
            public readonly List<string> IlLines = new List<string>();
            public readonly List<string> WordLines = new List<string>();
            public string Hello = string.Empty;
            public string PlcState = string.Empty;
            public string Frame38 = string.Empty;
            public int Pages;
            public int Steps;
            public int EndStep = -1;
            public bool CrossedPageBoundary;
        }

        private readonly PlcDeviceProfile profile;
        private ComboBox portCombo;
        private Button refreshButton;
        private Button readButton;
        private Button openFolderButton;
        private Label statusLabel;
        private TextBox programBox;
        private TextBox logBox;
        private bool busy;
        private string sessionDirectory;
        private string sessionLogPath;

        private readonly Color Shell = Color.FromArgb(18, 24, 31);
        private readonly Color Chrome = Color.FromArgb(27, 36, 46);
        private readonly Color Border = Color.FromArgb(55, 68, 82);
        private readonly Color Accent = Color.FromArgb(38, 166, 154);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(158, 169, 180);
        private readonly Color Warning = Color.FromArgb(224, 170, 64);
        private readonly Color Danger = Color.FromArgb(214, 87, 87);
        private readonly Color Success = Color.FromArgb(74, 190, 119);

        private static readonly byte[] PgHello = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };

        private static readonly byte[] HelloRun = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
        private static readonly byte[] HelloStop = new byte[] { 0x80, 0x01, 0x09, 0x75 };
        private static readonly byte[] F0Request = new byte[] { 0xF0, 0x00, 0x0F };
        private static readonly byte[] F0Response = new byte[] { 0x00, 0x02, 0x10, 0x22, 0xCB };
        private static readonly byte[] Frame38Request = new byte[] { 0x38, 0x00, 0xC7 };

        public TP02PgProgramReadForm(PlcDeviceProfile profile)
        {
            this.profile = profile;
            Text = "Leitura de programa PG/PC12 - WEG TP02";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(980, 700);
            Size = new Size(1160, 820);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            CreateSessionDirectory();
            BuildUi();
            RefreshPorts();
            LoadPreferredPort();
            AppendLog("Ferramenta READ-ONLY criada. Nenhum comando de escrita e implementado aqui.");
            AppendLog("Fluxo: HELLO -> F0 -> 38 -> 34, mantendo a COM aberta em 19200 8O1 DTR=off RTS=off.");
        }

        private void CreateSessionDirectory()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpenLadder Studio", "TP02 PG Reads");
            sessionDirectory = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(sessionDirectory);
            sessionLogPath = Path.Combine(sessionDirectory, "session.log");
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 88;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = NewLabel("TP02 - LEITURA DE PROGRAMA PG/PC12", 14.0f, FontStyle.Bold, Fore);
            title.Location = new Point(20, 13);
            header.Controls.Add(title);

            Label sub = NewLabel("SOMENTE LEITURA | 19200 8O1 | DTR=OFF | RTS=OFF | HELLO -> F0 -> 38 -> 34", 8.8f, FontStyle.Regular, Muted);
            sub.Location = new Point(22, 47);
            header.Controls.Add(sub);

            statusLabel = NewLabel("AGUARDANDO", 9.0f, FontStyle.Bold, Muted);
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.Dock = DockStyle.Right;
            statusLabel.Width = 260;
            header.Controls.Add(statusLabel);

            Panel setup = new Panel();
            setup.Dock = DockStyle.Top;
            setup.Height = 92;
            setup.Padding = new Padding(18, 10, 18, 8);
            Controls.Add(setup);

            Label portLabel = NewLabel("Porta COM", 8.0f, FontStyle.Bold, Muted);
            portLabel.Location = new Point(18, 12);
            setup.Controls.Add(portLabel);

            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Location = new Point(18, 34);
            portCombo.Size = new Size(145, 25);
            setup.Controls.Add(portCombo);

            refreshButton = NewButton("ATUALIZAR", 176, 32, 105, false);
            refreshButton.Click += delegate { RefreshPorts(); };
            setup.Controls.Add(refreshButton);

            readButton = NewButton("LER PROGRAMA PG", 310, 25, 190, true);
            readButton.Click += delegate { StartRead(); };
            setup.Controls.Add(readButton);

            openFolderButton = NewButton("ABRIR PASTA", 518, 25, 130, false);
            openFolderButton.Click += delegate { OpenSessionFolder(); };
            setup.Controls.Add(openFolderButton);

            Label note = NewLabel("Se o programa ultrapassar 80 passos, a leitura continua em paginas de 80 e o log marca a fronteira como ainda experimental.", 8.3f, FontStyle.Regular, Warning);
            note.Location = new Point(680, 36);
            note.MaximumSize = new Size(430, 42);
            setup.Controls.Add(note);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI", 9.0f);
            Controls.Add(tabs);
            tabs.BringToFront();

            TabPage programTab = new TabPage("Programa decodificado");
            programTab.BackColor = Shell;
            tabs.TabPages.Add(programTab);

            programBox = NewTextBox();
            programBox.Dock = DockStyle.Fill;
            programTab.Controls.Add(programBox);

            TabPage logTab = new TabPage("Log bruto");
            logTab.BackColor = Shell;
            tabs.TabPages.Add(logTab);

            logBox = NewTextBox();
            logBox.Dock = DockStyle.Fill;
            logTab.Controls.Add(logBox);
        }

        private TextBox NewTextBox()
        {
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.WordWrap = false;
            box.ScrollBars = ScrollBars.Both;
            box.Font = new Font("Consolas", 9.0f);
            box.BackColor = Color.FromArgb(16, 22, 29);
            box.ForeColor = Fore;
            box.BorderStyle = BorderStyle.FixedSingle;
            return box;
        }

        private Label NewLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", size, style);
            label.ForeColor = color;
            return label;
        }

        private Button NewButton(string text, int left, int top, int width, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(left, top);
            button.Size = new Size(width, 38);
            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            if (primary)
            {
                button.BackColor = Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderSize = 0;
            }
            else
            {
                button.BackColor = Chrome;
                button.ForeColor = Fore;
                button.FlatAppearance.BorderColor = Border;
            }
            return button;
        }

        private void RefreshPorts()
        {
            string selected = portCombo == null || portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);
            portCombo.Items.Clear();
            for (int i = 0; i < ports.Length; i++) portCombo.Items.Add(ports[i]);
            if (!string.IsNullOrEmpty(selected) && portCombo.Items.Contains(selected))
                portCombo.SelectedItem = selected;
            else if (portCombo.Items.Count > 0)
                portCombo.SelectedIndex = 0;
        }

        private void LoadPreferredPort()
        {
            try
            {
                PlcConnectionSettings settings = PlcConnectionSettingsStore.Load(profile);
                if (settings != null && !string.IsNullOrEmpty(settings.PortName))
                {
                    if (!portCombo.Items.Contains(settings.PortName)) portCombo.Items.Add(settings.PortName);
                    portCombo.SelectedItem = settings.PortName;
                }
            }
            catch { }
        }

        private void StartRead()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.", "TP02 PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            SetBusy(true);
            programBox.Clear();
            AppendLog(new string('-', 78));
            AppendLog("LEITURA PG iniciada em " + portName + ".");
            SetStatusSafe("LENDO...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                PgReadResult result = null;
                Exception failure = null;
                try { result = ReadProgramWithSessionRetry(portName); }
                catch (Exception ex) { failure = ex; }

                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FALHA: " + failure.Message);
                        SetStatusSafe("FALHA", Danger);
                        MessageBox.Show(this, failure.Message, "TP02 - leitura PG falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        ShowProgram(result);
                        SetStatusSafe("LEITURA PG APROVADA", Success);
                        string extra = result.CrossedPageBoundary
                            ? "\r\n\r\nA leitura cruzou a fronteira de 80 passos. Os dados foram salvos, mas essa paginacao ainda deve ser confirmada fisicamente por comparacao."
                            : string.Empty;
                        MessageBox.Show(this,
                            "Programa lido pelo PG/PC12.\r\n\r\n"
                            + "Estado HELLO: " + result.PlcState + "\r\n"
                            + "Passos ate END: " + result.Steps.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "END global: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture) + "\r\n"
                            + "Paginas 34: " + result.Pages.ToString(CultureInfo.InvariantCulture) + extra,
                            "TP02 - LEITURA PG APROVADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SetBusy(false);
                }));
            });
        }

        private PgReadResult ReadProgramWithSessionRetry(string portName)
        {
            Exception last = null;
            for (int session = 1; session <= 2; session++)
            {
                try
                {
                    if (session == 2)
                    {
                        AppendLogSafe("PG SESSION RETRY: reabrindo a COM em DTR=off/RTS=off.");
                        Thread.Sleep(1000);
                    }
                    return ReadProgramSession(portName, session);
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("PG sessao " + session.ToString(CultureInfo.InvariantCulture) + " falhou: " + ex.Message);
                }
            }
            throw new IOException("Duas sessoes PG falharam. Ultimo erro: " + (last == null ? "desconhecido" : last.Message));
        }

        private PgReadResult ReadProgramSession(string portName, int session)
        {
            PgReadResult result = new PgReadResult();
            SerialPort port = null;
            try
            {
                port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                port.Handshake = Handshake.None;
                port.DtrEnable = false;
                port.RtsEnable = false;
                port.ReadTimeout = 80;
                port.WriteTimeout = 1200;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
                Thread.Sleep(session == 1 ? 850 : 1200);
                AppendLogSafe("PORTA: " + portName + " 19200 8O1 DTR=off RTS=off / sessao " + session.ToString(CultureInfo.InvariantCulture));

                result.Hello = PerformHello(port);
                result.PlcState = result.Hello.StartsWith("80", StringComparison.Ordinal) ? "STOP" : "RUN";
                AppendLogSafe("HELLO confirmado: " + result.Hello + " -> " + result.PlcState + ".");

                byte[] f0Raw = SendAndReadBurst(port, F0Request, 2800, 220, "F0");
                if (!Contains(f0Raw, F0Response))
                    throw new InvalidDataException("F0 nao retornou o quadro conhecido 00 02 10 22 CB.");
                SaveHex("f0-response.hex", F0Response);
                AppendLogSafe("F0 RX conhecido confirmado: 00 02 10 22 CB.");

                byte[] frame38 = SendAndReadFrame(port, Frame38Request, 0x02, 2, 3200, "38");
                result.Frame38 = ToHex(frame38);
                SaveHex("frame38.hex", frame38);
                AppendLogSafe("38 RX: " + result.Frame38 + ".");

                int startStep = 0;
                bool foundEnd = false;
                while (startStep < Tp02Pg34Pager.MaxProgramSteps)
                {
                    byte[] request34 = Tp02Pg34Pager.BuildReadRequest(startStep);
                    if (startStep > 0)
                    {
                        result.CrossedPageBoundary = true;
                        AppendLogSafe("PAGINACAO EXPERIMENTAL: solicitando pagina a partir do passo " + startStep.ToString("0000", CultureInfo.InvariantCulture) + ".");
                    }

                    byte[] frame34 = SendAndReadFrame(port, request34, Tp02Pg34Pager.PayloadLength, 3, 4500, "34@" + startStep.ToString("0000", CultureInfo.InvariantCulture));
                    result.Pages++;
                    SaveHex("page-" + startStep.ToString("0000", CultureInfo.InvariantCulture) + ".hex", frame34);

                    byte[] hint = startStep == 0 ? frame38 : null;
                    Tp02Pg34DecodeResult decoded = Tp02Pg34Decoder.Decode(frame34, hint);
                    if (!decoded.IsValid)
                        throw new InvalidDataException("Decoder 34 falhou na pagina " + startStep.ToString("0000", CultureInfo.InvariantCulture) + ": " + decoded.Error);

                    int localEnd;
                    bool hasEnd = Tp02Pg34Pager.TryFindEnd(frame34, out localEnd);
                    int count = decoded.Steps.Count;
                    if (hasEnd && localEnd + 1 < count) count = localEnd + 1;

                    AppendLogSafe("34 pagina " + startStep.ToString("0000", CultureInfo.InvariantCulture)
                        + ": passos ativos=" + decoded.Steps.Count.ToString(CultureInfo.InvariantCulture)
                        + ", unknown=" + decoded.UnknownSteps.ToString(CultureInfo.InvariantCulture)
                        + ", BRAW mismatch=" + decoded.BrawMismatches.ToString(CultureInfo.InvariantCulture) + ".");

                    for (int i = 0; i < count; i++)
                    {
                        Tp02Pg34Step step = decoded.Steps[i];
                        int global = startStep + i;
                        string word = step.High.ToString("X2", CultureInfo.InvariantCulture)
                            + step.Low.ToString("X2", CultureInfo.InvariantCulture)
                            + step.Braw.ToString("X2", CultureInfo.InvariantCulture);
                        result.WordLines.Add(word);
                        result.IlLines.Add(global.ToString("0000", CultureInfo.InvariantCulture)
                            + "  " + word + "  " + step.ToIl());
                        result.Steps++;
                    }

                    if (hasEnd)
                    {
                        result.EndStep = Tp02Pg34Pager.GlobalStep(startStep, localEnd);
                        foundEnd = true;
                        AppendLogSafe("F-00 END encontrado no passo global " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");
                        break;
                    }

                    if (decoded.Steps.Count < Tp02Pg34Pager.StepsPerPage)
                        throw new InvalidDataException("Pagina parcial sem F-00 END; leitura interrompida para nao inferir o fim do programa.");

                    startStep = Tp02Pg34Pager.NextStartStep(startStep);
                }

                if (!foundEnd)
                    throw new InvalidDataException("F-00 END nao encontrado antes do limite de 4000 passos.");

                SaveProgramFiles(result);
                return result;
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

        private string PerformHello(SerialPort port)
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                port.DiscardInBuffer();
                AppendLogSafe("HELLO TX " + attempt.ToString(CultureInfo.InvariantCulture) + ": " + ToHex(PgHello));
                port.Write(PgHello, 0, PgHello.Length);
                byte[] raw = ReadBurst(port, attempt == 1 ? 2200 : 2600, 220);
                AppendLogSafe("HELLO RX: " + (raw.Length == 0 ? "[]" : ToHex(raw)));
                if (Contains(raw, HelloStop)) return ToHex(HelloStop);
                if (Contains(raw, HelloRun)) return ToHex(HelloRun);
                Thread.Sleep(260);
            }
            throw new TimeoutException("HELLO PG nao confirmado em 5 tentativas.");
        }

        private byte[] SendAndReadBurst(SerialPort port, byte[] request, int timeoutMs, int quietMs, string label)
        {
            port.DiscardInBuffer();
            AppendLogSafe(label + " TX: " + ToHex(request));
            port.Write(request, 0, request.Length);
            byte[] raw = ReadBurst(port, timeoutMs, quietMs);
            AppendLogSafe(label + " RX RAW: " + (raw.Length == 0 ? "[]" : ToHex(raw)));
            return raw;
        }

        private byte[] SendAndReadFrame(SerialPort port, byte[] request, int expectedLenByte, int attempts, int timeoutMs, string label)
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                byte[] raw = SendAndReadBurst(port, request, timeoutMs, 240, label + " #" + attempt.ToString(CultureInfo.InvariantCulture));
                byte[] frame = FindFrame(raw, expectedLenByte);
                if (frame != null)
                {
                    AppendLogSafe(label + " FRAME: " + ToHex(frame));
                    return frame;
                }
                Thread.Sleep(260);
            }
            throw new InvalidDataException(label + " nao retornou quadro valido LEN=" + expectedLenByte.ToString("X2", CultureInfo.InvariantCulture) + " / checksum FF.");
        }

        private static byte[] FindFrame(byte[] raw, int expectedLenByte)
        {
            if (raw == null) return null;
            int total = expectedLenByte + 3;
            if (total < 3 || raw.Length < total) return null;

            for (int i = 0; i <= raw.Length - total; i++)
            {
                if (raw[i + 1] != (byte)expectedLenByte) continue;
                int sum = 0;
                for (int j = 0; j < total; j++) sum = (sum + raw[i + j]) & 0xFF;
                if (sum != 0xFF) continue;
                byte[] frame = new byte[total];
                Buffer.BlockCopy(raw, i, frame, 0, total);
                return frame;
            }
            return null;
        }

        private static byte[] ReadBurst(SerialPort port, int timeoutMs, int quietMs)
        {
            List<byte> bytes = new List<byte>();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            DateTime lastData = DateTime.MinValue;
            while (DateTime.UtcNow < deadline)
            {
                int available = port.BytesToRead;
                if (available > 0)
                {
                    byte[] buffer = new byte[available];
                    int got = port.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < got; i++) bytes.Add(buffer[i]);
                    lastData = DateTime.UtcNow;
                }
                else if (bytes.Count > 0 && lastData != DateTime.MinValue
                    && (DateTime.UtcNow - lastData).TotalMilliseconds >= quietMs)
                {
                    break;
                }
                Thread.Sleep(15);
            }
            return bytes.ToArray();
        }

        private static bool Contains(byte[] value, byte[] sequence)
        {
            if (value == null || sequence == null || sequence.Length == 0 || value.Length < sequence.Length) return false;
            for (int i = 0; i <= value.Length - sequence.Length; i++)
            {
                bool same = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (value[i + j] != sequence[j]) { same = false; break; }
                }
                if (same) return true;
            }
            return false;
        }

        private static string ToHex(byte[] value)
        {
            if (value == null || value.Length == 0) return string.Empty;
            StringBuilder text = new StringBuilder(value.Length * 3);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(value[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private void SaveHex(string name, byte[] frame)
        {
            File.WriteAllText(Path.Combine(sessionDirectory, name), ToHex(frame) + Environment.NewLine, Encoding.ASCII);
        }

        private void SaveProgramFiles(PgReadResult result)
        {
            File.WriteAllLines(Path.Combine(sessionDirectory, "program-words.txt"), result.WordLines.ToArray(), Encoding.ASCII);
            File.WriteAllLines(Path.Combine(sessionDirectory, "program-il.txt"), result.IlLines.ToArray(), Encoding.UTF8);

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("OpenLadder Studio - TP02 PG/PC12 READ-ONLY");
            summary.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            summary.AppendLine("Perfil: 19200 8O1 DTR=off RTS=off");
            summary.AppendLine("HELLO: " + result.Hello + " (" + result.PlcState + ")");
            summary.AppendLine("38: " + result.Frame38);
            summary.AppendLine("Paginas 34: " + result.Pages.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("Passos: " + result.Steps.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("END: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture));
            summary.AppendLine("Cruzou 80 passos: " + (result.CrossedPageBoundary ? "SIM - paginacao ainda experimental" : "NAO"));
            summary.AppendLine("Seguranca: nenhum comando de escrita/RUN/STOP/limpeza foi enviado.");
            File.WriteAllText(Path.Combine(sessionDirectory, "read-summary.txt"), summary.ToString(), Encoding.UTF8);
        }

        private void ShowProgram(PgReadResult result)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("TP02 PG/PC12 - PROGRAMA LIDO");
            text.AppendLine("HELLO: " + result.Hello + "  Estado: " + result.PlcState);
            text.AppendLine("Passos: " + result.Steps.ToString(CultureInfo.InvariantCulture)
                + "  END: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture)
                + "  Paginas: " + result.Pages.ToString(CultureInfo.InvariantCulture));
            if (result.CrossedPageBoundary)
                text.AppendLine("ATENCAO: leitura cruzou 80 passos; paginacao permanece experimental ate validacao cruzada.");
            text.AppendLine(new string('-', 86));
            text.AppendLine("PASSO  WORD    IL");
            text.AppendLine("-----  ------  ------------------------------------------------------------");
            for (int i = 0; i < result.IlLines.Count; i++) text.AppendLine(result.IlLines[i]);
            programBox.Text = text.ToString();
            programBox.SelectionStart = 0;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            portCombo.Enabled = !value;
            refreshButton.Enabled = !value;
            readButton.Enabled = !value;
            openFolderButton.Enabled = !value;
        }

        private void SetStatusSafe(string text, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { SetStatusSafe(text, color); }));
                return;
            }
            statusLabel.Text = text;
            statusLabel.ForeColor = color;
        }

        private void AppendLog(string text)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + text;
            if (logBox != null)
            {
                logBox.AppendText(line + Environment.NewLine);
                logBox.SelectionStart = logBox.TextLength;
                logBox.ScrollToCaret();
            }
            try { File.AppendAllText(sessionLogPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        private void AppendLogSafe(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { AppendLog(text); }));
                return;
            }
            AppendLog(text);
        }

        private void OpenSessionFolder()
        {
            try { System.Diagnostics.Process.Start("explorer.exe", sessionDirectory); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Abrir pasta", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
