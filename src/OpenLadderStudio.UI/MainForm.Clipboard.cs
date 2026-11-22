using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Copiar, recortar e colar elementos do diagrama, pelo teclado, pelo menu Editar e pelo
/// botão direito. O elemento vai para a área de transferência do Windows no mesmo texto da
/// grade (ex.: "TMR V0001 50"), então dá para copiar no diagrama e colar na grade, e vice-versa.
/// </summary>
public sealed partial class MainForm
{
    /// <summary>Área de transferência (substituível no teste da janela).</summary>
    private Func<string?> readClipboard = () => Clipboard.ContainsText() ? Clipboard.GetText() : null;
    private Action<string> writeClipboard = text => Clipboard.SetText(text);

    private void BuildClipboard()
    {
        diagram.ClipboardRequested += (_, action) => RunClipboard(action);
        diagram.ContextMenuRequested += (_, e) => ShowCellMenu(e.Location);
    }

    private void AddClipboardMenuItems(ToolStripMenuItem edit)
    {
        // Sem ShortcutKeys: o atalho é do diagrama, e Ctrl+C numa caixa de texto continua da caixa.
        edit.DropDownItems.Add(new ToolStripMenuItem("Recor&tar", null, (_, _) => RunClipboard(LadderClipboardAction.Cut)) { ShortcutKeyDisplayString = "Ctrl+X" });
        edit.DropDownItems.Add(new ToolStripMenuItem("&Copiar", null, (_, _) => RunClipboard(LadderClipboardAction.Copy)) { ShortcutKeyDisplayString = "Ctrl+C" });
        edit.DropDownItems.Add(new ToolStripMenuItem("C&olar", null, (_, _) => RunClipboard(LadderClipboardAction.Paste)) { ShortcutKeyDisplayString = "Ctrl+V" });
        edit.DropDownItems.Add(new ToolStripSeparator());
    }

    private LadderCellPosition SelectedPosition() =>
        new(diagram.SelectedRung, diagram.SelectedColumn, diagram.SelectedLane > 0 ? 1 : 0);

    private void RunClipboard(LadderClipboardAction action)
    {
        if (!diagramView || project.Rungs.Count == 0) return;
        LadderCellPosition position = SelectedPosition();
        LadderCell selected = LadderEditCommands.CellAt(project, position);
        switch (action)
        {
            case LadderClipboardAction.Copy:
            case LadderClipboardAction.Cut:
                if (selected.IsEmpty) { status.Text = "Célula vazia: nada para copiar."; return; }
                if (action == LadderClipboardAction.Cut && EditingBlocked()) return;
                writeClipboard(LadderCellText.Format(selected));
                if (action == LadderClipboardAction.Cut)
                {
                    history.Record(project);
                    LadderEditCommands.Clear(project, position);
                    AfterDiagramEdit(position, LadderCellText.Format(selected) + " recortado.");
                }
                else status.Text = LadderCellText.Format(selected) + " copiado.";
                break;
            case LadderClipboardAction.Paste:
                if (EditingBlocked()) return;
                PasteAt(position, readClipboard());
                break;
        }
    }

    private void PasteAt(LadderCellPosition position, string? text)
    {
        LadderCell cell;
        try
        {
            cell = string.IsNullOrWhiteSpace(text) ? new LadderCell() : LadderCellText.Parse(text.Trim());
        }
        catch (FormatException ex)
        {
            status.Text = "A área de transferência não tem um elemento Ladder: " + ex.Message;
            return;
        }
        if (cell.IsEmpty) { status.Text = "A área de transferência não tem um elemento Ladder."; return; }

        LadderCellPosition? target = LadderEditCommands.Target(cell.Kind, position, position.Lane == 1, out string error);
        if (target is null) { status.Text = error; return; }
        ApplyCell(target.Value, cell, LadderCellText.Format(cell) + " colado em L" + (target.Value.Rung + 1) + " C" + (target.Value.Column + 1) + ".");
    }

    private void ShowCellMenu(Point location)
    {
        LadderCellPosition position = SelectedPosition();
        bool occupied = !LadderEditCommands.CellAt(project, position).IsEmpty;
        bool canPaste = !string.IsNullOrWhiteSpace(readClipboard());

        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add(new ToolStripMenuItem("Editar elemento...", null, (_, _) => EditCell(position)) { Enabled = occupied, ShortcutKeyDisplayString = "Duplo clique" });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Recortar", null, (_, _) => RunClipboard(LadderClipboardAction.Cut)) { Enabled = occupied, ShortcutKeyDisplayString = "Ctrl+X" });
        menu.Items.Add(new ToolStripMenuItem("Copiar", null, (_, _) => RunClipboard(LadderClipboardAction.Copy)) { Enabled = occupied, ShortcutKeyDisplayString = "Ctrl+C" });
        menu.Items.Add(new ToolStripMenuItem("Colar", null, (_, _) => RunClipboard(LadderClipboardAction.Paste)) { Enabled = canPaste, ShortcutKeyDisplayString = "Ctrl+V" });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Apagar", null, (_, _) => ClearCell(position)) { Enabled = occupied, ShortcutKeyDisplayString = "Del" });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Comentar linha", null, (_, _) => diagram.BeginEditComment(position.Rung)));
        menu.Items.Add(new ToolStripMenuItem("Inserir linha abaixo", null, (_, _) => { SelectRung(position.Rung); InsertRung(); }));
        ModernPalette.Apply(menu);
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        diagram.ShowMenu(menu, location);
    }
}
