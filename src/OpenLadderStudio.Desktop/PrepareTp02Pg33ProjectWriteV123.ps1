$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}

$buttonNeedle = @'
            controls.Controls.Add(changeRestoreButton);
'@
$buttonReplacement = @'
            controls.Controls.Add(changeRestoreButton);

            Button writeProjectButton = NewButton("GRAVAR PROJETO ATUAL", 590, 70, 230, true);
            writeProjectButton.Click += delegate { StartProjectWriteV123(); };
            controls.Controls.Add(writeProjectButton);
'@
$shell = Replace-Required $shell $buttonNeedle $buttonReplacement 'botao gravar projeto PG33'

$anchor = '        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)'
$index = $shell.IndexOf($anchor, [System.StringComparison]::Ordinal)
if ($index -lt 0) { throw 'ReadCanonicalSnapshotOnOpenPort nao encontrado.' }

$methods = @'
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
            byte[] projectFrame;
            try
            {
                projectFrame = BuildValidatedProjectFrameV123(out expected, out logicalCount, out compilationReport);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Gravacao PG33 bloqueada",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "GRAVAR PROJETO ATUAL NO TP02 POR PG33\r\n\r\n"
                + "PLC obrigatoriamente em STOP.\r\n"
                + "Instrucoes logicas: " + logicalCount.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Palavras de maquina: " + expected.Count.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n"
                + "O Studio ira salvar o programa atual, transmitir UM quadro PG33, reler e comparar. "
                + "Se uma divergencia de conteudo for confirmada, tentara restaurar o backup uma unica vez.\r\n\r\n"
                + "Use somente em bancada segura. Continuar?",
                "Confirmar gravacao PG33", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG33 PROJECT WRITE v1.23 iniciado em " + portName + ".");
            AppendLog(compilationReport);
            SetBusy(true);
            SetStatus("BACKUP / GRAVACAO / VERIFY...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string successText = string.Empty;
                try
                {
                    successText = RunProjectWriteV123(portName, projectFrame, expected, logicalCount);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg33-project-write-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure == null)
                    {
                        AppendLog("PASS: projeto gravado e confirmado por readback.");
                        SetStatus("PROJETO GRAVADO / VERIFY OK", Success);
                        MessageBox.Show(this, successText, "TP02 - GRAVACAO PG33 APROVADA",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AppendLog("FALHA: " + failure.Message);
                        SetStatus("MANTER PLC EM STOP / VER LOG", Danger);
                        MessageBox.Show(this, failure.Message + "\r\n\r\nMANTENHA O TP02 EM STOP ate confirmar o programa pelo PC12.",
                            "TP02 - gravacao PG33 nao confirmada", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    SetBusy(false);
                }));
            });
        }

        private string RunProjectWriteV123(string portName, byte[] projectFrame,
            ProgramSnapshot expected, int logicalCount)
        {
            ProgramSnapshot backup = null;
            byte[] writeAck = null;
            bool transmitted = false;
            Exception last = null;

            for (int round = 1; round <= 5 && !transmitted; round++)
            {
                SerialPort port = null;
                bool attempted = false;
                try
                {
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN; gravacao bloqueada.");
                    PerformF0WriteQualified(port, "V123-PROJECT");
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3200,
                        "project-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    backup = ReadCanonicalSnapshotOnOpenPort(port,
                        "project-backup-r" + round.ToString(CultureInfo.InvariantCulture));
                    if (backup.Count < 1 || backup.EndStep < 0)
                        throw new InvalidDataException("Backup PG invalido: END ausente.");
                    if (backup.Count > 80)
                        throw new InvalidOperationException("Esta versao aceita backup/restauracao de ate 80 palavras. PLC atual tem "
                            + backup.Count.ToString(CultureInfo.InvariantCulture) + ". Nenhum PG33 foi enviado.");
                    SaveSnapshot("project-backup-original", backup);
                    File.WriteAllText(Path.Combine(sessionDirectory, "project-pg33-tx.hex"),
                        ToHex(projectFrame) + Environment.NewLine, Encoding.ASCII);

                    attempted = true;
                    writeAck = SendPg33OnceOnOpenPort(port, projectFrame, "PROJECT");
                    RequirePhysicalAck00(writeAck, "PG33 PROJECT");
                    transmitted = true;
                    AppendLogSafe("PG33 PROJECT ACEITO: ACK 00 00 FF. Nenhum RUN foi enviado.");
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (attempted)
                        throw new IOException("PG33 PROJECT foi transmitido uma vez e nao sera repetido automaticamente. " + ex.Message, ex);
                }
                finally { ClosePort(port); }
            }
            if (!transmitted)
                throw new IOException("Nao foi possivel concluir backup/preflight apos 5 rodadas. Nenhum PG33 foi enviado. Ultimo erro: "
                    + (last == null ? "desconhecido" : last.Message));

            ProgramSnapshot actual = null;
            last = null;
            for (int round = 1; round <= 5; round++)
            {
                SerialPort port = null;
                try
                {
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC saiu de STOP antes do verify.");
                    PerformF0WriteQualified(port, "V123-VERIFY");
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3200,
                        "project-verify-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    actual = ReadCanonicalSnapshotOnOpenPort(port,
                        "project-verify-r" + round.ToString(CultureInfo.InvariantCulture));
                    SaveSnapshot("project-readback", actual);
                    if (SnapshotsEqual(expected, actual)) break;

                    AppendLogSafe("VERIFY confirmou divergencia. Restaurando backup original uma unica vez.");
                    byte[] restore = BuildPg33SameProgram(backup);
                    File.WriteAllText(Path.Combine(sessionDirectory, "project-restore-pg33-tx.hex"),
                        ToHex(restore) + Environment.NewLine, Encoding.ASCII);
                    byte[] restoreAck = SendPg33OnceOnOpenPort(port, restore, "PROJECT-RESTORE");
                    RequirePhysicalAck00(restoreAck, "PG33 PROJECT RESTORE");
                    throw new InvalidDataException("O readback divergiu. O backup original recebeu RESTORE ACK 00 00 FF; confirme pelo PC12.");
                }
                catch (InvalidDataException) { throw; }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe("VERIFY rodada " + round.ToString(CultureInfo.InvariantCulture) + " falhou: " + ex.Message);
                }
                finally { ClosePort(port); }
            }

            if (actual == null || !SnapshotsEqual(expected, actual))
                throw new IOException("O projeto recebeu ACK 00 00 FF, mas o readback final nao foi obtido. "
                    + "Nao houve retransmissao nem restore cego. Diagnostico: "
                    + (last == null ? "desconhecido" : last.Message));

            string report = "PASS PG33 PROJECT WRITE v1.23\r\n"
                + "Instrucoes logicas: " + logicalCount.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Palavras: " + expected.Count.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "ACK: " + ToHex(writeAck) + "\r\n"
                + "Readback: identico ao projeto compilado\r\n"
                + "RUN remoto: nunca enviado\r\n";
            File.WriteAllText(Path.Combine(sessionDirectory, "pg33-project-write-report.txt"), report, Encoding.UTF8);
            return "Projeto gravado por PG33 e confirmado.\r\n\r\n"
                + "Instrucoes logicas: " + logicalCount.ToString(CultureInfo.InvariantCulture)
                + "\r\nPalavras: " + expected.Count.ToString(CultureInfo.InvariantCulture)
                + "\r\nACK: 00 00 FF\r\nReadback: IDENTICO AO PROJETO.\r\n\r\nO PLC permaneceu em STOP.";
        }

        private byte[] BuildValidatedProjectFrameV123(out ProgramSnapshot expected,
            out int logicalCount, out string report)
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
            if (compilation.Words.Count < 1 || compilation.Words.Count > 80)
                throw new InvalidDataException("PG33 v1.23 aceita 1..80 palavras; projeto possui "
                    + compilation.Words.Count.ToString(CultureInfo.InvariantCulture) + ".");

            logicalCount = ValidatePhysicalSubsetV123(compilation.Words);
            if (logicalCount > 20)
                throw new InvalidDataException("PG33 v1.23 aceita no maximo 20 instrucoes logicas em um quadro; projeto possui "
                    + logicalCount.ToString(CultureInfo.InvariantCulture) + ".");

            expected = SnapshotFromCompiledWordsV123(compilation.Words);
            if (expected.EndStep != expected.Count - 1)
                throw new InvalidDataException("F-00 END precisa ser a ultima palavra do projeto.");
            return BuildPg33CompiledV123(compilation.Words);
        }

        private static int ValidatePhysicalSubsetV123(IList<Tp02MachineWord> words)
        {
            int i = 0;
            int logical = 0;
            while (i < words.Count)
            {
                Tp02MachineWord w = words[i];
                int span = 0;
                int opcode = w.Low & 0x78;
                int deviceBase = w.High & 0x60;
                if ((w.High & 0x80) == 0 && (deviceBase == 0x00 || deviceBase == 0x20 || deviceBase == 0x40)
                    && (opcode == 0x10 || opcode == 0x18 || opcode == 0x20 || opcode == 0x28
                        || opcode == 0x30 || opcode == 0x38 || opcode == 0x40))
                    span = 1;
                else if ((w.High & 0x80) == 0 && (opcode == 0x60 || opcode == 0x68))
                {
                    span = 2;
                    RequireWordV123(words, i + 1, IsLiteralV123, "preset literal de TMR/CNT");
                }
                else if ((w.High == 0x17 || w.High == 0x18) && w.Low == 0x71)
                {
                    span = 2;
                    RequireWordV123(words, i + 1, IsValidatedYBitV123, "operando Y de SET/RST");
                }
                else if (w.High == 0x0D && w.Low == 0x77)
                {
                    span = 4;
                    RequireWordV123(words, i + 1, IsDOperandV123, "primeiro D de F-13w");
                    RequireWordV123(words, i + 2, IsDOperandV123, "segundo D de F-13w");
                    RequireWordV123(words, i + 3, IsLiteralV123, "literal de F-13w");
                }
                else if (w.High == 0x00 && w.Low == 0x70)
                    span = 1;
                else
                    throw new InvalidDataException("Instrucao ainda nao validada fisicamente no passo "
                        + i.ToString("0000", CultureInfo.InvariantCulture) + ": " + w.ToHex() + ".");

                if (i + span > words.Count)
                    throw new InvalidDataException("Instrucao incompleta no passo " + i.ToString("0000", CultureInfo.InvariantCulture) + ".");
                logical++;
                i += span;
            }
            return logical;
        }

        private delegate bool WordPredicateV123(Tp02MachineWord word);

        private static void RequireWordV123(IList<Tp02MachineWord> words, int index,
            WordPredicateV123 predicate, string label)
        {
            if (index >= words.Count || !predicate(words[index]))
                throw new InvalidDataException("Projeto fora do subconjunto PG33 validado: " + label + ".");
        }

        private static bool IsLiteralV123(Tp02MachineWord w)
        {
            return w.High >= 0x80 && w.High <= 0x9F && (w.Low & 0x80) == 0;
        }

        private static bool IsDOperandV123(Tp02MachineWord w)
        {
            return (w.High & 0xF8) == 0xF0 && (w.Low & 0x80) == 0;
        }

        private static bool IsValidatedYBitV123(Tp02MachineWord w)
        {
            return (w.High & 0xF8) == 0xC8 && (w.Low & 0x80) != 0;
        }

        private static ProgramSnapshot SnapshotFromCompiledWordsV123(IList<Tp02MachineWord> words)
        {
            ProgramSnapshot snapshot = new ProgramSnapshot();
            snapshot.PlcState = "STOP";
            for (int i = 0; i < words.Count; i++)
            {
                snapshot.High.Add(words[i].High);
                snapshot.Low.Add(words[i].Low);
                int braw = (words[i].High >> 4) + (words[i].High & 0x0F)
                    + (words[i].Low >> 4) + (words[i].Low & 0x0F);
                snapshot.External.Add((byte)(braw & 0x0F));
                if (words[i].High == 0x00 && words[i].Low == 0x70) snapshot.EndStep = i;
            }
            return snapshot;
        }

        private static byte[] BuildPg33CompiledV123(IList<Tp02MachineWord> words)
        {
            int count = words.Count;
            int len = (3 * count) + 4;
            byte[] frame = new byte[len + 3];
            frame[0] = 0x33;
            frame[1] = checked((byte)len);
            frame[2] = 0x00;
            frame[3] = 0x00;
            frame[4] = 0x00;
            frame[5] = checked((byte)(2 * count));
            int p = 6;
            for (int i = 0; i < count; i++)
            {
                frame[p++] = words[i].High;
                frame[p++] = words[i].Low;
            }
            for (int i = 0; i < count; i++) frame[p++] = 0x00;
            int sum = 0;
            for (int i = 0; i < frame.Length - 1; i++) sum = (sum + frame[i]) & 0xFF;
            frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);
            return frame;
        }

'@

$shell = $shell.Substring(0, $index) + $methods + $shell.Substring($index)
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Project Write V123 aplicado: projeto atual, subset validado, backup, TX unica e readback.'
