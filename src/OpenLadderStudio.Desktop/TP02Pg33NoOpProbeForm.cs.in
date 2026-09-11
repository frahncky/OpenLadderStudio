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
    /// Sonda física controlada do comando PG33 do WEG TP02.
    ///
    /// Estratégia de segurança:
    /// 1) exige HELLO=STOP;
    /// 2) lê o programa atual por HELLO/F0/38/34;
    /// 3) salva backup antes de qualquer escrita;
    /// 4) monta PG33 contendo EXATAMENTE as mesmas palavras lidas;
    /// 5) transmite no máximo 3 vezes, como o PC12 original;
    /// 6) captura e salva o ACK físico bruto;
    /// 7) relê o programa e exige igualdade byte a byte.
    ///
    /// Esta ferramenta NÃO transmite Clear All, RUN, STOP remoto, 0x09 nem WBP.
    /// O único comando de escrita permitido aqui é 0x33 e somente com conteúdo
    /// idêntico ao programa recém-lido.
    /// </summary>
    internal sealed class TP02Pg33NoOpProbeForm : Form
    {
        private sealed class ProgramSnapshot
        {
            public string PlcState = string.Empty;
            public int EndStep = -1;
            public readonly List<byte> High = new List<byte>();
            public readonly List<byte> Low = new List<byte>();
            public readonly List<byte> External = new List<byte>();

            public int Count { get { return High.Count; } }
        }

        private const int StepsPerPage = 80;
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

        private readonly PlcDeviceProfile profile;
        private ComboBox portCombo;
        private Button refreshButton;
        private Button probeButton;
        private Button folderButton;
        private TextBox logBox;
        private Label statusLabel;
        private Label sessionLabel;
        private string sessionDirectory = string.Empty;
        private string logPath = string.Empty;
        private bool busy;

        private readonly Color Shell = Color.FromArgb(18, 24, 31);
        private readonly Color Chrome = Color.FromArgb(27, 36, 46);
        private readonly Color Border = Color.FromArgb(55, 68, 82);
        private readonly Color Accent = Color.FromArgb(38, 166, 154);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(158, 169, 180);
        private readonly Color Warning = Color.FromArgb(224, 170, 64);
        private readonly Color Danger = Color.FromArgb(214, 87, 87);
        private readonly Color Success = Color.FromArgb(74, 190, 119);

        public TP02Pg33NoOpProbeForm(PlcDeviceProfile profile)
        {
            this.profile = profile;
            Text = "TP02 - Validacao fisica PG33 sem alterar programa";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 620);
            Size = new Size(1080, 720);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            RefreshPorts();
            LoadPreferredPort();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = NewLabel("TP02 - PROVA FISICA PG33 (NO-OP)", 14.0f, FontStyle.Bold, Fore);
            title.Location = new Point(20, 12);
            header.Controls.Add(title);

            Label sub = NewLabel(
                "Regrava exatamente o programa recém-lido para capturar o ACK físico do 0x33. PLC obrigatoriamente em STOP.",
                8.6f, FontStyle.Regular, Warning);
            sub.Location = new Point(22, 49);
            sub.MaximumSize = new Size(760, 36);
            header.Controls.Add(sub);

            statusLabel = NewLabel("AGUARDANDO", 9.0f, FontStyle.Bold, Muted);
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.Dock = DockStyle.Right;
            statusLabel.Width = 250;
            header.Controls.Add(statusLabel);

            Panel controls = new Panel();
            controls.Dock = DockStyle.Top;
            controls.Height = 118;
            controls.BackColor = Shell;
            Controls.Add(controls);

            Label portLabel = NewLabel("Porta COM", 8.0f, FontStyle.Bold, Muted);
            portLabel.Location = new Point(18, 12);
            controls.Controls.Add(portLabel);

            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Location = new Point(18, 35);
            portCombo.Size = new Size(145, 25);
            controls.Controls.Add(portCombo);

            refreshButton = NewButton("ATUALIZAR", 176, 32, 105, false);
            refreshButton.Click += delegate { RefreshPorts(); };
            controls.Controls.Add(refreshButton);

            probeButton = NewButton("VALIDAR PG33 NO-OP", 310, 25, 220, true);
            probeButton.Click += delegate { StartProbe(); };
            controls.Controls.Add(probeButton);

            folderButton = NewButton("ABRIR PASTA", 550, 25, 130, false);
            folderButton.Enabled = false;
            folderButton.Click += delegate { OpenFolder(); };
            controls.Controls.Add(folderButton);

            Label note = NewLabel(
                "A ferramenta salva backup, exige STOP, escreve somente os mesmos words e faz releitura/compare. Nenhum Clear All é enviado.",
                8.2f, FontStyle.Regular, Muted);
            note.Location = new Point(705, 28);
            note.MaximumSize = new Size(330, 52);
            controls.Controls.Add(note);

            sessionLabel = NewLabel("Sessao: -", 8.0f, FontStyle.Regular, Muted);
            sessionLabel.Location = new Point(18, 82);
            sessionLabel.MaximumSize = new Size(1000, 24);
            controls.Controls.Add(sessionLabel);

            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.WordWrap = false;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.Font = new Font("Consolas", 9.0f);
            logBox.BackColor = Color.FromArgb(16, 22, 29);
            logBox.ForeColor = Fore;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(logBox);
            logBox.BringToFront();
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
            button.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            button.BackColor = primary ? Accent : Chrome;
            button.ForeColor = primary ? Color.White : Fore;
            button.FlatAppearance.BorderColor = Border;
            if (primary) button.FlatAppearance.BorderSize = 0;
            return button;
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
                if (profile == null) return;
                PlcConnectionSettings settings = PlcConnectionSettingsStore.Load(profile);
                if (settings == null || string.IsNullOrEmpty(settings.PortName)) return;
                if (!portCombo.Items.Contains(settings.PortName)) portCombo.Items.Add(settings.PortName);
                portCombo.SelectedItem = settings.PortName;
            }
            catch { }
        }

        private void StartProbe()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 PG33", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "Este é um teste REAL de escrita PG no TP02.\r\n\r\n"
                + "O Studio irá:\r\n"
                + "1. exigir PLC em STOP;\r\n"
                + "2. ler e salvar o programa atual;\r\n"
                + "3. regravar EXATAMENTE os mesmos passos via comando 0x33;\r\n"
                + "4. capturar a resposta física;\r\n"
                + "5. reler e comparar byte a byte.\r\n\r\n"
                + "Faça o teste somente em bancada, com a máquina/processo em condição segura.\r\n\r\n"
                + "Deseja continuar?",
                "Confirmar prova física PG33",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG33 NO-OP iniciado em " + portName + ".");
            AppendLog("Perfil: 19200 8O1 DTR=off RTS=off.");
            AppendLog("REGRA: somente 0x33 com os mesmos words do backup pode ser transmitido.");
            SetBusy(true);
            SetStatus("LENDO BACKUP...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string successText = string.Empty;
                try
                {
                    ProgramSnapshot before = ReadSnapshotRobust(portName, "before");
                    if (!string.Equals(before.PlcState, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC está em RUN. O PG33 foi BLOQUEADO; coloque o TP02 em STOP.");
                    if (before.Count < 1 || before.EndStep < 0)
                        throw new InvalidDataException("Backup inválido: F-00 END não foi encontrado.");
                    if (before.Count > 80)
                        throw new InvalidOperationException("Esta primeira prova física aceita somente programas de até 80 passos. O programa atual tem "
                            + before.Count.ToString(CultureInfo.InvariantCulture) + ".");

                    SaveSnapshot("backup-before", before);
                    AppendLogSafe("BACKUP OK: " + before.Count.ToString(CultureInfo.InvariantCulture)
                        + " passos; END=" + before.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");

                    byte[] pg33 = BuildPg33SameProgram(before);
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-tx.hex"), ToHex(pg33) + Environment.NewLine, Encoding.ASCII);
                    AppendLogSafe("PG33 preparado: " + ToHex(pg33));

                    byte[] ack = ExecutePg33(portName, pg33);
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-ack-raw.hex"),
                        (ack.Length == 0 ? "(sem bytes)" : ToHex(ack)) + Environment.NewLine, Encoding.ASCII);

                    byte[] ackFrame = FindFirstValidResponseFrame(ack);
                    if (ackFrame == null)
                        throw new InvalidDataException("O 0x33 foi transmitido, mas não houve ACK físico com enquadramento/checksum válido. "
                            + "A sessão foi salva para análise; não faremos outra alteração.");
                    if ((ackFrame[0] & 0x80) != 0)
                        throw new InvalidDataException("O TP02 respondeu ao 0x33 com status de erro: " + ToHex(ackFrame));

                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-ack-frame.hex"), ToHex(ackFrame) + Environment.NewLine, Encoding.ASCII);
                    AppendLogSafe("ACK PG33 FÍSICO VÁLIDO: " + ToHex(ackFrame));

                    Thread.Sleep(1600);
                    ProgramSnapshot after = ReadSnapshotRobust(portName, "after");
                    SaveSnapshot("readback-after", after);
                    CompareSnapshots(before, after);

                    string report = "PASS PG33 NO-OP\r\n"
                        + "ACK físico: " + ToHex(ackFrame) + "\r\n"
                        + "Passos: " + before.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                        + "END: " + before.EndStep.ToString("0000", CultureInfo.InvariantCulture) + "\r\n"
                        + "Readback: idêntico ao backup\r\n"
                        + "Nenhum Clear All/RUN/STOP remoto/0x09/WBP foi enviado.\r\n";
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-probe-report.txt"), report, Encoding.UTF8);
                    successText = "PG33 físico aprovado.\r\n\r\nACK: " + ToHex(ackFrame)
                        + "\r\nPassos verificados: " + before.Count.ToString(CultureInfo.InvariantCulture)
                        + "\r\nReadback idêntico ao backup.\r\n\r\nO programa lógico permaneceu inalterado.";
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg33-probe-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FALHA: " + failure.Message);
                        SetStatus("FALHA / VER LOG", Danger);
                        MessageBox.Show(this, failure.Message,
                            "TP02 - PG33 não aprovado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        AppendLog("PASS: ACK físico e readback confirmados.");
                        SetStatus("PG33 FÍSICO APROVADO", Success);
                        MessageBox.Show(this, successText,
                            "TP02 - PG33 APROVADO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SetBusy(false);
                }));
            });
        }

        private ProgramSnapshot ReadSnapshotRobust(string portName, string tag)
        {
            Exception last = null;
            for (int session = 1; session <= 3; session++)
            {
                try
                {
                    if (session > 1) Thread.Sleep(1300 + (session * 250));
                    return ReadSnapshotSession(portName, tag, session);
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe(tag + " leitura sessão " + session.ToString(CultureInfo.InvariantCulture) + " falhou: " + ex.Message);
                }
            }
            throw new IOException("Falha ao ler snapshot " + tag + ". Último erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

        private ProgramSnapshot ReadSnapshotSession(string portName, string tag, int sessionNumber)
        {
            SerialPort port = null;
            try
            {
                port = OpenPort(portName);
                Thread.Sleep(sessionNumber == 1 ? 1500 : 1900);
                string state = PerformHello(port);
                PerformF0(port);
                SendAndReadFrame(port, Frame38Request, 0x02, 4, 3800, tag + "-38");

                ProgramSnapshot snapshot = new ProgramSnapshot();
                snapshot.PlcState = state;
                int startStep = 0;
                bool endFound = false;
                while (startStep < 4000)
                {
                    byte[] frame34 = SendAndReadFrame(port, Build34Request(startStep), PagePayloadLength,
                        4, 5600, tag + "-34-" + startStep.ToString("0000", CultureInfo.InvariantCulture));
                    File.WriteAllText(Path.Combine(sessionDirectory,
                        tag + "-page-" + startStep.ToString("0000", CultureInfo.InvariantCulture) + ".hex"),
                        ToHex(frame34) + Environment.NewLine, Encoding.ASCII);

                    for (int i = 0; i < StepsPerPage; i++)
                    {
                        byte high = frame34[2 + (2 * i)];
                        byte low = frame34[2 + (2 * i) + 1];
                        byte braw = frame34[2 + PageABLength + i];
                        snapshot.High.Add(high);
                        snapshot.Low.Add(low);
                        snapshot.External.Add(braw);
                        if (high == 0x00 && low == 0x70)
                        {
                            snapshot.EndStep = startStep + i;
                            endFound = true;
                            break;
                        }
                    }
                    if (endFound) break;
                    startStep += StepsPerPage;
                }
                if (!endFound) throw new InvalidDataException("F-00 END não encontrado no snapshot " + tag + ".");
                return snapshot;
            }
            finally { ClosePort(port); }
        }

        private byte[] ExecutePg33(string portName, byte[] frame)
        {
            SerialPort port = null;
            List<byte> all = new List<byte>();
            try
            {
                port = OpenPort(portName);
                Thread.Sleep(1800);
                string state = PerformHello(port);
                if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                    throw new InvalidOperationException("PLC saiu de STOP antes do PG33. Escrita bloqueada.");
                PerformF0(port);
                SendAndReadFrame(port, Frame38Request, 0x02, 4, 3800, "write-preflight-38");
                Thread.Sleep(500);

                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    port.DiscardInBuffer();
                    AppendLogSafe("PG33 TX tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + ": " + ToHex(frame));
                    port.Write(frame, 0, frame.Length);
                    byte[] raw = ReadBurst(port, 6000, 320);
                    AppendLogSafe("PG33 RX tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + ": "
                        + (raw.Length == 0 ? "[]" : ToHex(raw)));
                    for (int i = 0; i < raw.Length; i++) all.Add(raw[i]);
                    byte[] valid = FindFirstValidResponseFrame(raw);
                    if (valid != null)
                    {
                        AppendLogSafe("PG33 resposta estruturalmente válida na tentativa "
                            + attempt.ToString(CultureInfo.InvariantCulture) + ": " + ToHex(valid));
                        break;
                    }
                    if (attempt < 3) Thread.Sleep(650);
                }
                return all.ToArray();
            }
            finally { ClosePort(port); }
        }

        private static byte[] BuildPg33SameProgram(ProgramSnapshot snapshot)
        {
            int w = snapshot.Count;
            if (w < 1 || w > 80) throw new ArgumentOutOfRangeException("snapshot", "PG33 no-op aceita 1..80 words.");
            int len = (3 * w) + 4;
            byte[] frame = new byte[len + 3];
            frame[0] = 0x33;
            frame[1] = checked((byte)len);
            frame[2] = 0x00;
            frame[3] = 0x00;
            frame[4] = 0x00;
            frame[5] = checked((byte)(2 * w));
            int p = 6;
            for (int i = 0; i < w; i++)
            {
                frame[p++] = snapshot.High[i];
                frame[p++] = snapshot.Low[i];
            }
            // Para esta prova no-op usamos o terceiro byte lido do 34 como plano externo.
            // A operação só prossegue para o programa já validado em bancada e o compare
            // posterior detecta qualquer divergência. O valor é preservado byte a byte.
            for (int i = 0; i < w; i++) frame[p++] = snapshot.External[i];
            int sum = 0;
            for (int i = 0; i < frame.Length - 1; i++) sum = (sum + frame[i]) & 0xFF;
            frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);
            return frame;
        }

        private static void CompareSnapshots(ProgramSnapshot before, ProgramSnapshot after)
        {
            if (before.Count != after.Count)
                throw new InvalidDataException("Readback mudou a quantidade de passos: antes=" + before.Count
                    + ", depois=" + after.Count + ".");
            if (before.EndStep != after.EndStep)
                throw new InvalidDataException("Readback mudou a posição do END.");
            for (int i = 0; i < before.Count; i++)
            {
                if (before.High[i] != after.High[i] || before.Low[i] != after.Low[i] || before.External[i] != after.External[i])
                    throw new InvalidDataException("Readback divergiu no passo " + i.ToString("0000", CultureInfo.InvariantCulture)
                        + ": antes=" + WordText(before, i) + ", depois=" + WordText(after, i) + ".");
            }
        }

        private void SaveSnapshot(string prefix, ProgramSnapshot snapshot)
        {
            StringBuilder hex = new StringBuilder();
            for (int i = 0; i < snapshot.Count; i++)
            {
                hex.Append(i.ToString("0000", CultureInfo.InvariantCulture));
                hex.Append("  ");
                hex.AppendLine(WordText(snapshot, i));
            }
            File.WriteAllText(Path.Combine(sessionDirectory, prefix + ".words.txt"), hex.ToString(), Encoding.ASCII);
        }

        private static string WordText(ProgramSnapshot snapshot, int index)
        {
            return snapshot.High[index].ToString("X2", CultureInfo.InvariantCulture) + " "
                + snapshot.Low[index].ToString("X2", CultureInfo.InvariantCulture) + " "
                + snapshot.External[index].ToString("X2", CultureInfo.InvariantCulture);
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

        private string PerformHello(SerialPort port)
        {
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(HelloRequest, 0, HelloRequest.Length);
                byte[] raw = ReadBurst(port, 3000, 240);
                if (Contains(raw, HelloStop)) return "STOP";
                if (Contains(raw, HelloRun)) return "RUN";
                Thread.Sleep(350);
            }
            throw new TimeoutException("HELLO PG não confirmado.");
        }

        private void PerformF0(SerialPort port)
        {
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(F0Request, 0, F0Request.Length);
                byte[] raw = ReadBurst(port, 3600, 250);
                if (Contains(raw, F0Response)) return;
                Thread.Sleep(350);
            }
            throw new InvalidDataException("F0 não retornou 00 02 10 22 CB.");
        }

        private byte[] SendAndReadFrame(SerialPort port, byte[] request, int expectedLen,
            int attempts, int timeoutMs, string label)
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(request, 0, request.Length);
                byte[] raw = ReadBurst(port, timeoutMs, 280);
                byte[] frame = FindFrame(raw, expectedLen);
                AppendLogSafe(label + " tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));
                if (frame != null) return frame;
                Thread.Sleep(380);
            }
            throw new InvalidDataException(label + " não retornou quadro válido.");
        }

        private static byte[] Build34Request(int startStep)
        {
            byte[] frame = new byte[6];
            frame[0] = 0x34;
            frame[1] = 0x03;
            frame[2] = (byte)((startStep >> 8) & 0xFF);
            frame[3] = (byte)(startStep & 0xFF);
            frame[4] = 0xA0;
            int sum = 0;
            for (int i = 0; i < 5; i++) sum = (sum + frame[i]) & 0xFF;
            frame[5] = (byte)((0xFF - sum) & 0xFF);
            return frame;
        }

        private static byte[] FindFrame(byte[] raw, int expectedLen)
        {
            if (raw == null) return null;
            int total = expectedLen + 3;
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

        private static byte[] FindFirstValidResponseFrame(byte[] raw)
        {
            if (raw == null || raw.Length < 3) return null;
            for (int start = 0; start <= raw.Length - 3; start++)
            {
                int len = raw[start + 1];
                int total = len + 3;
                if (total < 3 || start + total > raw.Length) continue;
                int sum = 0;
                for (int i = 0; i < total; i++) sum = (sum + raw[start + i]) & 0xFF;
                if (sum != 0xFF) continue;
                byte[] frame = new byte[total];
                Buffer.BlockCopy(raw, start, frame, 0, total);
                return frame;
            }
            return null;
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
            StringBuilder sb = new StringBuilder(value.Length * 3);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(value[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static void ClosePort(SerialPort port)
        {
            if (port == null) return;
            try { if (port.IsOpen) port.Close(); } catch { }
            try { port.Dispose(); } catch { }
        }

        private void CreateSession()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpenLadder Studio", "TP02 PG33 Physical Probes");
            sessionDirectory = Path.Combine(root,
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(sessionDirectory);
            logPath = Path.Combine(sessionDirectory, "session.log");
            sessionLabel.Text = "Sessao: " + sessionDirectory;
            folderButton.Enabled = true;
        }

        private void SetBusy(bool value)
        {
            busy = value;
            portCombo.Enabled = !value;
            refreshButton.Enabled = !value;
            probeButton.Enabled = !value;
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
            if (!string.IsNullOrEmpty(logPath))
            {
                try { File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
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

        private void OpenFolder()
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
