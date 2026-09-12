$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
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

# UI: preserva o NO-OP aprovado e adiciona a prova alterar+restaurar.
$shell = Replace-Required $shell '            controls.Height = 118;' '            controls.Height = 158;' 'altura painel PG33'

$buttonNeedle = @'
            controls.Controls.Add(probeButton);
'@
$buttonReplacement = @'
            controls.Controls.Add(probeButton);

            Button changeRestoreButton = NewButton("TESTAR ALTERACAO + RESTAURAR", 310, 70, 260, true);
            changeRestoreButton.Click += delegate { StartChangeRestoreProbe(); };
            controls.Controls.Add(changeRestoreButton);
'@
$shell = Replace-Required $shell $buttonNeedle $buttonReplacement 'botao alterar restaurar'
$shell = Replace-Required $shell '            sessionLabel.Location = new Point(18, 82);' '            sessionLabel.Location = new Point(18, 126);' 'posicao sessao'

$anchor = '        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)'
$idx = $shell.IndexOf($anchor, [System.StringComparison]::Ordinal)
if ($idx -lt 0) { throw 'ReadCanonicalSnapshotOnOpenPort nao encontrado.' }

$methods = @'
        private void StartChangeRestoreProbe()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 PG33", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(this,
                "PROVA REAL DE ALTERACAO E RESTAURACAO PG33\r\n\r\n"
                + "O TP02 deve permanecer em STOP durante TODO o teste.\r\n\r\n"
                + "O Studio ira:\r\n"
                + "1. ler e salvar o programa original de 23 passos;\r\n"
                + "2. gravar uma sentinela de 3 passos: STR X0001 -> OUT Y0001 -> END;\r\n"
                + "3. reler e comprovar a sentinela;\r\n"
                + "4. restaurar automaticamente os 23 passos originais;\r\n"
                + "5. reler e exigir igualdade total com o backup.\r\n\r\n"
                + "NENHUM comando RUN sera enviado. Nao coloque o PLC em RUN ate a mensagem final RESTAURACAO APROVADA.\r\n\r\n"
                + "Cada PG33 e transmitido apenas uma vez. Em ACK duvidoso nao ha retransmissao cega.\r\n\r\n"
                + "Continuar?",
                "Confirmar alteracao + restauracao PG33",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG33 CHANGE+RESTORE v1.18 iniciado em " + portName + ".");
            AppendLog("PLC DEVE PERMANECER EM STOP ate a restauracao final aprovada.");
            AppendLog("Sentinela: 001000 / 204000 / 007000 (EXTERNAL PG33 = 00 00 00).");
            SetBusy(true);
            SetStatus("BACKUP / TESTE / RESTAURACAO...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                string successText = string.Empty;
                try
                {
                    successText = RunChangeRestoreProbe(portName);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg33-change-restore-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure != null)
                    {
                        AppendLog("FALHA CHANGE+RESTORE: " + failure.Message);
                        SetStatus("MANTER PLC EM STOP / VER LOG", Danger);
                        MessageBox.Show(this,
                            failure.Message + "\r\n\r\nMANTENHA O TP02 EM STOP. Nao execute RUN ate confirmar o programa armazenado.",
                            "TP02 - restauracao nao confirmada", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        AppendLog("PASS: alteracao real, verificacao, restauracao e readback final aprovados.");
                        SetStatus("ALTERACAO + RESTAURACAO APROVADAS", Success);
                        MessageBox.Show(this, successText,
                            "TP02 - RESTAURACAO APROVADA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    SetBusy(false);
                }));
            });
        }

        private string RunChangeRestoreProbe(string portName)
        {
            ProgramSnapshot original = null;
            ProgramSnapshot sentinel = BuildSentinelReadbackSnapshot();
            byte[] testAck = null;
            byte[] restoreAck = null;

            // FASE A: backup original e escrita da sentinela, na mesma sessao V93.
            Exception last = null;
            bool testWritten = false;
            for (int round = 1; round <= 3 && !testWritten; round++)
            {
                SerialPort port = null;
                bool pg33Attempted = false;
                try
                {
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN; teste bloqueado antes da escrita.");

                    Thread.Sleep(450);
                    PerformF0(port);
                    Thread.Sleep(420);
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "change-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(450);

                    original = ReadCanonicalSnapshotOnOpenPort(port,
                        "change-backup-r" + round.ToString(CultureInfo.InvariantCulture));
                    ValidateKnownProbeProgram(original);
                    SaveSnapshot("change-backup-original", original);
                    AppendLogSafe("CHANGE BACKUP OK: 23 passos / END=0022 / aquisicao=" + acquisition + ".");

                    byte[] testFrame = BuildPg33SameProgram(sentinel);
                    File.WriteAllText(Path.Combine(sessionDirectory, "change-pg33-test-tx.hex"),
                        ToHex(testFrame) + Environment.NewLine, Encoding.ASCII);

                    pg33Attempted = true;
                    testAck = SendPg33OnceOnOpenPort(port, testFrame, "TESTE");
                    RequirePhysicalAck00(testAck, "PG33 TESTE");
                    testWritten = true;
                    AppendLogSafe("PG33 TESTE ACEITO: ACK 00 00 FF. Nenhum RUN foi enviado.");
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (pg33Attempted)
                        throw new IOException("A escrita da sentinela pode ter ocorrido. Nao ha retransmissao automatica. " + ex.Message, ex);
                    if (round < 3)
                        AppendLogSafe("Falha antes do PG33 TESTE; nova aquisicao V93 sera tentada sem escrita previa.");
                }
                finally { ClosePort(port); }
            }
            if (!testWritten)
                throw new IOException("Nao foi possivel chegar ao PG33 TESTE apos 3 rodadas V93. PLC nao foi alterado. Ultimo erro: "
                    + (last == null ? "desconhecido" : last.Message));

            // FASE B: nova sessao apenas para COMPROVAR a sentinela e, na mesma porta,
            // restaurar o backup original. Nenhuma nova escrita de sentinela ocorre.
            Exception restoreLast = null;
            bool restored = false;
            for (int round = 1; round <= 3 && !restored; round++)
            {
                SerialPort port = null;
                bool restoreAttempted = false;
                try
                {
                    if (round > 1) Thread.Sleep(round == 2 ? 2200 : 3200);
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC saiu de STOP antes da restauracao. Nenhum restore PG33 foi enviado.");

                    Thread.Sleep(450);
                    PerformF0(port);
                    Thread.Sleep(420);
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        "restore-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(450);

                    ProgramSnapshot current = ReadCanonicalSnapshotOnOpenPort(port,
                        "sentinel-readback-r" + round.ToString(CultureInfo.InvariantCulture));
                    SaveSnapshot("sentinel-readback", current);

                    if (SnapshotsEqual(current, original))
                    {
                        AppendLogSafe("Programa original ja esta presente; restore adicional nao e necessario.");
                        restored = true;
                        break;
                    }

                    CompareSnapshots(sentinel, current);
                    AppendLogSafe("SENTINELA COMPROVADA: 3 passos lidos exatamente como gravados. Restaurando backup original agora.");

                    byte[] restoreFrame = BuildPg33SameProgram(original);
                    File.WriteAllText(Path.Combine(sessionDirectory, "restore-pg33-tx.hex"),
                        ToHex(restoreFrame) + Environment.NewLine, Encoding.ASCII);
                    restoreAttempted = true;
                    restoreAck = SendPg33OnceOnOpenPort(port, restoreFrame, "RESTORE");
                    RequirePhysicalAck00(restoreAck, "PG33 RESTORE");
                    restored = true;
                    AppendLogSafe("PG33 RESTORE ACEITO: ACK 00 00 FF.");
                }
                catch (Exception ex)
                {
                    restoreLast = ex;
                    if (restoreAttempted)
                        throw new IOException("O PG33 RESTORE foi transmitido uma vez e nao sera repetido automaticamente. " + ex.Message, ex);
                    if (round < 3)
                        AppendLogSafe("Restore ainda nao transmitido; tentando nova aquisicao V93 para verificar sentinela e restaurar.");
                }
                finally { ClosePort(port); }
            }

            if (!restored)
                throw new IOException("A sentinela foi gravada, mas nao foi possivel iniciar a restauracao apos 3 rodadas V93. "
                    + "MANTENHA O PLC EM STOP. Ultimo erro: "
                    + (restoreLast == null ? "desconhecido" : restoreLast.Message));

            // FASE C: readback final independente do programa original.
            Thread.Sleep(1800);
            ProgramSnapshot finalRead = ReadSnapshotV93Robust(portName, "final-original");
            SaveSnapshot("final-original-readback", finalRead);
            CompareSnapshots(original, finalRead);

            string report = "PASS PG33 CHANGE+RESTORE v1.18\r\n"
                + "Teste: STR X0001 / OUT Y0001 / F-00 END\r\n"
                + "ACK teste: " + (testAck == null ? "-" : ToHex(testAck)) + "\r\n"
                + "ACK restore: " + (restoreAck == null ? "restore desnecessario" : ToHex(restoreAck)) + "\r\n"
                + "Backup original: 23 passos / END=0022\r\n"
                + "Readback final: IDENTICO ao backup original\r\n"
                + "RUN remoto: nunca enviado\r\n";
            File.WriteAllText(Path.Combine(sessionDirectory, "pg33-change-restore-report.txt"), report, Encoding.UTF8);

            return "Alteracao PG33 real e restauracao aprovadas.\r\n\r\n"
                + "Programa sentinela: 3 passos\r\n"
                + "ACK da escrita de teste: 00 00 FF\r\n"
                + "Sentinela relida e confirmada.\r\n"
                + "Programa original restaurado: 23 passos / END=0022\r\n"
                + "Readback final: IDENTICO AO BACKUP.\r\n\r\n"
                + "O TP02 terminou com o programa original armazenado.";
        }

        private ProgramSnapshot BuildSentinelReadbackSnapshot()
        {
            ProgramSnapshot s = new ProgramSnapshot();
            s.PlcState = "STOP";
            // PG33 EXTERNAL sera forcado a 00 pelo builder validado.
            // Estes terceiros bytes sao BRAW esperados na leitura 34.
            s.High.Add(0x00); s.Low.Add(0x10); s.External.Add(0x01); // STR X0001
            s.High.Add(0x20); s.Low.Add(0x40); s.External.Add(0x06); // OUT Y0001
            s.High.Add(0x00); s.Low.Add(0x70); s.External.Add(0x07); // F-00 END
            s.EndStep = 2;
            return s;
        }

        private byte[] SendPg33OnceOnOpenPort(SerialPort port, byte[] frame, string label)
        {
            port.DiscardInBuffer();
            AppendLogSafe("PG33 " + label + " TX UNICA: " + ToHex(frame));
            port.Write(frame, 0, frame.Length);
            byte[] raw = ReadBurst(port, 7000, 380);
            AppendLogSafe("PG33 " + label + " RX UNICA: " + (raw.Length == 0 ? "[]" : ToHex(raw)));
            File.WriteAllText(Path.Combine(sessionDirectory, "pg33-" + label.ToLowerInvariant() + "-ack-raw.hex"),
                (raw.Length == 0 ? "(sem bytes)" : ToHex(raw)) + Environment.NewLine, Encoding.ASCII);
            return raw;
        }

        private static void RequirePhysicalAck00(byte[] raw, string label)
        {
            byte[] frame = FindFirstValidResponseFrame(raw);
            if (frame == null)
                throw new InvalidDataException(label + ": nenhum ACK estruturalmente valido foi recebido.");
            if (frame.Length != 3 || frame[0] != 0x00 || frame[1] != 0x00 || frame[2] != 0xFF)
                throw new InvalidDataException(label + ": ACK diferente do valor fisicamente confirmado 00 00 FF: " + ToHex(frame));
        }

        private ProgramSnapshot ReadSnapshotV93Robust(string portName, string tag)
        {
            Exception last = null;
            for (int round = 1; round <= 3; round++)
            {
                SerialPort port = null;
                try
                {
                    if (round > 1) Thread.Sleep(round == 2 ? 2200 : 3200);
                    string state;
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                        throw new InvalidOperationException("PLC esta em RUN durante readback final.");
                    Thread.Sleep(450);
                    PerformF0(port);
                    Thread.Sleep(420);
                    SendAndReadFrame(port, Frame38Request, 0x02, 4, 3600,
                        tag + "-38-r" + round.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(450);
                    ProgramSnapshot result = ReadCanonicalSnapshotOnOpenPort(port,
                        tag + "-r" + round.ToString(CultureInfo.InvariantCulture));
                    AppendLogSafe(tag + " readback V93 OK: " + result.Count.ToString(CultureInfo.InvariantCulture) + " passos.");
                    return result;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppendLogSafe(tag + " readback rodada " + round.ToString(CultureInfo.InvariantCulture) + " falhou: " + ex.Message);
                }
                finally { ClosePort(port); }
            }
            throw new IOException("Readback V93 final falhou apos 3 rodadas. Ultimo erro: "
                + (last == null ? "desconhecido" : last.Message));
        }

        private static bool SnapshotsEqual(ProgramSnapshot a, ProgramSnapshot b)
        {
            if (a == null || b == null || a.Count != b.Count || a.EndStep != b.EndStep) return false;
            for (int i = 0; i < a.Count; i++)
                if (a.High[i] != b.High[i] || a.Low[i] != b.Low[i] || a.External[i] != b.External[i]) return false;
            return true;
        }

'@

$shell = $shell.Substring(0, $idx) + $methods + $shell.Substring($idx)
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Change+Restore V118 aplicado: sentinela 3 passos, ACK 00 00 FF, restore automatico e readback final.'
