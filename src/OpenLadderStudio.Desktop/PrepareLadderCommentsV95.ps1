$ErrorActionPreference = 'Stop'
# V95 - Editor Ladder: canvas redesenhado e comentario por linha (etapa 3b).
# Roda depois do V94, no fim da cadeia. O canvas novo e o layout dele estao em
# LadderCanvasView.cs e LadderCanvasLayout.cs (entram na compilacao pelo Build.bat);
# aqui o editor gerado perde o canvas antigo e passa a ligar o novo:
#   - eventos de adicionar linha e de comentario editado;
#   - "Comentar linha" no menu de contexto, F2 e Enter (o canvas trata as teclas);
#   - o comentario pendente e confirmado antes de serializar (salvar, desfazer);
#   - nenhuma tecla de edicao do projeto age enquanto se digita o comentario.
# No shell: item "Comentar linha" em Editar, F2, e o campo de comentario do painel
# PROPRIEDADES. Tambem corrige o atalho Delete/Ctrl+Z que agia sobre o diagrama
# mesmo com o foco numa caixa de texto.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $ladderPath)) { throw 'V95: LadderEditor.build.cs nao encontrado.' }
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V95: UniversalStudioShell.build.cs nao encontrado.' }

function Read-Lf([string]$path) {
    $raw = [IO.File]::ReadAllText($path)
    return @{ Text = $raw.Replace("`r`n", "`n"); Crlf = $raw.Contains("`r`n") }
}

function Write-Back([string]$path, [string]$text, [bool]$crlf) {
    if ($crlf) { $text = $text.Replace("`n", "`r`n") }
    [IO.File]::WriteAllText($path, $text, [Text.Encoding]::UTF8)
}

function Rep([string]$text, [string]$old, [string]$new, [string]$label) {
    $old = $old.Replace("`r`n", "`n")
    $new = $new.Replace("`r`n", "`n")
    $first = $text.IndexOf($old, [StringComparison]::Ordinal)
    if ($first -lt 0) { throw "V95: ancora nao encontrada ($label). Ajuste PrepareLadderCommentsV95.ps1." }
    if ($text.IndexOf($old, $first + 1, [StringComparison]::Ordinal) -ge 0) { throw "V95: ancora ambigua ($label)." }
    return $text.Replace($old, $new)
}

function Replace-Region([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$new, [string]$label) {
    $a = $text.IndexOf($startAnchor, [StringComparison]::Ordinal)
    if ($a -lt 0) { throw "V95: inicio nao encontrado ($label). Ajuste PrepareLadderCommentsV95.ps1." }
    if ($text.IndexOf($startAnchor, $a + 1, [StringComparison]::Ordinal) -ge 0) { throw "V95: inicio ambiguo ($label)." }
    $b = $text.IndexOf($endAnchor, $a + $startAnchor.Length, [StringComparison]::Ordinal)
    if ($b -lt 0) { throw "V95: fim nao encontrado ($label). Ajuste PrepareLadderCommentsV95.ps1." }
    return $text.Substring(0, $a) + $new.Replace("`r`n", "`n") + $text.Substring($b)
}

# ---------------------------------------------------------------------------
# 1. Editor: sai o canvas antigo, entram as ligacoes do novo.
# ---------------------------------------------------------------------------
$ladderFile = Read-Lf $ladderPath
$ladder = $ladderFile.Text

if ($ladder.Contains('CommentSelectedRung')) {
    Write-Host 'OpenLadder Studio: comentarios V95 ja aplicados no editor.' -ForegroundColor DarkGray
}
else {
    # O canvas inteiro (geometria, pintura, ponteiro) agora vive em LadderCanvasView.cs.
    $ladder = Replace-Region $ladder '    internal sealed class LadderCanvas : ScrollableControl' '    /// <summary>Origem e destino de um elemento arrastado' '' 'canvas antigo'

    $ladder = Rep $ladder '            canvas.ElementContextMenu += CanvasElementContextMenu;' '            canvas.ElementContextMenu += CanvasElementContextMenu;
            canvas.AddRungRequested += delegate { AddRung(); };
            canvas.CommentEdited += CanvasCommentEdited;' 'ligacao dos eventos'

    $ladder = Rep $ladder '        private void CanvasElementContextMenu(object sender, MouseEventArgs e)' '        private void CanvasCommentEdited(object sender, LadderCommentEventArgs e)
        {
            SetRungComment(e.Rung, e.Text);
        }

        /// <summary>Abre a edicao do comentario da linha selecionada (F2, Enter e menu).</summary>
        private void CommentSelectedRung()
        {
            if (rungs.Count == 0) return;
            int r = canvas.SelectedRung;
            if (r < 0) r = 0;
            canvas.BeginEditComment(r);
        }

        private void CanvasElementContextMenu(object sender, MouseEventArgs e)' 'metodos do comentario'

    $ladder = Rep $ladder '            ToolStripMenuItem editar = new ToolStripMenuItem("Editar parâmetro");' '            ToolStripMenuItem comentar = new ToolStripMenuItem("Comentar linha");
            comentar.ShortcutKeyDisplayString = "F2";
            comentar.Click += delegate { CommentSelectedRung(); };
            menu.Items.Add(comentar);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem editar = new ToolStripMenuItem("Editar parâmetro");' 'item Comentar linha do menu'

    # Digitar o comentario nao pode apagar elemento nem desfazer o projeto.
    $ladder = Rep $ladder '        private void FormKeyDown(object sender, KeyEventArgs e)
        {' '        private void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (canvas != null && canvas.IsEditingComment)
            {
                // Salvar, novo e abrir passam (como no shell), com o comentario confirmado antes;
                // Delete, Esc e Ctrl+Z pertencem a caixa de texto.
                if (e.Control && (e.KeyCode == Keys.N || e.KeyCode == Keys.O || e.KeyCode == Keys.S))
                    canvas.CommitCommentEdit();
                else
                    return;
            }' 'atalhos durante a edicao do comentario'

    # O comentario ainda no campo entra no arquivo e no desfazer antes de serializar.
    $ladder = Rep $ladder '        private string SerializeProject()
        {' '        private string SerializeProject()
        {
            if (canvas != null) canvas.CommitCommentEdit();' 'confirmar comentario antes de serializar'

    $ladder = Rep $ladder '            MarkChanged("Nova linha adicionada.");' '            MarkChanged("Nova linha adicionada.");
            canvas.ScrollToRung(canvas.SelectedRung);' 'rolar ate a linha nova'

    # Texto ainda na caixa do diagrama nao marca "dirty": sem isto, Novo, Abrir e fechar a
    # janela descartam o comentario sem perguntar, ou a caixa viva contamina o projeto novo.
    $ladder = Rep $ladder '        private bool ConfirmDiscard()
        {' '        private bool ConfirmDiscard()
        {
            if (canvas != null) canvas.CommitCommentEdit();' 'confirmar comentario do diagrama antes de descartar'

    Write-Back $ladderPath $ladder $ladderFile.Crlf
    Write-Host 'OpenLadder Studio: comentarios por linha V95 aplicados ao editor (canvas novo, Comentar linha, F2).' -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# 2. Shell: menu, atalho, campo do painel PROPRIEDADES, e atalhos que respeitam
#    a caixa de texto com foco.
# ---------------------------------------------------------------------------
$shellFile = Read-Lf $shellPath
$shell = $shellFile.Text

if ($shell.Contains('CommentSelectedRung')) {
    Write-Host 'OpenLadder Studio: comentarios V95 ja aplicados no shell.' -ForegroundColor DarkGray
}
else {
    $shell = Rep $shell '            editar.DropDownItems.Add(DropItemKeys("Adicionar linha", null,' '            editar.DropDownItems.Add(DropItemKeys("Comentar linha", "F2", "Escreve ou edita o comentário da linha selecionada", delegate { InvokeLadder("CommentSelectedRung", null); }));
            editar.DropDownItems.Add(DropItemKeys("Adicionar linha", null,' 'menu Comentar linha'

    $shell = Rep $shell '        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {' '        /// <summary>
        /// Verdadeiro quando o foco esta num campo de texto. Delete, Esc e Ctrl+Z pertencem
        /// ao campo; sem esta checagem, apagar no campo de busca apagava o elemento do diagrama.
        /// </summary>
        private bool IsTypingInText()
        {
            Control focused = this;
            while (focused is ContainerControl && ((ContainerControl)focused).ActiveControl != null)
                focused = ((ContainerControl)focused).ActiveControl;
            return focused is TextBoxBase;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {' 'teste de foco em texto'

    $shell = Rep $shell '            bool ladderActive = tabStrip != null && tabStrip.Selected != null
                && string.Equals(tabStrip.Selected.Key, "LD", StringComparison.OrdinalIgnoreCase);
            if (ladderActive)
            {' '            bool ladderActive = tabStrip != null && tabStrip.Selected != null
                && string.Equals(tabStrip.Selected.Key, "LD", StringComparison.OrdinalIgnoreCase)
                && !IsTypingInText();
            if (ladderActive)
            {
                if (keyData == Keys.F2)
                {
                    InvokeLadder("CommentSelectedRung", null);
                    return true;
                }' 'atalhos do diagrama'

    # O texto pendente no campo do painel PROPRIEDADES entra no documento em que foi
    # escrito: toda acao do editor (menu, barra e atalhos) passa por InvokeLadder, entao
    # a confirmacao vive ali e vale para salvar, novo, abrir, desfazer e compilar.
    $shell = Rep $shell '        private void InvokeLadder(string methodName, object[] args)
        {' '        private void InvokeLadder(string methodName, object[] args)
        {
            if (properties != null) properties.CommitPendingComment();' 'confirmar comentario do painel em toda acao do editor'

    # Comentario da linha no painel PROPRIEDADES.
    $shell = Rep $shell '            properties = new SelectionPropertiesCard();' '            properties = new SelectionPropertiesCard();
            properties.CommentCommitted += delegate(int rung, string text) { SetRungCommentFromCard(rung, text); };' 'ligacao do campo de comentario'

    $shell = Rep $shell '        private void RefreshSelectionCard()
        {' '        private string RungCommentOf(int rung)
        {
            try
            {
                MethodInfo get = typeof(LadderEditorForm).GetMethod("GetRungComment", BindingFlags.Instance | BindingFlags.NonPublic);
                object value = get == null ? null : get.Invoke(ladderForm, new object[] { rung });
                return value as string ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private void SetRungCommentFromCard(int rung, string text)
        {
            if (ladderForm == null || ladderForm.IsDisposed) return;
            try
            {
                MethodInfo set = typeof(LadderEditorForm).GetMethod("SetRungComment", BindingFlags.Instance | BindingFlags.NonPublic);
                if (set != null) set.Invoke(ladderForm, new object[] { rung, text });
            }
            catch { }
            RefreshSelectionCard();
        }

        private void RefreshSelectionCard()
        {' 'metodos do comentario no shell'

    $shell = Rep $shell '                    properties.ShowSelection(-1, -1, 0, null, string.Empty, string.Empty);
                    return;' '                    properties.ShowSelection(-1, -1, 0, null, string.Empty, string.Empty);
                    properties.ShowRung(-1, string.Empty);
                    return;' 'sem selecao'

    $shell = Rep $shell '                    ElementKindName(element.Type), element.Address ?? string.Empty, parameter);' '                    ElementKindName(element.Type), element.Address ?? string.Empty, parameter);
                properties.ShowRung(canvas.SelectedRung, RungCommentOf(canvas.SelectedRung));' 'com selecao'

    Write-Back $shellPath $shell $shellFile.Crlf
    Write-Host 'OpenLadder Studio: comentarios V95 aplicados ao shell (Editar > Comentar linha, F2, painel Propriedades).' -ForegroundColor Cyan
}
