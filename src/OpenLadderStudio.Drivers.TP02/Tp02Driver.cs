using OpenLadderStudio.Core;

namespace OpenLadderStudio.Drivers.TP02;

public sealed class Tp02Driver : IPlcDriver
{
    public string Id => "weg.tp02.pg";
    public string DisplayName => "WEG TP02 — protocolo PG";

    public PlcDriverCapabilities Capabilities { get; } = new(
        Connect: true,
        Monitor: true,
        ReadProgram: true,
        WriteProgram: true,
        Run: true,
        Stop: false);

    public bool Supports(PlcDeviceIdentity device) =>
        device.ConnectionKind == PlcConnectionKind.Serial &&
        device.Manufacturer.Equals("WEG", StringComparison.OrdinalIgnoreCase) &&
        device.Family.Equals("TP02", StringComparison.OrdinalIgnoreCase);

    public byte[] BuildStaticRegisterRead(Tp02Pc12StaticReadFrames.Area area, int startAddress) =>
        Tp02Pc12StaticReadFrames.Build(area, startAddress);
}
