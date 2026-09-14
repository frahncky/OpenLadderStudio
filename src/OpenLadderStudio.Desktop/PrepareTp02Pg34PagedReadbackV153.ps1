$ErrorActionPreference = 'Stop'

# V1.53 - readback PG34 multipagina.
#
# A v1.52 comprovou fisicamente a escrita PG33 multibloco: 17 blocos foram
# aceitos pelo TP02 com ACK 00 00 FF. A verificacao falhou depois da escrita
# porque ReadCanonicalSnapshotOnOpenPort ainda encerrava a leitura se F-00 END
# nao aparecesse na primeira pagina PG34 de 80 passos.
#
# Esta correcao e estritamente de LEITURA:
# - mantem uma unica sessao serial ja qualificada;
# - pede PG34 em start=0,80,160,...;
# - salva cada pagina recebida;
# - concatena HIGH/LOW/BRAW na ordem global;
# - para somente quando encontra F-00 END (00 70);
# - nunca transmite PG33, restore, RUN, STOP remoto, Clear All, 0x09 ou WBP.

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V153: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

# Nao recortamos o corpo antigo por regex. O shell gerado possui varios helpers
# logo depois deste metodo e um recorte amplo pode remove-los. Em vez disso,
# renomeamos somente a assinatura legada e inserimos o novo metodo antes dela.
$legacySignature = '        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)'
$legacyCount = [System.Text.RegularExpressions.Regex]::Matches(
    $shell, [System.Text.RegularExpressions.Regex]::Escape($legacySignature)).Count
if ($legacyCount -ne 1) {
    throw "V153: assinatura ReadCanonicalSnapshotOnOpenPort esperada exatamente uma vez; encontrado: $legacyCount."
}

$newMethod = @'
        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)
        {
            if (port == null || !port.IsOpen)
                throw new InvalidOperationException("V153: porta PG fechada antes do readback multipagina.");

            ProgramSnapshot snapshot = new ProgramSnapshot();
            snapshot.PlcState = "STOP";
            int pages = 0;

            for (int startStep = 0; startStep < 4000; startStep += StepsPerPage)
            {
                if (pages > 0) Thread.Sleep(300);

                byte[] request34 = Build34Request(startStep);
                string pageId = startStep.ToString("0000", CultureInfo.InvariantCulture);
                AppendLogSafe(tag + " V153 PG34 pagina start=" + pageId
                    + " TX=" + ToHex(request34));

                byte[] frame34 = SendAndReadFrame(port, request34, PagePayloadLength,
                    4, 5600, tag + "-34-" + pageId);
                File.WriteAllText(Path.Combine(sessionDirectory, tag + "-page-" + pageId + ".hex"),
                    ToHex(frame34) + Environment.NewLine, Encoding.ASCII);

                pages++;
                AppendLogSafe(tag + " V153 PG34 pagina " + pageId
                    + " recebida; procurando END nos proximos "
                    + StepsPerPage.ToString(CultureInfo.InvariantCulture) + " passos.");

                for (int localStep = 0; localStep < StepsPerPage; localStep++)
                {
                    int globalStep = startStep + localStep;
                    if (globalStep >= 4000) break;

                    byte high = frame34[2 + (2 * localStep)];
                    byte low = frame34[2 + (2 * localStep) + 1];
                    byte braw = frame34[2 + PageABLength + localStep];
                    snapshot.High.Add(high);
                    snapshot.Low.Add(low);
                    snapshot.External.Add(braw);

                    if (high == 0x00 && low == 0x70)
                    {
                        snapshot.EndStep = globalStep;
                        AppendLogSafe(tag + " V153 END encontrado no passo "
                            + globalStep.ToString("0000", CultureInfo.InvariantCulture)
                            + " apos " + pages.ToString(CultureInfo.InvariantCulture)
                            + " pagina(s) PG34; palavras="
                            + snapshot.Count.ToString(CultureInfo.InvariantCulture) + ".");

                        StringBuilder pagingReport = new StringBuilder();
                        pagingReport.AppendLine("PG34 PAGED READBACK v1.53");
                        pagingReport.AppendLine("pages=" + pages.ToString(CultureInfo.InvariantCulture));
                        pagingReport.AppendLine("words=" + snapshot.Count.ToString(CultureInfo.InvariantCulture));
                        pagingReport.AppendLine("end=" + snapshot.EndStep.ToString("0000", CultureInfo.InvariantCulture));
                        pagingReport.AppendLine("last_page_start=" + startStep.ToString("0000", CultureInfo.InvariantCulture));
                        pagingReport.AppendLine("PG33_TX=NO");
                        pagingReport.AppendLine("RESTORE_TX=NO");
                        File.WriteAllText(Path.Combine(sessionDirectory, tag + "-pg34-paged-v153.txt"),
                            pagingReport.ToString(), Encoding.UTF8);
                        return snapshot;
                    }
                }
            }

            throw new InvalidDataException(
                "F-00 END nao encontrado apos leitura PG34 multipagina de 4000 passos. "
                + "Nenhum PG33 foi retransmitido e nenhum restore foi executado.");
        }

'@

$renamedLegacy = '        private ProgramSnapshot ReadCanonicalSnapshotFirstPageLegacyV153(SerialPort port, string tag)'
$insertion = $newMethod + $renamedLegacy
$shell = $shell.Replace($legacySignature, $insertion)

# Corrige o texto do metodo legado caso ele apareca em diagnostico interno.
$shell = $shell.Replace(
    'F-00 END nao encontrado na primeira pagina; PG33 bloqueado antes da escrita.',
    'F-00 END nao encontrado na primeira pagina do leitor legado V153.')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'TP02 PG34 Paged Readback V153 aplicado: leitura 0/80/160/... ate F-00 END, sem remover helpers do leitor PG.'
