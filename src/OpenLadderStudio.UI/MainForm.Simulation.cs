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
    private readonly Panel simulationPanel = new();
    private readonly TextBox plantState = new();
    private readonly ToolStripMenuItem plantMenu = new("&Planta simulada");
    private readonly ToolStripMenuItem faultsMenu = new("&Falhas da planta");
    private readonly ToolStripMenuItem sampleItem = new("Abrir &exemplo da planta...");
    private string? selectedPlantId;
    private readonly Splitter simulationSplitter = new() { Dock = DockStyle.Right, Visible = false };
    private readonly ToolStripMenuItem simulateItem = new("&Simular", null, null, Keys.F5);
    private bool simulating;
    private bool[] poweredRungs = Array.Empty<bool>();

    private void BuildSimulationPanel()
    {
        simulateItem.Click += (_, _) => ToggleSimulation();

        simulationPanel.Dock = DockStyle.Right;
        simulationPanel.Width = 320;
        simulationPanel.Visible = false;
        plantState.Dock = DockStyle.Bottom;
        plantState.Multiline = true;
        plantState.ReadOnly = true;
        plantState.WordWrap = true;
        plantState.ScrollBars = ScrollBars.Vertical;
        plantState.Visible = false;
        simulationList.Dock = DockStyle.Fill;
        simulationPanel.Controls.Add(simulationList);
        simulationPanel.Controls.Add(plantState);
        BuildPlantMenus();

        simulationList.View = View.Details;
        simulationList.ShowItemToolTips = true;
        simulationList.FullRowSelect = true;
        simulationList.MultiSelect = false;
        simulationList.HideSelection = false;
        simulationList.Columns.Add("Endereço");
        simulationList.Columns.Add("Descrição");
        simulationList.Columns.Add("Estado");
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
            toggle.Enabled = point.Area == LadderSimulationArea.Input && !point.DrivenByPlant;
            release.Enabled = simulation.IsForced(point.Address);
        };
        simulationList.ContextMenuStrip = menu;

        simulationTimer.Tick += (_, _) => SimulationTick();
    }

    private void FitSimulationColumns()
    {
        float[] share = { 0.23f, 0.42f, 0.35f };
        int width = simulationList.ClientSize.Width;
        for (int i = 0; i < share.Length && i < simulationList.Columns.Count; i++)
            simulationList.Columns[i].Width = (int)(width * share[i]);
    }

    private void BuildPlantMenus()
    {
        var none = new ToolStripMenuItem("&Nenhuma (só o programa)") { Tag = null };
        plantMenu.DropDownItems.Add(none);
        plantMenu.DropDownItems.Add(new ToolStripSeparator());
        foreach (LadderPlantInfo plant in LadderSimulationSession.AvailablePlants)
            plantMenu.DropDownItems.Add(new ToolStripMenuItem(plant.Name) { Tag = plant.Id, ToolTipText = plant.Description });
        foreach (ToolStripMenuItem item in plantMenu.DropDownItems.OfType<ToolStripMenuItem>())
            item.Click += (_, _) => SelectPlant((string?)item.Tag);
        plantMenu.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripMenuItem item in plantMenu.DropDownItems.OfType<ToolStripMenuItem>())
                item.Checked = (string?)item.Tag == selectedPlantId;
        };

        // As falhas mudam com a planta: o submenu é montado ao abrir.
        faultsMenu.DropDownItems.Add(new ToolStripMenuItem("(sem planta)") { Enabled = false });
        faultsMenu.DropDownOpening += (_, _) =>
        {
            faultsMenu.DropDownItems.Clear();
            if (!simulating || simulation.Faults.Count == 0)
            {
                faultsMenu.DropDownItems.Add(new ToolStripMenuItem(simulating ? "(planta sem falhas)" : "(inicie a simulação com uma planta)") { Enabled = false });
                return;
            }
            foreach (LadderPlantFault fault in simulation.Faults)
            {
                var item = new ToolStripMenuItem(fault.Name) { Checked = fault.Active, ToolTipText = fault.Description, Tag = fault.Id };
                item.Click += (_, _) => ToggleFault((string)item.Tag!);
                faultsMenu.DropDownItems.Add(item);
            }
        };

        sampleItem.Click += (_, _) => OpenPlantSample();
    }

    private void SelectPlant(string? plantId)
    {
        selectedPlantId = plantId;
        string name = plantId is null ? "nenhuma" : LadderSimulationSession.AvailablePlants.First(p => p.Id == plantId).Name;
        if (!simulating)
        {
            status.Text = "Planta simulada: " + name + ". F5 inicia a simulação.";
            return;
        }
        // Com a simulação ativa a troca vale na hora, partindo do repouso.
        simulation.AttachPlant(plantId);
        simulation.Reset();
        RebuildPointList();
        RefreshSimulation();
    }

    private void ToggleFault(string faultId)
    {
        bool active = simulation.Faults.First(f => f.Id == faultId).Active;
        simulation.SetFault(faultId, !active);
        RefreshSimulation();
    }

    /// <summary>Substitui o projeto pelo programa de exemplo da planta escolhida (ou da padrão).</summary>
    private void OpenPlantSample()
    {
        if (!ConfirmDiscard()) return;
        StopSimulation();
        string plantId = selectedPlantId ?? LadderSimulationSession.DefaultPlantId;
        project = LadderSimulationSession.CreateSampleProject(plantId, out string description);
        selectedPlantId = plantId;
        currentPath = null;
        history.Clear();
        // Exemplo ainda não salvo: o título fica marcado até o usuário salvar.
        savedState = projects.Serialize(projects.CreateNew());
        LoadIntoGrid();
        MarkDirty();
        ClearVerification();
        report.Text = description.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        ShowReport(true);
        status.Text = "Exemplo de " + LadderSimulationSession.AvailablePlants.First(p => p.Id == plantId).Name + " aberto. F5 simula com a planta.";
    }

    private void RebuildPointList()
    {
        simulationList.BeginUpdate();
        simulationList.Items.Clear();
        foreach (LadderSimulationPoint point in simulation.Points)
        {
            string text = point.Name.Length > 0 ? point.Name : point.AreaText;
            if (point.DrivenByPlant) text += " (planta)";
            simulationList.Items.Add(new ListViewItem(new[] { point.Address, text, string.Empty }) { Tag = point, ToolTipText = text + " - " + point.AreaText });
        }
        simulationList.EndUpdate();
        plantState.Visible = simulation.PlantId is not null;
        plantState.Height = plantState.Font.Height * 8;
        for (int rung = 0; rung < poweredRungs.Length; rung++) PaintRung(rung, false);
        poweredRungs = new bool[project.Rungs.Count];
    }

    private void ToggleSimulation()
    {
        if (simulating) StopSimulation(); else StartSimulation();
    }

    private void StartSimulation()
    {
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;

        simulation.AttachPlant(selectedPlantId);
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

        RebuildPointList();
        simulationPanel.Visible = true;
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
        simulationPanel.Visible = false;
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
            // Estado numa coluna só: o painel é estreito para uma coluna "Forçado" a 125% e 175%.
            string state = simulation.GetBit(point.Address) ? "LIGADO" : "desligado";
            if (point.Area == LadderSimulationArea.TimerCounter) state += " · " + simulation.GetValue(point.Address);
            if (simulation.IsForced(point.Address)) state += " · forçado";
            if (item.SubItems[2].Text != state) item.SubItems[2].Text = state;
        }

        for (int rung = 0; rung < poweredRungs.Length; rung++)
        {
            bool powered = simulation.IsRungPowered(rung);
            if (powered == poweredRungs[rung]) continue;
            poweredRungs[rung] = powered;
            PaintRung(rung, powered);
        }

        if (plantState.Visible)
        {
            // Falhas ativas logo abaixo do nome: o resumo da planta é longo e rola para fora da caixa.
            string faults = string.Join(", ", simulation.Faults.Where(f => f.Active).Select(f => f.Name));
            string text = simulation.PlantName
                + (faults.Length > 0 ? Environment.NewLine + "Falhas ativas: " + faults : string.Empty)
                + Environment.NewLine + simulation.PlantState.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            if (plantState.Text != text) plantState.Text = text;
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
        if (point.DrivenByPlant)
        {
            status.Text = point.Address + " é escrito pela planta: use Forçar no menu de contexto.";
            return;
        }
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
