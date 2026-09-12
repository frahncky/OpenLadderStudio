$ErrorActionPreference = 'Stop'

# V134 ataca o motivo real de READ/WRITE falharem com a tela em CONECTADO/STOP.
#
# Desde o v1.33 o CONECTAR confirma presenca somente com HELLO, entao o rotulo
# fica em STOP sem que o F0 tenha sido exercitado. Mas a aquisicao operacional
# exige F0 para declarar STOP (HELLO-RUN qualifica sem F0; HELLO-STOP nao), e o
# orcamento de F0 do caminho sticky/preferencial era de uma unica transmissao por
# HELLO, com 650-820 ms. A prova fisica do PG33 (PerformF0, V113) usa 4
# tentativas com 3600 ms, e o CHANGELOG da v0.96 registra que o TP02 ignora
# sequencias de tentativas - por isso a PG Lab subiu o F0 para 3 rodadas.
#
# O que muda no fio: somente mais repeticoes do quadro F0 00 0F, que ja e
# READ_ONLY_VERIFIED e ja e transmitido hoje. Nenhum opcode novo. F0 continua
# OBRIGATORIO para o estado STOP, portanto a trava de 38 00 C7 permanece.
#
# Tambem troca a mensagem de falha: quando nenhum perfil qualifica, a aquisicao
# devolvia null com state vazio e os consumidores caiam no mesmo
# if (state != "STOP") -> "PLC esta em RUN durante readback", acusando o modo do
# PLC quando o enlace simplesmente nao subiu. Agora a aquisicao lanca o motivo
# verdadeiro; todos os chamadores ja tratam excecao e registram ex.Message.
#
# Texto de interface usa escape \uXXXX de C#: este arquivo permanece ASCII, o
# que evita depender de BOM sob Windows PowerShell 5.1, e o compilador produz o
# acento correto na tela.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V134: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "V134: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "V134: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# -----------------------------------------------------------------------------
# 1. Campo que carrega o motivo da ultima falha de qualificacao.
# -----------------------------------------------------------------------------

$fieldAnchor = '        private static bool pgCachedDtrV132;'
if ($shell.IndexOf($fieldAnchor, [System.StringComparison]::Ordinal) -lt 0) {
    throw 'V134: ancora do campo de cache do perfil PG nao encontrada.'
}
if ($shell.IndexOf('pgLastQualifyReasonV134', [System.StringComparison]::Ordinal) -lt 0) {
    # O shell gerado usa LF, igual a base UniversalStudioShell.cs e a todos os
    # blocos injetados pela cadeia. Nao usar [Environment]::NewLine aqui: no
    # Windows ele traria CRLF e deixaria o arquivo com fim de linha misturado.
    $newField = '        private static string pgLastQualifyReasonV134 = string.Empty;'
    $shell = $shell.Replace($fieldAnchor, $newField + "`n" + $fieldAnchor)
}

# -----------------------------------------------------------------------------
# 2. Perfil especifico: HELLO com a persistencia observada em bancada (a resposta
#    veio na 5a tentativa nos dois registros fisicos de 2026-09-09) e F0 com o
#    mesmo orcamento que a prova PG33 usa.
# -----------------------------------------------------------------------------

$profileStart = '        private SerialPort TryAcquireSpecificProfileV132(string portName, bool dtr, bool rts,'
$profileEnd = '        private SerialPort TryAcquireRememberedProfileV132'
$profileReplacement = @'
        private SerialPort TryAcquireSpecificProfileV132(string portName, bool dtr, bool rts,
            string profileName, int helloTimeoutMs, int f0TimeoutMs,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            SerialPort serial = null;
            bool keepOpen = false;
            bool helloStopSeen = false;
            string lastHelloRx = "[]";
            string lastF0Rx = "[]";
            try
            {
                serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                serial.Handshake = Handshake.None;
                serial.DtrEnable = dtr;
                serial.RtsEnable = rts;
                serial.ReadTimeout = 60;
                serial.WriteTimeout = 1000;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                Thread.Sleep(90);

                // Os dois registros de bancada de 2026-09-09 receberam o HELLO
                // somente na 5a tentativa (248 ms e 239 ms). Duas tentativas por
                // abertura ficavam abaixo desse limiar.
                for (int helloAttempt = 1; helloAttempt <= 5; helloAttempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] helloRaw = ReadUntilSequenceV127(serial, HelloStop, HelloRun, helloTimeoutMs);
                    lastHelloRx = helloRaw.Length == 0 ? "[]" : ToHex(helloRaw);
                    AppendLogSafe("V134 HELLO " + profileName + " / "
                        + helloAttempt.ToString(CultureInfo.InvariantCulture) + " RX=" + lastHelloRx);

                    if (Contains(helloRaw, HelloRun))
                    {
                        state = "RUN";
                        acquisitionLabel = "STICKY " + profileName + " / HELLO "
                            + helloAttempt.ToString(CultureInfo.InvariantCulture);
                        keepOpen = true;
                        return serial;
                    }

                    if (!Contains(helloRaw, HelloStop))
                    {
                        if (helloAttempt < 5) Thread.Sleep(180);
                        continue;
                    }

                    // HELLO STOP confirmado: a sessao existe. Repetir HELLO daqui
                    // nao acrescenta nada, e o F0 passa a ser o unico portao. Ele
                    // recebe o mesmo orcamento da prova fisica do PG33.
                    helloStopSeen = true;
                    int f0Budget = Math.Max(f0TimeoutMs, 3600);
                    for (int f0Attempt = 1; f0Attempt <= 4; f0Attempt++)
                    {
                        Thread.Sleep(f0Attempt == 1 ? 420 : 350);
                        serial.DiscardInBuffer();
                        serial.Write(F0Request, 0, F0Request.Length);
                        byte[] f0Raw = ReadBurst(serial, f0Budget, 260);
                        lastF0Rx = f0Raw.Length == 0 ? "[]" : ToHex(f0Raw);
                        AppendLogSafe("V134 F0 " + profileName + " / "
                            + f0Attempt.ToString(CultureInfo.InvariantCulture) + " RX=" + lastF0Rx);

                        if (Contains(f0Raw, F0Response))
                        {
                            state = "STOP";
                            acquisitionLabel = "STICKY " + profileName + " / HELLO "
                                + helloAttempt.ToString(CultureInfo.InvariantCulture) + " / F0 "
                                + f0Attempt.ToString(CultureInfo.InvariantCulture);
                            keepOpen = true;
                            return serial;
                        }
                    }

                    break;
                }

                if (helloStopSeen)
                {
                    pgLastQualifyReasonV134 = "O HELLO respondeu STOP em " + profileName
                        + ", mas o F0 ficou mudo nas 4 tentativas (\u00faltimo RX=" + lastF0Rx
                        + "). Sem F0 confirmado na mesma porta, o estado STOP n\u00e3o qualifica"
                        + " e o quadro 38 00 C7 continua bloqueado.";
                }
                else
                {
                    pgLastQualifyReasonV134 = "Nenhuma resposta de HELLO em " + profileName
                        + " nas 5 tentativas (\u00faltimo RX=" + lastHelloRx + ").";
                }
                return null;
            }
            catch (Exception ex)
            {
                pgLastQualifyReasonV134 = "Perfil " + profileName + " indispon\u00edvel: " + ex.Message;
                AppendLogSafe("V134 perfil indisponivel: " + profileName + " - " + ex.Message);
                return null;
            }
            finally
            {
                if (!keepOpen && serial != null) ClosePort(serial);
            }
        }

'@

$shell = Replace-Section $shell $profileStart $profileEnd $profileReplacement 'TryAcquireSpecificProfileV132 F0 persistente'

# -----------------------------------------------------------------------------
# 3. Funil de aquisicao: falha passa a ser reportada pelo motivo real, em vez de
#    devolver null e deixar os consumidores acusarem "PLC esta em RUN".
# -----------------------------------------------------------------------------

$acqStart = '        private SerialPort AcquireStablePgPortV93(string portName, int round,'
$acqEnd = '        private SerialPort TryAcquireFastV127'
$acqReplacement = @'
        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            if (round <= 1) pgLastQualifyReasonV134 = string.Empty;

            SerialPort remembered = TryAcquireRememberedProfileV132(portName, round,
                out state, out acquisitionLabel);
            if (remembered != null)
            {
                AppendLogSafe("V132 STICKY QUALIFICADO: reconexao rapida no perfil lembrado.");
                return remembered;
            }

            LoadPgProfileCacheV132();
            if (!pgProfileCacheValidV132 || !string.Equals(pgCachedPortV132, portName, StringComparison.OrdinalIgnoreCase))
            {
                for (int preferredAttempt = 1; preferredAttempt <= 2; preferredAttempt++)
                {
                    SerialPort preferred = TryAcquireSpecificProfileV132(portName, true, true,
                        "19200 8O1 DTR=on RTS=on",
                        preferredAttempt == 1 ? 520 : 700,
                        preferredAttempt == 1 ? 720 : 900,
                        out state, out acquisitionLabel);
                    if (preferred != null)
                    {
                        RememberPgProfileV132(portName, true, true, "19200 8O1 DTR=on RTS=on");
                        acquisitionLabel += " / preferencial V132";
                        AppendLogSafe("V132 PREFERENCIAL QUALIFICADO: perfil salvo para proximas operacoes.");
                        return preferred;
                    }
                    if (preferredAttempt < 2) Thread.Sleep(80);
                }
            }

            AppendLogSafe("V132: perfil lembrado/preferencial nao qualificou; usando FAST PG V127.");
            try
            {
                SerialPort fast = TryAcquireFastV127(portName, round, out state, out acquisitionLabel);
                if (fast != null)
                {
                    RememberPgProfileV132(portName, fast.DtrEnable, fast.RtsEnable,
                        "19200 8O1 DTR=" + (fast.DtrEnable ? "on" : "off")
                        + " RTS=" + (fast.RtsEnable ? "on" : "off"));
                    acquisitionLabel += " / memorizado V132";
                    return fast;
                }
            }
            catch (Exception ex)
            {
                AppendLogSafe("V132 FAST PG indisponivel: " + ex.Message);
            }

            AppendLogSafe("V132 FALLBACK: qualificacao conservadora V127.");

            // AcquireStablePgPortConservativeV127 nunca devolve null: quando nenhum
            // perfil qualifica, ele lanca TimeoutException com uma mensagem generica
            // que nao distingue "HELLO nunca respondeu" de "HELLO respondeu STOP e o
            // F0 ficou mudo". Essa distincao e o que decide se a proxima investigacao
            // e de cabo/conversor ou do preflight F0 desta unidade, e e exatamente o
            // que pgLastQualifyReasonV134 carrega. Por isso a excecao e traduzida
            // aqui, em vez de um throw depois da chamada, que seria inalcancavel.
            try
            {
                SerialPort fallback = AcquireStablePgPortConservativeV127(portName, round,
                    out state, out acquisitionLabel);
                if (fallback != null)
                {
                    RememberPgProfileV132(portName, fallback.DtrEnable, fallback.RtsEnable,
                        "19200 8O1 DTR=" + (fallback.DtrEnable ? "on" : "off")
                        + " RTS=" + (fallback.RtsEnable ? "on" : "off"));
                    acquisitionLabel += " / memorizado V132";
                    return fallback;
                }
            }
            catch (Exception ex)
            {
                throw new IOException(DescribePgQualifyFailureV134(portName, round, ex.Message), ex);
            }

            throw new IOException(DescribePgQualifyFailureV134(portName, round, string.Empty));
        }

        private static string DescribePgQualifyFailureV134(string portName, int round,
            string innerMessage)
        {
            string reason = string.IsNullOrEmpty(pgLastQualifyReasonV134)
                ? "nenhum perfil DTR/RTS respondeu ao HELLO."
                : pgLastQualifyReasonV134;
            string text = "Enlace PG n\u00e3o qualificou em " + portName
                + " (rodada " + round.ToString(CultureInfo.InvariantCulture) + "). " + reason
                + "\r\nO estado do PLC s\u00f3 \u00e9 declarado depois de HELLO e F0"
                + " responderem na mesma porta aberta.";
            if (!string.IsNullOrEmpty(innerMessage))
                text += "\r\nDetalhe do fallback conservador: " + innerMessage;
            return text;
        }

'@

$shell = Replace-Section $shell $acqStart $acqEnd $acqReplacement 'AcquireStablePgPortV93 falha descritiva'

$shell = $shell.Replace('    |    v1.33";', '    |    v1.34";')

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 PG F0 Persistence V134 aplicado: F0 com 4 tentativas/3600ms no perfil qualificado; falha de enlace deixa de ser reportada como PLC em RUN.' -ForegroundColor Cyan
