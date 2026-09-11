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
    /// <summary>
    /// Entrada standalone mantida por compatibilidade com builds/ferramentas antigas.
    /// A tela atual e um leitor PG/PC12 SOMENTE LEITURA.
    /// </summary>
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

    /// <summary>
    /// Leitor fisico READ-ONLY do programa do WEG TP02 via PG/PC12.
    ///
    /// Fluxo usado em bancada:
    ///   HELLO (CON-ICB CR) -> F0 -> 38 -> 34
    ///
    /// Perfil confirmado no conjunto TP-232PG do laboratorio:
    ///   19200 8O1, DTR=OFF, RTS=OFF.
    ///
    /// Esta classe NAO implementa WBP, RUN, STOP, CLEAR nem qualquer comando
    /// de escrita. A unica finalidade e coletar e decodificar o programa.
    /// </summary>
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
            public bool CrossedPageBoundary;
            public int BrawChecked;
            public int BrawMismatches;
            public readonly List<string> Words = new List<string>();
            public readonly List<string> Il = new List<string>();
        }

        private const int StepsPerPage = 80;
        private const int MaxProgramSteps = 4000;
        private const int PagePayloadLength = 0xF0;
        private const int PageFrameLength = PagePayloadLength + 3;
        private const int PageABLength = 0xA0;

        private static readonly byte[] HelloRequest = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };

        private static readonly byte[] HelloRun = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
        private static readonly byte[] HelloStop = new byte[] { 0x80, 0x01, 0x09, 0x75 };
        private static readonly byte[] F0Request = new byte[] { 0xF0, 0x00, 0x0F };
        private static readonly byte[] F0KnownResponse = new byte[] { 0x00, 0x02, 0x10, 0x22, 0xCB };
        private static readonly byte[] Frame38Request = new byte[] { 0x38, 0x00, 0xC7 };

        private ComboBox portCombo;
        private Button refreshButton;
        private Button readButton;
        private Button openFolderButton;
        private TextBox outputBox;
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

            Label title = NewLabel("TP02 - LEITURA DE PROGRAMA PG/PC12", 14.0f, FontStyle.Bold, Fore);
            title.Location = new Point(20, 13);
            header.Controls.Add(title);

            Label sub = NewLabel(
                "SOMENTE LEITURA | TP-232PG | 19200 8O1 | DTR=OFF | RTS=OFF | HELLO -> F0 -> 38 -> 34",
                8.8f, FontStyle.Regular, Muted);
            sub.Location = new Point(22, 47);
            header.Controls.Add(sub);

            statusLabel = NewLabel("AGUARDANDO", 9.0f, FontStyle.Bold, Muted);
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.Dock = DockStyle.Right;
            statusLabel.Width = 250;
            header.Controls.Add(statusLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 116;
            config.BackColor = Shell;
            Controls.Add(config);

            Label portLabel = NewLabel("Porta COM", 8.0f, FontStyle.Bold, Muted);
            portLabel.Location = new Point(18, 13);
            config.Controls.Add(portLabel);

            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Location = new Point(18, 36);
            portCombo.Size = new Size(145, 25);
            config.Controls.Add(portCombo);

            refreshButton = NewButton("ATUALIZAR", 176, 34, 105, false);
            refreshButton.Click += delegate { RefreshPorts(); };
            config.Controls.Add(refreshButton);

            readButton = NewButton("LER PROGRAMA PG", 310, 27, 190, true);
            readButton.Click += delegate { StartRead(); };
            config.Controls.Add(readButton);

            openFolderButton = NewButton("ABRIR PASTA", 520, 27, 130, false);
            openFolderButton.Enabled = false;
            openFolderButton.Click += delegate { OpenSessionFolder(); };
            config.Controls.Add(openFolderButton);

            Label safety = NewLabel(
                "Nao envia RUN, STOP, limpeza ou escrita. Se ultrapassar 80 passos, a paginacao continua marcada como experimental.",
                8.3f, FontStyle.Regular, Warning);
            safety.Location = new Point(680, 35);
            safety.MaximumSize = new Size(430, 42);
            config.Controls.Add(safety);

            sessionLabel = NewLabel("Sessao: -", 8.0f, FontStyle.Regular, Muted);
            sessionLabel.Location = new Point(18, 82);
            sessionLabel.MaximumSize = new Size(1080, 24);
            config.Controls.Add(sessionLabel);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Segoe UI", 9.0f);
            Controls.Add(tabs);
            tabs.BringToFront();

            TabPage programTab = new TabPage("Programa lido");
            programTab.BackColor = Shell;
            tabs.TabPages.Add(programTab);

            outputBox = NewTextBox();
            outputBox.Dock = DockStyle.Fill;
            programTab.Controls.Add(outputBox);

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
            string selected = portCombo == null || portCombo.SelectedItem == null
                ? string.Empty : portCombo.SelectedItem.ToString();
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
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            CreateSessionDirectory();
            outputBox.Clear();
            logBox.Clear();
            AppendLog("Sessao READ-ONLY iniciada.");
            AppendLog("Porta escolhida: " + portName + ".");
            AppendLog("Perfil fixo: 19200 8O1 DTR=off RTS=off.");
            AppendLog("Sequencia: HELLO -> F0 -> 38 -> 34.");
            AppendLog("SEGURANCA: nao existe comando de escrita/RUN/STOP/limpeza nesta ferramenta.");
            SetBusy(true);
            SetStatusSafe("LENDO...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                ReadResult result = null;
                Exception failure = null;
                try
                {
                    result = ReadProgramWithSessionRetry(portName);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FALHA FINAL: " + failure.Message);
                        SetStatusSafe("FALHA", Danger);
                        MessageBox.Show(this, failure.Message,
                            "TP02 - leitura PG falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        ShowProgram(result);
                        SetStatusSafe("LEITURA PG APROVADA", Success);
                        string pageNotice = result.CrossedPageBoundary
                            ? "\r\n\r\nATENCAO: a leitura cruzou 80 passos. Os dados foram preservados, mas essa paginacao ainda e experimental ate comparacao fisica adicional."
                            : string.Empty;
                        MessageBox.Show(this,
                            "Programa lido pelo PG/PC12.\r\n\r\n"
                            + "PLC no HELLO: " + result.PlcState + "\r\n"
                            + "Passos ate F-00 END: " + result.Steps.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "END global: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture) + "\r\n"
                            + "Paginas 34: " + result.Pages.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "BRAW: " + result.BrawChecked.ToString(CultureInfo.InvariantCulture)
                            + " verificado(s), " + result.BrawMismatches.ToString(CultureInfo.InvariantCulture) + " divergencia(s)."
                            + pageNotice,
                            "TP02 - LEITURA PG APROVADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SetBusy(false);
                }));
            });
        }

        private void CreateSessionDirectory()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpenLadder Studio", "TP02 PG Reads");
            sessionDirectory = Path.Combine(root,
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(sessionDirectory);
            sessionLogPath = Path.Combine(sessionDirectory, "session.log");
            sessionLabel.Text = "Sessao: " + sessionDirectory;
            openFolderButton.Enabled = true;
        }

        private ReadResult ReadProgramWithSessionRetry(string portName)
        {
            Exception last = null;
            for (int session = 1; session <= 2; session++)
            {
                try
                {
                    if (session == 2)
                    {
                        AppendLogSafe("PG SESSION RETRY: primeira sessao falhou; fechando/reabrindo a COM automaticamente.");
                        Thread.Sleep(1100);
                    }
                    return ReadProgramSession(portName, session);
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("PG sessao " + session.ToString(CultureInfo.InvariantCulture)
                        + " falhou: " + ex.Message);
                }
            }

            throw new IOException("Duas sessoes PG de leitura falharam. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

        private ReadResult ReadProgramSession(string portName, int sessionNumber)
        {
            ReadResult result = new ReadResult();
            SerialPort port = null;
            try
            {
                port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                port.Handshake = Handshake.None;
                port.DtrEnable = false;
                port.RtsEnable = false;
                port.ReadTimeout = 100;
                port.WriteTimeout = 1500;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                Thread.Sleep(sessionNumber == 1 ? 900 : 1300);
                AppendLogSafe("COM aberta: " + portName + " 19200 8O1 DTR=off RTS=off | sessao "
                    + sessionNumber.ToString(CultureInfo.InvariantCulture) + ".");

                result.Hello = PerformHello(port);
                result.PlcState = result.Hello.StartsWith("80", StringComparison.Ordinal)
                    ? "STOP" : "RUN";
                AppendLogSafe("HELLO confirmado: " + result.Hello + " => " + result.PlcState + ".");
                Thread.Sleep(220);

                byte[] f0Frame = PerformF0(port);
                SaveHex("f0-response.hex", f0Frame);
                Thread.Sleep(240);

                byte[] frame38 = SendAndReadFrame(port, Frame38Request, 0x02, 3, 3200, "38");
                result.Frame38 = ToHex(frame38);
                SaveHex("frame38.hex", frame38);
                AppendLogSafe("38 valido: " + result.Frame38 + ".");
                Thread.Sleep(260);

                int startStep = 0;
                bool foundEnd = false;
                while (startStep < MaxProgramSteps)
                {
                    if (startStep > 0)
                    {
                        result.CrossedPageBoundary = true;
                        AppendLogSafe("PAGINACAO EXPERIMENTAL: solicitando pagina a partir do passo "
                            + startStep.ToString("0000", CultureInfo.InvariantCulture) + ".");
                    }

                    byte[] request34 = Build34Request(startStep);
                    byte[] frame34 = SendAndReadFrame(port, request34, PagePayloadLength, 3, 5000,
                        "34@" + startStep.ToString("0000", CultureInfo.InvariantCulture));
                    result.Pages++;
                    SaveHex("page-" + startStep.ToString("0000", CultureInfo.InvariantCulture) + ".hex", frame34);

                    int activeCount = DetectPageTailCount(frame34);
                    int localEnd;
                    bool hasEnd = TryFindEnd(frame34, out localEnd);
                    int countToStore = activeCount;
                    if (hasEnd && localEnd + 1 < countToStore) countToStore = localEnd + 1;

                    AppendLogSafe("34 pagina " + startStep.ToString("0000", CultureInfo.InvariantCulture)
                        + ": ativos=" + activeCount.ToString(CultureInfo.InvariantCulture)
                        + (hasEnd ? ", END local=" + localEnd.ToString("00", CultureInfo.InvariantCulture) : ", END ausente") + ".");

                    for (int i = 0; i < countToStore; i++)
                    {
                        byte high;
                        byte low;
                        byte braw;
                        GetStep(frame34, i, out high, out low, out braw);
                        string word = high.ToString("X2", CultureInfo.InvariantCulture)
                            + low.ToString("X2", CultureInfo.InvariantCulture)
                            + braw.ToString("X2", CultureInfo.InvariantCulture);
                        int global = startStep + i;
                        byte expectedBraw = CalculateBraw(high, low);
                        bool brawOk = expectedBraw == braw;
                        result.BrawChecked++;
                        if (!brawOk) result.BrawMismatches++;

                        result.Words.Add(word);
                        result.Il.Add(global.ToString("0000", CultureInfo.InvariantCulture)
                            + "  " + word + "  " + DecodeStep(high, low, braw)
                            + (brawOk ? string.Empty : "  [BRAW esperado=" + expectedBraw.ToString("X2", CultureInfo.InvariantCulture) + "]"));
                        result.Steps++;
                    }

                    if (hasEnd)
                    {
                        result.EndStep = startStep + localEnd;
                        foundEnd = true;
                        AppendLogSafe("F-00 END encontrado no passo global "
                            + result.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");
                        break;
                    }

                    if (activeCount < StepsPerPage)
                    {
                        throw new InvalidDataException("Pagina 34 parcial sem F-00 END. A leitura foi interrompida para nao inferir o fim do programa.");
                    }

                    startStep += StepsPerPage;
                    Thread.Sleep(300);
                }

                if (!foundEnd)
                    throw new InvalidDataException("F-00 END nao encontrado antes do limite de 4000 passos.");

                SaveProgramFiles(result);
                AppendLogSafe("LEITURA CONCLUIDA: " + result.Steps.ToString(CultureInfo.InvariantCulture)
                    + " passo(s), END=" + result.EndStep.ToString("0000", CultureInfo.InvariantCulture)
                    + ", paginas=" + result.Pages.ToString(CultureInfo.InvariantCulture) + ".");
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
                AppendLogSafe("HELLO TX " + attempt.ToString(CultureInfo.InvariantCulture) + ": " + ToHex(HelloRequest));
                port.Write(HelloRequest, 0, HelloRequest.Length);
                byte[] raw = ReadBurst(port, attempt == 1 ? 2300 : 2800, 230);
                AppendLogSafe("HELLO RX: " + (raw.Length == 0 ? "[]" : ToHex(raw)));

                if (Contains(raw, HelloStop)) return ToHex(HelloStop);
                if (Contains(raw, HelloRun)) return ToHex(HelloRun);
                Thread.Sleep(280);
            }
            throw new TimeoutException("HELLO PG nao confirmado em 5 tentativas.");
        }

        private byte[] PerformF0(SerialPort port)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                byte[] raw = SendAndReadBurst(port, F0Request, 3200, 230,
                    "F0 #" + attempt.ToString(CultureInfo.InvariantCulture));
                if (Contains(raw, F0KnownResponse))
                {
                    AppendLogSafe("F0 conhecido confirmado: " + ToHex(F0KnownResponse) + ".");
                    return F0KnownResponse;
                }
                Thread.Sleep(280);
            }
            throw new InvalidDataException("F0 nao retornou o quadro conhecido 00 02 10 22 CB.");
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

        private byte[] SendAndReadFrame(SerialPort port, byte[] request, int expectedLenByte,
            int attempts, int timeoutMs, string label)
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                byte[] raw = SendAndReadBurst(port, request, timeoutMs, 260,
                    label + " #" + attempt.ToString(CultureInfo.InvariantCulture));
                byte[] frame = FindFrame(raw, expectedLenByte);
                if (frame != null)
                {
                    AppendLogSafe(label + " FRAME OK: " + ToHex(frame) + ".");
                    return frame;
                }
                Thread.Sleep(300);
            }

            throw new InvalidDataException(label + " nao retornou quadro valido LEN="
                + expectedLenByte.ToString("X2", CultureInfo.InvariantCulture) + " e checksum FF.");
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

        private static byte[] Build34Request(int startStep)
        {
            if (startStep < 0 || startStep >= MaxProgramSteps)
                throw new ArgumentOutOfRangeException("startStep");
            if ((startStep % StepsPerPage) != 0)
                throw new ArgumentException("Inicio da pagina 34 deve ser multiplo de 80.", "startStep");

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

        private static int DetectPageTailCount(byte[] frame34)
        {
            Validate34Frame(frame34);
            for (int i = StepsPerPage - 1; i >= 0; i--)
            {
                byte high;
                byte low;
                byte braw;
                GetStep(frame34, i, out high, out low, out braw);
                if (high != 0 || low != 0 || braw != 0) return i + 1;
            }
            return 0;
        }

        private static bool TryFindEnd(byte[] frame34, out int localStep)
        {
            Validate34Frame(frame34);
            for (int i = 0; i < StepsPerPage; i++)
            {
                byte high;
                byte low;
                byte braw;
                GetStep(frame34, i, out high, out low, out braw);
                if (high == 0x00 && low == 0x70)
                {
                    localStep = i;
                    return true;
                }
            }
            localStep = -1;
            return false;
        }

        private static void GetStep(byte[] frame34, int index, out byte high, out byte low, out byte braw)
        {
            Validate34Frame(frame34);
            if (index < 0 || index >= StepsPerPage) throw new ArgumentOutOfRangeException("index");
            int payload = 2;
            high = frame34[payload + (2 * index)];
            low = frame34[payload + (2 * index) + 1];
            braw = frame34[payload + PageABLength + index];
        }

        private static void Validate34Frame(byte[] frame34)
        {
            if (frame34 == null || frame34.Length != PageFrameLength)
                throw new InvalidDataException("Quadro 34 deve ter 243 bytes.");
            if (frame34[1] != PagePayloadLength)
                throw new InvalidDataException("Quadro 34 deve ter LEN=F0.");
            int sum = 0;
            for (int i = 0; i < frame34.Length; i++) sum = (sum + frame34[i]) & 0xFF;
            if (sum != 0xFF)
                throw new InvalidDataException("Checksum do quadro 34 diferente de FF.");
        }

        private static byte CalculateBraw(byte high, byte low)
        {
            int sum = (high >> 4) + (high & 0x0F) + (low >> 4) + (low & 0x0F);
            return (byte)(sum & 0x0F);
        }

        private static string DecodeStep(byte high, byte low, byte braw)
        {
            if (high == 0x00 && low == 0x70) return "F-00 END";
            if (high == 0x00 && low == 0x00 && braw == 0x00) return "NOP";
            if (high == 0x00 && low == 0x01) return "AND STR";
            if (high == 0x00 && low == 0x02) return "OR STR";

            string instruction = DecodeBooleanInstruction(low);
            string device;
            int number;
            if (!string.IsNullOrEmpty(instruction)
                && TryDecodeBitDevice(high, low, out device, out number))
            {
                return instruction + " " + device
                    + number.ToString("0000", CultureInfo.InvariantCulture);
            }

            return "RAW " + high.ToString("X2", CultureInfo.InvariantCulture)
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

        private static bool Contains(byte[] value, byte[] sequence)
        {
            if (value == null || sequence == null || sequence.Length == 0 || value.Length < sequence.Length)
                return false;
            for (int i = 0; i <= value.Length - sequence.Length; i++)
            {
                bool same = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (value[i + j] != sequence[j])
                    {
                        same = false;
                        break;
                    }
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

        private void SaveHex(string fileName, byte[] frame)
        {
            File.WriteAllText(Path.Combine(sessionDirectory, fileName),
                ToHex(frame) + Environment.NewLine, Encoding.ASCII);
        }

        private void SaveProgramFiles(ReadResult result)
        {
            File.WriteAllLines(Path.Combine(sessionDirectory, "program-words.txt"),
                result.Words.ToArray(), Encoding.ASCII);
            File.WriteAllLines(Path.Combine(sessionDirectory, "program-il.txt"),
                result.Il.ToArray(), Encoding.UTF8);

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("OpenLadder Studio - TP02 PG/PC12 READ-ONLY");
            summary.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            summary.AppendLine("Perfil: 19200 8O1 DTR=off RTS=off");
            summary.AppendLine("HELLO: " + result.Hello + " (" + result.PlcState + ")");
            summary.AppendLine("38: " + result.Frame38);
            summary.AppendLine("Paginas 34: " + result.Pages.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("Passos ate END: " + result.Steps.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("END global: " + result.EndStep.ToString("0000", CultureInfo.InvariantCulture));
            summary.AppendLine("BRAW verificados: " + result.BrawChecked.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("BRAW divergentes: " + result.BrawMismatches.ToString(CultureInfo.InvariantCulture));
            summary.AppendLine("Cruzou 80 passos: "
                + (result.CrossedPageBoundary ? "SIM - paginacao ainda experimental" : "NAO"));
            summary.AppendLine("Seguranca: nenhum comando de escrita/RUN/STOP/limpeza foi enviado.");
            File.WriteAllText(Path.Combine(sessionDirectory, "read-summary.txt"),
                summary.ToString(), Encoding.UTF8);
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
                + " verificados / " + result.BrawMismatches.ToString(CultureInfo.InvariantCulture) + " divergencias");
            if (result.CrossedPageBoundary)
                text.AppendLine("ATENCAO: leitura cruzou 80 passos; paginacao permanece experimental.");
            text.AppendLine(new string('-', 92));
            text.AppendLine("PASSO  WORD    IL / DECODIFICACAO");
            text.AppendLine("-----  ------  ---------------------------------------------------------------");
            for (int i = 0; i < result.Il.Count; i++) text.AppendLine(result.Il[i]);
            outputBox.Text = text.ToString();
            outputBox.SelectionStart = 0;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            portCombo.Enabled = !value;
            refreshButton.Enabled = !value;
            readButton.Enabled = !value;
            openFolderButton.Enabled = !value && !string.IsNullOrEmpty(sessionDirectory);
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
            if (!string.IsNullOrEmpty(sessionLogPath))
            {
                try { File.AppendAllText(sessionLogPath, line + Environment.NewLine, Encoding.UTF8); }
                catch { }
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
