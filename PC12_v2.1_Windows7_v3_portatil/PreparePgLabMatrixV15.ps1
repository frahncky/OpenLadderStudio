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
                            port = OpenPort(portName, profile);
                            LogEvent("PERFIL", DescribeProfile(profile), string.Empty, null, totalWatch.ElapsedMilliseconds);
                            int attempts = profile.attempts <= 0 ? 1 : profile.attempts;
'@ @'
                            port = OpenPort(portName, profile);
                            LogEvent("PERFIL", DescribeProfile(profile), string.Empty, null, totalWatch.ElapsedMilliseconds);

                            byte[] preTx = ReadBurst(port, 500, 120);
                            if (preTx.Length == 0)
                                LogEvent("PASSIVO PRE-TX", "nenhum byte espontaneo em 500 ms antes do primeiro HELLO.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            else
                            {
                                RecordFrame("PASSIVO PRE-TX RX", "bytes presentes antes do primeiro HELLO", preTx, totalWatch.ElapsedMilliseconds);
                                foreach (byte[] f in DiscoverChecksumFrames(preTx))
                                    RecordFrame("FRAME FF", "quadro encontrado antes do primeiro HELLO", f, totalWatch.ElapsedMilliseconds);
                            }

                            int attempts = profile.attempts <= 0 ? 1 : profile.attempts;
'@ 'Escuta passiva pre-TX por perfil'

$text = Replace-Required $text '        private const string EngineVersion = "1.4";' '        private const string EngineVersion = "1.5";' 'EngineVersion 1.4'

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.5 aplicado: escuta passiva pre-TX e matriz de perfis controlada pelo pacote.'
