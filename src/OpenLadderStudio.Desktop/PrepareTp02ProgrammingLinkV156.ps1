$ErrorActionPreference = 'Stop'

# V1.56 - estabiliza a qualificacao operacional antes de READ/WRITE/VERIFY e
# permite que um projeto novo de bloco unico substitua com seguranca um programa
# atual longo, preservando backup multipagina e sem restore cego.
#
# Evidencia de bancada (2026-09-14):
# - CONECTAR confirmou COM1 | STOP por HELLO rapido;
# - duas tentativas de ESCREVER falharam antes de qualquer PG33 porque a nova
#   abertura de porta nao conseguiu HELLO+F0 na mesma sessao;
# - portanto o indicador HOME (HELLO-only) nao equivale a qualificacao de
#   programacao (HELLO+F0).
#
# Guardrails mantidos:
# - STOP so qualifica depois de HELLO STOP + F0 na MESMA porta aberta;
# - nenhum PG33 e enviado enquanto a qualificacao nao terminar;
# - cada PG33 e transmitido no maximo uma vez;
# - sem retry cego, Clear All, WBP ou 0x09;
# - backup completo e salvo antes da escrita;
# - restore automatico permanece desabilitado no fluxo seguro V152/V156.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V156: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

$acqAnchor = '        private SerialPort AcquireStablePgPortV93(string portName, int round,'
$acqIndex = $shell.IndexOf($acqAnchor, [System.StringComparison]::Ordinal)
if ($acqIndex -lt 0) { throw 'V156: AcquireStablePgPortV93 nao encontrado.' }

$helper = @'
        private SerialPort TryAcquireSettledProgrammingV156(string portName,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            LoadPgProfileCacheV132();
            if (!pgProfileCacheValidV132 ||
                !string.Equals(pgCachedPortV132, portName, StringComparison.OrdinalIgnoreCase))
                return null;

            bool dtr = pgCachedDtrV132;
            bool rts = pgCachedRtsV132;
            string profileName = string.IsNullOrEmpty(pgCachedProfileNameV132)
                ? "perfil lembrado" : pgCachedProfileNameV132;
            SerialPort serial = null;
            bool keepOpen = false;
            bool helloStopSeen = false;
            string lastHello = "[]";
            string lastF0 = "[]";

            try
            {
                serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                serial.Handshake = Handshake.None;
                serial.DtrEnable = dtr;
                serial.RtsEnable = rts;
                serial.ReadTimeout = 80;
                serial.WriteTimeout = 1200;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                Thread.Sleep(650);

                for (int helloAttempt = 1; helloAttempt <= 8; helloAttempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] raw = ReadUntilSequenceV127(serial, HelloStop, HelloRun,
                        helloAttempt <= 4 ? 650 : 900);
                    lastHello = raw.Length == 0 ? "[]" : ToHex(raw);
                    AppendLogSafe("V156 SETTLED HELLO " + profileName + " / "
                        + helloAttempt.ToString(CultureInfo.InvariantCulture)
                        + " RX=" + lastHello);

                    if (Contains(raw, HelloRun))
                    {
                        state = "RUN";
                        acquisitionLabel = "V156 SETTLED " + profileName + " / HELLO RUN "
                            + helloAttempt.ToString(CultureInfo.InvariantCulture);
                        keepOpen = true;
                        return serial;
                    }

                    if (!Contains(raw, HelloStop))
                    {
                        if (helloAttempt < 8) Thread.Sleep(260);
                        continue;
                    }

                    helloStopSeen = true;
                    Thread.Sleep(500);
                    for (int f0Attempt = 1; f0Attempt <= 8; f0Attempt++)
                    {
                        serial.DiscardInBuffer();
                        serial.Write(F0Request, 0, F0Request.Length);
                        byte[] f0Raw = ReadUntilSequenceV127(serial, F0Response, null,
                            f0Attempt <= 4 ? 1000 : 1500);
                        lastF0 = f0Raw.Length == 0 ? "[]" : ToHex(f0Raw);
                        AppendLogSafe("V156 SETTLED F0 " + profileName + " / "
                            + f0Attempt.ToString(CultureInfo.InvariantCulture)
                            + " RX=" + lastF0);

                        if (Contains(f0Raw, F0Response))
                        {
                            state = "STOP";
                            acquisitionLabel = "V156 SETTLED " + profileName
                                + " / HELLO " + helloAttempt.ToString(CultureInfo.InvariantCulture)
                                + " / F0 " + f0Attempt.ToString(CultureInfo.InvariantCulture);
                            RememberPgProfileV132(portName, dtr, rts, profileName);
                            keepOpen = true;
                            return serial;
                        }
                        if (f0Attempt < 8) Thread.Sleep(450);
                    }
                    break;
                }

                pgLastQualifyReasonV134 = helloStopSeen
                    ? "V156: HELLO STOP respondeu no perfil lembrado, mas F0 ficou mudo nas 8 tentativas da mesma sessao (ultimo RX=" + lastF0 + ")."
                    : "V156: perfil lembrado nao respondeu ao HELLO nas 8 tentativas apos estabilizacao (ultimo RX=" + lastHello + ").";
                return null;
            }
            catch (Exception ex)
            {
                pgLastQualifyReasonV134 = "V156: perfil settled indisponivel: " + ex.Message;
                AppendLogSafe(pgLastQualifyReasonV134);
                return null;
            }
            finally
            {
                if (!keepOpen && serial != null) ClosePort(serial);
            }
        }

'@
$shell = $shell.Substring(0, $acqIndex) + $helper + $shell.Substring($acqIndex)

$acqNeedle = @'
        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            if (round <= 1) pgLastQualifyReasonV134 = string.Empty;

            SerialPort remembered = TryAcquireRememberedProfileV132(portName, round,
'@
$acqReplacement = @'
        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            if (round <= 1) pgLastQualifyReasonV134 = string.Empty;

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

            SerialPort remembered = TryAcquireRememberedProfileV132(portName, round,
'@
if (-not $shell.Contains($acqNeedle)) { throw 'V156: cabecalho AcquireStablePgPortV93 V134 nao encontrado.' }
$shell = $shell.Replace($acqNeedle, $acqReplacement)

$dispatchOld = @'
                    if (blocks.Count == 1)
                    {
                        // Preserva integralmente o caminho ja validado fisicamente para projetos pequenos.
                        successText = RunProjectWriteV123(portName, blocks[0].Frame, expected, logicalCount);
                    }
                    else
                    {
                        successText = RunProjectWriteMultiV152(portName, blocks, expected, logicalCount);
                    }
'@
$dispatchNew = @'
                    // V1.56: um unico bloco tambem usa o fluxo seguro V152, pois o
                    // programa que JA esta no PLC pode ter centenas de palavras.
                    // Assim o backup e multipagina e nao existe restore cego.
                    successText = RunProjectWriteMultiV152(portName, blocks, expected, logicalCount);
'@
if (-not $shell.Contains($dispatchOld)) { throw 'V156: dispatch V152 bloco unico/multibloco nao encontrado.' }
$shell = $shell.Replace($dispatchOld, $dispatchNew)

$guardOld = @'
            if (blocks == null || blocks.Count < 2)
                throw new ArgumentException("Fluxo multibloco exige pelo menos dois blocos.", "blocks");
'@
$guardNew = @'
            if (blocks == null || blocks.Count < 1)
                throw new ArgumentException("Fluxo PG33 seguro exige pelo menos um bloco.", "blocks");
'@
if (-not $shell.Contains($guardOld)) { throw 'V156: guarda RunProjectWriteMultiV152 nao encontrada.' }
$shell = $shell.Replace($guardOld, $guardNew)

$shell = $shell.Replace('BLOCO UNICO: caminho fisicamente validado', 'BLOCO UNICO: fluxo seguro com backup multipagina v1.56')
$shell = $shell.Replace('O caminho de bloco unico preserva o fluxo previamente validado em bancada.',
    'O bloco unico usa o mesmo fluxo seguro de backup multipagina e verify read-only; nao ha restore automatico.')

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 Programming Link V156 aplicado: qualificacao settled HELLO+F0 e bloco unico com backup multipagina seguro.' -ForegroundColor Cyan
