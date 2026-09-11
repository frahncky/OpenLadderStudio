$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

# v1.20: somente OFF/OFF pode qualificar a sessao que seguira para F0/38/34/PG33.
# ON/OFF e ON/ON sao usados apenas como condicionamento HELLO e sempre sao fechados
# antes de voltar para OFF/OFF. Isso preserva o unico perfil fisicamente comprovado
# ate o ACK PG33 00 00 FF.
# Usa as mesmas ancoras estruturais da v1.19. O corpo contem blocos aninhados e
# nao deve ser localizado por uma regex que tente equilibrar chaves.
$acqStartAnchor = '        private SerialPort AcquireStablePgPortV93'
$acqEndAnchor = '        private void StartChangeRestoreProbe()'
$acqStart = $shell.IndexOf($acqStartAnchor, [System.StringComparison]::Ordinal)
if ($acqStart -lt 0) { throw 'Inicio de AcquireStablePgPortV93 V120 nao encontrado.' }
$acqEnd = $shell.IndexOf($acqEndAnchor, $acqStart, [System.StringComparison]::Ordinal)
if ($acqEnd -lt 0) { throw 'Inicio de StartChangeRestoreProbe nao encontrado apos AcquireStablePgPortV93 V120.' }

$replacement = @'
        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;

            AppendLogSafe("V120 STARTUP: rodada " + round.ToString(CultureInfo.InvariantCulture)
                + " | somente OFF/OFF pode qualificar escrita.");
            Thread.Sleep(round == 1 ? 1800 : (round == 2 ? 2800 : 3800));

            // CICLO 1: perfil fisicamente comprovado ate PG33.
            SerialPort qualified = TryAcquireOffOffV120(portName, 1, out state, out acquisitionLabel);
            if (qualified != null) return qualified;

            // Condicionamento: estes perfis NUNCA seguem para F0/PG33.
            AppendLogSafe("V120 CONDITIONING: OFF/OFF sem HELLO; usando ON/OFF e ON/ON apenas para acordar a interface.");
            ConditionProfileV120(portName, true, false, "DTR=on RTS=off");
            ConditionProfileV120(portName, true, true, "DTR=on RTS=on");

            RecoverPgSerialV120(portName);
            Thread.Sleep(1600);

            // CICLO 2: obrigatoriamente volta ao perfil comprovado.
            qualified = TryAcquireOffOffV120(portName, 2, out state, out acquisitionLabel);
            if (qualified != null) return qualified;

            throw new TimeoutException("V120: OFF/OFF nao confirmou HELLO mesmo apos condicionamento seguro.");
        }

        private SerialPort TryAcquireOffOffV120(string portName, int cycle,
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
                serial.DtrEnable = false;
                serial.RtsEnable = false;
                serial.ReadTimeout = 80;
                serial.WriteTimeout = 1000;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();

                int settle = cycle == 1 ? 1000 : 1500;
                Thread.Sleep(settle);
                AppendLogSafe("V120 OFF/OFF ciclo " + cycle.ToString(CultureInfo.InvariantCulture)
                    + " | settle=" + settle.ToString(CultureInfo.InvariantCulture) + " ms.");

                int attempts = cycle == 1 ? 7 : 9;
                int rxMs = cycle == 1 ? 2800 : 3200;
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] raw = ReadBurst(serial, rxMs, 240);
                    AppendLogSafe("V120 OFF/OFF HELLO " + attempt.ToString(CultureInfo.InvariantCulture)
                        + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));

                    if (Contains(raw, HelloStop))
                    {
                        state = "STOP";
                        acquisitionLabel = "19200 8O1 DTR=off RTS=off / ciclo "
                            + cycle.ToString(CultureInfo.InvariantCulture)
                            + " / tentativa " + attempt.ToString(CultureInfo.InvariantCulture);
                        keepOpen = true;
                        AppendLogSafe("V120 QUALIFICADO: HELLO STOP em OFF/OFF; esta mesma COM seguira para F0/38/34/PG33.");
                        return serial;
                    }
                    if (Contains(raw, HelloRun))
                    {
                        state = "RUN";
                        acquisitionLabel = "19200 8O1 DTR=off RTS=off / ciclo "
                            + cycle.ToString(CultureInfo.InvariantCulture)
                            + " / tentativa " + attempt.ToString(CultureInfo.InvariantCulture);
                        keepOpen = true;
                        AppendLogSafe("V120 QUALIFICADO: HELLO RUN em OFF/OFF; escrita sera bloqueada por estado RUN.");
                        return serial;
                    }
                    Thread.Sleep(360);
                }
                return null;
            }
            finally
            {
                if (!keepOpen && serial != null)
                {
                    ClosePort(serial);
                    Thread.Sleep(500);
                }
            }
        }

        private void ConditionProfileV120(string portName, bool dtr, bool rts, string label)
        {
            SerialPort serial = null;
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
                Thread.Sleep(650);

                for (int i = 1; i <= 2; i++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] raw = ReadBurst(serial, 2200, 220);
                    AppendLogSafe("V120 CONDITION " + label + " HELLO "
                        + i.ToString(CultureInfo.InvariantCulture) + " RX="
                        + (raw.Length == 0 ? "[]" : ToHex(raw)));
                    if (Contains(raw, HelloStop) || Contains(raw, HelloRun))
                    {
                        AppendLogSafe("V120 CONDITION: " + label
                            + " acordou o enlace; fechando este perfil e voltando obrigatoriamente a OFF/OFF.");
                        break;
                    }
                    Thread.Sleep(260);
                }
            }
            catch (Exception ex)
            {
                AppendLogSafe("V120 CONDITION " + label + " parcial: " + ex.Message);
            }
            finally
            {
                ClosePort(serial);
                Thread.Sleep(450);
            }
        }

        private void RecoverPgSerialV93(string portName)
        {
            RecoverPgSerialV120(portName);
        }

        private void RecoverPgSerialV119(string portName)
        {
            RecoverPgSerialV120(portName);
        }

        private void RecoverPgSerialV120(string portName)
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
                Thread.Sleep(900);
                recovery.DtrEnable = true;
                recovery.RtsEnable = false;
                Thread.Sleep(220);
                recovery.DtrEnable = true;
                recovery.RtsEnable = true;
                Thread.Sleep(220);
                recovery.DtrEnable = false;
                recovery.RtsEnable = false;
                Thread.Sleep(900);
                AppendLogSafe("V120 RECOVERY: linhas condicionadas e finalizadas em OFF/OFF, sem TX de dados.");
            }
            catch (Exception ex)
            {
                AppendLogSafe("V120 RECOVERY parcial: " + ex.Message);
            }
            finally { ClosePort(recovery); }
        }

        private void PerformF0WriteQualified(SerialPort port, string tag)
        {
            // O HELLO ja foi confirmado em OFF/OFF. Nao enviar outro HELLO.
            Thread.Sleep(900);
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                port.DiscardInBuffer();
                AppendLogSafe(tag + " F0 " + attempt.ToString(CultureInfo.InvariantCulture) + " TX: " + ToHex(F0Request));
                port.Write(F0Request, 0, F0Request.Length);
                byte[] raw = ReadBurst(port, 4300, 300);
                AppendLogSafe(tag + " F0 " + attempt.ToString(CultureInfo.InvariantCulture) + " RX="
                    + (raw.Length == 0 ? "[]" : ToHex(raw)));
                if (Contains(raw, F0Response))
                {
                    AppendLogSafe(tag + " F0 QUALIFICADO: 00 02 10 22 CB confirmado em OFF/OFF.");
                    return;
                }
                Thread.Sleep(650);
            }
            throw new InvalidDataException("F0 nao retornou 00 02 10 22 CB na sessao OFF/OFF qualificada.");
        }

'@

$shell = $shell.Substring(0, $acqStart) + $replacement + $shell.Substring($acqEnd)

# Apenas a prova alterar+restaurar/readbacks usa o F0 qualificado novo.
$start = $shell.IndexOf('        private void StartChangeRestoreProbe()', [System.StringComparison]::Ordinal)
if ($start -lt 0) { throw 'StartChangeRestoreProbe nao encontrado.' }
$tail = $shell.Substring($start)
$tail = $tail.Replace('PerformF0(port);', 'PerformF0WriteQualified(port, "V120-WRITE");')
$tail = $tail.Replace('PG33 CHANGE+RESTORE v1.19 iniciado em ', 'PG33 CHANGE+RESTORE v1.20 iniciado em ')
$tail = $tail.Replace('PASS PG33 CHANGE+RESTORE v1.19', 'PASS PG33 CHANGE+RESTORE v1.20')
$shell = $shell.Substring(0, $start) + $tail

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Qualified Write V120 aplicado: somente OFF/OFF avanca para F0/38/34/PG33; outros perfis apenas condicionam HELLO.'
