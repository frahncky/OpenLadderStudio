$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path (Get-Location) 'TP02FullProtocolCapture.cs'
$buildPath = Join-Path (Get-Location) 'TP02FullProtocolCapture.build.cs'
if (-not (Test-Path -LiteralPath $sourcePath)) { throw 'TP02FullProtocolCapture.cs nao encontrado.' }

# Build.bat pode restaurar o fonte por redirecionamento do git no Windows.
# Aceitamos UTF-8/UTF-16 (inclusive UTF-16 sem BOM) e eliminamos NUL residual.
[byte[]]$sourceBytes = [IO.File]::ReadAllBytes($sourcePath)
if ($sourceBytes.Length -ge 2 -and $sourceBytes[0] -eq 0xFF -and $sourceBytes[1] -eq 0xFE) {
    $text = [Text.Encoding]::Unicode.GetString($sourceBytes,2,$sourceBytes.Length-2)
}
elseif ($sourceBytes.Length -ge 2 -and $sourceBytes[0] -eq 0xFE -and $sourceBytes[1] -eq 0xFF) {
    $text = [Text.Encoding]::BigEndianUnicode.GetString($sourceBytes,2,$sourceBytes.Length-2)
}
elseif ($sourceBytes.Length -ge 4 -and $sourceBytes[1] -eq 0 -and $sourceBytes[3] -eq 0) {
    $text = [Text.Encoding]::Unicode.GetString($sourceBytes)
}
else {
    $text = [Text.Encoding]::UTF8.GetString($sourceBytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
}
$text = $text.Replace([string][char]0, '')

# Não dependemos mais de uma frase inteira byte-a-byte. O pipeline PT-BR pode
# normalizar textos/line endings; os tokens de C# abaixo permanecem estáveis.
$lines = @($text -split '\r?\n')
$patchedAcquire = $false
$patchedBaseF0 = $false
$patched38 = $false
$patched34 = $false
$insertAt = -1

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $indent = [regex]::Match($line, '^\s*').Value

    if (-not $patchedAcquire -and $line.Contains('port =') -and $line.Contains('AcquirePort') -and $line.Contains('out state')) {
        $lines[$i] = $line.Replace('AcquirePort','AcquireQualifiedPortV159')
        $patchedAcquire = $true
        continue
    }

    if (-not $patchedBaseF0 -and $line.Contains('CaptureF0') -and $line.Contains('BASE-F0')) {
        $lines[$i] = $indent + 'CaptureF0AlreadyQualifiedV159(port);'
        $patchedBaseF0 = $true
        continue
    }

    if (-not $patched38 -and $line.Contains('ExchangeRaw(port, Frame38') -and $line.Contains('program-read')) {
        $lines[$i] = $indent + 'ExchangeExpectedV159(port, Frame38, 2, 6, 3200, 220, prefix + "-38", "program-read");'
        $patched38 = $true
        continue
    }

    if (-not $patched34 -and $line.Contains('ExchangeRaw(port, request') -and $line.Contains('5000') -and $line.Contains('program-read')) {
        $lines[$i] = $indent + 'byte[] raw = ExchangeExpectedV159(port, request, Tp02Pg34Pager.PayloadLength, 4, 5500, 260, prefix + "-34-" + start.ToString("0000", CultureInfo.InvariantCulture), "program-read");'
        $patched34 = $true
        continue
    }

    if ($insertAt -lt 0 -and $line.Contains('private static List<PortProfile> BuildProfiles')) {
        $insertAt = $i
    }
}

if (-not $patchedAcquire) { throw 'V159: chamada principal AcquirePort nao localizada.' }
if (-not $patchedBaseF0) { throw 'V159: BASE-F0 nao localizado.' }
if (-not $patched38) { throw 'V159: chamada PG38 nao localizada.' }
if (-not $patched34) { throw 'V159: chamada PG34 nao localizada.' }
if ($insertAt -lt 0) { throw 'V159: BuildProfiles nao localizado.' }

$methods = @'
        private static void CaptureF0AlreadyQualifiedV159(SerialPort port)
        {
            // AcquireQualifiedPortV159 já confirmou F0 nesta mesma abertura da COM.
            // Não repetir F0 imediatamente evita o silêncio observado na bancada v1.58.
            if (port == null || !port.IsOpen) throw new InvalidOperationException("Sessao PG v1.59 nao esta aberta.");
        }

        // v1.59: HELLO isolado prova presença, mas não prova que a sessão PG aceita
        // os comandos seguintes. Só retornamos a porta após HELLO+F0 na mesma sessão.
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
                    Thread.Sleep(p == 0 ? 650 : 900);

                    for (int helloAttempt = 1; helloAttempt <= 8; helloAttempt++)
                    {
                        int helloTimeout = helloAttempt <= 4 ? 900 : 1300;
                        byte[] helloRaw = ExchangeRaw(port, Hello, helloTimeout, 140,
                            "V159-ACQUIRE-" + (p + 1).ToString("00", CultureInfo.InvariantCulture)
                            + "-HELLO-" + helloAttempt.ToString(CultureInfo.InvariantCulture), "session");

                        string detected = null;
                        if (Contains(helloRaw, HelloStop)) detected = "STOP";
                        else if (Contains(helloRaw, HelloRun)) detected = "RUN";
                        if (detected == null)
                        {
                            Thread.Sleep(250);
                            continue;
                        }

                        Log("V159: HELLO confirmou " + detected + "; estabilizando a mesma sessao antes do F0.");
                        Thread.Sleep(650);

                        for (int f0Attempt = 1; f0Attempt <= 8; f0Attempt++)
                        {
                            int f0Timeout = f0Attempt <= 4 ? 1400 : 2100;
                            byte[] f0Raw = ExchangeRaw(port, F0, f0Timeout, 180,
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
                            Thread.Sleep(500);
                        }

                        // HELLO sem F0 não qualifica o enlace. Uma última consulta na mesma
                        // abertura distingue sessão ainda viva de sessão que precisa reabrir.
                        byte[] rehello = ExchangeRaw(port, Hello, 1200, 140,
                            "V159-ACQUIRE-" + (p + 1).ToString("00", CultureInfo.InvariantCulture)
                            + "-REHELLO", "qualification");
                        if (!Contains(rehello, HelloStop) && !Contains(rehello, HelloRun)) break;
                        Thread.Sleep(750);
                    }
                }
                catch (Exception ex) { last = ex; }
                ClosePort(port);
                Thread.Sleep(500);
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
                Thread.Sleep(attempt == 1 ? 400 : 700);
            }
            return last;
        }
'@

$before = @($lines[0..($insertAt-1)])
$after = @($lines[$insertAt..($lines.Count-1)])
$patched = ($before -join "`r`n") + "`r`n" + $methods.TrimEnd() + "`r`n" + ($after -join "`r`n") + "`r`n"

if (-not $patched.Contains('AcquireQualifiedPortV159')) { throw 'V159: helper qualificado ausente no resultado.' }
if ($patched.Contains('port = AcquirePort(PortName, out state)')) { throw 'V159: chamada antiga AcquirePort ainda presente.' }
if ($patched.Contains('CaptureF0(port, "BASE-F0")')) { throw 'V159: BASE-F0 antigo ainda presente.' }

[IO.File]::WriteAllText($buildPath,$patched,(New-Object Text.UTF8Encoding($false)))
Write-Host 'TP02 Full Capture v1.59 aplicado por tokens de linha: HELLO+F0 mesma sessao e retries PG38/PG34.'
