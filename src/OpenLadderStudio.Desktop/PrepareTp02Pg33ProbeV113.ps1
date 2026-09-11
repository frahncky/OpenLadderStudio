$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)

$pattern = '(?s)        private byte\[\] ExecutePg33\(string portName, byte\[\] frame\)\s*\{.*?\r?\n        \}\r?\n\r?\n(?=        private static void ValidateKnownProbeProgram)'
$matches = [System.Text.RegularExpressions.Regex]::Matches($shell, $pattern)
if ($matches.Count -ne 1) {
    throw "ExecutePg33 esperado exatamente uma vez; encontrado: $($matches.Count)."
}

$replacement = @'
        private byte[] ExecutePg33(string portName, byte[] frame)
        {
            Exception last = null;

            // O TP-232PG pode perder o estado de enlace ao fechar/reabrir a COM.
            // Por isso o preflight pode abrir ate 3 sessoes independentes.
            // IMPORTANTE: retries de sessao so acontecem ANTES do primeiro 0x33.
            for (int session = 1; session <= 3; session++)
            {
                SerialPort port = null;
                bool pg33Attempted = false;
                try
                {
                    AppendLogSafe("write-preflight: preparando sessao "
                        + session.ToString(CultureInfo.InvariantCulture) + " de 3.");

                    WarmUpLink(portName, "write-preflight-s" + session.ToString(CultureInfo.InvariantCulture));
                    port = OpenPort(portName);

                    int settle = session == 1 ? 1800 : (session == 2 ? 2200 : 2600);
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

                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3800,
                        "write-preflight-38-s" + session.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(500);

                    // A partir deste ponto NAO ha mais retry de sessao automatico.
                    // Mesmo um ACK ausente e tratado como resultado inconclusivo,
                    // pois o PLC pode ter aceitado o quadro e perdido somente a resposta.
                    port.DiscardInBuffer();
                    AppendLogSafe("PG33 TX UNICA: " + ToHex(frame));
                    pg33Attempted = true;
                    port.Write(frame, 0, frame.Length);

                    byte[] raw = ReadBurst(port, 6500, 350);
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

                    if (session < 3)
                    {
                        AppendLogSafe("write-preflight: nenhuma escrita foi feita; tentando nova sessao automaticamente.");
                        Thread.Sleep(session == 1 ? 1600 : 2200);
                    }
                }
                finally
                {
                    ClosePort(port);
                }
            }

            throw new IOException(
                "Preflight PG33 falhou nas 3 sessoes antes de qualquer transmissao 0x33. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

'@

$shell = [System.Text.RegularExpressions.Regex]::Replace($shell, $pattern, $replacement, 1)
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Probe V113 aplicado: preflight robusto em 3 sessoes, mas PG33 transmitido no maximo uma vez.'
