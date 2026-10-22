$ErrorActionPreference = 'Stop'

# V137: o primeiro teste fisico do RUN mostrou que a tela principal conseguia
# conectar ao TP02, mas o preflight do RUN chamava AcquireStablePgPortV93 apenas
# com round=1. O enlace real ja demonstrou precisar de rodadas posteriores.
#
# Este hotfix preserva integralmente o guardrail do V136:
# - nenhum quadro RUN e enviado enquanto STOP/RUN nao for qualificado;
# - o quadro 02 00 FD continua sendo transmitido no maximo UMA vez;
# - nao existe retransmissao automatica do RUN;
# - a verificacao final continua sendo somente HELLO.
#
# A unica mudanca e o preflight: ele passa a usar ate 5 rodadas de aquisicao,
# exatamente para tolerar o mesmo comportamento intermitente ja observado no
# CONECTAR/READ do TP02 real.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V137: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

$needle = @'
                    string initialState;
                    string acquisition;
                    serial = AcquireStablePgPortV93(portName, 1, out initialState, out acquisition);
                    if (serial == null || !serial.IsOpen)
                        throw new IOException("A porta PG nao permaneceu aberta apos a qualificacao.");

                    AppendLogSafe("V136 PREFLIGHT: " + acquisition + " / estado=" + initialState + ".");
'@

$replacement = @'
                    string initialState = string.Empty;
                    string acquisition = string.Empty;
                    List<string> preflightFailuresV137 = new List<string>();
                    bool preflightQualifiedV137 = false;

                    for (int preflightRoundV137 = 1; preflightRoundV137 <= 5; preflightRoundV137++)
                    {
                        try
                        {
                            serial = AcquireStablePgPortV93(portName, preflightRoundV137,
                                out initialState, out acquisition);

                            if (serial != null && serial.IsOpen &&
                                (string.Equals(initialState, "STOP", StringComparison.Ordinal)
                                 || string.Equals(initialState, "RUN", StringComparison.Ordinal)))
                            {
                                preflightQualifiedV137 = true;
                                AppendLogSafe("V137 PREFLIGHT QUALIFICADO: rodada "
                                    + preflightRoundV137.ToString(CultureInfo.InvariantCulture)
                                    + " / " + acquisition + " / estado=" + initialState + ".");
                                break;
                            }

                            preflightFailuresV137.Add("rodada "
                                + preflightRoundV137.ToString(CultureInfo.InvariantCulture)
                                + ": estado PG nao confirmado");
                        }
                        catch (Exception exRoundV137)
                        {
                            preflightFailuresV137.Add("rodada "
                                + preflightRoundV137.ToString(CultureInfo.InvariantCulture)
                                + ": " + exRoundV137.Message);
                            AppendLogSafe("V137 PREFLIGHT rodada "
                                + preflightRoundV137.ToString(CultureInfo.InvariantCulture)
                                + " falhou: " + exRoundV137.Message);
                        }
                        finally
                        {
                            if (!preflightQualifiedV137)
                            {
                                ClosePort(serial);
                                serial = null;
                            }
                        }

                        if (!preflightQualifiedV137 && preflightRoundV137 < 5)
                            Thread.Sleep(preflightRoundV137 < 3 ? 140 : 240);
                    }

                    if (!preflightQualifiedV137 || serial == null || !serial.IsOpen)
                    {
                        throw new IOException("RUN preflight nao conseguiu qualificar o enlace PG apos 5 rodadas."
                            + (preflightFailuresV137.Count == 0 ? string.Empty
                                : "\r\n\r\n" + string.Join("\r\n", preflightFailuresV137.ToArray())));
                    }
'@

$needleLf = $needle.Replace("`r`n", "`n")
$needleCrLf = $needleLf.Replace("`n", "`r`n")
$replacementLf = $replacement.Replace("`r`n", "`n")
$replacementCrLf = $replacementLf.Replace("`n", "`r`n")

if ($shell.Contains($needleCrLf)) {
    $shell = $shell.Replace($needleCrLf, $replacementCrLf)
}
elseif ($shell.Contains($needleLf)) {
    $shell = $shell.Replace($needleLf, $replacementLf)
}
else {
    throw 'V137: ancora do preflight RUN V136 nao encontrada. O fluxo mudou; ajuste o hotfix antes de publicar.'
}

# Atualiza somente mensagens do fluxo RUN inserido pelo V136. Nao altera protocolo.
$shell = $shell.Replace('RUN PG v1.36 iniciado em ', 'RUN PG v1.37 iniciado em ')
$shell = $shell.Replace('V1.36 RUN cancelado', 'V1.37 RUN cancelado')
$shell = $shell.Replace('V136 RUN TX UNICO', 'V137 RUN TX UNICO')
$shell = $shell.Replace('V136 RUN RX PASSIVO', 'V137 RUN RX PASSIVO')
$shell = $shell.Replace('V136 VERIFY HELLO', 'V137 VERIFY HELLO')
$shell = $shell.Replace('PASS V1.36: RUN confirmado', 'PASS V1.37: RUN confirmado')
$shell = $shell.Replace('V1.36: TP02 permanece em STOP', 'V1.37: TP02 permanece em STOP')
$shell = $shell.Replace('V1.36: estado final desconhecido', 'V1.37: estado final desconhecido')

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 RUN Preflight V137 aplicado: ate 5 rodadas antes do unico TX RUN; sem retransmissao.' -ForegroundColor Cyan
