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

# v1.15: o log de bancada 2026-09-09 20:26 confirmou novamente HELLO-STOP e F0,
# mas o comando 38 devolveu um segundo vetor valido com checksum FF:
#   00 02 00 02 FB
# Alem do vetor observado anteriormente:
#   00 02 00 0A F3
# Como a semantica dos dois bytes de payload do 38 ainda nao esta decodificada,
# esta versao aceita SOMENTE esses dois vetores efetivamente observados em bancada.
# Isso evita generalizar o protocolo sem evidencia e permite prosseguir ao 34 quando
# qualquer um dos dois retornos conhecidos aparecer.

$text = Replace-Required $text @'
            byte[] good38 = ParseHex("00 02 00 0A F3");
'@ @'
            byte[] good38A = ParseHex("00 02 00 0A F3");
            byte[] good38B = ParseHex("00 02 00 02 FB");
'@ 'Vetores conhecidos do comando 38'

$text = Replace-Required $text @'
                                byte[] rx38 = CleanTxRx(port, cmd38, "38", 1600, totalWatch);
                                if (IndexOfSequence(rx38, good38) < 0)
                                {
                                    LogEvent("RETRY", "38 nao confirmou 00 02 00 0A F3; fechando a sessao sem enviar 34.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    retry = true;
                                }
                                else
                                {
                                    LogEvent("ETAPA", "38 VALIDADO: 00 02 00 0A F3", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    Thread.Sleep(120);
'@ @'
                                byte[] rx38 = CleanTxRx(port, cmd38, "38", 1600, totalWatch);
                                bool good38ASeen = IndexOfSequence(rx38, good38A) >= 0;
                                bool good38BSeen = IndexOfSequence(rx38, good38B) >= 0;
                                if (!good38ASeen && !good38BSeen)
                                {
                                    LogEvent("RETRY", "38 nao confirmou nenhum dos vetores observados em bancada (00 02 00 0A F3 / 00 02 00 02 FB); fechando a sessao sem enviar 34.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    retry = true;
                                }
                                else
                                {
                                    string valid38 = good38ASeen ? "00 02 00 0A F3" : "00 02 00 02 FB";
                                    LogEvent("ETAPA", "38 VALIDADO POR VETOR OBSERVADO: " + valid38, string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    Thread.Sleep(120);
'@ 'Validacao dos dois vetores observados do 38'

if (-not $text.Contains('        private const string EngineVersion = "1.14";')) { throw 'EngineVersion 1.14 nao encontrado apos HelloRetryV24.' }
$text = $text.Replace('        private const string EngineVersion = "1.14";', '        private const string EngineVersion = "1.15";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.15 aplicado: 38 aceita os dois vetores observados e pode prosseguir ao 34.'
