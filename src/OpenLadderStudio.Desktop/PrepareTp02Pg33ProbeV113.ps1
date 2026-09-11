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

# v1.15: voltar ao handshake que ja funcionou fisicamente na v1.10.
# Nao enviar HELLO adicional depois de um HELLO valido e nao ressincronizar
# HELLO entre tentativas de F0. Se uma sessao falhar antes do PG33, fecha-se a
# COM e tenta-se uma nova sessao limpa.

# 1) Preflight de escrita: um warm-up inicial e ate 5 sessoes limpas.
# O PG33 continua podendo ser transmitido no maximo UMA vez.
$executePattern = '(?s)        private byte\[\] ExecutePg33\(string portName, byte\[\] frame\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private static void ValidateKnownProbeProgram)'
$executeReplacement = @'
        private byte[] ExecutePg33(string portName, byte[] frame)
        {
            Exception last = null;

            // Mesmo condicionamento do leitor PG v1.10: warm-up uma unica vez,
            // seguido por sessoes seriais limpas.
            WarmUpLink(portName, "write-preflight");

            for (int session = 1; session <= 5; session++)
            {
                SerialPort port = null;
                bool pg33Attempted = false;
                try
                {
                    if (session > 1)
                    {
                        AppendLogSafe("write-preflight PG AUTO-RETRY: preparando sessao "
                            + session.ToString(CultureInfo.InvariantCulture) + ".");
                        Thread.Sleep(session == 2 ? 1500 : 2000);
                    }

                    port = OpenPort(portName);
                    int settle = session == 1 ? 1600 : (session == 2 ? 1900 : 2300);
                    Thread.Sleep(settle);
                    AppendLogSafe("write-preflight COM aberta | sessao "
                        + session.ToString(CultureInfo.InvariantCulture)
                        + " | estabilizacao " + settle.ToString(CultureInfo.InvariantCulture) + " ms.");

                    string state = PerformHello(port);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC saiu de STOP antes do PG33. Escrita bloqueada.");
                    Thread.Sleep(450);

                    PerformF0(port);
                    Thread.Sleep(420);

                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "write-preflight-38-s" + session.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(450);

                    // A partir deste ponto NAO existe retry automatico de escrita.
                    port.DiscardInBuffer();
                    AppendLogSafe("PG33 TX UNICA: " + ToHex(frame));
                    pg33Attempted = true;
                    port.Write(frame, 0, frame.Length);

                    byte[] raw = ReadBurst(port, 6500, 350);
                    AppendLogSafe("PG33 RX UNICA: " + (raw.Length == 0 ? "[]" : ToHex(raw)));

                    byte[] valid = FindFirstValidResponseFrame(raw);
                    if (valid != null)
                        AppendLogSafe("PG33 resposta estruturalmente valida: " + ToHex(valid));
                    else
                        AppendLogSafe("PG33 foi transmitido, mas nenhum ACK valido foi confirmado. "
                            + "Por seguranca, nao havera retransmissao automatica.");

                    return raw;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("write-preflight sessao "
                        + session.ToString(CultureInfo.InvariantCulture) + " falhou: " + ex.Message);

                    if (pg33Attempted)
                    {
                        throw new IOException(
                            "O PG33 pode ter sido transmitido. A operacao NAO sera repetida automaticamente. "
                            + "Erro: " + ex.Message, ex);
                    }

                    if (session < 5)
                        AppendLogSafe("write-preflight: nenhuma escrita foi feita; abrindo nova sessao limpa.");
                }
                finally
                {
                    ClosePort(port);
                }
            }

            throw new IOException(
                "Preflight PG33 falhou nas 5 sessoes antes de qualquer transmissao 0x33. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

'@
$shell = Replace-RegexOnce $shell $executePattern $executeReplacement 'ExecutePg33'

# 2) Snapshot inicial: manter warm-up v1.10 do V111, mas permitir ate 5 sessoes.
$snapshotPattern = '(?s)(        private ProgramSnapshot ReadSnapshotRobust\(string portName, string tag\).*?for \(int session = 1; session <= )3(; session\+\+\))'
$snapshotMatch = [System.Text.RegularExpressions.Regex]::Matches($shell, $snapshotPattern)
if ($snapshotMatch.Count -eq 1) {
    $shell = [System.Text.RegularExpressions.Regex]::Replace($shell, $snapshotPattern, '${1}5${2}', 1)
}
elseif ($snapshotMatch.Count -ne 0) {
    throw "ReadSnapshotRobust loop ambiguo; encontrado: $($snapshotMatch.Count)."
}

# 3) HELLO exatamente no estilo do leitor v1.10 validado fisicamente.
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

# 4) F0 exatamente no estilo do leitor v1.10: sem HELLO extra entre tentativas.
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

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Probe V115 aplicado: handshake v1.10 restaurado, 5 sessoes limpas e PG33 transmitido no maximo uma vez.'
