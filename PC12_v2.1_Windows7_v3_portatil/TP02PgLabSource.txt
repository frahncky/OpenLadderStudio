using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ModernPC12
{
    internal sealed class PgLabPackage
    {
        public int schemaVersion { get; set; }
        public string packageVersion { get; set; }
        public string title { get; set; }
        public int sweeps { get; set; }
        public int passiveCaptureMs { get; set; }
        public bool stopOnUnknown { get; set; }
        public List<PgLabProfile> serialProfiles { get; set; }
        public List<PgLabStep> steps { get; set; }
        public PgLabSafety safety { get; set; }
    }

    internal sealed class PgLabProfile
    {
        public string name { get; set; }
        public int baud { get; set; }
        public int dataBits { get; set; }
        public string parity { get; set; }
        public string stopBits { get; set; }
        public bool dtr { get; set; }
        public bool rts { get; set; }
        public int attempts { get; set; }
        public int rxWindowMs { get; set; }
        public int interAttemptMs { get; set; }
    }

    internal sealed class PgLabStep
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool enabled { get; set; }
        public bool sweepProfiles { get; set; }
        public string safetyClass { get; set; }
        public string txHex { get; set; }
        public int timeoutMs { get; set; }
        public int passiveAfterMs { get; set; }
        public List<PgLabExpected> expected { get; set; }
    }

    internal sealed class PgLabExpected
    {
        public string name { get; set; }
        public string hex { get; set; }
    }

    internal sealed class PgLabSafety
    {
        public List<string> readOnlyAllowlist { get; set; }
        public List<string> blockedTx { get; set; }
        public List<string> notes { get; set; }
    }

    internal sealed class PgLabReport
    {
        public string engineVersion { get; set; }
        public string packageVersion { get; set; }
        public string startedUtc { get; set; }
        public string finishedUtc { get; set; }
        public string port { get; set; }
        public string result { get; set; }
        public string profile { get; set; }
        public string response { get; set; }
        public List<PgLabReportEvent> events { get; set; }
    }

    internal sealed class PgLabReportEvent
    {
        public string time { get; set; }
        public string kind { get; set; }
        public string detail { get; set; }
        public string hex { get; set; }
        public int sum8 { get; set; }
        public long elapsedMs { get; set; }
    }

    internal static class TP02PgLabProgram
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02PgLabForm());
        }
    }

    internal sealed class TP02PgLabForm : Form
    {
        private const string EngineVersion = "1.0";
        private const string PackageFileName = "TP02-PG-Tests.json";
        private const string RemotePackageUrl = "https://raw.githubusercontent.com/frahncky/OpenLadderStudio/main/PC12_v2.1_Windows7_v3_portatil/TP02-PG-Tests.json";
        private const string BuiltInHello = "43 4F 4E 2D 49 43 42 0D";

        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 120, 20);
        private readonly Color Danger = Color.FromArgb(183, 54, 54);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);

        private ComboBox portCombo;
        private Label stateLabel;
        private Label packageLabel;
        private Label summaryLabel;
        private ListView stepsView;
        private TextBox logBox;
        private Button runButton;
        private Button stopButton;
        private Button exportButton;
        private CheckBox allowReadOnly;

        private volatile bool running;
        private volatile bool cancelRequested;
        private PgLabPackage package;
        private PgLabReport report;
        private readonly object reportLock = new object();
        private string lastReportTxt = string.Empty;
        private string lastReportJson = string.Empty;

        public TP02PgLabForm()
        {
            Text = "OpenLadder Studio - Laboratorio PG TP02";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 720);
            Size = new Size(1300, 850);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            RefreshPorts();
            LoadPackageFromBestSource();
            FormClosing += delegate { cancelRequested = true; };
            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { StartPackageAutoUpdate(); })); };
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 78;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label title = NewLabel("LABORATORIO PG - WEG TP02", 16.0f, FontStyle.Bold, Navy);
            title.Location = new Point(20, 10);
            header.Controls.Add(title);

            Label subtitle = NewLabel("Motor permanente de testes + pacote de protocolo atualizavel", 9.0f, FontStyle.Regular, TextSecondary);
            subtitle.Location = new Point(22, 44);
            header.Controls.Add(subtitle);

            stateLabel = NewLabel("NAO EXECUTADO", 9.2f, FontStyle.Bold, TextSecondary);
            stateLabel.Dock = DockStyle.Right;
            stateLabel.Width = 340;
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            header.Controls.Add(stateLabel);

            Panel config = new Panel();
            config.Dock = DockStyle.Top;
            config.Height = 238;
            config.BackColor = Color.White;
            Controls.Add(config);

            config.Controls.Add(LabelAt("Porta COM", 20, 14, TextPrimary, true));
            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Location = new Point(20, 36);
            portCombo.Size = new Size(130, 25);
            config.Controls.Add(portCombo);

            Button refreshPorts = ButtonAt("ATUALIZAR PORTAS", 160, 33, 145, false);
            refreshPorts.Click += delegate { RefreshPorts(); };
            config.Controls.Add(refreshPorts);

            Button updatePackage = ButtonAt("ATUALIZAR PACOTE", 318, 33, 150, false);
            updatePackage.Click += delegate { StartPackageManualUpdate(); };
            config.Controls.Add(updatePackage);

            runButton = ButtonAt("EXECUTAR TESTE COMPLETO", 490, 31, 225, true);
            runButton.Click += delegate { StartTest(); };
            config.Controls.Add(runButton);

            stopButton = ButtonAt("PARAR", 728, 33, 100, false);
            stopButton.Enabled = false;
            stopButton.Click += delegate { cancelRequested = true; LogUi("CONTROLE", "parada solicitada pelo usuario."); };
            config.Controls.Add(stopButton);

            exportButton = ButtonAt("EXPORTAR RELATORIO", 842, 33, 170, false);
            exportButton.Enabled = false;
            exportButton.Click += delegate { ExportReport(); };
            config.Controls.Add(exportButton);

            Button clear = ButtonAt("LIMPAR", 1024, 33, 90, false);
            clear.Click += delegate { if (!running) logBox.Clear(); };
            config.Controls.Add(clear);

            packageLabel = LabelAt("Pacote: carregando...", 20, 76, Navy, true);
            config.Controls.Add(packageLabel);
            summaryLabel = LabelAt("", 20, 100, TextSecondary, false);
            summaryLabel.MaximumSize = new Size(1200, 40);
            config.Controls.Add(summaryLabel);

            allowReadOnly = new CheckBox();
            allowReadOnly.Text = "Permitir etapas READ-ONLY VERIFICADAS do pacote";
            allowReadOnly.AutoSize = true;
            allowReadOnly.Location = new Point(20, 130);
            allowReadOnly.ForeColor = Warning;
            allowReadOnly.Font = new Font("Segoe UI Semibold", 8.7f, FontStyle.Bold);
            config.Controls.Add(allowReadOnly);

            Label safety = LabelAt("Modo padrao: somente HANDSHAKE e captura passiva. Candidatos, comandos nao classificados e comandos bloqueados nunca sao enviados automaticamente.", 20, 160, Danger, true);
            safety.MaximumSize = new Size(1200, 38);
            config.Controls.Add(safety);

            stepsView = new ListView();
            stepsView.View = View.Details;
            stepsView.FullRowSelect = true;
            stepsView.GridLines = true;
            stepsView.Location = new Point(20, 190);
            stepsView.Size = new Size(1210, 42);
            stepsView.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            stepsView.Columns.Add("Etapa", 230);
            stepsView.Columns.Add("Classe", 150);
            stepsView.Columns.Add("Estado", 110);
            stepsView.Columns.Add("TX", 320);
            stepsView.Columns.Add("Observacao", 360);
            config.Controls.Add(stepsView);

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

            logBox.BringToFront();
            config.BringToFront();
            header.BringToFront();
        }

        private void RefreshPorts()
        {
            string old = portCombo.SelectedItem == null ? string.Empty : portCombo.SelectedItem.ToString();
            portCombo.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);
            foreach (string p in ports) portCombo.Items.Add(p);
            if (!string.IsNullOrEmpty(old) && portCombo.Items.Contains(old)) portCombo.SelectedItem = old;
            else if (portCombo.Items.Count > 0) portCombo.SelectedIndex = 0;
        }

        private void LoadPackageFromBestSource()
        {
            string cache = GetCachePackagePath();
            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PackageFileName);
            string selected = File.Exists(cache) ? cache : bundled;
            try
            {
                ApplyPackage(ReadPackage(selected), selected);
            }
            catch (Exception ex)
            {
                package = null;
                packageLabel.Text = "Pacote: INVALIDO";
                packageLabel.ForeColor = Danger;
                summaryLabel.Text = ex.Message;
                LogUi("PACOTE", "falha ao carregar pacote: " + ex.Message);
            }
        }

        private PgLabPackage ReadPackage(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Arquivo de pacote nao encontrado.", path);
            string json = File.ReadAllText(path, Encoding.UTF8);
            return ParseAndValidatePackage(json);
        }

        private PgLabPackage ParseAndValidatePackage(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            PgLabPackage p = serializer.Deserialize<PgLabPackage>(json);
            if (p == null) throw new InvalidDataException("Pacote vazio.");
            if (p.schemaVersion != 1) throw new InvalidDataException("schemaVersion de pacote nao suportada.");
            if (string.IsNullOrEmpty(p.packageVersion)) throw new InvalidDataException("packageVersion ausente.");
            if (p.serialProfiles == null || p.serialProfiles.Count == 0) throw new InvalidDataException("Nenhum perfil serial definido.");
            if (p.steps == null || p.steps.Count == 0) throw new InvalidDataException("Nenhuma etapa definida.");

            bool hasHandshake = false;
            foreach (PgLabStep step in p.steps)
            {
                if (step == null) continue;
                string cls = SafeUpper(step.safetyClass);
                string tx = NormalizeHex(step.txHex);
                if (cls == "HANDSHAKE")
                {
                    if (tx != NormalizeHex(BuiltInHello))
                        throw new InvalidDataException("Pacote tentou alterar o TX HANDSHAKE protegido.");
                    hasHandshake = true;
                }
                if (cls == "BLOCKED" && step.enabled)
                    throw new InvalidDataException("Etapa BLOCKED nao pode estar habilitada.");
            }
            if (!hasHandshake) throw new InvalidDataException("Etapa HANDSHAKE obrigatoria ausente.");
            return p;
        }

        private void ApplyPackage(PgLabPackage p, string source)
        {
            package = p;
            packageLabel.Text = "Pacote: " + p.packageVersion + "  |  motor " + EngineVersion;
            packageLabel.ForeColor = Success;
            int enabled = 0;
            int candidates = 0;
            foreach (PgLabStep s in p.steps)
            {
                if (s != null && s.enabled) enabled++;
                if (s != null && SafeUpper(s.safetyClass) == "CANDIDATE") candidates++;
            }
            summaryLabel.Text = "Fonte: " + source + "  |  " + p.serialProfiles.Count.ToString(CultureInfo.InvariantCulture) + " perfis  |  " + enabled.ToString(CultureInfo.InvariantCulture) + " etapas habilitadas  |  " + candidates.ToString(CultureInfo.InvariantCulture) + " candidatos registrados.";
            RefreshStepsView();
            LogUi("PACOTE", "carregado " + p.packageVersion + " de " + source);
        }

        private void RefreshStepsView()
        {
            stepsView.Items.Clear();
            if (package == null || package.steps == null) return;
            foreach (PgLabStep s in package.steps)
            {
                if (s == null) continue;
                string state = s.enabled ? "ATIVA" : "DESATIVADA";
                string note = SafeUpper(s.safetyClass) == "READ_ONLY_VERIFIED" ? "exige permissao manual" : string.Empty;
                if (SafeUpper(s.safetyClass) == "CANDIDATE") note = "somente registro; nao transmite";
                if (SafeUpper(s.safetyClass) == "BLOCKED") note = "bloqueada pelo pacote e pelo motor";
                ListViewItem item = new ListViewItem(s.name ?? s.id ?? "etapa");
                item.SubItems.Add(s.safetyClass ?? string.Empty);
                item.SubItems.Add(state);
                item.SubItems.Add(s.txHex ?? string.Empty);
                item.SubItems.Add(note);
                stepsView.Items.Add(item);
            }
        }

        private void StartPackageAutoUpdate()
        {
            Thread t = new Thread(new ThreadStart(delegate { UpdatePackageWorker(false); }));
            t.IsBackground = true;
            t.Start();
        }

        private void StartPackageManualUpdate()
        {
            Thread t = new Thread(new ThreadStart(delegate { UpdatePackageWorker(true); }));
            t.IsBackground = true;
            t.Start();
        }

        private void UpdatePackageWorker(bool verbose)
        {
            try
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers[HttpRequestHeader.UserAgent] = "OpenLadder-Studio-PG-Lab/" + EngineVersion;
                    string json = wc.DownloadString(RemotePackageUrl + "?v=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                    PgLabPackage remote = ParseAndValidatePackage(json);
                    string localVersion = package == null ? string.Empty : package.packageVersion;
                    if (!string.Equals(localVersion, remote.packageVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        string cache = GetCachePackagePath();
                        Directory.CreateDirectory(Path.GetDirectoryName(cache));
                        File.WriteAllText(cache, json, new UTF8Encoding(false));
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            ApplyPackage(remote, "cache atualizado automaticamente");
                            LogUi("PACOTE", "atualizado sem reinstalar o OpenLadder Studio.");
                        }));
                    }
                    else if (verbose)
                    {
                        BeginInvoke(new MethodInvoker(delegate { LogUi("PACOTE", "ja esta na versao mais recente: " + localVersion); }));
                    }
                }
            }
            catch (Exception ex)
            {
                if (verbose && !IsDisposed)
                    BeginInvoke(new MethodInvoker(delegate { LogUi("PACOTE", "nao foi possivel atualizar agora: " + ex.Message); }));
            }
        }

        private void StartTest()
        {
            if (running) return;
            if (package == null)
            {
                MessageBox.Show(this, "Nenhum pacote de testes valido esta carregado.", "Laboratorio PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM do TP02.", "Laboratorio PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            bool readOnlyApproved = allowReadOnly.Checked;
            cancelRequested = false;
            running = true;
            runButton.Enabled = false;
            stopButton.Enabled = true;
            exportButton.Enabled = false;
            stateLabel.Text = "EXECUTANDO TESTE COMPLETO...";
            stateLabel.ForeColor = Warning;
            logBox.Clear();

            report = new PgLabReport();
            report.engineVersion = EngineVersion;
            report.packageVersion = package.packageVersion;
            report.startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            report.port = portName;
            report.result = "RUNNING";
            report.profile = string.Empty;
            report.response = string.Empty;
            report.events = new List<PgLabReportEvent>();

            LogUi("INICIO", "motor=" + EngineVersion + " pacote=" + package.packageVersion + " porta=" + portName);
            LogUi("SEGURANCA", "READ_ONLY=" + (readOnlyApproved ? "AUTORIZADO PELO USUARIO" : "DESATIVADO") + "; candidatos e BLOCKED nunca sao enviados.");

            Thread worker = new Thread(new ThreadStart(delegate { RunFullTest(portName, readOnlyApproved); }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunFullTest(string portName, bool readOnlyApproved)
        {
            Stopwatch totalWatch = Stopwatch.StartNew();
            try
            {
                int handshakeIndex = FindHandshakeStepIndex();
                if (handshakeIndex < 0) throw new InvalidDataException("Etapa HANDSHAKE habilitada nao encontrada.");
                PgLabStep handshake = package.steps[handshakeIndex];
                int sweeps = package.sweeps <= 0 ? 1 : package.sweeps;
                bool anyRx = false;

                for (int sweep = 1; sweep <= sweeps && !cancelRequested; sweep++)
                {
                    LogEvent("CICLO", "varredura " + sweep.ToString(CultureInfo.InvariantCulture) + "/" + sweeps.ToString(CultureInfo.InvariantCulture), string.Empty, null, totalWatch.ElapsedMilliseconds);
                    if (sweep > 1)
                    {
                        PulseSerialLines(portName);
                        Thread.Sleep(900);
                    }

                    foreach (PgLabProfile profile in package.serialProfiles)
                    {
                        if (cancelRequested) break;
                        SerialPort port = null;
                        try
                        {
                            port = OpenPort(portName, profile);
                            LogEvent("PERFIL", DescribeProfile(profile), string.Empty, null, totalWatch.ElapsedMilliseconds);
                            int attempts = profile.attempts <= 0 ? 1 : profile.attempts;
                            for (int attempt = 1; attempt <= attempts && !cancelRequested; attempt++)
                            {
                                string reason;
                                if (!IsStepAllowed(handshake, readOnlyApproved, out reason))
                                    throw new InvalidOperationException("HANDSHAKE recusado pelo Safety Gate: " + reason);

                                byte[] tx = ParseHex(handshake.txHex);
                                port.DiscardInBuffer();
                                Stopwatch sw = Stopwatch.StartNew();
                                port.Write(tx, 0, tx.Length);
                                RecordFrame("TX", "HANDSHAKE tentativa " + attempt.ToString(CultureInfo.InvariantCulture), tx, sw.ElapsedMilliseconds);

                                int rxWindow = profile.rxWindowMs > 0 ? profile.rxWindowMs : (handshake.timeoutMs > 0 ? handshake.timeoutMs : 1500);
                                byte[] raw = ReadBurst(port, rxWindow, 220);
                                sw.Stop();
                                if (raw.Length == 0)
                                {
                                    LogEvent("RX", "[]", string.Empty, null, sw.ElapsedMilliseconds);
                                    Thread.Sleep(profile.interAttemptMs > 0 ? profile.interAttemptMs : 120);
                                    continue;
                                }

                                anyRx = true;
                                RecordFrame("RX RAW", "resposta ao HANDSHAKE", raw, sw.ElapsedMilliseconds);
                                byte[] withoutEcho = RemoveLeadingExactEcho(raw, tx);
                                if (withoutEcho.Length != raw.Length)
                                    RecordFrame("RX SEM ECO", "eco exato removido", withoutEcho, sw.ElapsedMilliseconds);
                                else
                                    LogEvent("ECO", "nenhum eco exato do TX", string.Empty, null, sw.ElapsedMilliseconds);

                                List<byte[]> checksumFrames = DiscoverChecksumFrames(withoutEcho);
                                foreach (byte[] f in checksumFrames)
                                    RecordFrame("FRAME FF", "candidato por soma modulo 256 = FF", f, sw.ElapsedMilliseconds);

                                PgLabExpected matched = MatchExpected(handshake, withoutEcho);
                                if (matched == null)
                                {
                                    LogEvent("DESCONHECIDO", "houve RX, mas nenhuma resposta esperada do pacote foi localizada.", string.Empty, null, sw.ElapsedMilliseconds);
                                    if (package.stopOnUnknown)
                                    {
                                        FinishRun("UNKNOWN_RESPONSE", DescribeProfile(profile), ToHex(withoutEcho));
                                        return;
                                    }
                                    Thread.Sleep(profile.interAttemptMs > 0 ? profile.interAttemptMs : 120);
                                    continue;
                                }

                                LogEvent("LINK", "ESTABLISHED - " + matched.name + " com " + DescribeProfile(profile), string.Empty, null, sw.ElapsedMilliseconds);
                                if (report != null)
                                {
                                    report.profile = DescribeProfile(profile);
                                    report.response = NormalizeHex(matched.hex);
                                }

                                bool sequenceOk = RunPostHandshakeSteps(port, handshakeIndex + 1, readOnlyApproved, totalWatch);
                                if (!sequenceOk) return;
                                FinishRun("SUCCESS", DescribeProfile(profile), NormalizeHex(matched.hex));
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogEvent("ERRO", DescribeProfile(profile) + ": " + ex.Message, string.Empty, null, totalWatch.ElapsedMilliseconds);
                        }
                        finally
                        {
                            if (port != null)
                            {
                                try { if (port.IsOpen) port.Close(); } catch { }
                                port.Dispose();
                            }
                            Thread.Sleep(250);
                        }
                    }
                }

                if (cancelRequested) FinishRun("CANCELLED", string.Empty, string.Empty);
                else FinishRun(anyRx ? "NO_KNOWN_RESPONSE" : "NO_RX", string.Empty, string.Empty);
            }
            catch (Exception ex)
            {
                LogEvent("FATAL", ex.Message, string.Empty, null, totalWatch.ElapsedMilliseconds);
                FinishRun("ERROR", string.Empty, string.Empty);
            }
        }

        private bool RunPostHandshakeSteps(SerialPort port, int startIndex, bool readOnlyApproved, Stopwatch totalWatch)
        {
            for (int i = startIndex; i < package.steps.Count && !cancelRequested; i++)
            {
                PgLabStep step = package.steps[i];
                if (step == null || !step.enabled) continue;
                string type = SafeUpper(step.type);
                if (type == "PASSIVE")
                {
                    int ms = step.passiveAfterMs > 0 ? step.passiveAfterMs : package.passiveCaptureMs;
                    if (ms <= 0) ms = 5000;
                    LogEvent("PASSIVO", step.name + " por " + ms.ToString(CultureInfo.InvariantCulture) + " ms", string.Empty, null, totalWatch.ElapsedMilliseconds);
                    byte[] passive = ReadBurst(port, ms, 300);
                    if (passive.Length == 0)
                        LogEvent("PASSIVO RX", "nenhum byte adicional", string.Empty, null, totalWatch.ElapsedMilliseconds);
                    else
                    {
                        RecordFrame("PASSIVO RX", step.name, passive, totalWatch.ElapsedMilliseconds);
                        foreach (byte[] f in DiscoverChecksumFrames(passive))
                            RecordFrame("FRAME FF", "candidato encontrado na captura passiva", f, totalWatch.ElapsedMilliseconds);
                    }
                    continue;
                }

                string reason;
                if (!IsStepAllowed(step, readOnlyApproved, out reason))
                {
                    LogEvent("BLOQUEIO", step.name + " - " + reason, string.Empty, null, totalWatch.ElapsedMilliseconds);
                    continue;
                }

                byte[] tx = ParseHex(step.txHex);
                port.DiscardInBuffer();
                Stopwatch sw = Stopwatch.StartNew();
                port.Write(tx, 0, tx.Length);
                RecordFrame("TX", step.name, tx, sw.ElapsedMilliseconds);
                byte[] raw = ReadBurst(port, step.timeoutMs > 0 ? step.timeoutMs : 2000, 250);
                sw.Stop();
                if (raw.Length == 0)
                {
                    LogEvent("RX", step.name + " -> []", string.Empty, null, sw.ElapsedMilliseconds);
                    if (package.stopOnUnknown)
                    {
                        FinishRun("STEP_NO_RX", report == null ? string.Empty : report.profile, step.id);
                        return false;
                    }
                }
                else
                {
                    RecordFrame("RX RAW", step.name, raw, sw.ElapsedMilliseconds);
                    byte[] noEcho = RemoveLeadingExactEcho(raw, tx);
                    PgLabExpected matched = MatchExpected(step, noEcho);
                    if (matched != null)
                        LogEvent("ETAPA", step.name + " confirmou " + matched.name, string.Empty, null, sw.ElapsedMilliseconds);
                    else if (step.expected != null && step.expected.Count > 0)
                    {
                        LogEvent("DESCONHECIDO", step.name + " retornou quadro fora da lista esperada.", string.Empty, null, sw.ElapsedMilliseconds);
                        if (package.stopOnUnknown)
                        {
                            FinishRun("UNKNOWN_STEP_RESPONSE", report == null ? string.Empty : report.profile, ToHex(noEcho));
                            return false;
                        }
                    }
                    foreach (byte[] f in DiscoverChecksumFrames(noEcho))
                        RecordFrame("FRAME FF", "candidato apos " + step.name, f, sw.ElapsedMilliseconds);
                }

                if (step.passiveAfterMs > 0)
                {
                    byte[] extra = ReadBurst(port, step.passiveAfterMs, 250);
                    if (extra.Length > 0) RecordFrame("PASSIVO RX", "apos " + step.name, extra, totalWatch.ElapsedMilliseconds);
                }
            }
            return !cancelRequested;
        }

        private bool IsStepAllowed(PgLabStep step, bool readOnlyApproved, out string reason)
        {
            reason = string.Empty;
            if (step == null) { reason = "etapa nula"; return false; }
            if (SafeUpper(step.type) == "PASSIVE") return true;
            string cls = SafeUpper(step.safetyClass);
            string tx = NormalizeHex(step.txHex);

            if (IsBuiltInBlocked(tx))
            {
                reason = "quadro consta no bloqueio interno do motor";
                return false;
            }
            if (PackageBlocked(tx))
            {
                reason = "quadro consta na blockedTx do pacote";
                return false;
            }
            if (cls == "HANDSHAKE")
            {
                if (tx == NormalizeHex(BuiltInHello)) return true;
                reason = "HANDSHAKE diferente do CON-ICB<CR> protegido";
                return false;
            }
            if (cls == "READ_ONLY_VERIFIED")
            {
                if (!readOnlyApproved)
                {
                    reason = "READ_ONLY exige autorizacao manual na caixa de selecao";
                    return false;
                }
                if (!PackageReadOnlyAllowed(tx))
                {
                    reason = "quadro nao esta na readOnlyAllowlist do pacote";
                    return false;
                }
                return true;
            }
            if (cls == "CANDIDATE")
            {
                reason = "CANDIDATE e somente informativo";
                return false;
            }
            if (cls == "BLOCKED")
            {
                reason = "etapa classificada como BLOCKED";
                return false;
            }
            reason = "classe de seguranca nao reconhecida";
            return false;
        }

        private bool IsBuiltInBlocked(string tx)
        {
            string n = NormalizeHex(tx);
            return n == "0F 00 F0" || n == "F0 00 0F";
        }

        private bool PackageBlocked(string tx)
        {
            if (package == null || package.safety == null || package.safety.blockedTx == null) return false;
            string n = NormalizeHex(tx);
            foreach (string b in package.safety.blockedTx)
                if (NormalizeHex(b) == n) return true;
            return false;
        }

        private bool PackageReadOnlyAllowed(string tx)
        {
            if (package == null || package.safety == null || package.safety.readOnlyAllowlist == null) return false;
            string n = NormalizeHex(tx);
            foreach (string a in package.safety.readOnlyAllowlist)
                if (NormalizeHex(a) == n) return true;
            return false;
        }

        private int FindHandshakeStepIndex()
        {
            for (int i = 0; i < package.steps.Count; i++)
            {
                PgLabStep s = package.steps[i];
                if (s != null && s.enabled && SafeUpper(s.safetyClass) == "HANDSHAKE" && s.sweepProfiles) return i;
            }
            return -1;
        }

        private SerialPort OpenPort(string portName, PgLabProfile profile)
        {
            SerialPort port = new SerialPort(portName, profile.baud <= 0 ? 19200 : profile.baud, ParseParity(profile.parity), profile.dataBits <= 0 ? 8 : profile.dataBits, ParseStopBits(profile.stopBits));
            port.Handshake = Handshake.None;
            port.DtrEnable = profile.dtr;
            port.RtsEnable = profile.rts;
            port.ReadTimeout = 80;
            port.WriteTimeout = 1000;
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            Thread.Sleep(140);
            return port;
        }

        private void PulseSerialLines(string portName)
        {
            SerialPort pulse = null;
            try
            {
                pulse = new SerialPort(portName, 19200, Parity.None, 8, StopBits.One);
                pulse.Handshake = Handshake.None;
                pulse.DtrEnable = false;
                pulse.RtsEnable = false;
                pulse.Open();
                Thread.Sleep(500);
                pulse.DtrEnable = true;
                pulse.RtsEnable = true;
                Thread.Sleep(250);
                pulse.DtrEnable = false;
                pulse.RtsEnable = false;
                Thread.Sleep(250);
                LogEvent("RECOVERY", "DTR/RTS alternados sem transmitir bytes.", string.Empty, null, 0);
            }
            catch (Exception ex)
            {
                LogEvent("RECOVERY", "falha no rearme: " + ex.Message, string.Empty, null, 0);
            }
            finally
            {
                if (pulse != null)
                {
                    try { if (pulse.IsOpen) pulse.Close(); } catch { }
                    pulse.Dispose();
                }
            }
        }

        private void FinishRun(string result, string profile, string response)
        {
            if (report != null)
            {
                report.finishedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                report.result = result;
                if (!string.IsNullOrEmpty(profile)) report.profile = profile;
                if (!string.IsNullOrEmpty(response)) report.response = response;
            }
            SaveReportsAutomatically();
            if (IsDisposed) return;
            BeginInvoke(new MethodInvoker(delegate
            {
                running = false;
                runButton.Enabled = true;
                stopButton.Enabled = false;
                exportButton.Enabled = !string.IsNullOrEmpty(lastReportTxt);
                stateLabel.Text = result;
                stateLabel.ForeColor = result == "SUCCESS" ? Success : (result == "CANCELLED" ? Warning : Danger);
                LogUi("RESULTADO", result + (string.IsNullOrEmpty(profile) ? string.Empty : " | " + profile));
                if (!string.IsNullOrEmpty(lastReportTxt)) LogUi("RELATORIO", lastReportTxt);
            }));
        }

        private void SaveReportsAutomatically()
        {
            try
            {
                string dir = GetReportsDirectory();
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string baseName = "TP02-PG-Lab-" + stamp;
                string txt = Path.Combine(dir, baseName + ".txt");
                string json = Path.Combine(dir, baseName + ".json");
                string logText = GetLogTextSafe();
                File.WriteAllText(txt, logText, new UTF8Encoding(false));
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                File.WriteAllText(json, serializer.Serialize(report), new UTF8Encoding(false));
                lastReportTxt = txt;
                lastReportJson = json;
            }
            catch (Exception ex)
            {
                LogEvent("RELATORIO", "falha ao salvar automaticamente: " + ex.Message, string.Empty, null, 0);
            }
        }

        private string GetLogTextSafe()
        {
            if (logBox.InvokeRequired)
            {
                string value = string.Empty;
                logBox.Invoke(new MethodInvoker(delegate { value = logBox.Text; }));
                return value;
            }
            return logBox.Text;
        }

        private void ExportReport()
        {
            if (string.IsNullOrEmpty(lastReportTxt) || !File.Exists(lastReportTxt)) return;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Relatorio de texto (*.txt)|*.txt|Relatorio JSON (*.json)|*.json";
            dlg.FileName = Path.GetFileName(lastReportTxt);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                if (string.Equals(Path.GetExtension(dlg.FileName), ".json", StringComparison.OrdinalIgnoreCase))
                    File.Copy(lastReportJson, dlg.FileName, true);
                else
                    File.Copy(lastReportTxt, dlg.FileName, true);
                MessageBox.Show(this, "Relatorio exportado.", "Laboratorio PG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Falha ao exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RecordFrame(string kind, string detail, byte[] bytes, long elapsedMs)
        {
            LogEvent(kind, detail, string.Empty, bytes, elapsedMs);
        }

        private void LogEvent(string kind, string detail, string unused, byte[] bytes, long elapsedMs)
        {
            string hex = bytes == null ? string.Empty : ToHex(bytes);
            int sum = bytes == null ? -1 : Sum8(bytes);
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + kind.PadRight(12) + detail;
            if (!string.IsNullOrEmpty(hex)) line += "  [" + hex + "] soma=0x" + sum.ToString("X2", CultureInfo.InvariantCulture);
            if (elapsedMs > 0) line += "  t=" + elapsedMs.ToString(CultureInfo.InvariantCulture) + "ms";
            AppendLogSafe(line);

            lock (reportLock)
            {
                if (report != null && report.events != null)
                {
                    PgLabReportEvent ev = new PgLabReportEvent();
                    ev.time = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    ev.kind = kind;
                    ev.detail = detail;
                    ev.hex = hex;
                    ev.sum8 = sum;
                    ev.elapsedMs = elapsedMs;
                    report.events.Add(ev);
                }
            }
        }

        private void LogUi(string kind, string detail)
        {
            LogEvent(kind, detail, string.Empty, null, 0);
        }

        private void AppendLogSafe(string line)
        {
            if (logBox.IsDisposed) return;
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new MethodInvoker(delegate { AppendLogSafe(line); }));
                return;
            }
            logBox.AppendText(line + Environment.NewLine);
        }

        private PgLabExpected MatchExpected(PgLabStep step, byte[] bytes)
        {
            if (step.expected == null || bytes == null) return null;
            foreach (PgLabExpected expected in step.expected)
            {
                if (expected == null || string.IsNullOrEmpty(expected.hex)) continue;
                byte[] pattern = ParseHex(expected.hex);
                if (IndexOfSequence(bytes, pattern) >= 0 && Sum8(pattern) == 0xFF) return expected;
            }
            return null;
        }

        private List<byte[]> DiscoverChecksumFrames(byte[] bytes)
        {
            List<byte[]> frames = new List<byte[]>();
            if (bytes == null || bytes.Length < 3) return frames;
            int maxFrames = 20;
            for (int start = 0; start < bytes.Length && frames.Count < maxFrames; start++)
            {
                int maxLen = Math.Min(32, bytes.Length - start);
                for (int len = 3; len <= maxLen && frames.Count < maxFrames; len++)
                {
                    int sum = 0;
                    for (int j = 0; j < len; j++) sum = (sum + bytes[start + j]) & 0xFF;
                    if (sum != 0xFF) continue;
                    byte[] f = new byte[len];
                    Buffer.BlockCopy(bytes, start, f, 0, len);
                    bool duplicate = false;
                    foreach (byte[] old in frames)
                    {
                        if (ByteArraysEqual(old, f)) { duplicate = true; break; }
                    }
                    if (!duplicate) frames.Add(f);
                }
            }
            return frames;
        }

        private static byte[] ReadBurst(SerialPort port, int totalMs, int idleMs)
        {
            List<byte> data = new List<byte>();
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch idle = Stopwatch.StartNew();
            while (total.ElapsedMilliseconds < totalMs)
            {
                int available = 0;
                try { available = port.BytesToRead; } catch { break; }
                if (available > 0)
                {
                    byte[] buffer = new byte[available];
                    int read = port.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < read; i++) data.Add(buffer[i]);
                    idle.Restart();
                }
                else
                {
                    if (data.Count > 0 && idle.ElapsedMilliseconds >= idleMs) break;
                    Thread.Sleep(10);
                }
            }
            return data.ToArray();
        }

        private static byte[] RemoveLeadingExactEcho(byte[] raw, byte[] tx)
        {
            if (raw == null) return new byte[0];
            if (tx == null || tx.Length == 0 || raw.Length < tx.Length) return raw;
            for (int i = 0; i < tx.Length; i++) if (raw[i] != tx[i]) return raw;
            byte[] result = new byte[raw.Length - tx.Length];
            Buffer.BlockCopy(raw, tx.Length, result, 0, result.Length);
            return result;
        }

        private static int IndexOfSequence(byte[] data, byte[] pattern)
        {
            if (data == null || pattern == null || pattern.Length == 0 || data.Length < pattern.Length) return -1;
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < pattern.Length; j++) if (data[i + j] != pattern[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }

        private static byte[] ParseHex(string text)
        {
            string normalized = NormalizeHex(text);
            if (string.IsNullOrEmpty(normalized)) return new byte[0];
            string[] parts = normalized.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++) bytes[i] = byte.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }

        private static string NormalizeHex(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder sb = new StringBuilder();
            string t = text.Replace("0x", " ").Replace("0X", " ").Replace(",", " ").Replace("-", " ").Replace("[", " ").Replace("]", " ");
            string[] parts = t.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in parts)
            {
                string p = raw.Trim();
                if (p.Length == 1) p = "0" + p;
                if (p.Length != 2) continue;
                byte b;
                if (!byte.TryParse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static int Sum8(byte[] bytes)
        {
            int sum = 0;
            if (bytes != null) foreach (byte b in bytes) sum = (sum + b) & 0xFF;
            return sum;
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static Parity ParseParity(string text)
        {
            string p = SafeUpper(text);
            if (p == "ODD" || p == "O") return Parity.Odd;
            if (p == "EVEN" || p == "E") return Parity.Even;
            if (p == "MARK") return Parity.Mark;
            if (p == "SPACE") return Parity.Space;
            return Parity.None;
        }

        private static StopBits ParseStopBits(string text)
        {
            string s = SafeUpper(text);
            if (s == "TWO" || s == "2") return StopBits.Two;
            if (s == "ONEPOINTFIVE" || s == "1.5") return StopBits.OnePointFive;
            return StopBits.One;
        }

        private static string SafeUpper(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim().ToUpperInvariant();
        }

        private static string DescribeProfile(PgLabProfile p)
        {
            return (p.name ?? "perfil") + " | " + p.baud.ToString(CultureInfo.InvariantCulture) + " " + p.dataBits.ToString(CultureInfo.InvariantCulture) + ParityLetter(p.parity) + StopLetter(p.stopBits) + " | DTR=" + (p.dtr ? "on" : "off") + " RTS=" + (p.rts ? "on" : "off");
        }

        private static string ParityLetter(string value)
        {
            string p = SafeUpper(value);
            if (p == "ODD" || p == "O") return "O";
            if (p == "EVEN" || p == "E") return "E";
            return "N";
        }

        private static string StopLetter(string value)
        {
            string s = SafeUpper(value);
            if (s == "TWO" || s == "2") return "2";
            return "1";
        }

        private static string GetBaseDataDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenLadderStudio", "PG-Lab");
        }

        private static string GetCachePackagePath()
        {
            return Path.Combine(GetBaseDataDirectory(), PackageFileName);
        }

        private static string GetReportsDirectory()
        {
            return Path.Combine(GetBaseDataDirectory(), "Reports");
        }

        private Label NewLabel(string text, float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = color;
            l.Font = new Font("Segoe UI", size, style);
            return l;
        }

        private Label LabelAt(string text, int x, int y, Color color, bool bold)
        {
            Label l = NewLabel(text, 8.8f, bold ? FontStyle.Bold : FontStyle.Regular, color);
            l.Location = new Point(x, y);
            return l;
        }

        private Button ButtonAt(string text, int x, int y, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(width, 30);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(180, 188, 196);
            b.BackColor = primary ? Accent : Color.White;
            b.ForeColor = primary ? Color.White : TextPrimary;
            b.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            return b;
        }
    }
}
