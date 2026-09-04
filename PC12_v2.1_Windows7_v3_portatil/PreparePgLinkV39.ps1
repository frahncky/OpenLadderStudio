$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'TP02PgLinkV38.build.cs'
$outputPath = Join-Path (Get-Location) 'TP02PgLinkV39.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

# Hotfix v0.40: a analise estatica do PC12 original mostrou que F0 00 0F
# pertence ao comando PLC -> Clear All Memory (ID de menu 0x142). Portanto,
# esta ferramenta volta a ser estritamente de handshake: somente CON-ICB<CR>
# pode ser transmitido. Nenhum terceiro/segundo comando PG e emitido.
$text = $text.Replace('TP02PgLinkV38Form', 'TP02PgLinkV39Form')
$text = $text.Replace('TP02PgLinkV38Program', 'TP02PgLinkV39Program')
$text = $text.Replace('v0.38', 'v0.40')
$text = $text.Replace('segundo estagio PG com handshake adaptativo', 'validacao PG segura - comandos destrutivos bloqueados')
$text = $text.Replace('AUTO: sequencia validada da v0.34', 'SEGURO: somente CON-ICB<CR> e transmitido')
$text = $text.Replace('TESTAR ETAPA 2 PG', 'TESTAR LINK PG SEGURO')
$text = $text.Replace('1. Reproduz exatamente a sequencia de perfis que funcionou na v0.34.  2. Sao 4 tentativas por perfil, com os mesmos tempos.  3. Somente apos C0 01 09 35 envia F0 00 0F uma vez.  4. Registra RX bruto.  5. Mantem escuta passiva por mais 5 s sem qualquer novo TX.', '1. Reproduz a sequencia de perfis validada no PLC fisico.  2. Procura exclusivamente C0 01 09 35.  3. Quando o Link PG e confirmado, encerra sem transmitir outro quadro.  4. F0 00 0F esta bloqueado: no PC12 original corresponde a Clear All Memory.  5. RUN, STOP, escrita, download e apagamento permanecem bloqueados.')
$text = $text.Replace('2º TX: F0 00 0F · soma modulo 256 = FF · enviado UMA unica vez apos o handshake exato', 'BLOQUEADO: F0 00 0F = Clear All Memory no PC12 original (comando 0x142)')
$text = $text.Replace('MODO SEGURO: se C0 01 09 35 nao for confirmado, F0 00 0F NAO e enviado. Depois de F0 00 0F, a ferramenta somente escuta e registra bytes. RUN, STOP, escrita, download e apagamento continuam bloqueados.', 'MODO SEGURO v0.40: somente CON-ICB<CR> pode ser transmitido. F0 00 0F, Clear Program, Clear All Memory, RUN, STOP, escrita e download permanecem bloqueados.')
$text = $text.Replace('Log("INFO", "A v0.38 reproduz a sequencia de handshake da v0.34 antes de tentar a etapa 2.");', 'Log("INFO", "A v0.40 reproduz somente o handshake validado no PLC fisico.");')
$text = $text.Replace('Log("INFO", "F0 00 0F so sera enviado se C0 01 09 35 for recebido nesta execucao.");', 'Log("SEGURANCA", "F0 00 0F esta bloqueado: a analise do PC12 original o identifica como Clear All Memory.");')
$text = $text.Replace('Log("INFO", "Depois do segundo quadro nenhum outro TX sera realizado.");', 'Log("SEGURANCA", "Depois do HELLO nenhum outro TX sera realizado.");')
$text = $text.Replace(' · ', ' - ')

$stage2Needle = @'
        private static readonly byte[] Pc12Stage2 = new byte[]
        {
            0xF0, 0x00, 0x0F
        };
'@
$stage2Safe = @'
        // BLOQUEADO POR SEGURANCA (v0.40).
        // No PC12 original, F0 00 0F e o comando PLC -> Clear All Memory (0x142).
        private static readonly byte[] Pc12Stage2 = new byte[0];
'@
if (-not $text.Contains($stage2Needle.Trim())) { throw 'Bloco Pc12Stage2 nao encontrado.' }
$text = $text.Replace($stage2Needle.Trim(), $stage2Safe.Trim())

$startAnchor = '        private void TestWorker(string portName)'
$endAnchor = '        private int CaptureAfterStage2(SerialPort port)'
$start = $text.IndexOf($startAnchor)
if ($start -lt 0) { throw 'Inicio de TestWorker nao encontrado em TP02PgLinkV38.build.cs.' }
$end = $text.IndexOf($endAnchor, $start)
if ($end -lt 0) { throw 'Fim de TestWorker nao encontrado em TP02PgLinkV38.build.cs.' }

$replacement = @'
        private void TestWorker(string portName)
        {
            string[] profileNames = new string[]
            {
                "19200 8N1 - DTR/RTS off",
                "19200 8N1 - DTR/RTS on",
                "19200 8O1 - DTR/RTS off",
                "19200 8O1 - DTR/RTS on"
            };
            Parity[] parities = new Parity[] { Parity.None, Parity.None, Parity.Odd, Parity.Odd };
            bool[] dtr = new bool[] { false, true, false, true };
            bool[] rts = new bool[] { false, true, false, true };

            string lastError = string.Empty;
            int totalRxBytes = 0;

            for (int sweep = 1; sweep <= 2 && !cancelRequested; sweep++)
            {
                LogSafe("CICLO", "handshake seguro " + sweep.ToString(CultureInfo.InvariantCulture) + "/2");

                if (sweep == 2)
                {
                    LogSafe("RECOVERY", "nenhum handshake confirmado no primeiro ciclo; rearme das linhas DTR/RTS sem TX de dados.");
                    PulseSerialLines(portName);
                    Thread.Sleep(1000);
                }

                for (int profileIndex = 0; profileIndex < profileNames.Length && !cancelRequested; profileIndex++)
                {
                    SerialPort port = null;
                    try
                    {
                        string profileName = profileNames[profileIndex];
                        int attempts = profileIndex == 3 ? (sweep == 2 ? 8 : 5) : 4;
                        int rxWindow = profileIndex == 3 ? 1700 : 1300;

                        LogSafe("PERFIL", profileName + " - tentativas=" + attempts.ToString(CultureInfo.InvariantCulture));
                        port = new SerialPort(portName, 19200, parities[profileIndex], 8, StopBits.One);
                        port.Handshake = Handshake.None;
                        port.DtrEnable = dtr[profileIndex];
                        port.RtsEnable = rts[profileIndex];
                        port.ReadTimeout = 80;
                        port.WriteTimeout = 1000;
                        port.Open();
                        port.DiscardInBuffer();
                        port.DiscardOutBuffer();
                        Thread.Sleep(profileIndex == 3 ? 180 : 120);

                        LogSafe("PORTA", portName + "  " + profileName);

                        for (int attempt = 1; attempt <= attempts && !cancelRequested; attempt++)
                        {
                            port.DiscardInBuffer();
                            LogSafe("PG HELLO TX", "tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + "  " + ToHex(Pc12Hello));
                            port.Write(Pc12Hello, 0, Pc12Hello.Length);

                            byte[] raw = ReadBurst(port, rxWindow, 220);
                            if (raw.Length == 0)
                            {
                                LogSafe("PG HELLO RX", "[]");
                                Thread.Sleep(profileIndex == 3 ? 180 : 120);
                                continue;
                            }

                            totalRxBytes += raw.Length;
                            TP02PgFrameParserV33.ParseResult parsed = TP02PgFrameParserV33.Parse(raw, Pc12Hello);
                            LogSafe("PG HELLO RX RAW", ToHex(parsed.Raw) + "  soma=0x" + parsed.RawSum.ToString("X2", CultureInfo.InvariantCulture));
                            LogSafe("PG ECO", "quantidade=" + parsed.EchoCount.ToString(CultureInfo.InvariantCulture));
                            LogSafe("PG RX SEM ECO", ToHex(parsed.WithoutEcho) + "  soma=0x" + parsed.WithoutEchoSum.ToString("X2", CultureInfo.InvariantCulture));
                            LogSafe("PG PARSER", parsed.Detail);

                            int knownIndex = IndexOfSequence(parsed.WithoutEcho, KnownHelloResponse);
                            if (knownIndex < 0)
                            {
                                LogSafe("PG BLOQUEIO", "C0 01 09 35 nao foi localizado; nenhum outro comando sera transmitido.");
                                Thread.Sleep(150);
                                continue;
                            }

                            LogSafe("PG FRAME", ToHex(KnownHelloResponse));
                            LogSafe("PG CHECKSUM", "HELLO RX soma modulo 256 = 0x" + Sum8(KnownHelloResponse).ToString("X2", CultureInfo.InvariantCulture));
                            LogSafe("PG LINK", "ESTABLISHED - C0 01 09 35 confirmado com " + profileName + ".");
                            if (parities[profileIndex] == Parity.Odd && dtr[profileIndex] && rts[profileIndex]) SaveProfile(portName);
                            LogSafe("SEGURANCA", "F0 00 0F NAO enviado. No PC12 original este quadro corresponde a Clear All Memory.");
                            LogSafe("EVIDENCIA", "A resposta 40 02 10 22 8B foi observada anteriormente, mas nao sera provocada novamente automaticamente.");
                            FinishHandshakeSafe(true, profileName, totalRxBytes, string.Empty);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        LogSafe("ERRO", profileNames[profileIndex] + ": " + ex.Message);
                    }
                    finally
                    {
                        if (port != null)
                        {
                            try { if (port.IsOpen) port.Close(); } catch { }
                            port.Dispose();
                        }
                        Thread.Sleep(300);
                    }
                }
            }

            if (totalRxBytes == 0)
                LogSafe("PG DIAG", "zero bytes recebidos em todos os perfis e nos dois ciclos.");
            else
                LogSafe("PG DIAG", "houve " + totalRxBytes.ToString(CultureInfo.InvariantCulture) + " byte(s), mas C0 01 09 35 nao foi confirmado.");

            if (cancelRequested) FinishCancelled();
            else FinishHandshakeSafe(false, string.Empty, totalRxBytes, lastError);
        }

        private void PulseSerialLines(string portName)
        {
            SerialPort pulse = null;
            try
            {
                pulse = new SerialPort(portName, 19200, Parity.None, 8, StopBits.One);
                pulse.Handshake = Handshake.None;
                pulse.ReadTimeout = 80;
                pulse.WriteTimeout = 1000;
                pulse.DtrEnable = false;
                pulse.RtsEnable = false;
                pulse.Open();
                Thread.Sleep(600);
                pulse.DtrEnable = true;
                pulse.RtsEnable = true;
                Thread.Sleep(250);
                pulse.DtrEnable = false;
                pulse.RtsEnable = false;
                Thread.Sleep(250);
                LogSafe("RECOVERY", "linhas DTR/RTS alternadas sem envio de qualquer byte.");
            }
            catch (Exception ex)
            {
                LogSafe("RECOVERY", "rearme das linhas nao concluido: " + ex.Message);
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

        private void FinishHandshakeSafe(bool success, string profileName, int totalRxBytes, string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { FinishHandshakeSafe(success, profileName, totalRxBytes, error); }));
                return;
            }

            running = false;
            testButton.Enabled = true;

            if (success)
            {
                profileLabel.Text = "LINK confirmado: " + profileName + " - F0 BLOQUEADO";
                profileLabel.ForeColor = Success;
                SetState("●  LINK PG CONFIRMADO - MODO SEGURO", Success);
                Log("RESULTADO", "O TP02 respondeu ao HELLO com C0 01 09 35 e checksum FF.");
                Log("RESULTADO", "Nenhum quadro posterior foi transmitido.");
                Log("SEGURANCA", "F0 00 0F permanece bloqueado porque o PC12 original o associa a Clear All Memory.");
                Log("RESULTADO", "RUN/STOP/escrita/download/apagamento continuam bloqueados.");
                return;
            }

            SetState("●  HANDSHAKE NAO CONFIRMADO", Danger);
            Log("RESULTADO", "C0 01 09 35 nao foi confirmado nesta execucao.");
            Log("SEGURANCA", "Nenhum comando alem de CON-ICB<CR> foi transmitido.");
            if (totalRxBytes > 0) Log("DETALHE", totalRxBytes.ToString(CultureInfo.InvariantCulture) + " byte(s) recebido(s) no total.");
            if (!string.IsNullOrEmpty(error)) Log("DETALHE", error);
        }

'@

$text = $text.Substring(0, $start) + $replacement + $text.Substring($end)
[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
