$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$path = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path $path)) { throw 'V79: LadderEditor.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)

# O arquivo gerado nao segue a convencao do repositorio: a V57 e a V58 o
# escrevem com LF. Normalizar as ancoras para CRLF as faria falhar todas, entao
# a convencao e lida do proprio arquivo.
$script:eol = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }

function Replace-Required([string]$body, [string]$needle, [string]$replacement, [string]$label) {
    $n = $needle.Replace("`r`n", "`n").Replace("`n", $script:eol)
    $v = $replacement.Replace("`r`n", "`n").Replace("`n", $script:eol)
    if (-not $body.Contains($n)) { throw "V79: ancora nao encontrada ($label)." }
    return $body.Replace($n, $v)
}

if ($text.Contains('private void DrawColumnHeader')) {
    Write-Host 'OpenLadder Studio: grade Ladder V79 ja aplicada.' -ForegroundColor DarkGray
    return
}

# ---------------------------------------------------------------------------
# 1. Espaco para o cabecalho fixo. A V57 deixava TopMargin em 30, e o rotulo de
#    coluna era desenhado em TopMargin - 27, colado na borda superior.
# ---------------------------------------------------------------------------
$text = Replace-Required $text `
    '        private const int TopMargin = 30;' `
    "        private const int TopMargin = 46;`r`n        private const int HeaderHeight = 22;" `
    'margem para o cabecalho'

# ---------------------------------------------------------------------------
# 2. Os rotulos C1..C8 saiam junto com o conteudo ao rolar a pagina, entao em
#    programa grande o usuario perdia a referencia de coluna. O bloco sai daqui
#    e volta no fim, desenhado numa faixa fixa.
# ---------------------------------------------------------------------------
$text = Replace-Required $text @'
            using (Font outputFont = new Font("Segoe UI Semibold", 7.0f, FontStyle.Bold))
            using (Brush outputText = new SolidBrush(OpenLadderPalette.Faint))
            {
                for (int c = 0; c < LadderRung.ColumnCount; c++)
                {
                    string label = "C" + (c + 1).ToString();
                    SizeF ls = g.MeasureString(label, outputFont);
                    g.DrawString(label, outputFont, outputText,
                        LeftRail + c * cellWidth + (cellWidth - ls.Width) / 2f, TopMargin - 27);
                }
            }

'@ '' 'cabecalho de coluna antigo'

# ---------------------------------------------------------------------------
# 3. O mesmo para a etiqueta de linha: L1..Ln saiam de vista ao rolar na
#    horizontal.
# ---------------------------------------------------------------------------
$text = Replace-Required $text @'
                Rectangle badge = new Rectangle(10, y - 13, 36, 25);
                using (Brush badgeFill = new SolidBrush(r == SelectedRung ? OpenLadderPalette.SelectionFill : OpenLadderPalette.Chrome))
                    g.FillRectangle(badgeFill, badge);
                using (Pen badgePen = new Pen(r == SelectedRung ? OpenLadderPalette.SelectionEdge : OpenLadderPalette.Border, 1.0f))
                    g.DrawRectangle(badgePen, badge);
                using (Font lineFont = new Font("Segoe UI Semibold", 7.8f, FontStyle.Bold))
                using (Brush lineBrush = new SolidBrush(r == SelectedRung ? OpenLadderPalette.Accent : OpenLadderPalette.Muted))
                {
                    string number = "L" + (r + 1).ToString();
                    SizeF ns = g.MeasureString(number, lineFont);
                    g.DrawString(number, lineFont, lineBrush, badge.Left + (badge.Width - ns.Width) / 2f, badge.Top + 5f);
                }

'@ '' 'etiqueta de linha antiga'

# ---------------------------------------------------------------------------
# 4. Linhas verticais da grade: os segmentos paravam 2 px antes do fim de cada
#    rung, entao a grade aparecia tracejada nos limites de linha. Agora cada
#    segmento encosta no seguinte e a coluna le como uma linha continua.
#    (A formatacao colada do "using" seguinte vinha da insercao da V58.)
# ---------------------------------------------------------------------------
$text = Replace-Required $text @'
                        g.DrawLine(gridPen, gx, lineTop + 2, gx, lineTop + RungHeight - 2);
                    }
                }                using (Pen wirePen = new Pen(OpenLadderPalette.Wire, 2.0f))
'@ @'
                        g.DrawLine(gridPen, gx, lineTop, gx, lineTop + RungHeight);
                    }
                }

                using (Pen wirePen = new Pen(OpenLadderPalette.Wire, 2.0f))
'@ 'grade vertical continua'

# ---------------------------------------------------------------------------
# 5. Separador horizontal: sobrava folga de 1 px de cada lado, deixando a linha
#    solta em vez de encostar nos dois trilhos.
# ---------------------------------------------------------------------------
$text = Replace-Required $text `
    '                    g.DrawLine(separator, LeftRail + 1, lineTop + RungHeight - 2, rightRail - 1, lineTop + RungHeight - 2);' `
    '                    g.DrawLine(separator, LeftRail, lineTop + RungHeight, rightRail, lineTop + RungHeight);' `
    'separador horizontal'

# ---------------------------------------------------------------------------
# 6. Cabecalho e calha desenhados por ultimo, sobre o conteudo ja rolado, cada
#    um congelado no proprio eixo: o cabecalho acompanha so a rolagem
#    horizontal e a calha so a vertical. E o comportamento de planilha, e
#    mantem a referencia L/C visivel em programa grande.
# ---------------------------------------------------------------------------
$text = Replace-Required $text @'
            g.ResetTransform();
        }
'@ @'
            DrawRowGutter(g, scroll);
            DrawColumnHeader(g, scroll, cellWidth, rightRail);

            g.ResetTransform();
        }

        /// <summary>Faixa de colunas fixa no topo: acompanha so a rolagem horizontal.</summary>
        private void DrawColumnHeader(Graphics g, Point scroll, int cellWidth, int rightRail)
        {
            using (Matrix header = new Matrix(EffectiveZoom, 0.0f, 0.0f, EffectiveZoom, scroll.X, 0.0f))
                g.Transform = header;

            // Faixa e divisorias sao retangulos alinhados ao eixo: com antialias a
            // borda superior fica meio pixel para dentro e deixa passar uma linha do
            // conteudo que rolou por baixo.
            g.SmoothingMode = SmoothingMode.None;

            // Larga o bastante para cobrir a faixa inteira em qualquer rolagem:
            // parar no ultimo trilho deixava um vao a direita do C8.
            int width = rightRail + ClientSize.Width + 80;
            using (Brush band = new SolidBrush(OpenLadderPalette.Chrome))
                g.FillRectangle(band, -width, -HeaderHeight, width * 2, HeaderHeight * 2);
            using (Pen edge = new Pen(OpenLadderPalette.Border, 1.0f))
                g.DrawLine(edge, -width, HeaderHeight, width * 2, HeaderHeight);

            using (Font font = new Font("Segoe UI Semibold", 7.0f, FontStyle.Bold))
            {
                for (int c = 0; c < LadderRung.ColumnCount; c++)
                {
                    int left = LeftRail + c * cellWidth;
                    bool active = c == SelectedColumn;

                    if (active)
                    {
                        using (Brush fill = new SolidBrush(OpenLadderPalette.SelectionFill))
                            g.FillRectangle(fill, left + 1, 1, cellWidth - 2, HeaderHeight - 2);
                    }
                    else if (c > 0)
                    {
                        using (Pen tick = new Pen(OpenLadderPalette.Border, 1.0f))
                            g.DrawLine(tick, left, 5, left, HeaderHeight - 5);
                    }

                    string label = "C" + (c + 1).ToString();
                    SizeF size = g.MeasureString(label, font);
                    using (Brush ink = new SolidBrush(active ? OpenLadderPalette.Accent : OpenLadderPalette.Muted))
                        g.DrawString(label, font, ink, left + (cellWidth - size.Width) / 2f, 3f);
                }
            }
        }

        /// <summary>Calha de linhas fixa a esquerda: acompanha so a rolagem vertical.</summary>
        private void DrawRowGutter(Graphics g, Point scroll)
        {
            using (Matrix gutter = new Matrix(EffectiveZoom, 0.0f, 0.0f, EffectiveZoom, 0.0f, scroll.Y))
                g.Transform = gutter;

            g.SmoothingMode = SmoothingMode.None;

            int rows = Rungs == null ? 0 : Rungs.Count;
            int height = TopMargin + Math.Max(1, rows) * RungHeight + 44;
            using (Brush band = new SolidBrush(OpenLadderPalette.Chrome))
                g.FillRectangle(band, -LeftRail, -height, LeftRail * 2 - 1, height * 2);
            using (Pen edge = new Pen(OpenLadderPalette.Border, 1.0f))
                g.DrawLine(edge, LeftRail - 1, -height, LeftRail - 1, height * 2);

            using (Font font = new Font("Segoe UI Semibold", 7.8f, FontStyle.Bold))
            {
                for (int r = 0; r < rows; r++)
                {
                    int y = TopMargin + r * RungHeight + 44;
                    bool active = r == SelectedRung;
                    Rectangle badge = new Rectangle(7, y - 13, LeftRail - 16, 25);

                    using (Brush fill = new SolidBrush(active ? OpenLadderPalette.SelectionFill : OpenLadderPalette.ChromeLight))
                        g.FillRectangle(fill, badge);
                    using (Pen pen = new Pen(active ? OpenLadderPalette.SelectionEdge : OpenLadderPalette.Border, 1.0f))
                        g.DrawRectangle(pen, badge);

                    string label = "L" + (r + 1).ToString();
                    SizeF size = g.MeasureString(label, font);
                    using (Brush ink = new SolidBrush(active ? OpenLadderPalette.Accent : OpenLadderPalette.Muted))
                        g.DrawString(label, font, ink, badge.Left + (badge.Width - size.Width) / 2f, badge.Top + 5f);
                }
            }
        }
'@ 'cabecalho e calha congelados'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'OpenLadder Studio: grade Ladder V79 aplicada (cabecalho C fixo, calha L fixa, grade continua).' -ForegroundColor Cyan
