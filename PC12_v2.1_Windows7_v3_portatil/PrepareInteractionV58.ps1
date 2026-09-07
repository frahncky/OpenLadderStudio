$ErrorActionPreference = 'Stop'

$root = Get-Location
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
foreach ($p in @($ladderPath, $shellPath)) {
    if (-not (Test-Path $p)) { throw "Arquivo de build nao encontrado: $p" }
}

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# -----------------------------------------------------------------------------
# Canvas: feedback de hover e coluna de saida visualmente identificada.
# -----------------------------------------------------------------------------
$ladder = LF ([System.IO.File]::ReadAllText($ladderPath))

$fieldAnchor = '        public int SelectedLane = 0;'
$fieldReplacement = @'
        public int SelectedLane = 0;
        private int HoverRung = -1;
        private int HoverColumn = -1;
'@
$ladder = Replace-Required $ladder $fieldAnchor $fieldReplacement.TrimEnd() 'campos hover'

$mouseDownAnchor = '            MouseDown += CanvasMouseDown;'
$mouseDownReplacement = @'
            MouseDown += CanvasMouseDown;
            MouseMove += CanvasMouseMove;
            MouseLeave += delegate { HoverRung = -1; HoverColumn = -1; Invalidate(); };
'@
$ladder = Replace-Required $ladder $mouseDownAnchor $mouseDownReplacement.TrimEnd() 'eventos hover'

$gridAnchor = '            using (Pen railPen = new Pen(Color.FromArgb(32, 53, 70), 3.0f))'
$outputGuide = @'
            int outputLeft = LeftRail + (LadderRung.ColumnCount - 1) * cellWidth;
            using (Brush outputShade = new SolidBrush(Color.FromArgb(248, 250, 253)))
                g.FillRectangle(outputShade, outputLeft + 1, TopMargin - 10, cellWidth - 2, Math.Max(1, bottom - TopMargin + 10));
            using (Font outputFont = new Font("Segoe UI Semibold", 7.0f, FontStyle.Bold))
            using (Brush outputText = new SolidBrush(Color.FromArgb(132, 145, 158)))
            {
                for (int c = 0; c < LadderRung.ColumnCount; c++)
                {
                    string label = "C" + (c + 1).ToString();
                    SizeF ls = g.MeasureString(label, outputFont);
                    g.DrawString(label, outputFont, outputText,
                        LeftRail + c * cellWidth + (cellWidth - ls.Width) / 2f, TopMargin - 27);
                }
            }

            using (Pen railPen = new Pen(Color.FromArgb(32, 53, 70), 3.0f))
'@
$ladder = Replace-Required $ladder $gridAnchor $outputGuide.TrimEnd() 'guia coluna de saida'

$mainCellAnchor = '                    Rectangle mainCell = new Rectangle(cellLeft + 3, y - 31, cellWidth - 6, 62);'
$mainCellReplacement = @'
                    Rectangle mainCell = new Rectangle(cellLeft + 3, y - 31, cellWidth - 6, 62);
                    if (r == HoverRung && c == HoverColumn && !(r == SelectedRung && c == SelectedColumn && SelectedLane == 0))
                        DrawHover(g, mainCell);
'@
$ladder = Replace-Required $ladder $mainCellAnchor $mainCellReplacement.TrimEnd() 'hover celula'

$selectionAnchor = '        private static void DrawSelection(Graphics g, Rectangle cell)'
$hoverMethod = @'
        private static void DrawHover(Graphics g, Rectangle cell)
        {
            Rectangle box = Rectangle.Inflate(cell, -3, -3);
            using (Brush hover = new SolidBrush(Color.FromArgb(247, 250, 253))) g.FillRectangle(hover, box);
            using (Pen hoverPen = new Pen(Color.FromArgb(203, 213, 223), 1.0f))
            {
                hoverPen.DashStyle = DashStyle.Dot;
                g.DrawRectangle(hoverPen, box);
            }
        }

        private static void DrawSelection(Graphics g, Rectangle cell)
'@
$ladder = Replace-Required $ladder $selectionAnchor $hoverMethod.TrimEnd() 'metodo hover'

# Pinte os fundos antes da fiacao: selecao e hover nao podem apagar os fios.
$mainHighlight = @'
                    if (r == HoverRung && c == HoverColumn && !(r == SelectedRung && c == SelectedColumn && SelectedLane == 0))
                        DrawHover(g, mainCell);
                    if (r == SelectedRung && c == SelectedColumn && SelectedLane == 0) DrawSelection(g, mainCell);
'@
$ladder = LF $ladder
$mainHighlight = LF $mainHighlight
$ladder = Replace-Required $ladder $mainHighlight.TrimEnd() '' 'retirar destaque sobre fio principal'
$branchHighlight = '                        if (r == SelectedRung && c == SelectedColumn && SelectedLane == 1) DrawSelection(g, branchCell);'
$ladder = Replace-Required $ladder $branchHighlight '' 'retirar destaque sobre ramificacao'
$wireAnchor = '                using (Pen wirePen = new Pen(Color.FromArgb(48, 65, 78), 2.0f))'
$backgroundPass = @'
                // Fundos primeiro; fios e simbolos permanecem sobre o destaque.
                for (int c = 0; c < LadderRung.ColumnCount; c++)
                {
                    int cellLeft = LeftRail + c * cellWidth;
                    Rectangle mainCell = new Rectangle(cellLeft + 3, y - 31, cellWidth - 6, 62);
                    if (r == HoverRung && c == HoverColumn && !(r == SelectedRung && c == SelectedColumn && SelectedLane == 0))
                        DrawHover(g, mainCell);
                    if (r == SelectedRung && c == SelectedColumn && SelectedLane == 0) DrawSelection(g, mainCell);
                    if (r == SelectedRung && c == SelectedColumn && SelectedLane == 1
                        && Rungs[r].Parallel[c].Type != LadderElementType.Empty && c < LadderRung.ColumnCount - 1)
                    {
                        Rectangle branchCell = new Rectangle(cellLeft + 3, branchY - 22, cellWidth - 6, 44);
                        DrawSelection(g, branchCell);
                    }
                }


                using (Pen gridPen = new Pen(OpenLadderPalette.GridLine, 1.0f))
                {
                    for (int c = 1; c < LadderRung.ColumnCount; c++)
                    {
                        int gx = LeftRail + c * cellWidth;
                        g.DrawLine(gridPen, gx, lineTop + 2, gx, lineTop + RungHeight - 2);
                    }
                }
'@
$ladder = Replace-Required $ladder $wireAnchor ($backgroundPass + $wireAnchor) 'fundos antes dos fios'
$ladder = Replace-Required $ladder 'DrawElement(g, Rungs[r].Elements[c], mainCell, y, false);' 'DrawElement(g, Rungs[r].Elements[c], mainCell, y, false, ElementBackground(r, c, false));' 'fundo do elemento principal'
$ladder = Replace-Required $ladder 'DrawElement(g, branch, branchCell, branchY, true);' 'DrawElement(g, branch, branchCell, branchY, true, ElementBackground(r, c, true));' 'fundo do elemento paralelo'
$elementAnchor = '        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch)'
$elementMethod = @'
        private Color ElementBackground(int rung, int column, bool branch)
        {
            if (rung == SelectedRung && column == SelectedColumn && SelectedLane == (branch ? 1 : 0))
                return OpenLadderPalette.SelectionFill;
            if (!branch && rung == HoverRung && column == HoverColumn)
                return OpenLadderPalette.NavHover;
            return rung == SelectedRung || column == LadderRung.ColumnCount - 1
                ? OpenLadderPalette.Chrome : OpenLadderPalette.Canvas;
        }

        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background)
'@
$ladder = Replace-Required $ladder $elementAnchor $elementMethod.TrimEnd() 'fundo real dos contatos e bobinas'
$ladder = Replace-Required $ladder 'new Pen(Color.White, 4.0f)' 'new Pen(background, 4.0f)' 'mascara na cor do destaque'
$ladder = Replace-Required $ladder 'cx - 23, y, cx + 23, y' 'cx - 16, y, cx + 16, y' 'fio encosta no contato'
$ladder = Replace-Required $ladder 'cx - 28, y, cx + 28, y' 'cx - 24, y, cx + 24, y' 'fio encosta na bobina'

$mouseAnchor = '        private void CanvasMouseDown(object sender, MouseEventArgs e)'
$mouseMove = @'
        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (Rungs == null || Rungs.Count == 0) return;
            Point scroll = AutoScrollPosition;
            int px = e.X - scroll.X;
            int py = e.Y - scroll.Y;
            int width = Math.Max(ClientSize.Width - RightMargin, 920);
            int rightRail = width - 30;
            int usable = rightRail - LeftRail;
            int cellWidth = usable / LadderRung.ColumnCount;
            int rung = (py - TopMargin) / RungHeight;
            int col = (px - LeftRail) / cellWidth;
            if (rung < 0 || rung >= Rungs.Count || col < 0 || col >= LadderRung.ColumnCount)
            {
                if (HoverRung != -1 || HoverColumn != -1) { HoverRung = -1; HoverColumn = -1; Invalidate(); }
                return;
            }
            if (HoverRung != rung || HoverColumn != col)
            {
                HoverRung = rung;
                HoverColumn = col;
                Invalidate();
            }
        }

        private void CanvasMouseDown(object sender, MouseEventArgs e)
'@
$ladder = Replace-Required $ladder $mouseAnchor $mouseMove.TrimEnd() 'mouse move hover'

[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Atualizacao: se a rede falhar na primeira tentativa, repete algumas vezes.
# Nao mostra mensagens quando o usuario ja esta na ultima versao.
# -----------------------------------------------------------------------------
$shell = LF ([System.IO.File]::ReadAllText($shellPath))
$checkMethod = @'
        private void CheckForUpdatesInBackground()
        {
            Thread worker = new Thread(delegate()
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    string latestVersion;
                    bool available = PC12UpdateChecker.TryGetAvailableVersion(out latestVersion);
                    if (IsDisposed) return;

                    if (!string.IsNullOrEmpty(latestVersion))
                    {
                        if (!available) return;
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                if (IsDisposed || updateNotice == null || updateNotice.Visible) return;
                                updateNotice.Text = "● NOVA VERSÃO v" + latestVersion + " — ATUALIZAR";
                                updateNotice.Width = 300;
                                updateNotice.LinkColor = Color.FromArgb(251, 191, 36);
                                updateNotice.ActiveLinkColor = Color.White;
                                updateNotice.Visible = true;
                                if (statusText != null)
                                {
                                    statusText.Text = "Nova versão v" + latestVersion + " disponível.";
                                    statusText.ForeColor = Color.FromArgb(251, 191, 36);
                                }
                                MessageBox.Show(this,
                                    "Uma nova versão do OpenLadder Studio está disponível: v" + latestVersion + ".\r\n\r\nClique em Atualizar para instalar.",
                                    "Nova versão disponível", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            });
                        }
                        catch (InvalidOperationException) { }
                        return;
                    }

                    if (attempt < 2) Thread.Sleep(6000);
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

'@
$shell = Replace-Section $shell '        private void CheckForUpdatesInBackground()' '        private void RefreshProfileUi()' $checkMethod 'retry de atualizacao'
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

Write-Host 'V58 aplicada: hover no canvas, coluna de saida identificada e update com retry.'
