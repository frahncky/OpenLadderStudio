$ErrorActionPreference = 'Stop'

# V1.52 - PG33 multibloco para projetos maiores que 80 machine words.
#
# Evidencia reproduzivel ja presente no repositorio:
# - o PC12 encerra cada bloco na 20a INSTRUCAO LOGICA;
# - cada instrucao ocupa 1..4 machine words (StepSpan);
# - portanto um bloco PG33 possui 1..80 machine words;
# - o quadro 33 carrega START_H/START_L do passo real do bloco;
# - apos sucesso, o PC12 preserva o cursor real e inicia o bloco seguinte.
#
# Guardrails produtivos deste patch:
# - o subconjunto fisicamente validado de instrucoes permanece o mesmo da V123;
# - PLC deve estar em STOP;
# - cada bloco PG33 e transmitido no maximo UMA vez;
# - qualquer falha/ACK incerto depois do primeiro TX aborta toda a sequencia;
# - nao ha retransmissao cega nem restore multibloco automatico;
# - o backup e salvo antes do primeiro bloco;
# - depois de todos os ACKs, o programa e relido e comparado;
# - o caminho legado de um unico bloco continua usando RunProjectWriteV123.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V152: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "V152: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "V152: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$writeStart = '        private void StartProjectWriteV123()'
$writeEnd = '        private string RunProjectWriteV123('
$writeReplacement = @'
        private sealed class Pg33BlockV152
        {
            public int StartStep;
            public int NextStep;
            public int InstructionCount;
            public int MachineWordCount;
            public byte[] Frame;
        }

        private void StartProjectWriteV123()
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
            IList<Pg33BlockV152> blocks;
            try
            {
                blocks = BuildValidatedProjectPlanV152(out expected, out logicalCount, out compilationReport);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Gravacao PG33 bloqueada",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool multi = blocks.Count > 1;
            string modeTextV152 = multi
                ? "MULTIBLOCO: " + blocks.Count.ToString(CultureInfo.InvariantCulture) + " quadros PG33"
                : "BLOCO UNICO: caminho fisicamente validado";

            DialogResult confirm = MessageBox.Show(this,
                "GRAVAR PROJETO ATUAL NO TP02 POR PG33\r\n\r\n"
                + "PLC obrigatoriamente em STOP.\r\n"
                + "Instrucoes logicas: " + logicalCount.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Palavras de maquina: " + expected.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + modeTextV152 + "\r\n\r\n"
                + (multi
                    ? "Cada bloco sera transmitido UMA UNICA VEZ, em sequencia, na mesma sessao serial. "
                      + "Se houver falha depois que qualquer bloco tiver sido enviado, a sequencia sera abortada sem retry cego e sem restore multibloco automatico. "
                      + "O backup sera salvo e o TP02 devera permanecer em STOP ate a verificacao.\r\n\r\n"
                    : "O caminho de bloco unico preserva o fluxo previamente validado em bancada.\r\n\r\n")
                + "Use somente em bancada/processo seguro. Continuar?",
                "Confirmar gravacao PG33", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG33 PROJECT WRITE v1.52 iniciado em " + portName + ".");
            AppendLog(compilationReport);
            SetBusy(true);
            SetStatus(multi ? "BACKUP / PG33 MULTIBLOCO / VERIFY..." : "BACKUP / GRAVACAO / VERIFY...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string successText = string.Empty;
                try
                {
                    if (blocks.Count == 1)
                    {
                        // Preserva integralmente o caminho ja validado fisicamente para projetos pequenos.
                        successText = RunProjectWriteV123(portName, blocks[0].Frame, expected, logicalCount);
                    }
                    else
                    {
                        successText = RunProjectWriteMultiV152(portName, blocks, expected, logicalCount);
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg33-project-write-v152-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure == null)
                    {
                        AppendLog("PASS V1.52: projeto gravado e confirmado por readback.");
                        SetStatus("PROJETO GRAVADO / VERIFY OK", Success);
                        MessageBox.Show(this, successText, "TP02 - GRAVACAO PG33 APROVADA",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AppendLog("FALHA V1.52: " + failure.Message);
                        SetStatus("MANTER PLC EM STOP / VER LOG", Danger);
                        MessageBox.Show(this,
                            failure.Message + "\r\n\r\nMANTENHA O TP02 EM STOP ate confirmar o programa pelo OpenLadder/PC12.",
                            "TP02 - gravacao PG33 nao confirmada", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    SetBusy(false);
                }));
            });
        }

        private IList<Pg33BlockV152> BuildValidatedProjectPlanV152(
            out ProgramSnapshot expected, out int logicalCount, out string report)
        {
            if (ladderForm == null || ladderForm.IsDisposed)
                throw new InvalidOperationException("Editor Ladder nao esta disponivel.");

            System.Reflection.MethodInfo method = typeof(LadderEditorForm).GetMethod(
                "SerializeProject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException("SerializeProject nao encontrado.");

            string text = method.Invoke(ladderForm, null) as string;
            LadderProjectDocument document = LadderProjectCodec.Deserialize(text);
            Tp02LadderCompilationResult compilation = Tp02LadderTargetCompiler.Compile(document);
            report = compilation.BuildReport();
            if (!compilation.Success)
                throw new InvalidDataException("Projeto possui " + compilation.Errors.Count.ToString(CultureInfo.InvariantCulture)
                    + " erro(s) de compilacao TP02.");
            if (compilation.Words.Count < 1 || compilation.Words.Count > 4000)
                throw new InvalidDataException("PG33 multibloco v1.52 aceita 1..4000 palavras; projeto possui "
                    + compilation.Words.Count.ToString(CultureInfo.InvariantCulture) + ".");

            expected = SnapshotFromCompiledWordsV123(compilation.Words);
            if (expected.EndStep != expected.Count - 1)
                throw new InvalidDataException("F-00 END precisa ser a ultima palavra do projeto.");

            List<Pg33BlockV152> blocks = new List<Pg33BlockV152>();
            int cursor = 0;
            logicalCount = 0;

            while (cursor < compilation.Words.Count)
            {
                int blockStart = cursor;
                int blockInstructions = 0;
                int blockWords = 0;

                while (cursor < compilation.Words.Count && blockInstructions < 20)
                {
                    int span = GetValidatedInstructionSpanV152(compilation.Words, cursor);
                    if (span < 1 || span > 4)
                        throw new InvalidDataException("StepSpan PG33 invalido no passo "
                            + cursor.ToString("0000", CultureInfo.InvariantCulture) + ".");
                    if (blockWords + span > 80) break;

                    cursor += span;
                    blockWords += span;
                    blockInstructions++;
                    logicalCount++;
                }

                if (blockInstructions < 1 || blockWords < 1)
                    throw new InvalidDataException("Nao foi possivel formar bloco PG33 no passo "
                        + blockStart.ToString("0000", CultureInfo.InvariantCulture) + ".");

                Pg33BlockV152 block = new Pg33BlockV152();
                block.StartStep = blockStart;
                block.NextStep = cursor;
                block.InstructionCount = blockInstructions;
                block.MachineWordCount = blockWords;
                block.Frame = BuildPg33FrameV152(compilation.Words, blockStart, blockWords);
                blocks.Add(block);
            }

            StringBuilder plan = new StringBuilder(report);
            plan.AppendLine();
            plan.AppendLine("PG33 MULTIBLOCO v1.52");
            plan.AppendLine("Total de blocos: " + blocks.Count.ToString(CultureInfo.InvariantCulture));
            plan.AppendLine("Total de instrucoes logicas: " + logicalCount.ToString(CultureInfo.InvariantCulture));
            plan.AppendLine("Total de machine words: " + expected.Count.ToString(CultureInfo.InvariantCulture));
            for (int b = 0; b < blocks.Count; b++)
            {
                Pg33BlockV152 block = blocks[b];
                plan.Append("B");
                plan.Append((b + 1).ToString("000", CultureInfo.InvariantCulture));
                plan.Append(" start=");
                plan.Append(block.StartStep.ToString("0000", CultureInfo.InvariantCulture));
                plan.Append(" next=");
                plan.Append(block.NextStep.ToString("0000", CultureInfo.InvariantCulture));
                plan.Append(" instr=");
                plan.Append(block.InstructionCount.ToString(CultureInfo.InvariantCulture));
                plan.Append(" words=");
                plan.AppendLine(block.MachineWordCount.ToString(CultureInfo.InvariantCulture));
            }
            report = plan.ToString();
            return blocks;
        }

        private static int GetValidatedInstructionSpanV152(IList<Tp02MachineWord> words, int index)
        {
            if (words == null || index < 0 || index >= words.Count)
                throw new ArgumentOutOfRangeException("index");

            Tp02MachineWord w = words[index];
            int opcode = w.Low & 0x78;
            int deviceBase = w.High & 0x60;

            if ((w.High & 0x80) == 0 && (deviceBase == 0x00 || deviceBase == 0x20 || deviceBase == 0x40)
                && (opcode == 0x10 || opcode == 0x18 || opcode == 0x20 || opcode == 0x28
                    || opcode == 0x30 || opcode == 0x38 || opcode == 0x40))
                return 1;

            if ((w.High & 0x80) == 0 && (opcode == 0x60 || opcode == 0x68))
            {
                RequireWordV123(words, index + 1, IsLiteralV123, "preset literal de TMR/CNT");
                return 2;
            }

            if ((w.High == 0x17 || w.High == 0x18) && w.Low == 0x71)
            {
                RequireWordV123(words, index + 1, IsValidatedYBitV123, "operando Y de SET/RST");
                return 2;
            }

            if (w.High == 0x0D && w.Low == 0x77)
            {
                RequireWordV123(words, index + 1, IsDOperandV123, "primeiro D de F-13w");
                RequireWordV123(words, index + 2, IsDOperandV123, "segundo D de F-13w");
                RequireWordV123(words, index + 3, IsLiteralV123, "literal de F-13w");
                return 4;
            }

            if (w.High == 0x00 && w.Low == 0x70)
                return 1;

            throw new InvalidDataException("Instrucao ainda nao validada fisicamente no passo "
                + index.ToString("0000", CultureInfo.InvariantCulture) + ": " + w.ToHex() + ".");
        }

        private static byte[] BuildPg33FrameV152(IList<Tp02MachineWord> words, int startStep, int count)
        {
            if (words == null) throw new ArgumentNullException("words");
            if (startStep < 0 || startStep >= words.Count) throw new ArgumentOutOfRangeException("startStep");
            if (count < 1 || count > 80 || startStep + count > words.Count)
                throw new ArgumentOutOfRangeException("count");
            if (startStep > 3999 || startStep + count > 4000)
                throw new ArgumentOutOfRangeException("startStep", "Bloco ultrapassa o limite PG de 4000 passos.");

            int bytesAfterLength = (3 * count) + 4;
            byte[] frame = new byte[bytesAfterLength + 3];
            frame[0] = 0x33;
            frame[1] = checked((byte)bytesAfterLength);
            frame[2] = 0x00;
            frame[3] = (byte)((startStep >> 8) & 0xFF);
            frame[4] = (byte)(startStep & 0xFF);
            frame[5] = checked((byte)(2 * count));

            int p = 6;
            for (int i = 0; i < count; i++)
            {
                Tp02MachineWord word = words[startStep + i];
                frame[p++] = word.High;
                frame[p++] = word.Low;
            }
            for (int i = 0; i < count; i++)
                frame[p++] = words[startStep + i].External;

            int sum = 0;
            for (int i = 0; i < frame.Length - 1; i++) sum = (sum + frame[i]) & 0xFF;
            frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);

            int verify = 0;
            for (int i = 0; i < frame.Length; i++) verify = (verify + frame[i]) & 0xFF;
            if (verify != 0xFF) throw new InvalidDataException("Checksum interno PG33 v1.52 invalido.");
            return frame;
        }

        private string BuildPg33PlanTextV152(IList<Pg33BlockV152> blocks)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("PG33 MULTIBLOCK PLAN v1.52");
            text.AppendLine("blocks=" + blocks.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < blocks.Count; i++)
            {
                Pg33BlockV152 b = blocks[i];
                text.Append("block=");
                text.Append((i + 1).ToString("000", CultureInfo.InvariantCulture));
                text.Append(" start=");
                text.Append(b.StartStep.ToString("0000", CultureInfo.InvariantCulture));
                text.Append(" next=");
                text.Append(b.NextStep.ToString("0000", CultureInfo.InvariantCulture));
                text.Append(" instructions=");
                text.Append(b.InstructionCount.ToString(CultureInfo.InvariantCulture));
                text.Append(" words=");
                text.Append(b.MachineWordCount.ToString(CultureInfo.InvariantCulture));
                text.Append(" bytes=");
                text.AppendLine(b.Frame.Length.ToString(CultureInfo.InvariantCulture));
                text.AppendLine(ToHex(b.Frame));
            }
            return text.ToString();
        }

        private string RunProjectWriteMultiV152(string portName, IList<Pg33BlockV152> blocks,
            ProgramSnapshot expected, int logicalCount)
        {
            if (blocks == null || blocks.Count < 2)
                throw new ArgumentException("Fluxo multibloco exige pelo menos dois blocos.", "blocks");

            ProgramSnapshot backup = null;
            Exception last = null;
            int ackedBlocks = 0;
            bool possibleTx = false;

            File.WriteAllText(Path.Combine(sessionDirectory, "pg33-multiblock-v152-plan.txt"),
                BuildPg33PlanTextV152(blocks), Encoding.UTF8);

            // PREWRITE pode repetir somente enquanto nenhum PG33 sequer foi tentado.
            for (int round = 1; round <= 5 && ackedBlocks == 0 && !possibleTx; round++)
            {
                SerialPort port = null;
                try
                {
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (port == null || !port.IsOpen)
                        throw new IOException("Porta PG nao permaneceu aberta no preflight multibloco.");
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN; gravacao multibloco bloqueada.");

                    AppendLogSafe("V152 PREFLIGHT: " + acquisition + " / STOP confirmado.");
                    PerformF0WriteQualified(port, "V152-MULTI-PREWRITE");
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "v152-prewrite-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    backup = ReadCanonicalSnapshotOnOpenPort(port,
                        "v152-backup-r" + round.ToString(CultureInfo.InvariantCulture));
                    if (backup.Count < 1 || backup.EndStep < 0)
                        throw new InvalidDataException("Backup PG invalido: END ausente. Nenhum PG33 foi enviado.");
                    SaveSnapshot("v152-backup-original", backup);
                    AppendLogSafe("V152 BACKUP OK: " + backup.Count.ToString(CultureInfo.InvariantCulture)
                        + " palavras; iniciando " + blocks.Count.ToString(CultureInfo.InvariantCulture) + " blocos PG33.");

                    for (int i = 0; i < blocks.Count; i++)
                    {
                        Pg33BlockV152 block = blocks[i];
                        string tag = "V152-B" + (i + 1).ToString("000", CultureInfo.InvariantCulture)
                            + "-S" + block.StartStep.ToString("0000", CultureInfo.InvariantCulture);

                        File.WriteAllText(Path.Combine(sessionDirectory,
                            "pg33-v152-block-" + (i + 1).ToString("000", CultureInfo.InvariantCulture)
                            + "-start-" + block.StartStep.ToString("0000", CultureInfo.InvariantCulture) + ".hex"),
                            ToHex(block.Frame) + Environment.NewLine, Encoding.ASCII);

                        // A partir deste ponto a chamada pode ter colocado bytes no fio.
                        // Qualquer excecao passa a ser estado incerto: este bloco NAO sera repetido.
                        possibleTx = true;
                        byte[] ack = SendPg33OnceOnOpenPort(port, block.Frame, tag);
                        RequirePhysicalAck00(ack, "PG33 MULTIBLOCO " + tag);
                        ackedBlocks++;
                        possibleTx = false;
                        AppendLogSafe("V152 ACK bloco " + (i + 1).ToString(CultureInfo.InvariantCulture)
                            + "/" + blocks.Count.ToString(CultureInfo.InvariantCulture)
                            + " start=" + block.StartStep.ToString("0000", CultureInfo.InvariantCulture)
                            + " words=" + block.MachineWordCount.ToString(CultureInfo.InvariantCulture)
                            + " RX=" + ToHex(ack));

                        if (i + 1 < blocks.Count) Thread.Sleep(120);
                    }

                    break;
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (possibleTx || ackedBlocks > 0)
                    {
                        throw new IOException(
                            "GRAVACAO MULTIBLOCO PARCIAL/INCERTA. "
                            + ackedBlocks.ToString(CultureInfo.InvariantCulture) + " bloco(s) tiveram ACK confirmado; "
                            + "o bloco seguinte pode ter sido transmitido uma vez e NAO sera repetido. "
                            + "Nao houve restore automatico. Backup salvo na sessao. Detalhe: " + ex.Message, ex);
                    }
                }
                finally
                {
                    ClosePort(port);
                }
            }

            if (ackedBlocks != blocks.Count)
                throw new IOException("Nao foi possivel concluir o preflight antes da escrita multibloco. Nenhum PG33 foi enviado. Diagnostico: "
                    + (last == null ? "desconhecido" : last.Message));

            // VERIFY e estritamente readback-only. Todos os blocos ja tiveram ACK;
            // nenhum PG33 sera retransmitido daqui em diante.
            ProgramSnapshot actual = null;
            last = null;
            bool confirmedMismatch = false;
            for (int round = 1; round <= 8; round++)
            {
                SerialPort port = null;
                try
                {
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC saiu de STOP antes do verify multibloco.");
                    PerformF0WriteQualified(port, "V152-MULTI-VERIFY");
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "v152-verify-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    actual = ReadCanonicalSnapshotOnOpenPort(port,
                        "v152-verify-r" + round.ToString(CultureInfo.InvariantCulture));
                    SaveSnapshot("v152-readback-r" + round.ToString(CultureInfo.InvariantCulture), actual);

                    if (SnapshotsEqual(expected, actual))
                    {
                        string report = "PASS PG33 MULTIBLOCK WRITE v1.52\r\n"
                            + "Blocks: " + blocks.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "Logical instructions: " + logicalCount.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "Machine words: " + expected.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "ACKs confirmed: " + ackedBlocks.ToString(CultureInfo.InvariantCulture) + "\r\n"
                            + "Readback: identical\r\n"
                            + "Automatic restore: NO\r\n"
                            + "RUN: NO\r\n";
                        File.WriteAllText(Path.Combine(sessionDirectory, "pg33-multiblock-v152-report.txt"), report, Encoding.UTF8);
                        return "Projeto PG33 multibloco gravado e confirmado.\r\n\r\n"
                            + "Blocos: " + blocks.Count.ToString(CultureInfo.InvariantCulture)
                            + "\r\nACKs: " + ackedBlocks.ToString(CultureInfo.InvariantCulture) + "/" + blocks.Count.ToString(CultureInfo.InvariantCulture)
                            + "\r\nPalavras: " + actual.Count.ToString(CultureInfo.InvariantCulture)
                            + "\r\nReadback: IDENTICO AO PROJETO.\r\n\r\nO PLC permaneceu em STOP.";
                    }

                    confirmedMismatch = true;
                    AppendLogSafe("V152 VERIFY confirmou divergencia. Por seguranca, nenhum restore multibloco automatico sera enviado.");
                    break;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("V152 VERIFY rodada " + round.ToString(CultureInfo.InvariantCulture)
                        + " falhou sem escrita: " + ex.Message);
                }
                finally { ClosePort(port); }
            }

            if (confirmedMismatch)
                throw new InvalidDataException(
                    "Todos os " + blocks.Count.ToString(CultureInfo.InvariantCulture)
                    + " blocos receberam ACK 00 00 FF, mas o readback diverge do projeto atual. "
                    + "NAO houve retransmissao nem restore automatico. O backup original foi salvo na sessao; mantenha o TP02 em STOP.");

            throw new IOException(
                "Todos os " + blocks.Count.ToString(CultureInfo.InvariantCulture)
                + " blocos receberam ACK 00 00 FF, mas o readback final nao foi obtido. "
                + "Nenhum PG33 sera retransmitido e nenhum restore cego sera feito. "
                + "Use VERIFICAR GRAVACAO para reler sem escrever. Diagnostico: "
                + (last == null ? "desconhecido" : last.Message));
        }

'@

$shell = Replace-Section $shell $writeStart $writeEnd $writeReplacement 'StartProjectWriteV123 + multibloco V152'

# O VERIFY readback-only deve aceitar a mesma referencia longa, sem gerar TX PG33.
$oldVerifyCompile = @'
                byte[] unusedFrame = BuildValidatedProjectFrameV123(
                    out expected, out logicalCount, out compilationReport);
                if (unusedFrame == null || unusedFrame.Length == 0)
                    throw new InvalidDataException("Nao foi possivel preparar a referencia de verificacao.");
'@
$newVerifyCompile = @'
                IList<Pg33BlockV152> unusedPlan = BuildValidatedProjectPlanV152(
                    out expected, out logicalCount, out compilationReport);
                if (unusedPlan == null || unusedPlan.Count == 0)
                    throw new InvalidDataException("Nao foi possivel preparar a referencia de verificacao multibloco.");
'@
if (-not $shell.Contains($oldVerifyCompile)) {
    throw 'V152: trecho de compilacao do VERIFY v1.24 nao encontrado.'
}
$shell = $shell.Replace($oldVerifyCompile, $newVerifyCompile)
$shell = $shell.Replace('PG33 READBACK-ONLY VERIFY v1.24 iniciado em ', 'PG33 READBACK-ONLY VERIFY v1.52 iniciado em ')
$shell = $shell.Replace('PASS PG33 READBACK-ONLY VERIFY v1.24', 'PASS PG33 READBACK-ONLY VERIFY v1.52')

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 PG33 MultiBlock V152 aplicado: >80 words em blocos de ate 20 instrucoes/80 words, TX unica por bloco e verify read-only.' -ForegroundColor Cyan
