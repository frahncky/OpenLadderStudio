using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Quadros WBP do TP02 para inspeção (dry-run). Recompila a cada mudança de estação, início
/// ou RI. Nada é transmitido: a ferramenta da camada Application não conhece porta serial.
/// </summary>
public sealed class Tp02WbpDialog : Form
{
    private readonly Tp02OfflineTools tools;
    private readonly LadderProjectModel project;
    private readonly NumericUpDown station = Number(0, Tp02OfflineTools.MaxStation, 1);
    private readonly NumericUpDown start = Number(0, Tp02OfflineTools.MaxStart, 0);
    private readonly NumericUpDown responseCode = Number(0, Tp02OfflineTools.MaxResponseCode, 5);
    private readonly TextBox output = new();
    private readonly Label state = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 6, 3, 3) };

    public Tp02WbpDialog(Tp02OfflineTools tools, LadderProjectModel project)
    {
        this.tools = tools;
        this.project = project;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Quadros WBP do TP02 (dry-run, nada é enviado ao PLC)";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(960, 600);
        MinimumSize = new Size(640, 400);
        ShowInTaskbar = false;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        bar.Controls.Add(Caption("Estação"));
        bar.Controls.Add(station);
        bar.Controls.Add(Caption("Início"));
        bar.Controls.Add(start);
        bar.Controls.Add(Caption("RI"));
        bar.Controls.Add(responseCode);
        var export = new Button { Text = "Exportar relatório...", AutoSize = true, Margin = new Padding(12, 3, 3, 3) };
        export.Click += (_, _) => ExportReport();
        bar.Controls.Add(export);
        bar.Controls.Add(state);

        output.Dock = DockStyle.Fill;
        output.Multiline = true;
        output.ReadOnly = true;
        output.WordWrap = false;
        output.ScrollBars = ScrollBars.Both;
        output.Font = new Font(FontFamily.GenericMonospace, 9F);

        Controls.Add(output);
        Controls.Add(bar);

        foreach (NumericUpDown number in new[] { station, start, responseCode })
            number.ValueChanged += (_, _) => Recompile();
        WindowFit.Attach(this);
        ModernPalette.Apply(this);
        ResumeLayout(false);
        PerformLayout();
        Recompile();
    }

    public string Report => output.Text;

    private void Recompile()
    {
        Tp02WbpPreview preview = tools.PreviewWbp(project, (int)station.Value, (int)start.Value, (int)responseCode.Value);
        output.Text = preview.Report.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        output.SelectionStart = 0;
        state.Text = preview.Success
            ? preview.StepCount + " passo(s), " + preview.Frames.Count + " quadro(s). Nada foi transmitido."
            : "Programa com erro: nenhum quadro WBP gerado.";
    }

    private void ExportReport()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Relatório TP02 (*.txt)|*.txt",
            DefaultExt = "txt",
            FileName = "tp02_precompilacao_dryrun.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, output.Text, System.Text.Encoding.UTF8);
            state.Text = "Relatório exportado. Nada foi transmitido.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Não foi possível exportar o relatório.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label Caption(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 6, 3, 3) };

    private static NumericUpDown Number(int minimum, int maximum, int value) =>
        new() { Minimum = minimum, Maximum = maximum, Value = value, Width = 70, Margin = new Padding(3) };
}
