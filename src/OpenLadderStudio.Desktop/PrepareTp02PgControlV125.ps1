$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($text.Contains($needleCrLf)) { return $text.Replace($needleCrLf, $replacementCrLf) }
    if ($text.Contains($needleLf)) { return $text.Replace($needleLf, $replacementLf) }
    throw "Ancora nao encontrada ($label)."
}

# v1.25: transforma a antiga tela de prova PG33 em um painel operacional PG.
# READ/WRITE/VERIFY usam apenas caminhos ja existentes e validados.
# STOP/RUN aparecem na interface final, mas continuam sem TX ate que o quadro
# de mudanca de estado PG seja identificado e validado no TP02 fisico.
$shell = Replace-Required $shell '            Text = "TP02 - Validacao fisica PG33 sem alterar programa";' '            Text = "TP02 - Controle PG / TP-232PG";' 'titulo da janela PG v1.25'
$shell = Replace-Required $shell '            Label title = NewLabel("TP02 - PROVA FISICA PG33 (NO-OP)", 14.0f, FontStyle.Bold, Fore);' '            Label title = NewLabel("TP02 - CONTROLE PG / TP-232PG", 14.0f, FontStyle.Bold, Fore);' 'titulo interno PG v1.25'

$subNeedle = @'
            header.Controls.Add(sub);
'@
$subReplacement = @'
            header.Controls.Add(sub);
            sub.Text = "READ / WRITE / VERIFY no protocolo PG. STOP e RUN ficam protegidos ate validacao do quadro de estado.";
'@
$shell = Replace-Required $shell $subNeedle $subReplacement 'subtitulo PG v1.25'

$buttonNeedle = @'
            controls.Controls.Add(verifyProjectButton);
'@
$buttonReplacement = @'
            controls.Controls.Add(verifyProjectButton);

            // Barra operacional v1.25. Os botoes antigos de laboratorio permanecem
            // no codigo, mas WRITE/VERIFY antigos ficam ocultos para evitar duplicidade.
            changeRestoreButton.Visible = false;
            writeProjectButton.Visible = false;
            verifyProjectButton.Visible = false;

            Button readPgButton = NewButton("READ", 310, 70, 100, false);
            readPgButton.Click += delegate { StartPgReadV125(); };
            controls.Controls.Add(readPgButton);

            Button writePgButton = NewButton("WRITE", 420, 70, 110, true);
            writePgButton.Click += delegate { StartProjectWriteV123(); };
            controls.Controls.Add(writePgButton);

            Button verifyPgButton = NewButton("VERIFY", 540, 70, 115, false);
            verifyPgButton.Click += delegate { StartProjectVerifyV124(); };
            controls.Controls.Add(verifyPgButton);

            Button stopPgButton = NewButton("STOP", 665, 70, 105, false);
            stopPgButton.ForeColor = Warning;
            stopPgButton.Click += delegate { ShowPgRunStopPendingV125("STOP"); };
            controls.Controls.Add(stopPgButton);

            Button runPgButton = NewButton("RUN", 780, 70, 105, false);
            runPgButton.ForeColor = Success;
            runPgButton.Click += delegate { ShowPgRunStopPendingV125("RUN"); };
            controls.Controls.Add(runPgButton);

            Label pgControlNote = NewLabel("STOP/RUN: protegido", 7.7f, FontStyle.Bold, Warning);
            pgControlNote.Location = new Point(900, 81);
            pgControlNote.MaximumSize = new Size(145, 32);
            controls.Controls.Add(pgControlNote);
'@
$shell = Replace-Required $shell $buttonNeedle $buttonReplacement 'botoes READ WRITE VERIFY STOP RUN'

$anchor = '        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)'
$index = $shell.IndexOf($anchor, [System.StringComparison]::Ordinal)
if ($index -lt 0) { throw 'ReadCanonicalSnapshotOnOpenPort nao encontrado para V125.' }

$methods = @'
        private void StartPgReadV125()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG READ v1.25 iniciado em " + portName + ".");
            AppendLog("SEGURANCA: READ nao transmite PG33, nao restaura e nao altera o programa.");
            SetBusy(true);
            SetStatus("READ / LENDO...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                ProgramSnapshot snapshot = null;
                string successText = string.Empty;
                try
                {
                    snapshot = ReadSnapshotV93Robust(portName, "manual-read-v125");
                    if (snapshot == null || snapshot.Count < 1 || snapshot.EndStep < 0)
                        throw new InvalidDataException("READ PG invalido: F-00 END nao foi encontrado.");

                    SaveSnapshot("manual-read-v125", snapshot);
                    AppendLogSafe("READ OK: estado=" + snapshot.PlcState
                        + " palavras=" + snapshot.Count.ToString(CultureInfo.InvariantCulture)
                        + " END=" + snapshot.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");
                    AppendLogSafe("PROGRAMA LIDO (HIGH LOW BRAW):");
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        AppendLogSafe(i.ToString("0000", CultureInfo.InvariantCulture) + "  "
                            + snapshot.High[i].ToString("X2", CultureInfo.InvariantCulture) + " "
                            + snapshot.Low[i].ToString("X2", CultureInfo.InvariantCulture) + " "
                            + snapshot.External[i].ToString("X2", CultureInfo.InvariantCulture));
                    }

                    string report = "PASS PG READ v1.25\r\n"
                        + "Estado: " + snapshot.PlcState + "\r\n"
                        + "Palavras: " + snapshot.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                        + "END: " + snapshot.EndStep.ToString("0000", CultureInfo.InvariantCulture) + "\r\n"
                        + "PG33 transmitido: NAO\r\n"
                        + "Alteracao do PLC: NAO\r\n";
                    File.WriteAllText(Path.Combine(sessionDirectory, "pg-read-v125-report.txt"), report, Encoding.UTF8);

                    successText = "READ PG concluido.\r\n\r\n"
                        + "Estado: " + snapshot.PlcState
                        + "\r\nPalavras: " + snapshot.Count.ToString(CultureInfo.InvariantCulture)
                        + "\r\nEND: " + snapshot.EndStep.ToString("0000", CultureInfo.InvariantCulture)
                        + "\r\n\r\nO programa lido foi registrado no log e na pasta da sessao."
                        + "\r\nNenhuma escrita foi realizada.";
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg-read-v125-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure == null)
                    {
                        AppendLog("PASS: READ concluido; nenhuma escrita foi realizada.");
                        SetStatus("READ OK / SOMENTE LEITURA", Success);
                        MessageBox.Show(this, successText, "TP02 - READ PG",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AppendLog("READ FALHOU: " + failure.Message);
                        SetStatus("READ PENDENTE / SEM ESCRITA", Warning);
                        MessageBox.Show(this,
                            failure.Message + "\r\n\r\nNenhuma escrita foi realizada.",
                            "TP02 - READ PG nao concluido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    SetBusy(false);
                }));
            });
        }

        private void ShowPgRunStopPendingV125(string action)
        {
            if (busy) return;
            string normalized = string.Equals(action, "RUN", StringComparison.Ordinal) ? "RUN" : "STOP";
            AppendLog("V1.25 " + normalized + " PG: BLOQUEADO por seguranca; nenhum byte foi transmitido.");
            SetStatus(normalized + " PG / AGUARDANDO VALIDACAO", Warning);
            MessageBox.Show(this,
                normalized + " pelo enlace TP-232PG ainda nao possui quadro de mudanca de estado fisicamente validado.\r\n\r\n"
                + "O botao ja faz parte do OpenLadder, mas permanece protegido para nao enviar um opcode desconhecido ao TP02.\r\n\r\n"
                + "NENHUM BYTE FOI ENVIADO.",
                "TP02 - " + normalized + " PG protegido",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

'@

$shell = $shell.Substring(0, $index) + $methods + $shell.Substring($index)

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG Control V125 aplicado: READ/WRITE/VERIFY integrados; STOP/RUN visiveis com trava sem TX desconhecido.'
