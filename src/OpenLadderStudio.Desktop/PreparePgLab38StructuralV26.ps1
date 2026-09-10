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

# v1.16: novo log de bancada com dois contatos em serie mostrou o terceiro retorno
# valido do comando 38:
#   00 02 00 04 F9
# Os retornos 00 02 00 02 FB, 00 02 00 04 F9 e 00 02 00 0A F3 compartilham
# a mesma estrutura: FLAGS=00, LEN=02, payload[0]=00 e checksum FF. O segundo byte
# do payload varia com o programa, portanto nao deve mais ser tratado como constante.
# Esta versao valida o 38 estruturalmente, sem atribuir semantica ao valor variavel.
# O fluxo permanece estritamente READ-ONLY e o 34 so e enviado apos HELLO-STOP,
# F0 valido e quadro 38 estruturalmente valido.

$text = Replace-Required $text @'
            byte[] good38A = ParseHex("00 02 00 0A F3");
            byte[] good38B = ParseHex("00 02 00 02 FB");
'@ @'
'@ 'Remocao dos vetores fixos do 38'

$text = Replace-Required $text @'
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
'@ @'
                                byte[] rx38 = CleanTxRx(port, cmd38, "38", 1600, totalWatch);
                                byte[] frame38;
                                byte[] payload38;
                                bool frame38Ok = TryExtractPgLengthFrame(rx38, out frame38, out payload38)
                                    && frame38 != null
                                    && frame38.Length == 5
                                    && frame38[0] == 0x00
                                    && frame38[1] == 0x02
                                    && payload38 != null
                                    && payload38.Length == 2
                                    && payload38[0] == 0x00;
                                if (!frame38Ok)
                                {
                                    LogEvent("RETRY", "38 sem quadro estrutural valido (esperado FLAGS=00 LEN=02 payload[0]=00 checksum FF); fechando a sessao sem enviar 34.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    retry = true;
                                }
                                else
                                {
                                    string valid38 = ToHex(frame38);
                                    LogEvent("ETAPA", "38 VALIDADO ESTRUTURALMENTE: " + valid38 + " | valor_variavel=0x" + payload38[1].ToString("X2", CultureInfo.InvariantCulture), string.Empty, null, totalWatch.ElapsedMilliseconds);
                                    Thread.Sleep(120);
'@ 'Validacao estrutural do 38'

if (-not $text.Contains('        private const string EngineVersion = "1.15";')) { throw 'EngineVersion 1.15 nao encontrado apos 38VariantsV25.' }
$text = $text.Replace('        private const string EngineVersion = "1.15";', '        private const string EngineVersion = "1.16";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.16 aplicado: 38 validado por estrutura FLAGS/LEN/payload/checksum, sem fixar o valor variavel.'
