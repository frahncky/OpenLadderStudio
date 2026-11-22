using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Diagrama Ladder do editor moderno: o mesmo canvas do editor legado, com caixa de
/// ferramentas, zoom, edição por clique, arrasto e comentário no lugar. A grade de texto
/// continua disponível (Exibir) e as duas vistas mostram sempre o mesmo projeto.
/// </summary>
public sealed partial class MainForm
{
    private readonly LadderDiagram diagram = new() { Dock = DockStyle.Fill };
    private readonly ToolStrip toolbox = new() { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, ShowItemToolTips = true };
    private readonly ToolStripMenuItem diagramViewItem = new("&Diagrama Ladder", null, null, Keys.Control | Keys.D1);
    private readonly ToolStripMenuItem gridViewItem = new("&Grade de texto", null, null, Keys.Control | Keys.D2);
    private bool diagramView = true;

    /// <summary>Pergunta o elemento ao usuário (substituível no teste da janela).</summary>
    private Func<string, string, string, LadderCell?> cellPrompt = null!;

    private static readonly (LadderDiagramTool Tool, string Text, string Tip, string Template)[] Tools =
    {
        (LadderDiagramTool.Select, "Selecionar", "Selecionar e arrastar elementos (Esc)", ""),
        (LadderDiagramTool.ContactNO, "NA", "Contato normalmente aberto", "NO "),
        (LadderDiagramTool.ContactNC, "NF", "Contato normalmente fechado", "NC "),
        (LadderDiagramTool.ParallelNO, "NA ∥", "Contato NA no ramo paralelo", "NO "),
        (LadderDiagramTool.ParallelNC, "NF ∥", "Contato NF no ramo paralelo", "NC "),
        (LadderDiagramTool.Coil, "Bobina", "Bobina (saída na coluna 8)", "COIL "),
        (LadderDiagramTool.Timer, "Temp.", "Temporizador: endereço V e preset", "TMR "),
        (LadderDiagramTool.Counter, "Cont.", "Contador: endereço V e preset", "CNT "),
        (LadderDiagramTool.Set, "SET", "SET (F-23)", "SET "),
        (LadderDiagramTool.Reset, "RST", "RESET (F-24)", "RST "),
        (LadderDiagramTool.RisingEdge, "Subida", "Borda de subida (F-05)", "EUP"),
        (LadderDiagramTool.FallingEdge, "Descida", "Borda de descida (F-06)", "EDN"),
        (LadderDiagramTool.Function, "Função", "Função especial: código e parâmetro", "FUN "),
        (LadderDiagramTool.End, "END", "Fim do programa (F-00)", "END"),
        (LadderDiagramTool.Erase, "Apagar", "Apagar o elemento clicado", ""),
    };

    private void BuildDiagram()
    {
        cellPrompt = PromptWithDialog;

        foreach (var entry in Tools)
        {
            var button = new ToolStripButton(entry.Text)
            {
                ToolTipText = entry.Tip,
                Tag = entry.Tool,
                CheckOnClick = false,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageAboveText,
                ImageScaling = ToolStripItemImageScaling.None,
                Padding = new Padding(4, 2, 4, 1),
                Margin = new Padding(1, 2, 1, 2)
            };
            button.Click += (_, _) => diagram.ActiveTool = diagram.ActiveTool == entry.Tool ? LadderDiagramTool.Select : entry.Tool;
            toolbox.Items.Add(button);
            if (entry.Tool is LadderDiagramTool.Select or LadderDiagramTool.ParallelNC or LadderDiagramTool.FallingEdge or LadderDiagramTool.End)
                toolbox.Items.Add(new ToolStripSeparator());
        }
        toolbox.Padding = new Padding(6, 2, 6, 2);
        toolbox.Font = new Font("Segoe UI", 8.25F);
        RefreshToolIcons();
        ModernPalette.Changed += (_, _) => RefreshToolIcons();
        DpiChanged += (_, _) => BeginInvoke(RefreshToolIcons);
        diagram.ToolChanged += (_, _) => SyncToolbox();
        SyncToolbox();

        diagram.PlaceRequested += (_, e) => PlaceWithTool(e);
        diagram.EditRequested += (_, e) => EditCell(new LadderCellPosition(e.Rung, e.Column, e.Lane));
        diagram.ClearRequested += (_, e) => ClearCell(new LadderCellPosition(e.Rung, e.Column, e.Lane));
        diagram.MoveRequested += (_, e) => MoveCell(e);
        diagram.CommentRequested += (_, e) => CommentRung(e.Rung, e.Text);
        diagram.AddRungRequested += (_, _) =>
        {
            SelectRungInViews(project.Rungs.Count - 1);
            InsertRung();
        };
        diagram.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) diagram.ActiveTool = LadderDiagramTool.Select;
        };

        diagramViewItem.Click += (_, _) => SetDiagramView(true);
        gridViewItem.Click += (_, _) => SetDiagramView(false);
    }

    private void AddDiagramMenuItems(ToolStripMenuItem view)
    {
        view.DropDownItems.Add(diagramViewItem);
        view.DropDownItems.Add(gridViewItem);
        view.DropDownItems.Add(new ToolStripMenuItem("&Aproximar", null, (_, _) => diagram.ZoomPercent += 10, Keys.Control | Keys.Oemplus));
        view.DropDownItems.Add(new ToolStripMenuItem("A&fastar", null, (_, _) => diagram.ZoomPercent -= 10, Keys.Control | Keys.OemMinus));
        view.DropDownItems.Add(new ToolStripMenuItem("Zoom &100%", null, (_, _) => diagram.ZoomPercent = 100, Keys.Control | Keys.D0));
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownOpening += (_, _) =>
        {
            diagramViewItem.Checked = diagramView;
            gridViewItem.Checked = !diagramView;
        };
    }

    private void SetDiagramView(bool showDiagram)
    {
        if (!showDiagram == !diagramView) return;
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;

        // A seleção vai junto para a outra vista.
        if (showDiagram && grid.CurrentCell is { } cell && cell.ColumnIndex >= 1 && cell.ColumnIndex <= LadderRungModel.ColumnCount)
            diagram.Select(RungOf(cell.RowIndex), cell.ColumnIndex - 1, IsParallel(cell.RowIndex) ? 1 : 0);
        else if (!showDiagram && grid.Rows.Count > 0)
            grid.CurrentCell = grid.Rows[Math.Min(diagram.SelectedRung * 2 + diagram.SelectedLane, grid.Rows.Count - 1)].Cells[diagram.SelectedColumn + 1];

        diagramView = showDiagram;
        diagram.Visible = showDiagram;
        toolbox.Visible = showDiagram;
        grid.Visible = !showDiagram;
        SyncPropertiesVisibility();
        (showDiagram ? (Control)diagram : grid).Focus();
        status.Text = showDiagram ? "Diagrama Ladder." : "Grade de texto.";
    }

    private void RefreshDiagram()
    {
        diagram.ShowProject(project);
        RefreshProperties();
        diagram.SetRungPower(simulating ? poweredRungs : null);
    }

    /// <summary>Ícones no tamanho do monitor atual e nas cores do tema.</summary>
    private void RefreshToolIcons()
    {
        int size = (int)Math.Round(22 * DeviceDpi / 96.0);
        foreach (ToolStripButton button in toolbox.Items.OfType<ToolStripButton>())
        {
            var icon = Enum.Parse<LadderToolIcon>(((LadderDiagramTool)button.Tag!).ToString());
            Image? old = button.Image;
            button.Image = LadderToolIcons.Create(icon, size);
            old?.Dispose();
        }
    }

    private void SyncToolbox()
    {
        foreach (ToolStripButton button in toolbox.Items.OfType<ToolStripButton>())
            button.Checked = (LadderDiagramTool)button.Tag! == diagram.ActiveTool;
    }

    private void SelectRungInViews(int rung)
    {
        diagram.Select(rung, 0, 0);
        SelectRung(rung);
    }

    private void PlaceWithTool(LadderCellEventArgs e)
    {
        if (EditingBlocked()) return;
        var clicked = new LadderCellPosition(e.Rung, e.Column, e.Lane);
        LadderDiagramTool tool = diagram.ActiveTool;
        if (tool == LadderDiagramTool.Erase)
        {
            ClearCell(clicked);
            return;
        }

        var entry = Tools.First(t => t.Tool == tool);
        LadderCell? cell = entry.Template is "EUP" or "EDN" or "END"
            ? LadderCellText.Parse(entry.Template)
            : cellPrompt(entry.Tip, "Complete o elemento. Exemplo: " + Example(tool), entry.Template);
        if (cell is null) return;

        bool parallel = tool is LadderDiagramTool.ParallelNO or LadderDiagramTool.ParallelNC;
        LadderCellPosition? target = LadderEditCommands.Target(cell.Kind, clicked, parallel, out string error);
        if (target is null)
        {
            status.Text = error;
            return;
        }
        ApplyCell(target.Value, cell, "Elemento colocado em L" + (target.Value.Rung + 1) + " C" + (target.Value.Column + 1) + ".");
    }

    private void EditCell(LadderCellPosition position)
    {
        if (EditingBlocked()) return;
        LadderCell current = LadderEditCommands.CellAt(project, position);
        LadderCell? cell = cellPrompt("Editar elemento", "Mnemônico e campos separados por espaço. Vazio apaga o elemento.", LadderCellText.Format(current));
        if (cell is null) return;
        LadderCellPosition? target = LadderEditCommands.Target(cell.Kind, position, position.Lane == 1, out string error);
        if (target is null)
        {
            status.Text = error;
            return;
        }
        if (target.Value != position && !cell.IsEmpty)
        {
            // Saída digitada numa célula de condição vai para a coluna 8; a célula editada fica vazia.
            history.Record(project);
            LadderEditCommands.Clear(project, position);
            LadderEditCommands.Put(project, target.Value, cell);
            AfterDiagramEdit(target.Value, "Saída colocada na coluna 8.");
            return;
        }
        ApplyCell(position, cell, cell.IsEmpty ? "Elemento apagado." : "Elemento atualizado.");
    }

    private void ClearCell(LadderCellPosition position)
    {
        if (EditingBlocked()) return;
        if (LadderEditCommands.CellAt(project, position).IsEmpty) return;
        history.Record(project);
        LadderEditCommands.Clear(project, position);
        AfterDiagramEdit(position, "Elemento apagado.");
    }

    private void MoveCell(LadderMoveRequestEventArgs e)
    {
        if (EditingBlocked()) return;
        var from = new LadderCellPosition(e.FromRung, e.FromColumn, e.Lane);
        LadderProjectModel before = projects.Deserialize(projects.Serialize(project));
        if (!LadderEditCommands.Move(project, from, e.ToRung, e.ToColumn, out string error))
        {
            if (error.Length > 0) status.Text = "Movimento recusado: " + error;
            return;
        }
        history.Record(before);
        AfterDiagramEdit(new LadderCellPosition(e.ToRung, e.ToColumn, e.Lane), "Elemento movido.");
    }

    private void CommentRung(int rung, string text)
    {
        if (EditingBlocked() || rung < 0 || rung >= project.Rungs.Count) return;
        string comment = LadderProjectService.NormalizeComment(text);
        if (project.Rungs[rung].Comment == comment) return;
        history.Record(project);
        project.Rungs[rung].Comment = comment;
        LoadIntoGrid();
        MarkDirty();
        status.Text = comment.Length > 0 ? "Comentário da linha " + (rung + 1) + " atualizado." : "Comentário da linha " + (rung + 1) + " removido.";
    }

    private void ApplyCell(LadderCellPosition position, LadderCell cell, string message)
    {
        if (LadderCellText.Format(cell) == LadderCellText.Format(LadderEditCommands.CellAt(project, position))) return;
        history.Record(project);
        LadderEditCommands.Put(project, position, cell);
        AfterDiagramEdit(position, message);
    }

    private void AfterDiagramEdit(LadderCellPosition position, string message)
    {
        LoadIntoGrid();
        MarkDirty();
        diagram.Select(position.Rung, position.Column, position.Lane);
        status.Text = message;
    }

    private LadderCell? PromptWithDialog(string title, string prompt, string initial)
    {
        using var dialog = new CellPromptDialog(title, prompt, initial);
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Result : null;
    }

    private static string Example(LadderDiagramTool tool) => tool switch
    {
        LadderDiagramTool.ContactNO or LadderDiagramTool.ParallelNO => "NO X0001",
        LadderDiagramTool.ContactNC or LadderDiagramTool.ParallelNC => "NC X0002",
        LadderDiagramTool.Coil => "COIL Y0001",
        LadderDiagramTool.Timer => "TMR V0001 1000",
        LadderDiagramTool.Counter => "CNT V0001 10",
        LadderDiagramTool.Set => "SET Y0001",
        LadderDiagramTool.Reset => "RST C0001",
        LadderDiagramTool.Function => "FUN F-13w D0001,10,D0002",
        _ => ""
    };
}
