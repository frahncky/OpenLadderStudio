using OpenLadderStudio.Core;

namespace OpenLadderStudio.Application;

public sealed class PlcDriverCatalog
{
    private readonly IReadOnlyList<IPlcDriver> drivers;

    public PlcDriverCatalog(IEnumerable<IPlcDriver> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        this.drivers = drivers.ToArray();
    }

    public IReadOnlyList<IPlcDriver> Drivers => drivers;

    public IPlcDriver? FindDriver(PlcDeviceIdentity device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return drivers.FirstOrDefault(driver => driver.Supports(device));
    }
}
