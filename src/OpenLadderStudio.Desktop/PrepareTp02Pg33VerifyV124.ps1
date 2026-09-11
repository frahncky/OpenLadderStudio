$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}

# v1.24: depois de um ACK PG33 valido, a verificacao pode ser repetida sem
# transmitir outro PG33. O botao abaixo faz somente HELLO/F0/38/34 + compare.
$buttonNeedle = @'
            controls.Controls.Add(writeProjectButton);
'@
$buttonReplacement = @'
            controls.Controls.Add(writeProjectButton);

            Button verifyProjectButton = NewButton("VERIFICAR GRAVACAO", 830, 70, 210, false);
            verifyProjectButton.Click += delegate { StartProjectVerifyV124(); };
            controls.Controls.Add(verifyProjectButton);
'@
$shell = Replace-Required $shell $buttonNeedle $buttonReplacement 'botao verificar gravacao PG33'

$anchor = '        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)'
$index = $shell.IndexOf($anchor, [System.StringComparison]::Ordinal)
if ($index -lt 0) { throw 'ReadCanonicalSnapshotOnOpenPort nao encontrado para V124.' }

$methods = @'
        private void StartProjectVerifyV124()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 PG33", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ProgramSnapshot expected;
            int logicalCount;
            string compilationReport;
            try
            {
                // Reutiliza exatamente a mesma compilacao/normalizacao da gravacao,
                // mas descarta o quadro PG33: esta operacao nunca escreve no PLC.
                byte[] unusedFrame = BuildValidatedProjectFrameV123(
                    out expected, out logicalCount, out compilationReport);
                if (unusedFrame == null || unusedFrame.Length == 0)
                    throw new InvalidDataException("Nao foi possivel preparar a referencia de verificacao.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Verificacao PG33 bloqueada",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG33 READBACK-ONLY VERIFY v1.24 iniciado em " + portName + ".");
            AppendLog("SEGURANCA: esta rotina NAO transmite PG33, NAO restaura e NAO altera o programa.");
            AppendLog(compilationReport);
            SetBusy(true);
            SetStatus("VERIFICANDO SEM ESCRITA...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string successText = string.Empty;
                try
                {
                    successText = RunProjectVerifyV124(portName, expected, logicalCount);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg33-project-verify-v124-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure == null)
                    {
                        AppendLog("PASS: readback identico ao projeto atual; nenhuma escrita foi feita.");
                        SetStatus("GRAVACAO CONFIRMADA / READBACK OK", Success);
                        MessageBox.Show(this, successText,
                            "TP02 - GRAVACAO CONFIRMADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AppendLog("VERIFICACAO: " + failure.Message);
                        SetStatus("VERIFICACAO PENDENTE / SEM NOVA ESCRITA", Warning);
                        MessageBox.Show(this,
                            failure.Message + "\r\n\r\nNenhum PG33 foi transmitido nesta verificacao.",
                            "TP02 - verificacao nao concluida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    SetBusy(false);
                }));
            });
        }

        private string RunProjectVerifyV124(string portName, ProgramSnapshot expected, int logicalCount)
        {
            Exception last = null;
            bool confirmedMismatch = false;
            ProgramSnapshot actual = null;

            // O enlace PG do TP02 pode precisar de varias reacquisicoes. Como esta
            // rotina e somente leitura, repetimos a qualificacao sem risco de
            // retransmitir a gravacao. Se ainda falhar, o usuario pode aciona-la
            // novamente sem modificar o PLC.
            for (int round = 1; round <= 8; round++)
            {
                SerialPort port = null;
                try
                {
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN; mantenha o TP02 em STOP para verificar a gravacao.");

                    PerformF0WriteQualified(port, "V124-VERIFY-ONLY");
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "verify-only-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    actual = ReadCanonicalSnapshotOnOpenPort(port,
                        "verify-only-r" + round.ToString(CultureInfo.InvariantCulture));
                    SaveSnapshot("verify-only-readback-r" + round.ToString(CultureInfo.InvariantCulture), actual);

                    if (SnapshotsEqual(expected, actual))
                    {
                        string report = "PASS PG33 READBACK-ONLY VERIFY v1.24\r\n"
                            + "Instrucoes logicas: " + logicalCount.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "Palavras esperadas: " + expected.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "Palavras lidas: " + actual.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "Readback: identico ao projeto compilado\r\n"
                            + "PG33 transmitido nesta operacao: NAO\r\n"
                            + "Restore transmitido nesta operacao: NAO\r\n"
                            + "RUN remoto: NAO\r\n";
                        File.WriteAllText(Path.Combine(sessionDirectory, "pg33-project-verify-v124-report.txt"), report, Encoding.UTF8);
                        return "Gravacao confirmada por readback.\r\n\r\n"
                            + "Projeto no TP02: IDENTICO AO PROJETO ATUAL.\r\n"
                            + "Palavras: " + actual.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "Tentativa de reconexao: " + round.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n"
                            + "Nenhuma escrita foi realizada durante a verificacao.";
                    }

                    confirmedMismatch = true;
                    AppendLogSafe("V124: readback foi obtido, mas diverge do projeto atual. Nenhuma restauracao sera feita.");
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("V124 VERIFY rodada " + round.ToString(CultureInfo.InvariantCulture)
                        + " falhou sem escrita: " + ex.Message);
                }
                finally { ClosePort(port); }

                if (confirmedMismatch) break;
            }

            if (confirmedMismatch)
                throw new InvalidDataException(
                    "O readback foi obtido, mas o programa armazenado diverge do projeto atual. "
                    + "Nenhuma retransmissao e nenhuma restauracao foram executadas.");

            throw new IOException(
                "Nao foi possivel obter o readback apos 8 rodadas de reconexao. "
                + "A gravacao NAO foi repetida. Aguarde o enlace estabilizar e use VERIFICAR GRAVACAO novamente. Diagnostico: "
                + (last == null ? "desconhecido" : last.Message));
        }

'@

$shell = $shell.Substring(0, $index) + $methods + $shell.Substring($index)

# A mensagem da escrita passa a distinguir ACK confirmado de readback pendente.
$shell = $shell.Replace(
    'O projeto recebeu ACK 00 00 FF, mas o readback final nao foi obtido. ',
    'O TP02 confirmou a gravacao com ACK 00 00 FF, mas a reconexao de verificacao falhou. ')
$shell = $shell.Replace(
    'Nao houve retransmissao nem restore cego. Diagnostico: ',
    'O PG33 nao sera reenviado. Use VERIFICAR GRAVACAO para conferir o projeto sem escrever novamente. Diagnostico: ')
$shell = $shell.Replace('PG33 PROJECT WRITE v1.23 iniciado em ', 'PG33 PROJECT WRITE v1.24 iniciado em ')
$shell = $shell.Replace('PASS PG33 PROJECT WRITE v1.23', 'PASS PG33 PROJECT WRITE v1.24')
$shell = $shell.Replace('PG33 v1.23 aceita ', 'PG33 v1.24 aceita ')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Verify V124 aplicado: verificacao de readback repetivel sem PG33/restore e mensagem de ACK pendente corrigida.'
