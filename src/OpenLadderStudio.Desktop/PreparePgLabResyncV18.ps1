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

# v1.8: a bancada da v0.92 mostrou que a variante F0 TX RTS on -> RX RTS off
# pode devolver novamente o HELLO-STOP 80 01 09 75. Isto passa a ser tratado
# como ressincronizacao valida da sessao, nao como quadro desconhecido.
$needle = @'
                        LogEvent("DESCONHECIDO", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " recebeu quadro nao classificado.", string.Empty, null, sw.ElapsedMilliseconds);
                        foreach (byte[] f in DiscoverChecksumFrames(noEcho))
                            RecordFrame("FRAME FF", "candidato apos F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture), f, sw.ElapsedMilliseconds);
                        Thread.Sleep(120);
'@
$replacement = @'
                        if (ContainsKnownHello(noEcho))
                        {
                            LogEvent("RESYNC", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " provocou HELLO conhecido; mantendo a mesma COM e tentando F0 novamente.", string.Empty, null, sw.ElapsedMilliseconds);
                            if (TryF0AfterHelloResync(port, step, totalWatch, v + 1))
                            {
                                success = true;
                                return true;
                            }
                            LogEvent("RESYNC", "HELLO reconhecido, mas o F0 ainda nao confirmou o vetor conhecido apos a sequencia de ressincronizacao.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            Thread.Sleep(120);
                            continue;
                        }

                        LogEvent("DESCONHECIDO", "F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture) + " recebeu quadro nao classificado.", string.Empty, null, sw.ElapsedMilliseconds);
                        foreach (byte[] f in DiscoverChecksumFrames(noEcho))
                            RecordFrame("FRAME FF", "candidato apos F0 variante " + (v + 1).ToString(CultureInfo.InvariantCulture), f, sw.ElapsedMilliseconds);
                        Thread.Sleep(120);
'@
$text = Replace-Required $text $needle $replacement 'Tratamento de HELLO durante F0'

$insertNeedle = @'
        private static void WaitTxDrain(SerialPort port, int maxMs)
'@
$insertReplacement = @'
        private bool TryF0AfterHelloResync(SerialPort port, PgLabStep step, Stopwatch totalWatch, int sourceVariant)
        {
            bool dtr = port.DtrEnable;
            bool initialRts = port.RtsEnable;
            bool[] rtsModes = new bool[] { initialRts, true, false, true };
            int[] delays = new int[] { 80, 220, 450, 700 };

            for (int i = 0; i < rtsModes.Length && !cancelRequested; i++)
            {
                port.DtrEnable = dtr;
                port.RtsEnable = rtsModes[i];
                port.DiscardInBuffer();
                Thread.Sleep(delays[i]);

                byte[] tx = ParseHex(step.txHex);
                Stopwatch sw = Stopwatch.StartNew();
                port.Write(tx, 0, tx.Length);
                WaitTxDrain(port, 300);
                RecordFrame("TX", "F0 RESYNC origem=" + sourceVariant.ToString(CultureInfo.InvariantCulture) + " tentativa=" + (i + 1).ToString(CultureInfo.InvariantCulture) + " | DTR=" + (dtr ? "on" : "off") + " RTS=" + (port.RtsEnable ? "on" : "off") + " delay=" + delays[i].ToString(CultureInfo.InvariantCulture) + "ms", tx, sw.ElapsedMilliseconds);

                byte[] raw = ReadBurst(port, 1700, 220);
                sw.Stop();
                if (raw.Length == 0)
                {
                    LogEvent("RX", "F0 RESYNC tentativa " + (i + 1).ToString(CultureInfo.InvariantCulture) + " -> []", string.Empty, null, sw.ElapsedMilliseconds);
                    continue;
                }

                RecordFrame("RX RAW", "F0 RESYNC tentativa " + (i + 1).ToString(CultureInfo.InvariantCulture), raw, sw.ElapsedMilliseconds);
                byte[] noEcho = RemoveLeadingExactEcho(raw, tx);
                PgLabExpected matched = MatchExpected(step, noEcho);
                if (matched != null && NormalizeHex(matched.hex) == "00 02 10 22 CB")
                {
                    f0ValidatedInCurrentSession = true;
                    LogEvent("PREFLIGHT", "F0 validado apos HELLO de ressincronizacao; RTS=" + (port.RtsEnable ? "on" : "off") + ". O 38 pode seguir na mesma sessao.", string.Empty, null, sw.ElapsedMilliseconds);
                    return true;
                }

                if (ContainsKnownHello(noEcho))
                {
                    LogEvent("RESYNC", "F0 RESYNC tentativa " + (i + 1).ToString(CultureInfo.InvariantCulture) + " recebeu outro HELLO conhecido; continuando na mesma COM.", string.Empty, null, sw.ElapsedMilliseconds);
                    continue;
                }

                LogEvent("DESCONHECIDO", "F0 RESYNC tentativa " + (i + 1).ToString(CultureInfo.InvariantCulture) + " recebeu quadro nao classificado.", string.Empty, null, sw.ElapsedMilliseconds);
                foreach (byte[] f in DiscoverChecksumFrames(noEcho))
                    RecordFrame("FRAME FF", "candidato apos F0 RESYNC", f, sw.ElapsedMilliseconds);
            }
            return false;
        }

        private static bool ContainsKnownHello(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            return IndexOfSequence(bytes, ParseHex("80 01 09 75")) >= 0 ||
                   IndexOfSequence(bytes, ParseHex("C0 01 09 35")) >= 0 ||
                   IndexOfSequence(bytes, ParseHex("0D 01 09 E8")) >= 0;
        }

        private static void WaitTxDrain(SerialPort port, int maxMs)
'@
$text = Replace-Required $text $insertNeedle $insertReplacement 'Metodos de ressincronizacao'

if (-not $text.Contains('        private const string EngineVersion = "1.7";')) { throw 'EngineVersion 1.7 nao encontrado.' }
$text = $text.Replace('        private const string EngineVersion = "1.7";', '        private const string EngineVersion = "1.8";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.8 aplicado: HELLO durante F0 vira ressincronizacao e dispara nova tentativa automatica.'
