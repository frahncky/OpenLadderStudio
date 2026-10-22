$ErrorActionPreference = 'Stop'

# V1.55 - guarda logica antes do RUN remoto.
#
# Motivacao fisica (2026-09-14): o quadro RUN 02 00 FD colocou o TP02 real em
# RUN, confirmado depois por reconexao/HELLO, mas o projeto de fronteira de
# 323 palavras entrou em ERR durante a execucao. O readback mostrou repeticoes
# de STR X0001 / OUT C0001. A ajuda oficial do PC12 lista Double OUT entre os
# erros de autodiagnostico.
#
# Esta alteracao NAO muda o protocolo RUN e NAO transmite nenhum byte novo.
# Ela somente bloqueia RUN antes do preflight serial quando o projeto Ladder
# aberto viola regras de diagnostico que podemos comprovar localmente.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V155: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

$anchor = '        private void StartPgRunV136()'
if (-not $shell.Contains($anchor)) { throw 'V155: StartPgRunV136 nao encontrado.' }

$helpers = @'
        private bool TryValidateRunLogicV155(out string detail)
        {
            detail = string.Empty;
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                detail = "Editor Ladder nao esta disponivel.";
                return false;
            }

            try
            {
                System.Reflection.MethodInfo method = typeof(LadderEditorForm).GetMethod(
                    "SerializeProject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (method == null)
                {
                    detail = "SerializeProject nao encontrado.";
                    return false;
                }

                string text = method.Invoke(ladderForm, null) as string;
                LadderProjectDocument document = LadderProjectCodec.Deserialize(text);
                Tp02LadderCompilationResult compilation = Tp02LadderTargetCompiler.Compile(document);
                if (!compilation.Success)
                {
                    detail = "Compilacao TP02 possui "
                        + compilation.Errors.Count.ToString(CultureInfo.InvariantCulture)
                        + " erro(s). Corrija antes de RUN.";
                    return false;
                }

                List<string> errors = new List<string>();
                Dictionary<string, int> firstOutRung = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, string> timerCounterUse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> uniqueFunctions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> pairCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                pairCounts["F-01"] = 0; pairCounts["F-02"] = 0;
                pairCounts["F-03"] = 0; pairCounts["F-04"] = 0;
                pairCounts["F-07"] = 0; pairCounts["F-08"] = 0;
                pairCounts["F-46"] = 0; pairCounts["F-47"] = 0;

                string[] singleton = new string[] { "F-52", "F-53", "F-55", "F-58", "F-59" };
                for (int s = 0; s < singleton.Length; s++) uniqueFunctions[singleton[s]] = 0;

                for (int i = 0; i < compilation.Trace.Count; i++)
                {
                    Tp02CompilationTrace row = compilation.Trace[i];
                    string op = (row.BooleanText ?? string.Empty).Trim();
                    if (op.Length == 0) continue;

                    if (op.StartsWith("OUT ", StringComparison.OrdinalIgnoreCase))
                    {
                        string address = op.Substring(4).Trim().ToUpperInvariant();
                        int first;
                        if (firstOutRung.TryGetValue(address, out first))
                        {
                            errors.Add("Double OUT: " + address + " aparece nos rungs "
                                + first.ToString(CultureInfo.InvariantCulture) + " e "
                                + row.Rung.ToString(CultureInfo.InvariantCulture) + ".");
                        }
                        else firstOutRung[address] = row.Rung;
                    }

                    bool isTmr = op.StartsWith("TMR ", StringComparison.OrdinalIgnoreCase);
                    bool isCnt = op.StartsWith("CNT ", StringComparison.OrdinalIgnoreCase);
                    if (isTmr || isCnt)
                    {
                        int space = op.IndexOf(' ');
                        string address = space >= 0 ? op.Substring(space + 1).Trim().ToUpperInvariant() : string.Empty;
                        if (address.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                        {
                            string kind = isTmr ? "TMR" : "CNT";
                            string previous;
                            if (timerCounterUse.TryGetValue(address, out previous) && !string.Equals(previous, kind, StringComparison.Ordinal))
                                errors.Add("T/C Double Used: " + address + " usado como " + previous + " e " + kind + ".");
                            else timerCounterUse[address] = kind;
                        }
                    }

                    foreach (string key in singleton)
                    {
                        if (op.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(op, key, StringComparison.OrdinalIgnoreCase))
                            uniqueFunctions[key] = uniqueFunctions[key] + 1;
                    }

                    string[] paired = new string[] { "F-01", "F-02", "F-03", "F-04", "F-07", "F-08", "F-46", "F-47" };
                    for (int p = 0; p < paired.Length; p++)
                    {
                        string key = paired[p];
                        if (op.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase) || string.Equals(op, key, StringComparison.OrdinalIgnoreCase))
                            pairCounts[key] = pairCounts[key] + 1;
                    }
                }

                for (int s = 0; s < singleton.Length; s++)
                {
                    string key = singleton[s];
                    if (uniqueFunctions[key] > 1)
                        errors.Add("FUN. Double Used: " + key + " aparece "
                            + uniqueFunctions[key].ToString(CultureInfo.InvariantCulture) + " vezes.");
                }

                if (pairCounts["F-01"] != pairCounts["F-02"])
                    errors.Add("MCS/MCR Error: F-01=" + pairCounts["F-01"] + ", F-02=" + pairCounts["F-02"] + ".");
                if (pairCounts["F-03"] != pairCounts["F-04"])
                    errors.Add("JCS/JCR Error: F-03=" + pairCounts["F-03"] + ", F-04=" + pairCounts["F-04"] + ".");
                if (pairCounts["F-07"] != pairCounts["F-08"])
                    errors.Add("SKIP/ENDS Error: F-07=" + pairCounts["F-07"] + ", F-08=" + pairCounts["F-08"] + ".");
                if (pairCounts["F-46"] != pairCounts["F-47"])
                    errors.Add("FOR/NEXT Error: F-46=" + pairCounts["F-46"] + ", F-47=" + pairCounts["F-47"] + ".");

                if (errors.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("CHECK LOGIC TP02 v1.55 BLOQUEOU RUN.");
                    sb.AppendLine();
                    int shown = Math.Min(errors.Count, 12);
                    for (int e = 0; e < shown; e++) sb.AppendLine("- " + errors[e]);
                    if (errors.Count > shown)
                        sb.AppendLine("- ... e mais " + (errors.Count - shown).ToString(CultureInfo.InvariantCulture) + " erro(s).");
                    sb.AppendLine();
                    sb.AppendLine("Nenhum quadro RUN foi transmitido.");
                    detail = sb.ToString();
                    return false;
                }

                detail = "CHECK LOGIC TP02 v1.55: PASS local para regras implementadas; palavras="
                    + compilation.Words.Count.ToString(CultureInfo.InvariantCulture) + ".";
                return true;
            }
            catch (Exception ex)
            {
                detail = "CHECK LOGIC TP02 v1.55 falhou: " + ex.Message;
                return false;
            }
        }

'@

$shell = $shell.Replace($anchor, $helpers + $anchor)

$startNeedle = @'
        private void StartPgRunV136()
        {
            if (busy) return;
            runAttemptedV136 = false;
'@
$startReplacement = @'
        private void StartPgRunV136()
        {
            if (busy) return;

            string logicDetailV155;
            if (!TryValidateRunLogicV155(out logicDetailV155))
            {
                AppendLog("V1.55 RUN BLOQUEADO PELO CHECK LOGIC: " + logicDetailV155.Replace("\r", " ").Replace("\n", " "));
                SetStatus("RUN BLOQUEADO / CHECK LOGIC", Danger);
                MessageBox.Show(this, logicDetailV155,
                    "TP02 - CHECK LOGIC BLOQUEOU RUN", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            AppendLog("V1.55 " + logicDetailV155);

            runAttemptedV136 = false;
'@

$startNeedleLf = $startNeedle.Replace("`r`n", "`n")
$startNeedleCrLf = $startNeedleLf.Replace("`n", "`r`n")
$startReplacementLf = $startReplacement.Replace("`r`n", "`n")
$startReplacementCrLf = $startReplacementLf.Replace("`n", "`r`n")

if ($shell.Contains($startNeedleCrLf)) { $shell = $shell.Replace($startNeedleCrLf, $startReplacementCrLf) }
elseif ($shell.Contains($startNeedleLf)) { $shell = $shell.Replace($startNeedleLf, $startReplacementLf) }
else { throw 'V155: ancora do inicio de StartPgRunV136 nao encontrada apos inserir helpers.' }

$required = @(
    'TryValidateRunLogicV155',
    'Double OUT:',
    'T/C Double Used:',
    'FUN. Double Used:',
    'RUN BLOQUEADO / CHECK LOGIC',
    'Nenhum quadro RUN foi transmitido.'
)
foreach ($token in $required) {
    if (-not $shell.Contains($token)) { throw "V155: guarda de build falhou; token ausente: $token" }
}

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 RUN Logic Guard V155 aplicado: Double OUT/T-C/FUN/pairs bloqueiam RUN antes de qualquer TX.' -ForegroundColor Cyan
