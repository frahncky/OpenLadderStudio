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

# Estado efemero da sessao: o candidato 38 so pode sair depois de uma resposta F0
# fisicamente conhecida na mesma porta aberta. Reiniciar o teste limpa esta prova.
$text = Replace-Required $text @'
        private volatile bool running;
        private volatile bool cancelRequested;
        private PgLabPackage package;
'@ @'
        private volatile bool running;
        private volatile bool cancelRequested;
        private bool f0ValidatedInCurrentSession;
        private PgLabPackage package;
'@ 'Estado F0 da sessao'

$text = Replace-Required $text '            allowReadOnly.Text = "Permitir etapas READ-ONLY VERIFICADAS do pacote";' '            allowReadOnly.Text = "Permitir consultas READ-ONLY do pacote (inclui 38 apos F0 validado)";' 'Rotulo READ-ONLY'
$text = Replace-Required $text '            Label safety = LabelAt("Modo padrao: somente HANDSHAKE e captura passiva. Candidatos, comandos nao classificados e comandos bloqueados nunca sao enviados automaticamente.", 20, 160, Danger, true);' '            Label safety = LabelAt("Modo padrao: HANDSHAKE e captura passiva. READ_ONLY_CANDIDATE exige F0 validado na mesma sessao, permissao manual e allowlist explicita.", 20, 160, Danger, true);' 'Aviso de seguranca'

$text = Replace-Required $text @'
                if (cls == "BLOCKED" && step.enabled)
                    throw new InvalidDataException("Etapa BLOCKED nao pode estar habilitada.");
'@ @'
                if (cls == "BLOCKED" && step.enabled)
                    throw new InvalidDataException("Etapa BLOCKED nao pode estar habilitada.");
                if (cls == "READ_ONLY_CANDIDATE" && tx != "38 00 C7")
                    throw new InvalidDataException("READ_ONLY_CANDIDATE protegido: somente 38 00 C7 e aceito pelo motor 1.3.");
'@ 'Validacao de candidato'

$text = Replace-Required $text '                if (s != null && SafeUpper(s.safetyClass) == "CANDIDATE") candidates++;' '                if (s != null && (SafeUpper(s.safetyClass) == "CANDIDATE" || SafeUpper(s.safetyClass) == "READ_ONLY_CANDIDATE")) candidates++;' 'Contagem de candidatos'

$text = Replace-Required $text @'
                string note = SafeUpper(s.safetyClass) == "READ_ONLY_VERIFIED" ? "exige permissao manual" : string.Empty;
                if (SafeUpper(s.safetyClass) == "CANDIDATE") note = "somente registro; nao transmite";
'@ @'
                string note = SafeUpper(s.safetyClass) == "READ_ONLY_VERIFIED" ? "exige permissao manual" : string.Empty;
                if (SafeUpper(s.safetyClass) == "READ_ONLY_CANDIDATE") note = "1 TX; exige F0 validado + permissao manual";
                if (SafeUpper(s.safetyClass) == "CANDIDATE") note = "somente registro; nao transmite";
'@ 'Descricao do candidato'

$text = Replace-Required $text @'
            cancelRequested = false;
            running = true;
'@ @'
            cancelRequested = false;
            f0ValidatedInCurrentSession = false;
            running = true;
'@ 'Reset do preflight'

$text = Replace-Required $text '            LogUi("SEGURANCA", "READ_ONLY=" + (readOnlyApproved ? "AUTORIZADO PELO USUARIO" : "DESATIVADO") + "; candidatos e BLOCKED nunca sao enviados.");' '            LogUi("SEGURANCA", "READ_ONLY=" + (readOnlyApproved ? "AUTORIZADO PELO USUARIO" : "DESATIVADO") + "; 38 exige F0 valido na mesma sessao; demais CANDIDATE e BLOCKED nunca sao enviados.");' 'Log de seguranca'

$text = Replace-Required $text @'
                    if (matched != null)
                        LogEvent("ETAPA", step.name + " confirmou " + matched.name, string.Empty, null, sw.ElapsedMilliseconds);
'@ @'
                    if (matched != null)
                    {
                        LogEvent("ETAPA", step.name + " confirmou " + matched.name, string.Empty, null, sw.ElapsedMilliseconds);
                        if (NormalizeHex(step.txHex) == "F0 00 0F" && NormalizeHex(matched.hex) == "00 02 10 22 CB")
                        {
                            f0ValidatedInCurrentSession = true;
                            LogEvent("PREFLIGHT", "F0 validado nesta sessao; READ_ONLY_CANDIDATE 38 pode passar somente se autorizado.", string.Empty, null, sw.ElapsedMilliseconds);
                        }
                    }
'@ 'Confirmacao F0 na mesma sessao'

$text = Replace-Required $text @'
            if (cls == "CANDIDATE")
            {
                reason = "CANDIDATE e somente informativo";
                return false;
            }
'@ @'
            if (cls == "READ_ONLY_CANDIDATE")
            {
                if (!readOnlyApproved)
                {
                    reason = "READ_ONLY_CANDIDATE exige autorizacao manual na caixa de selecao";
                    return false;
                }
                if (!f0ValidatedInCurrentSession)
                {
                    reason = "READ_ONLY_CANDIDATE exige F0 00 0F validado na mesma sessao serial";
                    return false;
                }
                if (tx != "38 00 C7")
                {
                    reason = "motor 1.3 permite como candidato somente 38 00 C7";
                    return false;
                }
                if (!PackageReadOnlyAllowed(tx))
                {
                    reason = "quadro candidato nao esta na readOnlyAllowlist do pacote";
                    return false;
                }
                return true;
            }
            if (cls == "CANDIDATE")
            {
                reason = "CANDIDATE e somente informativo";
                return false;
            }
'@ 'Safety gate READ_ONLY_CANDIDATE'

$text = Replace-Required $text '        private const string EngineVersion = "1.2";' '        private const string EngineVersion = "1.3";' 'EngineVersion 1.2'

[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'PG Lab safety gate 1.3 aplicado: 38 somente apos F0 validado na mesma sessao e permissao manual.'
