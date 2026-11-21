using OpenLadderStudio.Drivers.TP02;

namespace OpenLadderStudio.UI;

/// <summary>
/// PLC físico na trilha moderna. Por enquanto só leitura do programa do TP02; transferir e
/// RUN entram depois de validados em bancada.
/// </summary>
public sealed partial class MainForm
{
    private ToolStripMenuItem BuildPlcMenu()
    {
        var plc = new ToolStripMenuItem("P&LC");
        var readItem = new ToolStripMenuItem("&Ler programa do TP02...", null, (_, _) => ReadFromTp02());
        plc.DropDownOpening += (_, _) => readItem.Enabled = Tp02() != null;
        plc.DropDownItems.Add(readItem);
        return plc;
    }

    private Tp02Driver? Tp02() => catalog.Drivers.OfType<Tp02Driver>().FirstOrDefault();

    private void ReadFromTp02()
    {
        Tp02Driver? driver = Tp02();
        if (driver == null) return;
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;
        using var dialog = new Tp02ReadDialog(driver);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) return;
        ApplyIlImport(tp02Offline.ImportIl(dialog.Result.ToIl(), "TP02 em " + dialog.PortName));
    }
}
