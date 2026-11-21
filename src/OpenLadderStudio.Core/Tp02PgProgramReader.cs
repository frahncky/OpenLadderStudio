using System.Globalization;
using System.Text;

namespace OpenLadderStudio.Core;

/// <summary>
/// Enlace serial com o TP02, sem política de protocolo. A implementação real (SerialPort a
/// 19200 8O1, DTR e RTS desligados) fica no driver; o autoteste usa um PLC falso.
/// </summary>
public interface ITp02PgLink : IDisposable
{
    void Open();
    void DiscardInput();
    void Write(byte[] frame);

    /// <summary>Lê até o prazo ou até ficar <paramref name="quietMs"/> sem bytes novos depois do primeiro.</summary>
    byte[] ReadBurst(int timeoutMs, int quietMs);
}

public sealed record Tp02PgReadStep(int Step, string Word, string Instruction, bool BrawMatches);

public sealed class Tp02PgReadResult
{
    internal Tp02PgReadResult() { }

    /// <summary>"STOP" ou "RUN", conforme a resposta do HELLO.</summary>
    public string PlcState { get; internal set; } = string.Empty;
    public string Frame38 { get; internal set; } = string.Empty;
    public int Pages { get; internal set; }
    public int EndStep { get; internal set; }
    public int BrawMismatches { get; internal set; }
    public int UnknownSteps { get; internal set; }
    public List<Tp02PgReadStep> Steps { get; } = new();

    /// <summary>IL no formato "NNNN: OPERAÇÃO OPERANDO", aceito pela importação de IL verificada.</summary>
    public string ToIl()
    {
        var text = new StringBuilder();
        text.AppendLine("; Lido do TP02 pelo protocolo PG (HELLO, F0, 38, 34). PLC no HELLO: " + PlcState + ".");
        foreach (Tp02PgReadStep step in Steps)
            text.AppendLine(step.Step.ToString("0000", CultureInfo.InvariantCulture) + ": "
                // O decodificador mostra o fim como "F-00 END"; a IL verificada o escreve "END".
                + (step.Instruction == "F-00 END" ? "END" : step.Instruction));
        return text.ToString();
    }
}

/// <summary>
/// Leitura do programa do TP02 pelo protocolo PG, na mesma sequência, nos mesmos tempos e
/// com as mesmas tentativas do leitor validado em bancada (TP02ProgramReader.cs, v1.47):
/// aquecimento do enlace, até três sessões, HELLO, F0, 38 e páginas 34 de 80 passos até o
/// F-00 END. Só transmite quadros de leitura: qualquer outro quadro é recusado antes de sair.
/// </summary>
public sealed class Tp02PgProgramReader
{
    internal static readonly byte[] HelloRequest = { 0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D };
    internal static readonly byte[] HelloStop = { 0x80, 0x01, 0x09, 0x75 };
    internal static readonly byte[] HelloRun = { 0xC0, 0x01, 0x09, 0x35 };
    internal static readonly byte[] F0Request = { 0xF0, 0x00, 0x0F };
    internal static readonly byte[] F0Response = { 0x00, 0x02, 0x10, 0x22, 0xCB };
    internal static readonly byte[] Frame38Request = { 0x38, 0x00, 0xC7 };

    private readonly Func<ITp02PgLink> openLink;
    private readonly Action<string> log;
    private readonly Action<int> delay;

    /// <param name="delay">Pausa entre quadros; o padrão é Thread.Sleep. O autoteste passa uma pausa nula.</param>
    public Tp02PgProgramReader(Func<ITp02PgLink> openLink, Action<string> log = null, Action<int> delay = null)
    {
        this.openLink = openLink ?? throw new ArgumentNullException(nameof(openLink));
        this.log = log ?? (_ => { });
        this.delay = delay ?? Thread.Sleep;
    }

    /// <summary>Verdadeiro só para HELLO, F0, 38 e pedidos 34 de página válida.</summary>
    public static bool IsReadOnlyFrame(byte[] frame)
    {
        if (frame == null) return false;
        if (frame.AsSpan().SequenceEqual(HelloRequest) || frame.AsSpan().SequenceEqual(F0Request)
            || frame.AsSpan().SequenceEqual(Frame38Request)) return true;
        if (frame.Length != 6 || frame[0] != 0x34) return false;
        try
        {
            int start = Tp02Pg34Pager.DecodeStartStep(frame);
            return start % Tp02Pg34Pager.StepsPerPage == 0
                && frame.AsSpan().SequenceEqual(Tp02Pg34Pager.BuildReadRequest(start));
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public Tp02PgReadResult Read(CancellationToken cancellation = default)
    {
        WarmUp(cancellation);
        Exception last = null;
        for (int session = 1; session <= 3; session++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (session > 1)
            {
                log("Nova tentativa: sessão " + session + ".");
                Pause(session == 2 ? 1500 : 2000);
            }
            try { return ReadSession(session, cancellation); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or TimeoutException or UnauthorizedAccessException)
            {
                last = ex;
                log("Sessão " + session + " falhou: " + ex.Message);
            }
        }
        throw new IOException("Três sessões PG falharam. Último erro: " + (last == null ? "desconhecido" : last.Message), last);
    }

    private void WarmUp(CancellationToken cancellation)
    {
        log("Aquecimento: abrindo a porta para estabilizar o cabo TP-232PG.");
        ITp02PgLink link = null;
        try
        {
            link = openLink();
            link.Open();
            Pause(1400);
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                byte[] raw = Exchange(link, HelloRequest, 2200, 230, "Aquecimento HELLO " + attempt);
                if (Contains(raw, HelloStop) || Contains(raw, HelloRun))
                {
                    log("Aquecimento confirmado; a leitura recomeça em sessão limpa.");
                    break;
                }
                Pause(300);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or InvalidOperationException)
        {
            log("O aquecimento não confirmou o enlace: " + ex.Message + ". A leitura continua.");
        }
        finally
        {
            link?.Dispose();
        }
        Pause(1100);
    }

    private Tp02PgReadResult ReadSession(int session, CancellationToken cancellation)
    {
        var result = new Tp02PgReadResult();
        using ITp02PgLink link = openLink();
        link.Open();
        int settle = session == 1 ? 1600 : session == 2 ? 1900 : 2300;
        Pause(settle);
        log("Porta aberta, sessão " + session + ", estabilização de " + settle + " ms.");

        result.PlcState = Hello(link, cancellation);
        log("HELLO confirmado: PLC em " + result.PlcState + ".");
        Pause(450);

        StatusF0(link, cancellation);
        Pause(420);

        byte[] frame38 = ExchangeFrame(link, Frame38Request, 0x02, 4, 3600, "38", cancellation);
        result.Frame38 = Hex(frame38);
        Pause(450);

        for (int start = 0; start < Tp02Pg34Pager.MaxProgramSteps; start += Tp02Pg34Pager.StepsPerPage)
        {
            cancellation.ThrowIfCancellationRequested();
            byte[] page = ExchangeFrame(link, Tp02Pg34Pager.BuildReadRequest(start), Tp02Pg34Pager.PayloadLength,
                4, 5500, "34@" + start.ToString("0000", CultureInfo.InvariantCulture), cancellation);
            result.Pages++;

            int active = ActiveSteps(page);
            bool hasEnd = Tp02Pg34Pager.TryFindEnd(page, out int localEnd);
            int count = hasEnd ? localEnd + 1 : active;
            log("Página 34 em " + start.ToString("0000", CultureInfo.InvariantCulture) + ": " + active + " passo(s) ativo(s)"
                + (hasEnd ? ", END no passo local " + localEnd + "." : ", sem END."));

            for (int i = 0; i < count; i++)
            {
                byte high = page[2 + 2 * i], low = page[3 + 2 * i], braw = page[2 + Tp02Pg34Pager.PlaneASize + i];
                Tp02Pg34Step step = Tp02Pg34Decoder.DecodeStep(start + i, high, low, braw);
                bool brawOk = Tp02Pg34Decoder.CalculateBraw(high, low) == braw;
                if (!brawOk) result.BrawMismatches++;
                string il = step.ToIl();
                if (il.StartsWith("UNKNOWN", StringComparison.Ordinal)) result.UnknownSteps++;
                result.Steps.Add(new Tp02PgReadStep(start + i, Hex(new[] { high, low, braw }).Replace(" ", ""), il, brawOk));
            }

            if (hasEnd)
            {
                result.EndStep = start + localEnd;
                log("Leitura concluída: " + result.Steps.Count + " passo(s), END em "
                    + result.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");
                return result;
            }
            if (active < Tp02Pg34Pager.StepsPerPage)
                throw new InvalidDataException("Página 34 parcial sem F-00 END.");
            Pause(450);
        }
        throw new InvalidDataException("F-00 END não encontrado antes do limite de 4000 passos.");
    }

    private string Hello(ITp02PgLink link, CancellationToken cancellation)
    {
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            byte[] raw = Exchange(link, HelloRequest, attempt == 1 ? 2600 : 3000, 240, "HELLO " + attempt);
            if (Contains(raw, HelloStop)) return "STOP";
            if (Contains(raw, HelloRun)) return "RUN";
            Pause(350);
        }
        throw new TimeoutException("O PLC não respondeu ao HELLO em 6 tentativas.");
    }

    private void StatusF0(ITp02PgLink link, CancellationToken cancellation)
    {
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Contains(Exchange(link, F0Request, 3600, 250, "F0 " + attempt), F0Response)) return;
            Pause(350);
        }
        throw new InvalidDataException("F0 não retornou 00 02 10 22 CB.");
    }

    private byte[] ExchangeFrame(ITp02PgLink link, byte[] request, int payloadLength, int attempts, int timeoutMs,
        string label, CancellationToken cancellation)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            byte[] frame = FindFrame(Exchange(link, request, timeoutMs, 280, label + " " + attempt), payloadLength);
            if (frame != null) return frame;
            Pause(380);
        }
        throw new InvalidDataException(label + " não retornou quadro válido (LEN="
            + payloadLength.ToString("X2", CultureInfo.InvariantCulture) + ", soma FF).");
    }

    private byte[] Exchange(ITp02PgLink link, byte[] request, int timeoutMs, int quietMs, string label)
    {
        if (!IsReadOnlyFrame(request))
            throw new InvalidOperationException("Quadro fora da lista de leitura recusado antes da transmissão: " + Hex(request));
        link.DiscardInput();
        log(label + " TX: " + Hex(request));
        link.Write(request);
        byte[] raw = link.ReadBurst(timeoutMs, quietMs) ?? Array.Empty<byte>();
        log(label + " RX: " + (raw.Length == 0 ? "[]" : Hex(raw)));
        return raw;
    }

    private void Pause(int milliseconds) => delay(milliseconds);

    internal static byte[] FindFrame(byte[] raw, int payloadLength)
    {
        int total = payloadLength + 3;
        if (raw == null || raw.Length < total) return null;
        for (int i = 0; i <= raw.Length - total; i++)
        {
            if (raw[i + 1] != (byte)payloadLength) continue;
            int sum = 0;
            for (int j = 0; j < total; j++) sum = (sum + raw[i + j]) & 0xFF;
            if (sum != 0xFF) continue;
            return raw.AsSpan(i, total).ToArray();
        }
        return null;
    }

    private static int ActiveSteps(byte[] page)
    {
        for (int i = Tp02Pg34Pager.StepsPerPage - 1; i >= 0; i--)
            if (page[2 + 2 * i] != 0 || page[3 + 2 * i] != 0 || page[2 + Tp02Pg34Pager.PlaneASize + i] != 0) return i + 1;
        return 0;
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack != null && haystack.AsSpan().IndexOf(needle) >= 0;

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace('-', ' ');
}
