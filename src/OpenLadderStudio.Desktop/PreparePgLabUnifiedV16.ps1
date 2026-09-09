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

# v1.6: um clique executa toda a cadeia de descoberta segura.
$text = Replace-Required $text '            Text = "OpenLadder Studio - Laboratorio PG TP02";' '            Text = "OpenLadder Studio - Teste Unico TP02";' 'Titulo da janela'
$text = Replace-Required $text '            Label title = NewLabel("LABORATORIO PG - WEG TP02", 16.0f, FontStyle.Bold, Navy);' '            Label title = NewLabel("TESTE UNICO PG - WEG TP02", 16.0f, FontStyle.Bold, Navy);' 'Titulo principal'
$text = Replace-Required $text '            Label subtitle = NewLabel("Motor permanente de testes + pacote de protocolo atualizavel", 9.0f, FontStyle.Regular, TextSecondary);' '            Label subtitle = NewLabel("Um clique: localizar enlace, testar consultas de leitura e gerar um relatorio unico", 9.0f, FontStyle.Regular, TextSecondary);' 'Subtitulo'
$text = Replace-Required $text '            runButton = ButtonAt("EXECUTAR TESTE COMPLETO", 490, 31, 225, true);' '            runButton = ButtonAt("EXECUTAR TESTE UNICO TP02", 490, 31, 225, true);' 'Botao principal'

$text = Replace-Required $text '            allowReadOnly.Text = "Permitir consultas READ-ONLY do pacote (inclui 38 apos F0 validado)";' '            allowReadOnly.Text = "Teste unico: somente consultas READ-ONLY da allowlist (ativo)";' 'Rotulo READ-ONLY'
$text = Replace-Required $text @'
            config.Controls.Add(allowReadOnly);

            Label safety = LabelAt("Modo padrao: HANDSHAKE e captura passiva. READ_ONLY_CANDIDATE exige F0 validado na mesma sessao, permissao manual e allowlist explicita.", 20, 160, Danger, true);
'@ @'
            config.Controls.Add(allowReadOnly);
            allowReadOnly.Checked = true;
            allowReadOnly.Enabled = false;

            Label safety = LabelAt("TESTE UNICO: nunca envia escrita, RUN/STOP remoto, download, apagamento ou firmware. Somente HELLO e consultas READ-ONLY explicitamente permitidas.", 20, 160, Danger, true);
'@ 'Modo unico e aviso de seguranca'

$text = Replace-Required $text '            bool readOnlyApproved = allowReadOnly.Checked;' '            bool readOnlyApproved = true;' 'READ_ONLY automatico'
$text = Replace-Required $text '            stateLabel.Text = "EXECUTANDO TESTE COMPLETO...";' '            stateLabel.Text = "EXECUTANDO TESTE UNICO...";' 'Estado de execucao'
$text = Replace-Required $text '            LogUi("SEGURANCA", "READ_ONLY=" + (readOnlyApproved ? "AUTORIZADO PELO USUARIO" : "DESATIVADO") + "; 38 exige F0 valido na mesma sessao; demais CANDIDATE e BLOCKED nunca sao enviados.");' '            LogUi("SEGURANCA", "MODO=TESTE_UNICO_READ_ONLY; 38 exige F0 valido na mesma sessao; escrita, RUN/STOP, download, apagamento, firmware e BLOCKED nunca sao enviados.");' 'Log de seguranca'

# O pacote pode habilitar probes de leitura recuperados por analise estatica do PC12.
# Eles passam somente se coincidirem byte a byte com esta lista interna E estiverem
# na readOnlyAllowlist do pacote. O 38 permanece em classe propria e exige F0.
$text = Replace-Required $text @'
                if (cls == "READ_ONLY_CANDIDATE" && tx != "38 00 C7")
                    throw new InvalidDataException("READ_ONLY_CANDIDATE protegido: somente 38 00 C7 e aceito pelo motor 1.4.");
'@ @'
                if (cls == "READ_ONLY_CANDIDATE" && tx != "38 00 C7")
                    throw new InvalidDataException("READ_ONLY_CANDIDATE protegido: somente 38 00 C7 usa a dependencia de F0.");
                if (cls == "READ_ONLY_PROBE" && !IsUnifiedReadProbe(tx))
                    throw new InvalidDataException("READ_ONLY_PROBE fora da lista interna do Teste Unico.");
'@ 'Validacao de probes unificados'

$text = Replace-Required $text '                if (s != null && (SafeUpper(s.safetyClass) == "CANDIDATE" || SafeUpper(s.safetyClass) == "READ_ONLY_CANDIDATE")) candidates++;' '                if (s != null && (SafeUpper(s.safetyClass) == "CANDIDATE" || SafeUpper(s.safetyClass) == "READ_ONLY_CANDIDATE" || SafeUpper(s.safetyClass) == "READ_ONLY_PROBE")) candidates++;' 'Contagem de probes'

$text = Replace-Required $text @'
                if (SafeUpper(s.safetyClass) == "READ_ONLY_CANDIDATE") note = "1 TX; exige F0 validado + permissao manual";
                if (SafeUpper(s.safetyClass) == "CANDIDATE") note = "somente registro; nao transmite";
'@ @'
                if (SafeUpper(s.safetyClass) == "READ_ONLY_CANDIDATE") note = "teste unico; exige F0 valido na mesma sessao";
                if (SafeUpper(s.safetyClass) == "READ_ONLY_PROBE") note = "teste unico; probe de leitura allowlisted";
                if (SafeUpper(s.safetyClass) == "CANDIDATE") note = "somente registro; nao transmite";
'@ 'Descricao dos probes'

$text = Replace-Required $text @'
            if (cls == "CANDIDATE")
            {
                reason = "CANDIDATE e somente informativo";
                return false;
            }
'@ @'
            if (cls == "READ_ONLY_PROBE")
            {
                if (!readOnlyApproved)
                {
                    reason = "READ_ONLY_PROBE exige modo de leitura habilitado";
                    return false;
                }
                if (!IsUnifiedReadProbe(tx))
                {
                    reason = "probe nao pertence a lista interna do Teste Unico";
                    return false;
                }
                if (!PackageReadOnlyAllowed(tx))
                {
                    reason = "probe nao esta na readOnlyAllowlist do pacote";
                    return false;
                }
                return true;
            }
            if (cls == "CANDIDATE")
            {
                reason = "CANDIDATE e somente informativo";
                return false;
            }
'@ 'Safety gate READ_ONLY_PROBE'

$helper = @'
        private bool IsUnifiedReadProbe(string tx)
        {
            string n = NormalizeHex(tx);
            return n == "34 03 00 00 A0 28" ||
                   n == "0A 03 60 00 AC E6" ||
                   n == "0A 03 60 AC AC 3A" ||
                   n == "14 00 EB";
        }

'@
$text = Replace-Required $text '        private bool IsBuiltInBlocked(string tx)' ($helper + '        private bool IsBuiltInBlocked(string tx)') 'Helper de probes READ_ONLY'

$text = Replace-Required $text '        private const string EngineVersion = "1.5";' '        private const string EngineVersion = "1.6";' 'EngineVersion 1.5'

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab 1.6 aplicado: Teste Unico TP02 com matriz de enlace + probes READ-ONLY em uma unica execucao.'
