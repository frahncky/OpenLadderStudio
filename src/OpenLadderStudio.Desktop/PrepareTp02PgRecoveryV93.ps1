$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($shellPath)
$startAnchor = '        private bool TryProbePgLink(string portName, out string helloVariant, out string profileName)'
$endAnchor = '        private static byte[] ReadPgBurst(SerialPort port, int timeoutMs)'
$start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Inicio de TryProbePgLink nao encontrado.' }
$end = $text.IndexOf($endAnchor, $start, [System.StringComparison]::Ordinal)
if ($end -lt 0) { throw 'Fim de TryProbePgLink nao encontrado.' }

$replacement = @'
        private bool TryProbePgLink(string portName, out string helloVariant, out string profileName)
        {
            helloVariant = string.Empty;
            profileName = string.Empty;
            byte[] hello = new byte[] { 0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D };
            byte[] helloC0 = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
            byte[] hello80 = new byte[] { 0x80, 0x01, 0x09, 0x75 };

            // O perfil fisicamente mais estavel fica sempre em primeiro lugar.
            string[] names = new string[]
            {
                "19200 8O1 DTR=on RTS=off",
                "19200 8O1 DTR=on RTS=on",
                "19200 8O1 DTR=off RTS=off"
            };
            bool[] dtr = new bool[] { true, true, false };
            bool[] rts = new bool[] { false, true, false };

            AppendLogSafe("PG STARTUP: aguardando estabilizacao do TP-232PG antes do primeiro HELLO...");
            Thread.Sleep(700);

            // Duas varreduras internas reproduzem automaticamente o comportamento
            // observado em bancada: a primeira abertura pode apenas inicializar o
            // conversor/linhas seriais e a segunda passa a responder normalmente.
            for (int sweep = 1; sweep <= 2; sweep++)
            {
                if (sweep == 2)
                {
                    AppendLogSafe("PG AUTO-RETRY: primeira varredura sem HELLO; rearmando a serial e tentando novamente sem intervencao do usuario.");
                    RearmPgSerial(portName);
                    Thread.Sleep(900);
                }

                for (int p = 0; p < names.Length; p++)
                {
                    SerialPort serial = null;
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

                        // Na segunda varredura damos mais tempo para o opto/acoplador
                        // e o PLC estabilizarem antes do primeiro byte transmitido.
                        Thread.Sleep(sweep == 1 ? 300 : 650);
                        AppendLogSafe("PG PERFIL [ciclo " + sweep.ToString(CultureInfo.InvariantCulture) + "/2]: " + names[p]);

                        int attempts = p == 0 ? 5 : 3;
                        for (int attempt = 1; attempt <= attempts; attempt++)
                        {
                            serial.DiscardInBuffer();
                            AppendLogSafe("PG HELLO TX " + attempt.ToString(CultureInfo.InvariantCulture) + ": 43 4F 4E 2D 49 43 42 0D");
                            serial.Write(hello, 0, hello.Length);
                            byte[] raw = ReadPgBurst(serial, sweep == 1 ? 1700 : 2200);
                            AppendLogSafe("PG HELLO RX: " + (raw.Length == 0 ? "[]" : PgHex(raw)));

                            if (PgContains(raw, helloC0) && PgSum8(helloC0) == 0xFF)
                            {
                                helloVariant = "C0 01 09 35";
                                profileName = names[p] + " / ciclo " + sweep.ToString(CultureInfo.InvariantCulture);
                                return true;
                            }
                            if (PgContains(raw, hello80) && PgSum8(hello80) == 0xFF)
                            {
                                helloVariant = "80 01 09 75";
                                profileName = names[p] + " / ciclo " + sweep.ToString(CultureInfo.InvariantCulture);
                                return true;
                            }
                            Thread.Sleep(sweep == 1 ? 160 : 240);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLogSafe("PG PERFIL sem confirmacao: " + names[p] + " - " + ex.Message);
                    }
                    finally
                    {
                        if (serial != null)
                        {
                            try { if (serial.IsOpen) serial.Close(); } catch { }
                            serial.Dispose();
                        }
                        Thread.Sleep(sweep == 1 ? 250 : 400);
                    }
                }
            }

            AppendLogSafe("PG RESULTADO: duas varreduras completas sem HELLO conhecido; somente agora sera considerado o fallback Computer Link.");
            return false;
        }

        private void RearmPgSerial(string portName)
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
                Thread.Sleep(450);
                recovery.DtrEnable = true;
                recovery.RtsEnable = false;
                Thread.Sleep(450);
                AppendLogSafe("PG RECOVERY: DTR/RTS rearmados sem envio de bytes.");
            }
            catch (Exception ex)
            {
                AppendLogSafe("PG RECOVERY: rearme parcial - " + ex.Message);
            }
            finally
            {
                if (recovery != null)
                {
                    try { if (recovery.IsOpen) recovery.Close(); } catch { }
                    recovery.Dispose();
                }
            }
        }

'@

$text = $text.Substring(0, $start) + $replacement + $text.Substring($end)
[System.IO.File]::WriteAllText($shellPath, $text, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG Recovery V93 aplicado: auto-retry e estabilizacao do TP-232PG.'
