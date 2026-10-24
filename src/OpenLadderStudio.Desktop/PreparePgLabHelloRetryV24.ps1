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

# v1.14: o log de bancada 2026-09-09 20:11 mostrou que o HELLO pode ficar
# silencioso por varias aberturas da COM e depois responder corretamente em STOP.
# Nesta versao, cada sessao serial pode tentar o HELLO ate 6 vezes, sem fechar a
# porta entre essas tentativas. Quando o HELLO-STOP e confirmado, F0 continua sendo
# enviado UMA unica vez naquela sessao. Se F0 falhar, a COM e fechada, aguarda 1,5 s
# e a proxima sessao recomeca desde o HELLO. 38 e 34 so seguem apos validacao.

$text = Replace-Required $text @'
            const int maxSessions = 12;
'@ @'
            const int maxSessions = 12;
            const int helloAttemptsPerSession = 6;
'@ 'Quantidade de tentativas de HELLO por sessao'

$text = Replace-Required $text @'
                        byte[] rxHello = CleanTxRx(port, hello, "HELLO", 1800, totalWatch);
                        if (IndexOfSequence(rxHello, helloRun) >= 0)
                        {
                            LogEvent("ESTADO", "PLC em RUN. Coloque o TP02 em STOP e execute novamente.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            FinishRun("PLC_RUN_NEEDS_STOP", DescribeProfile(profile), ToHex(helloRun));
                            return;
                        }
                        if (IndexOfSequence(rxHello, helloStop) < 0)
                        {
                            LogEvent("RETRY", "HELLO-STOP nao confirmado; fechando a COM e iniciando nova sessao.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            retry = true;
                        }
                        else
                        {
'@ @'
                        byte[] rxHello = new byte[0];
                        bool helloStopOk = false;
                        bool helloRunSeen = false;
                        for (int helloAttempt = 1; helloAttempt <= helloAttemptsPerSession && !cancelRequested; helloAttempt++)
                        {
                            string helloLabel = "HELLO tentativa " + helloAttempt.ToString(CultureInfo.InvariantCulture) + "/" + helloAttemptsPerSession.ToString(CultureInfo.InvariantCulture);
                            rxHello = CleanTxRx(port, hello, helloLabel, 1800, totalWatch);
                            if (IndexOfSequence(rxHello, helloRun) >= 0)
                            {
                                helloRunSeen = true;
                                break;
                            }
                            if (IndexOfSequence(rxHello, helloStop) >= 0)
                            {
                                helloStopOk = true;
                                break;
                            }
                            if (helloAttempt < helloAttemptsPerSession)
                            {
                                LogEvent("HELLO", "sem resposta valida; mantendo a mesma COM aberta para nova tentativa.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                                Thread.Sleep(180);
                            }
                        }

                        if (helloRunSeen)
                        {
                            LogEvent("ESTADO", "PLC em RUN. Coloque o TP02 em STOP e execute novamente.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            FinishRun("PLC_RUN_NEEDS_STOP", DescribeProfile(profile), ToHex(helloRun));
                            return;
                        }
                        if (!helloStopOk)
                        {
                            LogEvent("RETRY", "HELLO-STOP nao confirmado apos 6 tentativas na mesma COM; fechando e iniciando nova sessao.", string.Empty, null, totalWatch.ElapsedMilliseconds);
                            retry = true;
                        }
                        else
                        {
'@ 'Loop de HELLO na mesma sessao'

if (-not $text.Contains('        private const string EngineVersion = "1.13";')) { throw 'EngineVersion 1.13 nao encontrado apos CleanSessionV23.' }
$text = $text.Replace('        private const string EngineVersion = "1.13";', '        private const string EngineVersion = "1.14";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.14 aplicado: ate 6 HELLOs por sessao, mantendo um unico F0 por abertura da COM.'
