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

# v1.11: a bancada confirmou que 0A e uma LEITURA que devolve dados. O pedido
# 0A 03 [end_hi][end_lo][qtd] chk devolve resposta CMD=00 LEN=qtd payload. Aqui
# entra o tipo de etapa read_sweep_0a: a partir de um quadro 0A base, varre
# enderecos contiguos (passo = qtd) montando novos quadros 0A com checksum e
# capturando cada resposta, com retentativa por endereco. Trava intrinseca: o
# motor so consegue emitir CMD 0A (nunca escrita/apagamento/firmware).

# 1) tratador do novo tipo, inserido antes do gate padrao de RunPostHandshakeSteps
$text = Replace-Required $text @'
                string reason;
                if (!IsStepAllowed(step, readOnlyApproved, out reason))
                {
                    LogEvent("BLOQUEIO", step.name + " - " + reason, string.Empty, null, totalWatch.ElapsedMilliseconds);
                    continue;
                }
'@ @'
                if (type == "READ_SWEEP_0A")
                {
                    RunRead0ASweep(port, step, readOnlyApproved, totalWatch);
                    continue;
                }

                string reason;
                if (!IsStepAllowed(step, readOnlyApproved, out reason))
                {
                    LogEvent("BLOQUEIO", step.name + " - " + reason, string.Empty, null, totalWatch.ElapsedMilliseconds);
                    continue;
                }
'@ 'Tratador do tipo read_sweep_0a'

# 2) metodos da varredura, inseridos antes de WaitTxDrain
$text = Replace-Required $text @'
        private static void WaitTxDrain(SerialPort port, int maxMs)
'@ @'
        private void RunRead0ASweep(SerialPort port, PgLabStep step, bool readOnlyApproved, Stopwatch totalWatch)
        {
            if (!readOnlyApproved)
            {
                LogEvent("BLOQUEIO", step.name + " - varredura 0A exige o modo READ-ONLY", string.Empty, null, totalWatch.ElapsedMilliseconds);
                return;
            }
            byte[] baseFrame = ParseHex(step.txHex);
            if (baseFrame.Length != 6 || baseFrame[0] != 0x0A || baseFrame[1] != 0x03 || Sum8(baseFrame) != 0xFF)
            {
                LogEvent("BLOQUEIO", step.name + " - base precisa ser um quadro 0A 03 valido com soma FF", string.Empty, null, totalWatch.ElapsedMilliseconds);
                return;
            }
            int startAddr = (baseFrame[2] << 8) | baseFrame[3];
            int readLen = baseFrame[4];
            if (readLen <= 0) readLen = 1;
            int maxReads = 40;
            int perAddrTries = 3;
            int perTimeout = step.timeoutMs > 0 ? step.timeoutMs : 3500;
            int gap = step.passiveAfterMs > 0 ? step.passiveAfterMs : 200;
            int silentStreak = 0;
            LogEvent("VARREDURA", "0A read-only: inicio=0x" + startAddr.ToString("X4", CultureInfo.InvariantCulture) + " tamanho=0x" + readLen.ToString("X2", CultureInfo.InvariantCulture) + " leituras=" + maxReads.ToString(CultureInfo.InvariantCulture) + " tentativas/end=" + perAddrTries.ToString(CultureInfo.InvariantCulture), string.Empty, null, totalWatch.ElapsedMilliseconds);
            for (int n = 0; n < maxReads && !cancelRequested; n++)
            {
                int addr = (startAddr + n * readLen) & 0xFFFF;
                byte[] tx = BuildRead0AFrame(addr, readLen);
                if (tx[0] != 0x0A || tx.Length != 6) break;
                string addrHex = "0x" + addr.ToString("X4", CultureInfo.InvariantCulture);
                string txHexN = ToHex(tx);
                if (IsBuiltInBlocked(txHexN) || PackageBlocked(txHexN))
                {
                    LogEvent("BLOQUEIO", step.name + " " + addrHex + " - quadro na denylist; nao transmitido", string.Empty, null, totalWatch.ElapsedMilliseconds);
                    continue;
                }
                byte[] raw = new byte[0];
                byte[] noEcho = new byte[0];
                Stopwatch sw = Stopwatch.StartNew();
                for (int t = 1; t <= perAddrTries && !cancelRequested; t++)
                {
                    port.DiscardInBuffer();
                    port.Write(tx, 0, tx.Length);
                    if (t == 1) RecordFrame("TX", step.name + " " + addrHex, tx, sw.ElapsedMilliseconds);
                    raw = ReadBurst(port, perTimeout, 250);
                    noEcho = RemoveLeadingExactEcho(raw, tx);
                    if (noEcho.Length > 0) break;
                    if (t < perAddrTries) Thread.Sleep(gap);
                }
                sw.Stop();
                if (noEcho.Length == 0)
                {
                    LogEvent("RX", addrHex + " -> [] apos " + perAddrTries.ToString(CultureInfo.InvariantCulture) + " tentativas (sem eco)", string.Empty, null, sw.ElapsedMilliseconds);
                    silentStreak++;
                    if (silentStreak >= 6)
                    {
                        LogEvent("VARREDURA", "6 enderecos silenciosos seguidos; encerrando em " + addrHex, string.Empty, null, totalWatch.ElapsedMilliseconds);
                        break;
                    }
                }
                else
                {
                    silentStreak = 0;
                    RecordFrame("RX RAW", step.name + " " + addrHex, noEcho, sw.ElapsedMilliseconds);
                }
                if (gap > 0 && !cancelRequested) Thread.Sleep(gap);
            }
            LogEvent("VARREDURA", "varredura 0A concluida", string.Empty, null, totalWatch.ElapsedMilliseconds);
        }

        private static byte[] BuildRead0AFrame(int addr, int readLen)
        {
            byte hi = (byte)((addr >> 8) & 0xFF);
            byte lo = (byte)(addr & 0xFF);
            byte len = (byte)(readLen & 0xFF);
            int sum = (0x0A + 0x03 + hi + lo + len) & 0xFF;
            byte chk = (byte)((0xFF - sum) & 0xFF);
            return new byte[] { 0x0A, 0x03, hi, lo, len, chk };
        }

        private static void WaitTxDrain(SerialPort port, int maxMs)
'@ 'Metodos RunRead0ASweep e BuildRead0AFrame'

# 3) versao do motor
if (-not $text.Contains('        private const string EngineVersion = "1.10";')) { throw 'EngineVersion 1.10 nao encontrado apos F0RetryV20.' }
$text = $text.Replace('        private const string EngineVersion = "1.10";', '        private const string EngineVersion = "1.11";')

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.11 aplicado: varredura de enderecos 0A read-only (read_sweep_0a).'
