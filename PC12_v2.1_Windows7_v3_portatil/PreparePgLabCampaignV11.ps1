$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02PgLab.build.cs'
if (-not (Test-Path $path)) { throw 'TP02PgLab.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$source, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $source.Contains($needle)) { throw "$label nao encontrado." }
    return $source.Replace($needle, $replacement)
}

$pkgNeedle = "        public bool stopOnUnknown { get; set; }`n        public List<PgLabProfile> serialProfiles { get; set; }"
$pkgReplacement = "        public bool stopOnUnknown { get; set; }`n        public PgLabCampaign campaign { get; set; }`n        public List<PgLabProfile> serialProfiles { get; set; }"
if (-not $text.Contains($pkgNeedle)) {
    $pkgNeedle = "        public bool stopOnUnknown { get; set; }`r`n        public List<PgLabProfile> serialProfiles { get; set; }"
    $pkgReplacement = "        public bool stopOnUnknown { get; set; }`r`n        public PgLabCampaign campaign { get; set; }`r`n        public List<PgLabProfile> serialProfiles { get; set; }"
}
$text = Replace-Required $text $pkgNeedle $pkgReplacement 'Propriedades do pacote'

$campaignClass = @'
    internal sealed class PgLabCampaign
    {
        public bool enabled { get; set; }
        public int runs { get; set; }
        public bool rearmBetweenRuns { get; set; }
        public int rearmDelayMs { get; set; }
        public int attemptsPerRun { get; set; }
        public int rxWindowMs { get; set; }
        public int interAttemptMs { get; set; }
        public int passiveAfterEachMs { get; set; }
        public bool stopOnUnknown { get; set; }
    }

'@
$text = Replace-Required $text '    internal sealed class PgLabProfile' ($campaignClass + '    internal sealed class PgLabProfile') 'Ponto de insercao PgLabCampaign'

$reportNeedle = "        public string response { get; set; }`n        public List<PgLabReportEvent> events { get; set; }"
$reportReplacement = "        public string response { get; set; }`n        public PgLabCampaignResult campaign { get; set; }`n        public List<PgLabReportEvent> events { get; set; }"
if (-not $text.Contains($reportNeedle)) {
    $reportNeedle = "        public string response { get; set; }`r`n        public List<PgLabReportEvent> events { get; set; }"
    $reportReplacement = "        public string response { get; set; }`r`n        public PgLabCampaignResult campaign { get; set; }`r`n        public List<PgLabReportEvent> events { get; set; }"
}
$text = Replace-Required $text $reportNeedle $reportReplacement 'Propriedades do relatorio'

$resultClass = @'
    internal sealed class PgLabCampaignResult
    {
        public int requestedRuns { get; set; }
        public int completedRuns { get; set; }
        public int knownResponses { get; set; }
        public int helloC0 { get; set; }
        public int hello80 { get; set; }
        public int otherKnown { get; set; }
        public int noRx { get; set; }
        public int unknown { get; set; }
        public int variantSwitches { get; set; }
        public long minResponseMs { get; set; }
        public long maxResponseMs { get; set; }
        public long totalResponseMs { get; set; }
        public double avgResponseMs { get; set; }
        public string firstKnownHex { get; set; }
        public string lastKnownHex { get; set; }
        public List<string> samples { get; set; }
    }

'@
$text = Replace-Required $text '    internal sealed class PgLabReportEvent' ($resultClass + '    internal sealed class PgLabReportEvent') 'Ponto de insercao PgLabCampaignResult'

$text = Replace-Required $text '        private const string EngineVersion = "1.0";' '        private const string EngineVersion = "1.1";' 'EngineVersion'

$runStart = $text.IndexOf('        private void RunFullTest(string portName, bool readOnlyApproved)', [System.StringComparison]::Ordinal)
$runEnd = $text.IndexOf('        private bool RunPostHandshakeSteps(SerialPort port, int startIndex, bool readOnlyApproved, Stopwatch totalWatch)', $runStart, [System.StringComparison]::Ordinal)
if ($runStart -lt 0 -or $runEnd -lt 0 -or $runEnd -le $runStart) { throw 'Bloco RunFullTest nao localizado.' }

$newRun = @'
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

                                PgLabCampaign campaign = package.campaign;
                                if (campaign != null && campaign.enabled && campaign.runs > 1)
                                {
                                    bool campaignOk = RunHandshakeCampaign(port, profile, handshake, matched, attempt, sw.ElapsedMilliseconds, readOnlyApproved, totalWatch);
                                    if (!campaignOk) return;
                                }

                                bool sequenceOk = RunPostHandshakeSteps(port, handshakeIndex + 1, readOnlyApproved, totalWatch);
                                if (!sequenceOk) return;
                                FinishRun(campaign != null && campaign.enabled && campaign.runs > 1 ? "SUCCESS_CAMPAIGN" : "SUCCESS", DescribeProfile(profile), NormalizeHex(matched.hex));
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

        private bool RunHandshakeCampaign(SerialPort port, PgLabProfile profile, PgLabStep handshake, PgLabExpected firstMatched, int firstAttempt, long firstLatencyMs, bool readOnlyApproved, Stopwatch totalWatch)
        {
            PgLabCampaign c = package.campaign;
            if (c == null || !c.enabled || c.runs <= 1) return true;

            int requested = c.runs;
            if (requested > 50) requested = 50;
            int attempts = c.attemptsPerRun > 0 ? c.attemptsPerRun : (profile.attempts > 0 ? profile.attempts : 1);
            int rxWindow = c.rxWindowMs > 0 ? c.rxWindowMs : (profile.rxWindowMs > 0 ? profile.rxWindowMs : 1800);
            int interAttempt = c.interAttemptMs > 0 ? c.interAttemptMs : (profile.interAttemptMs > 0 ? profile.interAttemptMs : 150);
            int rearmDelay = c.rearmDelayMs > 0 ? c.rearmDelayMs : 900;

            PgLabCampaignResult stats = new PgLabCampaignResult();
            stats.requestedRuns = requested;
            stats.completedRuns = 1;
            stats.samples = new List<string>();
            AddCampaignKnown(stats, firstMatched, 1, firstAttempt, firstLatencyMs);
            if (report != null) report.campaign = stats;

            LogEvent("CAMPANHA", "iniciada caracterizacao do HELLO: " + requested.ToString(CultureInfo.InvariantCulture) + " ciclos; a descoberta atual conta como ciclo 1.", string.Empty, null, totalWatch.ElapsedMilliseconds);
            if (!CheckCampaignPassive(port, c, stats, 1, totalWatch)) return false;

            for (int run = 2; run <= requested && !cancelRequested; run++)
            {
                if (c.rearmBetweenRuns)
                {
                    RearmOpenPort(port, profile, rearmDelay);
                    LogEvent("REARME", "ciclo " + run.ToString(CultureInfo.InvariantCulture) + ": DTR/RTS rearmados sem TX de dados.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                }

                bool known = false;
                bool sawRx = false;
                for (int attempt = 1; attempt <= attempts && !cancelRequested; attempt++)
                {
                    string reason;
                    if (!IsStepAllowed(handshake, readOnlyApproved, out reason))
                        throw new InvalidOperationException("HANDSHAKE recusado pelo Safety Gate durante campanha: " + reason);

                    byte[] tx = ParseHex(handshake.txHex);
                    port.DiscardInBuffer();
                    Stopwatch sw = Stopwatch.StartNew();
                    port.Write(tx, 0, tx.Length);
                    RecordFrame("TX", "CAMPANHA ciclo " + run.ToString(CultureInfo.InvariantCulture) + " tentativa " + attempt.ToString(CultureInfo.InvariantCulture), tx, sw.ElapsedMilliseconds);
                    byte[] raw = ReadBurst(port, rxWindow, 220);
                    sw.Stop();

                    if (raw.Length == 0)
                    {
                        LogEvent("RX", "CAMPANHA ciclo " + run.ToString(CultureInfo.InvariantCulture) + " tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + " -> []", string.Empty, null, sw.ElapsedMilliseconds);
                        Thread.Sleep(interAttempt);
                        continue;
                    }

                    sawRx = true;
                    RecordFrame("RX RAW", "CAMPANHA ciclo " + run.ToString(CultureInfo.InvariantCulture), raw, sw.ElapsedMilliseconds);
                    byte[] withoutEcho = RemoveLeadingExactEcho(raw, tx);
                    if (withoutEcho.Length != raw.Length)
                        RecordFrame("RX SEM ECO", "CAMPANHA eco exato removido", withoutEcho, sw.ElapsedMilliseconds);
                    else
                        LogEvent("ECO", "CAMPANHA nenhum eco exato do TX", string.Empty, null, sw.ElapsedMilliseconds);

                    foreach (byte[] f in DiscoverChecksumFrames(withoutEcho))
                        RecordFrame("FRAME FF", "CAMPANHA candidato por soma modulo 256 = FF", f, sw.ElapsedMilliseconds);

                    PgLabExpected matched = MatchExpected(handshake, withoutEcho);
                    if (matched == null || !IsExactExpected(matched, withoutEcho))
                    {
                        stats.unknown++;
                        stats.completedRuns++;
                        if (stats.samples == null) stats.samples = new List<string>();
                        stats.samples.Add("run=" + run.ToString(CultureInfo.InvariantCulture) + ";attempt=" + attempt.ToString(CultureInfo.InvariantCulture) + ";UNKNOWN=" + ToHex(withoutEcho) + ";ms=" + sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                        if (report != null) report.campaign = stats;
                        LogEvent("CAMPANHA DESCONHECIDO", "ciclo " + run.ToString(CultureInfo.InvariantCulture) + " recebeu quadro diferente das variantes conhecidas: [" + ToHex(withoutEcho) + "]", string.Empty, null, sw.ElapsedMilliseconds);
                        LogCampaignSummary(stats, totalWatch);
                        if (c.stopOnUnknown)
                        {
                            FinishRun("CAMPAIGN_UNKNOWN_RESPONSE", DescribeProfile(profile), ToHex(withoutEcho));
                            return false;
                        }
                        break;
                    }

                    AddCampaignKnown(stats, matched, run, attempt, sw.ElapsedMilliseconds);
                    stats.completedRuns++;
                    if (report != null) report.campaign = stats;
                    LogEvent("CAMPANHA AMOSTRA", "ciclo " + run.ToString(CultureInfo.InvariantCulture) + " = " + matched.name + " tentativa=" + attempt.ToString(CultureInfo.InvariantCulture) + " t=" + sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms", NormalizeHex(matched.hex), ParseHex(matched.hex), sw.ElapsedMilliseconds);
                    known = true;
                    break;
                }

                if (cancelRequested) break;

                if (!known && !sawRx)
                {
                    stats.noRx++;
                    stats.completedRuns++;
                    if (stats.samples == null) stats.samples = new List<string>();
                    stats.samples.Add("run=" + run.ToString(CultureInfo.InvariantCulture) + ";NO_RX");
                    if (report != null) report.campaign = stats;
                    LogEvent("CAMPANHA AMOSTRA", "ciclo " + run.ToString(CultureInfo.InvariantCulture) + " = NO_RX apos " + attempts.ToString(CultureInfo.InvariantCulture) + " tentativas.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                }

                if (!CheckCampaignPassive(port, c, stats, run, totalWatch)) return false;
            }

            if (cancelRequested)
            {
                LogCampaignSummary(stats, totalWatch);
                FinishRun("CANCELLED", DescribeProfile(profile), report == null ? string.Empty : report.response);
                return false;
            }

            LogCampaignSummary(stats, totalWatch);
            return true;
        }

        private void AddCampaignKnown(PgLabCampaignResult stats, PgLabExpected matched, int run, int attempt, long latencyMs)
        {
            string hex = NormalizeHex(matched.hex);
            stats.knownResponses++;
            if (hex == "C0 01 09 35") stats.helloC0++;
            else if (hex == "80 01 09 75") stats.hello80++;
            else stats.otherKnown++;

            if (string.IsNullOrEmpty(stats.firstKnownHex)) stats.firstKnownHex = hex;
            if (!string.IsNullOrEmpty(stats.lastKnownHex) && stats.lastKnownHex != hex) stats.variantSwitches++;
            stats.lastKnownHex = hex;

            if (stats.minResponseMs == 0 || latencyMs < stats.minResponseMs) stats.minResponseMs = latencyMs;
            if (latencyMs > stats.maxResponseMs) stats.maxResponseMs = latencyMs;
            stats.totalResponseMs += latencyMs;
            stats.avgResponseMs = stats.knownResponses == 0 ? 0.0 : ((double)stats.totalResponseMs / (double)stats.knownResponses);
            if (stats.samples == null) stats.samples = new List<string>();
            stats.samples.Add("run=" + run.ToString(CultureInfo.InvariantCulture) + ";attempt=" + attempt.ToString(CultureInfo.InvariantCulture) + ";name=" + (matched.name ?? "KNOWN") + ";hex=" + hex + ";ms=" + latencyMs.ToString(CultureInfo.InvariantCulture));
        }

        private bool CheckCampaignPassive(SerialPort port, PgLabCampaign c, PgLabCampaignResult stats, int run, Stopwatch totalWatch)
        {
            int ms = c.passiveAfterEachMs;
            if (ms <= 0 || cancelRequested) return true;
            byte[] passive = ReadBurst(port, ms, 220);
            if (passive.Length == 0)
            {
                LogEvent("CAMPANHA PASSIVO", "ciclo " + run.ToString(CultureInfo.InvariantCulture) + ": nenhum byte espontaneo em " + ms.ToString(CultureInfo.InvariantCulture) + " ms.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                return true;
            }

            RecordFrame("CAMPANHA PASSIVO RX", "ciclo " + run.ToString(CultureInfo.InvariantCulture), passive, totalWatch.ElapsedMilliseconds);
            foreach (byte[] f in DiscoverChecksumFrames(passive))
                RecordFrame("FRAME FF", "CAMPANHA quadro encontrado na captura passiva", f, totalWatch.ElapsedMilliseconds);
            stats.unknown++;
            if (stats.samples == null) stats.samples = new List<string>();
            stats.samples.Add("run=" + run.ToString(CultureInfo.InvariantCulture) + ";PASSIVE_RX=" + ToHex(passive));
            if (report != null) report.campaign = stats;
            LogCampaignSummary(stats, totalWatch);
            if (c.stopOnUnknown)
            {
                FinishRun("CAMPAIGN_PASSIVE_RX", report == null ? string.Empty : report.profile, ToHex(passive));
                return false;
            }
            return true;
        }

        private void RearmOpenPort(SerialPort port, PgLabProfile profile, int delayMs)
        {
            try
            {
                port.DtrEnable = false;
                port.RtsEnable = false;
                Thread.Sleep(250);
                port.DtrEnable = profile.dtr;
                port.RtsEnable = profile.rts;
                Thread.Sleep(delayMs > 0 ? delayMs : 900);
                port.DiscardInBuffer();
            }
            catch (Exception ex)
            {
                LogEvent("REARME", "falha ao alternar DTR/RTS: " + ex.Message, string.Empty, null, 0);
            }
        }

        private bool IsExactExpected(PgLabExpected expected, byte[] frame)
        {
            if (expected == null || frame == null) return false;
            return NormalizeHex(expected.hex) == NormalizeHex(ToHex(frame));
        }

        private void LogCampaignSummary(PgLabCampaignResult stats, Stopwatch totalWatch)
        {
            if (stats == null) return;
            stats.avgResponseMs = stats.knownResponses == 0 ? 0.0 : ((double)stats.totalResponseMs / (double)stats.knownResponses);
            if (report != null) report.campaign = stats;
            string detail = "runs=" + stats.completedRuns.ToString(CultureInfo.InvariantCulture) + "/" + stats.requestedRuns.ToString(CultureInfo.InvariantCulture)
                + " C0=" + stats.helloC0.ToString(CultureInfo.InvariantCulture)
                + " 80=" + stats.hello80.ToString(CultureInfo.InvariantCulture)
                + " otherKnown=" + stats.otherKnown.ToString(CultureInfo.InvariantCulture)
                + " NO_RX=" + stats.noRx.ToString(CultureInfo.InvariantCulture)
                + " unknown=" + stats.unknown.ToString(CultureInfo.InvariantCulture)
                + " switches=" + stats.variantSwitches.ToString(CultureInfo.InvariantCulture)
                + " latency[min/avg/max]=" + stats.minResponseMs.ToString(CultureInfo.InvariantCulture) + "/" + stats.avgResponseMs.ToString("0.0", CultureInfo.InvariantCulture) + "/" + stats.maxResponseMs.ToString(CultureInfo.InvariantCulture) + " ms";
            LogEvent("CAMPANHA RESUMO", detail, string.Empty, null, totalWatch.ElapsedMilliseconds);
        }

'@

$text = $text.Substring(0, $runStart) + $newRun + $text.Substring($runEnd)

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
