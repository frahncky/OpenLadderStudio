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

# v1.16: o teste PG33 passa a manter BACKUP e PG33 na MESMA sessao serial.
# Isto remove a reabertura da COM entre a leitura bem-sucedida e o primeiro 0x33,
# exatamente o ponto que permaneceu intermitente nas v1.11..v1.15.

# 1) Substitui o fluxo da janela por uma prova de sessao unica.
$startPattern = '(?s)        private void StartProbe\(\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private void WarmUpLink)'
$startReplacement = @'
        private void StartProbe()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 PG33", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "Este e um teste REAL de escrita PG no TP02.\r\n\r\n"
                + "O Studio ira manter a MESMA sessao serial para:\r\n"
                + "1. confirmar STOP;\r\n"
                + "2. ler e salvar o programa atual;\r\n"
                + "3. validar a assinatura conhecida de 23 passos;\r\n"
                + "4. regravar exatamente o mesmo programa via 0x33;\r\n"
                + "5. capturar o ACK;\r\n"
                + "6. reler e comparar o programa.\r\n\r\n"
                + "Antes do primeiro 0x33 podem ocorrer retries de sessao.\r\n"
                + "Depois de PG33 TX UNICA nao existe retransmissao automatica.\r\n\r\n"
                + "Faca o teste somente em bancada, com a maquina/processo em condicao segura.\r\n\r\n"
                + "Deseja continuar?",
                "Confirmar prova fisica PG33",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG33 NO-OP v1.16 iniciado em " + portName + ".");
            AppendLog("Perfil: 19200 8O1 DTR=off RTS=off.");
            AppendLog("REGRA: backup e PG33 permanecem na mesma abertura da COM.");
            AppendLog("REGRA: PG33 TX UNICA pode ocorrer no maximo uma vez por execucao.");
            SetBusy(true);
            SetStatus("PROCURANDO SESSAO ESTAVEL...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string successText = string.Empty;
                try
                {
                    successText = RunSingleSessionProbe(portName);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg33-probe-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FALHA: " + failure.Message);
                        SetStatus("FALHA / VER LOG", Danger);
                        MessageBox.Show(this, failure.Message,
                            "TP02 - PG33 nao aprovado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        AppendLog("PASS: ACK fisico e readback confirmados.");
                        SetStatus("PG33 FISICO APROVADO", Success);
                        MessageBox.Show(this, successText,
                            "TP02 - PG33 APROVADO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SetBusy(false);
                }));
            });
        }

        private string RunSingleSessionProbe(string portName)
        {
            Exception last = null;

            // Mesmo warm-up que ja foi validado na leitura v1.10.
            WarmUpLink(portName, "single-session");

            for (int session = 1; session <= 5; session++)
            {
                SerialPort port = null;
                bool pg33Attempted = false;
                byte[] ackFrame = null;
                ProgramSnapshot before = null;
                try
                {
                    if (session > 1)
                    {
                        AppendLogSafe("SINGLE PG AUTO-RETRY: preparando sessao "
                            + session.ToString(CultureInfo.InvariantCulture) + " de 5.");
                        Thread.Sleep(session == 2 ? 1500 : 2000);
                    }

                    port = OpenPort(portName);
                    int settle = session == 1 ? 1600 : (session == 2 ? 1900 : 2300);
                    Thread.Sleep(settle);
                    AppendLogSafe("SINGLE COM aberta | sessao "
                        + session.ToString(CultureInfo.InvariantCulture)
                        + " | estabilizacao " + settle.ToString(CultureInfo.InvariantCulture) + " ms.");

                    string state = PerformHello(port);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN. O PG33 foi BLOQUEADO; coloque o TP02 em STOP.");
                    Thread.Sleep(450);

                    PerformF0(port);
                    Thread.Sleep(420);

                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "single-38-s" + session.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(450);

                    // BACKUP na MESMA sessao que, se validada, seguira para PG33.
                    before = ReadCanonicalSnapshotOnOpenPort(port, "before-single-s"
                        + session.ToString(CultureInfo.InvariantCulture));
                    if (before == null || before.Count < 1 || before.EndStep < 0)
                        throw new InvalidDataException("Backup invalido: F-00 END nao foi encontrado.");
                    if (before.Count > 80)
                        throw new InvalidOperationException("Esta prova fisica aceita somente programas de ate 80 passos.");

                    ValidateKnownProbeProgram(before);
                    SaveSnapshot("backup-before", before);
                    AppendLogSafe("BACKUP OK NA MESMA SESSAO: "
                        + before.Count.ToString(CultureInfo.InvariantCulture)
                        + " passos; END=" + before.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");

                    byte[] pg33 = BuildPg33SameProgram(before);
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-tx.hex"),
                        ToHex(pg33) + Environment.NewLine, Encoding.ASCII);
                    AppendLogSafe("PG33 preparado: " + ToHex(pg33));

                    // Nenhuma reabertura, nenhum novo HELLO/F0/38 daqui ate o 0x33.
                    Thread.Sleep(700);
                    port.DiscardInBuffer();
                    AppendLogSafe("PG33 TX UNICA: " + ToHex(pg33));
                    pg33Attempted = true;
                    port.Write(pg33, 0, pg33.Length);

                    byte[] ackRaw = ReadBurst(port, 7000, 380);
                    AppendLogSafe("PG33 RX UNICA: " + (ackRaw.Length == 0 ? "[]" : ToHex(ackRaw)));
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-ack-raw.hex"),
                        (ackRaw.Length == 0 ? "(sem bytes)" : ToHex(ackRaw)) + Environment.NewLine, Encoding.ASCII);

                    ackFrame = FindFirstValidResponseFrame(ackRaw);
                    if (ackFrame == null)
                        throw new InvalidDataException(
                            "O 0x33 foi transmitido UMA vez, mas nao houve ACK fisico valido. NAO repita o teste; envie o log/foto para analise.");
                    if ((ackFrame[0] & 0x80) != 0)
                        throw new InvalidDataException("O TP02 respondeu ao 0x33 com status de erro: " + ToHex(ackFrame));

                    File.WriteAllText(Path.Combine(sessionDirectory, "pg33-ack-frame.hex"),
                        ToHex(ackFrame) + Environment.NewLine, Encoding.ASCII);
                    AppendLogSafe("ACK PG33 FISICO VALIDO: " + ToHex(ackFrame));

                    // A escrita ja ocorreu. Fechamos a sessao somente agora.
                    ClosePort(port);
                    port = null;

                    Thread.Sleep(1600);
                    ProgramSnapshot after = ReadSnapshotRobust(portName, "after");
                    SaveSnapshot("readback-after", after);
                    CompareSnapshots(before, after);

                    string report = "PASS PG33 NO-OP v1.16\r\n"
                        + "ACK fisico: " + ToHex(ackFrame) + "\r\n"
                        + "Passos: " + before.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                        + "END: " + before.EndStep.ToString("0000", CultureInfo.InvariantCulture) + "\r\n"
                        + "Readback: identico ao backup\r\n"
                        + "Backup e PG33 foram feitos na mesma sessao serial.\r\n"
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
                    AppendLogSafe("SINGLE sessao " + session.ToString(CultureInfo.InvariantCulture)
                        + " falhou: " + ex.Message);

                    if (pg33Attempted)
                    {
                        throw new IOException(
                            "PG33 TX UNICA ja ocorreu. NAO execute novamente automaticamente. "
                            + "Resultado: " + ex.Message, ex);
                    }

                    if (session < 5)
                        AppendLogSafe("Nenhum PG33 foi enviado; abrindo nova sessao limpa.");
                }
                finally
                {
                    ClosePort(port);
                }
            }

            throw new IOException(
                "As 5 sessoes falharam antes de qualquer PG33. O PLC nao foi escrito. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)
        {
            byte[] frame34 = SendAndReadFrame(port, Build34Request(0), PagePayloadLength,
                4, 5600, tag + "-34-0000");
            File.WriteAllText(Path.Combine(sessionDirectory, tag + "-page-0000.hex"),
                ToHex(frame34) + Environment.NewLine, Encoding.ASCII);

            ProgramSnapshot snapshot = new ProgramSnapshot();
            snapshot.PlcState = "STOP";
            for (int i = 0; i < StepsPerPage; i++)
            {
                byte high = frame34[2 + (2 * i)];
                byte low = frame34[2 + (2 * i) + 1];
                byte braw = frame34[2 + PageABLength + i];
                snapshot.High.Add(high);
                snapshot.Low.Add(low);
                snapshot.External.Add(braw);
                if (high == 0x00 && low == 0x70)
                {
                    snapshot.EndStep = i;
                    return snapshot;
                }
            }

            throw new InvalidDataException(
                "F-00 END nao encontrado na primeira pagina; PG33 bloqueado antes da escrita.");
        }

'@
$shell = Replace-RegexOnce $shell $startPattern $startReplacement 'StartProbe + single-session'

# 2) Mantem HELLO exatamente no estilo do leitor v1.10 validado fisicamente.
$helloPattern = '(?s)        private string PerformHello\(SerialPort port\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private void PerformF0)'
$helloReplacement = @'
        private string PerformHello(SerialPort port)
        {
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(HelloRequest, 0, HelloRequest.Length);
                byte[] raw = ReadBurst(port, attempt == 1 ? 2600 : 3000, 240);
                AppendLogSafe("HELLO tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));
                if (Contains(raw, HelloStop)) return "STOP";
                if (Contains(raw, HelloRun)) return "RUN";
                Thread.Sleep(350);
            }
            throw new TimeoutException("HELLO PG nao confirmado.");
        }

'@
$shell = Replace-RegexOnce $shell $helloPattern $helloReplacement 'PerformHello'

# 3) F0 exatamente no estilo do leitor v1.10: sem HELLO entre tentativas.
$f0Pattern = '(?s)        private void PerformF0\(SerialPort port\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private byte\[\] SendAndReadFrame)'
$f0Replacement = @'
        private void PerformF0(SerialPort port)
        {
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(F0Request, 0, F0Request.Length);
                byte[] raw = ReadBurst(port, 3600, 250);
                AppendLogSafe("F0 tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));
                if (Contains(raw, F0Response)) return;
                Thread.Sleep(350);
            }
            throw new InvalidDataException("F0 nao retornou 00 02 10 22 CB.");
        }

'@
$shell = Replace-RegexOnce $shell $f0Pattern $f0Replacement 'PerformF0'

# 4) Readback posterior ainda pode usar retries robustos; ampliar 3 -> 5 quando aplicavel.
$snapshotPattern = '(?s)(        private ProgramSnapshot ReadSnapshotRobust\(string portName, string tag\).*?for \(int session = 1; session <= )3(; session\+\+\))'
$snapshotMatch = [System.Text.RegularExpressions.Regex]::Matches($shell, $snapshotPattern)
if ($snapshotMatch.Count -eq 1) {
    $shell = [System.Text.RegularExpressions.Regex]::Replace($shell, $snapshotPattern, '${1}5${2}', 1)
}
elseif ($snapshotMatch.Count -ne 0) {
    throw "ReadSnapshotRobust loop ambiguo; encontrado: $($snapshotMatch.Count)."
}

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Probe V116 aplicado: backup e PG33 na mesma sessao serial; retries somente antes do primeiro 0x33.'
