$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'TP02PgLinkV37.cs'
$outputPath = Join-Path (Get-Location) 'TP02PgLinkV38.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$text = $text.Replace('TP02PgLinkV37Form', 'TP02PgLinkV38Form')
$text = $text.Replace('TP02PgLinkV37Program', 'TP02PgLinkV38Program')
$text = $text.Replace('v0.37', 'v0.38')
$text = $text.Replace('segundo estagio PG controlado', 'segundo estagio PG com handshake adaptativo')
$text = $text.Replace('CONFIRMADO: 19200 8O1 · DTR/RTS on', 'AUTO: sequencia validada da v0.34')
$text = $text.Replace('1. Abre COM em 19200/8O1 com DTR/RTS on.  2. Confirma o HELLO conhecido.  3. Somente apos C0 01 09 35 envia F0 00 0F uma vez.  4. Registra RX bruto, remove apenas eco exato de F0 00 0F e calcula a soma.  5. Mantem escuta passiva por mais 5 s sem qualquer novo TX.', '1. Reproduz exatamente a sequencia de perfis que funcionou na v0.34.  2. Sao 4 tentativas por perfil, com os mesmos tempos.  3. Somente apos C0 01 09 35 envia F0 00 0F uma vez.  4. Registra RX bruto.  5. Mantem escuta passiva por mais 5 s sem qualquer novo TX.')
$text = $text.Replace('Log("INFO", "Perfil fisicamente confirmado: 19200 8O1 · DTR/RTS on.");', 'Log("INFO", "A v0.38 reproduz a sequencia de handshake da v0.34 antes de tentar a etapa 2.");')

$startAnchor = '        private void TestWorker(string portName)'
$endAnchor = '        private int CaptureAfterStage2(SerialPort port)'
$start = $text.IndexOf($startAnchor)
if ($start -lt 0) { throw 'Inicio de TestWorker nao encontrado em TP02PgLinkV37.cs.' }
$end = $text.IndexOf($endAnchor, $start)
if ($end -lt 0) { throw 'Fim de TestWorker nao encontrado em TP02PgLinkV37.cs.' }

$replacement = @'
        private void TestWorker(string portName)
        {
            string[] profileNames = new string[]
            {
                "19200 8N1 · DTR/RTS off",
                "19200 8N1 · DTR/RTS on",
                "19200 8O1 · DTR/RTS off",
                "19200 8O1 · DTR/RTS on"
            };
            Parity[] parities = new Parity[] { Parity.None, Parity.None, Parity.Odd, Parity.Odd };
            bool[] dtr = new bool[] { false, true, false, true };
            bool[] rts = new bool[] { false, true, false, true };

            bool handshakeConfirmed = false;
            bool stage2Sent = false;
            bool stage2Received = false;
            string lastError = string.Empty;

            for (int profileIndex = 0; profileIndex < profileNames.Length && !cancelRequested; profileIndex++)
            {
                SerialPort port = null;
                try
                {
                    string profileName = profileNames[profileIndex];
                    LogSafe("PERFIL", profileName);
                    port = new SerialPort(portName, 19200, parities[profileIndex], 8, StopBits.One);
                    port.Handshake = Handshake.None;
                    port.DtrEnable = dtr[profileIndex];
                    port.RtsEnable = rts[profileIndex];
                    port.ReadTimeout = 80;
                    port.WriteTimeout = 1000;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();
                    Thread.Sleep(100);

                    LogSafe("PORTA", portName + "  " + profileName);

                    for (int attempt = 1; attempt <= 4 && !cancelRequested; attempt++)
                    {
                        port.DiscardInBuffer();
                        LogSafe("PG HELLO TX", "tentativa " + attempt.ToString(CultureInfo.InvariantCulture) + "  " + ToHex(Pc12Hello));
                        port.Write(Pc12Hello, 0, Pc12Hello.Length);

                        byte[] raw = ReadBurst(port, 1200, 180);
                        if (raw.Length == 0)
                        {
                            LogSafe("PG HELLO RX", "[]");
                            Thread.Sleep(100);
                            continue;
                        }

                        TP02PgFrameParserV33.ParseResult parsed = TP02PgFrameParserV33.Parse(raw, Pc12Hello);
                        LogSafe("PG HELLO RX RAW", ToHex(parsed.Raw) + "  soma=0x" + parsed.RawSum.ToString("X2", CultureInfo.InvariantCulture));
                        LogSafe("PG ECO", "quantidade=" + parsed.EchoCount.ToString(CultureInfo.InvariantCulture));
                        LogSafe("PG RX SEM ECO", ToHex(parsed.WithoutEcho) + "  soma=0x" + parsed.WithoutEchoSum.ToString("X2", CultureInfo.InvariantCulture));
                        LogSafe("PG PARSER", parsed.Detail);

                        int knownIndex = IndexOfSequence(parsed.WithoutEcho, KnownHelloResponse);
                        if (knownIndex < 0)
                        {
                            LogSafe("PG BLOQUEIO", "C0 01 09 35 nao foi localizado; F0 00 0F permanece bloqueado.");
                            Thread.Sleep(100);
                            continue;
                        }

                        handshakeConfirmed = true;
                        LogSafe("PG FRAME", ToHex(KnownHelloResponse));
                        LogSafe("PG CHECKSUM", "HELLO RX soma modulo 256 = 0x" + Sum8(KnownHelloResponse).ToString("X2", CultureInfo.InvariantCulture));
                        LogSafe("PG LINK", "ESTABLISHED - C0 01 09 35 confirmado com " + profileName + ".");
                        if (parities[profileIndex] == Parity.Odd && dtr[profileIndex] && rts[profileIndex]) SaveProfile(portName);
                        SetState("●  LINK CONFIRMADO · ENVIANDO ETAPA 2...", Success);

                        int inlineOffset = knownIndex + KnownHelloResponse.Length;
                        if (inlineOffset < parsed.WithoutEcho.Length)
                        {
                            byte[] inline = Slice(parsed.WithoutEcho, inlineOffset, parsed.WithoutEcho.Length - inlineOffset);
                            if (inline.Length > 0)
                                LogSafe("PG HELLO EXTRA", ToHex(inline) + "  soma=0x" + Sum8(inline).ToString("X2", CultureInfo.InvariantCulture));
                        }

                        Thread.Sleep(140);
                        LogSafe("PG STAGE2 TX", ToHex(Pc12Stage2) + "  soma=0x" + Sum8(Pc12Stage2).ToString("X2", CultureInfo.InvariantCulture));
                        port.Write(Pc12Stage2, 0, Pc12Stage2.Length);
                        stage2Sent = true;
                        SetState("●  ETAPA 2 ENVIADA · CAPTURANDO RX...", Success);

                        byte[] stage2Raw = ReadBurst(port, Stage2TimeoutMs, 240);
                        if (stage2Raw.Length == 0)
                        {
                            LogSafe("PG STAGE2 RX", "[]");
                            LogSafe("PG DIAG", "nenhum byte retornou imediatamente apos F0 00 0F.");
                        }
                        else
                        {
                            stage2Received = true;
                            int echoCount;
                            byte[] withoutEcho = RemoveLeadingExactEcho(stage2Raw, Pc12Stage2, out echoCount);
                            LogSafe("PG STAGE2 RX RAW", ToHex(stage2Raw) + "  soma=0x" + Sum8(stage2Raw).ToString("X2", CultureInfo.InvariantCulture));
                            LogSafe("PG STAGE2 ECO", "quantidade=" + echoCount.ToString(CultureInfo.InvariantCulture));
                            LogSafe("PG STAGE2 SEM ECO", ToHex(withoutEcho) + "  soma=0x" + Sum8(withoutEcho).ToString("X2", CultureInfo.InvariantCulture));
                            if (withoutEcho.Length > 0 && Sum8(withoutEcho) == 0xFF)
                                LogSafe("PG STAGE2 FRAME?", "o bloco sem eco fecha soma FF; manter interpretacao em aberto ate comparar com o PC12.");
                            else if (withoutEcho.Length > 0)
                                LogSafe("PG STAGE2 FRAME?", "RX registrado sem assumir enquadramento; pode conter um ou mais quadros/bytes de estado.");
                        }

                        int postBursts = CaptureAfterStage2(port);
                        FinishSafe(handshakeConfirmed, stage2Sent, stage2Received, postBursts, string.Empty);
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
                }
            }

            if (cancelRequested) FinishCancelled();
            else FinishSafe(handshakeConfirmed, stage2Sent, stage2Received, 0, lastError);
        }

'@

$text = $text.Substring(0, $start) + $replacement + $text.Substring($end)
[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
