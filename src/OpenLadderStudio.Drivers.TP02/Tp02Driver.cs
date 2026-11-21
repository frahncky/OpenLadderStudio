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

    /// <summary>
    /// Lê o programa do PLC pela porta indicada. Só transmite HELLO, F0, 38 e 34; não muda o
    /// modo do PLC nem escreve nada nele.
    /// </summary>
    public Tp02PgReadResult ReadProgram(string portName, Action<string>? log = null, CancellationToken cancellation = default) =>
        new Tp02PgProgramReader(() => new Tp02SerialLink(portName), log).Read(cancellation);

    public byte[] BuildStaticRegisterRead(Tp02Pc12StaticReadFrames.Area area, int startAddress) =>
        Tp02Pc12StaticReadFrames.Build(area, startAddress);
}
