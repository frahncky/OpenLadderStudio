using OpenLadderStudio.Core;

namespace OpenLadderStudio.Drivers.Modbus;

public sealed class ModbusDriver : IPlcDriver
{
    public string Id => "generic.modbus";
    public string DisplayName => "Modbus genérico";

    public PlcDriverCapabilities Capabilities { get; } = new(
        Connect: true,
        Monitor: true,
        ReadProgram: false,
        WriteProgram: false,
        Run: false,
        Stop: false);

    public bool Supports(PlcDeviceIdentity device) =>
        device.ConnectionKind is PlcConnectionKind.Serial or PlcConnectionKind.Tcp;
}
