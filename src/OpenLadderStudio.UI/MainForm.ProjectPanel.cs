using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Painel de projeto à esquerda: as linhas do programa (com o comentário) e a referência
/// cruzada dos endereços. Clicar num nó leva a seleção até a linha ou a célula, no diagrama
/// ou na grade. É também aqui que ficam o modo foco (F11) e a volta ao layout padrão.
/// </summary>
public sealed partial class MainForm
{
    private const int ProjectPanelWidth = 230;
    private readonly TreeView projectTree = new() { Dock = DockStyle.Left, Width = ProjectPanelWidth, HideSelection = false, ShowLines = false, FullRowSelect = true, BorderStyle = BorderStyle.None, Indent = 16 };
    private readonly Splitter projectSplitter = new() { Dock = DockStyle.Left };
    private readonly ToolStripMenuItem projectPanelItem = new("Painel de &projeto");
    private readonly ToolStripMenuItem propertiesPanelItem = new("Painel de p&ropriedades");
    private readonly ToolStripMenuItem focusItem = new("&Modo foco", null, null, Keys.F11);
    private bool showProjectPanel = true;
    private bool showPropertiesPanel = true;
    private bool focusMode;
    private string projectTreeState = string.Empty;

    private void BuildProjectPanel()
    {
        projectTree.NodeMouseClick += (_, e) => NavigateTo(e.Node);
        projectTree.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && projectTree.SelectedNode is { } node) { NavigateTo(node); e.SuppressKeyPress = true; }
        };
        projectPanelItem.Click += (_, _) => { showProjectPanel = !showProjectPanel; focusMode = false; SyncPanels(); };
        propertiesPanelItem.Click += (_, _) => { showPropertiesPanel = !showPropertiesPanel; focusMode = false; SyncPanels(); };
        focusItem.Click += (_, _) => { focusMode = !focusMode; SyncPanels(); status.Text = focusMode ? "Modo foco: F11 devolve os painéis." : "Painéis restaurados."; };
    }

    private void AddPanelMenuItems(ToolStripMenuItem view)
    {
        view.DropDownItems.Add(projectPanelItem);
        view.DropDownItems.Add(propertiesPanelItem);
        view.DropDownItems.Add(focusItem);
        view.DropDownItems.Add(new ToolStripMenuItem("Restaurar &layout padrão", null, (_, _) => ResetLayout()));
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownOpening += (_, _) =>
        {
            projectPanelItem.Checked = projectTree.Visible;
            propertiesPanelItem.Checked = properties.Visible;
            focusItem.Checked = focusMode;
        };
    }

    private void ResetLayout()
    {
        showProjectPanel = showPropertiesPanel = true;
        focusMode = false;
        projectTree.Width = LogicalToDeviceUnits(ProjectPanelWidth);
        properties.Width = LogicalToDeviceUnits(280);
        diagram.ZoomPercent = 100;
        SetDiagramView(true);
        SyncPanels();
        status.Text = "Layout padrão restaurado.";
    }

    /// <summary>Painéis laterais: escolha do usuário, modo foco, vista e simulação.</summary>
    private void SyncPanels()
    {
        // A grade de texto precisa da largura toda para as oito colunas.
        bool project = showProjectPanel && !focusMode && diagramView;
        projectTree.Visible = projectSplitter.Visible = project;
        SyncPropertiesVisibility();
    }

    /// <summary>Refaz a árvore quando o programa mudou; mantém os nós abertos.</summary>
    private void RefreshProjectTree()
    {
        IReadOnlyList<LadderAddressUse> uses = LadderCrossReference.Build(project);
        string state = string.Join("\n", project.Rungs.Select(r => r.Comment)) + "\n#" + project.Rungs.Count + "\n"
            + string.Join("\n", uses.Select(u => u.Address + "@" + u.Where));
        if (state == projectTreeState) return;
        projectTreeState = state;

        bool programOpen = projectTree.Nodes.Count == 0 || projectTree.Nodes[0].IsExpanded;
        bool addressesOpen = projectTree.Nodes.Count > 1 && projectTree.Nodes[1].IsExpanded;
        var openAddresses = projectTree.Nodes.Count > 1
            ? projectTree.Nodes[1].Nodes.Cast<TreeNode>().Where(n => n.IsExpanded).Select(n => (string)n.Name).ToHashSet()
            : new HashSet<string>();

        projectTree.BeginUpdate();
        projectTree.Nodes.Clear();
        var program = new TreeNode("Main (PRG) · " + project.Rungs.Count + (project.Rungs.Count == 1 ? " linha" : " linhas"));
        for (int rung = 0; rung < project.Rungs.Count; rung++)
        {
            string comment = project.Rungs[rung].Comment;
            program.Nodes.Add(new TreeNode("Linha " + (rung + 1) + (comment.Length > 0 ? " · " + comment : string.Empty))
            {
                Tag = new LadderCellPosition(rung, 0, 0),
                ToolTipText = comment
            });
        }
        projectTree.Nodes.Add(program);

        var groups = uses.GroupBy(u => u.Address).ToList();
        var addresses = new TreeNode("Endereços · " + groups.Count);
        foreach (var group in groups)
        {
            var node = new TreeNode(group.Key + "  (" + group.Count() + ")") { Name = group.Key, Tag = group.First().Position };
            foreach (LadderAddressUse use in group)
                node.Nodes.Add(new TreeNode(use.Where) { Tag = use.Position });
            if (openAddresses.Contains(group.Key)) node.Expand();
            addresses.Nodes.Add(node);
        }
        projectTree.Nodes.Add(addresses);
        if (programOpen) program.Expand();
        if (addressesOpen) addresses.Expand();
        projectTree.EndUpdate();
    }

    private void NavigateTo(TreeNode node)
    {
        if (node.Tag is not LadderCellPosition position || position.Rung >= project.Rungs.Count) return;
        if (diagramView)
        {
            diagram.Select(position.Rung, position.Column, position.Lane);
            diagram.Focus();
        }
        else if (grid.Rows.Count > 0)
        {
            int row = Math.Min(position.Rung * 2 + position.Lane, grid.Rows.Count - 1);
            grid.CurrentCell = grid.Rows[row].Cells[position.Column + 1];
            grid.Focus();
        }
        status.Text = "Linha " + (position.Rung + 1) + ", coluna C" + (position.Column + 1) + ".";
    }
}
