$ErrorActionPreference = 'Stop'

# V1.57 - conexao PG em dois niveis: caminho curto primeiro, fallback robusto depois.
#
# Objetivo:
# - reduzir a espera percebida em CONECTAR e em READ/WRITE/VERIFY;
# - preservar integralmente os guardrails da V156;
# - nunca considerar STOP operacional sem HELLO STOP + F0 na MESMA abertura da COM;
# - se o caminho rapido nao qualificar, cair no V156/V134 existente sem inventar estado.
#
# Nenhum opcode novo e transmitido. Apenas HELLO e F0, ambos ja usados no fluxo atual.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V157: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

$insertAnchor = '        private SerialPort TryAcquireSettledProgrammingV156(string portName,'
$insertIndex = $shell.IndexOf($insertAnchor, [System.StringComparison]::Ordinal)
if ($insertIndex -lt 0) { throw 'V157: helper V156 nao encontrado.' }

$helpers = @'
        private bool TryHomePresenceSettledV157(string portName, out string state,
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
            string profileName = cached && !string.IsNullOrEmpty(pgCachedProfileNameV132)
                ? pgCachedProfileNameV132 : "19200 8O1 DTR=on RTS=on";

            SerialPort serial = null;
            try
            {
                serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                serial.Handshake = Handshake.None;
                serial.DtrEnable = dtr;
                serial.RtsEnable = rts;
                serial.ReadTimeout = 55;
                serial.WriteTimeout = 700;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                Thread.Sleep(cached ? 140 : 220);

                // Segunda chance de presenca, ainda HELLO-only. Evita entrar no
                // funil operacional completo apenas porque o primeiro HELLO chegou tarde.
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    int timeout = attempt == 1 ? 360 : (attempt == 2 ? 480 : 650);
                    byte[] raw = ReadUntilSequenceV127(serial, HelloStop, HelloRun, timeout);
                    AppendLogSafe("V157 HOME SETTLED " + profileName + " / "
                        + attempt.ToString(CultureInfo.InvariantCulture) + " RX="
                        + (raw.Length == 0 ? "[]" : ToHex(raw)));

                    if (Contains(raw, HelloStop))
                    {
                        state = "STOP";
                        detail = "V157 HELLO estabilizado / " + profileName + " / tentativa "
                            + attempt.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                    if (Contains(raw, HelloRun))
                    {
                        state = "RUN";
                        detail = "V157 HELLO estabilizado / " + profileName + " / tentativa "
                            + attempt.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }
                    if (attempt < 3) Thread.Sleep(90);
                }

                error = "V157: HELLO estabilizado nao confirmou STOP/RUN no perfil " + profileName + ".";
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

        private SerialPort TryAcquireFastProgrammingV157(string portName,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;

            LoadPgProfileCacheV132();
            bool cached = pgProfileCacheValidV132
                && string.Equals(pgCachedPortV132, portName, StringComparison.OrdinalIgnoreCase);
            bool dtr = cached ? pgCachedDtrV132 : true;
            bool rts = cached ? pgCachedRtsV132 : true;
            string profileName = cached && !string.IsNullOrEmpty(pgCachedProfileNameV132)
                ? pgCachedProfileNameV132 : "19200 8O1 DTR=on RTS=on";

            SerialPort serial = null;
            bool keepOpen = false;
            try
            {
                serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                serial.Handshake = Handshake.None;
                serial.DtrEnable = dtr;
                serial.RtsEnable = rts;
                serial.ReadTimeout = 55;
                serial.WriteTimeout = 700;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                Thread.Sleep(cached ? 140 : 180);

                // Fast path: duas janelas curtas. Se nao bastar, a V156 permanece
                // intacta como segundo nivel, com oito HELLO e oito F0.
                for (int helloAttempt = 1; helloAttempt <= 2; helloAttempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    int helloTimeout = helloAttempt == 1 ? 320 : 480;
                    byte[] helloRaw = ReadUntilSequenceV127(serial, HelloStop, HelloRun, helloTimeout);
                    AppendLogSafe("V157 FAST HELLO " + profileName + " / "
                        + helloAttempt.ToString(CultureInfo.InvariantCulture) + " RX="
                        + (helloRaw.Length == 0 ? "[]" : ToHex(helloRaw)));

                    if (Contains(helloRaw, HelloRun))
                    {
                        state = "RUN";
                        acquisitionLabel = "V157 FAST " + profileName + " / HELLO RUN "
                            + helloAttempt.ToString(CultureInfo.InvariantCulture);
                        RememberPgProfileV132(portName, dtr, rts, profileName);
                        keepOpen = true;
                        return serial;
                    }

                    if (!Contains(helloRaw, HelloStop))
                    {
                        if (helloAttempt < 2) Thread.Sleep(70);
                        continue;
                    }

                    Thread.Sleep(90);
                    for (int f0Attempt = 1; f0Attempt <= 2; f0Attempt++)
                    {
                        serial.DiscardInBuffer();
                        serial.Write(F0Request, 0, F0Request.Length);
                        int f0Timeout = f0Attempt == 1 ? 500 : 700;
                        byte[] f0Raw = ReadUntilSequenceV127(serial, F0Response, null, f0Timeout);
                        AppendLogSafe("V157 FAST F0 " + profileName + " / "
                            + f0Attempt.ToString(CultureInfo.InvariantCulture) + " RX="
                            + (f0Raw.Length == 0 ? "[]" : ToHex(f0Raw)));

                        if (Contains(f0Raw, F0Response))
                        {
                            state = "STOP";
                            acquisitionLabel = "V157 FAST " + profileName
                                + " / HELLO " + helloAttempt.ToString(CultureInfo.InvariantCulture)
                                + " / F0 " + f0Attempt.ToString(CultureInfo.InvariantCulture);
                            RememberPgProfileV132(portName, dtr, rts, profileName);
                            keepOpen = true;
                            return serial;
                        }
                        if (f0Attempt < 2) Thread.Sleep(100);
                    }

                    AppendLogSafe("V157 FAST: HELLO STOP respondeu, mas F0 nao qualificou nas duas janelas curtas.");
                    return null;
                }

                AppendLogSafe("V157 FAST: HELLO nao respondeu nas duas janelas curtas.");
                return null;
            }
            catch (Exception ex)
            {
                AppendLogSafe("V157 FAST indisponivel: " + ex.Message);
                return null;
            }
            finally
            {
                if (!keepOpen && serial != null) ClosePort(serial);
            }
        }

'@
$shell = $shell.Substring(0, $insertIndex) + $helpers + $shell.Substring($insertIndex)

$homeOld = @'
            AppendLogSafe("V133 HOME CONNECT: caminho rapido falhou (" + fastError
                + "); iniciando qualificacao robusta.");

            SerialPort port = null;
'@
$homeNew = @'
            AppendLogSafe("V133 HOME CONNECT: caminho rapido falhou (" + fastError
                + "); tentando HELLO estabilizado V157 antes da qualificacao robusta.");

            string settledHomeState;
            string settledHomeDetail;
            string settledHomeError;
            if (TryHomePresenceSettledV157(portName, out settledHomeState,
                out settledHomeDetail, out settledHomeError))
            {
                state = settledHomeState;
                detail = settledHomeDetail;
                AppendLogSafe("V157 HOME CONNECT: presenca confirmada sem F0; conexao principal concluida.");
                return true;
            }

            AppendLogSafe("V157 HOME CONNECT: HELLO estabilizado falhou (" + settledHomeError
                + "); iniciando qualificacao robusta.");

            SerialPort port = null;
'@
if (-not $shell.Contains($homeOld)) { throw 'V157: ponto de fallback HOME V133 nao encontrado.' }
$shell = $shell.Replace($homeOld, $homeNew)

$operationalOld = @'
            if (round == 1)
            {
                SerialPort settled = TryAcquireSettledProgrammingV156(portName,
                    out state, out acquisitionLabel);
                if (settled != null)
                {
                    AppendLogSafe("V156 SETTLED QUALIFICADO: mesma sessao HELLO+F0 preservada para a operacao.");
                    return settled;
                }
                AppendLogSafe("V156 SETTLED nao qualificou; usando funil V132/V134 sem reduzir guardrails.");
                Thread.Sleep(700);
            }
'@
$operationalNew = @'
            if (round == 1)
            {
                SerialPort fastV157 = TryAcquireFastProgrammingV157(portName,
                    out state, out acquisitionLabel);
                if (fastV157 != null)
                {
                    AppendLogSafe("V157 FAST QUALIFICADO: operacao liberada na mesma sessao HELLO+F0.");
                    return fastV157;
                }

                AppendLogSafe("V157 FAST nao qualificou; escalando para o caminho settled V156.");
                Thread.Sleep(80);

                SerialPort settled = TryAcquireSettledProgrammingV156(portName,
                    out state, out acquisitionLabel);
                if (settled != null)
                {
                    AppendLogSafe("V156 SETTLED QUALIFICADO: mesma sessao HELLO+F0 preservada para a operacao.");
                    return settled;
                }
                AppendLogSafe("V156 SETTLED nao qualificou; usando funil V132/V134 sem reduzir guardrails.");
                Thread.Sleep(120);
            }
'@
if (-not $shell.Contains($operationalOld)) { throw 'V157: bloco operacional V156 nao encontrado.' }
$shell = $shell.Replace($operationalOld, $operationalNew)

$required = @(
    'TryHomePresenceSettledV157',
    'TryAcquireFastProgrammingV157',
    'V157 FAST QUALIFICADO',
    'Thread.Sleep(cached ? 140 : 180);',
    'int helloTimeout = helloAttempt == 1 ? 320 : 480;',
    'int f0Timeout = f0Attempt == 1 ? 500 : 700;'
)
foreach ($token in $required) {
    if (-not $shell.Contains($token)) { throw "V157: guarda de build falhou; token ausente: $token" }
}

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 Fast Connect V157 aplicado: HOME em dois niveis e qualificacao operacional curta antes do fallback V156.' -ForegroundColor Cyan
