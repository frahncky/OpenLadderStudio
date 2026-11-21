namespace OpenLadderStudio.Core;

public enum PlcConnectionKind
{
    Virtual,
    Serial,
    Tcp
}

public sealed record PlcDeviceIdentity(
    string Manufacturer,
    string Family,
    string Model,
    PlcConnectionKind ConnectionKind);

public sealed record PlcDriverCapabilities(
    bool Connect,
    bool Monitor,
    bool ReadProgram,
    bool WriteProgram,
    bool Run,
    bool Stop);

public interface IPlcDriver
{
    string Id { get; }
    string DisplayName { get; }
    PlcDriverCapabilities Capabilities { get; }
    bool Supports(PlcDeviceIdentity device);
}
