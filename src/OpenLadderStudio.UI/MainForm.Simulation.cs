using System.Diagnostics;
using OpenLadderStudio.Application;

namespace OpenLadderStudio.UI;

/// <summary>
/// Modo Simular do editor moderno: roda o programa no PLC virtual (LadderSimulationSession,
/// mesmo motor do simulador legado). Nada é transmitido ao PLC; a edição fica bloqueada
/// enquanto a simulação está ativa para o que está na tela ser o que está rodando.
/// </summary>
public sealed partial class MainForm
{
    private const int SimulationTickMs = 30;

    private readonly LadderSimulationSession simulation = new();
    private readonly System.Windows.Forms.Timer simulationTimer = new() { Interval = SimulationTickMs };
    private readonly Stopwatch simulationClock = new();
    private readonly ListView simulationList = new();
    private readonly Splitter simulationSplitter = new() { Dock = DockStyle.Right, Visible = false };
    private readonly ToolStripMenuItem simulateItem = new("&Simular", null, null, Keys.F5);
    private bool simulating;
    private bool[] poweredRungs = Array.Empty<bool>();

    private void BuildSimulationPanel()
    {
        simulateItem.Click += (_, _) => ToggleSimulation();

        simulationList.Dock = DockStyle.Right;
        simulationList.Width = 320;
        simulationList.View = View.Details;
        simulationList.FullRowSelect = true;
        simulationList.MultiSelect = false;
        simulationList.HideSelection = false;
        simulationList.Visible = false;
        simulationList.Columns.Add("Endereço");
        simulationList.Columns.Add("Tipo");
        simulationList.Columns.Add("Estado");
        simulationList.Columns.Add("Forçado");
        // Larguras de coluna do ListView não acompanham a escala de DPI: são recalculadas
        // como fração da largura do painel, que essa sim é escalada.
        simulationList.ClientSizeChanged += (_, _) => FitSimulationColumns();
        simulationList.DoubleClick += (_, _) => ToggleSelectedInput();
        simulationList.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Space or Keys.Enter) { ToggleSelectedInput(); e.Handled = true; }
        };

        var menu = new ContextMenuStrip();
        var toggle = new ToolStripMenuItem("&Acionar / soltar entrada", null, (_, _) => ToggleSelectedInput());
        var forceOn = new ToolStripMenuItem("Forçar &ligado", null, (_, _) => ForceSelected(true));
        var forceOff = new ToolStripMenuItem("Forçar &desligado", null, (_, _) => ForceSelected(false));
        var release = new ToolStripMenuItem("&Liberar forçamento", null, (_, _) => ReleaseSelected());
        menu.Items.AddRange(new ToolStripItem[] { toggle, new ToolStripSeparator(), forceOn, forceOff, release });
        menu.Opening += (_, e) =>
        {
            LadderSimulationPoint? point = SelectedPoint();
            if (point is null || !simulating) { e.Cancel = true; return; }
            toggle.Enabled = point.Area == LadderSimulationArea.Input;
            release.Enabled = simulation.IsForced(point.Address);
        };
        simulationList.ContextMenuStrip = menu;

        simulationTimer.Tick += (_, _) => SimulationTick();
    }

    private void FitSimulationColumns()
    {
        float[] share = { 0.26f, 0.24f, 0.32f, 0.18f };
        int width = simulationList.ClientSize.Width;
        for (int i = 0; i < share.Length && i < simulationList.Columns.Count; i++)
            simulationList.Columns[i].Width = (int)(width * share[i]);
    }

    private void ToggleSimulation()
    {
        if (simulating) StopSimulation(); else StartSimulation();
    }

    private void StartSimulation()
    {
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;

        if (!simulation.Load(project))
        {
            status.Text = "Não foi possível simular: " + string.Join(" ", simulation.Diagnostics);
            return;
        }

        simulating = true;
        simulateItem.Checked = true;
        grid.ReadOnly = true;
        // O painel lateral tira largura da grade; o comentário não é usado ao simular e
        // cede o espaço às colunas do programa (senão "TMR V0001 1000" sai cortado).
        grid.Columns[CommentColumn].Visible = false;
        poweredRungs = new bool[project.Rungs.Count];

        simulationList.BeginUpdate();
        simulationList.Items.Clear();
        foreach (LadderSimulationPoint point in simulation.Points)
        {
            var item = new ListViewItem(new[] { point.Address, point.AreaText, string.Empty, string.Empty }) { Tag = point };
            simulationList.Items.Add(item);
        }
        simulationList.EndUpdate();
        simulationList.Visible = true;
        simulationSplitter.Visible = true;

        simulationClock.Restart();
        simulationTimer.Start();
        RefreshSimulation();
    }

    private void StopSimulation()
    {
        if (!simulating) return;
        simulationTimer.Stop();
        simulationClock.Reset();
        simulating = false;
        simulateItem.Checked = false;
        grid.ReadOnly = false;
        grid.Columns[CommentColumn].Visible = true;
        simulationList.Visible = false;
        simulationSplitter.Visible = false;
        for (int rung = 0; rung < poweredRungs.Length; rung++) PaintRung(rung, false);
        poweredRungs = Array.Empty<bool>();
        status.Text = "Simulação encerrada.";
    }

    private void ResetSimulation()
    {
        if (!simulating) { status.Text = "A simulação não está ativa. F5 inicia."; return; }
        simulation.Reset();
        simulationClock.Restart();
        RefreshSimulation();
    }

    private void SimulationTick()
    {
        double elapsed = simulationClock.Elapsed.TotalMilliseconds;
        simulationClock.Restart();
        simulation.Advance(elapsed);
        RefreshSimulation();
    }

    private void RefreshSimulation()
    {
        if (!simulating) return;

        foreach (ListViewItem item in simulationList.Items)
        {
            var point = (LadderSimulationPoint)item.Tag!;
            string state = simulation.GetBit(point.Address) ? "LIGADO" : "desligado";
            if (point.Area == LadderSimulationArea.TimerCounter) state += " (" + simulation.GetValue(point.Address) + ")";
            string forced = simulation.IsForced(point.Address) ? "sim" : string.Empty;
            if (item.SubItems[2].Text != state) item.SubItems[2].Text = state;
            if (item.SubItems[3].Text != forced) item.SubItems[3].Text = forced;
        }

        for (int rung = 0; rung < poweredRungs.Length; rung++)
        {
            bool powered = simulation.IsRungPowered(rung);
            if (powered == poweredRungs[rung]) continue;
            poweredRungs[rung] = powered;
            PaintRung(rung, powered);
        }

        status.Text = "Simulando (nada é transmitido ao PLC): " + simulation.ScanCount + " varredura(s), "
            + (simulation.ElapsedMs / 1000.0).ToString("0.0") + " s"
            + (simulation.ForcedCount > 0 ? ", " + simulation.ForcedCount + " forçado(s)" : string.Empty)
            + ". F5 encerra.";
    }

    // Linha energizada: as duas linhas da grade (série e paralelo) com a cor de destaque do sistema.
    private void PaintRung(int rung, bool powered)
    {
        for (int row = rung * 2; row <= rung * 2 + 1 && row < grid.Rows.Count; row++)
        {
            DataGridViewCellStyle style = grid.Rows[row].DefaultCellStyle;
            style.BackColor = powered ? SystemColors.Info : Color.Empty;
            style.ForeColor = powered ? SystemColors.InfoText : Color.Empty;
        }
        if (rung < project.Rungs.Count && rung * 2 < grid.Rows.Count)
            grid.Rows[rung * 2].Cells[0].Value = (rung + 1).ToString("000") + (powered ? " ▶" : string.Empty);
    }

    private LadderSimulationPoint? SelectedPoint() =>
        simulationList.SelectedItems.Count == 1 ? simulationList.SelectedItems[0].Tag as LadderSimulationPoint : null;

    private void ToggleSelectedInput()
    {
        LadderSimulationPoint? point = SelectedPoint();
        if (!simulating || point is null) return;
        if (point.Area != LadderSimulationArea.Input)
        {
            status.Text = point.Address + " não é entrada: use Forçar no menu de contexto.";
            return;
        }
        simulation.SetInput(point.Address, !simulation.GetInput(point.Address));
        simulation.Step();
        RefreshSimulation();
    }

    private void ForceSelected(bool value)
    {
        LadderSimulationPoint? point = SelectedPoint();
        if (!simulating || point is null) return;
        simulation.Force(point.Address, value);
        simulation.Step();
        RefreshSimulation();
    }

    private void ReleaseSelected()
    {
        LadderSimulationPoint? point = SelectedPoint();
        if (!simulating || point is null) return;
        simulation.Release(point.Address);
        simulation.Step();
        RefreshSimulation();
    }

    private bool EditingBlocked()
    {
        if (!simulating) return false;
        status.Text = "Edição bloqueada durante a simulação. F5 encerra a simulação.";
        return true;
    }
}
