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
    /// Transferencia de programa TP02 pela interface MMI em Computer Link.
    ///
    /// Leitura: RBP em blocos de ate 100 passos, ate F-00 END (007000).
    /// Escrita: exige PSR=STOP, faz backup RBP completo antes do primeiro WBP,
    /// grava em blocos de ate 100 passos e verifica por RBP bloco a bloco e ao final.
    /// </summary>
    internal sealed class TP02ProgramTransferForm : Form
    {
        private readonly PlcDeviceProfile profile;
        private readonly LadderEditorForm ladderForm;
        private ComboBox portCombo;
        private NumericUpDown stationBox;
        private Button refreshButton;
        private Button testButton;
        private Button readButton;
        private Button writeButton;
        private TextBox logBox;
        private Label stateLabel;
        private Label projectLabel;
        private bool busy;

        private Color Shell { get { return OpenLadderPalette.Shell; } }
        private Color Chrome { get { return OpenLadderPalette.Chrome; } }
        private Color Border { get { return OpenLadderPalette.Border; } }
        private Color Accent { get { return OpenLadderPalette.Accent; } }
        private Color Fore { get { return OpenLadderPalette.Fore; } }
        private Color Muted { get { return OpenLadderPalette.Muted; } }
        private Color Warning { get { return OpenLadderPalette.Warning; } }
        private Color Danger { get { return OpenLadderPalette.Danger; } }
        private Color Success { get { return OpenLadderPalette.Ok; } }

        private ProgramFlowSteps flow;
        private ProgramLogPanel logView;

        private string ProfileName()
        {
            return profile == null ? "PLC" : (profile.Manufacturer + " " + profile.Model).Trim();
        }

        public TP02ProgramTransferForm(PlcDeviceProfile profile, LadderEditorForm ladderForm)
        {
            this.profile = profile;
            this.ladderForm = ladderForm;

            Text = "Transferência de programa - " + ProfileName();
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 650);
            Size = new Size(1040, 760);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildUi();
            RefreshPorts();
            LoadPreferredPort();
            RefreshProjectSummary();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 82;
            header.BackColor = Chrome;
            header.Padding = new Padding(20, 12, 20, 10);

            Label title = NewLabel("TRANSFERÊNCIA DE PROGRAMA", 14.0f, FontStyle.Bold, Fore);
            title.Location = new Point(20, 14);
            header.Controls.Add(title);

            Label sub = NewLabel(ProfileName() + "  |  Computer Link pela porta MMI | 19200 bps, 7N1 | PG/COM em LOW: pino 4 ligado ao pino 5", 8.8f, FontStyle.Regular, Muted);
            sub.Location = new Point(22, 46);
            header.Controls.Add(sub);

            stateLabel = NewLabel("OFF-LINE", 9.0f, FontStyle.Bold, Muted);
            stateLabel.AutoSize = false;
            stateLabel.TextAlign = ContentAlignment.MiddleRight;
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 230;
            header.Controls.Add(stateLabel);

            Panel controls = new Panel();
            controls.Dock = DockStyle.Top;
            controls.Height = 148;
            controls.BackColor = Shell;
            controls.Padding = new Padding(18, 12, 18, 10);

            Label portLabel = NewLabel("Porta COM", 8.0f, FontStyle.Bold, Muted);
            portLabel.Location = new Point(18, 14);
            controls.Controls.Add(portLabel);

            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Location = new Point(18, 36);
            portCombo.Size = new Size(150, 25);
            controls.Controls.Add(portCombo);

            refreshButton = NewButton("ATUALIZAR", 180, 34, 105, false);
            refreshButton.Click += delegate { RefreshPorts(); };
            controls.Controls.Add(refreshButton);

            Label stationLabel = NewLabel("Estação", 8.0f, FontStyle.Bold, Muted);
            stationLabel.Location = new Point(310, 14);
            controls.Controls.Add(stationLabel);

            stationBox = new NumericUpDown();
            stationBox.Minimum = 1;
            stationBox.Maximum = 99;
            stationBox.Value = 1;
            stationBox.Location = new Point(310, 36);
            stationBox.Size = new Size(80, 25);
            controls.Controls.Add(stationBox);

            projectLabel = NewLabel("Projeto atual: -", 8.5f, FontStyle.Regular, Muted);
            projectLabel.Location = new Point(420, 38);
            projectLabel.MaximumSize = new Size(570, 40);
            controls.Controls.Add(projectLabel);

            testButton = NewButton("TESTAR CONEXÃO", 18, 88, 150, false);
            testButton.Click += delegate { TestConnection(); };
            controls.Controls.Add(testButton);

            readButton = NewButton("LER DO PLC", 180, 88, 140, true);
            readButton.Click += delegate { ReadFromPlc(); };
            controls.Controls.Add(readButton);

            writeButton = NewButton("GRAVAR PROJETO ATUAL", 332, 88, 190, false);
            writeButton.Click += delegate { WriteCurrentProject(); };
            controls.Controls.Add(writeButton);

            Label safe = NewLabel("A gravação so e liberada com PSR=STOP. Antes do primeiro WBP, o programa atual do PLC e salvo automaticamente em backup.", 8.4f, FontStyle.Regular, Warning);
            safe.Location = new Point(548, 92);
            safe.MaximumSize = new Size(430, 44);
            controls.Controls.Add(safe);

            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.WordWrap = false;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.Font = new Font("Consolas", 9.0f);
            logBox.BackColor = OpenLadderPalette.Canvas;
            logBox.ForeColor = Fore;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logView = new ProgramLogPanel(logBox);

            AppendLog("TP02 Computer Link pronto.");
            AppendLog("Leitura usa RBP. Escrita usa WBP com backup e verificação RBP.");
            AppendLog("Não use a porta PG do PC12: esta tela usa a MMI em Computer Link.");

            // A ancoragem resolve do ultimo filho para o primeiro: o que entra por ultimo
            // fica na borda externa. Ordem visual de cima para baixo: cabecalho, passos,
            // comandos e registro.
            flow = new ProgramFlowSteps("Conectar", "Verificar", "Transferir", "Confirmar");
            Controls.Add(logView);
            Controls.Add(controls);
            Controls.Add(flow);
            Controls.Add(header);
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
            button.Size = new Size(width, 36);
            button.FlatStyle = FlatStyle.Flat;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            if (primary)
            {
                button.BackColor = Accent;
                button.ForeColor = OpenLadderPalette.OnAccent;
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
                string text = CaptureProjectText();
                LadderProjectDocument document = LadderProjectCodec.Deserialize(text);
                Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(document);
                projectLabel.Text = result.Success
                    ? "Projeto atual: " + result.Words.Count.ToString(CultureInfo.InvariantCulture) + " passo(s) TP02 compilaveis"
                    : "Projeto atual: compilação TP02 bloqueada (" + result.Errors.Count.ToString(CultureInfo.InvariantCulture) + " erro(s))";
                projectLabel.ForeColor = result.Success ? Success : Warning;
            }
            catch (Exception ex)
            {
                projectLabel.Text = "Projeto atual: indisponivel - " + ex.Message;
                projectLabel.ForeColor = Warning;
            }
        }

        private string CaptureProjectText()
        {
            if (ladderForm == null) throw new InvalidOperationException("Editor Ladder não esta disponível.");
            MethodInfo method = typeof(LadderEditorForm).GetMethod("SerializeProject", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException("LadderEditorForm.SerializeProject não encontrado.");
            object value = method.Invoke(ladderForm, null);
            string text = value as string;
            if (string.IsNullOrEmpty(text)) throw new InvalidDataException("Projeto Ladder atual esta vazio.");
            return text;
        }

        private string SelectedPort()
        {
            if (portCombo.SelectedItem == null) throw new InvalidOperationException("Selecione uma porta COM.");
            return portCombo.SelectedItem.ToString();
        }

        private void TestConnection()
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

            RunBusy("TESTE", delegate
            {
                using (TP02ComputerLinkClient client = new TP02ComputerLinkClient(port, station, AppendLogSafe))
                {
                    Tp02ComputerLinkState state = client.ReadState();
                    SetStateSafe(state);
                    return "Conexão confirmada. Estado=" + state.ToString().ToUpperInvariant() + ".";
                }
            });
        }

        private void ReadFromPlc()
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

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Programa TP02 (*.tp02.hex)|*.tp02.hex|HEX (*.hex)|*.hex|Todos (*.*)|*.*";
            dialog.DefaultExt = "tp02.hex";
            dialog.AddExtension = true;
            dialog.FileName = "TP02-programa-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".tp02.hex";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string output = dialog.FileName;

            RunBusy("LEITURA", delegate
            {
                using (TP02ComputerLinkClient client = new TP02ComputerLinkClient(port, station, AppendLogSafe))
                {
                    Tp02ComputerLinkState state;
                    try
                    {
                        state = client.ReadState();
                        SetStateSafe(state);
                    }
                    catch
                    {
                        AppendLogSafe("PSR não confirmou o estado; RBP será tentado mesmo assim.");
                    }

                    List<Tp02MachineWord> words = client.ReadProgram();
                    TP02ComputerLinkFiles.SaveHex(output, words);
                    string dump = Path.ChangeExtension(output, ".rbpdump");
                    TP02ComputerLinkFiles.SaveDump(dump, words);
                    return "Leitura concluída: " + words.Count.ToString(CultureInfo.InvariantCulture)
                        + " passo(s). HEX=" + output + " | dump=" + dump;
                }
            });
        }

        private void WriteCurrentProject()
        {
            if (busy) return;

            string port;
            int station;
            List<Tp02MachineWord> words;
            string report;
            try
            {
                port = SelectedPort();
                station = (int)stationBox.Value;
                string projectText = CaptureProjectText();
                LadderProjectDocument document = LadderProjectCodec.Deserialize(projectText);
                Tp02LadderCompilationResult result = Tp02LadderTargetCompiler.Compile(document);
                report = result.BuildReport();
                if (!result.Success)
                {
                    AppendLog(report);
                    MessageBox.Show(this,
                        "O projeto atual possui erros de compilação TP02. Nenhum byte será transmitido. Consulte o log da transferencia.",
                        "Gravação bloqueada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (result.Words.Count < 1) throw new InvalidDataException("Projeto sem passos TP02.");
                if (!string.Equals(result.Words[result.Words.Count - 1].ToHex(), "007000", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Gravação bloqueada: o último passo precisa ser F-00 END (007000).");
                words = new List<Tp02MachineWord>(result.Words);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Gravação bloqueada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "O projeto atual será gravado no TP02 pela porta " + port + ".\r\n\r\n"
                + "Passos: " + words.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Estação: " + station.ToString("00", CultureInfo.InvariantCulture) + "\r\n\r\n"
                + "A operação so prossegue se PSR confirmar STOP. O programa existente será lido e salvo em backup ANTES do primeiro WBP. Depois, cada bloco será relido e comparado.\r\n\r\n"
                + "Deseja iniciar a gravação?",
                "Confirmar gravação TP02", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            AppendLog(report);
            RunBusy("GRAVAÇÃO", delegate
            {
                string backupRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "OpenLadder Studio", "TP02 Backups");

                using (TP02ComputerLinkClient client = new TP02ComputerLinkClient(port, station, AppendLogSafe))
                {
                    Tp02ComputerLinkState state = client.ReadState();
                    SetStateSafe(state);
                    if (state != Tp02ComputerLinkState.Stop)
                        throw new InvalidOperationException("WBP BLOQUEADO: TP02 esta " + state.ToString().ToUpperInvariant() + ". Coloque o controlador em STOP/PROGRAM.");

                    string backup = client.WriteProgramWithBackup(words, backupRoot);
                    return "GRAVAÇÃO CONFIRMADA. Backup anterior: " + backup;
                }
            });
        }

        private void RunBusy(string operation, Func<string> work)
        {
            if (busy) return;
            busy = true;
            SetButtonsEnabled(false);
            AppendLog(new string('-', 72));
            AppendLog(operation + " iniciado.");
            flow.Reset();
            flow.Advance(0);
            logView.SetBusy(operation, true);

            ThreadPool.QueueUserWorkItem(delegate
            {
                string success = string.Empty;
                Exception failure = null;
                try { success = work(); }
                catch (Exception ex) { failure = ex; }

                BeginInvoke(new MethodInvoker(delegate
                {
                    busy = false;
                    SetButtonsEnabled(true);
                    logView.SetBusy(operation, false);
                    if (failure == null)
                    {
                        if (operation.StartsWith("GRAVA", StringComparison.Ordinal)) flow.CompleteAll();
                        else if (operation == "TESTE") { flow.Set(0, FlowState.Done); flow.Set(1, FlowState.Done); }
                        else flow.Set(0, FlowState.Done);
                        AppendLog(success);
                        AppendLog(operation + " concluído.");
                        MessageBox.Show(this, success, "TP02", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        flow.FailActive();
                        AppendLog("ERRO: " + failure.Message);
                        stateLabel.Text = "FALHA";
                        stateLabel.ForeColor = Danger;
                        MessageBox.Show(this, failure.Message, operation + " falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    RefreshProjectSummary();
                }));
            });
        }

        private void SetButtonsEnabled(bool enabled)
        {
            refreshButton.Enabled = enabled;
            testButton.Enabled = enabled;
            readButton.Enabled = enabled;
            writeButton.Enabled = enabled;
            portCombo.Enabled = enabled;
            stationBox.Enabled = enabled;
        }

        private void SetStateSafe(Tp02ComputerLinkState state)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { SetStateSafe(state); }));
                return;
            }

            stateLabel.Text = "PLC " + state.ToString().ToUpperInvariant();
            stateLabel.ForeColor = state == Tp02ComputerLinkState.Stop
                ? Warning
                : state == Tp02ComputerLinkState.Run ? Success : Danger;
        }

        private void AppendLog(string text)
        {
            if (logBox == null) return;
            ObserveFlow(text);
            logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + text + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        /// <summary>So apresentacao: os marcos do registro movem a faixa de passos; nada e transmitido por aqui.</summary>
        private void ObserveFlow(string text)
        {
            if (flow == null || !busy || string.IsNullOrEmpty(text)) return;
            if (text.StartsWith("COM aberta", StringComparison.Ordinal)) flow.Advance(0);
            else if (text.StartsWith("Estado PLC", StringComparison.Ordinal)) flow.Advance(1);
            else if (text.StartsWith("Lendo programa atual para backup", StringComparison.Ordinal)) flow.Advance(2);
            else if (text.IndexOf("final do programa", StringComparison.OrdinalIgnoreCase) >= 0) flow.Advance(3);
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
    }

    internal static class TP02ComputerLinkFiles
    {
        public static void SaveHex(string path, IList<Tp02MachineWord> words)
        {
            if (words == null) throw new ArgumentNullException("words");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            StringBuilder text = new StringBuilder(words.Count * 8);
            int i;
            for (i = 0; i < words.Count; i++) text.AppendLine(words[i].ToHex());
            File.WriteAllText(path, text.ToString(), Encoding.ASCII);
        }

        public static void SaveDump(string path, IList<Tp02MachineWord> words)
        {
            if (words == null) throw new ArgumentNullException("words");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            StringBuilder text = new StringBuilder(words.Count * 16);
            int i;
            for (i = 0; i < words.Count; i++)
                text.AppendLine(i.ToString("0000", CultureInfo.InvariantCulture) + "  " + words[i].ToHex());
            File.WriteAllText(path, text.ToString(), Encoding.ASCII);
        }
    }

    internal sealed class TP02ComputerLinkClient : IDisposable
    {
        private readonly SerialPort port;
        private readonly int station;
        private readonly int responseCode;
        private readonly int timeoutMs;
        private readonly Action<string> log;

        public TP02ComputerLinkClient(string portName, int station, Action<string> log)
        {
            if (string.IsNullOrEmpty(portName)) throw new ArgumentException("Porta COM não informada.", "portName");
            if (station < 1 || station > 99) throw new ArgumentOutOfRangeException("station");

            this.station = station;
            this.responseCode = 5;
            this.timeoutMs = 2500;
            this.log = log;

            port = new SerialPort(portName, 19200, Parity.None, 7, StopBits.One);
            port.Handshake = Handshake.None;
            port.DtrEnable = false;
            port.RtsEnable = false;
            port.ReadTimeout = 100;
            port.WriteTimeout = timeoutMs;
            port.Encoding = Encoding.ASCII;
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            WriteLog("COM aberta: " + port.PortName + " 19200 7N1");
        }

        public Tp02ComputerLinkState ReadState()
        {
            string response = Exchange(Tp02ComputerLinkProgramCodec.BuildPsr(station, responseCode), "PSR");
            Tp02ComputerLinkState state;
            if (!Tp02ComputerLinkProgramCodec.TryGetPsrState(response, out state))
                throw new InvalidDataException("Resposta PSR inválida ou com checksum incorreto.");
            WriteLog("Estado PLC: " + state.ToString().ToUpperInvariant());
            return state;
        }

        public List<Tp02MachineWord> ReadProgram()
        {
            List<Tp02MachineWord> result = new List<Tp02MachineWord>();
            int address = 0;
            bool endFound = false;
            int block = 0;

            while (address <= Tp02ComputerLinkProgramCodec.MaxProgramAddress)
            {
                int capacity = Tp02ComputerLinkProgramCodec.MaxProgramAddress - address + 1;
                int count = Math.Min(Tp02ComputerLinkProgramCodec.MaxStepsPerFrame, capacity);
                List<Tp02MachineWord> words = ReadRange(address, count, "RBP " + block.ToString(CultureInfo.InvariantCulture));
                int i;
                for (i = 0; i < words.Count; i++)
                {
                    result.Add(words[i]);
                    if (string.Equals(words[i].ToHex(), "007000", StringComparison.OrdinalIgnoreCase))
                    {
                        endFound = true;
                        break;
                    }
                }

                WriteLog("Bloco lido: addr=" + address.ToString("0000", CultureInfo.InvariantCulture)
                    + " passos=" + count.ToString(CultureInfo.InvariantCulture)
                    + (endFound ? " END encontrado" : string.Empty));

                if (endFound) break;
                address += count;
                block++;
            }

            if (!endFound)
                throw new InvalidDataException("F-00 END (007000) não foi encontrado até o passo 4000.");
            return result;
        }

        public string WriteProgramWithBackup(IList<Tp02MachineWord> words, string backupRoot)
        {
            if (words == null || words.Count < 1) throw new ArgumentOutOfRangeException("words");
            if (words.Count > Tp02ComputerLinkProgramCodec.MaxProgramAddress + 1)
                throw new ArgumentOutOfRangeException("words", "Programa excede o limite TP02.");
            if (!string.Equals(words[words.Count - 1].ToHex(), "007000", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Programa sem F-00 END final; WBP bloqueado.");

            Tp02ComputerLinkState state = ReadState();
            if (state != Tp02ComputerLinkState.Stop)
                throw new InvalidOperationException("WBP BLOQUEADO: PSR precisa indicar STOP/PROGRAM.");

            WriteLog("Lendo programa atual para backup...");
            List<Tp02MachineWord> oldProgram = ReadProgram();
            Directory.CreateDirectory(backupRoot);
            string backupBase = Path.Combine(backupRoot,
                "TP02-st" + station.ToString("00", CultureInfo.InvariantCulture)
                + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            string backupHex = backupBase + ".tp02.hex";
            string backupDump = backupBase + ".rbpdump";
            TP02ComputerLinkFiles.SaveHex(backupHex, oldProgram);
            TP02ComputerLinkFiles.SaveDump(backupDump, oldProgram);
            WriteLog("Backup salvo ANTES da escrita: " + backupHex);

            int offset = 0;
            int block = 0;
            while (offset < words.Count)
            {
                int count = Math.Min(Tp02ComputerLinkProgramCodec.MaxStepsPerFrame, words.Count - offset);
                List<Tp02MachineWord> chunk = new List<Tp02MachineWord>(count);
                int i;
                for (i = 0; i < count; i++) chunk.Add(words[offset + i]);

                string wbp = Tp02ComputerLinkProgramCodec.BuildWbp(station, offset, chunk, responseCode);
                string response = Exchange(wbp, "WBP " + block.ToString(CultureInfo.InvariantCulture));
                string errorCode;
                if (!Tp02ComputerLinkProgramCodec.IsSuccessfulResponse(response, "WBP", out errorCode))
                    throw new InvalidDataException("TP02 não confirmou WBP no bloco " + block.ToString(CultureInfo.InvariantCulture)
                        + (string.IsNullOrEmpty(errorCode) ? "." : "; erro=" + errorCode + "."));

                List<Tp02MachineWord> verify = ReadRange(offset, count, "RBP verify " + block.ToString(CultureInfo.InvariantCulture));
                EnsureEqual(chunk, verify, offset, "verificação do bloco");
                WriteLog("VERIFY OK: bloco=" + block.ToString(CultureInfo.InvariantCulture)
                    + " addr=" + offset.ToString("0000", CultureInfo.InvariantCulture)
                    + " passos=" + count.ToString(CultureInfo.InvariantCulture));

                offset += count;
                block++;
            }

            WriteLog("Verificação final do programa até END...");
            List<Tp02MachineWord> finalRead = ReadProgram();
            EnsureEqual(words, finalRead, 0, "verificação final");
            WriteLog("Programa final relido sem diferencas.");
            return backupHex;
        }

        private List<Tp02MachineWord> ReadRange(int address, int count, string label)
        {
            string frame = Tp02ComputerLinkProgramCodec.BuildRbp(station, address, count, responseCode);
            string response = Exchange(frame, label);
            Tp02ComputerLinkResponse parsed = Tp02ComputerLinkProgramCodec.ParseResponse(response, "RBP");
            if (!parsed.ChecksumOk || parsed.IsError || parsed.Command != "RBP")
                throw new InvalidDataException(label + ": resposta RBP inválida"
                    + (parsed.IsError && !string.IsNullOrEmpty(parsed.ErrorCode) ? "; erro=" + parsed.ErrorCode : string.Empty) + ".");

            string data = NormalizeHex(parsed.Data);
            int expected = count * 6;
            if (data.Length != expected)
                throw new InvalidDataException(label + ": retornou " + data.Length.ToString(CultureInfo.InvariantCulture)
                    + " caracteres hex; esperado=" + expected.ToString(CultureInfo.InvariantCulture) + ".");

            return Tp02ComputerLinkProgramCodec.ParseMachineHex(data);
        }

        private string Exchange(string frame, string label)
        {
            port.DiscardInBuffer();
            WriteLog("TX " + label + "  " + Escape(frame));
            port.Write(frame);
            string response = ReadUntilCarriageReturn();
            if (string.IsNullOrEmpty(response)) throw new TimeoutException(label + ": nenhuma resposta do TP02.");
            WriteLog("RX " + label + "  " + Escape(response));
            return response;
        }

        private string ReadUntilCarriageReturn()
        {
            StringBuilder received = new StringBuilder();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int value;
                try { value = port.ReadByte(); }
                catch (TimeoutException) { continue; }
                if (value < 0) continue;
                received.Append((char)value);
                if (value == 13) return received.ToString();
            }
            return received.ToString();
        }

        private static void EnsureEqual(IList<Tp02MachineWord> expected, IList<Tp02MachineWord> actual, int baseAddress, string phase)
        {
            if (expected.Count != actual.Count)
                throw new InvalidDataException(phase + ": quantidade diferente. esperado="
                    + expected.Count.ToString(CultureInfo.InvariantCulture) + " recebido="
                    + actual.Count.ToString(CultureInfo.InvariantCulture) + ".");

            int i;
            for (i = 0; i < expected.Count; i++)
            {
                string e = expected[i].ToHex();
                string a = actual[i].ToHex();
                if (!string.Equals(e, a, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(phase + ": diferenca no passo "
                        + (baseAddress + i).ToString("0000", CultureInfo.InvariantCulture)
                        + ". esperado=" + e + " recebido=" + a + ".");
            }
        }

        private static string NormalizeHex(string value)
        {
            StringBuilder text = new StringBuilder();
            string source = value ?? string.Empty;
            int i;
            for (i = 0; i < source.Length; i++)
                if (Uri.IsHexDigit(source[i])) text.Append(char.ToUpperInvariant(source[i]));
            return text.ToString();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\r", "<CR>").Replace("\n", "<LF>");
        }

        private void WriteLog(string text)
        {
            if (log != null) log(text);
        }

        public void Dispose()
        {
            try { if (port != null && port.IsOpen) port.Close(); } catch { }
            if (port != null) port.Dispose();
        }
    }
}
