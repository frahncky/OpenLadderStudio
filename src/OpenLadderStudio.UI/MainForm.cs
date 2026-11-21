using OpenLadderStudio.Application;

namespace OpenLadderStudio.UI;

/// <summary>
/// Primeiro fluxo funcional da trilha moderna: criar, abrir e salvar .pladder pela camada
/// Application e editar as oito colunas de cada linha (via série e via paralela).
/// </summary>
public sealed class MainForm : Form
{
    private const int CommentColumn = LadderRungModel.ColumnCount + 1;

    private readonly PlcDriverCatalog catalog;
    private readonly LadderProjectService projects = new();
    private readonly DataGridView grid = new();
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private LadderProjectModel project;
    private string? currentPath;
    private bool dirty;
    private bool loading;

    public MainForm(PlcDriverCatalog catalog)
    {
        this.catalog = catalog;
        project = projects.CreateNew();

        // Medidas abaixo estão em pixels a 96 DPI; AutoScaleDimensions faz o WinForms
        // escalá-las para o DPI real (125%, 175%...) quando o layout é retomado.
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1100, 680);
        MinimumSize = new Size(720, 480);
        KeyPreview = true;

        BuildGrid();
        var menu = BuildMenu();
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(status);

        Controls.Add(grid);
        Controls.Add(menu);
        Controls.Add(statusStrip);
        MainMenuStrip = menu;

        LoadIntoGrid();
        FormClosing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        Load += (_, _) => FitToWorkingArea();
        DpiChanged += (_, _) => BeginInvoke(FitToWorkingArea);
        ResumeLayout(false);
        PerformLayout();
    }

    // A 175% numa tela 1080p a área útil tem cerca de 1097x590 pixels lógicos: o tamanho
    // escalado não pode passar dela, senão a janela abre cortada.
    private void FitToWorkingArea()
    {
        Rectangle area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var file = new ToolStripMenuItem("&Arquivo");
        file.DropDownItems.Add(new ToolStripMenuItem("&Novo", null, (_, _) => NewProject(), Keys.Control | Keys.N));
        file.DropDownItems.Add(new ToolStripMenuItem("&Abrir...", null, (_, _) => OpenProject(), Keys.Control | Keys.O));
        file.DropDownItems.Add(new ToolStripMenuItem("&Salvar", null, (_, _) => SaveProject(false), Keys.Control | Keys.S));
        file.DropDownItems.Add(new ToolStripMenuItem("Salvar &como...", null, (_, _) => SaveProject(true), Keys.Control | Keys.Shift | Keys.S));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("Sai&r", null, (_, _) => Close()));

        var edit = new ToolStripMenuItem("&Editar");
        edit.DropDownItems.Add(new ToolStripMenuItem("&Inserir linha abaixo", null, (_, _) => InsertRung(), Keys.Control | Keys.Enter));
        edit.DropDownItems.Add(new ToolStripMenuItem("&Excluir linha", null, (_, _) => DeleteRung(), Keys.Control | Keys.Delete));

        var help = new ToolStripMenuItem("A&juda");
        help.DropDownItems.Add(new ToolStripMenuItem("&Elementos...", null, (_, _) => ShowElementHelp()));
        help.DropDownItems.Add(new ToolStripMenuItem("&Drivers disponíveis...", null, (_, _) => ShowDrivers()));

        menu.Items.AddRange(new ToolStripItem[] { file, edit, help });
        return menu;
    }

    private void BuildGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AllowUserToOrderColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Altura da linha acompanha a fonte, inclusive quando o DPI muda com a janela aberta.
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Linha", ReadOnly = true, FillWeight = 60, SortMode = DataGridViewColumnSortMode.NotSortable });
        for (int column = 1; column <= LadderRungModel.ColumnCount; column++)
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "C" + column, FillWeight = 80, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Comentário", FillWeight = 160, SortMode = DataGridViewColumnSortMode.NotSortable });

        grid.CellValidating += OnCellValidating;
        grid.CellEndEdit += (_, e) => grid.Rows[e.RowIndex].ErrorText = string.Empty;
        grid.CellValueChanged += OnCellValueChanged;
        grid.KeyDown += OnGridKeyDown;
    }

    // Cada linha Ladder ocupa duas linhas da grade: par = via série, ímpar = via paralela.
    private static int RungOf(int row) => row / 2;
    private static bool IsParallel(int row) => row % 2 == 1;

    private LadderCell CellAt(int row, int column)
    {
        LadderRungModel rung = project.Rungs[RungOf(row)];
        return (IsParallel(row) ? rung.Parallel : rung.Series)[column - 1];
    }

    private void LoadIntoGrid()
    {
        loading = true;
        try
        {
            grid.Rows.Clear();
            for (int index = 0; index < project.Rungs.Count; index++)
            {
                LadderRungModel rung = project.Rungs[index];
                var series = new object[CommentColumn + 1];
                var parallel = new object[CommentColumn + 1];
                series[0] = (index + 1).ToString("000");
                parallel[0] = "  paralelo";
                for (int column = 1; column <= LadderRungModel.ColumnCount; column++)
                {
                    series[column] = LadderCellText.Format(rung.Series[column - 1]);
                    parallel[column] = LadderCellText.Format(rung.Parallel[column - 1]);
                }
                series[CommentColumn] = rung.Comment;
                parallel[CommentColumn] = string.Empty;
                grid.Rows.Add(series);
                int parallelRow = grid.Rows.Add(parallel);
                grid.Rows[parallelRow].Cells[CommentColumn].ReadOnly = true;
            }
        }
        finally
        {
            loading = false;
        }
        UpdateTitle();
    }

    private void OnCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (loading || e.ColumnIndex < 1 || e.ColumnIndex > LadderRungModel.ColumnCount) return;
        try
        {
            LadderCellText.Parse(Convert.ToString(e.FormattedValue));
            grid.Rows[e.RowIndex].ErrorText = string.Empty;
        }
        catch (FormatException ex)
        {
            grid.Rows[e.RowIndex].ErrorText = ex.Message;
            status.Text = ex.Message;
            e.Cancel = true;
        }
    }

    private void OnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (loading || e.RowIndex < 0) return;
        DataGridViewCell gridCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];

        loading = true;
        try
        {
            if (e.ColumnIndex == CommentColumn)
            {
                string comment = LadderProjectService.NormalizeComment(Convert.ToString(gridCell.Value));
                project.Rungs[RungOf(e.RowIndex)].Comment = comment;
                gridCell.Value = comment;
            }
            else if (e.ColumnIndex >= 1)
            {
                LadderCell parsed = LadderCellText.Parse(Convert.ToString(gridCell.Value));
                LadderCell target = CellAt(e.RowIndex, e.ColumnIndex);
                target.Kind = parsed.Kind;
                target.Address = parsed.Address;
                target.Parameter = parsed.Parameter;
                target.Mode = parsed.Mode;
                gridCell.Value = LadderCellText.Format(target);
            }
        }
        finally
        {
            loading = false;
        }
        MarkDirty();
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Delete || e.Modifiers != Keys.None || grid.CurrentCell is null || grid.IsCurrentCellInEditMode) return;
        DataGridViewCell cell = grid.CurrentCell;
        if (cell.ReadOnly || cell.ColumnIndex == 0) return;
        cell.Value = string.Empty;
        e.Handled = true;
    }

    private void InsertRung()
    {
        int rung = grid.CurrentCell is null ? project.Rungs.Count - 1 : RungOf(grid.CurrentCell.RowIndex);
        project.Rungs.Insert(rung + 1, new LadderRungModel());
        LoadIntoGrid();
        SelectRung(rung + 1);
        MarkDirty();
    }

    private void DeleteRung()
    {
        if (grid.CurrentCell is null) return;
        int rung = RungOf(grid.CurrentCell.RowIndex);
        project.Rungs.RemoveAt(rung);
        if (project.Rungs.Count == 0) project.Rungs.Add(new LadderRungModel());
        LoadIntoGrid();
        SelectRung(Math.Min(rung, project.Rungs.Count - 1));
        MarkDirty();
    }

    private void SelectRung(int rung)
    {
        if (grid.Rows.Count == 0) return;
        grid.CurrentCell = grid.Rows[rung * 2].Cells[1];
    }

    private void NewProject()
    {
        if (!ConfirmDiscard()) return;
        project = projects.CreateNew();
        currentPath = null;
        dirty = false;
        LoadIntoGrid();
        status.Text = "Projeto novo.";
    }

    private void OpenProject()
    {
        if (!ConfirmDiscard()) return;
        using var dialog = new OpenFileDialog
        {
            Filter = "Projeto Ladder moderno (*.pladder)|*.pladder|Todos os arquivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            project = projects.Open(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowError("Não foi possível abrir o projeto.", ex);
            return;
        }
        currentPath = dialog.FileName;
        dirty = false;
        LoadIntoGrid();
        status.Text = "Aberto: " + currentPath;
    }

    private bool SaveProject(bool askPath)
    {
        // Confirma a edição em andamento para não perder o que está na célula.
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return false;

        string? path = currentPath;
        if (askPath || string.IsNullOrEmpty(path))
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Projeto Ladder moderno (*.pladder)|*.pladder",
                DefaultExt = "pladder",
                FileName = string.IsNullOrEmpty(path) ? "Projeto_TP02.pladder" : Path.GetFileName(path)
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }

        try
        {
            projects.Save(project, path!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowError("Não foi possível salvar o projeto.", ex);
            return false;
        }
        currentPath = path;
        dirty = false;
        UpdateTitle();
        status.Text = "Salvo: " + currentPath;
        return true;
    }

    private bool ConfirmDiscard()
    {
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return false;
        if (!dirty) return true;
        DialogResult answer = MessageBox.Show(this,
            "O projeto tem alterações não salvas. Deseja salvar antes de continuar?",
            "OpenLadder Studio", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        if (answer == DialogResult.Cancel) return false;
        if (answer == DialogResult.Yes) return SaveProject(false);
        return true;
    }

    private void MarkDirty()
    {
        if (dirty) return;
        dirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string name = string.IsNullOrEmpty(currentPath) ? "Sem título" : Path.GetFileName(currentPath);
        Text = (dirty ? "* " : string.Empty) + name + " - OpenLadder Studio";
    }

    private void ShowElementHelp()
    {
        MessageBox.Show(this,
            "Digite o elemento na célula: mnemônico e campos separados por espaço.\n\n" +
            "NO X0  contato aberto\nNC X0  contato fechado\nCOIL Y0  bobina\n" +
            "TMR T0 K10 [modo]  temporizador\nCNT C0 K5  contador\nSET Y0 / RST Y0\n" +
            "EUP / EDN  bordas\nFUN nome [parâmetro]\nEND  fim do programa\n\n" +
            "Célula vazia ou Delete apaga o elemento.",
            "Elementos Ladder", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowDrivers()
    {
        string list = string.Join("\n", catalog.Drivers.Select(driver => "• " + driver.DisplayName));
        MessageBox.Show(this, list, "Drivers disponíveis", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(this, message + "\n\n" + ex.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
