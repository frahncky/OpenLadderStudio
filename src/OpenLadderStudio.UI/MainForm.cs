using System.Text.RegularExpressions;
using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Primeiro fluxo funcional da trilha moderna: criar, abrir e salvar .pladder pela camada
/// Application e editar as oito colunas de cada linha (via série e via paralela).
/// </summary>
public sealed partial class MainForm : Form
{
    private const int CommentColumn = LadderRungModel.ColumnCount + 1;

    private readonly PlcDriverCatalog catalog;
    private readonly LadderProjectService projects = new();
    private readonly Tp02ProgramVerifier verifier = new();
    private readonly LadderEditHistory history;
    private readonly DataGridView grid = new();
    private readonly TextBox report = new();
    private readonly Splitter reportSplitter = new() { Dock = DockStyle.Bottom, Visible = false };
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private LadderProjectModel project;
    private string? currentPath;
    private bool dirty;
    // Texto .pladder do último estado salvo ou aberto: desfazer até ele limpa o "*" do título.
    private string savedState;
    private bool loading;
    private bool verificationStale;

    public MainForm(PlcDriverCatalog catalog)
    {
        this.catalog = catalog;
        project = projects.CreateNew();
        history = new LadderEditHistory(projects);
        savedState = projects.Serialize(project);

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
        BuildReport();
        BuildSimulationPanel();
        var menu = BuildMenu();
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(status);

        // Ordem de encaixe: barra de status, menu, relatório e divisor embaixo; a grade ocupa o resto.
        Controls.Add(grid);
        Controls.Add(simulationSplitter);
        Controls.Add(simulationPanel);
        Controls.Add(synopticSplitter);
        Controls.Add(synoptic);
        Controls.Add(reportSplitter);
        Controls.Add(report);
        Controls.Add(menu);
        Controls.Add(statusStrip);
        MainMenuStrip = menu;

        LoadIntoGrid();
        FormClosing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; else StopSimulation(); };
        WindowFit.Attach(this);
        // Paleta única (tokens do núcleo, os mesmos do editor legado) e o tema salvo pelo usuário.
        ModernPalette.Apply(this);
        ModernPalette.Changed += OnThemeChanged;
        Disposed += (_, _) => ModernPalette.Changed -= OnThemeChanged;
        ResumeLayout(false);
        PerformLayout();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ModernPalette.Apply(this);
        RepaintPoweredRungs();
        synoptic.RefreshScene();
        status.Text = ModernPalette.IsDark ? "Tema escuro." : "Tema claro.";
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
        file.DropDownItems.Add(new ToolStripMenuItem("&Importar IL do TP02...", null, (_, _) => ImportIl()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("Sai&r", null, (_, _) => Close()));

        var edit = new ToolStripMenuItem("&Editar");
        var undoItem = new ToolStripMenuItem("&Desfazer", null, (_, _) => UndoEdit(), Keys.Control | Keys.Z);
        var redoItem = new ToolStripMenuItem("&Refazer", null, (_, _) => RedoEdit(), Keys.Control | Keys.Y);
        edit.DropDownItems.Add(undoItem);
        edit.DropDownItems.Add(redoItem);
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownOpening += (_, _) =>
        {
            undoItem.Enabled = !simulating && (history.CanUndo || grid.IsCurrentCellInEditMode);
            redoItem.Enabled = !simulating && history.CanRedo;
        };
        edit.DropDownItems.Add(new ToolStripMenuItem("&Inserir linha abaixo", null, (_, _) => InsertRung(), Keys.Control | Keys.Enter));
        edit.DropDownItems.Add(new ToolStripMenuItem("&Excluir linha", null, (_, _) => DeleteRung(), Keys.Control | Keys.Delete));

        var program = new ToolStripMenuItem("&Programa");
        program.DropDownItems.Add(new ToolStripMenuItem("&Verificar para TP02", null, (_, _) => VerifyProgram(), Keys.F7));
        program.DropDownItems.Add(new ToolStripMenuItem("&Ocultar resultado", null, (_, _) => ShowReport(false), Keys.Shift | Keys.F7));
        program.DropDownItems.Add(new ToolStripMenuItem("&Quadros WBP do TP02 (dry-run)...", null, (_, _) => ShowWbpPreview()));
        program.DropDownItems.Add(new ToolStripMenuItem("Exportar &HEX do TP02...", null, (_, _) => ExportHex()));
        program.DropDownItems.Add(new ToolStripSeparator());
        program.DropDownItems.Add(simulateItem);
        program.DropDownItems.Add(new ToolStripMenuItem("&Reiniciar simulação", null, (_, _) => ResetSimulation(), Keys.Control | Keys.F5));
        program.DropDownItems.Add(plantMenu);
        program.DropDownItems.Add(faultsMenu);
        program.DropDownItems.Add(sampleItem);

        var view = new ToolStripMenuItem("E&xibir");
        var darkItem = new ToolStripMenuItem("Tema &escuro") { CheckOnClick = false };
        darkItem.Click += (_, _) => ModernPalette.Use(!ModernPalette.IsDark);
        view.DropDownOpening += (_, _) => darkItem.Checked = ModernPalette.IsDark;
        view.DropDownItems.Add(darkItem);

        var help = new ToolStripMenuItem("A&juda");
        help.DropDownItems.Add(new ToolStripMenuItem("&Elementos...", null, (_, _) => ShowElementHelp()));
        help.DropDownItems.Add(new ToolStripMenuItem("&Drivers disponíveis...", null, (_, _) => ShowDrivers()));

        menu.Items.AddRange(new ToolStripItem[] { file, edit, view, program, help });
        return menu;
    }

    private void BuildReport()
    {
        report.Dock = DockStyle.Bottom;
        report.Height = 200;
        report.Multiline = true;
        report.ReadOnly = true;
        report.WordWrap = false;
        report.ScrollBars = ScrollBars.Both;
        report.Font = new Font(FontFamily.GenericMonospace, 9F);
        report.Visible = false;
    }

    // O relatório é de outro projeto depois de Novo ou Abrir: não pode continuar na tela.
    private void ClearVerification()
    {
        report.Clear();
        ShowReport(false);
        verificationStale = false;
    }

    private void ShowReport(bool visible)
    {
        report.Visible = visible;
        reportSplitter.Visible = visible;
    }

    /// <summary>
    /// Pré-compila o programa para o TP02 e mostra erros, avisos e passos gerados.
    /// Nada é transmitido: o verificador da camada Application não conhece porta nem driver.
    /// </summary>
    private void VerifyProgram()
    {
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;

        LadderVerificationResult result = verifier.Verify(project);
        report.Text = result.Report.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        report.SelectionStart = 0;
        report.ScrollToCaret();
        ShowReport(true);
        verificationStale = false;

        if (result.Success)
        {
            status.Text = "Verificação TP02: sem erros, " + result.Steps.Count + " passo(s)"
                + (result.Warnings.Count > 0 ? ", " + result.Warnings.Count + " aviso(s)." : ".");
            return;
        }

        status.Text = "Verificação TP02: " + result.Errors.Count + " erro(s). Nada foi transmitido.";
        int? rung = FirstRungOf(result.Errors);
        if (rung is int number && number >= 1 && number <= project.Rungs.Count) SelectRung(number - 1);
    }

    private static int? FirstRungOf(IEnumerable<string> messages)
    {
        foreach (string message in messages)
        {
            Match match = Regex.Match(message, @"^(?:Rung|Linha)\s+(\d+)", RegexOptions.IgnoreCase);
            if (match.Success) return int.Parse(match.Groups[1].Value);
        }
        return null;
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

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Linha", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells, SortMode = DataGridViewColumnSortMode.NotSortable });
        // C8 é a coluna de saída: recebe TMR, CNT e FUN com parâmetros, os textos mais longos.
        for (int column = 1; column <= LadderRungModel.ColumnCount; column++)
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "C" + column,
                FillWeight = column == LadderRungModel.ColumnCount ? 130 : 80,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Comentário", FillWeight = 140, SortMode = DataGridViewColumnSortMode.NotSortable });

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
                LadderRungModel rung = project.Rungs[RungOf(e.RowIndex)];
                if (rung.Comment != comment) history.Record(project);
                rung.Comment = comment;
                gridCell.Value = comment;
            }
            else if (e.ColumnIndex >= 1)
            {
                LadderCell parsed = LadderCellText.Parse(Convert.ToString(gridCell.Value));
                LadderCell target = CellAt(e.RowIndex, e.ColumnIndex);
                if (LadderCellText.Format(parsed) != LadderCellText.Format(target)) history.Record(project);
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
        if (e.KeyCode != Keys.Delete || e.Modifiers != Keys.None || grid.CurrentCell is null || grid.IsCurrentCellInEditMode || simulating) return;
        DataGridViewCell cell = grid.CurrentCell;
        if (cell.ReadOnly || cell.ColumnIndex == 0) return;
        cell.Value = string.Empty;
        e.Handled = true;
    }

    private void InsertRung()
    {
        if (EditingBlocked()) return;
        int rung = grid.CurrentCell is null ? project.Rungs.Count - 1 : RungOf(grid.CurrentCell.RowIndex);
        history.Record(project);
        project.Rungs.Insert(rung + 1, new LadderRungModel());
        LoadIntoGrid();
        SelectRung(rung + 1);
        MarkDirty();
    }

    private void DeleteRung()
    {
        if (EditingBlocked()) return;
        if (grid.CurrentCell is null) return;
        int rung = RungOf(grid.CurrentCell.RowIndex);
        history.Record(project);
        project.Rungs.RemoveAt(rung);
        if (project.Rungs.Count == 0) project.Rungs.Add(new LadderRungModel());
        LoadIntoGrid();
        SelectRung(Math.Min(rung, project.Rungs.Count - 1));
        MarkDirty();
    }

    private void UndoEdit()
    {
        if (EditingBlocked()) return;
        // Com a célula em edição, Ctrl+Z descarta o que foi digitado nela, como numa caixa de texto.
        if (grid.IsCurrentCellInEditMode)
        {
            grid.CancelEdit();
            grid.EndEdit();
            status.Text = "Edição da célula descartada.";
            return;
        }
        if (!history.CanUndo) { status.Text = "Nada para desfazer."; return; }
        RestoreFromHistory(history.Undo(project), "Última alteração desfeita.");
    }

    private void RedoEdit()
    {
        if (EditingBlocked()) return;
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;
        if (!history.CanRedo) { status.Text = "Nada para refazer."; return; }
        RestoreFromHistory(history.Redo(project), "Alteração refeita.");
    }

    private void RestoreFromHistory(LadderProjectModel restored, string message)
    {
        int row = grid.CurrentCell?.RowIndex ?? 0;
        int column = grid.CurrentCell?.ColumnIndex ?? 1;
        project = restored;
        LoadIntoGrid();
        row = Math.Min(row, grid.Rows.Count - 1);
        if (row >= 0) grid.CurrentCell = grid.Rows[row].Cells[Math.Max(column, 1)];
        MarkDirty();
        status.Text = message;
    }

    private void SelectRung(int rung)
    {
        if (grid.Rows.Count == 0) return;
        grid.CurrentCell = grid.Rows[rung * 2].Cells[1];
    }

    private void NewProject()
    {
        if (!ConfirmDiscard()) return;
        StopSimulation();
        project = projects.CreateNew();
        currentPath = null;
        dirty = false;
        history.Clear();
        savedState = projects.Serialize(project);
        LoadIntoGrid();
        ClearVerification();
        status.Text = "Projeto novo.";
    }

    private void OpenProject()
    {
        if (!ConfirmDiscard()) return;
        StopSimulation();
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
        history.Clear();
        savedState = projects.Serialize(project);
        LoadIntoGrid();
        ClearVerification();
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
        savedState = projects.Serialize(project);
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
        if (report.Visible && !verificationStale)
        {
            verificationStale = true;
            status.Text = "Programa alterado desde a última verificação. F7 verifica de novo.";
        }
        dirty = projects.Serialize(project) != savedState;
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
            "NO X0001  contato aberto\nNC X0002  contato fechado\nCOIL Y0001  bobina\n" +
            "TMR V0001 1000 [modo]  temporizador\nCNT V0001 10  contador\nSET Y0001 / RST C0001\n" +
            "EUP / EDN  bordas\nFUN F-13w D0001,10,D0002  função\nEND  fim do programa\n\n" +
            "Endereços no formato do TP02, com quatro dígitos (X0001, não X0).\n" +
            "Célula vazia ou Delete apaga o elemento. F7 verifica o programa para o TP02.\n" +
            "F5 simula no PLC virtual: duplo clique numa entrada X a aciona.",
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
