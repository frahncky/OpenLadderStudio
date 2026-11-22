using OpenLadderStudio.Application;

namespace OpenLadderStudio.UI;

/// <summary>
/// Painel de propriedades à direita do diagrama: edita endereço, parâmetro e retentivo do
/// elemento selecionado e o comentário da linha, pelos mesmos caminhos de edição do diagrama
/// (desfazer, marcação de alteração, sincronia com a grade). Some durante a simulação, quando
/// o lado direito mostra os endereços.
/// </summary>
public sealed partial class MainForm
{
    private readonly LadderPropertiesPanel properties = new() { Dock = DockStyle.Right, Width = 280 };
    private readonly Splitter propertiesSplitter = new() { Dock = DockStyle.Right };

    private void BuildProperties()
    {
        diagram.SelectionChanged += (_, _) => RefreshProperties();
        properties.CellChangeRequested += (_, cell) =>
        {
            if (EditingBlocked()) return;
            var position = new LadderCellPosition(diagram.SelectedRung, diagram.SelectedColumn, diagram.SelectedLane);
            ApplyCell(position, cell, "Elemento atualizado.");
            RefreshProperties();
        };
        properties.CommentChangeRequested += (_, text) => CommentRung(properties.Rung, text);
    }

    private void RefreshProperties()
    {
        if (project.Rungs.Count == 0) return;
        int rung = Math.Clamp(diagram.SelectedRung, 0, project.Rungs.Count - 1);
        int column = Math.Clamp(diagram.SelectedColumn, 0, LadderRungModel.ColumnCount - 1);
        int lane = diagram.SelectedLane > 0 ? 1 : 0;
        LadderCell cell = LadderEditCommands.CellAt(project, new LadderCellPosition(rung, column, lane));
        properties.Show(cell, rung, column, lane, project.Rungs[rung].Comment);
    }

    /// <summary>O painel aparece com o diagrama e fora da simulação.</summary>
    private void SyncPropertiesVisibility()
    {
        bool show = diagramView && !simulating && showPropertiesPanel && !focusMode;
        properties.Visible = show;
        propertiesSplitter.Visible = show;
    }
}
