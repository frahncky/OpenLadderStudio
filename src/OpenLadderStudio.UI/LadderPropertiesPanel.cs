using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Propriedades da seleção do diagrama: tipo e posição do elemento, campos editáveis
/// (endereço, parâmetro, retentivo) e o comentário da linha. Não altera o projeto: pede
/// as mudanças por eventos, e a janela as aplica com desfazer e marcação de alteração.
/// </summary>
public sealed class LadderPropertiesPanel : UserControl
{
    private readonly Label title = new() { AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
    private readonly Label position = new() { AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
    private readonly Label addressLabel = new() { AutoSize = true, Margin = new Padding(0, 4, 0, 2) };
    private readonly TextBox address = new() { CharacterCasing = CharacterCasing.Upper, Margin = new Padding(0, 0, 0, 6) };
    private readonly Label parameterLabel = new() { AutoSize = true, Margin = new Padding(0, 4, 0, 2) };
    private readonly TextBox parameter = new() { CharacterCasing = CharacterCasing.Upper, Margin = new Padding(0, 0, 0, 6) };
    private readonly CheckBox retentive = new() { Text = "Retentivo", AutoSize = true, Margin = new Padding(0, 2, 0, 6) };
    private readonly Button apply = new() { Text = "Aplicar", AutoSize = true, Margin = new Padding(0, 2, 0, 4) };
    private readonly Label error = new() { AutoSize = true, MaximumSize = new Size(260, 0), Margin = new Padding(0, 0, 0, 8) };
    private readonly Label commentLabel = new() { Text = "Comentário da linha", AutoSize = true, Margin = new Padding(0, 14, 0, 2) };
    private readonly TextBox comment = new() { Multiline = true, AcceptsReturn = false, ScrollBars = ScrollBars.Vertical, Height = 72, Margin = new Padding(0, 0, 0, 2) };
    private readonly Label commentHint = new() { AutoSize = true, Margin = new Padding(0, 0, 0, 0) };
    // Linhas fixas: a ordem não depende da ordem dos filhos, que muda quando o tema recria uma caixa.
    private readonly TableLayoutPanel stack = new() { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, Padding = new Padding(14, 12, 10, 12) };

    private LadderCell cell = new();
    private string shownComment = string.Empty;
    private bool readOnly;

    public LadderPropertiesPanel()
    {
        Control[] rows =
        {
            title, position, addressLabel, address, parameterLabel, parameter, retentive, apply, error,
            commentLabel, comment, commentHint
        };
        stack.RowCount = rows.Length + 1;
        for (int row = 0; row < rows.Length; row++)
        {
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.Controls.Add(rows[row], 0, row);
        }
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(stack);
        title.Font = new Font(Font.FontFamily, Font.Size * 1.25f, FontStyle.Bold);

        apply.Click += (_, _) => CommitFields();
        foreach (TextBox box in new[] { address, parameter })
        {
            box.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitFields(); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; ShowFields(); }
            };
            box.TextChanged += (_, _) => error.Text = string.Empty;
            // Sair do campo grava, como nas outras janelas de propriedades.
            box.Leave += (_, _) => { if (FieldsChanged()) CommitFields(); };
        }
        retentive.CheckedChanged += (_, _) =>
        {
            error.Text = string.Empty;
            if (retentive.Focused && FieldsChanged()) CommitFields();
        };
        comment.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitComment(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; comment.Text = shownComment; }
        };
        comment.Leave += (_, _) => CommitComment();
        Resize += (_, _) => FitWidths();
        ModernPalette.Changed += (_, _) => Recolor();
    }

    /// <summary>Título 25% maior que a fonte do painel, que o WinForms escala com o DPI do monitor.</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Font? old = title.Font == Font ? null : title.Font;
        title.Font = new Font(Font.FontFamily, Font.Size * 1.25f, FontStyle.Bold);
        old?.Dispose();
    }

    /// <summary>Pede a troca do elemento selecionado pelo elemento com os campos novos.</summary>
    public event EventHandler<LadderCell>? CellChangeRequested;

    /// <summary>Pede a gravação do comentário da linha selecionada.</summary>
    public event EventHandler<string>? CommentChangeRequested;

    public int Rung { get; private set; } = -1;

    public bool ReadOnly
    {
        get => readOnly;
        set
        {
            readOnly = value;
            ShowFields();
        }
    }

    public string ErrorText => error.Text;

    /// <summary>Mostra a seleção. Campos com texto não gravado são descartados.</summary>
    public void Show(LadderCell selected, int rung, int column, int lane, string rungComment)
    {
        // Cópia: a grade altera as células do projeto no lugar, e o painel compara com o que mostrou.
        cell = LadderCellText.Parse(LadderCellText.Format(selected));
        Rung = rung;
        position.Text = "Linha " + (rung + 1) + " · coluna C" + (column + 1) + (lane > 0 ? " · ramo paralelo" : string.Empty);
        shownComment = rungComment;
        comment.Text = rungComment;
        ShowFields();
    }

    /// <summary>Grava o que estiver digitado e ainda não aplicado (antes de salvar, abrir ou criar).</summary>
    public bool CommitPending()
    {
        if (comment.Text != shownComment) CommitComment();
        return !FieldsChanged() || CommitFields();
    }

    private bool FieldsChanged()
    {
        if (readOnly) return false;
        LadderCellFields fields = LadderCellProperties.Describe(cell);
        return fields.AddressLabel is not null
            && (address.Text.Trim() != fields.Address || parameter.Text.Trim() != fields.Parameter || retentive.Checked != fields.Retentive);
    }

    // Os campos de comentário e de elemento são estes; o teste da janela usa os mesmos.
    public TextBox AddressBox => address;
    public TextBox ParameterBox => parameter;
    public CheckBox RetentiveBox => retentive;
    public TextBox CommentBox => comment;

    private void ShowFields()
    {
        LadderCellFields fields = LadderCellProperties.Describe(cell);
        title.Text = fields.KindName;
        bool hasAddress = fields.AddressLabel is not null;
        addressLabel.Visible = address.Visible = hasAddress;
        addressLabel.Text = fields.AddressLabel ?? string.Empty;
        address.Text = fields.Address;
        bool hasParameter = fields.ParameterLabel is not null;
        parameterLabel.Visible = parameter.Visible = hasParameter;
        parameterLabel.Text = fields.ParameterLabel ?? string.Empty;
        parameter.Text = fields.Parameter;
        retentive.Visible = fields.CanBeRetentive;
        retentive.Checked = fields.Retentive;
        apply.Visible = hasAddress;
        foreach (Control control in new Control[] { address, parameter, retentive, apply, comment })
            control.Enabled = !readOnly;
        commentHint.Text = readOnly ? "Somente leitura durante a simulação." : string.Empty;
        commentHint.Visible = readOnly;
        error.Text = string.Empty;
        Recolor();
        FitWidths();
    }

    private bool CommitFields()
    {
        if (readOnly || cell.IsEmpty) return true;
        try
        {
            LadderCell changed = LadderCellProperties.With(cell, address.Text, parameter.Text, retentive.Checked);
            CellChangeRequested?.Invoke(this, changed);
            return true;
        }
        catch (FormatException ex)
        {
            error.Text = ex.Message;
            return false;
        }
    }

    private void CommitComment()
    {
        if (readOnly || Rung < 0 || comment.Text == shownComment) return;
        shownComment = comment.Text;
        CommentChangeRequested?.Invoke(this, comment.Text);
    }

    private void FitWidths()
    {
        int width = Math.Max(120, stack.ClientSize.Width - stack.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (Control control in new Control[] { address, parameter, comment })
            control.Width = width;
        foreach (Label label in new[] { error })
            label.MaximumSize = new Size(width, 0);
    }

    private void Recolor()
    {
        BackColor = stack.BackColor = ModernPalette.Chrome;
        foreach (Label label in new[] { position, commentHint, addressLabel, parameterLabel, commentLabel })
            label.ForeColor = ModernPalette.Muted;
        title.ForeColor = ModernPalette.Fore;
        error.ForeColor = ModernPalette.Danger;
        retentive.ForeColor = ModernPalette.Fore;
    }
}
