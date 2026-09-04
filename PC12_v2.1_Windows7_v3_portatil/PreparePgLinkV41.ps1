$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'TP02PgLinkV39.build.cs'
$outputPath = Join-Path (Get-Location) 'TP02PgLinkV41.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

# v0.41: o PLC fisico apresentou dois quadros de resposta ao mesmo HELLO:
#   C0 01 09 35  (soma FF)
#   80 01 09 75  (soma FF)
# Ambos sao apenas reconhecidos como variantes observadas do HELLO PG.
# Nao se atribui significado ao bit 0x40 que diferencia C0 de 80.
# A ferramenta continua estritamente segura: somente CON-ICB<CR> pode ser TX.

$text = $text.Replace('TP02PgLinkV39Form', 'TP02PgLinkV41Form')
$text = $text.Replace('TP02PgLinkV39Program', 'TP02PgLinkV41Program')
$text = $text.Replace('v0.40', 'v0.41')
$text = $text.Replace('RX exigido antes de avancar: C0 01 09 35 - soma modulo 256 = FF', 'RX HELLO aceito: C0 01 09 35 OU 80 01 09 75 - ambos com soma modulo 256 = FF')
$text = $text.Replace('2º TX: F0 00 0F - soma modulo 256 = FF - enviado UMA unica vez apos o handshake exato', 'BLOQUEADO: F0 00 0F = Clear All Memory no PC12 original - nao e transmitido')
$text = $text.Replace('2º TX: F0 00 0F - soma modulo 256 = FF - enviado UMA unica vez apos o handshake exato', 'BLOQUEADO: F0 00 0F = Clear All Memory no PC12 original - nao e transmitido')
$text = $text.Replace('2. Procura exclusivamente C0 01 09 35.', '2. Reconhece somente as respostas HELLO observadas fisicamente: C0 01 09 35 e 80 01 09 75, ambas com checksum FF.')
$text = $text.Replace('C0 01 09 35 nao foi localizado; nenhum outro comando sera transmitido.', 'nenhuma variante HELLO conhecida foi localizada; nenhum outro comando sera transmitido.')
$text = $text.Replace('houve " + totalRxBytes.ToString(CultureInfo.InvariantCulture) + " byte(s), mas C0 01 09 35 nao foi confirmado.', 'houve " + totalRxBytes.ToString(CultureInfo.InvariantCulture) + " byte(s), mas nenhuma variante HELLO conhecida foi confirmada.')
$text = $text.Replace('O TP02 respondeu ao HELLO com C0 01 09 35 e checksum FF.', 'O TP02 respondeu ao HELLO com uma variante PG observada fisicamente e checksum FF.')
$text = $text.Replace('C0 01 09 35 nao foi confirmado nesta execucao.', 'Nenhuma variante HELLO conhecida foi confirmada nesta execucao.')

$startAnchor = '                            int knownIndex = IndexOfSequence(parsed.WithoutEcho, KnownHelloResponse);'
$endAnchor = '                            FinishHandshakeSafe(true, profileName, totalRxBytes, string.Empty);'
$start = $text.IndexOf($startAnchor)
if ($start -lt 0) { throw 'Bloco de reconhecimento HELLO nao encontrado.' }
$endStart = $text.IndexOf($endAnchor, $start)
if ($endStart -lt 0) { throw 'Fim do bloco de reconhecimento HELLO nao encontrado.' }
$end = $text.IndexOf("`n", $endStart)
if ($end -lt 0) { $end = $text.Length } else { $end++ }

$replacement = @'
                            byte[] helloFrame = null;
                            string helloVariant = string.Empty;
                            int knownIndex = IndexOfSequence(parsed.WithoutEcho, KnownHelloResponse);
                            if (knownIndex >= 0)
                            {
                                helloFrame = KnownHelloResponse;
                                helloVariant = "C0 01 09 35";
                            }
                            else
                            {
                                byte[] observedAltHello = new byte[] { 0x80, 0x01, 0x09, 0x75 };
                                knownIndex = IndexOfSequence(parsed.WithoutEcho, observedAltHello);
                                if (knownIndex >= 0)
                                {
                                    helloFrame = observedAltHello;
                                    helloVariant = "80 01 09 75";
                                }
                            }

                            if (helloFrame == null)
                            {
                                LogSafe("PG BLOQUEIO", "nenhuma variante HELLO conhecida foi localizada; nenhum outro comando sera transmitido.");
                                Thread.Sleep(150);
                                continue;
                            }

                            if (Sum8(helloFrame) != 0xFF)
                            {
                                LogSafe("PG BLOQUEIO", "variante HELLO localizada, mas checksum nao fecha FF; link nao confirmado.");
                                Thread.Sleep(150);
                                continue;
                            }

                            LogSafe("PG FRAME", ToHex(helloFrame));
                            LogSafe("PG CHECKSUM", "HELLO RX soma modulo 256 = 0x" + Sum8(helloFrame).ToString("X2", CultureInfo.InvariantCulture));
                            LogSafe("PG LINK", "ESTABLISHED - variante " + helloVariant + " confirmada com " + profileName + ".");
                            if (helloFrame[0] == 0x80)
                                LogSafe("PG VARIANTE", "80 01 09 75 difere de C0 01 09 35 por 0x40 no primeiro byte; o significado desse bit permanece em aberto.");
                            else
                                LogSafe("PG VARIANTE", "C0 01 09 35 corresponde ao primeiro quadro HELLO observado na bancada.");

                            if (parities[profileIndex] == Parity.Odd && dtr[profileIndex] && rts[profileIndex]) SaveProfile(portName);
                            LogSafe("SEGURANCA", "nenhum quadro posterior sera transmitido; F0 00 0F permanece bloqueado.");
                            LogSafe("EVIDENCIA", "respostas HELLO observadas: C0 01 09 35 e 80 01 09 75; ambas fecham soma FF.");
                            FinishHandshakeSafe(true, profileName, totalRxBytes, string.Empty);
'@

$text = $text.Substring(0, $start) + $replacement + $text.Substring($end)
[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
