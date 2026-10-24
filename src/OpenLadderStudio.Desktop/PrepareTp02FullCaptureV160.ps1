$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02FullProtocolCapture.build.cs'
if (-not (Test-Path -LiteralPath $path)) { throw 'TP02FullProtocolCapture.build.cs nao encontrado apos v1.59.' }

$text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

$oldCall = 'AcquireQualifiedPortV159(PortName, out state)'
$newCall = 'AcquireQualifiedPortV160(PortName, out state)'
if (-not $text.Contains($oldCall)) { throw 'V160: chamada AcquireQualifiedPortV159 nao localizada.' }
$text = $text.Replace($oldCall, $newCall)

$anchor = '        private static void CaptureF0AlreadyQualifiedV159(SerialPort port)'
$idx = $text.IndexOf($anchor, [StringComparison]::Ordinal)
if ($idx -lt 0) { throw 'V160: helper V159 nao localizado para insercao.' }

$methods = @'
        // v1.60: replica a sequencia fisica comprovada pelo PG Lab 1.13/1.14.
        // Cada abertura da COM admite ate 6 HELLOs, mas EXATAMENTE UM F0.
        // Se o F0 ficar silencioso, a porta e fechada e reaberta apos 1500 ms.
        private static SerialPort AcquireQualifiedPortV160(string portName, out string state)
        {
            const int maxSessions = 12;
            const int helloAttemptsPerSession = 6;
            byte[] f0Expected = new byte[] { 0x00, 0x02, 0x10, 0x22, 0xCB };
            Exception last = null;

            PortProfile profile = new PortProfile();
            profile.Name = "V160 sessao limpa 19200 8O1 DTR=on RTS=off";
            profile.Dtr = true;
            profile.Rts = false;

            for (int session = 1; session <= maxSessions; session++)
            {
                SerialPort port = null;
                try
                {
                    Log("V160: iniciando sessao limpa " + session.ToString(CultureInfo.InvariantCulture)
                        + "/" + maxSessions.ToString(CultureInfo.InvariantCulture)
                        + " | 19200 8O1 DTR=on RTS=off.");

                    port = OpenPort(portName, true, false);
                    Thread.Sleep(650);

                    string detected = null;
                    for (int helloAttempt = 1; helloAttempt <= helloAttemptsPerSession; helloAttempt++)
                    {
                        byte[] helloRaw = ExchangeRaw(port, Hello, 1800, 220,
                            "V160-S" + session.ToString("00", CultureInfo.InvariantCulture)
                            + "-HELLO-" + helloAttempt.ToString(CultureInfo.InvariantCulture), "session");

                        if (Contains(helloRaw, HelloStop))
                        {
                            detected = "STOP";
                            break;
                        }
                        if (Contains(helloRaw, HelloRun))
                        {
                            detected = "RUN";
                            break;
                        }

                        if (helloAttempt < helloAttemptsPerSession) Thread.Sleep(180);
                    }

                    if (detected == null)
                    {
                        Log("V160: HELLO nao confirmado nesta abertura; fechando a COM sem enviar F0.");
                    }
                    else
                    {
                        Log("V160: HELLO confirmou " + detected + "; aguardando 180 ms antes do unico F0 desta abertura.");
                        Thread.Sleep(180);

                        // Regra critica: um unico F0 por abertura da COM.
                        byte[] f0Raw = ExchangeRaw(port, F0, 1600, 220,
                            "V160-S" + session.ToString("00", CultureInfo.InvariantCulture) + "-F0-ONLY", "qualification");

                        if (Contains(f0Raw, f0Expected))
                        {
                            ActiveProfile = profile;
                            state = detected;
                            RememberProfileV159(portName, profile);
                            Log("V160: HELLO+F0 confirmados na mesma sessao limpa: 00 02 10 22 CB.");
                            return port;
                        }

                        Log("V160: F0 sem resposta conhecida; nenhum 38/34/0A sera enviado nesta abertura.");
                    }
                }
                catch (Exception ex)
                {
                    last = ex;
                    Log("V160: falha na sessao limpa " + session.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
                }

                ClosePort(port);
                if (session < maxSessions)
                {
                    Log("V160: COM fechada; aguardando 1500 ms antes de nova sessao limpa.");
                    Thread.Sleep(1500);
                }
            }

            state = string.Empty;
            throw new IOException("V160: 12 sessoes limpas sem confirmar HELLO+F0. "
                + (last == null ? string.Empty : last.Message));
        }

'@

$text = $text.Substring(0, $idx) + $methods + $text.Substring($idx)

if (-not $text.Contains('AcquireQualifiedPortV160')) { throw 'V160: helper de sessao limpa ausente no resultado.' }
if ($text.Contains('port = AcquireQualifiedPortV159(PortName, out state)')) { throw 'V160: chamada principal ainda aponta para V159.' }

[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
Write-Host 'TP02 Full Capture v1.60 aplicado: sessao limpa, ate 6 HELLOs, um unico F0 e reabertura de 1500 ms.'
