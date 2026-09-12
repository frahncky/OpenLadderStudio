$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'LadderEditor.build.cs'
if (-not (Test-Path -LiteralPath $path)) { throw 'V129 interaction repair: LadderEditor.build.cs nao encontrado.' }
$text = [System.IO.File]::ReadAllText($path)

if ($text.Contains('private void CanvasElementMoved(object sender, LadderMoveEventArgs e)')) {
    Write-Host 'UI V129 interaction repair: interacoes Ladder ja preservadas.'
    return
}

$anchor = '        private void SetActiveTool(LadderTool tool)'
$idx = $text.IndexOf($anchor, [System.StringComparison]::Ordinal)
if ($idx -lt 0) { throw 'V129 interaction repair: SetActiveTool nao encontrado.' }

$members = @'
        private LadderElement clipboard;

        private void CanvasElementMoved(object sender, LadderMoveEventArgs e)
        {
            LadderElement origem = e.FromLane == 1
                ? rungs[e.FromRung].Parallel[e.FromColumn]
                : rungs[e.FromRung].Elements[e.FromColumn];
            if (origem.Type == LadderElementType.Empty) return;

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

'@

$text = $text.Substring(0, $idx) + $members + $text.Substring($idx)
[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'UI V129 interaction repair: arrasto, menu de contexto, copiar e colar preservados.'
