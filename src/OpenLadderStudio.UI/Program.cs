using OpenLadderStudio.Application;
using OpenLadderStudio.Core;
using OpenLadderStudio.Drivers.Modbus;
using OpenLadderStudio.Drivers.TP02;

namespace OpenLadderStudio.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var catalog = new PlcDriverCatalog(new IPlcDriver[]
        {
            new Tp02Driver(),
            new ModbusDriver()
        });
        System.Windows.Forms.Application.Run(new MainForm(catalog));
    }
}
