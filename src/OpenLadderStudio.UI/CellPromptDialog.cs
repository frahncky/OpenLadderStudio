using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Caixa para digitar um elemento no formato de célula ("TMR V0001 1000"). Valida com o mesmo
/// LadderCellText da grade e não fecha enquanto o texto for inválido.
/// </summary>
public sealed class CellPromptDialog : Form
{
    private readonly TextBox input = new() { Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 4) };
    private readonly Label error = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };

    public CellPromptDialog(string title, string prompt, string initial)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 150);
        Padding = new Padding(12);

        var label = new Label { Text = prompt, Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(436, 0) };
        input.Text = initial;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = "OK", AutoSize = true };
        ok.Click += (_, _) => Accept();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(error);
        Controls.Add(input);
        Controls.Add(label);
        Controls.Add(buttons);
        WindowFit.Attach(this);
        ModernPalette.Apply(this);
        error.ForeColor = ModernPalette.Danger;
        ResumeLayout(false);
        PerformLayout();
        Shown += (_, _) =>
        {
            input.Focus();
            input.SelectionStart = input.Text.Length;
        };
    }

    public LadderCell? Result { get; private set; }

    public string InputText
    {
        get => input.Text;
        set => input.Text = value;
    }

    public string ErrorText => error.Text;

    /// <summary>Valida e fecha com OK; com texto inválido mostra o motivo e continua aberta.</summary>
    public void Accept()
    {
        try
        {
            Result = LadderCellText.Parse(input.Text);
        }
        catch (FormatException ex)
        {
            error.Text = ex.Message;
            input.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
