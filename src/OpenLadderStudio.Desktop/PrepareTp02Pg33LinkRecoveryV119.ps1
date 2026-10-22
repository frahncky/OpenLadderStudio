$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)

# v1.19: restaurar na sonda PG33 a varredura de linhas que tornou a validacao
# fisica estavel: 8O1 OFF/OFF primeiro, depois ON/OFF e ON/ON. Nenhum byte alem
# de CON-ICB e enviado durante a aquisicao. Assim que HELLO e confirmado, a MESMA
# SerialPort permanece aberta para F0 -> 38 -> 34 -> PG33.
#
# Substituicao por ancoras estruturais, sem depender do corpo exato gerado pelas
# etapas anteriores do build.
$startAnchor = '        private SerialPort AcquireStablePgPortV93'
$endAnchor = '        private void StartChangeRestoreProbe()'
$start = $shell.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Inicio de AcquireStablePgPortV93 nao encontrado.' }
$end = $shell.IndexOf($endAnchor, $start, [System.StringComparison]::Ordinal)
if ($end -lt 0) { throw 'Inicio de StartChangeRestoreProbe nao encontrado apos AcquireStablePgPortV93.' }

$acqReplacement = @'
        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;

            string[] profileNames = new string[]
            {
                "19200 8O1 DTR=off RTS=off",
                "19200 8O1 DTR=on RTS=off",
                "19200 8O1 DTR=on RTS=on"
            };
            bool[] dtr = new bool[] { false, true, true };
            bool[] rts = new bool[] { false, false, true };

            AppendLogSafe("V119 STARTUP: rodada " + round.ToString(CultureInfo.InvariantCulture)
                + " | aquisicao 8O1 com varredura segura das linhas DTR/RTS.");

            Thread.Sleep(round == 1 ? 1500 : (round == 2 ? 2600 : 3600));

            for (int sweep = 1; sweep <= 2; sweep++)
            {
                if (sweep == 2)
                {
                    AppendLogSafe("V119 RECOVERY: primeiro ciclo sem HELLO; reacondicionando DTR/RTS sem TX de dados.");
                    RecoverPgSerialV119(portName);
                    Thread.Sleep(1400);
                }

                for (int p = 0; p < profileNames.Length; p++)
                {
                    SerialPort serial = null;
                    bool keepOpen = false;
                    try
                    {
                        serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                        serial.Handshake = Handshake.None;
                        serial.DtrEnable = dtr[p];
                        serial.RtsEnable = rts[p];
                        serial.ReadTimeout = 80;
                        serial.WriteTimeout = 1000;
                        serial.Open();
                        serial.DiscardInBuffer();
                        serial.DiscardOutBuffer();

                        int settleMs;
                        if (p == 0) settleMs = sweep == 1 ? 850 : 1100;
                        else if (p == 1) settleMs = 650;
                        else settleMs = 750;
                        Thread.Sleep(settleMs);

                        int attempts = p == 0 ? 6 : (p == 1 ? 3 : 4);
                        int receiveMs = p == 0 ? (sweep == 1 ? 2600 : 3000) : 2300;
                        AppendLogSafe("V119 PERFIL [ciclo " + sweep.ToString(CultureInfo.InvariantCulture)
                            + "/2]: " + profileNames[p]
                            + " | tentativas=" + attempts.ToString(CultureInfo.InvariantCulture));

                        for (int attempt = 1; attempt <= attempts; attempt++)
                        {
                            serial.DiscardInBuffer();
                            serial.Write(HelloRequest, 0, HelloRequest.Length);
                            byte[] raw = ReadBurst(serial, receiveMs, 220);
                            AppendLogSafe("V119 HELLO " + attempt.ToString(CultureInfo.InvariantCulture)
                                + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));

                            if (Contains(raw, HelloStop))
                            {
                                state = "STOP";
                                acquisitionLabel = profileNames[p] + " / ciclo "
                                    + sweep.ToString(CultureInfo.InvariantCulture)
                                    + " / tentativa " + attempt.ToString(CultureInfo.InvariantCulture);
                                keepOpen = true;
                                AppendLogSafe("V119 ESTAVEL: HELLO STOP confirmado; mantendo esta mesma COM/perfil aberto.");
                                return serial;
                            }
                            if (Contains(raw, HelloRun))
                            {
                                state = "RUN";
                                acquisitionLabel = profileNames[p] + " / ciclo "
                                    + sweep.ToString(CultureInfo.InvariantCulture)
                                    + " / tentativa " + attempt.ToString(CultureInfo.InvariantCulture);
                                keepOpen = true;
                                AppendLogSafe("V119 ESTAVEL: HELLO RUN confirmado; mantendo esta mesma COM/perfil aberto.");
                                return serial;
                            }

                            Thread.Sleep(p == 0 ? 320 : 220);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLogSafe("V119 PERFIL sem confirmacao: " + profileNames[p] + " - " + ex.Message);
                    }
                    finally
                    {
                        if (!keepOpen && serial != null)
                        {
                            ClosePort(serial);
                            Thread.Sleep(p == 0 ? 450 : 320);
                        }
                    }
                }
            }

            throw new TimeoutException("V119: HELLO PG nao confirmado apos varredura 8O1 OFF/OFF, ON/OFF e ON/ON.");
        }

        private void RecoverPgSerialV93(string portName)
        {
            RecoverPgSerialV119(portName);
        }

        private void RecoverPgSerialV119(string portName)
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
                Thread.Sleep(260);
                recovery.DtrEnable = true;
                recovery.RtsEnable = true;
                Thread.Sleep(260);
                recovery.DtrEnable = false;
                recovery.RtsEnable = false;
                Thread.Sleep(650);
                AppendLogSafe("V119 RECOVERY: DTR/RTS reacondicionados sem transmissao de bytes.");
            }
            catch (Exception ex)
            {
                AppendLogSafe("V119 RECOVERY parcial: " + ex.Message);
            }
            finally
            {
                ClosePort(recovery);
            }
        }

'@

$shell = $shell.Substring(0, $start) + $acqReplacement + $shell.Substring($end)

$shell = $shell.Replace('for (int round = 1; round <= 3 && !testWritten; round++)',
    'for (int round = 1; round <= 5 && !testWritten; round++)')
$shell = $shell.Replace('for (int round = 1; round <= 3 && !restored; round++)',
    'for (int round = 1; round <= 5 && !restored; round++)')
$shell = $shell.Replace('for (int round = 1; round <= 3; round++)',
    'for (int round = 1; round <= 5; round++)')

$shell = $shell.Replace('apos 3 rodadas V93. PLC nao foi alterado.',
    'apos 5 rodadas de aquisicao. PLC nao foi alterado.')
$shell = $shell.Replace('apos 3 rodadas V93. ',
    'apos 5 rodadas de aquisicao. ')
$shell = $shell.Replace('Readback V93 final falhou apos 3 rodadas.',
    'Readback final falhou apos 5 rodadas de aquisicao.')

$shell = $shell.Replace('PG33 CHANGE+RESTORE v1.18 iniciado em ', 'PG33 CHANGE+RESTORE v1.19 iniciado em ')
$shell = $shell.Replace('PASS PG33 CHANGE+RESTORE v1.18', 'PASS PG33 CHANGE+RESTORE v1.19')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Link Recovery V119 aplicado: full 8O1 DTR/RTS sweep, line recovery sem TX e 5 rodadas pre-escrita.'

# v1.20 e uma especializacao posterior: somente OFF/OFF pode qualificar a sessao
# binaria; os demais perfis ficam restritos a condicionamento do enlace.
& (Join-Path (Get-Location) 'PrepareTp02Pg33QualifiedWriteV120.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
