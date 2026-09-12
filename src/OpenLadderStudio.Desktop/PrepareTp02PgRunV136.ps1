$ErrorActionPreference = 'Stop'

# V136: primeira validacao fisica controlada do RUN no protocolo PG.
#
# A analise estatica reproduzivel do pc12.exe mapeou:
#   0x0046F4FA -> quadro 02 00 FD
#   chamadores em 0x004AE5DC..0x004AE836
#   string de resultado no mesmo handler: "PLC Mode: Running"
#
# Isso e evidencia estatica forte, mas NAO e validacao fisica. Por isso o RUN:
# - exige conexao previa e requalifica STOP por HELLO+F0 na mesma porta aberta;
# - pede confirmacao explicita sobre energizacao de saidas;
# - transmite 02 00 FD exatamente UMA vez;
# - nunca retransmite o RUN, mesmo se a verificacao falhar;
# - confirma o resultado somente por HELLO conhecido (C0 01 09 35 = RUN);
# - se o estado final ficar desconhecido, invalida a conexao logica da tela.
# STOP permanece protegido e sem TX nesta versao.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V136: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "V136: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "V136: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# -----------------------------------------------------------------------------
# 1. Substitui apenas o handler protegido RUN/STOP da janela PG.
# STOP conserva o comportamento anterior sem TX. RUN ganha o ensaio one-shot.
# -----------------------------------------------------------------------------
$runStart = '        private void ShowPgRunStopPendingV125(string action)'
$runEnd = '        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort'
$runReplacement = @'
        private bool runAttemptedV136;
        private string runResultStateV136 = "CANCELLED";

        internal bool RunAttemptedV136
        {
            get { return runAttemptedV136; }
        }

        internal string RunResultStateV136
        {
            get { return runResultStateV136 ?? string.Empty; }
        }

        private void ShowPgRunStopPendingV125(string action)
        {
            if (busy) return;
            string normalized = string.Equals(action, "RUN", StringComparison.Ordinal) ? "RUN" : "STOP";
            if (string.Equals(normalized, "RUN", StringComparison.Ordinal))
            {
                StartPgRunV136();
                return;
            }

            AppendLog("V1.36 STOP PG: BLOQUEADO por seguranca; nenhum byte foi transmitido.");
            SetStatus("STOP PG / AGUARDANDO VALIDACAO", Warning);
            MessageBox.Show(this,
                "STOP pelo enlace TP-232PG ainda nao possui validacao fisica independente nesta versao.\r\n\r\n"
                + "NENHUM BYTE FOI ENVIADO.",
                "TP02 - STOP PG protegido",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void StartPgRunV136()
        {
            if (busy) return;
            runAttemptedV136 = false;
            runResultStateV136 = "CANCELLED";

            if (portCombo == null || portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 - RUN PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirmation = MessageBox.Show(this,
                "ATENCAO: o TP02 sera colocado em RUN.\r\n\r\n"
                + "O programa do PLC podera energizar saidas fisicas e acionar a maquina/processo.\r\n"
                + "Confirme que pessoas, cargas e equipamentos estao em condicao segura.\r\n\r\n"
                + "Esta e a primeira validacao fisica deste quadro PG no OpenLadder.\r\n"
                + "O comando sera transmitido UMA UNICA VEZ e o resultado sera confirmado por HELLO.\r\n\r\n"
                + "Deseja realmente colocar o TP02 em RUN?",
                "TP02 - CONFIRMAR RUN",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
            {
                AppendLog("V1.36 RUN cancelado pelo operador; nenhum byte foi transmitido.");
                SetStatus("RUN CANCELADO / SEM TX", Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("RUN PG v1.36 iniciado em " + portName + ".");
            AppendLog("ORIGEM ESTATICA: PC12 0x0046F4FA -> 02 00 FD; handler mostra PLC Mode: Running.");
            AppendLog("SEGURANCA: STOP sera requalificado por HELLO+F0 antes do unico TX RUN.");
            AppendLog("SEGURANCA: nao existe retransmissao automatica do quadro RUN.");
            SetBusy(true);
            SetStatus("RUN / QUALIFICANDO STOP...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string resultState = "UNKNOWN";
                string resultText = string.Empty;
                bool sent = false;
                SerialPort serial = null;
                try
                {
                    string initialState;
                    string acquisition;
                    serial = AcquireStablePgPortV93(portName, 1, out initialState, out acquisition);
                    if (serial == null || !serial.IsOpen)
                        throw new IOException("A porta PG nao permaneceu aberta apos a qualificacao.");

                    AppendLogSafe("V136 PREFLIGHT: " + acquisition + " / estado=" + initialState + ".");

                    if (string.Equals(initialState, "RUN", StringComparison.Ordinal))
                    {
                        resultState = "RUN";
                        resultText = "O TP02 ja estava em RUN.\r\n\r\nNenhum quadro de mudanca de estado foi transmitido.";
                    }
                    else
                    {
                        if (!string.Equals(initialState, "STOP", StringComparison.Ordinal))
                            throw new InvalidOperationException("RUN recusado: o preflight nao confirmou STOP nem RUN.");

                        byte[] runFrame = new byte[] { 0x02, 0x00, 0xFD };
                        serial.DiscardInBuffer();
                        serial.Write(runFrame, 0, runFrame.Length);
                        sent = true;
                        runAttemptedV136 = true;
                        AppendLogSafe("V136 RUN TX UNICO: 02 00 FD");

                        // Somente escuta passiva. O conteudo recebido aqui nao confirma RUN;
                        // a confirmacao e feita exclusivamente pelo HELLO conhecido abaixo.
                        Thread.Sleep(220);
                        try
                        {
                            int available = serial.BytesToRead;
                            if (available > 0)
                            {
                                byte[] passive = new byte[available];
                                int got = serial.Read(passive, 0, passive.Length);
                                if (got > 0)
                                {
                                    if (got != passive.Length)
                                    {
                                        byte[] trimmed = new byte[got];
                                        Array.Copy(passive, trimmed, got);
                                        passive = trimmed;
                                    }
                                    AppendLogSafe("V136 RUN RX PASSIVO: " + ToHex(passive));
                                }
                            }
                            else AppendLogSafe("V136 RUN RX PASSIVO: []");
                        }
                        catch (Exception exPassive)
                        {
                            AppendLogSafe("V136 RUN RX PASSIVO indisponivel: " + exPassive.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    ClosePort(serial);
                    serial = null;
                }

                if (failure == null && !string.Equals(resultState, "RUN", StringComparison.Ordinal))
                {
                    // Verificacao separada: somente HELLO. Nunca retransmite 02 00 FD.
                    Thread.Sleep(260);
                    string lastError = string.Empty;
                    for (int verifyRound = 1; verifyRound <= 2; verifyRound++)
                    {
                        string verifyState;
                        string verifyDetail;
                        string verifyError;
                        bool seen = TryHomePresenceFastV133(portName, out verifyState, out verifyDetail, out verifyError);
                        AppendLogSafe("V136 VERIFY HELLO rodada "
                            + verifyRound.ToString(CultureInfo.InvariantCulture) + ": "
                            + (seen ? verifyState + " / " + verifyDetail : "SEM ESTADO / " + verifyError));

                        if (seen && string.Equals(verifyState, "RUN", StringComparison.Ordinal))
                        {
                            resultState = "RUN";
                            resultText = "RUN CONFIRMADO.\r\n\r\n"
                                + "O quadro 02 00 FD foi transmitido uma unica vez e o HELLO posterior confirmou o TP02 em RUN.";
                            break;
                        }
                        if (seen && string.Equals(verifyState, "STOP", StringComparison.Ordinal))
                        {
                            resultState = "STOP";
                            resultText = "RUN NAO CONFIRMADO.\r\n\r\n"
                                + "O quadro RUN foi transmitido uma unica vez, mas o HELLO posterior confirmou que o TP02 permanece em STOP.\r\n\r\n"
                                + "Nao houve retransmissao.";
                            break;
                        }

                        lastError = verifyError;
                        if (verifyRound < 2) Thread.Sleep(220);
                    }

                    if (string.Equals(resultState, "UNKNOWN", StringComparison.Ordinal))
                    {
                        resultText = "ESTADO FINAL DESCONHECIDO.\r\n\r\n"
                            + (sent
                                ? "O quadro RUN foi transmitido UMA UNICA VEZ, mas o HELLO final nao confirmou RUN nem STOP."
                                : "Nao foi possivel confirmar o estado do TP02.")
                            + "\r\n\r\nNAO houve retransmissao do RUN. Reconecte para confirmar o estado antes de outra operacao."
                            + (string.IsNullOrEmpty(lastError) ? string.Empty : "\r\n\r\nDetalhe: " + lastError);
                    }
                }

                if (failure != null)
                {
                    resultState = sent ? "UNKNOWN" : "STOP";
                    resultText = (sent
                        ? "O quadro RUN foi transmitido UMA UNICA VEZ, mas ocorreu falha antes da confirmacao do estado."
                        : "RUN NAO TRANSMITIDO: o preflight falhou antes do comando de mudanca de estado.")
                        + "\r\n\r\n" + failure.Message
                        + "\r\n\r\nNao houve retransmissao do RUN.";
                }

                runResultStateV136 = resultState;
                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    SetBusy(false);
                    if (string.Equals(resultState, "RUN", StringComparison.Ordinal))
                    {
                        AppendLog("PASS V1.36: RUN confirmado por HELLO C0 01 09 35.");
                        SetStatus("RUN CONFIRMADO", Success);
                        MessageBox.Show(this, resultText, "TP02 - RUN CONFIRMADO",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (string.Equals(resultState, "STOP", StringComparison.Ordinal))
                    {
                        AppendLog("V1.36: TP02 permanece em STOP; RUN nao foi retransmitido.");
                        SetStatus("STOP CONFIRMADO / SEM RETRY RUN", Warning);
                        MessageBox.Show(this, resultText, "TP02 - RUN NAO CONFIRMADO",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        AppendLog("V1.36: estado final desconhecido; RUN nao sera retransmitido.");
                        SetStatus("ESTADO DESCONHECIDO / SEM RETRY", Warning);
                        MessageBox.Show(this, resultText, "TP02 - ESTADO DESCONHECIDO",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    Close();
                }));
            });
        }

'@
$shell = Replace-Section $shell $runStart $runEnd $runReplacement 'RUN one-shot V136'

# -----------------------------------------------------------------------------
# 2. A tela principal so muda o indicador para RUN depois do resultado do HELLO.
# Se o TX ocorreu e o estado ficou desconhecido, a conexao logica e invalidada.
# -----------------------------------------------------------------------------
$homeStart = '        private void ExecuteTp02HomeCommandV126(string command)'
$homeEnd = '        private MenuStrip BuildMenu()'
$homeReplacement = @'
        private void ExecuteTp02HomeCommandV126(string command)
        {
            string normalized = (command ?? string.Empty).Trim().ToUpperInvariant();
            RefreshProfileUi();

            if (!tp02HomeConnectedV128)
            {
                statusText.Text = "TP02: SEM CONEXAO";
                if (connectionValue != null) connectionValue.Text = "Sem conexao";
                MessageBox.Show(this,
                    "SEM CONEXAO COM O TP02.\r\n\r\nClique em CONECTAR para estabelecer a comunicacao antes de executar "
                    + normalized + ".\r\n\r\nNenhum byte foi transmitido.",
                    "OpenLadder Studio - SEM CONEXAO",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentProfile.Model ?? string.Empty, tp02HomeModelV128, StringComparison.OrdinalIgnoreCase))
            {
                ResetTp02HomeConnectionV128("Controlador alterado; conecte novamente.");
                MessageBox.Show(this,
                    "SEM CONEXAO COM O TP02.\r\n\r\nO controlador ativo mudou. Clique em CONECTAR novamente.",
                    "OpenLadder Studio - SEM CONEXAO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if ((string.Equals(normalized, "WRITE", StringComparison.Ordinal) ||
                 string.Equals(normalized, "VERIFY", StringComparison.Ordinal)) &&
                !string.Equals(tp02HomeStateV128, "STOP", StringComparison.Ordinal))
            {
                MessageBox.Show(this,
                    normalized + " exige o TP02 em STOP.\r\n\r\nEstado atual detectado: " + tp02HomeStateV128 + ".",
                    "OpenLadder Studio - TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (ladderForm == null || ladderForm.IsDisposed) ShowLadder();
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                MessageBox.Show(this, "O editor Ladder nao esta disponivel.", "OpenLadder Studio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string runResult = string.Empty;
            bool runAttempted = false;
            using (TP02Pg33NoOpProbeForm dialog = new TP02Pg33NoOpProbeForm(currentProfile, ladderForm))
            {
                dialog.UseHomePortV128(tp02HomePortV128);
                dialog.QueueHomeCommandV126(normalized);
                dialog.ShowDialog(this);
                if (string.Equals(normalized, "RUN", StringComparison.Ordinal))
                {
                    runResult = dialog.RunResultStateV136;
                    runAttempted = dialog.RunAttemptedV136;
                }
            }

            if (string.Equals(normalized, "RUN", StringComparison.Ordinal))
            {
                if (string.Equals(runResult, "RUN", StringComparison.Ordinal))
                {
                    tp02HomeStateV128 = "RUN";
                    UpdateTp02HomeButtonsV128();
                    if (connectionValue != null)
                        connectionValue.Text = "Conectado " + tp02HomePortV128 + " / RUN";
                    if (modeText != null)
                        modeText.Text = currentProfile.Model + "    |    " + currentProfile.Protocol
                            + "    |    ON-LINE RUN    |    v1.28";
                    statusText.Text = "TP02 em RUN confirmado por HELLO";
                    return;
                }
                if (string.Equals(runResult, "STOP", StringComparison.Ordinal))
                {
                    tp02HomeStateV128 = "STOP";
                    UpdateTp02HomeButtonsV128();
                    if (connectionValue != null)
                        connectionValue.Text = "Conectado " + tp02HomePortV128 + " / STOP";
                    statusText.Text = runAttempted
                        ? "RUN nao confirmado; TP02 permanece em STOP"
                        : "TP02 permanece em STOP";
                    return;
                }
                if (runAttempted && string.Equals(runResult, "UNKNOWN", StringComparison.Ordinal))
                {
                    ResetTp02HomeConnectionV128("Estado do TP02 desconhecido apos tentativa unica de RUN; conecte novamente.");
                    return;
                }
            }

            statusText.Text = "TP02 conectado em " + tp02HomePortV128 + " / " + tp02HomeStateV128;
        }

'@
$shell = Replace-Section $shell $homeStart $homeEnd $homeReplacement 'estado RUN na barra principal V136'

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 PG RUN V136 aplicado: 02 00 FD TX unica, preflight STOP e verificacao por HELLO; STOP segue bloqueado.' -ForegroundColor Cyan
