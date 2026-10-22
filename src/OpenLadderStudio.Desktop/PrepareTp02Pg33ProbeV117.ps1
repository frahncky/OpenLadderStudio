$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-RegexOnce([string]$text, [string]$pattern, [string]$replacement, [string]$label) {
    $matches = [System.Text.RegularExpressions.Regex]::Matches($text, $pattern)
    if ($matches.Count -ne 1) {
        throw "$label esperado exatamente uma vez; encontrado: $($matches.Count)."
    }
    return [System.Text.RegularExpressions.Regex]::Replace($text, $pattern, $replacement, 1)
}

# v1.17: usar na prova PG33 a mesma estrategia de aquisicao PG Recovery V93
# que foi validada fisicamente na v1.08. A diferenca e que, ao encontrar HELLO
# valido, a porta permanece ABERTA e a mesma sessao segue para F0 -> 38 -> 34 -> PG33.

$runPattern = '(?s)        private string RunSingleSessionProbe\(string portName\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort)'
$runReplacement = @'
        private string RunSingleSessionProbe(string portName)
        {
            Exception last = null;

            // Cada rodada faz aquisicao V93 completa em DTR=off/RTS=off.
            // Se HELLO/F0/38/34 falhar, a porta e fechada e uma nova rodada
            // comeca do zero. Nenhum 0x33 foi enviado nesse caso.
            for (int round = 1; round <= 3; round++)
            {
                SerialPort port = null;
                bool pg33Attempted = false;
                ProgramSnapshot before = null;
                try
                {
                    if (round > 1)
                    {
                        AppendLogSafe("V93 ROUND: iniciando rodada "
                            + round.ToString(CultureInfo.InvariantCulture) + " de 3 apos falha pre-PG33.");
                        Thread.Sleep(round == 2 ? 2500 : 3500);
                    }

                    string helloState;
                    string acquisitionLabel;
                    port = AcquireStablePgPortV93(portName, round, out helloState, out acquisitionLabel);
                    AppendLogSafe("V93 LINK ADQUIRIDO: " + acquisitionLabel + " | estado=" + helloState + ".");

                    if (!string.Equals(helloState, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN. O PG33 foi BLOQUEADO; coloque o TP02 em STOP.");

                    // Depois do HELLO valido NAO mandamos outro HELLO. Segue o
                    // mesmo encadeamento que funcionou na leitura fisica v1.10.
                    Thread.Sleep(450);
                    PerformF0(port);
                    Thread.Sleep(420);

                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "v93-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(450);

                    before = ReadCanonicalSnapshotOnOpenPort(port,
                        "before-v93-r" + round.ToString(CultureInfo.InvariantCulture));
                    if (before == null || before.Count < 1 || before.EndStep < 0)
                        throw new InvalidDataException("Backup invalido: F-00 END nao foi encontrado.");
                    if (before.Count > 80)
                        throw new InvalidOperationException("Esta prova fisica aceita somente programas de ate 80 passos.");

                    ValidateKnownProbeProgram(before);
                    SaveSnapshot("backup-before", before);
                    AppendLogSafe("BACKUP OK NA MESMA SESSAO V93: "
                        + before.Count.ToString(CultureInfo.InvariantCulture)
                        + " passos; END=" + before.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");

                    byte[] pg33 = BuildPg33SameProgram(before);
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-tx.hex"),
                        ToHex(pg33) + Environment.NewLine, Encoding.ASCII);
                    AppendLogSafe("PG33 preparado: " + ToHex(pg33));

                    // Ponto sem retorno automatico: a partir daqui 0x33 ocorre
                    // no maximo uma vez por execucao, sem retry de rodada.
                    Thread.Sleep(700);
                    port.DiscardInBuffer();
                    AppendLogSafe("PG33 TX UNICA: " + ToHex(pg33));
                    pg33Attempted = true;
                    port.Write(pg33, 0, pg33.Length);

                    byte[] ackRaw = ReadBurst(port, 7000, 380);
                    AppendLogSafe("PG33 RX UNICA: " + (ackRaw.Length == 0 ? "[]" : ToHex(ackRaw)));
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-ack-raw.hex"),
                        (ackRaw.Length == 0 ? "(sem bytes)" : ToHex(ackRaw)) + Environment.NewLine, Encoding.ASCII);

                    byte[] ackFrame = FindFirstValidResponseFrame(ackRaw);
                    if (ackFrame == null)
                        throw new InvalidDataException(
                            "O 0x33 foi transmitido UMA vez, mas nao houve ACK fisico valido. NAO repita o teste; envie o log/foto para analise.");
                    if ((ackFrame[0] & 0x80) != 0)
                        throw new InvalidDataException("O TP02 respondeu ao 0x33 com status de erro: " + ToHex(ackFrame));

                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-ack-frame.hex"),
                        ToHex(ackFrame) + Environment.NewLine, Encoding.ASCII);
                    AppendLogSafe("ACK PG33 FISICO VALIDO: " + ToHex(ackFrame));

                    ClosePort(port);
                    port = null;

                    Thread.Sleep(1800);
                    ProgramSnapshot after = ReadSnapshotRobust(portName, "after");
                    SaveSnapshot("readback-after", after);
                    CompareSnapshots(before, after);

                    string report = "PASS PG33 NO-OP v1.17\r\n"
                        + "Aquisicao: " + acquisitionLabel + "\r\n"
                        + "ACK fisico: " + ToHex(ackFrame) + "\r\n"
                        + "Passos: " + before.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                        + "END: " + before.EndStep.ToString("0000", CultureInfo.InvariantCulture) + "\r\n"
                        + "Readback: identico ao backup\r\n"
                        + "Backup e PG33 ocorreram na mesma sessao serial V93.\r\n"
                        + "Nenhum Clear All/RUN/STOP remoto/0x09/WBP foi enviado.\r\n";
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-probe-report.txt"), report, Encoding.UTF8);

                    return "PG33 fisico aprovado.\r\n\r\nACK: " + ToHex(ackFrame)
                        + "\r\nPassos verificados: " + before.Count.ToString(CultureInfo.InvariantCulture)
                        + "\r\nReadback identico ao backup.\r\n\r\n"
                        + "O programa logico permaneceu inalterado.";
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("V93 rodada " + round.ToString(CultureInfo.InvariantCulture)
                        + " falhou: " + ex.Message);

                    if (pg33Attempted)
                    {
                        throw new IOException(
                            "PG33 TX UNICA ja ocorreu. NAO execute novamente automaticamente. Resultado: "
                            + ex.Message, ex);
                    }

                    if (round < 3)
                        AppendLogSafe("Nenhum PG33 foi enviado; a proxima rodada repetira a aquisicao V93 completa.");
                }
                finally
                {
                    ClosePort(port);
                }
            }

            throw new IOException(
                "As 3 rodadas V93 falharam antes de qualquer PG33. O PLC nao foi escrito. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;

            AppendLogSafe("V93 STARTUP: rodada " + round.ToString(CultureInfo.InvariantCulture)
                + " | perfil fisico confirmado = 19200 8O1 DTR=off RTS=off.");
            Thread.Sleep(850);

            for (int sweep = 1; sweep <= 2; sweep++)
            {
                if (sweep == 2)
                {
                    AppendLogSafe("V93 AUTO-RETRY: primeiro ciclo sem HELLO; recuperando COM sem transmitir bytes.");
                    RecoverPgSerialV93(portName);
                    Thread.Sleep(1000);
                }

                SerialPort serial = null;
                bool keepOpen = false;
                try
                {
                    serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                    serial.Handshake = Handshake.None;
                    serial.DtrEnable = false;
                    serial.RtsEnable = false;
                    serial.ReadTimeout = 80;
                    serial.WriteTimeout = 1000;
                    serial.Open();
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();

                    int settleMs = sweep == 1 ? 500 : 900;
                    Thread.Sleep(settleMs);
                    AppendLogSafe("V93 PERFIL [ciclo " + sweep.ToString(CultureInfo.InvariantCulture)
                        + "/2]: 19200 8O1 DTR=off RTS=off | settle="
                        + settleMs.ToString(CultureInfo.InvariantCulture) + " ms.");

                    int receiveMs = sweep == 1 ? 1900 : 2400;
                    for (int attempt = 1; attempt <= 5; attempt++)
                    {
                        serial.DiscardInBuffer();
                        serial.Write(HelloRequest, 0, HelloRequest.Length);
                        byte[] raw = ReadBurst(serial, receiveMs, 180);
                        AppendLogSafe("V93 HELLO " + attempt.ToString(CultureInfo.InvariantCulture)
                            + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));

                        if (Contains(raw, HelloStop))
                        {
                            state = "STOP";
                            acquisitionLabel = "DTR=off RTS=off / ciclo "
                                + sweep.ToString(CultureInfo.InvariantCulture)
                                + " / tentativa " + attempt.ToString(CultureInfo.InvariantCulture);
                            keepOpen = true;
                            AppendLogSafe("V93 ESTAVEL: HELLO STOP confirmado; mantendo esta mesma COM aberta.");
                            return serial;
                        }
                        if (Contains(raw, HelloRun))
                        {
                            state = "RUN";
                            acquisitionLabel = "DTR=off RTS=off / ciclo "
                                + sweep.ToString(CultureInfo.InvariantCulture)
                                + " / tentativa " + attempt.ToString(CultureInfo.InvariantCulture);
                            keepOpen = true;
                            AppendLogSafe("V93 ESTAVEL: HELLO RUN confirmado; mantendo esta mesma COM aberta.");
                            return serial;
                        }

                        Thread.Sleep(260);
                    }
                }
                finally
                {
                    if (!keepOpen && serial != null)
                    {
                        ClosePort(serial);
                        Thread.Sleep(350);
                    }
                }
            }

            throw new TimeoutException("V93: HELLO PG nao confirmado nos dois ciclos off/off.");
        }

        private void RecoverPgSerialV93(string portName)
        {
            SerialPort recovery = null;
            try
            {
                recovery = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                recovery.Handshake = Handshake.None;
                recovery.ReadTimeout = 80;
                recovery.WriteTimeout = 1000;
                recovery.DtrEnable = false;
                recovery.RtsEnable = false;
                recovery.Open();
                recovery.DiscardInBuffer();
                recovery.DiscardOutBuffer();
                Thread.Sleep(700);
                AppendLogSafe("V93 RECOVERY: COM estabilizada off/off por 700 ms, sem envio de bytes.");
            }
            catch (Exception ex)
            {
                AppendLogSafe("V93 RECOVERY parcial: " + ex.Message);
            }
            finally
            {
                ClosePort(recovery);
            }
        }

'@

$shell = Replace-RegexOnce $shell $runPattern $runReplacement 'RunSingleSessionProbe V117'

# Atualiza rotulos/logs do StartProbe sem depender da logica interna do template.
$shell = $shell.Replace('PG33 NO-OP v1.16 iniciado em ', 'PG33 NO-OP v1.17 iniciado em ')
$shell = $shell.Replace('REGRA: backup e PG33 permanecem na mesma abertura da COM.',
    'REGRA: aquisicao usa PG Recovery V93; backup e PG33 permanecem na mesma abertura da COM.')
$shell = $shell.Replace('PROCURANDO SESSAO ESTAVEL...', 'ADQUIRINDO LINK PG V93...')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Probe V117 aplicado: aquisicao V93 off/off + porta mantida aberta ate PG33.'
