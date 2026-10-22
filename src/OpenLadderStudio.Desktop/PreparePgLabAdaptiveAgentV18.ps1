$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02PgLab.build.cs'
if (-not (Test-Path $path)) { throw 'TP02PgLab.build.cs nao encontrado.' }
$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$source, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($source.Contains($needleCrLf)) { return $source.Replace($needleCrLf, $replacementCrLf) }
    if ($source.Contains($needleLf)) { return $source.Replace($needleLf, $replacementLf) }
    throw "$label nao encontrado."
}

# Campos de UI do modo adaptativo.
$text = Replace-Required $text @'
        private CheckBox allowReadOnly;

        private volatile bool running;
'@ @'
        private CheckBox allowReadOnly;
        private CheckBox useAiAgent;
        private Button configureAiButton;

        private volatile bool running;
'@ 'Campos do agente IA'

# Controle do agente no mesmo painel. O modo local continua disponivel desmarcando a caixa.
$text = Replace-Required $text @'
            config.Controls.Add(allowReadOnly);
            allowReadOnly.Checked = true;
            allowReadOnly.Enabled = false;

            Label safety = LabelAt("TESTE UNICO: nunca envia escrita, RUN/STOP remoto, download, apagamento ou firmware. Somente HELLO e consultas READ-ONLY explicitamente permitidas.", 20, 160, Danger, true);
'@ @'
            config.Controls.Add(allowReadOnly);
            allowReadOnly.Checked = true;
            allowReadOnly.Enabled = false;

            useAiAgent = new CheckBox();
            useAiAgent.Text = "AGENTE IA ADAPTATIVO";
            useAiAgent.AutoSize = true;
            useAiAgent.Location = new Point(790, 78);
            useAiAgent.ForeColor = Navy;
            useAiAgent.Font = new Font("Segoe UI Semibold", 8.7f, FontStyle.Bold);
            useAiAgent.Checked = Tp02OpenAiAgent.HasStoredKey();
            config.Controls.Add(useAiAgent);

            configureAiButton = ButtonAt("CONFIGURAR IA", 1015, 72, 150, false);
            configureAiButton.Click += delegate
            {
                try
                {
                    if (Tp02OpenAiAgent.PromptAndSaveApiKey(this))
                    {
                        useAiAgent.Checked = true;
                        LogUi("IA", "OpenAI API configurada para o usuario atual. A chave nao entra no relatorio.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Configurar IA", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            config.Controls.Add(configureAiButton);

            Label safety = LabelAt("AGENTE IA: a IA escolhe somente a proxima acao; o executor local bloqueia bytes arbitrarios, escrita, RUN/STOP, download, apagamento e firmware.", 20, 160, Danger, true);
'@ 'UI do agente adaptativo'

# Desvio para o loop adaptativo antes do teste deterministico.
$text = Replace-Required $text @'
            string portName = portCombo.SelectedItem.ToString();
            bool readOnlyApproved = true;
'@ @'
            string portName = portCombo.SelectedItem.ToString();
            if (useAiAgent != null && useAiAgent.Checked)
            {
                StartAdaptiveAgentTest(portName);
                return;
            }
            bool readOnlyApproved = true;
'@ 'Entrada do modo IA'

$insertAt = $text.IndexOf('        private void RunFullTest(string portName, bool readOnlyApproved)', [System.StringComparison]::Ordinal)
if ($insertAt -lt 0) { throw 'RunFullTest nao encontrado para inserir o agente.' }

$agentMethods = @'
        private void StartAdaptiveAgentTest(string portName)
        {
            string key = Tp02OpenAiAgent.LoadApiKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                if (!Tp02OpenAiAgent.PromptAndSaveApiKey(this)) return;
                key = Tp02OpenAiAgent.LoadApiKey();
                if (string.IsNullOrWhiteSpace(key)) return;
            }

            cancelRequested = false;
            f0ValidatedInCurrentSession = false;
            running = true;
            runButton.Enabled = false;
            stopButton.Enabled = true;
            exportButton.Enabled = false;
            stateLabel.Text = "AGENTE IA ANALISANDO TP02...";
            stateLabel.ForeColor = Warning;
            logBox.Clear();

            report = new PgLabReport();
            report.engineVersion = EngineVersion;
            report.packageVersion = package.packageVersion;
            report.startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            report.port = portName;
            report.result = "RUNNING_AI";
            report.profile = string.Empty;
            report.response = string.Empty;
            report.events = new List<PgLabReportEvent>();

            LogUi("INICIO", "MODO=AGENTE_IA_ADAPTATIVO motor=" + EngineVersion + " pacote=" + package.packageVersion + " porta=" + portName);
            LogUi("SEGURANCA", "A IA nao envia bytes arbitrarios. O executor aceita somente perfis do pacote, HELLO protegido, linhas DTR/RTS, captura passiva e probes READ-ONLY allowlisted.");

            Thread worker = new Thread(new ThreadStart(delegate { RunAdaptiveAgent(portName, key); }));
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunAdaptiveAgent(string portName, string key)
        {
            Stopwatch totalWatch = Stopwatch.StartNew();
            SerialPort port = null;
            PgLabProfile currentProfile = null;
            bool linkEstablished = false;
            string lastTx = string.Empty;
            string lastRx = string.Empty;
            List<string> history = new List<string>();
            int invalidActions = 0;

            try
            {
                Tp02OpenAiAgent agent = new Tp02OpenAiAgent(key);
                LogEvent("IA", "modelo=" + agent.Model + "; loop adaptativo iniciado. Limite local=40 decisoes.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                history.Add("Bancada conhecida: 19200 8O1. HELLO STOP=80 01 09 75. F0 conhecido=00 02 10 22 CB. Na captura v0.92, RTS TX on -> RX off fez reaparecer HELLO STOP.");

                for (int iteration = 1; iteration <= 40 && !cancelRequested; iteration++)
                {
                    Tp02AgentObservation observation = BuildAgentObservation(iteration, portName, port, currentProfile, linkEstablished, lastTx, lastRx, history);
                    Tp02AgentAction action = agent.NextAction(observation);
                    string actionName = SafeUpper(action.action);
                    LogEvent("IA-ORDEM", "#" + iteration.ToString(CultureInfo.InvariantCulture) + " " + actionName + FormatAgentAction(action) + (string.IsNullOrWhiteSpace(action.reason) ? string.Empty : " | " + action.reason), string.Empty, null, totalWatch.ElapsedMilliseconds);

                    string validation;
                    if (!ValidateAgentAction(action, port, currentProfile, linkEstablished, out validation))
                    {
                        invalidActions++;
                        string msg = "ordem recusada pelo Safety Gate local: " + validation;
                        LogEvent("IA-BLOQUEIO", msg, string.Empty, null, totalWatch.ElapsedMilliseconds);
                        AddAgentHistory(history, "REFUSED " + actionName + ": " + validation);
                        if (invalidActions >= 4)
                        {
                            FinishRun("AI_SAFETY_STOP", currentProfile == null ? string.Empty : DescribeProfile(currentProfile), lastRx);
                            return;
                        }
                        continue;
                    }
                    invalidActions = 0;

                    if (actionName == "FINISH")
                    {
                        AddAgentHistory(history, "FINISH: " + (action.reason ?? string.Empty));
                        FinishRun("SUCCESS", currentProfile == null ? string.Empty : DescribeProfile(currentProfile), lastRx);
                        return;
                    }

                    if (actionName == "OPEN_PROFILE")
                    {
                        PgLabProfile selected = FindAgentProfile(action.profile);
                        if (port != null)
                        {
                            try { if (port.IsOpen) port.Close(); } catch { }
                            port.Dispose();
                            port = null;
                        }
                        port = OpenPort(portName, selected);
                        currentProfile = selected;
                        linkEstablished = false;
                        f0ValidatedInCurrentSession = false;
                        lastTx = string.Empty;
                        lastRx = string.Empty;
                        LogEvent("IA-EXEC", "porta aberta: " + DescribeProfile(selected), string.Empty, null, totalWatch.ElapsedMilliseconds);
                        AddAgentHistory(history, "OPEN_PROFILE " + DescribeProfile(selected));
                        continue;
                    }

                    if (actionName == "SET_LINES")
                    {
                        bool beforeDtr = port.DtrEnable;
                        bool beforeRts = port.RtsEnable;
                        if (action.dtr.HasValue) port.DtrEnable = action.dtr.Value;
                        if (action.rts.HasValue) port.RtsEnable = action.rts.Value;
                        int window = ClampAgentWait(action.wait_ms, 350, 5000);
                        Thread.Sleep(25);
                        byte[] spontaneous = ReadBurst(port, window, 180);
                        lastTx = string.Empty;
                        lastRx = ToHex(spontaneous);
                        LogEvent("IA-LINHAS", "DTR " + (beforeDtr ? "on" : "off") + "->" + (port.DtrEnable ? "on" : "off") + ", RTS " + (beforeRts ? "on" : "off") + "->" + (port.RtsEnable ? "on" : "off") + ", captura=" + window.ToString(CultureInfo.InvariantCulture) + "ms", string.Empty, null, totalWatch.ElapsedMilliseconds);
                        if (spontaneous.Length > 0)
                        {
                            RecordFrame("IA-RX", "apos mudanca de linhas sem TX", spontaneous, totalWatch.ElapsedMilliseconds);
                            bool reHello = AgentContainsKnownHello(spontaneous);
                            if (reHello)
                            {
                                linkEstablished = true;
                                f0ValidatedInCurrentSession = false;
                                LogEvent("IA-CLASS", "HELLO conhecido espontaneo; tratando como re-sincronizacao.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            }
                            foreach (byte[] f in DiscoverChecksumFrames(spontaneous)) RecordFrame("FRAME FF", "agente / mudanca de linhas", f, totalWatch.ElapsedMilliseconds);
                        }
                        AddAgentHistory(history, "SET_LINES DTR=" + (port.DtrEnable ? "on" : "off") + " RTS=" + (port.RtsEnable ? "on" : "off") + " RX=" + (string.IsNullOrEmpty(lastRx) ? "[]" : lastRx));
                        continue;
                    }

                    if (actionName == "WAIT" || actionName == "PASSIVE")
                    {
                        int window = ClampAgentWait(action.wait_ms, actionName == "PASSIVE" ? 800 : 350, 5000);
                        byte[] passive = ReadBurst(port, window, 180);
                        lastTx = string.Empty;
                        lastRx = ToHex(passive);
                        if (passive.Length == 0)
                            LogEvent("IA-RX", actionName + " -> []", string.Empty, null, window);
                        else
                        {
                            RecordFrame("IA-RX", actionName + " captura passiva", passive, window);
                            if (AgentContainsKnownHello(passive))
                            {
                                linkEstablished = true;
                                f0ValidatedInCurrentSession = false;
                                LogEvent("IA-CLASS", "HELLO conhecido em captura passiva.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            }
                            foreach (byte[] f in DiscoverChecksumFrames(passive)) RecordFrame("FRAME FF", "agente / passivo", f, window);
                        }
                        AddAgentHistory(history, actionName + " " + window.ToString(CultureInfo.InvariantCulture) + "ms RX=" + (string.IsNullOrEmpty(lastRx) ? "[]" : lastRx));
                        continue;
                    }

                    if (actionName == "HELLO")
                    {
                        PgLabStep handshake = package.steps[FindHandshakeStepIndex()];
                        string reason;
                        if (!IsStepAllowed(handshake, true, out reason)) throw new InvalidOperationException("HELLO bloqueado localmente: " + reason);
                        byte[] tx = ParseHex(handshake.txHex);
                        port.DiscardInBuffer();
                        Stopwatch sw = Stopwatch.StartNew();
                        port.Write(tx, 0, tx.Length);
                        RecordFrame("IA-TX", "HELLO", tx, sw.ElapsedMilliseconds);
                        byte[] raw = ReadBurst(port, ClampAgentWait(action.wait_ms, 1800, 5000), 220);
                        sw.Stop();
                        lastTx = ToHex(tx);
                        lastRx = ToHex(raw);
                        if (raw.Length == 0)
                        {
                            LogEvent("IA-RX", "HELLO -> []", string.Empty, null, sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            RecordFrame("IA-RX", "HELLO", raw, sw.ElapsedMilliseconds);
                            byte[] noEcho = RemoveLeadingExactEcho(raw, tx);
                            PgLabExpected matched = MatchExpected(handshake, noEcho);
                            linkEstablished = matched != null || AgentContainsKnownHello(noEcho);
                            if (linkEstablished)
                            {
                                f0ValidatedInCurrentSession = false;
                                LogEvent("IA-CLASS", "HELLO conhecido; enlace estabelecido/re-sincronizado.", string.Empty, null, sw.ElapsedMilliseconds);
                            }
                            foreach (byte[] f in DiscoverChecksumFrames(noEcho)) RecordFrame("FRAME FF", "agente / HELLO", f, sw.ElapsedMilliseconds);
                        }
                        AddAgentHistory(history, "HELLO TX=" + lastTx + " RX=" + (string.IsNullOrEmpty(lastRx) ? "[]" : lastRx) + " link=" + linkEstablished.ToString());
                        continue;
                    }

                    if (actionName == "PROBE")
                    {
                        PgLabStep step = FindAgentProbe(action.probe_id);
                        string reason;
                        if (!IsStepAllowed(step, true, out reason))
                        {
                            AddAgentHistory(history, "PROBE " + action.probe_id + " BLOCKED: " + reason);
                            LogEvent("IA-BLOQUEIO", "probe " + action.probe_id + ": " + reason, string.Empty, null, totalWatch.ElapsedMilliseconds);
                            continue;
                        }

                        byte[] tx = ParseHex(step.txHex);
                        port.DiscardInBuffer();
                        Stopwatch sw = Stopwatch.StartNew();
                        port.Write(tx, 0, tx.Length);
                        RecordFrame("IA-TX", "probe " + step.id + " - " + step.name, tx, sw.ElapsedMilliseconds);
                        int window = ClampAgentWait(action.wait_ms, step.timeoutMs > 0 ? step.timeoutMs : 2200, 5000);
                        byte[] raw = ReadBurst(port, window, 220);
                        sw.Stop();
                        lastTx = ToHex(tx);
                        lastRx = ToHex(raw);

                        if (raw.Length == 0)
                        {
                            LogEvent("IA-RX", "probe " + step.id + " -> []", string.Empty, null, sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            RecordFrame("IA-RX", "probe " + step.id, raw, sw.ElapsedMilliseconds);
                            byte[] noEcho = RemoveLeadingExactEcho(raw, tx);
                            if (AgentContainsKnownHello(noEcho))
                            {
                                linkEstablished = true;
                                f0ValidatedInCurrentSession = false;
                                LogEvent("IA-CLASS", "probe produziu HELLO conhecido: re-sincronizacao, nao resposta do probe.", string.Empty, null, sw.ElapsedMilliseconds);
                            }
                            PgLabExpected matched = MatchExpected(step, noEcho);
                            if (matched != null)
                            {
                                LogEvent("IA-CLASS", "probe " + step.id + " confirmou " + matched.name, string.Empty, null, sw.ElapsedMilliseconds);
                                if (NormalizeHex(step.txHex) == "F0 00 0F" && NormalizeHex(matched.hex) == "00 02 10 22 CB")
                                {
                                    f0ValidatedInCurrentSession = true;
                                    LogEvent("PREFLIGHT", "F0 validado pelo agente na sessao atual; 38 pode ser considerado pelo Safety Gate.", string.Empty, null, sw.ElapsedMilliseconds);
                                }
                            }
                            foreach (byte[] f in DiscoverChecksumFrames(noEcho)) RecordFrame("FRAME FF", "agente / probe " + step.id, f, sw.ElapsedMilliseconds);
                        }
                        AddAgentHistory(history, "PROBE " + step.id + " TX=" + lastTx + " RX=" + (string.IsNullOrEmpty(lastRx) ? "[]" : lastRx) + " F0=" + f0ValidatedInCurrentSession.ToString());
                        continue;
                    }
                }

                if (cancelRequested) FinishRun("CANCELLED", currentProfile == null ? string.Empty : DescribeProfile(currentProfile), lastRx);
                else FinishRun("AI_LIMIT", currentProfile == null ? string.Empty : DescribeProfile(currentProfile), lastRx);
            }
            catch (Exception ex)
            {
                LogEvent("IA-ERRO", ex.Message, string.Empty, null, totalWatch.ElapsedMilliseconds);
                FinishRun("AI_ERROR", currentProfile == null ? string.Empty : DescribeProfile(currentProfile), lastRx);
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

        private Tp02AgentObservation BuildAgentObservation(int iteration, string portName, SerialPort port, PgLabProfile profile, bool linkEstablished, string lastTx, string lastRx, List<string> history)
        {
            Tp02AgentObservation o = new Tp02AgentObservation();
            o.iteration = iteration;
            o.port = portName;
            o.portOpen = port != null && port.IsOpen;
            o.profile = profile == null ? string.Empty : profile.name;
            o.dtr = port != null && port.IsOpen && port.DtrEnable;
            o.rts = port != null && port.IsOpen && port.RtsEnable;
            o.linkEstablished = linkEstablished;
            o.f0Validated = f0ValidatedInCurrentSession;
            o.lastTx = lastTx ?? string.Empty;
            o.lastRx = lastRx ?? string.Empty;
            o.recentHistory = new List<string>();
            int start = Math.Max(0, history.Count - 24);
            for (int i = start; i < history.Count; i++) o.recentHistory.Add(history[i]);
            o.allowedProfiles = new List<string>();
            foreach (PgLabProfile p in package.serialProfiles) if (p != null && !string.IsNullOrEmpty(p.name)) o.allowedProfiles.Add(p.name);
            o.allowedProbeIds = new List<string>();
            foreach (PgLabStep s in package.steps)
            {
                if (s == null || !s.enabled) continue;
                string cls = SafeUpper(s.safetyClass);
                if ((cls == "READ_ONLY_VERIFIED" || cls == "READ_ONLY_PROBE" || cls == "READ_ONLY_CANDIDATE") && !string.IsNullOrEmpty(s.id))
                    o.allowedProbeIds.Add(s.id);
            }
            return o;
        }

        private bool ValidateAgentAction(Tp02AgentAction action, SerialPort port, PgLabProfile profile, bool linkEstablished, out string reason)
        {
            reason = string.Empty;
            if (action == null) { reason = "acao nula"; return false; }
            string a = SafeUpper(action.action);
            if (a == "FINISH") return true;
            if (a == "OPEN_PROFILE")
            {
                if (FindAgentProfile(action.profile) == null) { reason = "perfil fora da lista do pacote"; return false; }
                return true;
            }
            if (port == null || !port.IsOpen) { reason = "porta ainda nao esta aberta; use open_profile"; return false; }
            if (a == "SET_LINES" || a == "PASSIVE" || a == "WAIT" || a == "HELLO") return true;
            if (a == "PROBE")
            {
                if (!linkEstablished) { reason = "probe exige HELLO conhecido na sessao"; return false; }
                PgLabStep step = FindAgentProbe(action.probe_id);
                if (step == null) { reason = "probe_id fora da lista READ-ONLY habilitada"; return false; }
                string gate;
                if (!IsStepAllowed(step, true, out gate)) { reason = gate; return false; }
                return true;
            }
            reason = "acao nao pertence ao enum local";
            return false;
        }

        private PgLabProfile FindAgentProfile(string name)
        {
            if (package == null || package.serialProfiles == null || string.IsNullOrEmpty(name)) return null;
            foreach (PgLabProfile p in package.serialProfiles)
                if (p != null && string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        private PgLabStep FindAgentProbe(string id)
        {
            if (package == null || package.steps == null || string.IsNullOrEmpty(id)) return null;
            foreach (PgLabStep s in package.steps)
            {
                if (s == null || !s.enabled || !string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase)) continue;
                string cls = SafeUpper(s.safetyClass);
                if (cls == "READ_ONLY_VERIFIED" || cls == "READ_ONLY_PROBE" || cls == "READ_ONLY_CANDIDATE") return s;
            }
            return null;
        }

        private bool AgentContainsKnownHello(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            return IndexOfSequence(bytes, ParseHex("80 01 09 75")) >= 0 ||
                   IndexOfSequence(bytes, ParseHex("C0 01 09 35")) >= 0 ||
                   IndexOfSequence(bytes, ParseHex("0D 01 09 E8")) >= 0;
        }

        private static int ClampAgentWait(int requested, int fallback, int max)
        {
            int value = requested > 0 ? requested : fallback;
            if (value < 50) value = 50;
            if (value > max) value = max;
            return value;
        }

        private static void AddAgentHistory(List<string> history, string line)
        {
            if (history == null) return;
            history.Add(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + line);
            if (history.Count > 80) history.RemoveRange(0, history.Count - 80);
        }

        private static string FormatAgentAction(Tp02AgentAction a)
        {
            if (a == null) return string.Empty;
            StringBuilder b = new StringBuilder();
            if (!string.IsNullOrEmpty(a.profile)) b.Append(" profile='").Append(a.profile).Append("'");
            if (!string.IsNullOrEmpty(a.probe_id)) b.Append(" probe='").Append(a.probe_id).Append("'");
            if (a.dtr.HasValue) b.Append(" DTR=").Append(a.dtr.Value ? "on" : "off");
            if (a.rts.HasValue) b.Append(" RTS=").Append(a.rts.Value ? "on" : "off");
            if (a.wait_ms > 0) b.Append(" wait=").Append(a.wait_ms.ToString(CultureInfo.InvariantCulture)).Append("ms");
            return b.ToString();
        }

'@

$text = $text.Substring(0, $insertAt) + $agentMethods + $text.Substring($insertAt)

if (-not $text.Contains('        private const string EngineVersion = "1.7";')) { throw 'EngineVersion 1.7 nao encontrado.' }
$text = $text.Replace('        private const string EngineVersion = "1.7";', '        private const string EngineVersion = "1.8";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.8 aplicado: agente OpenAI adaptativo com Safety Gate local e sem bytes arbitrarios.'
