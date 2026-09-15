$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path (Get-Location) 'TP02FullProtocolCapture.cs'
$buildPath = Join-Path (Get-Location) 'TP02FullProtocolCapture.build.cs'
if (-not (Test-Path -LiteralPath $sourcePath)) { throw 'TP02FullProtocolCapture.cs nao encontrado.' }
$text = [IO.File]::ReadAllText($sourcePath)

function Replace-Required([string]$input,[string]$needle,[string]$replacement,[string]$label) {
    if (-not $input.Contains($needle)) { throw "Ancora nao encontrada ($label): $needle" }
    return $input.Replace($needle,$replacement)
}

# Ancoras minimais de codigo: independentes de indentacao e textos PT-BR.
$text = Replace-Required $text 'AcquirePort(PortName, out state)' 'AcquireQualifiedPortV159(PortName, out state)' 'AcquirePort call'
$text = Replace-Required $text 'CaptureF0(port, "BASE-F0")' 'CaptureF0AlreadyQualifiedV159(port)' 'CaptureF0 call'
$text = Replace-Required $text 'ExchangeRaw(port, Frame38, 2200, 140, prefix + "-38", "program-read")' 'ExchangeExpectedV159(port, Frame38, 2, 6, 3200, 220, prefix + "-38", "program-read")' 'PG38 call'
$text = Replace-Required $text 'ExchangeRaw(port, request, 5000, 180, prefix + "-34-" + start.ToString("0000", CultureInfo.InvariantCulture), "program-read")' 'ExchangeExpectedV159(port, request, Tp02Pg34Pager.PayloadLength, 4, 5500, 260, prefix + "-34-" + start.ToString("0000", CultureInfo.InvariantCulture), "program-read")' 'PG34 call'

$anchor = 'BuildProfiles(string portName)'
$methodPos = $text.IndexOf($anchor,[StringComparison]::Ordinal)
if ($methodPos -lt 0) { throw 'Ancora BuildProfiles nao encontrada.' }
$idx = $text.LastIndexOf('        private static List<PortProfile>', $methodPos, [StringComparison]::Ordinal)
if ($idx -lt 0) { throw 'Inicio de BuildProfiles nao encontrado.' }

$methods = @'
        private static void CaptureF0AlreadyQualifiedV159(SerialPort port)
        {
            // Intencionalmente vazio: AcquireQualifiedPortV159 ja confirmou F0
            // nesta mesma abertura da COM. Evita repetir F0 imediatamente.
            if (port == null || !port.IsOpen) throw new InvalidOperationException("Sessao PG v1.59 nao esta aberta.");
        }

        // v1.59: HELLO isolado prova presenca, mas nao prova que a sessao PG ja aceita
        // os comandos seguintes. So retornamos a porta apos HELLO+F0 na mesma sessao.
        private static SerialPort AcquireQualifiedPortV159(string portName, out string state)
        {
            List<PortProfile> profiles = BuildProfiles(portName);
            Exception last = null;
            byte[] f0Expected = new byte[] { 0x00, 0x02, 0x10, 0x22, 0xCB };

            for (int p = 0; p < profiles.Count; p++)
            {
                PortProfile profile = profiles[p];
                SerialPort port = null;
                try
                {
                    port = OpenPort(portName, profile.Dtr, profile.Rts);
                    Thread.Sleep(p == 0 ? 550 : 850);

                    for (int helloAttempt = 1; helloAttempt <= 8; helloAttempt++)
                    {
                        int helloTimeout = helloAttempt <= 4 ? 850 : 1200;
                        byte[] helloRaw = ExchangeRaw(port, Hello, helloTimeout, 120,
                            "V159-ACQUIRE-" + (p + 1).ToString("00", CultureInfo.InvariantCulture)
                            + "-HELLO-" + helloAttempt.ToString(CultureInfo.InvariantCulture), "session");

                        string detected = null;
                        if (Contains(helloRaw, HelloStop)) detected = "STOP";
                        else if (Contains(helloRaw, HelloRun)) detected = "RUN";
                        if (detected == null)
                        {
                            Thread.Sleep(220);
                            continue;
                        }

                        Log("V159: HELLO confirmou " + detected + "; aguardando estabilizacao antes do F0.");
                        Thread.Sleep(500);

                        for (int f0Attempt = 1; f0Attempt <= 8; f0Attempt++)
                        {
                            int f0Timeout = f0Attempt <= 4 ? 1200 : 1800;
                            byte[] f0Raw = ExchangeRaw(port, F0, f0Timeout, 160,
                                "V159-ACQUIRE-" + (p + 1).ToString("00", CultureInfo.InvariantCulture)
                                + "-F0-" + f0Attempt.ToString(CultureInfo.InvariantCulture), "qualification");
                            if (Contains(f0Raw, f0Expected))
                            {
                                ActiveProfile = profile;
                                state = detected;
                                RememberProfileV159(portName, profile);
                                Log("V159: F0 confirmado na mesma sessao: 00 02 10 22 CB.");
                                return port;
                            }
                            Thread.Sleep(450);
                        }

                        byte[] rehello = ExchangeRaw(port, Hello, 1000, 120,
                            "V159-ACQUIRE-" + (p + 1).ToString("00", CultureInfo.InvariantCulture)
                            + "-REHELLO", "qualification");
                        if (!Contains(rehello, HelloStop) && !Contains(rehello, HelloRun)) break;
                        Thread.Sleep(650);
                    }
                }
                catch (Exception ex) { last = ex; }
                ClosePort(port);
                Thread.Sleep(400);
            }

            state = string.Empty;
            throw new IOException("Nenhum perfil confirmou HELLO+F0 na mesma sessao PG. "
                + (last == null ? string.Empty : last.Message));
        }

        private static void RememberProfileV159(string portName, PortProfile profile)
        {
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenLadderStudio");
                Directory.CreateDirectory(root);
                string path = Path.Combine(root, "tp02-pg-last-profile.txt");
                string content = "PORT=" + portName + Environment.NewLine
                    + "NAME=" + profile.Name + Environment.NewLine
                    + "DTR=" + (profile.Dtr ? "1" : "0") + Environment.NewLine
                    + "RTS=" + (profile.Rts ? "1" : "0") + Environment.NewLine;
                File.WriteAllText(path, content, Encoding.UTF8);
            }
            catch { }
        }

        private static byte[] ExchangeExpectedV159(SerialPort port, byte[] request, int expectedLen,
            int attempts, int timeoutMs, int quietMs, string label, string context)
        {
            byte[] last = new byte[0];
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                string oneLabel = label + "-TRY" + attempt.ToString(CultureInfo.InvariantCulture);
                last = ExchangeRaw(port, request, timeoutMs, quietMs, oneLabel, context);
                if (FindFrame(last, expectedLen) != null) return last;
                Thread.Sleep(attempt < 2 ? 350 : 650);
            }
            return last;
        }

'@
$text = $text.Substring(0,$idx) + $methods + $text.Substring($idx)

[IO.File]::WriteAllText($buildPath,$text,[Text.Encoding]::UTF8)
Write-Host 'TP02 Full Capture v1.59 aplicado: HELLO+F0 mesma sessao e retries PG38/PG34.'
