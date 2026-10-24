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

$text = Replace-Required $text '            useAiAgent.Text = "AGENTE IA ADAPTATIVO";' '            useAiAgent.Text = "PESQUISA CONTINUA IA";' 'Rotulo do modo continuo'
$text = Replace-Required $text '            stateLabel.Text = "AGENTE IA ANALISANDO TP02...";' '            stateLabel.Text = "PESQUISA CONTINUA TP02...";' 'Estado do modo continuo'
$text = Replace-Required $text '            LogUi("INICIO", "MODO=AGENTE_IA_ADAPTATIVO motor=" + EngineVersion + " pacote=" + package.packageVersion + " porta=" + portName);' '            LogUi("INICIO", "MODO=PESQUISA_CONTINUA_IA motor=" + EngineVersion + " pacote=" + package.packageVersion + " porta=" + portName);' 'Log de inicio continuo'

$text = Replace-Required $text @'
            int invalidActions = 0;

            try
            {
                Tp02OpenAiAgent agent = new Tp02OpenAiAgent(key);
                LogEvent("IA", "modelo=" + agent.Model + "; loop adaptativo iniciado. Limite local=40 decisoes.", string.Empty, null, totalWatch.ElapsedMilliseconds);
'@ @'
            int invalidActions = 0;
            HashSet<string> attemptedProbes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> probesWithRx = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                Tp02PersistentOpenAiAgent agent = new Tp02PersistentOpenAiAgent(key);
                LogEvent("IA", "modelo=" + agent.Model + "; pesquisa continua iniciada. Limite de seguranca=200 decisoes/60 min; COM permanece aberta enquanto util.", string.Empty, null, totalWatch.ElapsedMilliseconds);
'@ 'Agente persistente e conjuntos de fechamento'

$text = Replace-Required $text @'
                for (int iteration = 1; iteration <= 40 && !cancelRequested; iteration++)
                {
                    Tp02AgentObservation observation = BuildAgentObservation(iteration, portName, port, currentProfile, linkEstablished, lastTx, lastRx, history);
                    Tp02AgentAction action = agent.NextAction(observation);
                    string actionName = SafeUpper(action.action);
'@ @'
                for (int iteration = 1; iteration <= 200 && !cancelRequested && totalWatch.Elapsed < TimeSpan.FromMinutes(60); iteration++)
                {
                    bool closureReady = IsProtocolClosureReady(linkEstablished, f0ValidatedInCurrentSession, attemptedProbes, probesWithRx);
                    AddAgentHistory(history, "PROTOCOL_CLOSURE_READY=" + closureReady.ToString().ToLowerInvariant() + " attempted=[" + string.Join(",", new List<string>(attemptedProbes).ToArray()) + "] rx=[" + string.Join(",", new List<string>(probesWithRx).ToArray()) + "]");
                    Tp02AgentObservation observation = BuildAgentObservation(iteration, portName, port, currentProfile, linkEstablished, lastTx, lastRx, history);
                    Tp02AgentAction action = agent.NextAction(observation);
                    string actionName = SafeUpper(action.action);
                    LogEvent("IA-CTX", "turn=" + agent.TurnCount.ToString(CultureInfo.InvariantCulture) + " response_id=" + agent.PreviousResponseId, string.Empty, null, totalWatch.ElapsedMilliseconds);
'@ 'Loop persistente e estado de fechamento'

$text = Replace-Required $text '                    if (!ValidateAgentAction(action, port, currentProfile, linkEstablished, out validation))' '                    if (!ValidateAgentAction(action, port, currentProfile, linkEstablished, closureReady, out validation))' 'Gate de fechamento no FINISH'

$text = Replace-Required $text @'
                    if (actionName == "PROBE")
                    {
                        PgLabStep step = FindAgentProbe(action.probe_id);
'@ @'
                    if (actionName == "PROBE")
                    {
                        PgLabStep step = FindAgentProbe(action.probe_id);
                        if (step != null) attemptedProbes.Add(step.id);
'@ 'Registrar probes tentados'

$text = Replace-Required $text @'
                        else
                        {
                            RecordFrame("IA-RX", "probe " + step.id, raw, sw.ElapsedMilliseconds);
'@ @'
                        else
                        {
                            probesWithRx.Add(step.id);
                            RecordFrame("IA-RX", "probe " + step.id, raw, sw.ElapsedMilliseconds);
'@ 'Registrar probes com RX'

$text = Replace-Required $text @'
        private bool ValidateAgentAction(Tp02AgentAction action, SerialPort port, PgLabProfile profile, bool linkEstablished, out string reason)
        {
            reason = string.Empty;
            if (action == null) { reason = "acao nula"; return false; }
            string a = SafeUpper(action.action);
            if (a == "FINISH") return true;
'@ @'
        private bool ValidateAgentAction(Tp02AgentAction action, SerialPort port, PgLabProfile profile, bool linkEstablished, bool closureReady, out string reason)
        {
            reason = string.Empty;
            if (action == null) { reason = "acao nula"; return false; }
            string a = SafeUpper(action.action);
            if (a == "FINISH")
            {
                if (!closureReady)
                {
                    reason = "finish bloqueado: pesquisa ainda nao atingiu PROTOCOL_CLOSURE_READY";
                    return false;
                }
                return true;
            }
'@ 'Impedir encerramento prematuro'

$insertNeedle = @'
        private PgLabProfile FindAgentProfile(string name)
'@
$insertReplacement = @'
        private bool IsProtocolClosureReady(bool linkEstablished, bool f0Validated, HashSet<string> attempted, HashSet<string> withRx)
        {
            if (!linkEstablished || !f0Validated || attempted == null || withRx == null) return false;
            string[] requiredAttempts = new string[]
            {
                "status-preflight-f0",
                "read-program-preflight-38",
                "read-program-static",
                "read-system-part-1",
                "read-system-part-2",
                "password-prelude-14"
            };
            foreach (string id in requiredAttempts) if (!attempted.Contains(id)) return false;

            // Para considerar o protocolo de leitura suficientemente fechado, exigimos
            // RX real no F0, na leitura de programa e em pelo menos uma leitura de sistema.
            if (!withRx.Contains("status-preflight-f0")) return false;
            if (!withRx.Contains("read-program-static")) return false;
            if (!withRx.Contains("read-system-part-1") && !withRx.Contains("read-system-part-2")) return false;
            return true;
        }

        private PgLabProfile FindAgentProfile(string name)
'@
$text = Replace-Required $text $insertNeedle $insertReplacement 'Criterio local de fechamento do protocolo'

# O script AdaptiveV18 eleva o motor 1.7 -> 1.8. Aqui elevamos o resultado final para 1.9.
if (-not $text.Contains('        private const string EngineVersion = "1.8";')) { throw 'EngineVersion 1.8 nao encontrado apos AdaptiveV18.' }
$text = $text.Replace('        private const string EngineVersion = "1.8";', '        private const string EngineVersion = "1.9";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.9 aplicado: pesquisa continua com conversa persistente, COM preservada e fechamento local do protocolo.'
