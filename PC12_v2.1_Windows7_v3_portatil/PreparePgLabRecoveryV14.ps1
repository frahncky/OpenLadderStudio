$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02PgLab.build.cs'
if (-not (Test-Path $path)) { throw 'TP02PgLab.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$source, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($source.Contains($needleCrLf)) { return $source.Replace($needleCrLf, $replacementCrLf) }
    if ($source.Contains($needleLf)) { return $source.Replace($needleLf, $replacementLf) }
    throw "$label nao encontrado."
}

$text = Replace-Required $text @'
                                if (raw.Length == 0)
                                {
                                    LogEvent("RX", "[]", string.Empty, null, sw.ElapsedMilliseconds);
                                    Thread.Sleep(profile.interAttemptMs > 0 ? profile.interAttemptMs : 120);
                                    continue;
                                }
'@ @'
                                if (raw.Length == 0)
                                {
                                    LogEvent("RX", "[]", string.Empty, null, sw.ElapsedMilliseconds);

                                    if (attempt < attempts && (attempt % 2) == 0)
                                    {
                                        f0ValidatedInCurrentSession = false;
                                        if ((attempt % 4) == 0)
                                        {
                                            LogEvent("RECOVERY", "HANDSHAKE sem RX por " + attempt.ToString(CultureInfo.InvariantCulture) + " tentativas; reabrindo a porta serial sem TX adicional.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                            ReopenPortForHandshake(port, profile, 1200);
                                        }
                                        else
                                        {
                                            LogEvent("RECOVERY", "HANDSHAKE sem RX por " + attempt.ToString(CultureInfo.InvariantCulture) + " tentativas; rearmando DTR/RTS sem TX adicional.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                            RearmOpenPort(port, profile, 900);
                                        }
                                    }

                                    Thread.Sleep(profile.interAttemptMs > 0 ? profile.interAttemptMs : 120);
                                    continue;
                                }
'@ 'Recovery do HANDSHAKE principal'

$helper = @'
        private void ReopenPortForHandshake(SerialPort port, PgLabProfile profile, int delayMs)
        {
            try
            {
                if (port.IsOpen) port.Close();
                Thread.Sleep(500);
                port.DtrEnable = profile.dtr;
                port.RtsEnable = profile.rts;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();
                Thread.Sleep(delayMs > 0 ? delayMs : 1200);
            }
            catch (Exception ex)
            {
                LogEvent("RECOVERY", "falha ao reabrir a porta durante HANDSHAKE: " + ex.Message, string.Empty, null, 0);
                throw;
            }
        }

'@
$text = Replace-Required $text '        private void FinishRun(string result, string profile, string response)' ($helper + '        private void FinishRun(string result, string profile, string response)') 'Helper de reabertura da porta'

$text = Replace-Required $text '        private const string EngineVersion = "1.3";' '        private const string EngineVersion = "1.4";' 'EngineVersion 1.3'
$text = $text.Replace('motor 1.3 permite como candidato somente 38 00 C7', 'motor 1.4 permite como candidato somente 38 00 C7')
$text = $text.Replace('somente 38 00 C7 e aceito pelo motor 1.3', 'somente 38 00 C7 e aceito pelo motor 1.4')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab recovery 1.4 aplicado: rearme DTR/RTS e reabertura controlada durante NO_RX do HELLO.'
