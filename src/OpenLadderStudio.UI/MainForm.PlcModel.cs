using OpenLadderStudio.Application;

namespace OpenLadderStudio.UI;

/// <summary>
/// Controlador alvo: menu PLC → Modelo do PLC... e o segmento "Alvo" da barra de status
/// (clicável). A escolha fica no mesmo arquivo do editor legado, então os dois abrem no
/// mesmo modelo.
/// </summary>
public sealed partial class MainForm
{
    /// <summary>Onde a escolha é guardada (substituível no teste da janela).</summary>
    private PlcProfileStore plcStore = new();
    private PlcDeviceProfile plcProfile = PlcProfileCatalog.Default;
    private PlcMemoryMapStore memoryMaps = new();

    /// <summary>Abre o diálogo e devolve o modelo escolhido (substituível no teste da janela).</summary>
    private Func<PlcDeviceProfile, PlcDeviceProfile?> plcModelPrompt = null!;

    private void BuildPlcModel()
    {
        plcModelPrompt = current =>
        {
            using var dialog = new PlcModelDialog(catalog, current);
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.Selected : null;
        };
        plcProfile = plcStore.Load();
        targetSegment.Click += (_, _) => ChoosePlcModel();
        ShowPlcTarget();
    }

    private ToolStripMenuItem BuildPlcMenu()
    {
        var plc = new ToolStripMenuItem("P&LC");
        plc.DropDownItems.Add(new ToolStripMenuItem("&Modelo do PLC...", null, (_, _) => ChoosePlcModel()));
        plc.DropDownItems.Add(new ToolStripSeparator());
        plc.DropDownItems.Add(new ToolStripMenuItem("Mapa de &memória...", null, (_, _) => EditMemoryMap()));
        return plc;
    }

    private void ChoosePlcModel()
    {
        PlcDeviceProfile? chosen = plcModelPrompt(plcProfile);
        if (chosen is null || chosen.Id == plcProfile.Id) return;
        plcProfile = chosen;
        try
        {
            plcStore.Save(chosen);
            status.Text = "Alvo: " + chosen.DisplayName + ".";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = "Alvo: " + chosen.DisplayName + " (não foi possível lembrar a escolha: " + ex.Message + ")";
        }
        ShowPlcTarget();
    }

    private void EditMemoryMap()
    {
        using var dialog = new PlcMemoryMapDialog(memoryMaps, plcProfile);
        if (dialog.ShowDialog(this) == DialogResult.OK) status.Text = "Mapa de memória de " + plcProfile.DisplayName + " salvo.";
    }

    private void ShowPlcTarget()
    {
        targetSegment.Text = "Alvo: " + plcProfile.DisplayName;
        targetSegment.ToolTipText = "Controlador alvo (" + plcProfile.SupportText.ToLowerInvariant() + "). Clique para trocar.";
    }
}
