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

# v1.13: teste focado em sessao limpa para a cadeia de leitura do programa.
# A bancada confirmou HELLO-STOP 80 01 09 75, F0 -> 00 02 10 22 CB,
# 38 -> 00 02 00 0A F3 e resposta longa do 34. O F0, porem, pode ficar
# silencioso mesmo apos HELLO valido. Em vez de repetir F0 varias vezes na
# mesma porta aberta, esta versao envia apenas UM F0 por sessao. Se falhar,
# fecha a COM, aguarda e recomeca desde o HELLO. O fluxo nao envia 0A, 14,
# RUN/STOP remoto, download, escrita, erase ou firmware.

$text = Replace-Required $text @'
            Thread worker = new Thread(new ThreadStart(delegate { RunFullTest(portName, readOnlyApproved); }));
'@ @'
            Thread worker = new Thread(new ThreadStart(delegate { RunCleanSessionReadProgram(portName, readOnlyApproved); }));
'@ 'Entrada do teste de sessao limpa'

$text = Replace-Required $text @'
        private void RunFullTest(string portName, bool readOnlyApproved)
'@ @'
        private void RunCleanSessionReadProgram(string portName, bool readOnlyApproved)
        {
            Stopwatch totalWatch = Stopwatch.StartNew();
            const int maxSessions = 12;
            PgLabProfile profile = new PgLabProfile();
            profile.name = "Sessao limpa TP02 - DTR on RTS off";
            profile.baud = 19200;
            profile.dataBits = 8;
            profile.parity = "Odd";
            profile.stopBits = "One";
            profile.dtr = true;
            profile.rts = false;
            profile.attempts = 1;
            profile.rxWindowMs = 1800;
            profile.interAttemptMs = 0;

            byte[] hello = ParseHex(BuiltInHello);
            byte[] helloStop = ParseHex("80 01 09 75");
            byte[] helloRun = ParseHex("C0 01 09 35");
            byte[] f0 = ParseHex("F0 00 0F");
            byte[] goodF0 = ParseHex("00 02 10 22 CB");
            byte[] cmd38 = ParseHex("38 00 C7");
            byte[] good38 = ParseHex("00 02 00 0A F3");
            byte[] cmd34 = ParseHex("34 03 00 00 A0 28");

            try
            {
                if (!readOnlyApproved)
                {
                    LogEvent("BLOQUEIO", "Sessao limpa exige a permissao READ-ONLY do operador.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                    FinishRun("READ_ONLY_REQUIRED", string.Empty, string.Empty);
                    return;
                }

                LogEvent("MODO", "READ PLC focado: HELLO -> F0 -> 38 -> 34; um unico F0 por sessao serial.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                LogEvent("SEGURANCA", "Sem 0A, 14, RUN/STOP remoto, escrita, download, apagamento ou firmware.", string.Empty, null, totalWatch.ElapsedMilliseconds);

                for (int session = 1; session <= maxSessions && !cancelRequested; session++)
                {
                    SerialPort port = null;
                    bool retry = false;
                    try
                    {
                        LogEvent("SESSAO", "limpa " + session.ToString(CultureInfo.InvariantCulture) + "/" + maxSessions.ToString(CultureInfo.InvariantCulture), string.Empty, null, totalWatch.ElapsedMilliseconds);
                        port = OpenPort(portName, profile);
                        Thread.Sleep(650);
                        LogEvent("PERFIL", DescribeProfile(profile), string.Empty, null, totalWatch.ElapsedMilliseconds);

                        byte[] rxHello = CleanTxRx(port, hello, "HELLO", 1800, totalWatch);
                        if (IndexOfSequence(rxHello, helloRun) >= 0)
                        {
                            LogEvent("ESTADO", "PLC em RUN. Coloque o TP02 em STOP e execute novamente.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            FinishRun("PLC_RUN_NEEDS_STOP", DescribeProfile(profile), ToHex(helloRun));
                            return;
                        }
                        if (IndexOfSequence(rxHello, helloStop) < 0)
                        {
                            LogEvent("RETRY", "HELLO-STOP nao confirmado; fechando a COM e iniciando nova sessao.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            retry = true;
                        }
                        else
                        {
                            LogEvent("LINK", "HELLO-STOP confirmado.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            if (report != null)
                            {
                                report.profile = DescribeProfile(profile);
                                report.response = ToHex(helloStop);
                            }

                            Thread.Sleep(180);
                            byte[] rxF0 = CleanTxRx(port, f0, "F0", 1600, totalWatch);
                            if (IndexOfSequence(rxF0, goodF0) < 0)
                            {
                                LogEvent("RETRY", "F0 sem vetor conhecido; nenhum 38/34 sera enviado nesta sessao. Fechando COM.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                retry = true;
                            }
                            else
                            {
                                LogEvent("ETAPA", "F0 VALIDADO: 00 02 10 22 CB", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                Thread.Sleep(120);

                                byte[] rx38 = CleanTxRx(port, cmd38, "38", 1600, totalWatch);
                                if (IndexOfSequence(rx38, good38) < 0)
                                {
                                    LogEvent("RETRY", "38 nao confirmou 00 02 00 0A F3; fechando a sessao sem enviar 34.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    retry = true;
                                }
                                else
                                {
                                    LogEvent("ETAPA", "38 VALIDADO: 00 02 00 0A F3", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    Thread.Sleep(120);

                                    byte[] rx34 = CleanTxRx(port, cmd34, "34", 3200, totalWatch);
                                    byte[] frame34;
                                    if (TryExtractPgLengthFrame(rx34, out frame34))
                                    {
                                        int payloadLen = frame34[1];
                                        LogEvent("ETAPA", "34 VALIDO: flags=0x" + frame34[0].ToString("X2", CultureInfo.InvariantCulture) + " LEN=0x" + payloadLen.ToString("X2", CultureInfo.InvariantCulture) + " (" + payloadLen.ToString(CultureInfo.InvariantCulture) + " bytes)", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                        Save34Dump(frame34);
                                        FinishRun("SUCCESS", DescribeProfile(profile), ToHex(frame34));
                                        return;
                                    }

                                    LogEvent("RETRY", "34 sem quadro LEN/checksum FF valido; fechando a sessao.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    retry = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEvent("ERRO", "sessao " + session.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message, string.Empty, null, totalWatch.ElapsedMilliseconds);
                        retry = true;
                    }
                    finally
                    {
                        if (port != null)
                        {
                            try { if (port.IsOpen) port.Close(); } catch { }
                            port.Dispose();
                        }
                    }

                    if (cancelRequested) break;
                    if (retry && session < maxSessions)
                    {
                        LogEvent("REARM", "COM fechada; aguardando 1500 ms antes da proxima sessao limpa.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                        Thread.Sleep(1500);
                    }
                }

                if (cancelRequested)
                    FinishRun("CANCELLED", string.Empty, string.Empty);
                else
                    FinishRun("CLEAN_SESSION_NO_F0_38_34", DescribeProfile(profile), string.Empty);
            }
            catch (Exception ex)
            {
                LogEvent("FATAL", ex.Message, string.Empty, null, totalWatch.ElapsedMilliseconds);
                FinishRun("ERROR", string.Empty, string.Empty);
            }
        }

        private byte[] CleanTxRx(SerialPort port, byte[] tx, string label, int timeoutMs, Stopwatch totalWatch)
        {
            port.DiscardInBuffer();
            Thread.Sleep(80);
            Stopwatch sw = Stopwatch.StartNew();
            port.Write(tx, 0, tx.Length);
            RecordFrame("TX", label, tx, totalWatch.ElapsedMilliseconds);
            byte[] raw = ReadBurst(port, timeoutMs, 220);
            sw.Stop();
            if (raw.Length == 0)
            {
                LogEvent("RX", label + " -> []", string.Empty, null, sw.ElapsedMilliseconds);
                return raw;
            }

            RecordFrame("RX RAW", label, raw, sw.ElapsedMilliseconds);
            byte[] noEcho = RemoveLeadingExactEcho(raw, tx);
            if (noEcho.Length != raw.Length)
                RecordFrame("RX SEM ECO", label, noEcho, sw.ElapsedMilliseconds);
            foreach (byte[] f in DiscoverChecksumFrames(noEcho))
                RecordFrame("FRAME FF", "candidato apos " + label, f, sw.ElapsedMilliseconds);
            return noEcho;
        }

        private static bool TryExtractPgLengthFrame(byte[] bytes, out byte[] frame)
        {
            frame = new byte[0];
            if (bytes == null || bytes.Length < 3) return false;
            for (int start = 0; start <= bytes.Length - 3; start++)
            {
                int payloadLen = bytes[start + 1];
                int total = payloadLen + 3;
                if (total < 3 || start + total > bytes.Length) continue;
                byte[] candidate = new byte[total];
                Buffer.BlockCopy(bytes, start, candidate, 0, total);
                if (Sum8(candidate) != 0xFF) continue;
                frame = candidate;
                return true;
            }
            return false;
        }

        private void Save34Dump(byte[] frame)
        {
            if (frame == null || frame.Length < 3) return;
            try
            {
                string dir = GetReportsDirectory();
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string framePath = Path.Combine(dir, "TP02-PG-34-frame-" + stamp + ".bin");
                string payloadPath = Path.Combine(dir, "TP02-PG-34-payload-" + stamp + ".bin");
                File.WriteAllBytes(framePath, frame);
                int payloadLen = frame[1];
                byte[] payload = new byte[payloadLen];
                if (payloadLen > 0) Buffer.BlockCopy(frame, 2, payload, 0, payloadLen);
                File.WriteAllBytes(payloadPath, payload);
                LogEvent("DUMP", "34 salvo: " + framePath + " | payload: " + payloadPath, string.Empty, null, 0);
            }
            catch (Exception ex)
            {
                LogEvent("DUMP", "falha ao salvar bloco 34: " + ex.Message, string.Empty, null, 0);
            }
        }

        private void RunFullTest(string portName, bool readOnlyApproved)
'@ 'Motor de sessao limpa e helpers'

if (-not $text.Contains('        private const string EngineVersion = "1.12";')) { throw 'EngineVersion 1.12 nao encontrado apos DecodeV22.' }
$text = $text.Replace('        private const string EngineVersion = "1.12";', '        private const string EngineVersion = "1.13";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.13 aplicado: sessao limpa HELLO-F0-38-34, um F0 por abertura da COM.'
