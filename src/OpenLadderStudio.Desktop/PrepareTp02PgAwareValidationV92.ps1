$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($shellPath)

function Replace-Required([string]$source, [string]$old, [string]$new, [string]$label) {
    if (-not $source.Contains($old)) { throw "Ancora nao encontrada ($label)." }
    return $source.Replace($old, $new)
}

# A tela final agora representa os dois transportes reais do TP02.
$text = Replace-Required $text `
    'Porta MMI | Computer Link | 19200 7N1 | fase 1 somente leitura; fase 2 exige STOP e confirmacao explicita' `
    'AUTO: PG/PC12 19200 8O1 (TP-232PG) ou Computer Link 19200 7N1 | nenhum comando destrutivo na fase 1' `
    'subtitulo da validacao'
$text = Replace-Required $text `
    'readValidationButton = NewButton("1. VALIDAR LEITURA", 20, 18, 210, true);' `
    'readValidationButton = NewButton("1. DETECTAR / VALIDAR LINK", 20, 18, 210, true);' `
    'botao fase 1'
$text = Replace-Required $text `
    'readLabel = NewLabel("PENDENTE - PSR + RBP duas vezes + comparacao integral", 8.8f, FontStyle.Bold, Warning);' `
    'readLabel = NewLabel("PENDENTE - tenta PG/PC12 seguro primeiro; depois Computer Link", 8.8f, FontStyle.Bold, Warning);' `
    'rotulo fase 1'
$text = Replace-Required $text `
    'AppendLog("FASE 1 nao grava nada no PLC.");' `
    'AppendLog("FASE 1 nao grava nada: primeiro testa PG/PC12 somente com CON-ICB; se nao responder, tenta Computer Link.");' `
    'log inicial fase 1'
$text = Replace-Required $text `
    'AppendLog("FASE 2 somente e habilitada depois da leitura e requer STOP + confirmacao explicita.");' `
    'AppendLog("FASE 2 so e habilitada quando Computer Link confirmar leitura completa + STOP. Em PG/PC12 a gravacao permanece bloqueada.");' `
    'log inicial fase 2'

$text = Replace-Required $text `
    '        private bool writePassed;' `
    "        private bool writePassed;`r`n        private bool pgLinkDetected;`r`n        private string pgHelloVariant = string.Empty;" `
    'estado PG'

$readStart = $text.IndexOf('        private void RunReadValidation()', [System.StringComparison]::Ordinal)
$writeStart = $text.IndexOf('        private void RunWriteValidation()', $readStart, [System.StringComparison]::Ordinal)
if ($readStart -lt 0 -or $writeStart -lt 0) { throw 'Metodos RunReadValidation/RunWriteValidation nao encontrados.' }

$newRead = @'
        private void RunReadValidation()
        {
            if (busy) return;
            string port;
            int station;
            try
            {
                port = SelectedPort();
                station = (int)stationBox.Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            readPassed = false;
            writePassed = false;
            pgLinkDetected = false;
            pgHelloVariant = string.Empty;
            lastState = Tp02ComputerLinkState.Unknown;
            UpdateStageLabels();
            AppendLog(new string('-', 78));
            AppendLog("FASE 1 iniciada: autodeteccao segura em " + port + ".");
            AppendLog("Prioridade: PG/PC12 19200 8O1 (TP-232PG). Somente CON-ICB<CR> pode ser transmitido nesse teste.");

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                try
                {
                    string hello;
                    if (TryProbePgLink(port, out hello))
                    {
                        pgLinkDetected = true;
                        pgHelloVariant = hello;
                        lastState = Tp02ComputerLinkState.Unknown;
                        SetPgStateSafe(hello);
                        AppendLogSafe("PASS LINK PG/PC12: HELLO " + hello + " confirmado com checksum FF.");
                        AppendLogSafe("SEGURANCA: nenhum comando PG posterior ao CON-ICB foi transmitido.");
                        AppendLogSafe("GRAVACAO: permanece bloqueada neste transporte; WBP pertence ao Computer Link e a escrita PG proprietaria ainda nao esta habilitada.");
                    }
                    else
                    {
                        AppendLogSafe("PG/PC12 nao confirmou HELLO. Tentando Computer Link 19200 7N1...");
                        using (TP02ComputerLinkClient client = new TP02ComputerLinkClient(port, station, AppendLogSafe))
                        {
                            lastState = client.ReadState();
                            SetStateSafe(lastState);

                            AppendLogSafe("RBP #1: leitura completa ate F-00 END...");
                            List<Tp02MachineWord> first = client.ReadProgram();
                            TP02ComputerLinkFiles.SaveHex(Path.Combine(sessionDirectory, "read-1.tp02.hex"), first);
                            TP02ComputerLinkFiles.SaveDump(Path.Combine(sessionDirectory, "read-1.rbpdump"), first);

                            AppendLogSafe("RBP #2: repetindo a leitura para verificar estabilidade...");
                            List<Tp02MachineWord> second = client.ReadProgram();
                            TP02ComputerLinkFiles.SaveHex(Path.Combine(sessionDirectory, "read-2.tp02.hex"), second);
                            TP02ComputerLinkFiles.SaveDump(Path.Combine(sessionDirectory, "read-2.rbpdump"), second);

                            EnsureEqual(first, second, "RBP #1 x RBP #2");
                            AppendLogSafe("PASS COMPUTER LINK: duas leituras RBP identicas, " + first.Count.ToString(CultureInfo.InvariantCulture) + " passo(s).");
                            readPassed = true;
                        }
                    }
                }
                catch (Exception ex) { failure = ex; }

                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FAIL FASE 1: " + failure.Message);
                        readPassed = false;
                        pgLinkDetected = false;
                    }
                    else if (pgLinkDetected)
                    {
                        AppendLog("FASE 1 APROVADA: link PG/PC12 confirmado de forma nao destrutiva.");
                    }
                    else
                    {
                        AppendLog("FASE 1 APROVADA: Computer Link + leitura RBP confirmados.");
                    }

                    SetBusy(false);
                    UpdateStageLabels();
                    WriteReport();

                    if (failure != null)
                    {
                        MessageBox.Show(this, failure.Message, "TP02 - Fase 1 falhou", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (pgLinkDetected)
                    {
                        MessageBox.Show(this,
                            "Link PG/PC12 confirmado pelo TP-232PG (HELLO " + pgHelloVariant + ").\r\n\r\n"
                            + "O cabo, a COM e o TP02 foram reconhecidos pelo OpenLadderStudio. Nenhum comando destrutivo foi enviado.\r\n\r\n"
                            + "A gravacao continua bloqueada neste modo porque a escrita proprietaria PG ainda nao esta habilitada. Nao e necessario alterar seu cabo para considerar o link PG aprovado.",
                            "TP02 - LINK PG APROVADO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(this,
                            lastState == Tp02ComputerLinkState.Stop
                                ? "Computer Link e leitura fisica aprovados. O PLC esta em STOP; a validacao de gravacao foi liberada."
                                : "Computer Link e leitura fisica aprovados. Coloque o PLC em STOP e execute novamente a fase 1 para liberar a gravacao.",
                            "TP02 - Fase 1", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }));
            });
        }

        private bool TryProbePgLink(string portName, out string helloVariant)
        {
            helloVariant = string.Empty;
            byte[] hello = new byte[] { 0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D };
            byte[] helloRun = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
            byte[] helloAlt = new byte[] { 0x80, 0x01, 0x09, 0x75 };

            string[] profileNames = new string[]
            {
                "19200 8O1 DTR=on RTS=off",
                "19200 8O1 DTR=on RTS=on",
                "19200 8O1 DTR=off RTS=off"
            };
            bool[] dtr = new bool[] { true, true, false };
            bool[] rts = new bool[] { false, true, false };

            for (int profile = 0; profile < profileNames.Length; profile++)
            {
                SerialPort serial = null;
                try
                {
                    serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                    serial.Handshake = Handshake.None;
                    serial.DtrEnable = dtr[profile];
                    serial.RtsEnable = rts[profile];
                    serial.ReadTimeout = 80;
                    serial.WriteTimeout = 1000;
                    serial.Open();
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    Thread.Sleep(140);
                    AppendLogSafe("PG PERFIL: " + profileNames[profile]);

                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        serial.DiscardInBuffer();
                        AppendLogSafe("PG HELLO TX " + attempt.ToString(CultureInfo.InvariantCulture) + ": 43 4F 4E 2D 49 43 42 0D");
                        serial.Write(hello, 0, hello.Length);
                        byte[] raw = ReadPgBurst(serial, 1700);
                        AppendLogSafe("PG HELLO RX: " + (raw.Length == 0 ? "[]" : PgHex(raw)));

                        if (PgContains(raw, helloRun) && PgSum8(helloRun) == 0xFF)
                        {
                            helloVariant = "C0 01 09 35";
                            AppendLogSafe("PG LINK ESTABLISHED: " + helloVariant + " | " + profileNames[profile]);
                            return true;
                        }
                        if (PgContains(raw, helloAlt) && PgSum8(helloAlt) == 0xFF)
                        {
                            helloVariant = "80 01 09 75";
                            AppendLogSafe("PG LINK ESTABLISHED: " + helloVariant + " | " + profileNames[profile]);
                            return true;
                        }
                        Thread.Sleep(130);
                    }
                }
                catch (Exception ex)
                {
                    AppendLogSafe("PG PERFIL sem confirmacao: " + profileNames[profile] + " - " + ex.Message);
                }
                finally
                {
                    if (serial != null)
                    {
                        try { if (serial.IsOpen) serial.Close(); } catch { }
                        serial.Dispose();
                    }
                    Thread.Sleep(180);
                }
            }
            return false;
        }

        private static byte[] ReadPgBurst(SerialPort port, int timeoutMs)
        {
            List<byte> bytes = new List<byte>();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            DateTime lastData = DateTime.MinValue;
            while (DateTime.UtcNow < deadline)
            {
                int available = port.BytesToRead;
                if (available > 0)
                {
                    byte[] buffer = new byte[available];
                    int got = port.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < got; i++) bytes.Add(buffer[i]);
                    lastData = DateTime.UtcNow;
                }
                else if (bytes.Count > 0 && lastData != DateTime.MinValue && (DateTime.UtcNow - lastData).TotalMilliseconds >= 180)
                {
                    break;
                }
                Thread.Sleep(15);
            }
            return bytes.ToArray();
        }

        private static bool PgContains(byte[] value, byte[] sequence)
        {
            if (value == null || sequence == null || sequence.Length == 0 || value.Length < sequence.Length) return false;
            for (int i = 0; i <= value.Length - sequence.Length; i++)
            {
                bool same = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (value[i + j] != sequence[j]) { same = false; break; }
                }
                if (same) return true;
            }
            return false;
        }

        private static byte PgSum8(byte[] value)
        {
            int sum = 0;
            if (value != null)
                for (int i = 0; i < value.Length; i++) sum = (sum + value[i]) & 0xFF;
            return (byte)sum;
        }

        private static string PgHex(byte[] value)
        {
            if (value == null || value.Length == 0) return string.Empty;
            StringBuilder text = new StringBuilder(value.Length * 3);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(value[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        private void SetPgStateSafe(string helloVariant)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { SetPgStateSafe(helloVariant); }));
                return;
            }
            stateLabel.Text = "PLC: LINK PG/PC12";
            stateLabel.ForeColor = Success;
        }

'@
$text = $text.Substring(0, $readStart) + $newRead + $text.Substring($writeStart)

# Evita qualquer liberacao de WBP quando o transporte detectado for PG.
$text = Replace-Required $text `
    '            writeValidationButton.Enabled = !value && readPassed && lastState == Tp02ComputerLinkState.Stop;' `
    '            writeValidationButton.Enabled = !value && !pgLinkDetected && readPassed && lastState == Tp02ComputerLinkState.Stop;' `
    'SetBusy write guard'

$labelStart = $text.IndexOf('        private void UpdateStageLabels()', [System.StringComparison]::Ordinal)
$stateStart = $text.IndexOf('        private void SetStateSafe(Tp02ComputerLinkState state)', $labelStart, [System.StringComparison]::Ordinal)
if ($labelStart -lt 0 -or $stateStart -lt 0) { throw 'UpdateStageLabels/SetStateSafe nao encontrados.' }
$newLabels = @'
        private void UpdateStageLabels()
        {
            if (pgLinkDetected)
            {
                readLabel.Text = "PASS - link PG/PC12 confirmado por HELLO seguro (TP-232PG)";
                readLabel.ForeColor = Success;
                writeLabel.Text = "BLOQUEADA EM PG - escrita proprietaria PG ainda nao habilitada";
                writeLabel.ForeColor = Warning;
                writeValidationButton.Enabled = false;
                return;
            }

            if (readPassed)
            {
                readLabel.Text = "PASS - Computer Link: PSR + duas leituras RBP completas e identicas";
                readLabel.ForeColor = Success;
            }
            else
            {
                readLabel.Text = "PENDENTE - tenta PG/PC12 seguro primeiro; depois Computer Link";
                readLabel.ForeColor = Warning;
            }

            if (writePassed)
            {
                writeLabel.Text = "PASS - backup + WBP + verify por bloco + releitura final";
                writeLabel.ForeColor = Success;
            }
            else if (readPassed && lastState == Tp02ComputerLinkState.Stop)
            {
                writeLabel.Text = "LIBERADA - Computer Link + PLC em STOP; exige confirmacao";
                writeLabel.ForeColor = Warning;
            }
            else if (readPassed)
            {
                writeLabel.Text = "AGUARDANDO STOP - nenhuma escrita sera enviada em RUN";
                writeLabel.ForeColor = Warning;
            }
            else
            {
                writeLabel.Text = "BLOQUEADA - primeiro o transporte precisa ser validado";
                writeLabel.ForeColor = Muted;
            }
            writeValidationButton.Enabled = !busy && !pgLinkDetected && readPassed && lastState == Tp02ComputerLinkState.Stop;
        }

'@
$text = $text.Substring(0, $labelStart) + $newLabels + $text.Substring($stateStart)

$reportStart = $text.IndexOf('        private void WriteReport()', [System.StringComparison]::Ordinal)
$folderStart = $text.IndexOf('        private void OpenSessionFolder()', $reportStart, [System.StringComparison]::Ordinal)
if ($reportStart -lt 0 -or $folderStart -lt 0) { throw 'WriteReport/OpenSessionFolder nao encontrados.' }
$newReport = @'
        private void WriteReport()
        {
            try
            {
                StringBuilder report = new StringBuilder();
                string mode = pgLinkDetected ? "PG/PC12" : (readPassed ? "COMPUTER LINK" : "NAO CONFIRMADO");
                report.AppendLine("OpenLadder Studio - Validacao fisica WEG TP02");
                report.AppendLine("Data: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                report.AppendLine("Sessao: " + sessionDirectory);
                report.AppendLine("Porta: " + (portCombo != null && portCombo.SelectedItem != null ? portCombo.SelectedItem.ToString() : "-"));
                report.AppendLine("Estacao Computer Link: " + (stationBox == null ? "-" : ((int)stationBox.Value).ToString("00", CultureInfo.InvariantCulture)));
                report.AppendLine("Transporte detectado: " + mode);
                if (pgLinkDetected) report.AppendLine("HELLO PG: " + pgHelloVariant);
                report.AppendLine("Estado PLC via Computer Link: " + lastState.ToString().ToUpperInvariant());
                report.AppendLine("FASE 1: " + (pgLinkDetected ? "PASS LINK PG/PC12 (HELLO seguro; programa nao lido pelo caminho PG)" : (readPassed ? "PASS COMPUTER LINK + RBP" : "PENDENTE/FAIL")));
                report.AppendLine("FASE 2 - gravacao: " + (writePassed ? "PASS" : (pgLinkDetected ? "BLOQUEADA EM PG" : "PENDENTE/FAIL")));
                report.AppendLine("Resultado: " + (pgLinkDetected ? "LINK PG/PC12 APROVADO; ESCRITA PG PROPRIETARIA AINDA NAO HABILITADA" : (readPassed && writePassed ? "RBP/WBP APROVADO EM HARDWARE REAL" : "AINDA NAO FECHADO")));
                File.WriteAllText(Path.Combine(sessionDirectory, "validation-report.txt"), report.ToString(), Encoding.UTF8);
            }
            catch { }
        }

'@
$text = $text.Substring(0, $reportStart) + $newReport + $text.Substring($folderStart)

# Remove a rotulagem enganosa do antigo leitor ASCII RBP como se fosse leitor PG.
$text = $text.Replace('"Leitor PG experimental..."', '"Leitor RBP/Host experimental..."')
$text = $text.Replace('"Transferir programa TP02..."', '"Transferir programa TP02 (Computer Link)..."')

[System.IO.File]::WriteAllText($shellPath, $text, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG-aware Physical Validation V92 aplicada: PG/PC12 seguro + fallback Computer Link.'
