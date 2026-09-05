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

$ctorOld = @'
            MouseDown += CanvasMouseDown;
            MouseDoubleClick += CanvasMouseDoubleClick;
'@
$ctorNew = @'
            MouseDown += CanvasMouseDown;
            MouseDoubleClick += CanvasMouseDoubleClick;
            MouseMove += CanvasMouseMove;
            MouseLeave += delegate { HoverRung = -1; HoverColumn = -1; Invalidate(); };
'@
$ladder = Replace-Required $ladder $ctorOld.TrimEnd() $ctorNew.TrimEnd() 'eventos hover'

# Marca a ultima coluna como area de saida sem adicionar ruido ao diagrama.
$gridAnchor = @'
            using (Pen railPen = new Pen(Color.FromArgb(32, 53, 70), 3.0f))
'@
$outputGuide = @'
            int outputLeft = LeftRail + (LadderRung.ColumnCount - 1) * cellWidth;
            using (Brush outputShade = new SolidBrush(Color.FromArgb(248, 250, 253)))
                g.FillRectangle(outputShade, outputLeft + 1, TopMargin - 10, cellWidth - 2, Math.Max(1, bottom - TopMargin + 10));
            using (Font outputFont = new Font("Segoe UI Semibold", 7.0f, FontStyle.Bold))
            using (Brush outputText = new SolidBrush(Color.FromArgb(132, 145, 158)))
            {
                string label = "SAÍDA";
                SizeF ls = g.MeasureString(label, outputFont);
                g.DrawString(label, outputFont, outputText, outputLeft + (cellWidth - ls.Width) / 2f, TopMargin - 27);
            }

            using (Pen railPen = new Pen(Color.FromArgb(32, 53, 70), 3.0f))
'@
$ladder = Replace-Required $ladder $gridAnchor.TrimEnd() $outputGuide.TrimEnd() 'guia coluna de saida'

# Hover sutil apenas sobre a celula apontada; selecao continua tendo prioridade.
$cellOld = @'
                    Rectangle mainCell = new Rectangle(cellLeft + 3, y - 31, cellWidth - 6, 62);
                    if (r == SelectedRung && c == SelectedColumn && SelectedLane == 0) DrawSelection(g, mainCell);
                    DrawElement(g, Rungs[r].Elements[c], mainCell, y, false);
'@
$cellNew = @'
                    Rectangle mainCell = new Rectangle(cellLeft + 3, y - 31, cellWidth - 6, 62);
                    if (r == HoverRung && c == HoverColumn && !(r == SelectedRung && c == SelectedColumn && SelectedLane == 0))
                        DrawHover(g, mainCell);
                    if (r == SelectedRung && c == SelectedColumn && SelectedLane == 0) DrawSelection(g, mainCell);
                    DrawElement(g, Rungs[r].Elements[c], mainCell, y, false);
'@
$ladder = Replace-Required $ladder $cellOld.TrimEnd() $cellNew.TrimEnd() 'hover celula'

$selectionAnchor = @'
        private static void DrawSelection(Graphics g, Rectangle cell)
'@
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
$ladder = Replace-Required $ladder $selectionAnchor.TrimEnd() $hoverMethod.TrimEnd() 'metodo hover'

$mouseAnchor = @'
        private void CanvasMouseDown(object sender, MouseEventArgs e)
'@
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
$ladder = Replace-Required $ladder $mouseAnchor.TrimEnd() $mouseMove.TrimEnd() 'mouse move hover'

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

                    // latestVersion preenchida significa que a consulta respondeu.
                    // Se nao ha update, encerra sem novas requisicoes ou avisos.
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
