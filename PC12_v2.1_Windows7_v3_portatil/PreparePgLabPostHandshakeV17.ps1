$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02PgLab.build.cs'
if (-not (Test-Path $path)) { throw 'TP02PgLab.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)

# v1.7: a bancada da v0.91 localizou HELLO com DTR=on/RTS=off, mas o F0 ficou
# silencioso. Em uma sessao anterior o F0 respondeu com DTR=on/RTS=on. Portanto,
# depois de um HELLO conhecido, testar automaticamente apenas temporizacao e RTS
# na MESMA porta aberta. Nenhum novo comando e introduzido: o unico TX desta matriz
# continua sendo o F0 00 0F, ja classificado como READ_ONLY_VERIFIED.

$runStart = $text.IndexOf('        private bool RunPostHandshakeSteps(SerialPort port, int startIndex, bool readOnlyApproved, Stopwatch totalWatch)', [System.StringComparison]::Ordinal)
$runEnd = $text.IndexOf('        private bool IsStepAllowed(PgLabStep step, bool readOnlyApproved, out string reason)', $runStart, [System.StringComparison]::Ordinal)
if ($runStart -lt 0 -or $runEnd -lt 0 -or $runEnd -le $runStart) { throw 'RunPostHandshakeSteps nao localizado.' }

$newBlock = @'
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

                if (NormalizeHex(step.txHex) == "F0 00 0F")
                {
                    RunF0PostHandshakeMatrix(port, step, totalWatch);
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

        private bool RunF0PostHandshakeMatrix(SerialPort port, PgLabStep step, Stopwatch totalWatch)
        {
            bool originalDtr = port.DtrEnable;
            bool originalRts = port.RtsEnable;
            bool success = false;
            f0ValidatedInCurrentSession = false;

            string[] labels = new string[]
            {
                "estado vencedor / 120 ms",
                "estado vencedor / 600 ms",
                "RTS on / 120 ms",
                "RTS on / 600 ms",
                "TX RTS on -> RX RTS off",
                "TX RTS off -> RX RTS on"
            };
            bool[] txRts = new bool[] { originalRts, originalRts, true, true, true, false };
            bool[] rxRts = new bool[] { originalRts, originalRts, true, true, false, true };
            int[] preDelay = new int[] { 120, 600, 120, 600, 120, 120 };
            bool[] switchAfterTx = new bool[] { false, false, false, false, true, true };

            LogEvent("POST-HS", "iniciando matriz F0 na mesma sessao; DTR permanece " + (originalDtr ? "on" : "off") + "; RTS vencedor=" + (originalRts ? "on" : "off") + ".", string.Empty, null, totalWatch.ElapsedMilliseconds);

            try
            {
                for (int v = 0; v < labels.Length && !cancelRequested; v++)
                {
                    try
                    {
                        port.DtrEnable = originalDtr;
                        port.RtsEnable = txRts[v];
                        port.DiscardInBuffer();
                        Thread.Sleep(preDelay[v]);

                        byte[] tx = ParseHex(step.txHex);
                        Stopwatch sw = Stopwatch.StartNew();
                        port.Write(tx, 0, tx.Length);
                        WaitTxDrain(port, 300);
                        if (switchAfterTx[v])
                        {
                            port.RtsEnable = rxRts[v];
                            Thread.Sleep(35);
                        }
                        RecordFrame("TX", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + "/" + labels.Length.ToString(CultureInfo.InvariantCulture) + " - " + labels[v] + " | DTR=" + (originalDtr ? "on" : "off") + " RTS-TX=" + (txRts[v] ? "on" : "off") + " RTS-RX=" + (port.RtsEnable ? "on" : "off"), tx, sw.ElapsedMilliseconds);

                        byte[] raw = ReadBurst(port, 1600, 220);
                        sw.Stop();
                        if (raw.Length == 0)
                        {
                            LogEvent("RX", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " -> []", string.Empty, null, sw.ElapsedMilliseconds);
                            Thread.Sleep(120);
                            continue;
                        }

                        RecordFrame("RX RAW", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " - " + labels[v], raw, sw.ElapsedMilliseconds);
                        byte[] noEcho = RemoveLeadingExactEcho(raw, tx);
                        PgLabExpected matched = MatchExpected(step, noEcho);
                        if (matched != null && NormalizeHex(matched.hex) == "00 02 10 22 CB")
                        {
                            f0ValidatedInCurrentSession = true;
                            success = true;
                            LogEvent("PREFLIGHT", "F0 validado na variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " - " + labels[v] + "; RTS mantido em " + (port.RtsEnable ? "on" : "off") + " para o proximo probe.", string.Empty, null, sw.ElapsedMilliseconds);
                            return true;
                        }

                        LogEvent("DESCONHECIDO", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " recebeu quadro nao classificado.", string.Empty, null, sw.ElapsedMilliseconds);
                        foreach (byte[] f in DiscoverChecksumFrames(noEcho))
                            RecordFrame("FRAME FF", "candidato apos F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture), f, sw.ElapsedMilliseconds);
                        Thread.Sleep(120);
                    }
                    catch (Exception ex)
                    {
                        LogEvent("POST-HS", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " falhou: " + ex.Message, string.Empty, null, totalWatch.ElapsedMilliseconds);
                    }
                }
            }
            finally
            {
                if (!success)
                {
                    try
                    {
                        port.DtrEnable = originalDtr;
                        port.RtsEnable = originalRts;
                    }
                    catch { }
                }
            }

            LogEvent("POST-HS", "nenhuma variante F0 respondeu com o vetor fisico conhecido; estado serial vencedor restaurado.", string.Empty, null, totalWatch.ElapsedMilliseconds);
            return false;
        }

        private static void WaitTxDrain(SerialPort port, int maxMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < maxMs)
            {
                try
                {
                    if (port.BytesToWrite <= 0) break;
                }
                catch { break; }
                Thread.Sleep(5);
            }
            Thread.Sleep(20);
        }

'@

$text = $text.Substring(0, $runStart) + $newBlock + $text.Substring($runEnd)
if (-not $text.Contains('        private const string EngineVersion = "1.6";')) { throw 'EngineVersion 1.6 nao encontrado.' }
$text = $text.Replace('        private const string EngineVersion = "1.6";', '        private const string EngineVersion = "1.7";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.7 aplicado: matriz pos-handshake F0 por temporizacao/RTS na mesma sessao.'
