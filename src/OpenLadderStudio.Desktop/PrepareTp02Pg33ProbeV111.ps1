$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$probePath = Join-Path (Get-Location) 'TP02Pg33NoOpProbeForm.cs.in'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $probePath)) { throw 'TP02Pg33NoOpProbeForm.cs.in nao encontrado.' }

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($text.Contains($needleCrLf)) { return $text.Replace($needleCrLf, $replacementCrLf) }
    if ($text.Contains($needleLf)) { return $text.Replace($needleLf, $replacementLf) }
    throw "Ancora nao encontrada ($label)."
}

$shell = [System.IO.File]::ReadAllText($shellPath)

# O V90 cria a entrada semantica do leitor PG. Inserimos a sonda logo antes dela.
$menuNeedle = '                plcMenu.DropDownItems.Add(DropItem("Leitor PG experimental...", delegate { ShowReader(); }));'
if (-not $shell.Contains($menuNeedle)) {
    $m = [System.Text.RegularExpressions.Regex]::Match($shell,
        '(?m)^(?<indent>[ \t]*)(?<owner>[A-Za-z_][A-Za-z0-9_]*)\.DropDownItems\.Add\(DropItem\("Leitor PG experimental\.\.\.",\s*delegate\s*\{\s*ShowReader\(\);\s*\}\)\);\s*$')
    if (-not $m.Success) { throw 'Item Leitor PG experimental nao encontrado.' }
    $menuNeedle = $m.Value
    $indent = $m.Groups['indent'].Value
    $owner = $m.Groups['owner'].Value
    $menuReplacement = $indent + $owner + '.DropDownItems.Add(DropItem("Validar escrita PG33 (sem alterar programa)...", delegate { ShowTp02Pg33NoOpProbe(); }));' + "`r`n" + $m.Value
    $shell = $shell.Substring(0, $m.Index) + $menuReplacement + $shell.Substring($m.Index + $m.Length)
}
else {
    $menuReplacement = '                plcMenu.DropDownItems.Add(DropItem("Validar escrita PG33 (sem alterar programa)...", delegate { ShowTp02Pg33NoOpProbe(); }));' + "`r`n" + $menuNeedle
    $shell = $shell.Replace($menuNeedle, $menuReplacement)
}

$methodNeedle = '        private void ShowReader()'
$methodInsert = @'
        private void ShowTp02Pg33NoOpProbe()
        {
            RefreshProfileUi();
            if (currentProfile == null || currentDriver == null ||
                !string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Selecione o controlador WEG TP02-60MR antes da prova PG33.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ladderForm == null || ladderForm.IsDisposed) ShowLadder();
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                MessageBox.Show(this, "O editor Ladder nao esta disponivel.", "OpenLadder Studio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (TP02Pg33NoOpProbeForm dialog = new TP02Pg33NoOpProbeForm(currentProfile, ladderForm))
            {
                dialog.ShowDialog(this);
            }
            statusText.Text = "Prova PG33 TP02 encerrada";
        }

'@
$idx = $shell.IndexOf($methodNeedle, [System.StringComparison]::Ordinal)
if ($idx -lt 0) { throw 'Metodo ShowReader nao encontrado para inserir PG33 probe.' }
$shell = $shell.Substring(0, $idx) + $methodInsert + $shell.Substring($idx)

# Carrega o template da sonda e aplica todos os guardrails antes de incorpora-lo
# ao shell temporario. O template usa .cs.in para nao ser tratado como unidade
# standalone pelo validador de projeto.
$probe = [System.IO.File]::ReadAllText($probePath)

$dpiNeedle = '            AutoScaleMode = AutoScaleMode.Dpi;'
$dpiReplacement = '            AutoScaleDimensions = new SizeF(96F, 96F);' + "`r`n" + $dpiNeedle
$probe = Replace-Required $probe $dpiNeedle $dpiReplacement 'AutoScaleDimensions PG33 probe'

# A sonda v1.11 tinha uma rotina de snapshot mais curta que o leitor PG v1.10
# validado fisicamente. A v1.12 replica o mesmo warm-up, tempos de estabilizacao
# e intervalos entre HELLO/F0/38/34 antes de qualquer possibilidade de PG33.
$robustNeedle = @'
        private ProgramSnapshot ReadSnapshotRobust(string portName, string tag)
        {
            Exception last = null;
'@
$robustReplacement = @'
        private ProgramSnapshot ReadSnapshotRobust(string portName, string tag)
        {
            WarmUpLink(portName, tag);
            Exception last = null;
'@
$probe = Replace-Required $probe $robustNeedle $robustReplacement 'warm-up antes do snapshot'

$retryNeedle = '                    if (session > 1) Thread.Sleep(1300 + (session * 250));'
$retryReplacement = @'
                    if (session > 1)
                    {
                        AppendLogSafe(tag + " PG AUTO-RETRY: preparando sessao " + session.ToString(CultureInfo.InvariantCulture) + ".");
                        Thread.Sleep(session == 2 ? 1500 : 2000);
                    }
'@
$probe = Replace-Required $probe $retryNeedle $retryReplacement 'retry igual ao leitor v1.10'

$settleNeedle = '                Thread.Sleep(sessionNumber == 1 ? 1500 : 1900);'
$settleReplacement = @'
                int settle = sessionNumber == 1 ? 1600 : (sessionNumber == 2 ? 1900 : 2300);
                Thread.Sleep(settle);
                AppendLogSafe(tag + " COM aberta | sessao " + sessionNumber.ToString(CultureInfo.InvariantCulture)
                    + " | estabilizacao " + settle.ToString(CultureInfo.InvariantCulture) + " ms.");
'@
$probe = Replace-Required $probe $settleNeedle $settleReplacement 'estabilizacao por sessao'

$helloGapNeedle = '                string state = PerformHello(port);'
$helloGapReplacement = $helloGapNeedle + "`r`n" + '                Thread.Sleep(450);'
# Ha duas ocorrencias (snapshot e write preflight); substituir ambas e desejado.
$probe = $probe.Replace($helloGapNeedle, $helloGapReplacement)

$f0GapNeedle = '                PerformF0(port);'
$f0GapReplacement = $f0GapNeedle + "`r`n" + '                Thread.Sleep(420);'
$probe = $probe.Replace($f0GapNeedle, $f0GapReplacement)

$frame38SnapshotNeedle = '                SendAndReadFrame(port, Frame38Request, 0x02, 4, 3800, tag + "-38");'
$frame38SnapshotReplacement = $frame38SnapshotNeedle + "`r`n" + '                Thread.Sleep(450);'
$probe = Replace-Required $probe $frame38SnapshotNeedle $frame38SnapshotReplacement 'intervalo apos 38 snapshot'

$frame38WriteNeedle = '                SendAndReadFrame(port, Frame38Request, 0x02, 4, 3800, "write-preflight-38");'
$frame38WriteReplacement = $frame38WriteNeedle + "`r`n" + '                Thread.Sleep(450);'
$probe = Replace-Required $probe $frame38WriteNeedle $frame38WriteReplacement 'intervalo apos 38 write'

# O endereco do comando 34 deve usar LOW, HIGH, exatamente como o leitor v1.10
# fisicamente validado. Em pagina zero ambos eram 00, por isso a divergencia
# nao explicava a falha atual, mas seria incorreta em paginas posteriores.
$pageOrderNeedle = @'
            frame[2] = (byte)((startStep >> 8) & 0xFF);
            frame[3] = (byte)(startStep & 0xFF);
'@
$pageOrderReplacement = @'
            frame[2] = (byte)(startStep & 0xFF);
            frame[3] = (byte)((startStep >> 8) & 0xFF);
'@
$probe = Replace-Required $probe $pageOrderNeedle $pageOrderReplacement 'ordem LOW HIGH do comando 34'

# Warm-up identico ao leitor PG v1.10: somente CON-ICB, fecha a porta e depois
# inicia uma sessao limpa. Nenhum comando de escrita e enviado no warm-up.
$warmupAnchor = '        private ProgramSnapshot ReadSnapshotRobust(string portName, string tag)'
$warmupMethod = @'
        private void WarmUpLink(string portName, string tag)
        {
            SerialPort port = null;
            try
            {
                AppendLogSafe(tag + " PG WARM-UP: pre-estabilizando TP-232PG somente com HELLO.");
                port = OpenPort(portName);
                Thread.Sleep(1400);
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    port.DiscardInBuffer();
                    port.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] raw = ReadBurst(port, 2200, 230);
                    AppendLogSafe(tag + " WARM-UP HELLO " + attempt.ToString(CultureInfo.InvariantCulture)
                        + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));
                    if (Contains(raw, HelloStop) || Contains(raw, HelloRun))
                    {
                        AppendLogSafe(tag + " PG WARM-UP confirmado; iniciando sessao limpa.");
                        break;
                    }
                    Thread.Sleep(300);
                }
            }
            catch (Exception ex)
            {
                AppendLogSafe(tag + " PG WARM-UP nao confirmou link: " + ex.Message
                    + ". O retry completo continuara.");
            }
            finally
            {
                ClosePort(port);
            }
            Thread.Sleep(1100);
        }

'@
$warmupIndex = $probe.IndexOf($warmupAnchor, [System.StringComparison]::Ordinal)
if ($warmupIndex -lt 0) { throw 'ReadSnapshotRobust nao encontrado para inserir WarmUpLink.' }
$probe = $probe.Substring(0, $warmupIndex) + $warmupMethod + $probe.Substring($warmupIndex)

# Antes do preflight de escrita, condiciona o TP-232PG novamente, pois o teste
# fisico mostrou que uma nova abertura da COM pode voltar ao estado intermitente.
$executeNeedle = @'
        private byte[] ExecutePg33(string portName, byte[] frame)
        {
            SerialPort port = null;
'@
$executeReplacement = @'
        private byte[] ExecutePg33(string portName, byte[] frame)
        {
            WarmUpLink(portName, "write-preflight");
            SerialPort port = null;
'@
$probe = Replace-Required $probe $executeNeedle $executeReplacement 'warm-up antes do PG33'
$probe = Replace-Required $probe '                Thread.Sleep(1800);' '                Thread.Sleep(2000);' 'settle write preflight'

# HELLO/F0 passam a registrar RX bruto e usam o mesmo primeiro timeout do leitor
# v1.10, permitindo diagnosticar sem repetir a operacao de escrita.
$helloReadNeedle = '                byte[] raw = ReadBurst(port, 3000, 240);'
$helloReadReplacement = @'
                byte[] raw = ReadBurst(port, attempt == 1 ? 2600 : 3000, 240);
                AppendLogSafe("HELLO tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));
'@
$probe = Replace-Required $probe $helloReadNeedle $helloReadReplacement 'log HELLO bruto'

$f0ReadNeedle = '                byte[] raw = ReadBurst(port, 3600, 250);'
$f0ReadReplacement = @'
                byte[] raw = ReadBurst(port, 3600, 250);
                AppendLogSafe("F0 tentativa " + attempt.ToString(CultureInfo.InvariantCulture)
                    + " RX=" + (raw.Length == 0 ? "[]" : ToHex(raw)));
'@
$probe = Replace-Required $probe $f0ReadNeedle $f0ReadReplacement 'log F0 bruto'

# Gate fisico: injecao por uma linha de codigo ASCII estavel, sem depender de
# acentos/normalizacao Unicode do texto ao redor.
$saveNeedle = '                    SaveSnapshot("backup-before", before);'
$saveReplacement = '                    ValidateKnownProbeProgram(before);' + "`r`n" + $saveNeedle
$probe = Replace-Required $probe $saveNeedle $saveReplacement 'gate assinatura fisica'

# BRAW lido pelo 34 nao e EXTERNAL usado pelo PG33. Para a assinatura canonica
# de 23 passos todos os EXTERNAL foram confirmados offline como 00.
$externalNeedle = '            for (int i = 0; i < w; i++) frame[p++] = snapshot.External[i];'
$externalReplacement = '            for (int i = 0; i < w; i++) frame[p++] = 0x00;'
$probe = Replace-Required $probe $externalNeedle $externalReplacement 'BRAW diferente de EXTERNAL'

# Nunca aceitar o proprio quadro 0x33 eventualmente ecoado pela interface como
# resposta do PLC. Um ACK fisico deve ser outro quadro estruturalmente valido.
$ackNeedle = @'
            for (int start = 0; start <= raw.Length - 3; start++)
            {
                int len = raw[start + 1];
'@
$ackReplacement = @'
            for (int start = 0; start <= raw.Length - 3; start++)
            {
                if (raw[start] == 0x33) continue;
                int len = raw[start + 1];
'@
$probe = Replace-Required $probe $ackNeedle $ackReplacement 'ignorar eco PG33 como ACK'

$buildAnchor = '        private static byte[] BuildPg33SameProgram(ProgramSnapshot snapshot)'
$validationMethod = @'
        private static void ValidateKnownProbeProgram(ProgramSnapshot snapshot)
        {
            // Assinatura do programa fisico de 23 passos validado na bancada em v1.10.
            // O gate impede que um programa arbitrario seja convertido para PG33 antes
            // de o mapeamento BRAW -> EXTERNAL estar generalizado.
            string[] expected = new string[]
            {
                "0010", "0060", "8768", "4040", "0011", "0012", "0168", "800A",
                "4041", "0013", "1771", "C880", "0014", "1871", "C880", "0015",
                "0D77", "F001", "F000", "800A", "0016", "2041", "0070"
            };

            if (snapshot == null || snapshot.Count != expected.Length || snapshot.EndStep != 22)
                throw new InvalidOperationException(
                    "PG33 BLOQUEADO: esta primeira prova fisica exige exatamente o programa canonico de 23 passos (END=0022) ja validado em leitura.");

            for (int i = 0; i < expected.Length; i++)
            {
                string actual = snapshot.High[i].ToString("X2", CultureInfo.InvariantCulture)
                    + snapshot.Low[i].ToString("X2", CultureInfo.InvariantCulture);
                if (!string.Equals(actual, expected[i], StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "PG33 BLOQUEADO: assinatura diferente no passo "
                        + i.ToString("0000", CultureInfo.InvariantCulture)
                        + ". Esperado " + expected[i] + ", lido " + actual + ".");
            }
        }

'@
$buildIndex = $probe.IndexOf($buildAnchor, [System.StringComparison]::Ordinal)
if ($buildIndex -lt 0) { throw 'BuildPg33SameProgram nao encontrado.' }
$probe = $probe.Substring(0, $buildIndex) + $validationMethod + $probe.Substring($buildIndex)

# Incorpora a sonda ao shell temporario sem alterar a lista gigante do csc.
$probeLines = $probe -split "`r?`n"
$usingLines = New-Object System.Collections.Generic.List[string]
$bodyLines = New-Object System.Collections.Generic.List[string]
foreach ($line in $probeLines) {
    if ($line -match '^using\s+.+;\s*$') { [void]$usingLines.Add($line) }
    else { [void]$bodyLines.Add($line) }
}
$prefix = [string]::Join("`r`n", $usingLines.ToArray()) + "`r`n"
$body = [string]::Join("`r`n", $bodyLines.ToArray()).Trim()
$shell = $prefix + $shell.TrimStart([char]0xFEFF) + "`r`n`r`n" + $body + "`r`n"

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG33 Probe V112 aplicado: warm-up v1.10, retry robusto, LOW/HIGH 34, gate, DPI, EXTERNAL seguro e anti-eco.'
