using System.IO.Ports;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Drivers.TP02;

/// <summary>
/// Porta serial do cabo TP-232PG com a mesma configuração do leitor legado validado em bancada:
/// 19200 bauds, 8 bits, paridade ímpar, 1 bit de parada, sem handshake, DTR e RTS desligados.
/// </summary>
public sealed class Tp02SerialLink : ITp02PgLink
{
    private readonly SerialPort port;

    public Tp02SerialLink(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName)) throw new ArgumentException("Informe a porta COM.", nameof(portName));
        port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 100,
            WriteTimeout = 1500
        };
    }

    public static string[] PortNames()
    {
        string[] names = SerialPort.GetPortNames();
        Array.Sort(names, StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public void Open()
    {
        port.Open();
        port.DiscardInBuffer();
        port.DiscardOutBuffer();
    }

    public void DiscardInput() => port.DiscardInBuffer();

    public void Write(byte[] frame) => port.Write(frame, 0, frame.Length);

    public byte[] ReadBurst(int timeoutMs, int quietMs)
    {
        var bytes = new List<byte>();
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        DateTime last = DateTime.MinValue;
        while (DateTime.UtcNow < deadline)
        {
            int available = port.BytesToRead;
            if (available > 0)
            {
                var buffer = new byte[available];
                int got = port.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < got; i++) bytes.Add(buffer[i]);
                last = DateTime.UtcNow;
            }
            else if (bytes.Count > 0 && (DateTime.UtcNow - last).TotalMilliseconds >= quietMs)
            {
                break;
            }
            Thread.Sleep(15);
        }
        return bytes.ToArray();
    }

    public void Dispose()
    {
        try { if (port.IsOpen) port.Close(); } catch (IOException) { }
        port.Dispose();
    }
}
