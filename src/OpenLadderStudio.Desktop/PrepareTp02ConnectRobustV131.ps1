$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V131: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    $needleLf = $needle.Replace("`r`n", "`n")
    $needleCrLf = $needleLf.Replace("`n", "`r`n")
    $replacementLf = $replacement.Replace("`r`n", "`n")
    $replacementCrLf = $replacementLf.Replace("`n", "`r`n")
    if ($text.Contains($needleCrLf)) { return $text.Replace($needleCrLf, $replacementCrLf) }
    if ($text.Contains($needleLf)) { return $text.Replace($needleLf, $replacementLf) }
    throw "V131: ancora nao encontrada ($label)."
}

$old = @'
            portName = portCombo.SelectedItem.ToString();
            SerialPort port = null;
            try
            {
                string acquisition;
                port = AcquireStablePgPortV93(portName, 1, out state, out acquisition);
                if (port == null || !port.IsOpen)
                    throw new IOException("A porta nao permaneceu aberta apos a qualificacao PG.");
                detail = acquisition;
                return string.Equals(state, "STOP", StringComparison.Ordinal)
                    || string.Equals(state, "RUN", StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                ClosePort(port);
            }
'@

$new = @'
            portName = portCombo.SelectedItem.ToString();
            SerialPort port = null;
            List<string> failures = new List<string>();

            // A comunicacao PG do TP02 pode precisar de mais de uma rodada de
            // reacquisicao. O readback fisico da v1.24 ja confirmou esse
            // comportamento. A v1.28 usava somente a rodada 1 e podia declarar
            // SEM CONEXAO mesmo com PLC/cabo corretos.
            for (int round = 1; round <= 5; round++)
            {
                try
                {
                    string acquisition;
                    port = AcquireStablePgPortV93(portName, round, out state, out acquisition);
                    if (port != null && port.IsOpen &&
                        (string.Equals(state, "STOP", StringComparison.Ordinal)
                         || string.Equals(state, "RUN", StringComparison.Ordinal)))
                    {
                        detail = acquisition + " / conexao rodada "
                            + round.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    failures.Add("rodada " + round.ToString(CultureInfo.InvariantCulture)
                        + ": estado PG nao confirmado");
                }
                catch (Exception ex)
                {
                    failures.Add("rodada " + round.ToString(CultureInfo.InvariantCulture)
                        + ": " + ex.Message);
                }
                finally
                {
                    ClosePort(port);
                    port = null;
                }

                if (round < 5) Thread.Sleep(round < 3 ? 120 : 220);
            }

            error = "Nao foi possivel confirmar HELLO/estado PG em " + portName
                + " apos 5 rodadas.\r\n\r\n" + string.Join("\r\n", failures.ToArray());
            return false;
'@

$shell = Replace-Required $shell $old $new 'TryHomeConnectV128 robusto'
$shell = $shell.Replace('    |    v1.28";', '    |    v1.31";')

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 Connect V131 aplicado: ate 5 rodadas de reacquisicao PG antes de declarar SEM CONEXAO.' -ForegroundColor Cyan
