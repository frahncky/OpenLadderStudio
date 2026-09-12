$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V133: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "V133: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "V133: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$start = '        internal bool TryHomeConnectV128'
$end = '        internal void UseHomePortV128'
$replacement = @'
        private bool TryHomePresenceFastV133(string portName, out string state,
            out string detail, out string error)
        {
            state = string.Empty;
            detail = string.Empty;
            error = string.Empty;

            LoadPgProfileCacheV132();
            bool cached = pgProfileCacheValidV132
                && string.Equals(pgCachedPortV132, portName, StringComparison.OrdinalIgnoreCase);
            bool dtr = cached ? pgCachedDtrV132 : true;
            bool rts = cached ? pgCachedRtsV132 : true;
            string profileName = cached
                ? pgCachedProfileNameV132
                : "19200 8O1 DTR=on RTS=on";

            SerialPort serial = null;
            try
            {
                serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                serial.Handshake = Handshake.None;
                serial.DtrEnable = dtr;
                serial.RtsEnable = rts;
                serial.ReadTimeout = 45;
                serial.WriteTimeout = 650;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                Thread.Sleep(70);

                // CONECTAR na tela principal precisa somente confirmar presenca e
                // estado do TP02. A qualificacao completa HELLO+F0 continua sendo
                // executada dentro de READ/WRITE/VERIFY antes de qualquer operacao.
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] raw = ReadUntilSequenceV127(serial, HelloStop, HelloRun,
                        attempt == 1 ? 280 : (attempt == 2 ? 360 : 460));

                    AppendLogSafe("V133 HOME HELLO RAPIDO " + profileName + " / "
                        + attempt.ToString(CultureInfo.InvariantCulture) + " RX="
                        + (raw.Length == 0 ? "[]" : ToHex(raw)));

                    if (Contains(raw, HelloStop))
                    {
                        state = "STOP";
                        detail = "HELLO rapido / " + profileName + " / tentativa "
                            + attempt.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                    if (Contains(raw, HelloRun))
                    {
                        state = "RUN";
                        detail = "HELLO rapido / " + profileName + " / tentativa "
                            + attempt.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (attempt < 3) Thread.Sleep(65);
                }

                error = "HELLO rapido nao confirmou STOP/RUN no perfil " + profileName + ".";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                ClosePort(serial);
            }
        }

        internal bool TryHomeConnectV128(out string state, out string portName,
            out string detail, out string error)
        {
            state = string.Empty;
            portName = string.Empty;
            detail = string.Empty;
            error = string.Empty;

            if (portCombo == null || portCombo.SelectedItem == null)
            {
                error = "Nenhuma porta COM foi selecionada para o TP-232PG.";
                return false;
            }

            portName = portCombo.SelectedItem.ToString();

            // Caminho normal de reconexao: confirma somente HELLO no ultimo perfil
            // conhecido. Isso evita repetir F0 e toda a varredura de perfis apenas
            // para atualizar o indicador CONECTADO da tela principal.
            string fastState;
            string fastDetail;
            string fastError;
            if (TryHomePresenceFastV133(portName, out fastState, out fastDetail, out fastError))
            {
                state = fastState;
                detail = fastDetail;
                AppendLogSafe("V133 HOME CONNECT: estado confirmado por HELLO rapido; sem F0.");
                return true;
            }

            AppendLogSafe("V133 HOME CONNECT: caminho rapido falhou (" + fastError
                + "); iniciando qualificacao robusta.");

            SerialPort port = null;
            List<string> failures = new List<string>();
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
                        detail = acquisition + " / conexao robusta rodada "
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

            error = "O HELLO rapido e a qualificacao robusta nao confirmaram o TP02 em "
                + portName + ".\r\n\r\n" + string.Join("\r\n", failures.ToArray());
            return false;
        }

'@

$shell = Replace-Section $shell $start $end $replacement 'TryHomeConnectV128'

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 Fast Home Connect V133 aplicado: CONECTAR usa HELLO rapido; operacoes mantem qualificacao completa.' -ForegroundColor Cyan
