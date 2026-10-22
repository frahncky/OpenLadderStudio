$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

# v1.22: um perfil so qualifica a sessao quando HELLO STOP e F0 respondem
# na mesma SerialPort, sem fechar ou alternar DTR/RTS entre as duas etapas.
# OFF/OFF, ON/OFF e ON/ON sao tentados apenas com comandos de leitura; nenhum
# PG33 e transmitido enquanto o perfil nao estiver integralmente qualificado.
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

            AppendLogSafe("V122 STARTUP: rodada " + round.ToString(CultureInfo.InvariantCulture)
                + " | qualificacao exige HELLO STOP + F0 na mesma COM e no mesmo perfil.");
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
                SerialPort qualified = TryAcquireQualifiedProfileV122(
                    portName, dtr[profile], rts[profile], names[profile],
                    round, out state, out acquisitionLabel);
                if (qualified != null) return qualified;
                Thread.Sleep(650);
            }

            RecoverPgSerialV120(portName);
            throw new TimeoutException(
                "V122: nenhum perfil confirmou HELLO STOP e F0 00 02 10 22 CB na mesma sessao.");
        }

        private SerialPort TryAcquireQualifiedProfileV122(
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
                AppendLogSafe("V122 PERFIL: " + profileName + ".");

                for (int helloAttempt = 1; helloAttempt <= 2; helloAttempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] helloRaw = ReadBurst(serial, 1800, 240);
                    AppendLogSafe("V122 " + profileName + " HELLO "
                        + helloAttempt.ToString(CultureInfo.InvariantCulture) + " RX="
                        + (helloRaw.Length == 0 ? "[]" : ToHex(helloRaw)));

                    if (Contains(helloRaw, HelloRun))
                    {
                        state = "RUN";
                        acquisitionLabel = profileName;
                        keepOpen = true;
                        AppendLogSafe("V122 BLOQUEADO: HELLO indica RUN; nenhum F0/PG33 sera enviado.");
                        return serial;
                    }

                    if (!Contains(helloRaw, HelloStop))
                    {
                        Thread.Sleep(220);
                        continue;
                    }

                    AppendLogSafe("V122 HELLO STOP confirmado em " + profileName
                        + "; qualificando F0 sem fechar nem alterar as linhas.");

                    for (int f0Attempt = 1; f0Attempt <= 2; f0Attempt++)
                    {
                        Thread.Sleep(f0Attempt == 1 ? 450 : 300);
                        serial.DiscardInBuffer();
                        AppendLogSafe("V122 " + profileName + " F0 "
                            + f0Attempt.ToString(CultureInfo.InvariantCulture) + " TX: " + ToHex(F0Request));
                        serial.Write(F0Request, 0, F0Request.Length);
                        byte[] f0Raw = ReadBurst(serial, 3000, 300);
                        AppendLogSafe("V122 " + profileName + " F0 "
                            + f0Attempt.ToString(CultureInfo.InvariantCulture) + " RX="
                            + (f0Raw.Length == 0 ? "[]" : ToHex(f0Raw)));

                        if (Contains(f0Raw, F0Response))
                        {
                            state = "STOP";
                            acquisitionLabel = profileName + " / rodada "
                                + round.ToString(CultureInfo.InvariantCulture)
                                + " / HELLO " + helloAttempt.ToString(CultureInfo.InvariantCulture)
                                + " / F0 " + f0Attempt.ToString(CultureInfo.InvariantCulture);
                            keepOpen = true;
                            AppendLogSafe("V122 QUALIFICADO: HELLO STOP + F0 confirmados na mesma COM/perfil; liberando somente 38/34 e o teste controlado.");
                            return serial;
                        }
                    }

                    AppendLogSafe("V122 " + profileName
                        + ": HELLO respondeu, mas F0 ficou sem resposta; fechando este perfil sem transmitir PG33.");
                    return null;
                }
                return null;
            }
            catch (Exception ex)
            {
                AppendLogSafe("V122 PERFIL sem qualificacao: " + profileName + " - " + ex.Message);
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
                AppendLogSafe("V122 RECOVERY: linhas condicionadas sem TX de dados.");
            }
            catch (Exception ex)
            {
                AppendLogSafe("V122 RECOVERY parcial: " + ex.Message);
            }
            finally { ClosePort(recovery); }
        }

        private void PerformF0WriteQualified(SerialPort port, string tag)
        {
            if (port == null || !port.IsOpen)
                throw new InvalidOperationException("V122: porta qualificada foi fechada antes de 38/34.");
            AppendLogSafe(tag + " V122: F0 ja confirmado durante a qualificacao da mesma COM/perfil; nenhuma repeticao necessaria.");
        }
'@

$shell = $shell.Substring(0, $acqStart) + $replacement + $shell.Substring($acqEnd)

# Apenas a prova alterar+restaurar/readbacks usa o F0 qualificado novo.
$start = $shell.IndexOf('        private void StartChangeRestoreProbe()', [System.StringComparison]::Ordinal)
if ($start -lt 0) { throw 'StartChangeRestoreProbe nao encontrado.' }
$tail = $shell.Substring($start)
$tail = $tail.Replace('PerformF0(port);', 'PerformF0WriteQualified(port, "V122-WRITE");')
$tail = $tail.Replace('PG33 CHANGE+RESTORE v1.19 iniciado em ', 'PG33 CHANGE+RESTORE v1.22 iniciado em ')
$tail = $tail.Replace('PASS PG33 CHANGE+RESTORE v1.19', 'PASS PG33 CHANGE+RESTORE v1.22')
$tail = $tail.Replace(
    'Readback final falhou apos 5 rodadas de aquisicao. Ultimo erro: ',
    'A restauracao recebeu ACK 00 00 FF, mas a verificacao final independente nao conseguiu reconectar apos 5 rodadas. Confirme o programa pelo PC12. Diagnostico: ')
$shell = $shell.Substring(0, $start) + $tail

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Qualified Write V122 aplicado: HELLO STOP + F0 qualificam dinamicamente o mesmo perfil antes de 38/34/PG33.'
