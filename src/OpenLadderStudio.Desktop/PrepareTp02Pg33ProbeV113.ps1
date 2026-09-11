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

# 1) Preflight de escrita: pode recuperar sessoes antes do primeiro 0x33,
# mas nunca retransmite automaticamente depois que o PG33 foi tentado.
$executePattern = '(?s)        private byte\[\] ExecutePg33\(string portName, byte\[\] frame\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private static void ValidateKnownProbeProgram)'
$executeReplacement = @'
        private byte[] ExecutePg33(string portName, byte[] frame)
        {
            Exception last = null;

            // O TP-232PG pode perder o estado de enlace ao fechar/reabrir a COM.
            // O preflight pode abrir ate 5 sessoes independentes, mas somente
            // ANTES da primeira transmissao 0x33.
            for (int session = 1; session <= 5; session++)
            {
                SerialPort port = null;
                bool pg33Attempted = false;
                try
                {
                    AppendLogSafe("write-preflight: preparando sessao "
                        + session.ToString(CultureInfo.InvariantCulture) + " de 5.");

                    WarmUpLink(portName, "write-preflight-s" + session.ToString(CultureInfo.InvariantCulture));
                    port = OpenPort(portName);

                    int settle = 1800 + ((session - 1) * 400);
                    Thread.Sleep(settle);
                    AppendLogSafe("write-preflight COM aberta | sessao "
                        + session.ToString(CultureInfo.InvariantCulture)
                        + " | estabilizacao " + settle.ToString(CultureInfo.InvariantCulture) + " ms.");

                    string state = PerformHello(port);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC saiu de STOP antes do PG33. Escrita bloqueada.");
                    Thread.Sleep(700);

                    PerformF0(port);
                    Thread.Sleep(600);

                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 4200,
                        "write-preflight-38-s" + session.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(650);

                    // A partir deste ponto NAO ha retry de sessao automatico.
                    port.DiscardInBuffer();
                    AppendLogSafe("PG33 TX UNICA: " + ToHex(frame));
                    pg33Attempted = true;
                    port.Write(frame, 0, frame.Length);

                    byte[] raw = ReadBurst(port, 7000, 380);
                    AppendLogSafe("PG33 RX UNICA: " + (raw.Length == 0 ? "[]" : ToHex(raw)));

                    byte[] valid = FindFirstValidResponseFrame(raw);
                    if (valid != null)
                    {
                        AppendLogSafe("PG33 resposta estruturalmente valida: " + ToHex(valid));
                    }
                    else
                    {
                        AppendLogSafe("PG33 foi transmitido, mas nenhum ACK valido foi confirmado. "
                            + "Por seguranca, nao havera retransmissao automatica.");
                    }
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
                    {
                        AppendLogSafe("write-preflight: nenhuma escrita foi feita; tentando nova sessao automaticamente.");
                        Thread.Sleep(session <= 2 ? 1800 : 2600);
                    }
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

# 2) Snapshot inicial: ampliar as sessoes automaticas de 3 para 5.
$snapshotPattern = '(?s)(        private ProgramSnapshot ReadSnapshotRobust\(string portName, string tag\).*?for \(int session = 1; session <= )3(; session\+\+\))'
$snapshotMatch = [System.Text.RegularExpressions.Regex]::Matches($shell, $snapshotPattern)
if ($snapshotMatch.Count -ne 1) { throw "ReadSnapshotRobust loop esperado uma vez; encontrado: $($snapshotMatch.Count)." }
$shell = [System.Text.RegularExpressions.Regex]::Replace($shell, $snapshotPattern, '${1}5${2}', 1)

# 3) HELLO: quando finalmente responde, dar um pulso de confirmacao extra antes
# de prosseguir. O segundo pulso e somente leitura/handshake e nao bloqueia se ficar mudo.
$helloPattern = '(?s)        private string PerformHello\(SerialPort port\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private void PerformF0)'
$helloReplacement = @'
        private string PerformHello(SerialPort port)
        {
            for (int attempt = 1; attempt <= 8; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(HelloRequest, 0, HelloRequest.Length);
                byte[] raw = ReadBurst(port, attempt == 1 ? 2800 : 3200, 250);
                AppendLogSafe("HELLO tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));

                string state = null;
                if (Contains(raw, HelloStop)) state = "STOP";
                else if (Contains(raw, HelloRun)) state = "RUN";

                if (!string.IsNullOrEmpty(state))
                {
                    AppendLogSafe("HELLO valido; executando confirmacao extra de enlace antes do F0.");
                    Thread.Sleep(500);
                    port.DiscardInBuffer();
                    port.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] confirm = ReadBurst(port, 2400, 250);
                    AppendLogSafe("HELLO confirmacao RX="
                        + (confirm.Length == 0 ? "[]" : ToHex(confirm)));
                    Thread.Sleep(700);
                    return state;
                }
                Thread.Sleep(400);
            }
            throw new TimeoutException("HELLO PG nao confirmado.");
        }

'@
$shell = Replace-RegexOnce $shell $helloPattern $helloReplacement 'PerformHello'

# 4) F0: se ficar mudo, ressincronizar com HELLO antes do proximo F0.
# Nenhum comando de escrita e usado nesta recuperacao.
$f0Pattern = '(?s)        private void PerformF0\(SerialPort port\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private byte\[\] SendAndReadFrame)'
$f0Replacement = @'
        private void PerformF0(SerialPort port)
        {
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                port.DiscardInBuffer();
                port.Write(F0Request, 0, F0Request.Length);
                byte[] raw = ReadBurst(port, 3900, 270);
                AppendLogSafe("F0 tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));
                if (Contains(raw, F0Response)) return;

                if (attempt < 6)
                {
                    AppendLogSafe("F0 sem resposta valida; ressincronizando com HELLO antes do proximo F0.");
                    Thread.Sleep(450);
                    port.DiscardInBuffer();
                    port.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] sync = ReadBurst(port, 2500, 250);
                    AppendLogSafe("F0 RESYNC HELLO RX="
                        + (sync.Length == 0 ? "[]" : ToHex(sync)));
                    Thread.Sleep(650);
                }
            }
            throw new InvalidDataException("F0 nao retornou 00 02 10 22 CB apos ressincronizacao HELLO.");
        }

'@
$shell = Replace-RegexOnce $shell $f0Pattern $f0Replacement 'PerformF0'

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Probe V114 aplicado: HELLO confirmacao, F0 resync, 5 sessoes antes do PG33 e escrita unica.'
