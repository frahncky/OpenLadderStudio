$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Block([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# v1.27: caminho rapido primeiro, com fallback integral para a aquisicao
# conservadora V122. Nenhuma regra de seguranca de escrita e removida.
$acqStart = '        private SerialPort AcquireStablePgPortV93'
$acqEnd = '        private void StartChangeRestoreProbe()'
$acqReplacement = @'
        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;

            AppendLogSafe("V127 FAST PG: tentando aquisicao rapida antes do fallback conservador.");
            try
            {
                SerialPort fast = TryAcquireFastV127(portName, round, out state, out acquisitionLabel);
                if (fast != null) return fast;
            }
            catch (Exception ex)
            {
                AppendLogSafe("V127 FAST PG indisponivel: " + ex.Message);
            }

            AppendLogSafe("V127 FALLBACK: retomando qualificacao conservadora HELLO STOP + F0.");
            return AcquireStablePgPortConservativeV127(portName, round, out state, out acquisitionLabel);
        }

        private SerialPort TryAcquireFastV127(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;

            // O perfil ON/ON foi o ultimo perfil fisicamente confirmado no readback
            // v1.24. Os demais continuam sendo tentados sem qualquer escrita.
            bool[] dtr = new bool[] { true, false, true };
            bool[] rts = new bool[] { true, false, false };
            string[] names = new string[]
            {
                "19200 8O1 DTR=on RTS=on",
                "19200 8O1 DTR=off RTS=off",
                "19200 8O1 DTR=on RTS=off"
            };

            for (int profile = 0; profile < names.Length; profile++)
            {
                SerialPort serial = null;
                bool keepOpen = false;
                try
                {
                    serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                    serial.Handshake = Handshake.None;
                    serial.DtrEnable = dtr[profile];
                    serial.RtsEnable = rts[profile];
                    serial.ReadTimeout = 60;
                    serial.WriteTimeout = 1000;
                    serial.Open();
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();

                    // Apenas uma curta janela para o conversor/driver assumir as linhas.
                    Thread.Sleep(profile == 0 ? 120 : 150);
                    AppendLogSafe("V127 FAST perfil: " + names[profile] + ".");

                    for (int helloAttempt = 1; helloAttempt <= 2; helloAttempt++)
                    {
                        serial.DiscardInBuffer();
                        serial.Write(HelloRequest, 0, HelloRequest.Length);
                        byte[] helloRaw = ReadUntilSequenceV127(serial, HelloStop, HelloRun, 900);
                        AppendLogSafe("V127 FAST HELLO " + helloAttempt.ToString(CultureInfo.InvariantCulture)
                            + " RX=" + (helloRaw.Length == 0 ? "[]" : ToHex(helloRaw)));

                        if (Contains(helloRaw, HelloRun))
                        {
                            state = "RUN";
                            acquisitionLabel = "FAST " + names[profile] + " / HELLO "
                                + helloAttempt.ToString(CultureInfo.InvariantCulture);
                            keepOpen = true;
                            return serial;
                        }

                        if (!Contains(helloRaw, HelloStop))
                            continue;

                        // F0 e enviado imediatamente na MESMA porta/perfil. O quadro
                        // PG33 continua bloqueado ate esta qualificacao responder.
                        serial.DiscardInBuffer();
                        serial.Write(F0Request, 0, F0Request.Length);
                        byte[] f0Raw = ReadUntilSequenceV127(serial, F0Response, null, 1200);
                        AppendLogSafe("V127 FAST F0 RX=" + (f0Raw.Length == 0 ? "[]" : ToHex(f0Raw)));
                        if (Contains(f0Raw, F0Response))
                        {
                            state = "STOP";
                            acquisitionLabel = "FAST " + names[profile] + " / rodada "
                                + round.ToString(CultureInfo.InvariantCulture) + " / HELLO "
                                + helloAttempt.ToString(CultureInfo.InvariantCulture);
                            keepOpen = true;
                            AppendLogSafe("V127 FAST QUALIFICADO: HELLO STOP + F0 na mesma COM/perfil.");
                            return serial;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLogSafe("V127 FAST perfil sem qualificacao: " + names[profile] + " - " + ex.Message);
                }
                finally
                {
                    if (!keepOpen && serial != null) ClosePort(serial);
                }
            }
            return null;
        }

        private byte[] ReadUntilSequenceV127(SerialPort port, byte[] primary, byte[] secondary, int timeoutMs)
        {
            List<byte> bytes = new List<byte>();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int available = port.BytesToRead;
                if (available > 0)
                {
                    byte[] buffer = new byte[available];
                    int got = port.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < got; i++) bytes.Add(buffer[i]);
                    byte[] raw = bytes.ToArray();
                    if (Contains(raw, primary) || (secondary != null && Contains(raw, secondary)))
                        return raw;
                }
                Thread.Sleep(4);
            }
            return bytes.ToArray();
        }

        private SerialPort AcquireStablePgPortConservativeV127(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            Thread.Sleep(round == 1 ? 900 : (round == 2 ? 1300 : 1700));

            bool[] dtr = new bool[] { false, true, true };
            bool[] rts = new bool[] { false, false, true };
            string[] names = new string[]
            {
                "19200 8O1 DTR=off RTS=off",
                "19200 8O1 DTR=on RTS=off",
                "19200 8O1 DTR=on RTS=on"
            };

            for (int profile = 0; profile < names.Length; profile++)
            {
                SerialPort qualified = TryAcquireConservativeProfileV127(
                    portName, dtr[profile], rts[profile], names[profile], round,
                    out state, out acquisitionLabel);
                if (qualified != null) return qualified;
                Thread.Sleep(650);
            }

            RecoverPgSerialV127(portName);
            throw new TimeoutException(
                "V127 fallback: nenhum perfil confirmou HELLO STOP e F0 00 02 10 22 CB na mesma sessao.");
        }

        private SerialPort TryAcquireConservativeProfileV127(
            string portName, bool dtr, bool rts, string profileName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            SerialPort serial = null;
            bool keepOpen = false;
            try
            {
                serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                serial.Handshake = Handshake.None;
                serial.DtrEnable = dtr;
                serial.RtsEnable = rts;
                serial.ReadTimeout = 80;
                serial.WriteTimeout = 1000;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();

                Thread.Sleep(profileName.IndexOf("off RTS=off", StringComparison.Ordinal) >= 0 ? 700 : 450);
                AppendLogSafe("V127 FALLBACK perfil: " + profileName + ".");

                for (int helloAttempt = 1; helloAttempt <= 2; helloAttempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] helloRaw = ReadBurst(serial, 1800, 240);
                    if (Contains(helloRaw, HelloRun))
                    {
                        state = "RUN";
                        acquisitionLabel = "FALLBACK " + profileName;
                        keepOpen = true;
                        return serial;
                    }
                    if (!Contains(helloRaw, HelloStop))
                    {
                        Thread.Sleep(220);
                        continue;
                    }

                    for (int f0Attempt = 1; f0Attempt <= 2; f0Attempt++)
                    {
                        Thread.Sleep(f0Attempt == 1 ? 450 : 300);
                        serial.DiscardInBuffer();
                        serial.Write(F0Request, 0, F0Request.Length);
                        byte[] f0Raw = ReadBurst(serial, 3000, 300);
                        if (Contains(f0Raw, F0Response))
                        {
                            state = "STOP";
                            acquisitionLabel = "FALLBACK " + profileName + " / rodada "
                                + round.ToString(CultureInfo.InvariantCulture)
                                + " / HELLO " + helloAttempt.ToString(CultureInfo.InvariantCulture)
                                + " / F0 " + f0Attempt.ToString(CultureInfo.InvariantCulture);
                            keepOpen = true;
                            return serial;
                        }
                    }
                    return null;
                }
                return null;
            }
            catch (Exception ex)
            {
                AppendLogSafe("V127 FALLBACK perfil sem qualificacao: " + profileName + " - " + ex.Message);
                return null;
            }
            finally
            {
                if (!keepOpen && serial != null)
                {
                    ClosePort(serial);
                    Thread.Sleep(300);
                }
            }
        }

        private void RecoverPgSerialV93(string portName) { RecoverPgSerialV127(portName); }
        private void RecoverPgSerialV119(string portName) { RecoverPgSerialV127(portName); }
        private void RecoverPgSerialV120(string portName) { RecoverPgSerialV127(portName); }

        private void RecoverPgSerialV127(string portName)
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
                recovery.DtrEnable = true;
                recovery.RtsEnable = false;
                Thread.Sleep(240);
                recovery.DtrEnable = true;
                recovery.RtsEnable = true;
                Thread.Sleep(240);
                recovery.DtrEnable = false;
                recovery.RtsEnable = false;
                Thread.Sleep(700);
                AppendLogSafe("V127 FALLBACK recovery: linhas condicionadas sem TX de dados.");
            }
            catch (Exception ex)
            {
                AppendLogSafe("V127 FALLBACK recovery parcial: " + ex.Message);
            }
            finally { ClosePort(recovery); }
        }

        private void PerformF0WriteQualified(SerialPort port, string tag)
        {
            if (port == null || !port.IsOpen)
                throw new InvalidOperationException("V127: porta qualificada foi fechada antes de 38/34.");
            AppendLogSafe(tag + " V127: F0 ja confirmado na mesma COM/perfil; sem repeticao.");
        }

'@
$shell = Replace-Block $shell $acqStart $acqEnd $acqReplacement 'aquisicao PG fast/fallback v1.27'

# O quadro e devolvido assim que comprimento+checksum completos aparecem, sem
# aguardar a janela fixa de silencio usada durante a engenharia reversa.
$frameStart = '        private byte[] SendAndReadFrame(SerialPort port, byte[] request, int expectedLen,'
$frameEnd = '        private static byte[] Build34Request(int startStep)'
$frameReplacement = @'
        private byte[] SendAndReadFrame(SerialPort port, byte[] request, int expectedLen,
            int attempts, int timeoutMs, string label)
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(request, 0, request.Length);

                List<byte> bytes = new List<byte>();
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    int available = port.BytesToRead;
                    if (available > 0)
                    {
                        byte[] buffer = new byte[available];
                        int got = port.Read(buffer, 0, buffer.Length);
                        for (int i = 0; i < got; i++) bytes.Add(buffer[i]);
                        byte[] rawNow = bytes.ToArray();
                        byte[] frame = FindFrame(rawNow, expectedLen);
                        if (frame != null)
                        {
                            AppendLogSafe(label + " tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                                + " RX=" + ToHex(rawNow) + " [FAST]");
                            return frame;
                        }
                    }
                    Thread.Sleep(4);
                }

                byte[] raw = bytes.ToArray();
                AppendLogSafe(label + " tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)) + " [timeout]");
                if (attempt < attempts) Thread.Sleep(80);
            }
            throw new InvalidDataException(label + " nao retornou quadro valido.");
        }

'@
$shell = Replace-Block $shell $frameStart $frameEnd $frameReplacement 'SendAndReadFrame fast v1.27'

# ACK 00 00 FF: encerra a espera assim que um quadro de resposta valido chega.
$ackStart = '        private byte[] SendPg33OnceOnOpenPort(SerialPort port, byte[] frame, string label)'
$ackEnd = '        private static void RequirePhysicalAck00(byte[] raw, string label)'
$ackReplacement = @'
        private byte[] SendPg33OnceOnOpenPort(SerialPort port, byte[] frame, string label)
        {
            port.DiscardInBuffer();
            AppendLogSafe("PG33 " + label + " TX UNICA: " + ToHex(frame));
            port.Write(frame, 0, frame.Length);

            List<byte> bytes = new List<byte>();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(7000);
            while (DateTime.UtcNow < deadline)
            {
                int available = port.BytesToRead;
                if (available > 0)
                {
                    byte[] buffer = new byte[available];
                    int got = port.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < got; i++) bytes.Add(buffer[i]);
                    byte[] rawNow = bytes.ToArray();
                    if (FindFirstValidResponseFrame(rawNow) != null)
                    {
                        AppendLogSafe("PG33 " + label + " RX UNICA: " + ToHex(rawNow) + " [FAST]");
                        File.WriteAllText(Path.Combine(sessionDirectory,
                            "pg33-" + label.ToLowerInvariant() + "-ack-raw.hex"),
                            ToHex(rawNow) + Environment.NewLine, Encoding.ASCII);
                        return rawNow;
                    }
                }
                Thread.Sleep(4);
            }

            byte[] raw = bytes.ToArray();
            AppendLogSafe("PG33 " + label + " RX UNICA: " + (raw.Length == 0 ? "[]" : ToHex(raw)));
            File.WriteAllText(Path.Combine(sessionDirectory,
                "pg33-" + label.ToLowerInvariant() + "-ack-raw.hex"),
                (raw.Length == 0 ? "(sem bytes)" : ToHex(raw)) + Environment.NewLine, Encoding.ASCII);
            return raw;
        }

'@
$shell = Replace-Block $shell $ackStart $ackEnd $ackReplacement 'ACK PG33 fast v1.27'

# READ manual usa esta rotina. Remove esperas fixas entre uma resposta ja valida
# e o comando seguinte; em falha, mantem retry e fallback conservador.
$readStart = '        private ProgramSnapshot ReadSnapshotV93Robust(string portName, string tag)'
$readEnd = '        private static bool SnapshotsEqual(ProgramSnapshot a, ProgramSnapshot b)'
$readReplacement = @'
        private ProgramSnapshot ReadSnapshotV93Robust(string portName, string tag)
        {
            Exception last = null;
            for (int round = 1; round <= 3; round++)
            {
                SerialPort port = null;
                try
                {
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN durante readback.");

                    PerformF0WriteQualified(port, "V127-READ");
                    SendAndReadFrame(port, Frame38Request, 0x02, 3, 1800,
                        tag + "-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    ProgramSnapshot result = ReadCanonicalSnapshotOnOpenPort(port,
                        tag + "-r" + round.ToString(CultureInfo.InvariantCulture));
                    AppendLogSafe(tag + " FAST PG OK: " + result.Count.ToString(CultureInfo.InvariantCulture)
                        + " passos | " + acquisition + ".");
                    return result;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe(tag + " rodada " + round.ToString(CultureInfo.InvariantCulture)
                        + " falhou: " + ex.Message);
                    if (round < 3) Thread.Sleep(150 * round);
                }
                finally { ClosePort(port); }
            }
            throw new IOException("Readback falhou apos 3 rodadas FAST/fallback. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

'@
$shell = Replace-Block $shell $readStart $readEnd $readReplacement 'ReadSnapshot fast v1.27'

# Identificacao visual e logs da nova camada; STOP/RUN permanecem sem TX.
$shell = $shell.Replace('TP02 / TP-232PG";', 'TP02 / TP-232PG  •  FAST";')
$shell = $shell.Replace('STOP / RUN protegidos ate validacao fisica', 'FAST PG ativo  |  STOP / RUN protegidos ate validacao fisica')
$shell = $shell.Replace('PG READ v1.25 iniciado em ', 'PG READ FAST v1.27 iniciado em ')
$shell = $shell.Replace('PG33 READBACK-ONLY VERIFY v1.24 iniciado em ', 'PG33 READBACK-ONLY VERIFY FAST v1.27 iniciado em ')
$shell = $shell.Replace('PG33 PROJECT WRITE v1.24 iniciado em ', 'PG33 PROJECT WRITE FAST v1.27 iniciado em ')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 Fast PG V127 aplicado: resposta dirigida por quadro, aquisicao rapida com fallback e ACK imediato.'
