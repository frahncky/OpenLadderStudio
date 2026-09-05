$ErrorActionPreference = 'Stop'

$root = Get-Location
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path $ladderPath)) { throw 'LadderEditor.build.cs nao encontrado.' }

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$ladder = LF ([System.IO.File]::ReadAllText($ladderPath))

# Identidade visivel: remove restos do antigo PC12 Ladder Studio.
$ladder = $ladder.Replace('Text = "PC12 Ladder Studio";', 'Text = "OpenLadder Studio";')
$ladder = $ladder.Replace('brand.Text = "PC12 LADDER STUDIO";', 'brand.Text = "OpenLadder Studio";')
$ladder = $ladder.Replace('"PC12 Ladder Studio"', '"OpenLadder Studio"')
$ladder = $ladder.Replace('—( )—  OUT / Bobina', '—( )—  Bobina')
$ladder = $ladder.Replace('return "OUT / Bobina";', 'return "Bobina";')

# Canvas: mais espaco, hierarquia visual, guias discretas e selecao mais clara.
$ladder = $ladder.Replace('        private const int TopMargin = 24;', '        private const int TopMargin = 30;')
$ladder = $ladder.Replace('        private const int RungHeight = 116;', '        private const int RungHeight = 124;')
$ladder = $ladder.Replace('        private const int LeftRail = 46;', '        private const int LeftRail = 58;')
$ladder = $ladder.Replace('        private const int RightMargin = 34;', '        private const int RightMargin = 42;')

$paint = @'
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Rungs == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int totalHeight = TopMargin + Math.Max(1, Rungs.Count) * RungHeight + 44;
            AutoScrollMinSize = new Size(920, totalHeight);
            Point scroll = AutoScrollPosition;
            g.TranslateTransform(scroll.X, scroll.Y);

            int width = Math.Max(ClientSize.Width - RightMargin, 920);
            int rightRail = width - 30;
            int usable = rightRail - LeftRail;
            int cellWidth = usable / LadderRung.ColumnCount;
            int bottom = TopMargin + Math.Max(1, Rungs.Count) * RungHeight - 14;

            using (Brush page = new SolidBrush(Color.White))
                g.FillRectangle(page, 0, 0, width + 40, totalHeight);

            // Guias de coluna bem discretas: ajudam a posicionar sem poluir o diagrama.
            using (Pen gridPen = new Pen(Color.FromArgb(232, 237, 242), 1.0f))
            {
                gridPen.DashStyle = DashStyle.Dot;
                for (int c = 1; c < LadderRung.ColumnCount; c++)
                {
                    int gx = LeftRail + c * cellWidth;
                    g.DrawLine(gridPen, gx, TopMargin - 10, gx, bottom);
                }
            }

            using (Pen railPen = new Pen(Color.FromArgb(32, 53, 70), 3.0f))
            {
                g.DrawLine(railPen, LeftRail, TopMargin - 10, LeftRail, bottom);
                g.DrawLine(railPen, rightRail, TopMargin - 10, rightRail, bottom);
            }

            for (int r = 0; r < Rungs.Count; r++)
            {
                int lineTop = TopMargin + r * RungHeight;
                int y = lineTop + 44;
                int branchY = y + 42;

                if (r == SelectedRung)
                {
                    using (Brush row = new SolidBrush(Color.FromArgb(248, 251, 254)))
                        g.FillRectangle(row, LeftRail + 1, lineTop + 2, rightRail - LeftRail - 2, RungHeight - 5);
                }

                using (Pen separator = new Pen(Color.FromArgb(241, 244, 247), 1.0f))
                    g.DrawLine(separator, LeftRail + 1, lineTop + RungHeight - 2, rightRail - 1, lineTop + RungHeight - 2);

                using (Pen wirePen = new Pen(Color.FromArgb(48, 65, 78), 2.0f))
                    g.DrawLine(wirePen, LeftRail, y, rightRail, y);

                Rectangle badge = new Rectangle(10, y - 13, 36, 25);
                using (Brush badgeFill = new SolidBrush(r == SelectedRung ? Color.FromArgb(225, 239, 252) : Color.FromArgb(245, 248, 250)))
                    g.FillRectangle(badgeFill, badge);
                using (Pen badgePen = new Pen(r == SelectedRung ? Color.FromArgb(47, 128, 237) : Color.FromArgb(220, 226, 232), 1.0f))
                    g.DrawRectangle(badgePen, badge);
                using (Font lineFont = new Font("Segoe UI Semibold", 7.8f, FontStyle.Bold))
                using (Brush lineBrush = new SolidBrush(r == SelectedRung ? Color.FromArgb(35, 96, 178) : Color.FromArgb(112, 126, 140)))
                {
                    string number = (r + 1).ToString("000");
                    SizeF ns = g.MeasureString(number, lineFont);
                    g.DrawString(number, lineFont, lineBrush, badge.Left + (badge.Width - ns.Width) / 2f, badge.Top + 5f);
                }

                for (int c = 0; c < LadderRung.ColumnCount; c++)
                {
                    int cellLeft = LeftRail + c * cellWidth;
                    Rectangle mainCell = new Rectangle(cellLeft + 3, y - 31, cellWidth - 6, 62);
                    if (r == SelectedRung && c == SelectedColumn && SelectedLane == 0) DrawSelection(g, mainCell);
                    DrawElement(g, Rungs[r].Elements[c], mainCell, y, false);

                    LadderElement branch = Rungs[r].Parallel[c];
                    if (branch.Type != LadderElementType.Empty && c < LadderRung.ColumnCount - 1)
                    {
                        int x1 = cellLeft + 10;
                        int x2 = cellLeft + cellWidth - 10;
                        using (Pen bp = new Pen(Color.FromArgb(48, 65, 78), 1.8f))
                        {
                            g.DrawLine(bp, x1, y, x1, branchY);
                            g.DrawLine(bp, x1, branchY, x2, branchY);
                            g.DrawLine(bp, x2, branchY, x2, y);
                        }
                        Rectangle branchCell = new Rectangle(cellLeft + 3, branchY - 22, cellWidth - 6, 44);
                        if (r == SelectedRung && c == SelectedColumn && SelectedLane == 1) DrawSelection(g, branchCell);
                        DrawElement(g, branch, branchCell, branchY, true);
                    }
                }
            }
            g.ResetTransform();
        }

        private static void DrawSelection(Graphics g, Rectangle cell)
        {
            Rectangle box = Rectangle.Inflate(cell, -2, -2);
            using (Brush sel = new SolidBrush(Color.FromArgb(232, 243, 253))) g.FillRectangle(sel, box);
            using (Pen selPen = new Pen(Color.FromArgb(47, 128, 237), 1.6f)) g.DrawRectangle(selPen, box);
        }

        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch)
        {
            if (element.Type == LadderElementType.Empty) return;
            int cx = cell.Left + cell.Width / 2;
            Color symbol = Color.FromArgb(31, 48, 62);
            Color address = Color.FromArgb(25, 105, 145);

            using (Pen p = new Pen(symbol, 2.1f))
            {
                if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC)
                {
                    using (Pen clear = new Pen(Color.White, 4.0f)) g.DrawLine(clear, cx - 23, y, cx + 23, y);
                    g.DrawLine(p, cx - 16, y - 13, cx - 16, y + 13);
                    g.DrawLine(p, cx + 16, y - 13, cx + 16, y + 13);
                    if (element.Type == LadderElementType.ContactNC)
                        g.DrawLine(p, cx - 20, y + 15, cx + 20, y - 15);
                }
                else if (element.Type == LadderElementType.Coil)
                {
                    using (Pen clear = new Pen(Color.White, 4.0f)) g.DrawLine(clear, cx - 28, y, cx + 28, y);
                    g.DrawArc(p, new Rectangle(cx - 24, y - 16, 23, 32), 90, 180);
                    g.DrawArc(p, new Rectangle(cx + 1, y - 16, 23, 32), -90, 180);
                }
                else
                {
                    Rectangle block = new Rectangle(cx - 36, y - 21, 72, 42);
                    using (Brush fill = new SolidBrush(Color.FromArgb(248, 250, 252))) g.FillRectangle(fill, block);
                    using (Pen border = new Pen(Color.FromArgb(86, 105, 120), 1.4f)) g.DrawRectangle(border, block);
                }
            }

            string top = string.Empty;
            string bottom = string.Empty;
            if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC || element.Type == LadderElementType.Coil) top = element.Address;
            else if (element.Type == LadderElementType.Timer) { top = element.Mode == "RESET" ? "TMR-R" : "TMR"; bottom = element.Address + " " + element.Parameter; }
            else if (element.Type == LadderElementType.Counter) { top = "CNT"; bottom = element.Address + " " + element.Parameter; }
            else if (element.Type == LadderElementType.Set) { top = "SET F-23"; bottom = element.Address; }
            else if (element.Type == LadderElementType.Reset) { top = "RST F-24"; bottom = element.Address; }
            else if (element.Type == LadderElementType.EdgeUp) top = "↑ F-05";
            else if (element.Type == LadderElementType.EdgeDown) top = "↓ F-06";
            else if (element.Type == LadderElementType.Function) { top = element.Address; bottom = element.Parameter; }
            else if (element.Type == LadderElementType.End) top = "END F-00";

            using (Font f = new Font("Consolas", branch ? 7.5f : 8.3f, FontStyle.Bold))
            using (Brush b = new SolidBrush(address))
            {
                if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC || element.Type == LadderElementType.Coil)
                {
                    SizeF size = g.MeasureString(top, f);
                    float ty = branch ? y + 14 : y - 32;
                    g.DrawString(top, f, b, cx - size.Width / 2, ty);
                }
                else
                {
                    SizeF size = g.MeasureString(top, f);
                    g.DrawString(top, f, b, cx - size.Width / 2, y - 13);
                    if (!string.IsNullOrEmpty(bottom))
                    {
                        using (Font f2 = new Font("Consolas", 7.1f, FontStyle.Regular))
                        using (Brush b2 = new SolidBrush(Color.FromArgb(82, 98, 112)))
                        {
                            string text = bottom.Length > 15 ? bottom.Substring(0, 15) : bottom;
                            SizeF s2 = g.MeasureString(text, f2);
                            g.DrawString(text, f2, b2, cx - s2.Width / 2, y + 3);
                        }
                    }
                }
            }
        }

'@
$ladder = Replace-Section $ladder '        protected override void OnPaint(PaintEventArgs e)' '        private void CanvasMouseDown(object sender, MouseEventArgs e)' $paint 'canvas Ladder V57'

[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)
Write-Host 'UI V57 aplicada: canvas Ladder refinado, selecao leve e identidade PC12 removida.'
