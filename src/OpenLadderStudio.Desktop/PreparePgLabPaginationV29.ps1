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

# v1.19: experimento isolado de paginacao do comando 34.
# Hipotese a validar em bancada: 34 03 [step_hi] [step_lo] A0 chk usa um contador
# de passos e o segundo bloco de 80 passos comeca em step=0x0050.
# O novo TX exato e: 34 03 00 50 A0 D8.
#
# Guardas:
# - exige a mesma autorizacao READ-ONLY manual do fluxo focado;
# - so ocorre depois de HELLO-STOP, F0 valido, 38 estrutural e pagina 0 valida;
# - so e tentado quando 38.payload[1] >= 0xA0, isto e, quando a hipotese atual
#   sugere que a cauda do programa ultrapassa os primeiros 80 passos;
# - envia NO MAXIMO UMA vez a pagina 1 por execucao, sem retentativa automatica;
# - nao acrescenta escrita, download, erase, firmware ou RUN/STOP remoto.
#
# A pagina 1 e salva bruta para analise. O decoder nao e estendido ainda para
# baseStep=80: primeiro queremos confirmar fisicamente a geometria/paginacao.

$text = Replace-Required $text @'
            byte[] cmd34 = ParseHex("34 03 00 00 A0 28");
'@ @'
            byte[] cmd34 = ParseHex("34 03 00 00 A0 28");
            byte[] cmd34Page1 = ParseHex("34 03 00 50 A0 D8");
'@ 'Comando da pagina 1 do 34'

$text = Replace-Required $text @'
                                        LogEvent("ETAPA", "34 VALIDO: flags=0x" + frame34[0].ToString("X2", CultureInfo.InvariantCulture) + " LEN=0x" + payloadLen.ToString("X2", CultureInfo.InvariantCulture) + " (" + payloadLen.ToString(CultureInfo.InvariantCulture) + " bytes)", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                        Log34Decode(frame34, frame38, totalWatch.ElapsedMilliseconds);
                                        Save34Dump(frame34);
                                        FinishRun("SUCCESS", DescribeProfile(profile), ToHex(frame34));
                                        return;
'@ @'
                                        LogEvent("ETAPA", "34 VALIDO: flags=0x" + frame34[0].ToString("X2", CultureInfo.InvariantCulture) + " LEN=0x" + payloadLen.ToString("X2", CultureInfo.InvariantCulture) + " (" + payloadLen.ToString(CultureInfo.InvariantCulture) + " bytes)", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                        Log34Decode(frame34, frame38, totalWatch.ElapsedMilliseconds);
                                        Save34Dump(frame34);

                                        int hintLastPair = frame38 != null && frame38.Length == 5 ? frame38[3] : -1;
                                        if (hintLastPair < 0xA0)
                                        {
                                            LogEvent("PAG34", "pagina 1 nao solicitada: 38.payload[1]=0x" + hintLastPair.ToString("X2", CultureInfo.InvariantCulture) + " nao sugere ultrapassar os primeiros 80 passos.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                            FinishRun("SUCCESS", DescribeProfile(profile), ToHex(frame34));
                                            return;
                                        }

                                        if (cmd34Page1.Length != 6
                                            || cmd34Page1[0] != 0x34
                                            || cmd34Page1[1] != 0x03
                                            || cmd34Page1[2] != 0x00
                                            || cmd34Page1[3] != 0x50
                                            || cmd34Page1[4] != 0xA0
                                            || Sum8(cmd34Page1) != 0xFF)
                                        {
                                            LogEvent("BLOQUEIO", "quadro experimental da pagina 1 falhou na guarda interna; nenhum TX adicional.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                            FinishRun("PAG34_INTERNAL_GUARD", DescribeProfile(profile), ToHex(frame34));
                                            return;
                                        }

                                        LogEvent("PAG34", "38.payload[1]=0x" + hintLastPair.ToString("X2", CultureInfo.InvariantCulture) + "; enviando UMA leitura experimental da pagina 1 em step=0x0050: 34 03 00 50 A0 D8.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                        Thread.Sleep(120);

                                        byte[] rx34Page1 = CleanTxRx(port, cmd34Page1, "34 PAGE1 step=0x0050", 3200, totalWatch);
                                        byte[] frame34Page1;
                                        if (TryExtractPgLengthFrame(rx34Page1, out frame34Page1)
                                            && frame34Page1 != null
                                            && frame34Page1.Length == 0xF0 + 3
                                            && frame34Page1[1] == 0xF0)
                                        {
                                            LogEvent("PAG34", "pagina 1 VALIDA: flags=0x" + frame34Page1[0].ToString("X2", CultureInfo.InvariantCulture) + " LEN=F0 checksum=FF; resposta preservada sem inferir semantica.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                            Save34PageDump(frame34Page1);
                                            FinishRun("SUCCESS_PAG34_PAGE1", DescribeProfile(profile), ToHex(frame34Page1));
                                            return;
                                        }

                                        LogEvent("PAG34", "pagina 1 sem quadro LEN=F0/checksum FF valido. A sonda nao sera repetida nesta execucao.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                        FinishRun("PAG34_PAGE1_NO_VALID_FRAME", DescribeProfile(profile), ToHex(rx34Page1));
                                        return;
'@ 'Sonda unica e condicional da pagina 1'

$helper = @'
        private void Save34PageDump(byte[] frame)
        {
            if (frame == null || frame.Length < 3) return;
            try
            {
                string dir = GetReportsDirectory();
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                string framePath = Path.Combine(dir, "TP02-PG-34-page1-step0050-frame-" + stamp + ".bin");
                string payloadPath = Path.Combine(dir, "TP02-PG-34-page1-step0050-payload-" + stamp + ".bin");
                File.WriteAllBytes(framePath, frame);
                int payloadLen = frame[1];
                byte[] payload = new byte[payloadLen];
                if (payloadLen > 0) Buffer.BlockCopy(frame, 2, payload, 0, payloadLen);
                File.WriteAllBytes(payloadPath, payload);
                LogEvent("DUMP", "34 pagina 1 salva: " + framePath + " | payload: " + payloadPath, string.Empty, null, 0);
            }
            catch (Exception ex)
            {
                LogEvent("DUMP", "falha ao salvar pagina 1 do 34: " + ex.Message, string.Empty, null, 0);
            }
        }

'@

$text = Replace-Required $text '        private void RunFullTest(string portName, bool readOnlyApproved)' ($helper + '        private void RunFullTest(string portName, bool readOnlyApproved)') 'Helper de dump da pagina 1'

if (-not $text.Contains('        private const string EngineVersion = "1.18";')) { throw 'EngineVersion 1.18 nao encontrado apos MixedDecodeV28.' }
$text = $text.Replace('        private const string EngineVersion = "1.18";', '        private const string EngineVersion = "1.19";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.19 aplicado: sonda unica, condicional e READ-ONLY da pagina 1 do 34 em step=0x0050.'
