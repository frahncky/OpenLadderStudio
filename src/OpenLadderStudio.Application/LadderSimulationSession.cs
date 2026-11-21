using ModernPC12;

namespace OpenLadderStudio.Application;

public enum LadderSimulationArea
{
    Input,
    Output,
    Auxiliary,
    Special,
    TimerCounter
}

/// <summary>Endereço usado pelo programa, na ordem em que aparece.</summary>
public sealed record LadderSimulationPoint(string Address, LadderSimulationArea Area, string AreaText);

/// <summary>
/// PLC virtual do editor moderno. Usa o mesmo LadderScanEngine do simulador legado
/// (núcleo, sem WinForms) e nunca fala com hardware: entradas vêm do usuário,
/// saídas só existem na imagem de processo em memória.
/// </summary>
public sealed class LadderSimulationSession
{
    /// <summary>Passo de varredura do simulador legado, em milissegundos.</summary>
    public const double ScanStepMs = 10.0;

    /// <summary>Limite de tempo simulado por chamada, para a janela não travar depois de uma pausa longa.</summary>
    public const double MaxAdvanceMs = 1000.0;

    private readonly LadderScanEngine engine = new();
    private readonly List<LadderSimulationPoint> points = new();
    private double pendingMs;

    public bool Loaded { get; private set; }
    public IReadOnlyList<string> Diagnostics => engine.Diagnostics.ToArray();
    public IReadOnlyList<LadderSimulationPoint> Points => points;
    public long ScanCount => engine.ScanCount;
    public double ElapsedMs => engine.TotalMilliseconds;
    public int RungCount => engine.RungCount;
    public int ForcedCount => engine.Forces.Count;

    public bool Load(LadderProjectModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        engine.Forces.ReleaseAll();
        engine.Field.Clear();
        engine.Image.ClearAll();
        pendingMs = 0;
        Loaded = engine.Load(ToUniversal(model));
        CollectPoints();
        return Loaded;
    }

    /// <summary>Volta ao estado de partida: zera lógica, tempo, entradas de campo e forçamentos.</summary>
    public void Reset()
    {
        engine.Reset();
        engine.Forces.ReleaseAll();
        engine.Field.Clear();
        engine.Image.ClearAll();
        pendingMs = 0;
    }

    /// <summary>Executa uma varredura de <see cref="ScanStepMs"/>.</summary>
    public void Step()
    {
        RequireLoaded();
        engine.Execute(ScanStepMs);
    }

    /// <summary>Avança o tempo simulado em passos de <see cref="ScanStepMs"/>; a sobra fica para a próxima chamada.</summary>
    public int Advance(double elapsedMs)
    {
        RequireLoaded();
        pendingMs += Math.Clamp(elapsedMs, 0, MaxAdvanceMs);
        int scans = 0;
        while (pendingMs >= ScanStepMs)
        {
            engine.Execute(ScanStepMs);
            pendingMs -= ScanStepMs;
            scans++;
        }
        return scans;
    }

    /// <summary>Entrada de campo (botoeira ou chave). Só aceita endereços X.</summary>
    public void SetInput(string address, bool value)
    {
        SimBitRef bit = Parse(address);
        if (bit.Area != SimBitArea.Input) throw new ArgumentException("Só entradas X podem ser acionadas no campo: " + address, nameof(address));
        engine.Field.Set(bit, value);
    }

    public bool GetInput(string address) => engine.Field.Get(Parse(address));

    public bool GetBit(string address) => engine.Image.GetBit(Parse(address));

    /// <summary>Valor atual de temporizador ou contador (V).</summary>
    public int GetValue(string address)
    {
        SimBitRef bit = Parse(address);
        if (bit.Area != SimBitArea.Variable) throw new ArgumentException("Valor só existe para V: " + address, nameof(address));
        return engine.Image.GetVariableValue(bit.Index);
    }

    public void Force(string address, bool value) => engine.Forces.Force(Parse(address), value);
    public void Release(string address) => engine.Forces.Release(Parse(address));
    public bool IsForced(string address) => engine.Forces.IsForced(Parse(address));

    public bool IsRungPowered(int rungIndex) =>
        rungIndex >= 0 && rungIndex < engine.Rungs.Count && engine.Rungs[rungIndex].LastPower;

    private void RequireLoaded()
    {
        if (!Loaded) throw new InvalidOperationException("Nenhum programa carregado no simulador.");
    }

    private static SimBitRef Parse(string address)
    {
        if (!SimAddress.TryParseBit(address, out SimBitRef bit))
            throw new ArgumentException("Endereço inválido para o simulador: " + address, nameof(address));
        return bit;
    }

    private void CollectPoints()
    {
        points.Clear();
        var seen = new HashSet<int>();
        foreach (CompiledRung rung in engine.Rungs)
        {
            for (int column = 0; column < LadderScanEngine.ConditionColumns; column++)
            {
                Add(rung.Series[column]?.Resolved == true ? rung.Series[column].Bit : SimBitRef.Invalid, seen);
                Add(rung.Parallel[column]?.Resolved == true ? rung.Parallel[column].Bit : SimBitRef.Invalid, seen);
            }
            if (rung.Output != null) Add(rung.Output.Bit, seen);
        }
        points.Sort((a, b) => a.Area != b.Area ? a.Area.CompareTo(b.Area) : string.CompareOrdinal(a.Address, b.Address));
    }

    private void Add(SimBitRef bit, HashSet<int> seen)
    {
        if (!bit.Valid || !seen.Add(bit.Key)) return;
        LadderSimulationArea area = bit.Area switch
        {
            SimBitArea.Input => LadderSimulationArea.Input,
            SimBitArea.Output => LadderSimulationArea.Output,
            SimBitArea.Auxiliary => LadderSimulationArea.Auxiliary,
            SimBitArea.Special => LadderSimulationArea.Special,
            _ => LadderSimulationArea.TimerCounter
        };
        points.Add(new LadderSimulationPoint(SimAddress.Format(bit), area, SimAddress.AreaText(bit.Area)));
    }

    /// <summary>Mesma conversão do UniversalLadderAdapter legado, a partir do modelo do editor moderno.</summary>
    internal static UniversalLadderProgram ToUniversal(LadderProjectModel model)
    {
        var program = new UniversalLadderProgram { Name = "Projeto" };
        foreach (LadderRungModel source in model.Rungs)
        {
            var rung = new UniversalLadderRung();
            for (int column = 0; column < LadderRungModel.ColumnCount; column++)
            {
                if (Convert(source.Series[column], column) is UniversalLadderElement series) rung.Series.Add(series);
                if (Convert(source.Parallel[column], column) is UniversalLadderElement parallel) rung.Parallel.Add(parallel);
            }
            program.Rungs.Add(rung);
        }
        return program;
    }

    private static UniversalLadderElement? Convert(LadderCell cell, int column)
    {
        UniversalElementKind kind;
        switch (cell.Kind)
        {
            case LadderElementKind.ContactNormallyOpen: kind = UniversalElementKind.ContactNO; break;
            case LadderElementKind.ContactNormallyClosed: kind = UniversalElementKind.ContactNC; break;
            case LadderElementKind.Coil: kind = UniversalElementKind.Coil; break;
            case LadderElementKind.Set: kind = UniversalElementKind.Set; break;
            case LadderElementKind.Reset: kind = UniversalElementKind.Reset; break;
            case LadderElementKind.Timer: kind = UniversalElementKind.Timer; break;
            case LadderElementKind.Counter: kind = UniversalElementKind.Counter; break;
            case LadderElementKind.RisingEdge: kind = UniversalElementKind.RisingEdge; break;
            case LadderElementKind.FallingEdge: kind = UniversalElementKind.FallingEdge; break;
            case LadderElementKind.Function: kind = UniversalElementKind.Function; break;
            case LadderElementKind.End: kind = UniversalElementKind.End; break;
            default: return null;
        }
        return new UniversalLadderElement
        {
            Kind = kind,
            Address = cell.Address,
            Parameter = cell.Parameter,
            FunctionCode = cell.Mode,
            Column = column
        };
    }
}
