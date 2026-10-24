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

# v1.10: a bancada mostrou o HELLO respondendo (80 01 09 75) enquanto o F0 ficou mudo nas
# 6 variantes da matriz. Como o TP02 ignora sequencias de tentativas antes de responder
# (o proprio HELLO so saiu apos reabrir a porta), 6 disparos unicos do F0 sao poucos.
# Aqui a matriz F0 passa a ser repetida ate 3 rodadas na mesma sessao, parando assim que
# uma variante devolver o vetor fisico conhecido. Nenhum opcode novo e introduzido: cada
# rodada transmite somente F0 00 0F, ja READ_ONLY_VERIFIED. Sem F0 validado, o 38 nao abre.

$text = Replace-Required $text @'
                if (NormalizeHex(step.txHex) == "F0 00 0F")
                {
                    RunF0PostHandshakeMatrix(port, step, totalWatch);
                    continue;
                }
'@ @'
                if (NormalizeHex(step.txHex) == "F0 00 0F")
                {
                    int f0Rounds = 3;
                    for (int f0Round = 1; f0Round <= f0Rounds && !cancelRequested; f0Round++)
                    {
                        LogEvent("POST-HS", "rodada F0 " + f0Round.ToString(CultureInfo.InvariantCulture) + "/" + f0Rounds.ToString(CultureInfo.InvariantCulture) + " - matriz de 6 variantes por rodada", string.Empty, null, totalWatch.ElapsedMilliseconds);
                        if (RunF0PostHandshakeMatrix(port, step, totalWatch)) break;
                        if (f0Round < f0Rounds && !cancelRequested) Thread.Sleep(400);
                    }
                    continue;
                }
'@ 'Retentativa da matriz F0'

if (-not $text.Contains('        private const string EngineVersion = "1.9";')) { throw 'EngineVersion 1.9 nao encontrado apos ContinuousResearchV19.' }
$text = $text.Replace('        private const string EngineVersion = "1.9";', '        private const string EngineVersion = "1.10";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.10 aplicado: matriz F0 repetida ate 3 rodadas na mesma sessao.'
