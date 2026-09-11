using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class TP02RbpProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppBranding.Install();
            Application.Run(new TP02ProgramReaderForm());
        }
    }

    // Leitor PG/PC12 estritamente READ-ONLY.
    // Unicos comandos transmitidos: HELLO (CON-ICB), F0, 38 e 34.
    internal sealed class TP02ProgramReaderForm : Form
    {
        private sealed class ReadResult
        {
            public string Hello = string.Empty;
            public string PlcState = string.Empty;
            public string Frame38 = string.Empty;
            public int Pages;
            public int Steps;
            public int EndStep = -1;
            public int UnknownSteps;
            public int BrawChecked;
            public int BrawMismatches;
            public bool CrossedPageBoundary;
            public readonly List<string> Words = new List<string>();
            public readonly List<string> Il = new List<string>();
        }

        private const int StepsPerPage = 80;
        private const int MaxProgramSteps = 4000;
        private const int PagePayloadLength = 0xF0;
        private const int PageFrameLength = 0xF3;
        private const int PageABLength = 0xA0;

        private static readonly byte[] HelloRequest = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };
        private static readonly byte[] HelloStop = new byte[] { 0x80, 0x01, 0x09, 0x75 };
        private static readonly byte[] HelloRun = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
        private static readonly byte[] F0Request = new byte[] { 0xF0, 0x00, 0x0F };
        private static readonly byte[] F0Response = new byte[] { 0x00, 0x02, 0x10, 0x22, 0xCB };
        private static readonly byte[] Frame38Request = new byte[] { 0x38, 0x00, 0xC7 };

        private ComboBox portCombo;
        private Button readButton;
        private Button refreshButton;
        private Button folderButton;
        private TextBox programBox;
        private TextBox logBox;
        private Label statusLabel;
        private Label sessionLabel;
        private bool busy;
        private string sessionDirectory = string.Empty;
        private string sessionLogPath = string.Empty;

        private readonly Color Shell = Color.FromArgb(18, 24, 31);
        private readonly Color Chrome = Color.FromArgb(27, 36, 46);
        private readonly Color Border = Color.FromArgb(55, 68, 82);
        private readonly Color Accent = Color.FromArgb(38, 166, 154);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(158, 169, 180);
        private readonly Color Warning = Color.FromArgb(224, 170, 64);
        private readonly Color Danger = Color.FromArgb(214, 87, 87);
        private readonly Color Success = Color.FromArgb(74, 190, 119);

        public TP02ProgramReaderForm()
        {
            Text = "TP02 - Leitura de programa PG/PC12";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 650);
            Size = new Size(1160, 780);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            RefreshPorts();
            LoadPreferredPort();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 86;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = LabelAt("TP02 - LEITURA DE PROGRAMA PG/PC12", 14.0f, FontStyle.Bold, Fore, 20, 13);
            header.Controls.Add(title);
            Label sub = LabelAt("SOMENTE LEITURA | TP-232PG | 19200 8O1 | DTR=OFF | RTS=OFF | HELLO -> F0 -> 38 -> 34",
                8.8f, FontStyle.Regular, Muted, 22, 47);
            header.Controls.Add(sub);

            statusLabel = LabelAt("AGUARDANDO", 9.0f, FontStyle.Bold, Muted, 0, 0);
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.Dock = DockStyle.Right;
            statusLabel.Width = 260;
            header.Controls.Add(statusLabel);

            Panel command = new Panel();
            command.Dock = DockStyle.Top;
            command.Height = 116;
            command.BackColor = Shell;
            Controls.Add(command);

            command.Controls.Add(LabelAt("Porta COM", 8.0f, FontStyle.Bold, Muted, 18, 13));
            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Location = new Point(18, 36);
            portCombo.Size = new Size(145, 25);
            command.Controls.Add(portCombo);

            refreshButton = ButtonAt("ATUALIZAR", 176, 34, 105, false);
            refreshButton.Click += delegate { RefreshPorts(); };
            command.Controls.Add(refreshButton);

            readButton = ButtonAt("LER PROGRAMA PG", 310, 27, 190, true);
            readButton.Click += delegate { StartRead(); };
            command.Controls.Add(readButton);

            folderButton = ButtonAt("ABRIR PASTA", 520, 27, 130, false);
            folderButton.Enabled = false;
            folderButton.Click += delegate { OpenSessionFolder(); };
            command.Controls.Add(folderButton);

            Label safe = LabelAt("Nao envia RUN, STOP, limpeza ou escrita. A v1.10 faz pre-estabilizacao segura e retries automaticos.",
                8.3f, FontStyle.Regular, Warning, 680, 35);
            safe.MaximumSize = new Size(430, 42);
            command.Controls.Add(safe);

            sessionLabel = LabelAt("Sessao: -", 8.0f, FontStyle.Regular, Muted, 18, 82);
            sessionLabel.MaximumSize = new Size(1080, 24);
            command.Controls.Add(sessionLabel);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            Controls.Add(tabs);
            tabs.BringToFront();

            TabPage programTab = new TabPage("Programa lido");
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

        private Button ButtonAt(string text, int left, int top, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 38);
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            b.BackColor = primary ? Accent : Chrome;
            b.ForeColor = primary ? Color.White : Fore;
            b.FlatAppearance.BorderColor = Border;
            if (primary) b.FlatAppearance.BorderSize = 0;
            return b;
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

        private void RefreshPorts()
        {
            string selected = portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);
            portCombo.Items.Clear();
            for (int i = 0; i < ports.Length; i++) portCombo.Items.Add(ports[i]);
            if (!string.IsNullOrEmpty(selected) && portCombo.Items.Contains(selected)) portCombo.SelectedItem = selected;
            else if (portCombo.Items.Count > 0) portCombo.SelectedIndex = 0;
        }

        private void LoadPreferredPort()
        {
            try
            {
                PlcDeviceProfile profile = PlcProfileStore.Load();
                if (profile == null) return;
                PlcConnectionSettings settings = PlcConnectionSettingsStore.Load(profile);
                if (settings == null || string.IsNullOrEmpty(settings.PortName)) return;
                if (!portCombo.Items.Contains(settings.PortName)) portCombo.Items.Add(settings.PortName);
                portCombo.SelectedItem = settings.PortName;
            }
            catch { }
        }

        private void StartRead()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.", "TP02 PG",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            CreateSessionDirectory();
            programBox.Clear();
            logBox.Clear();
            AppendLog("Sessao READ-ONLY v1.10 iniciada.");
            AppendLog("Porta: " + portName + " | 19200 8O1 | DTR=off | RTS=off.");
            AppendLog("Seguranca: somente HELLO, F0, 38 e 34 podem ser transmitidos.");
            SetBusy(true);
            SetStatus("LENDO...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                ReadResult result = null;
                Exception failure = null;
                try { result = ReadProgramRobust(portName); }
                catch (Exception ex) { failure = ex; }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FALHA FINAL: " + failure.Message);
                        SetStatus("FALHA", Danger);
                        MessageBox.Show(this, failure.Message, "TP02 - leitura PG falhou",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        ShowProgram(result);
                        SetStatus("LEITURA PG APROVADA", Success);
                        string pageNotice = result.CrossedPageBoundary
                            ? "\r\nATENCAO: leitura cruzou 80 passos; a paginacao acima da primeira pagina ainda e experimental."
                            : string.Empty;
                        MessageBox.Show(this,
                            "Programa lido pelo PG/PC12.\r\n\r\n"
                            + "PLC no HELLO: " + result.PlcState + "\r\n"
                            + "Passos ate F-00 END: " + result.Steps.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "END global: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture) + "\r\n"
                            + "Paginas 34: " + result.Pages.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "BRAW: " + result.BrawChecked.ToString(CultureInfo.InvariantCulture) + " verificado(s), "
                            + result.BrawMismatches.ToString(CultureInfo.InvariantCulture) + " divergencia(s).\r\n"
                            + "UNKNOWN: " + result.UnknownSteps.ToString(CultureInfo.InvariantCulture)
                            + pageNotice,
                            "TP02 - LEITURA PG APROVADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SetBusy(false);
                }));
            });
        }

        private ReadResult ReadProgramRobust(string portName)
        {
            WarmUpLink(portName);
            Exception last = null;
            for (int session = 1; session <= 3; session++)
            {
                if (session > 1)
                {
                    AppendLogSafe("PG AUTO-RETRY: preparando sessao " + session.ToString(CultureInfo.InvariantCulture) + ".");
                    Thread.Sleep(session == 2 ? 1500 : 2000);
                }
                try { return ReadProgramSession(portName, session); }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("Sessao " + session.ToString(CultureInfo.InvariantCulture) + " falhou: " + ex.Message);
                }
            }
            throw new IOException("Tres sessoes PG falharam. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

        private void WarmUpLink(string portName)
        {
            SerialPort port = null;
            try
            {
                AppendLogSafe("PG WARM-UP: abrindo COM para pre-estabilizar TP-232PG.");
                port = OpenPort(portName);
                Thread.Sleep(1400);
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    port.DiscardInBuffer();
                    port.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] raw = ReadBurst(port, 2200, 230);
                    AppendLogSafe("WARM-UP HELLO " + attempt.ToString(CultureInfo.InvariantCulture) + " RX: "
                        + (raw.Length == 0 ? "[]" : ToHex(raw)));
                    if (Contains(raw, HelloStop) || Contains(raw, HelloRun))
                    {
                        AppendLogSafe("PG WARM-UP confirmado; reiniciando a sessao de leitura limpa.");
                        break;
                    }
                    Thread.Sleep(300);
                }
            }
            catch (Exception ex)
            {
                AppendLogSafe("PG WARM-UP nao confirmou link: " + ex.Message + ". O retry completo continuara.");
            }
            finally
            {
                ClosePort(port);
            }
            Thread.Sleep(1100);
        }

        private ReadResult ReadProgramSession(string portName, int sessionNumber)
        {
            ReadResult result = new ReadResult();
            SerialPort port = null;
            try
            {
                port = OpenPort(portName);
                int settle = sessionNumber == 1 ? 1600 : (sessionNumber == 2 ? 1900 : 2300);
                Thread.Sleep(settle);
                AppendLogSafe("COM aberta | sessao " + sessionNumber.ToString(CultureInfo.InvariantCulture)
                    + " | estabilizacao " + settle.ToString(CultureInfo.InvariantCulture) + " ms.");

                result.Hello = PerformHello(port);
                result.PlcState = result.Hello.StartsWith("80", StringComparison.Ordinal) ? "STOP" : "RUN";
                AppendLogSafe("HELLO confirmado: " + result.Hello + " => " + result.PlcState + ".");
                Thread.Sleep(450);

                byte[] f0 = PerformF0(port);
                SaveHex("f0-response.hex", f0);
                Thread.Sleep(420);

                byte[] frame38 = SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600, "38");
                result.Frame38 = ToHex(frame38);
                SaveHex("frame38.hex", frame38);
                AppendLogSafe("38 valido: " + result.Frame38 + ".");
                Thread.Sleep(450);

                bool foundEnd = false;
                int startStep = 0;
                while (startStep < MaxProgramSteps)
                {
                    if (startStep > 0)
                    {
                        result.CrossedPageBoundary = true;
                        AppendLogSafe("PAGINACAO EXPERIMENTAL: passo inicial "
                            + startStep.ToString("0000", CultureInfo.InvariantCulture) + ".");
                    }

                    byte[] frame34 = SendAndReadFrame(port, Build34Request(startStep), PagePayloadLength,
                        4, 5500, "34@" + startStep.ToString("0000", CultureInfo.InvariantCulture));
                    result.Pages++;
                    SaveHex("page-" + startStep.ToString("0000", CultureInfo.InvariantCulture) + ".hex", frame34);

                    int activeCount = DetectPageTailCount(frame34);
                    int localEnd;
                    bool hasEnd = TryFindEnd(frame34, out localEnd);
                    int countToStore = hasEnd ? localEnd + 1 : activeCount;

                    AppendLogSafe("Pagina 34 " + startStep.ToString("0000", CultureInfo.InvariantCulture)
                        + ": ativos=" + activeCount.ToString(CultureInfo.InvariantCulture)
                        + (hasEnd ? ", END local=" + localEnd.ToString("00", CultureInfo.InvariantCulture) : ", END ausente") + ".");

                    for (int i = 0; i < countToStore; i++)
                    {
                        byte high, low, braw;
                        GetStep(frame34, i, out high, out low, out braw);
                        byte expected = CalculateBraw(high, low);
                        bool brawOk = expected == braw;
                        result.BrawChecked++;
                        if (!brawOk) result.BrawMismatches++;

                        string word = high.ToString("X2", CultureInfo.InvariantCulture)
                            + low.ToString("X2", CultureInfo.InvariantCulture)
                            + braw.ToString("X2", CultureInfo.InvariantCulture);
                        string decoded = DecodeStep(high, low, braw);
                        if (decoded.StartsWith("UNKNOWN", StringComparison.Ordinal)) result.UnknownSteps++;
                        int global = startStep + i;
                        result.Words.Add(word);
                        result.Il.Add(global.ToString("0000", CultureInfo.InvariantCulture) + "  " + word + "  " + decoded
                            + (brawOk ? string.Empty : "  [BRAW esperado=" + expected.ToString("X2", CultureInfo.InvariantCulture) + "]"));
                        result.Steps++;
                    }

                    if (hasEnd)
                    {
                        result.EndStep = startStep + localEnd;
                        foundEnd = true;
                        break;
                    }
                    if (activeCount < StepsPerPage)
                        throw new InvalidDataException("Pagina 34 parcial sem F-00 END.");
                    startStep += StepsPerPage;
                    Thread.Sleep(450);
                }

                if (!foundEnd) throw new InvalidDataException("F-00 END nao encontrado antes do limite de 4000 passos.");
                SaveProgramFiles(result);
                AppendLogSafe("LEITURA CONCLUIDA: passos=" + result.Steps.ToString(CultureInfo.InvariantCulture)
                    + ", END=" + result.EndStep.ToString("0000", CultureInfo.InvariantCulture)
                    + ", UNKNOWN=" + result.UnknownSteps.ToString(CultureInfo.InvariantCulture) + ".");
                return result;
            }
            finally { ClosePort(port); }
        }

        private static SerialPort OpenPort(string portName)
        {
            SerialPort port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
            port.Handshake = Handshake.None;
            port.DtrEnable = false;
            port.RtsEnable = false;
            port.ReadTimeout = 100;
            port.WriteTimeout = 1500;
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            return port;
        }

        private static void ClosePort(SerialPort port)
        {
            if (port == null) return;
            try { if (port.IsOpen) port.Close(); } catch { }
            try { port.Dispose(); } catch { }
        }

        private string PerformHello(SerialPort port)
        {
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                port.DiscardInBuffer();
                AppendLogSafe("HELLO TX " + attempt.ToString(CultureInfo.InvariantCulture) + ": " + ToHex(HelloRequest));
                port.Write(HelloRequest, 0, HelloRequest.Length);
                byte[] raw = ReadBurst(port, attempt == 1 ? 2600 : 3000, 240);
                AppendLogSafe("HELLO RX: " + (raw.Length == 0 ? "[]" : ToHex(raw)));
                if (Contains(raw, HelloStop)) return ToHex(HelloStop);
                if (Contains(raw, HelloRun)) return ToHex(HelloRun);
                Thread.Sleep(350);
            }
            throw new TimeoutException("HELLO PG nao confirmado em 6 tentativas.");
        }

        private byte[] PerformF0(SerialPort port)
        {
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                byte[] raw = SendAndReadBurst(port, F0Request, 3600, 250,
                    "F0 #" + attempt.ToString(CultureInfo.InvariantCulture));
                if (Contains(raw, F0Response)) return F0Response;
                Thread.Sleep(350);
            }
            throw new InvalidDataException("F0 nao retornou 00 02 10 22 CB.");
        }

        private byte[] SendAndReadFrame(SerialPort port, byte[] request, int expectedLen,
            int attempts, int timeoutMs, string label)
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                byte[] raw = SendAndReadBurst(port, request, timeoutMs, 280,
                    label + " #" + attempt.ToString(CultureInfo.InvariantCulture));
                byte[] frame = FindFrame(raw, expectedLen);
                if (frame != null)
                {
                    AppendLogSafe(label + " FRAME OK: " + ToHex(frame) + ".");
                    return frame;
                }
                Thread.Sleep(380);
            }
            throw new InvalidDataException(label + " nao retornou quadro valido LEN="
                + expectedLen.ToString("X2", CultureInfo.InvariantCulture) + " / checksum FF.");
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

        private static byte[] ReadBurst(SerialPort port, int timeoutMs, int quietMs)
        {
            List<byte> bytes = new List<byte>();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            DateTime last = DateTime.MinValue;
            while (DateTime.UtcNow < deadline)
            {
                int available = port.BytesToRead;
                if (available > 0)
                {
                    byte[] buffer = new byte[available];
                    int got = port.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < got; i++) bytes.Add(buffer[i]);
                    last = DateTime.UtcNow;
                }
                else if (bytes.Count > 0 && last != DateTime.MinValue
                    && (DateTime.UtcNow - last).TotalMilliseconds >= quietMs) break;
                Thread.Sleep(15);
            }
            return bytes.ToArray();
        }

        private static byte[] FindFrame(byte[] raw, int expectedLen)
        {
            if (raw == null) return null;
            int total = expectedLen + 3;
            if (raw.Length < total) return null;
            for (int i = 0; i <= raw.Length - total; i++)
            {
                if (raw[i + 1] != (byte)expectedLen) continue;
                int sum = 0;
                for (int j = 0; j < total; j++) sum = (sum + raw[i + j]) & 0xFF;
                if (sum != 0xFF) continue;
                byte[] frame = new byte[total];
                Buffer.BlockCopy(raw, i, frame, 0, total);
                return frame;
            }
            return null;
        }

        private static byte[] Build34Request(int startStep)
        {
            byte[] frame = new byte[6];
            frame[0] = 0x34;
            frame[1] = 0x03;
            frame[2] = (byte)(startStep & 0xFF);
            frame[3] = (byte)((startStep >> 8) & 0xFF);
            frame[4] = 0xA0;
            int sum = 0;
            for (int i = 0; i < 5; i++) sum = (sum + frame[i]) & 0xFF;
            frame[5] = (byte)((0xFF - sum) & 0xFF);
            return frame;
        }

        private static void Validate34Frame(byte[] frame)
        {
            if (frame == null || frame.Length != PageFrameLength || frame[1] != PagePayloadLength)
                throw new InvalidDataException("Quadro 34 invalido.");
            int sum = 0;
            for (int i = 0; i < frame.Length; i++) sum = (sum + frame[i]) & 0xFF;
            if (sum != 0xFF) throw new InvalidDataException("Checksum do quadro 34 diferente de FF.");
        }

        private static void GetStep(byte[] frame, int index, out byte high, out byte low, out byte braw)
        {
            Validate34Frame(frame);
            int p = 2;
            high = frame[p + (2 * index)];
            low = frame[p + (2 * index) + 1];
            braw = frame[p + PageABLength + index];
        }

        private static int DetectPageTailCount(byte[] frame)
        {
            Validate34Frame(frame);
            for (int i = StepsPerPage - 1; i >= 0; i--)
            {
                byte high, low, braw;
                GetStep(frame, i, out high, out low, out braw);
                if (high != 0 || low != 0 || braw != 0) return i + 1;
            }
            return 0;
        }

        private static bool TryFindEnd(byte[] frame, out int localStep)
        {
            for (int i = 0; i < StepsPerPage; i++)
            {
                byte high, low, braw;
                GetStep(frame, i, out high, out low, out braw);
                if (high == 0x00 && low == 0x70)
                {
                    localStep = i;
                    return true;
                }
            }
            localStep = -1;
            return false;
        }

        private static byte CalculateBraw(byte high, byte low)
        {
            int sum = (high >> 4) + (high & 0x0F) + (low >> 4) + (low & 0x0F);
            return (byte)(sum & 0x0F);
        }

        // Espelha o mapa confirmado do Tp02Pg34Decoder do nucleo.
        private static string DecodeStep(byte high, byte low, byte braw)
        {
            if (high == 0x00 && low == 0x00 && braw == 0x00) return "NOP";
            if (high == 0x00 && low == 0x70) return "F-00 END";
            if (high == 0x00 && low == 0x01) return "AND STR";
            if (high == 0x00 && low == 0x02) return "OR STR";

            string instruction = DecodeBooleanInstruction(low);
            string device;
            int number;
            if (!string.IsNullOrEmpty(instruction) && TryDecodeBitDevice(high, low, out device, out number))
                return instruction + " " + device + number.ToString("0000", CultureInfo.InvariantCulture);

            if ((high & 0x80) == 0 && TryDecodeTimerCounter(high, low, out instruction, out number))
                return instruction + " V" + number.ToString("0000", CultureInfo.InvariantCulture);

            if (high == 0x17 && low == 0x71) return "F-23 SET";
            if (high == 0x18 && low == 0x71) return "F-24 RST";
            if (high == 0x0D && low == 0x77) return "F-13w ADD";

            if (TryDecodeLiteral(high, low, out number))
                return "K" + number.ToString(CultureInfo.InvariantCulture);

            if (TryDecodeSpecialBitOperand(high, low, out device, out number))
                return "ARG " + device + number.ToString("0000", CultureInfo.InvariantCulture);

            if (TryDecodeDOperand(high, low, out number))
                return "ARG D" + number.ToString("0000", CultureInfo.InvariantCulture);

            return "UNKNOWN " + high.ToString("X2", CultureInfo.InvariantCulture)
                + low.ToString("X2", CultureInfo.InvariantCulture)
                + " B=" + braw.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static string DecodeBooleanInstruction(byte low)
        {
            switch (low & 0x78)
            {
                case 0x10: return "STR";
                case 0x18: return "STR NOT";
                case 0x20: return "AND";
                case 0x28: return "AND NOT";
                case 0x30: return "OR";
                case 0x38: return "OR NOT";
                case 0x40: return "OUT";
                default: return string.Empty;
            }
        }

        private static bool TryDecodeBitDevice(byte high, byte low, out string device, out int number)
        {
            device = string.Empty;
            number = 0;
            if ((high & 0x80) != 0) return false;
            int deviceBase = high & 0x60;
            if (deviceBase == 0x00) device = "X";
            else if (deviceBase == 0x20) device = "Y";
            else if (deviceBase == 0x40) device = "C";
            else return false;
            int group = high & 0x1F;
            int bit = low & 0x07;
            number = (group * 8) + bit + 1;
            return number > 0;
        }

        private static bool TryDecodeTimerCounter(byte high, byte low, out string instruction, out int number)
        {
            instruction = string.Empty;
            number = 0;
            int opcode = low & 0x78;
            if (opcode == 0x60) instruction = "TMR";
            else if (opcode == 0x68) instruction = "CNT";
            else return false;
            int index = (high & 0x7F) | ((low & 0x07) << 7);
            number = index + 1;
            return true;
        }

        private static bool TryDecodeLiteral(byte high, byte low, out int value)
        {
            value = 0;
            if (high < 0x80 || high > 0x9F || (low & 0x80) != 0) return false;
            int highNibble = (high & 0x1E) >> 1;
            int lowByte = ((high & 0x01) << 7) | (low & 0x7F);
            value = (highNibble << 8) | lowByte;
            return true;
        }

        private static bool TryDecodeSpecialBitOperand(byte high, byte low, out string device, out int number)
        {
            device = string.Empty;
            number = 0;
            if ((low & 0x80) == 0) return false;
            int deviceBase = high & 0xF8;
            if (deviceBase == 0xC0) device = "X";
            else if (deviceBase == 0xC8) device = "Y";
            else if (deviceBase == 0xD0) device = "C";
            else return false;
            int bit = high & 0x07;
            int group = low & 0x7F;
            number = (group * 8) + bit + 1;
            return true;
        }

        private static bool TryDecodeDOperand(byte high, byte low, out int number)
        {
            number = 0;
            if ((high & 0xF8) != 0xF0 || (low & 0x80) != 0) return false;
            int h = (high & 0x0E) >> 1;
            int l = ((high & 0x01) << 7) | (low & 0x7F);
            number = ((h << 8) | l) + 1;
            return true;
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

        private void CreateSessionDirectory()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpenLadder Studio", "TP02 PG Reads");
            sessionDirectory = Path.Combine(root,
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(sessionDirectory);
            sessionLogPath = Path.Combine(sessionDirectory, "session.log");
            sessionLabel.Text = "Sessao: " + sessionDirectory;
            folderButton.Enabled = true;
        }

        private void SaveHex(string fileName, byte[] frame)
        {
            File.WriteAllText(Path.Combine(sessionDirectory, fileName), ToHex(frame) + Environment.NewLine, Encoding.ASCII);
        }

        private void SaveProgramFiles(ReadResult result)
        {
            File.WriteAllLines(Path.Combine(sessionDirectory, "program-words.txt"), result.Words.ToArray(), Encoding.ASCII);
            File.WriteAllLines(Path.Combine(sessionDirectory, "program-il.txt"), result.Il.ToArray(), Encoding.UTF8);
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("OpenLadder Studio - TP02 PG/PC12 READ-ONLY v1.10");
            summary.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            summary.AppendLine("Perfil: 19200 8O1 DTR=off RTS=off");
            summary.AppendLine("HELLO: " + result.Hello + " (" + result.PlcState + ")");
            summary.AppendLine("38: " + result.Frame38);
            summary.AppendLine("Paginas 34: " + result.Pages.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("Passos: " + result.Steps.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("END: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture));
            summary.AppendLine("BRAW divergentes: " + result.BrawMismatches.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("UNKNOWN: " + result.UnknownSteps.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("Seguranca: nenhum comando de escrita/RUN/STOP/limpeza foi enviado.");
            File.WriteAllText(Path.Combine(sessionDirectory, "read-summary.txt"), summary.ToString(), Encoding.UTF8);
        }

        private void ShowProgram(ReadResult result)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("TP02 PG/PC12 - PROGRAMA LIDO");
            text.AppendLine("HELLO: " + result.Hello + "   PLC: " + result.PlcState);
            text.AppendLine("Passos: " + result.Steps.ToString(CultureInfo.InvariantCulture)
                + "   END: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture)
                + "   Paginas: " + result.Pages.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("BRAW: " + result.BrawChecked.ToString(CultureInfo.InvariantCulture)
                + " verificados / " + result.BrawMismatches.ToString(CultureInfo.InvariantCulture)
                + " divergencias   UNKNOWN: " + result.UnknownSteps.ToString(CultureInfo.InvariantCulture));
            text.AppendLine(new string('-', 92));
            text.AppendLine("PASSO  WORD    IL / DECODIFICACAO");
            text.AppendLine("-----  ------  ---------------------------------------------------------------");
            for (int i = 0; i < result.Il.Count; i++) text.AppendLine(result.Il[i]);
            programBox.Text = text.ToString();
            programBox.SelectionStart = 0;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            portCombo.Enabled = !value;
            readButton.Enabled = !value;
            refreshButton.Enabled = !value;
            folderButton.Enabled = !value && !string.IsNullOrEmpty(sessionDirectory);
        }

        private void SetStatus(string text, Color color)
        {
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
            if (!string.IsNullOrEmpty(sessionLogPath))
            {
                try { File.AppendAllText(sessionLogPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
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
            if (string.IsNullOrEmpty(sessionDirectory) || !Directory.Exists(sessionDirectory)) return;
            try { System.Diagnostics.Process.Start("explorer.exe", sessionDirectory); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Abrir pasta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
