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

# v1.12: decodificar a resposta de cada endereco da varredura 0A no proprio relatorio.
# A resposta segue CMD=00 LEN payload; a linha DECOD mostra o LEN e quantos bytes do
# payload sao nao-zero, para o operador ver de imediato quais enderecos guardam dados.
# Somente leitura de bytes ja recebidos; nao transmite nada.

# 1) chamada da decodificacao logo apos registrar o RX RAW de cada endereco
$text = Replace-Required $text @'
                    RecordFrame("RX RAW", step.name + " " + addrHex, noEcho, sw.ElapsedMilliseconds);
'@ @'
                    RecordFrame("RX RAW", step.name + " " + addrHex, noEcho, sw.ElapsedMilliseconds);
                    LogRead0ADecode(noEcho, addrHex, sw.ElapsedMilliseconds);
'@ 'Chamada da decodificacao 0A'

# 2) metodo de decodificacao, inserido antes de BuildRead0AFrame
$text = Replace-Required $text @'
        private static byte[] BuildRead0AFrame(int addr, int readLen)
'@ @'
        private void LogRead0ADecode(byte[] frame, string addrHex, long elapsedMs)
        {
            if (frame == null || frame.Length < 3 || frame[0] != 0x00) return;
            int len = frame[1];
            int avail = frame.Length - 3;
            if (len > avail) len = avail;
            int nonZero = 0;
            for (int i = 0; i < len; i++) if (frame[2 + i] != 0x00) nonZero++;
            LogEvent("DECOD", addrHex + ": CMD=00 LEN=0x" + len.ToString("X2", CultureInfo.InvariantCulture) + " (" + len.ToString(CultureInfo.InvariantCulture) + " bytes) nao-zero=" + nonZero.ToString(CultureInfo.InvariantCulture), string.Empty, null, elapsedMs);
        }

        private static byte[] BuildRead0AFrame(int addr, int readLen)
'@ 'Metodo LogRead0ADecode'

# 3) versao do motor
if (-not $text.Contains('        private const string EngineVersion = "1.11";')) { throw 'EngineVersion 1.11 nao encontrado apos ReadSweepV21.' }
$text = $text.Replace('        private const string EngineVersion = "1.11";', '        private const string EngineVersion = "1.12";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.12 aplicado: decodificacao CMD=00/LEN por endereco da varredura 0A.'
