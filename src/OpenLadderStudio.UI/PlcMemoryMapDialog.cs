using System.Globalization;
using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Mapa de memória do modelo alvo: áreas (nome, tipo, faixa de endereços, prefixo) numa
/// grade editável. Salvar valida tudo antes de gravar; Cancelar descarta. O arquivo é o
/// mesmo do editor legado.
/// </summary>
public sealed class PlcMemoryMapDialog : Form
{
    private readonly PlcMemoryMapStore store;
    private readonly PlcDeviceProfile profile;
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize };
    private readonly Label message = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 8, 3, 3) };
    private readonly Label summary = new() { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
    private bool error;

    private static readonly PlcMemoryAreaKind[] Kinds = Enum.GetValues<PlcMemoryAreaKind>();

    public PlcMemoryMapDialog(PlcMemoryMapStore store, PlcDeviceProfile profile)
    {
        this.store = store;
        this.profile = profile;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Mapa de memória — " + profile.DisplayName;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 520);
        MinimumSize = new Size(640, 380);
        ShowInTaskbar = false;
        MinimizeBox = false;

        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(12, 10, 12, 8) };
        header.Controls.Add(summary);

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Nome", FillWeight = 22 });
        var kind = new DataGridViewComboBoxColumn { Name = "kind", HeaderText = "Tipo", FillWeight = 22, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing };
        kind.Items.AddRange(Kinds.Select(PlcMemoryArea.KindName).Cast<object>().ToArray());
        grid.Columns.Add(kind);
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "start", HeaderText = "Início", FillWeight = 10 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "length", HeaderText = "Tamanho", FillWeight = 10 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "end", HeaderText = "Fim", FillWeight = 10, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "prefix", HeaderText = "Prefixo", FillWeight = 10 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "notes", HeaderText = "Observação", FillWeight = 30 });
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            UpdateEnd(grid.Rows[e.RowIndex]);
            Check();
        };
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            // O tipo (lista) vale assim que é escolhido, sem esperar sair da célula.
            if (grid.CurrentCell is DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        var add = new Button { Text = "Adicionar área", AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        add.Click += (_, _) => AddArea();
        var remove = new Button { Text = "Remover", AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        remove.Click += (_, _) => RemoveArea();
        var defaults = new Button { Text = "Restaurar padrão", AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        defaults.Click += (_, _) => { Fill(PlcMemoryMapStore.Defaults(profile)); message.Text = "Mapa padrão carregado. Salvar grava."; };
        var left = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Left, WrapContents = false, Margin = new Padding(0) };
        left.Controls.AddRange(new Control[] { add, remove, defaults, message });

        var save = new Button { Text = "Salvar", AutoSize = true, Margin = new Padding(6, 3, 0, 3) };
        save.Click += (_, _) => SaveAndClose();
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(6, 3, 0, 3) };
        var right = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0) };
        right.Controls.AddRange(new Control[] { cancel, save });
        CancelButton = cancel;

        // Rodapé com a altura dos botões (que crescem com o DPI): ações à esquerda, Salvar/Cancelar à direita.
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, RowCount = 1, Padding = new Padding(12, 6, 12, 8) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(left, 0, 0);
        footer.Controls.Add(right, 1, 0);

        Controls.Add(grid);
        Controls.Add(header);
        Controls.Add(footer);

        WindowFit.Attach(this);
        ModernPalette.Apply(this);
        ResumeLayout(false);
        PerformLayout();

        Fill(store.Load(profile));
    }

    public DataGridView Areas => grid;

    public string Message => message.Text;

    public void AddArea()
    {
        int next = grid.Rows.Count == 0 ? 0 : grid.Rows.Cast<DataGridViewRow>().Select(r => int.TryParse(CellText(r, "start"), out int s) && int.TryParse(CellText(r, "length"), out int l) ? s + l : 0).Max();
        Add(new PlcMemoryArea("Nova área", PlcMemoryAreaKind.HoldingRegister, Math.Min(next, PlcMemoryArea.MaxAddress), 10, "HR", string.Empty));
        grid.CurrentCell = grid.Rows[^1].Cells["name"];
        grid.BeginEdit(true);
        Check();
    }

    private void RemoveArea()
    {
        if (grid.CurrentRow is null) return;
        grid.Rows.Remove(grid.CurrentRow);
        Check();
    }

    /// <summary>Valida e grava; com erro, a janela continua aberta e a mensagem diz o que corrigir.</summary>
    public bool SaveAndClose()
    {
        if (grid.IsCurrentCellInEditMode) grid.EndEdit();
        if (!TryRead(out List<PlcMemoryArea> areas, out string problem))
        {
            ShowMessage(problem, true);
            return false;
        }
        try
        {
            store.Save(profile, areas);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowMessage("Não foi possível gravar o mapa: " + ex.Message, true);
            return false;
        }
        DialogResult = DialogResult.OK;
        Close();
        return true;
    }

    private void Fill(IReadOnlyList<PlcMemoryArea> areas)
    {
        grid.Rows.Clear();
        foreach (PlcMemoryArea area in areas) Add(area);
        Check();
    }

    private void Add(PlcMemoryArea area)
    {
        int row = grid.Rows.Add(area.Name, PlcMemoryArea.KindName(area.Kind),
            area.StartAddress.ToString(CultureInfo.InvariantCulture), area.Length.ToString(CultureInfo.InvariantCulture),
            string.Empty, area.Prefix, area.Notes);
        UpdateEnd(grid.Rows[row]);
    }

    private void UpdateEnd(DataGridViewRow row)
    {
        bool ok = int.TryParse(CellText(row, "start"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)
            & int.TryParse(CellText(row, "length"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int length);
        string end = ok && length > 0 ? (start + length - 1).ToString(CultureInfo.InvariantCulture) : "—";
        if (!Equals(row.Cells["end"].Value, end)) row.Cells["end"].Value = end;
    }

    /// <summary>Resumo e problemas a cada mudança, sem esperar o Salvar.</summary>
    private void Check()
    {
        summary.Text = profile.DisplayName + " · " + profile.Protocol + " · " + (grid.Rows.Count == 1 ? "1 área" : grid.Rows.Count + " áreas");
        if (!TryRead(out List<PlcMemoryArea> areas, out string problem)) { ShowMessage(problem, true); return; }
        IReadOnlyList<string> overlaps = PlcMemoryMapStore.Overlaps(areas);
        ShowMessage(overlaps.Count > 0 ? "Atenção: " + overlaps[0] : string.Empty, false);
    }

    private bool TryRead(out List<PlcMemoryArea> areas, out string problem)
    {
        areas = new List<PlcMemoryArea>();
        problem = string.Empty;
        foreach (DataGridViewRow row in grid.Rows)
        {
            string label = "Linha " + (row.Index + 1) + ": ";
            int kindIndex = Array.IndexOf(Kinds.Select(PlcMemoryArea.KindName).ToArray(), CellText(row, "kind"));
            if (!int.TryParse(CellText(row, "start"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)) { problem = label + "início precisa ser um número."; return false; }
            if (!int.TryParse(CellText(row, "length"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int length)) { problem = label + "tamanho precisa ser um número."; return false; }
            var area = new PlcMemoryArea(CellText(row, "name"), kindIndex < 0 ? PlcMemoryAreaKind.HoldingRegister : Kinds[kindIndex], start, length, CellText(row, "prefix"), CellText(row, "notes"));
            try { PlcMemoryMapStore.Validate(area); }
            catch (FormatException ex) { problem = label + ex.Message; return false; }
            areas.Add(area);
        }
        return true;
    }

    private void ShowMessage(string text, bool isError)
    {
        error = isError;
        message.Text = text;
        message.ForeColor = isError ? ModernPalette.Danger : ModernPalette.Muted;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        summary.ForeColor = ModernPalette.Muted;
        message.ForeColor = error ? ModernPalette.Danger : ModernPalette.Muted;
    }

    private static string CellText(DataGridViewRow row, string column) => Convert.ToString(row.Cells[column].Value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
}
