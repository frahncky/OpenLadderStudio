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

# v1.17: a captura completa de 2026-09-10 confirmou 26 passos booleanos no
# primeiro bloco 34 e mostrou BRAW igual a soma dos quatro nibbles de HIGH/LOW
# em todos esses passos. O comando 38=...32... tambem coincidiu com o offset
# 2*(N-1) do ultimo par ativo. Esta versao adiciona SOMENTE decodificacao local
# dos bytes ja recebidos; nenhum novo TX, probe ou permissao serial e introduzido.

$text = Replace-Required $text @'
                                        LogEvent("ETAPA", "34 VALIDO: flags=0x" + frame34[0].ToString("X2", CultureInfo.InvariantCulture) + " LEN=0x" + payloadLen.ToString("X2", CultureInfo.InvariantCulture) + " (" + payloadLen.ToString(CultureInfo.InvariantCulture) + " bytes)", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                        Save34Dump(frame34);
'@ @'
                                        LogEvent("ETAPA", "34 VALIDO: flags=0x" + frame34[0].ToString("X2", CultureInfo.InvariantCulture) + " LEN=0x" + payloadLen.ToString("X2", CultureInfo.InvariantCulture) + " (" + payloadLen.ToString(CultureInfo.InvariantCulture) + " bytes)", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                        Log34Decode(frame34, frame38, totalWatch.ElapsedMilliseconds);
                                        Save34Dump(frame34);
'@ 'Chamada da decodificacao PG34'

$helper = @'
        private void Log34Decode(byte[] frame34, byte[] frame38, long elapsedMs)
        {
            OpenLadderStudio.Core.Tp02Pg34DecodeResult decoded =
                OpenLadderStudio.Core.Tp02Pg34Decoder.Decode(frame34, frame38);

            if (!decoded.IsValid)
            {
                LogEvent("DECOD34", "falha: " + decoded.Error, string.Empty, null, elapsedMs);
                return;
            }

            string source = decoded.StepCountFrom38Hint ? "38+cauda" : "cauda";
            string braw = decoded.BrawMismatches == 0
                ? "BRAW booleano OK " + decoded.BrawChecked.ToString(CultureInfo.InvariantCulture) + "/" + decoded.BrawChecked.ToString(CultureInfo.InvariantCulture)
                : "BRAW divergencias=" + decoded.BrawMismatches.ToString(CultureInfo.InvariantCulture)
                    + "/" + decoded.BrawChecked.ToString(CultureInfo.InvariantCulture);

            LogEvent(
                "DECOD34",
                "passos=" + decoded.StepCount.ToString(CultureInfo.InvariantCulture)
                    + " fonte=" + source
                    + "; booleanos=" + decoded.BooleanSteps.ToString(CultureInfo.InvariantCulture)
                    + "; desconhecidos=" + decoded.UnknownSteps.ToString(CultureInfo.InvariantCulture)
                    + "; " + braw,
                string.Empty,
                null,
                elapsedMs);

            if (decoded.HasFrame38StepCountHint)
            {
                LogEvent(
                    "DECOD34",
                    "38 hint ultimo-par=0x" + decoded.Frame38LastPairOffsetHint.ToString("X2", CultureInfo.InvariantCulture)
                        + "; cauda=" + decoded.PayloadTailStepCount.ToString(CultureInfo.InvariantCulture)
                        + "; coerente=" + (decoded.Frame38HintMatchesPayloadTail ? "sim" : "nao")
                        + (decoded.Frame38HintMatchesPayloadTail ? string.Empty : "; hint nao usado para truncar o payload"),
                    string.Empty,
                    null,
                    elapsedMs);
            }

            for (int i = 0; i < decoded.Steps.Count; i++)
            {
                OpenLadderStudio.Core.Tp02Pg34Step step = decoded.Steps[i];
                string integrity = step.BrawCheckApplicable
                    ? (step.BrawMatches
                        ? " BRAW=OK"
                        : " BRAW=DIVERGE esperado=" + step.ExpectedBraw.ToString("X2", CultureInfo.InvariantCulture))
                    : " BRAW=NAO-VALIDADO";

                LogEvent(
                    "IL34",
                    step.Index.ToString("000", CultureInfo.InvariantCulture)
                        + " H=" + step.High.ToString("X2", CultureInfo.InvariantCulture)
                        + " L=" + step.Low.ToString("X2", CultureInfo.InvariantCulture)
                        + " B=" + step.Braw.ToString("X2", CultureInfo.InvariantCulture)
                        + " -> " + step.ToIl()
                        + integrity,
                    string.Empty,
                    null,
                    elapsedMs);
            }
        }

'@

$text = Replace-Required $text '        private void Save34Dump(byte[] frame)' ($helper + '        private void Save34Dump(byte[] frame)') 'Metodo Log34Decode'

if (-not $text.Contains('        private const string EngineVersion = "1.16";')) { throw 'EngineVersion 1.16 nao encontrado apos 38StructuralV26.' }
$text = $text.Replace('        private const string EngineVersion = "1.16";', '        private const string EngineVersion = "1.17";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.17 aplicado: decode local do 34, IL booleana e verificacao BRAW; nenhum novo TX.'
