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

# v1.20: paginação genérica do comando 34 em modo estritamente READ-ONLY.
#
# Evidência estática do PC12 + geometria física já confirmada:
#   34 03 [step_hi] [step_lo] A0 chk
#   80 passos por página; endereço é contador de passos.
#
# Estratégia:
# - página 0 continua sendo lida somente após HELLO-STOP, F0 e 38 válidos;
# - o 38 passa a ser somente metadado/hint e NÃO decide a paginação;
# - se a página atual não contém F-00 END (00 70), a próxima começa em +80;
# - cada página dinâmica é transmitida uma única vez, sem retentativa;
# - parada obrigatória ao encontrar END ou ao alcançar o limite de 4000 passos;
# - todo TX dinâmico passa por guarda local: 34 03, A0 e checksum FF.
#
# Nenhuma escrita, download, erase, firmware ou RUN/STOP remoto é adicionada.

# Remove a variável experimental fixa de v1.19; a página passa a ser construída
# pelo helper Tp02Pg34Pager a partir do contador inicial de passos.
$text = Replace-Required $text @'
            byte[] cmd34 = ParseHex("34 03 00 00 A0 28");
            byte[] cmd34Page1 = ParseHex("34 03 00 50 A0 D8");
'@ @'
            byte[] cmd34 = ParseHex("34 03 00 00 A0 28");
'@ 'Remocao do comando fixo da pagina 1'

$text = Replace-Required $text @'
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
'@ @'
                                        RunGeneric34Pages(port, frame34, frame38, profile, totalWatch);
                                        return;
'@ 'Substituicao da sonda fixa pela paginacao generica'

$text = Replace-Required $text @'
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

'@ @'
        private void RunGeneric34Pages(SerialPort port, byte[] firstPage, byte[] frame38, PgLabProfile profile, Stopwatch totalWatch)
        {
            int endLocal;
            if (OpenLadderStudio.Core.Tp02Pg34Pager.TryFindEnd(firstPage, out endLocal))
            {
                LogEvent("PAG34", "F-00 END encontrado na pagina 0: passo local/global=" + endLocal.ToString(CultureInfo.InvariantCulture) + ". Nenhuma pagina adicional sera lida.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                FinishRun("SUCCESS_PAG34_END", DescribeProfile(profile), ToHex(firstPage));
                return;
            }

            if (frame38 != null && frame38.Length == 5)
            {
                LogEvent("PAG34", "38.payload[1]=0x" + frame38[3].ToString("X2", CultureInfo.InvariantCulture) + " preservado apenas como hint; a parada da paginacao e determinada por F-00 END ou pelo limite de 4000 passos.", string.Empty, null, totalWatch.ElapsedMilliseconds);
            }

            int startStep = OpenLadderStudio.Core.Tp02Pg34Pager.StepsPerPage;
            int pageNumber = 1;
            while (startStep < OpenLadderStudio.Core.Tp02Pg34Pager.MaxProgramSteps && !cancelRequested)
            {
                byte[] tx = OpenLadderStudio.Core.Tp02Pg34Pager.BuildReadRequest(startStep);
                if (!IsSafeProgramRead34Request(tx, startStep))
                {
                    LogEvent("BLOQUEIO", "guarda interna rejeitou a pagina dinamica em step=0x" + startStep.ToString("X4", CultureInfo.InvariantCulture) + "; nenhum TX adicional.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                    FinishRun("PAG34_INTERNAL_GUARD", DescribeProfile(profile), ToHex(tx));
                    return;
                }

                LogEvent("PAG34", "pagina " + pageNumber.ToString(CultureInfo.InvariantCulture) + " READ-ONLY; startStep=0x" + startStep.ToString("X4", CultureInfo.InvariantCulture) + "; TX=" + ToHex(tx) + "; uma tentativa.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                Thread.Sleep(120);

                byte[] rx = CleanTxRx(port, tx, "34 PAGE start=0x" + startStep.ToString("X4", CultureInfo.InvariantCulture), 3200, totalWatch);
                byte[] page;
                if (!TryExtractPgLengthFrame(rx, out page) || !OpenLadderStudio.Core.Tp02Pg34Pager.IsValidPageFrame(page))
                {
                    LogEvent("PAG34", "pagina em step=0x" + startStep.ToString("X4", CultureInfo.InvariantCulture) + " sem quadro LEN=F0/checksum FF valido. Sem retentativa automatica.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                    FinishRun("PAG34_PAGE_NO_VALID_FRAME", DescribeProfile(profile), ToHex(rx));
                    return;
                }

                LogEvent("PAG34", "pagina " + pageNumber.ToString(CultureInfo.InvariantCulture) + " VALIDA: startStep=0x" + startStep.ToString("X4", CultureInfo.InvariantCulture) + " flags=0x" + page[0].ToString("X2", CultureInfo.InvariantCulture) + " LEN=F0 checksum=FF.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                Save34PageDump(page, startStep);
                Log34Decode(page, null, totalWatch.ElapsedMilliseconds);

                if (OpenLadderStudio.Core.Tp02Pg34Pager.TryFindEnd(page, out endLocal))
                {
                    int globalStep = OpenLadderStudio.Core.Tp02Pg34Pager.GlobalStep(startStep, endLocal);
                    LogEvent("PAG34", "F-00 END encontrado: pagina=" + pageNumber.ToString(CultureInfo.InvariantCulture) + " local=" + endLocal.ToString(CultureInfo.InvariantCulture) + " global=" + globalStep.ToString(CultureInfo.InvariantCulture) + ". Leitura concluida.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                    FinishRun("SUCCESS_PAG34_END", DescribeProfile(profile), ToHex(page));
                    return;
                }

                startStep = OpenLadderStudio.Core.Tp02Pg34Pager.NextStartStep(startStep);
                pageNumber++;
            }

            if (cancelRequested)
            {
                FinishRun("CANCELLED", DescribeProfile(profile), string.Empty);
                return;
            }

            LogEvent("PAG34", "F-00 END nao encontrado antes do limite de 4000 passos; leitura encerrada sem extrapolar a memoria de programa.", string.Empty, null, totalWatch.ElapsedMilliseconds);
            FinishRun("PAG34_NO_END_WITHIN_4000", DescribeProfile(profile), string.Empty);
        }

        private static bool IsSafeProgramRead34Request(byte[] tx, int expectedStartStep)
        {
            if (tx == null || tx.Length != 6) return false;
            if (tx[0] != 0x34 || tx[1] != 0x03 || tx[4] != 0xA0) return false;
            int encodedStart = (tx[2] << 8) | tx[3];
            if (encodedStart != expectedStartStep) return false;
            return Sum8(tx) == 0xFF;
        }

        private void Save34PageDump(byte[] frame, int startStep)
        {
            if (frame == null || frame.Length < 3) return;
            try
            {
                string dir = GetReportsDirectory();
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                string baseName = "TP02-PG-34-page-step" + startStep.ToString("X4", CultureInfo.InvariantCulture) + "-" + stamp;
                string framePath = Path.Combine(dir, baseName + "-frame.bin");
                string payloadPath = Path.Combine(dir, baseName + "-payload.bin");
                File.WriteAllBytes(framePath, frame);
                int payloadLen = frame[1];
                byte[] payload = new byte[payloadLen];
                if (payloadLen > 0) Buffer.BlockCopy(frame, 2, payload, 0, payloadLen);
                File.WriteAllBytes(payloadPath, payload);
                LogEvent("DUMP", "34 pagina start=0x" + startStep.ToString("X4", CultureInfo.InvariantCulture) + " salva: " + framePath + " | payload: " + payloadPath, string.Empty, null, 0);
            }
            catch (Exception ex)
            {
                LogEvent("DUMP", "falha ao salvar pagina do 34: " + ex.Message, string.Empty, null, 0);
            }
        }

'@ 'Generalizacao do helper de pagina'

if (-not $text.Contains('        private const string EngineVersion = "1.19";')) { throw 'EngineVersion 1.19 nao encontrado apos PaginationV29.' }
$text = $text.Replace('        private const string EngineVersion = "1.19";', '        private const string EngineVersion = "1.20";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.20 aplicado: paginacao generica READ-ONLY do 34 por passos, parada em END e limite de 4000; 38 apenas como hint.'
