$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$path = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path $path)) { throw 'V81: LadderEditor.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)
$script:eol = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }

function Replace-Required([string]$body, [string]$needle, [string]$replacement, [string]$label) {
    $n = $needle.Replace("`r`n", "`n").Replace("`n", $script:eol)
    $v = $replacement.Replace("`r`n", "`n").Replace("`n", $script:eol)
    if (-not $body.Contains($n)) { throw "V81: ancora nao encontrada ($label)." }
    return $body.Replace($n, $v)
}

if ($text.Contains('private bool HitTest')) {
    Write-Host 'OpenLadder Studio: interacao Ladder V81 ja aplicada.' -ForegroundColor DarkGray
    return
}

# ---------------------------------------------------------------------------
# 1. Um unico hit-testing.
#
#    CanvasMouseMove e SelectFromPoint calculavam a celula de formas diferentes:
#    o hover usava LogicalCanvasWidth e trilho em -30; a selecao usava
#    ClientSize com piso 850 e trilho em -28. Em janela estreita o hover
#    destacava uma celula e o clique selecionava outra. A deteccao de via ainda
#    usava y = rungTop + 40, enquanto o desenho ja passara para + 44.
# ---------------------------------------------------------------------------
$hitTest = @'
        /// <summary>Converte um ponto da tela em celula. Fonte unica para hover,
        /// selecao, arrasto e menu de contexto: quando cada um calculava por
        /// conta propria, o destaque e o clique discordavam em janela estreita.</summary>
        private bool HitTest(Point point, out int rung, out int column, out int lane)
        {
            rung = -1;
            column = -1;
            lane = 0;
            if (Rungs == null || Rungs.Count == 0) return false;

            Point scroll = AutoScrollPosition;
            float zoom = Math.Max(0.01f, EffectiveZoom);
            int px = (int)Math.Floor((point.X - scroll.X) / zoom);
            int py = (int)Math.Floor((point.Y - scroll.Y) / zoom);

            int width = LogicalCanvasWidth - RightMargin;
            int rightRail = width - 30;
            int usable = rightRail - LeftRail;
            int cellWidth = usable / LadderRung.ColumnCount;
            if (cellWidth <= 0) return false;

            int r = (py - TopMargin) / RungHeight;
            int c = (px - LeftRail) / cellWidth;
            if (py < TopMargin || px < LeftRail) return false;
            if (r < 0 || r >= Rungs.Count || c < 0 || c >= LadderRung.ColumnCount) return false;

            int y = TopMargin + r * RungHeight + 44;
            int branchY = y + 42;

            rung = r;
            column = c;
            lane = (Math.Abs(py - branchY) <= 22 && Rungs[r].Parallel[c].Type != LadderElementType.Empty) ? 1 : 0;
            return true;
        }

        private LadderElement CellElement(int rung, int column, int lane)
        {
            if (Rungs == null || rung < 0 || rung >= Rungs.Count) return null;
            if (column < 0 || column >= LadderRung.ColumnCount) return null;
            return lane == 1 ? Rungs[rung].Parallel[column] : Rungs[rung].Elements[column];
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
'@
$text = Replace-Required $text '        private void CanvasMouseMove(object sender, MouseEventArgs e)' $hitTest 'hit-testing unico'

# CanvasMouseMove passa a usar o hit-testing comum e a conduzir o arrasto.
$moveOld = @'
        {
            if (Rungs == null || Rungs.Count == 0) return;
            Point scroll = AutoScrollPosition;
            int px = (int)Math.Floor((e.X - scroll.X) / Math.Max(0.01f, EffectiveZoom));
            int py = (int)Math.Floor((e.Y - scroll.Y) / Math.Max(0.01f, EffectiveZoom));
            int width = LogicalCanvasWidth - RightMargin;
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
'@
$moveNew = @'
        {
            int rung, col, lane;
            bool inside = HitTest(e.Location, out rung, out col, out lane);

            // O arrasto so comeca depois de o ponteiro sair do lugar: um clique
            // com tremor de mao continua sendo clique, nao movimento de elemento.
            if (dragArmed && !dragActive && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                if (Math.Abs(e.X - dragOrigin.X) > 4 || Math.Abs(e.Y - dragOrigin.Y) > 4)
                {
                    dragActive = true;
                    Cursor = Cursors.SizeAll;
                }
            }

            if (dragActive)
            {
                int alvoRung = inside ? rung : -1;
                int alvoCol = inside ? col : -1;
                if (dropRung != alvoRung || dropColumn != alvoCol)
                {
                    dropRung = alvoRung;
                    dropColumn = alvoCol;
                    Invalidate();
                }
                return;
            }

            if (!inside)
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
'@
$text = Replace-Required $text ('        private void CanvasMouseMove(object sender, MouseEventArgs e)' + $script:eol + $moveOld.Replace("`r`n", "`n").Replace("`n", $script:eol)) `
    ('        private void CanvasMouseMove(object sender, MouseEventArgs e)' + $script:eol + $moveNew) 'corpo do MouseMove'

# ---------------------------------------------------------------------------
# 2. Botao esquerdo arma o arrasto; botao direito abre menu no elemento.
# ---------------------------------------------------------------------------
$downOld = @'
        private void CanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Botao direito nunca insere: ele devolve o mouse ao modo ponteiro.
                if (ToolReleased != null) ToolReleased(this, EventArgs.Empty);
                return;
            }

            SelectFromPoint(e.Location);
            if (ElementAction != null) ElementAction(this, EventArgs.Empty);
        }
'@
$downNew = @'
        private void CanvasMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Com ferramenta armada o botao direito devolve o mouse. Em modo
                // ponteiro ele passa a abrir o menu do elemento sob o cursor:
                // clicar com o direito e nao acontecer nada nao e comportamento
                // de editor.
                int r, c, l;
                if (DragEnabled && HitTest(e.Location, out r, out c, out l))
                {
                    SelectFromPoint(e.Location);
                    if (ElementContextMenu != null) ElementContextMenu(this, new MouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
                    return;
                }

                if (ToolReleased != null) ToolReleased(this, EventArgs.Empty);
                return;
            }

            SelectFromPoint(e.Location);

            // Segurar o botao sobre um elemento arma o arrasto. Ele so vira
            // movimento de verdade quando o ponteiro anda (ver CanvasMouseMove).
            dragArmed = false;
            dragActive = false;
            if (DragEnabled)
            {
                int r, c, l;
                if (HitTest(e.Location, out r, out c, out l))
                {
                    LadderElement origem = CellElement(r, c, l);
                    if (origem != null && origem.Type != LadderElementType.Empty)
                    {
                        dragArmed = true;
                        dragOrigin = e.Location;
                        dragRung = r;
                        dragColumn = c;
                        dragLane = l;
                        dropRung = -1;
                        dropColumn = -1;
                    }
                }
            }

            if (ElementAction != null) ElementAction(this, EventArgs.Empty);
        }

        private void CanvasMouseUp(object sender, MouseEventArgs e)
        {
            bool moveu = dragActive;
            int destinoRung = dropRung;
            int destinoColuna = dropColumn;

            dragArmed = false;
            dragActive = false;
            dropRung = -1;
            dropColumn = -1;
            Cursor = DragEnabled ? Cursors.Default : Cursors.Cross;

            if (!moveu) return;
            Invalidate();

            if (destinoRung < 0 || destinoColuna < 0) return;
            if (destinoRung == dragRung && destinoColuna == dragColumn) return;

            LadderMoveEventArgs args = new LadderMoveEventArgs(dragRung, dragColumn, dragLane, destinoRung, destinoColuna);
            if (ElementMoved != null) ElementMoved(this, args);
        }
'@
$text = Replace-Required $text $downOld $downNew 'MouseDown e MouseUp'

# Campos, eventos e a classe de argumento do arrasto.
$text = Replace-Required $text @'
        public event EventHandler ToolReleased;
'@ @'
        public event EventHandler ToolReleased;
        public event MouseEventHandler ElementContextMenu;
        public event EventHandler<LadderMoveEventArgs> ElementMoved;

        /// <summary>Arrastar e abrir menu so valem em modo ponteiro. Com uma
        /// ferramenta armada o clique existe para inserir.</summary>
        public bool DragEnabled = true;

        private bool dragArmed;
        private bool dragActive;
        private Point dragOrigin;
        private int dragRung = -1;
        private int dragColumn = -1;
        private int dragLane;
        private int dropRung = -1;
        private int dropColumn = -1;
'@ 'campos e eventos do arrasto'

$text = Replace-Required $text @'
            MouseDown += CanvasMouseDown;
'@ @'
            MouseDown += CanvasMouseDown;
            MouseUp += CanvasMouseUp;
'@ 'assinatura do MouseUp'

# Realce da celula de destino durante o arrasto, desenhado junto do conteudo.
$text = Replace-Required $text @'
            DrawRowGutter(g, scroll);
'@ @'
            if (dragActive && dropRung >= 0 && dropColumn >= 0)
            {
                int alvoLeft = LeftRail + dropColumn * cellWidth;
                int alvoTop = TopMargin + dropRung * RungHeight + 44 - 31;
                Rectangle alvo = new Rectangle(alvoLeft + 3, alvoTop, cellWidth - 6, 62);
                using (Pen marca = new Pen(OpenLadderPalette.Accent, 2.0f))
                {
                    marca.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(marca, alvo);
                }
            }

            DrawRowGutter(g, scroll);
'@ 'realce do destino'

# Classe de argumento, ao lado do canvas.
$text = Replace-Required $text @'
    internal sealed class FlatActionButton : Button
'@ @'
    /// <summary>Origem e destino de um elemento arrastado no diagrama.</summary>
    internal sealed class LadderMoveEventArgs : EventArgs
    {
        public readonly int FromRung;
        public readonly int FromColumn;
        public readonly int FromLane;
        public readonly int ToRung;
        public readonly int ToColumn;

        public LadderMoveEventArgs(int fromRung, int fromColumn, int fromLane, int toRung, int toColumn)
        {
            FromRung = fromRung;
            FromColumn = fromColumn;
            FromLane = fromLane;
            ToRung = toRung;
            ToColumn = toColumn;
        }
    }

    internal sealed class FlatActionButton : Button
'@ 'classe de argumento do arrasto'

# ---------------------------------------------------------------------------
# 3. Lado do formulario: ligar os eventos, mover com desfazer e montar o menu.
# ---------------------------------------------------------------------------
$text = Replace-Required $text @'
            canvas.ToolReleased += delegate { SetActiveTool(LadderTool.Select); };
'@ @'
            canvas.ToolReleased += delegate { SetActiveTool(LadderTool.Select); };
            canvas.ElementMoved += CanvasElementMoved;
            canvas.ElementContextMenu += CanvasElementContextMenu;
'@ 'ligacao dos eventos no formulario'

$text = Replace-Required $text @'
            if (canvas != null) canvas.Cursor = tool == LadderTool.Select ? Cursors.Default : Cursors.Cross;
'@ @'
            if (canvas != null)
            {
                canvas.Cursor = tool == LadderTool.Select ? Cursors.Default : Cursors.Cross;
                canvas.DragEnabled = tool == LadderTool.Select;
            }
'@ 'arrasto so em modo ponteiro'

$formMembers = @'
        private LadderElement clipboard;

        private void CanvasElementMoved(object sender, LadderMoveEventArgs e)
        {
            LadderElement origem = e.FromLane == 1
                ? rungs[e.FromRung].Parallel[e.FromColumn]
                : rungs[e.FromRung].Elements[e.FromColumn];
            if (origem.Type == LadderElementType.Empty) return;

            // A ultima coluna e reservada a saida, e o ramo paralelo nao pode
            // ocupa-la. Recusar aqui evita gravar um rung que a validacao
            // recusaria depois.
            bool saida = IsOutputType(origem.Type);
            bool ultimaColuna = e.ToColumn == LadderRung.ColumnCount - 1;
            if (saida && !ultimaColuna)
            {
                statusLabel.Text = "Saídas (OUT, TMR, CNT, SET, RESET, END) só podem ficar na última coluna.";
                return;
            }
            if (!saida && ultimaColuna)
            {
                statusLabel.Text = "A última coluna é reservada para instruções de saída.";
                return;
            }

            LadderElement destino = rungs[e.ToRung].Elements[e.ToColumn];
            if (destino.Type != LadderElementType.Empty)
            {
                statusLabel.Text = "A célula de destino já tem um elemento. Apague antes de mover.";
                return;
            }

            SaveUndoState();
            destino.Type = origem.Type;
            destino.Address = origem.Address;
            destino.Parameter = origem.Parameter;
            destino.Mode = origem.Mode;
            origem.Clear();

            canvas.SelectedRung = e.ToRung;
            canvas.SelectedColumn = e.ToColumn;
            canvas.SelectedLane = 0;
            MarkChanged("Elemento movido para L" + (e.ToRung + 1).ToString() + " C" + (e.ToColumn + 1).ToString() + ".");
        }

        private void CanvasElementContextMenu(object sender, MouseEventArgs e)
        {
            LadderElement atual = GetSelectedElement();
            bool ocupada = atual.Type != LadderElementType.Empty;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = OpenLadderPalette.Chrome;
            menu.ForeColor = OpenLadderPalette.Fore;
            menu.ShowImageMargin = false;

            ToolStripMenuItem editar = new ToolStripMenuItem("Editar parâmetro");
            editar.Enabled = ocupada;
            editar.Click += delegate { CanvasElementDoubleClick(canvas, EventArgs.Empty); };
            menu.Items.Add(editar);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem copiar = new ToolStripMenuItem("Copiar");
            copiar.Enabled = ocupada;
            copiar.Click += delegate { CopySelected(); };
            menu.Items.Add(copiar);

            ToolStripMenuItem colar = new ToolStripMenuItem("Colar");
            colar.Enabled = clipboard != null && !ocupada;
            colar.Click += delegate { PasteIntoSelected(); };
            menu.Items.Add(colar);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem apagar = new ToolStripMenuItem("Apagar");
            apagar.Enabled = ocupada;
            apagar.Click += delegate { DeleteSelectedElement(); };
            menu.Items.Add(apagar);

            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = OpenLadderPalette.Chrome;
                item.ForeColor = item.Enabled ? OpenLadderPalette.Fore : OpenLadderPalette.Disabled;
            }

            menu.Show(canvas, new Point(e.X, e.Y));
        }

        private void CopySelected()
        {
            LadderElement atual = GetSelectedElement();
            if (atual.Type == LadderElementType.Empty) return;

            clipboard = new LadderElement();
            clipboard.Type = atual.Type;
            clipboard.Address = atual.Address;
            clipboard.Parameter = atual.Parameter;
            clipboard.Mode = atual.Mode;
            statusLabel.Text = ElementDisplay(atual) + " copiado.";
        }

        private void PasteIntoSelected()
        {
            if (clipboard == null) return;
            if (canvas.SelectedRung < 0 || canvas.SelectedColumn < 0) return;

            LadderElement destino = GetSelectedElement();
            if (destino.Type != LadderElementType.Empty)
            {
                statusLabel.Text = "A célula já tem um elemento. Apague antes de colar.";
                return;
            }

            bool saida = IsOutputType(clipboard.Type);
            bool ultimaColuna = canvas.SelectedColumn == LadderRung.ColumnCount - 1;
            if (saida != ultimaColuna)
            {
                statusLabel.Text = saida
                    ? "Saídas só podem ficar na última coluna."
                    : "A última coluna é reservada para instruções de saída.";
                return;
            }

            SaveUndoState();
            destino.Type = clipboard.Type;
            destino.Address = clipboard.Address;
            destino.Parameter = clipboard.Parameter;
            destino.Mode = clipboard.Mode;
            MarkChanged(ElementDisplay(destino) + " colado.");
        }

        private void SetActiveTool(LadderTool tool)
'@

$text = Replace-Required $text '        private void SetActiveTool(LadderTool tool)' $formMembers 'metodos de mover, copiar, colar e menu'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'OpenLadder Studio: interacao Ladder V81 aplicada (hit-test unico, arrasto, menu de contexto).' -ForegroundColor Cyan
