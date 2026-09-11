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
    /// Bateria final de validacao do TP02 real pela porta MMI / Computer Link.
    /// Fase 1 e somente leitura: PSR + duas leituras completas RBP e comparacao.
    /// Fase 2 e destrutiva e so pode ser iniciada explicitamente pelo usuario:
    /// exige STOP, compila o projeto Ladder atual, faz backup, WBP e verificacao RBP.
    /// Todos os artefatos da sessao sao salvos em Meus Documentos.
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
            MinimumSize = new Size(940, 690);
            Size = new Size(1100, 790);
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

            Label sub = NewLabel("Porta MMI | Computer Link | 19200 7N1 | fase 1 somente leitura; fase 2 exige STOP e confirmacao explicita", 8.7f, FontStyle.Regular, Muted);
            sub.Location = new Point(22, 47);
            header.Controls.Add(sub);

            stateLabel = NewLabel("PLC: NAO TESTADO", 9.0f, FontStyle.Bold, Muted);
            stateLabel.AutoSize = false;
            stateLabel.TextAlign = ContentAlignment.MiddleRight;
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 245;
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

            Label stationLabel = NewLabel("Estacao", 8.0f, FontStyle.Bold, Muted);
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
            projectLabel.Location = new Point(404, 36);
            projectLabel.MaximumSize = new Size(650, 40);
            setup.Controls.Add(projectLabel);

            Label folder = NewLabel("Sessao: " + sessionDirectory, 8.0f, FontStyle.Regular, Muted);
            folder.Location = new Point(18, 78);
            folder.MaximumSize = new Size(850, 24);
            setup.Controls.Add(folder);

            openFolderButton = NewButton("ABRIR PASTA", 890, 70, 130, false);
            openFolderButton.Click += delegate { OpenSessionFolder(); };
            setup.Controls.Add(openFolderButton);

            Panel stages = new Panel();
            stages.Dock = DockStyle.Top;
            stages.Height = 142;
            stages.BackColor = Chrome;
            Controls.Add(stages);

            readValidationButton = NewButton("1. VALIDAR LEITURA", 20, 18, 210, true);
            readValidationButton.Click += delegate { RunReadValidation(); };
            stages.Controls.Add(readValidationButton);

            readLabel = NewLabel("PENDENTE - PSR + RBP duas vezes + comparacao integral", 8.8f, FontStyle.Bold, Warning);
            readLabel.Location = new Point(252, 28);
            stages.Controls.Add(readLabel);

            writeValidationButton = NewButton("2. VALIDAR GRAVACAO", 20, 78, 210, false);
            writeValidationButton.Enabled = false;
            writeValidationButton.Click += delegate { RunWriteValidation(); };
            stages.Controls.Add(writeValidationButton);

            writeLabel = NewLabel("BLOQUEADA - primeiro a leitura precisa passar; depois o PLC deve estar em STOP", 8.8f, FontStyle.Bold, Muted);
            writeLabel.Location = new Point(252, 88);
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
            AppendLog("FASE 1 nao grava nada no PLC.");
            AppendLog("FASE 2 somente e habilitada depois da leitura e requer STOP + confirmacao explicita.");
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
            if (!result.Success) throw new InvalidDataException("compilacao TP02 possui " + result.Errors.Count.ToString(CultureInfo.InvariantCulture) + " erro(s)");
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
            UpdateStageLabels();
            AppendLog(new string('-', 78));
            AppendLog("FASE 1 iniciada: validacao de leitura real em " + port + ".");

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                try
                {
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
                        AppendLogSafe("PASS: duas leituras RBP identicas, " + first.Count.ToString(CultureInfo.InvariantCulture) + " passo(s).");
                        readPassed = true;
                    }
                }
                catch (Exception ex) { failure = ex; }

                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FAIL FASE 1: " + failure.Message);
                        readPassed = false;
                    }
                    else
                    {
                        AppendLog("FASE 1 APROVADA.");
                    }
                    SetBusy(false);
                    UpdateStageLabels();
                    WriteReport();
                    if (failure == null)
                    {
                        MessageBox.Show(this,
                            lastState == Tp02ComputerLinkState.Stop
                                ? "Leitura fisica aprovada. O PLC esta em STOP; a validacao de gravacao foi liberada."
                                : "Leitura fisica aprovada. Coloque o PLC em STOP e execute novamente a fase 1 para liberar a gravacao.",
                            "TP02 - Fase 1", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(this, failure.Message, "TP02 - Fase 1 falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            });
        }

        private void RunWriteValidation()
        {
            if (busy || !readPassed || lastState != Tp02ComputerLinkState.Stop) return;

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
                "TESTE FISICO DE GRAVACAO REAL\r\n\r\n"
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
            AppendLog("FASE 2 iniciada: gravacao e verificacao real.");

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
                        MessageBox.Show(this, "Leitura e gravacao fisicas aprovadas. O protocolo RBP/WBP passou na validacao real.", "TP02 - VALIDACAO APROVADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            writeValidationButton.Enabled = !value && readPassed && lastState == Tp02ComputerLinkState.Stop;
        }

        private void UpdateStageLabels()
        {
            if (readPassed)
            {
                readLabel.Text = "PASS - PSR e duas leituras RBP completas e identicas";
                readLabel.ForeColor = Success;
            }
            else
            {
                readLabel.Text = "PENDENTE - PSR + RBP duas vezes + comparacao integral";
                readLabel.ForeColor = Warning;
            }

            if (writePassed)
            {
                writeLabel.Text = "PASS - backup + WBP + verify por bloco + releitura final";
                writeLabel.ForeColor = Success;
            }
            else if (readPassed && lastState == Tp02ComputerLinkState.Stop)
            {
                writeLabel.Text = "LIBERADA - PLC em STOP; exige confirmacao antes do WBP";
                writeLabel.ForeColor = Warning;
            }
            else if (readPassed)
            {
                writeLabel.Text = "AGUARDANDO STOP - nenhuma escrita sera enviada em RUN";
                writeLabel.ForeColor = Warning;
            }
            else
            {
                writeLabel.Text = "BLOQUEADA - primeiro a leitura precisa passar";
                writeLabel.ForeColor = Muted;
            }
            writeValidationButton.Enabled = !busy && readPassed && lastState == Tp02ComputerLinkState.Stop;
        }

        private void SetStateSafe(Tp02ComputerLinkState state)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { SetStateSafe(state); }));
                return;
            }
            stateLabel.Text = "PLC: " + state.ToString().ToUpperInvariant();
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
                report.AppendLine("OpenLadder Studio - Validacao fisica WEG TP02");
                report.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                report.AppendLine("Sessao: " + sessionDirectory);
                report.AppendLine("Porta: " + (portCombo != null && portCombo.SelectedItem != null ? portCombo.SelectedItem.ToString() : "-"));
                report.AppendLine("Estacao: " + (stationBox == null ? "-" : ((int)stationBox.Value).ToString("00", CultureInfo.InvariantCulture)));
                report.AppendLine("Estado PLC: " + lastState.ToString().ToUpperInvariant());
                report.AppendLine("FASE 1 - leitura: " + (readPassed ? "PASS" : "PENDENTE/FAIL"));
                report.AppendLine("FASE 2 - gravacao: " + (writePassed ? "PASS" : "PENDENTE/FAIL"));
                report.AppendLine("Resultado protocolo RBP/WBP: " + (readPassed && writePassed ? "APROVADO EM HARDWARE REAL" : "AINDA NAO FECHADO"));
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
