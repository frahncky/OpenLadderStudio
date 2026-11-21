using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Barra de status em segmentos: mensagem à esquerda; à direita, modo (edição ou simulação),
/// posição selecionada, tamanho do programa, zoom e alvo. Os segmentos são relidos do estado
/// atual a cada 150 ms, então nenhum fluxo precisa lembrar de atualizá-los.
/// </summary>
public sealed partial class MainForm
{
    private readonly ToolStripStatusLabel modeSegment = Segment("EDIÇÃO", "Modo do editor");
    private readonly ToolStripStatusLabel positionSegment = Segment(string.Empty, "Linha e coluna selecionadas");
    private readonly ToolStripStatusLabel sizeSegment = Segment(string.Empty, "Linhas do programa");
    private readonly ToolStripStatusLabel zoomSegment = Segment("Zoom 100%", "Zoom do diagrama (clique para voltar a 100%)");
    private readonly ToolStripStatusLabel targetSegment = Segment("Alvo: WEG TP02", "Controlador para o qual o programa é verificado");

    private readonly System.Windows.Forms.Timer statusRefresh = new() { Interval = 150 };

    private StatusStrip BuildStatusBar()
    {
        var strip = new StatusStrip { SizingGrip = false, Padding = new Padding(8, 3, 8, 3) };
        status.Margin = new Padding(0, 3, 8, 2);
        modeSegment.Tag = ModernStatus.Off;
        modeSegment.BorderSides = ToolStripStatusLabelBorderSides.None;
        modeSegment.Margin = new Padding(8, 4, 8, 3);
        modeSegment.Font = new Font("Segoe UI Semibold", 8.25F, FontStyle.Bold);
        zoomSegment.IsLink = false;
        zoomSegment.Click += (_, _) => diagram.ZoomPercent = 100;
        strip.Items.AddRange(new ToolStripItem[] { status, modeSegment, positionSegment, sizeSegment, zoomSegment, targetSegment });
        statusRefresh.Tick += RefreshStatusSegments;
        statusRefresh.Start();
        Disposed += (_, _) => statusRefresh.Dispose();
        Shown += (_, _) => RefreshStatusSegments(null, EventArgs.Empty);
        return strip;
    }

    private static ToolStripStatusLabel Segment(string text, string tip) => new(text)
    {
        ToolTipText = tip,
        BorderSides = ToolStripStatusLabelBorderSides.Left,
        Padding = new Padding(8, 0, 8, 0),
        Margin = new Padding(0, 3, 0, 2)
    };

    private void RefreshStatusSegments(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        SetSegment(modeSegment, simulating ? "SIMULANDO" : "EDIÇÃO");
        var tone = simulating ? ModernStatus.On : ModernStatus.Off;
        if (!Equals(modeSegment.Tag, tone))
        {
            modeSegment.Tag = tone;
            modeSegment.Invalidate();
        }

        int rung = Math.Clamp(diagram.SelectedRung, 0, Math.Max(0, project.Rungs.Count - 1));
        SetSegment(positionSegment, "Linha " + (rung + 1) + " · C" + (diagram.SelectedColumn + 1)
            + (diagram.SelectedLane > 0 ? " · paralelo" : string.Empty));
        SetSegment(sizeSegment, project.Rungs.Count == 1 ? "1 linha" : project.Rungs.Count + " linhas");
        zoomSegment.Visible = diagramView;
        SetSegment(zoomSegment, "Zoom " + diagram.ZoomPercent + "%");
    }

    private static void SetSegment(ToolStripStatusLabel segment, string text)
    {
        if (segment.Text != text) segment.Text = text;
    }
}
