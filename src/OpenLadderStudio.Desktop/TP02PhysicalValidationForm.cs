using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    /// <summary>
    /// Validacao fisica final do WEG TP02 com autodeteccao de transporte.
    ///
    /// PG/PC12 (TP-232PG): 19200 8O1. A fase 1 transmite SOMENTE CON-ICB<CR>
    /// e aceita as duas respostas HELLO observadas fisicamente. Nenhum comando
    /// de escrita, RUN/STOP, limpeza ou transferencia e transmitido nesse modo.
    ///
    /// Computer Link: 19200 7N1. A fase 1 faz PSR e duas leituras RBP completas.
    /// A fase 2 somente e habilitada neste transporte, com PLC em STOP, e executa
    /// backup, WBP e verificacao integral por releitura.
    /// </summary>
    internal sealed class TP02PhysicalValidationForm : Form
    {
        private readonly PlcDeviceProfile profile;
        private readonly LadderEditorForm ladderForm;

        private ComboBox portCombo;
        private NumericUpDown stationBox;
        private Button refreshButton;
        private Button readValidationButton;
        private Button writeValidationButton;
        private Button openFolderButton;
        private TextBox logBox;
        private Label stateLabel;
        private Label readLabel;
        private Label writeLabel;
        private Label projectLabel;

        private bool busy;
        private bool readPassed;
        private bool writePassed;
        private bool pgLinkDetected;
        private string pgHelloVariant = string.Empty;
        private string pgProfile = string.Empty;
        private Tp02ComputerLinkState lastState = Tp02ComputerLinkState.Unknown;
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

        public TP02PhysicalValidationForm(PlcDeviceProfile profile, LadderEditorForm ladderForm)
        {
            this.profile = profile;
            this.ladderForm = ladderForm;

            Text = "Validacao fisica final - WEG TP02";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(960, 700);
            Size = new Size(1120, 800);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            CreateSessionDirectory();
            BuildUi();
            RefreshPorts();
            LoadPreferredPort();
            RefreshProjectSummary();
            WriteReport();
        }

        private void CreateSessionDirectory()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpenLadder Studio", "TP02 Validation");
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

            Label title = NewLabel("TP02 - VALIDACAO FISICA FINAL", 14.0f, FontStyle.Bold, Fore);
            title.Location = new Point(20, 14);
            header.Controls.Add(title);

            Label sub = NewLabel(
                "AUTO: PG/PC12 19200 8O1 (TP-232PG) ou Computer Link 19200 7N1 | fase 1 sem comandos destrutivos",
                8.7f, FontStyle.Regular, Muted);
            sub.Location = new Point(22, 47);
            header.Controls.Add(sub);

            stateLabel = NewLabel("PLC: NAO TESTADO", 9.0f, FontStyle.Bold, Muted);
            stateLabel.AutoSize = false;
            stateLabel.TextAlign = ContentAlignment.MiddleRight;
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 255;
            header.Controls.Add(stateLabel);

            Panel setup = new Panel();
            setup.Dock = DockStyle.Top;
            setup.Height = 112;
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

            Label stationLabel = NewLabel("Estacao (Computer Link)", 8.0f, FontStyle.Bold, Muted);
            stationLabel.Location = new Point(302, 12);
            setup.Controls.Add(stationLabel);

            stationBox = new NumericUpDown();
            stationBox.Minimum = 1;
            stationBox.Maximum = 99;
            stationBox.Value = 1;
            stationBox.Location = new Point(302, 34);
            stationBox.Size = new Size(76, 25);
            setup.Controls.Add(stationBox);

            projectLabel = NewLabel("Projeto atual: -", 8.5f, FontStyle.Regular, Muted);
            projectLabel.Location = new Point(410, 36);
            projectLabel.MaximumSize = new Size(650, 40);
            setup.Controls.Add(projectLabel);

            Label folder = NewLabel("Sessao: " + sessionDirectory, 8.0f, FontStyle.Regular, Muted);
            folder.Location = new Point(18, 78);
            folder.MaximumSize = new Size(850, 24);
            setup.Controls.Add(folder);

            openFolderButton = NewButton("ABRIR PASTA", 910, 70, 130, false);
            openFolderButton.Click += delegate { OpenSessionFolder(); };
            setup.Controls.Add(openFolderButton);

            Panel stages = new Panel();
            stages.Dock = DockStyle.Top;
            stages.Height = 150;
            stages.BackColor = Chrome;
            Controls.Add(stages);

            readValidationButton = NewButton("1. DETECTAR / VALIDAR LINK", 20, 18, 220, true);
            readValidationButton.Click += delegate { RunReadValidation(); };
            stages.Controls.Add(readValidationButton);

            readLabel = NewLabel("PENDENTE - tenta PG/PC12 seguro primeiro; depois Computer Link", 8.8f, FontStyle.Bold, Warning);
            readLabel.Location = new Point(260, 28);
            stages.Controls.Add(readLabel);

            writeValidationButton = NewButton("2. VALIDAR GRAVACAO", 20, 82, 220, false);
            writeValidationButton.Enabled = false;
            writeValidationButton.Click += delegate { RunWriteValidation(); };
            stages.Controls.Add(writeValidationButton);

            writeLabel = NewLabel("BLOQUEADA - somente Computer Link + leitura aprovada + STOP libera WBP", 8.8f, FontStyle.Bold, Muted);
            writeLabel.Location = new Point(260, 92);
            stages.Controls.Add(writeLabel);

            Panel logPanel = new Panel();
            logPanel.Dock = DockStyle.Fill;
            logPanel.Padding = new Padding(12);
            logPanel.BackColor = Shell;
            Controls.Add(logPanel);
            logPanel.BringToFront();

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
            logPanel.Controls.Add(logBox);

            AppendLog("Sessao de validacao criada.");
            AppendLog("FASE 1: primeiro tenta o link PG/PC12 usando somente CON-ICB<CR> em 19200 8O1.");
            AppendLog("Se PG nao responder, tenta Computer Link: PSR + duas leituras RBP completas.");
            AppendLog("FASE 2: WBP somente em Computer Link, depois de leitura aprovada e PLC em STOP.");
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
            int i;
            for (i = 0; i < ports.Length; i++) portCombo.Items.Add(ports[i]);
            if (!string.IsNullOrEmpty(selected) && portCombo.Items.Contains(selected))
                portCombo.SelectedItem = selected;
            else if (portCombo.Items.Count > 0)
                portCombo.SelectedIndex = 0;
            AppendLog("Portas detectadas: " + (ports.Length == 0 ? "nenhuma" : string.Join(", ", ports)));
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

        private void RefreshProjectSummary()
        {
            try
            {
                List<Tp02MachineWord> words = CompileCurrentProject();
                projectLabel.Text = "Projeto atual: " + words.Count.ToString(CultureInfo.InvariantCulture) + " passo(s) TP02 compilaveis";
                projectLabel.ForeColor = Success;
            }
            catch (Exception ex)
            {
                projectLabel.Text = "Projeto atual: nao pronto para gravacao - " + ex.Message;
                projectLabel.ForeColor = Warning;
            }
        }

        private string CaptureProjectText()
        {
            if (ladderForm == null) throw new InvalidOperationException("Editor Ladder nao esta disponivel.");
            MethodInfo method = typeof(LadderEditorForm).GetMethod("SerializeProject", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException("LadderEditorForm.SerializeProject nao encontrado.");
            object value = method.Invoke(ladderForm, null);
            string text = value as string;
            if (string.IsNullOrEmpty(text)) throw new InvalidDataException("Projeto Ladder atual esta vazio.");
            return text;
        }

        private List<Tp02MachineWord> CompileCurrentProject()
        {
            LadderProjectDocument document = LadderProjectCodec.Deserialize(CaptureProjectText());
            Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(document);
            if (!result.Success)
                throw new InvalidDataException("compilacao TP02 possui " + result.Errors.Count.ToString(CultureInfo.InvariantCulture) + " erro(s)");
            if (result.Words.Count < 1) throw new InvalidDataException("projeto sem passos TP02");
            if (!string.Equals(result.Words[result.Words.Count - 1].ToHex(), "007000", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ultimo passo nao e F-00 END (007000)");
            return new List<Tp02MachineWord>(result.Words);
        }

        private string SelectedPort()
        {
            if (portCombo.SelectedItem == null) throw new InvalidOperationException("Selecione uma porta COM.");
            return portCombo.SelectedItem.ToString();
        }

        private void RunReadValidation()
        {
            if (busy) return;
            string port;
            int station;
            try
            {
                port = SelectedPort();
                station = (int)stationBox.Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            readPassed = false;
            writePassed = false;
            pgLinkDetected = false;
            pgHelloVariant = string.Empty;
            pgProfile = string.Empty;
            lastState = Tp02ComputerLinkState.Unknown;
            UpdateStageLabels();
            AppendLog(new string('-', 78));
            AppendLog("FASE 1 iniciada: autodeteccao segura em " + port + ".");

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                try
                {
                    string hello;
                    string profileName;
                    if (TryProbePgLink(port, out hello, out profileName))
                    {
                        pgLinkDetected = true;
                        pgHelloVariant = hello;
                        pgProfile = profileName;
                        SetPgStateSafe();
                        AppendLogSafe("PASS LINK PG/PC12: HELLO " + hello + " confirmado com checksum FF.");
                        AppendLogSafe("Perfil confirmado: " + profileName + ".");
                        AppendLogSafe("SEGURANCA: nenhum comando PG posterior ao CON-ICB foi transmitido.");
                        AppendLogSafe("GRAVACAO: bloqueada em PG. WBP pertence ao Computer Link; escrita PG proprietaria ainda nao habilitada.");
                    }
                    else
                    {
                        AppendLogSafe("PG/PC12 nao confirmou HELLO. Tentando Computer Link 19200 7N1...");
                        using (TP02ComputerLinkClient client = new TP02ComputerLinkClient(port, station, AppendLogSafe))
                        {
                            lastState = client.ReadState();
                            SetStateSafe(lastState);

                            AppendLogSafe("RBP #1: leitura completa ate F-00 END...");
                            List<Tp02MachineWord> first = client.ReadProgram();
                            TP02ComputerLinkFiles.SaveHex(Path.Combine(sessionDirectory, "read-1.tp02.hex"), first);
                            TP02ComputerLinkFiles.SaveDump(Path.Combine(sessionDirectory, "read-1.rbpdump"), first);

                            AppendLogSafe("RBP #2: repetindo a leitura para verificar estabilidade...");
                            List<Tp02MachineWord> second = client.ReadProgram();
                            TP02ComputerLinkFiles.SaveHex(Path.Combine(sessionDirectory, "read-2.tp02.hex"), second);
                            TP02ComputerLinkFiles.SaveDump(Path.Combine(sessionDirectory, "read-2.rbpdump"), second);

                            EnsureEqual(first, second, "RBP #1 x RBP #2");
                            readPassed = true;
                            AppendLogSafe("PASS COMPUTER LINK: duas leituras RBP identicas, " + first.Count.ToString(CultureInfo.InvariantCulture) + " passo(s).");
                        }
                    }
                }
                catch (Exception ex) { failure = ex; }

                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FAIL FASE 1: " + failure.Message);
                        readPassed = false;
                        pgLinkDetected = false;
                    }
                    else if (pgLinkDetected)
                    {
                        AppendLog("FASE 1 APROVADA: link PG/PC12 confirmado de forma nao destrutiva.");
                    }
                    else
                    {
                        AppendLog("FASE 1 APROVADA: Computer Link + leitura RBP confirmados.");
                    }

                    SetBusy(false);
                    UpdateStageLabels();
                    WriteReport();

                    if (failure != null)
                    {
                        MessageBox.Show(this, failure.Message, "TP02 - Fase 1 falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (pgLinkDetected)
                    {
                        MessageBox.Show(this,
                            "Link PG/PC12 confirmado pelo TP-232PG.\r\n\r\n"
                            + "HELLO: " + pgHelloVariant + "\r\n"
                            + "Perfil: " + pgProfile + "\r\n\r\n"
                            + "O OpenLadderStudio confirmou o cabo, a COM e o link com o TP02. Nenhum comando destrutivo foi enviado.\r\n\r\n"
                            + "A gravacao permanece bloqueada neste modo porque a escrita PG proprietaria ainda nao esta habilitada.",
                            "TP02 - LINK PG APROVADO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(this,
                            lastState == Tp02ComputerLinkState.Stop
                                ? "Computer Link e leitura fisica aprovados. O PLC esta em STOP; a validacao de gravacao foi liberada."
                                : "Computer Link e leitura fisica aprovados. Coloque o PLC em STOP e execute novamente a fase 1 para liberar a gravacao.",
                            "TP02 - Fase 1", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }));
            });
        }

        private bool TryProbePgLink(string portName, out string helloVariant, out string profileName)
        {
            helloVariant = string.Empty;
            profileName = string.Empty;
            byte[] hello = new byte[] { 0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D };
            byte[] helloC0 = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
            byte[] hello80 = new byte[] { 0x80, 0x01, 0x09, 0x75 };

            string[] names = new string[]
            {
                "19200 8O1 DTR=on RTS=off",
                "19200 8O1 DTR=on RTS=on",
                "19200 8O1 DTR=off RTS=off"
            };
            bool[] dtr = new bool[] { true, true, false };
            bool[] rts = new bool[] { false, true, false };

            for (int p = 0; p < names.Length; p++)
            {
                SerialPort serial = null;
                try
                {
                    serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                    serial.Handshake = Handshake.None;
                    serial.DtrEnable = dtr[p];
                    serial.RtsEnable = rts[p];
                    serial.ReadTimeout = 80;
                    serial.WriteTimeout = 1000;
                    serial.Open();
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    Thread.Sleep(140);
                    AppendLogSafe("PG PERFIL: " + names[p]);

                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        serial.DiscardInBuffer();
                        AppendLogSafe("PG HELLO TX " + attempt.ToString(CultureInfo.InvariantCulture) + ": 43 4F 4E 2D 49 43 42 0D");
                        serial.Write(hello, 0, hello.Length);
                        byte[] raw = ReadPgBurst(serial, 1700);
                        AppendLogSafe("PG HELLO RX: " + (raw.Length == 0 ? "[]" : PgHex(raw)));

                        if (PgContains(raw, helloC0) && PgSum8(helloC0) == 0xFF)
                        {
                            helloVariant = "C0 01 09 35";
                            profileName = names[p];
                            return true;
                        }
                        if (PgContains(raw, hello80) && PgSum8(hello80) == 0xFF)
                        {
                            helloVariant = "80 01 09 75";
                            profileName = names[p];
                            return true;
                        }
                        Thread.Sleep(130);
                    }
                }
                catch (Exception ex)
                {
                    AppendLogSafe("PG PERFIL sem confirmacao: " + names[p] + " - " + ex.Message);
                }
                finally
                {
                    if (serial != null)
                    {
                        try { if (serial.IsOpen) serial.Close(); } catch { }
                        serial.Dispose();
                    }
                    Thread.Sleep(180);
                }
            }
            return false;
        }

        private static byte[] ReadPgBurst(SerialPort port, int timeoutMs)
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
                else if (bytes.Count > 0 && lastData != DateTime.MinValue && (DateTime.UtcNow - lastData).TotalMilliseconds >= 180)
                {
                    break;
                }
                Thread.Sleep(15);
            }
            return bytes.ToArray();
        }

        private static bool PgContains(byte[] value, byte[] sequence)
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

        private static byte PgSum8(byte[] value)
        {
            int sum = 0;
            if (value != null)
                for (int i = 0; i < value.Length; i++) sum = (sum + value[i]) & 0xFF;
            return (byte)sum;
        }

        private static string PgHex(byte[] value)
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

        private void SetPgStateSafe()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { SetPgStateSafe(); }));
                return;
            }
            stateLabel.Text = "PLC: LINK PG/PC12";
            stateLabel.ForeColor = Success;
        }

        private void RunWriteValidation()
        {
            if (busy || pgLinkDetected || !readPassed || lastState != Tp02ComputerLinkState.Stop) return;

            string port;
            int station;
            List<Tp02MachineWord> words;
            try
            {
                port = SelectedPort();
                station = (int)stationBox.Value;
                words = CompileCurrentProject();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Gravacao bloqueada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "TESTE FISICO DE GRAVACAO REAL - COMPUTER LINK\r\n\r\n"
                + "Porta: " + port + "\r\n"
                + "Estacao: " + station.ToString("00", CultureInfo.InvariantCulture) + "\r\n"
                + "Passos do projeto atual: " + words.Count.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n"
                + "O programa existente sera salvo em backup ANTES da escrita. O WBP so sera enviado se PSR confirmar STOP. Cada bloco e o programa final serao relidos por RBP e comparados.\r\n\r\n"
                + "Continuar com a gravacao real?",
                "Confirmar teste final TP02", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true);
            writePassed = false;
            UpdateStageLabels();
            AppendLog(new string('-', 78));
            AppendLog("FASE 2 iniciada: gravacao e verificacao real via Computer Link.");

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string backup = string.Empty;
                try
                {
                    string backupRoot = Path.Combine(sessionDirectory, "backup-before-write");
                    using (TP02ComputerLinkClient client = new TP02ComputerLinkClient(port, station, AppendLogSafe))
                    {
                        Tp02ComputerLinkState state = client.ReadState();
                        lastState = state;
                        SetStateSafe(state);
                        if (state != Tp02ComputerLinkState.Stop)
                            throw new InvalidOperationException("PLC deixou STOP; nenhum WBP sera enviado.");

                        backup = client.WriteProgramWithBackup(words, backupRoot);
                        AppendLogSafe("Releitura independente apos WriteProgramWithBackup...");
                        List<Tp02MachineWord> finalRead = client.ReadProgram();
                        EnsureEqual(words, finalRead, "projeto compilado x releitura final independente");
                        TP02ComputerLinkFiles.SaveHex(Path.Combine(sessionDirectory, "after-write.tp02.hex"), finalRead);
                        TP02ComputerLinkFiles.SaveDump(Path.Combine(sessionDirectory, "after-write.rbpdump"), finalRead);
                        writePassed = true;
                        AppendLogSafe("PASS: WBP confirmado e releitura final independente sem diferencas.");
                    }
                }
                catch (Exception ex) { failure = ex; }

                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FAIL FASE 2: " + failure.Message);
                        writePassed = false;
                    }
                    else
                    {
                        AppendLog("FASE 2 APROVADA. Backup anterior: " + backup);
                    }
                    SetBusy(false);
                    UpdateStageLabels();
                    WriteReport();
                    if (failure == null)
                        MessageBox.Show(this, "Leitura e gravacao fisicas aprovadas via Computer Link.", "TP02 - VALIDACAO APROVADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show(this, failure.Message, "TP02 - Fase 2 falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            });
        }

        private void EnsureEqual(IList<Tp02MachineWord> expected, IList<Tp02MachineWord> actual, string label)
        {
            if (expected == null || actual == null) throw new InvalidDataException(label + ": lista nula.");
            if (expected.Count != actual.Count)
                throw new InvalidDataException(label + ": quantidade diferente, " + expected.Count.ToString(CultureInfo.InvariantCulture) + " x " + actual.Count.ToString(CultureInfo.InvariantCulture) + ".");
            int i;
            for (i = 0; i < expected.Count; i++)
            {
                string a = expected[i].ToHex();
                string b = actual[i].ToHex();
                if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(label + ": diferenca no passo " + i.ToString("0000", CultureInfo.InvariantCulture) + ", " + a + " x " + b + ".");
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            refreshButton.Enabled = !value;
            readValidationButton.Enabled = !value;
            portCombo.Enabled = !value;
            stationBox.Enabled = !value;
            openFolderButton.Enabled = !value;
            writeValidationButton.Enabled = !value && !pgLinkDetected && readPassed && lastState == Tp02ComputerLinkState.Stop;
        }

        private void UpdateStageLabels()
        {
            if (pgLinkDetected)
            {
                readLabel.Text = "PASS - link PG/PC12 confirmado por HELLO seguro (TP-232PG)";
                readLabel.ForeColor = Success;
                writeLabel.Text = "BLOQUEADA EM PG - escrita proprietaria PG ainda nao habilitada";
                writeLabel.ForeColor = Warning;
                writeValidationButton.Enabled = false;
                return;
            }

            if (readPassed)
            {
                readLabel.Text = "PASS - Computer Link: PSR + duas leituras RBP completas e identicas";
                readLabel.ForeColor = Success;
            }
            else
            {
                readLabel.Text = "PENDENTE - tenta PG/PC12 seguro primeiro; depois Computer Link";
                readLabel.ForeColor = Warning;
            }

            if (writePassed)
            {
                writeLabel.Text = "PASS - backup + WBP + verify por bloco + releitura final";
                writeLabel.ForeColor = Success;
            }
            else if (readPassed && lastState == Tp02ComputerLinkState.Stop)
            {
                writeLabel.Text = "LIBERADA - Computer Link + PLC em STOP; exige confirmacao";
                writeLabel.ForeColor = Warning;
            }
            else if (readPassed)
            {
                writeLabel.Text = "AGUARDANDO STOP - nenhuma escrita sera enviada em RUN";
                writeLabel.ForeColor = Warning;
            }
            else
            {
                writeLabel.Text = "BLOQUEADA - primeiro o transporte precisa ser validado";
                writeLabel.ForeColor = Muted;
            }
            writeValidationButton.Enabled = !busy && !pgLinkDetected && readPassed && lastState == Tp02ComputerLinkState.Stop;
        }

        private void SetStateSafe(Tp02ComputerLinkState state)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { SetStateSafe(state); }));
                return;
            }
            stateLabel.Text = "PLC: " + state.ToString().ToUpperInvariant() + " (COMPUTER LINK)";
            stateLabel.ForeColor = state == Tp02ComputerLinkState.Stop ? Warning
                : state == Tp02ComputerLinkState.Run ? Success : Danger;
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

        private void WriteReport()
        {
            try
            {
                StringBuilder report = new StringBuilder();
                string mode = pgLinkDetected ? "PG/PC12" : (readPassed ? "COMPUTER LINK" : "NAO CONFIRMADO");
                report.AppendLine("OpenLadder Studio - Validacao fisica WEG TP02");
                report.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                report.AppendLine("Sessao: " + sessionDirectory);
                report.AppendLine("Porta: " + (portCombo != null && portCombo.SelectedItem != null ? portCombo.SelectedItem.ToString() : "-"));
                report.AppendLine("Transporte detectado: " + mode);
                if (pgLinkDetected)
                {
                    report.AppendLine("HELLO PG: " + pgHelloVariant);
                    report.AppendLine("Perfil PG: " + pgProfile);
                }
                report.AppendLine("Estacao Computer Link: " + (stationBox == null ? "-" : ((int)stationBox.Value).ToString("00", CultureInfo.InvariantCulture)));
                report.AppendLine("Estado via Computer Link: " + lastState.ToString().ToUpperInvariant());
                report.AppendLine("FASE 1: " + (pgLinkDetected
                    ? "PASS LINK PG/PC12 (HELLO seguro; programa ainda nao lido pelo caminho PG)"
                    : (readPassed ? "PASS COMPUTER LINK + RBP" : "PENDENTE/FAIL")));
                report.AppendLine("FASE 2 - gravacao: " + (writePassed ? "PASS" : (pgLinkDetected ? "BLOQUEADA EM PG" : "PENDENTE/FAIL")));
                report.AppendLine("Resultado: " + (pgLinkDetected
                    ? "LINK PG/PC12 APROVADO; ESCRITA PG PROPRIETARIA AINDA NAO HABILITADA"
                    : (readPassed && writePassed ? "RBP/WBP APROVADO EM HARDWARE REAL" : "AINDA NAO FECHADO")));
                File.WriteAllText(Path.Combine(sessionDirectory, "validation-report.txt"), report.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void OpenSessionFolder()
        {
            try { System.Diagnostics.Process.Start("explorer.exe", sessionDirectory); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Abrir pasta", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
