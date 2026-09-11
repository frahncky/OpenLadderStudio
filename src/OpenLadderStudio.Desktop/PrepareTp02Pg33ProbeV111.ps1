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

            using (TP02Pg33NoOpProbeForm dialog = new TP02Pg33NoOpProbeForm(currentProfile))
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
Write-Host 'TP02 PG33 Physical No-Op Probe V111 aplicado com gate, DPI, EXTERNAL seguro e protecao contra eco.'
