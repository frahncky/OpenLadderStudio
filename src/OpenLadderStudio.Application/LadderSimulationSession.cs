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

/// <summary>
/// Endereço do programa ou da planta. <see cref="Name"/> vem da planta (vazio sem planta);
/// <see cref="DrivenByPlant"/> marca sensores escritos pela planta, que o usuário não aciona.
/// </summary>
public sealed record LadderSimulationPoint(
    string Address,
    LadderSimulationArea Area,
    string AreaText,
    string Name = "",
    bool DrivenByPlant = false);

public sealed record LadderPlantInfo(string Id, string Name, string Description);

public sealed record LadderPlantFault(string Id, string Name, string Description, bool Active);

/// <summary>
/// PLC virtual do editor moderno. Usa o mesmo LadderScanEngine e as mesmas plantas do
/// simulador legado (núcleo, sem WinForms) e nunca fala com hardware: entradas vêm do
/// usuário ou da planta, saídas só existem na imagem de processo em memória.
/// </summary>
public sealed class LadderSimulationSession
{
    /// <summary>Passo de varredura do simulador legado, em milissegundos.</summary>
    public const double ScanStepMs = 10.0;

    /// <summary>Limite de tempo simulado por chamada, para a janela não travar depois de uma pausa longa.</summary>
    public const double MaxAdvanceMs = 1000.0;

    private readonly LadderScanEngine engine = new();
    private readonly List<LadderSimulationPoint> points = new();
    private ISimulatedProcess? plant;
    private double pendingMs;

    public bool Loaded { get; private set; }
    public IReadOnlyList<string> Diagnostics => engine.Diagnostics.ToArray();
    public IReadOnlyList<LadderSimulationPoint> Points => points;
    public long ScanCount => engine.ScanCount;
    public double ElapsedMs => engine.TotalMilliseconds;
    public int RungCount => engine.RungCount;
    public int ForcedCount => engine.Forces.Count;

    // ---- Plantas ----------------------------------------------------------------------

    public static string DefaultPlantId => SimulatedProcessCatalog.DefaultId;

    public static IReadOnlyList<LadderPlantInfo> AvailablePlants =>
        SimulatedProcessCatalog.Create().Select(p => new LadderPlantInfo(p.Id, p.DisplayName, p.Description)).ToArray();

    /// <summary>Programa de exemplo da planta, já no modelo do editor, e a explicação rung a rung.</summary>
    public static LadderProjectModel CreateSampleProject(string plantId, out string description)
    {
        ISimulatedProcess process = Find(plantId);
        description = process.DescribeSampleProgram();
        return FromUniversal(process.BuildSampleProgram());
    }

    public string? PlantId => plant?.Id;
    public string PlantName => plant?.DisplayName ?? string.Empty;
    public string PlantState => plant?.StateSummary() ?? string.Empty;

    public IReadOnlyList<LadderPlantFault> Faults =>
        plant is null
            ? Array.Empty<LadderPlantFault>()
            : plant.Faults.Select(f => new LadderPlantFault(f.Id, f.Name, f.Description, f.Active)).ToArray();

    /// <summary>Liga a planta à imagem de processo (null desliga). A planta parte do repouso, sem falhas.</summary>
    public void AttachPlant(string? plantId)
    {
        plant = plantId is null ? null : Find(plantId);
        RestartPlant();
        CollectPoints();
    }

    public void SetFault(string faultId, bool active)
    {
        if (plant is null) throw new InvalidOperationException("Nenhuma planta ligada ao simulador.");
        SimulatedFault fault = plant.Faults.FirstOrDefault(f => f.Id == faultId)
            ?? throw new ArgumentException("Falha desconhecida nesta planta: " + faultId, nameof(faultId));
        fault.Active = active;
    }

    // ---- Programa e varredura ---------------------------------------------------------

    public bool Load(LadderProjectModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Loaded = engine.Load(ToUniversal(model));
        ClearState();
        RestartPlant();
        CollectPoints();
        return Loaded;
    }

    /// <summary>Volta ao estado de partida: zera lógica, tempo, entradas de campo, forçamentos e a planta.</summary>
    public void Reset()
    {
        engine.Reset();
        ClearState();
        RestartPlant();
    }

    /// <summary>Executa uma varredura de <see cref="ScanStepMs"/>, com a planta avançando antes, como no legado.</summary>
    public void Step()
    {
        RequireLoaded();
        plant?.Step(ScanStepMs / 1000.0, engine.Image);
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
            Step();
            pendingMs -= ScanStepMs;
            scans++;
        }
        return scans;
    }

    /// <summary>Entrada de campo (botoeira ou chave). Só aceita X; sensor escrito pela planta se força.</summary>
    public void SetInput(string address, bool value)
    {
        SimBitRef bit = Parse(address);
        if (bit.Area != SimBitArea.Input) throw new ArgumentException("Só entradas X podem ser acionadas no campo: " + address, nameof(address));
        if (IsPlantDriven(bit)) throw new ArgumentException(address + " é escrito pela planta: use Forçar.", nameof(address));
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

    private void ClearState()
    {
        engine.Forces.ReleaseAll();
        engine.Field.Clear();
        engine.Image.ClearAll();
        pendingMs = 0;
    }

    // Mesmo Restart do autoteste legado: botoeiras soltas, planta em repouso e sem falhas.
    private void RestartPlant()
    {
        if (plant is null) return;
        foreach (SimulatedIoPoint point in plant.Points)
        {
            if (point.Direction != SimIoDirection.PlcInput || point.DrivenByProcess) continue;
            if (SimAddress.TryParseBit(point.Address, out SimBitRef bit)) engine.Field.Set(bit, false);
        }
        plant.Reset();
        foreach (SimulatedFault fault in plant.Faults) fault.Active = false;
    }

    private bool IsPlantDriven(SimBitRef bit) =>
        plant is not null && plant.Points.Any(p =>
            p.DrivenByProcess && p.Direction == SimIoDirection.PlcInput &&
            SimAddress.TryParseBit(p.Address, out SimBitRef other) && other.Key == bit.Key);

    private void RequireLoaded()
    {
        if (!Loaded) throw new InvalidOperationException("Nenhum programa carregado no simulador.");
    }

    private static ISimulatedProcess Find(string plantId) =>
        SimulatedProcessCatalog.Create().FirstOrDefault(p => p.Id == plantId)
        ?? throw new ArgumentException("Planta desconhecida: " + plantId, nameof(plantId));

    private static SimBitRef Parse(string address)
    {
        if (!SimAddress.TryParseBit(address, out SimBitRef bit))
            throw new ArgumentException("Endereço inválido para o simulador: " + address, nameof(address));
        return bit;
    }

    private void CollectPoints()
    {
        points.Clear();
        var names = new Dictionary<int, SimulatedIoPoint>();
        if (plant is not null)
            foreach (SimulatedIoPoint point in plant.Points)
                if (SimAddress.TryParseBit(point.Address, out SimBitRef bit)) names[bit.Key] = point;

        var seen = new HashSet<int>();
        foreach (CompiledRung rung in engine.Rungs)
        {
            for (int column = 0; column < LadderScanEngine.ConditionColumns; column++)
            {
                Add(rung.Series[column]?.Resolved == true ? rung.Series[column].Bit : SimBitRef.Invalid, seen, names);
                Add(rung.Parallel[column]?.Resolved == true ? rung.Parallel[column].Bit : SimBitRef.Invalid, seen, names);
            }
            if (rung.Output != null) Add(rung.Output.Bit, seen, names);
        }
        // Pontos da planta que o programa não usa também aparecem: o usuário vê o que a planta oferece.
        foreach (SimulatedIoPoint point in names.Values)
            if (SimAddress.TryParseBit(point.Address, out SimBitRef bit)) Add(bit, seen, names);

        points.Sort((a, b) => a.Area != b.Area ? a.Area.CompareTo(b.Area) : string.CompareOrdinal(a.Address, b.Address));
    }

    private void Add(SimBitRef bit, HashSet<int> seen, Dictionary<int, SimulatedIoPoint> names)
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
        names.TryGetValue(bit.Key, out SimulatedIoPoint? source);
        bool driven = source is not null && source.DrivenByProcess && source.Direction == SimIoDirection.PlcInput;
        points.Add(new LadderSimulationPoint(SimAddress.Format(bit), area, SimAddress.AreaText(bit.Area), source?.Name ?? string.Empty, driven));
    }

    // ---- Conversões ---------------------------------------------------------------------

    private static readonly (LadderElementKind Editor, UniversalElementKind Universal)[] KindMap =
    {
        (LadderElementKind.ContactNormallyOpen, UniversalElementKind.ContactNO),
        (LadderElementKind.ContactNormallyClosed, UniversalElementKind.ContactNC),
        (LadderElementKind.Coil, UniversalElementKind.Coil),
        (LadderElementKind.Set, UniversalElementKind.Set),
        (LadderElementKind.Reset, UniversalElementKind.Reset),
        (LadderElementKind.Timer, UniversalElementKind.Timer),
        (LadderElementKind.Counter, UniversalElementKind.Counter),
        (LadderElementKind.RisingEdge, UniversalElementKind.RisingEdge),
        (LadderElementKind.FallingEdge, UniversalElementKind.FallingEdge),
        (LadderElementKind.Function, UniversalElementKind.Function),
        (LadderElementKind.End, UniversalElementKind.End),
    };

    /// <summary>Mesma conversão do UniversalLadderAdapter legado, a partir do modelo do editor moderno.</summary>
    internal static UniversalLadderProgram ToUniversal(LadderProjectModel model)
    {
        var program = new UniversalLadderProgram { Name = "Projeto" };
        foreach (LadderRungModel source in model.Rungs)
        {
            var rung = new UniversalLadderRung();
            for (int column = 0; column < LadderRungModel.ColumnCount; column++)
            {
                if (ToUniversal(source.Series[column], column) is UniversalLadderElement series) rung.Series.Add(series);
                if (ToUniversal(source.Parallel[column], column) is UniversalLadderElement parallel) rung.Parallel.Add(parallel);
            }
            program.Rungs.Add(rung);
        }
        return program;
    }

    /// <summary>Caminho inverso, para abrir no editor o programa de exemplo de uma planta.</summary>
    internal static LadderProjectModel FromUniversal(UniversalLadderProgram program)
    {
        var model = new LadderProjectModel();
        foreach (UniversalLadderRung source in program.Rungs)
        {
            var rung = new LadderRungModel();
            foreach (UniversalLadderElement element in source.Series) Place(element, rung.Series);
            foreach (UniversalLadderElement element in source.Parallel) Place(element, rung.Parallel);
            model.Rungs.Add(rung);
        }
        if (model.Rungs.Count == 0) model.Rungs.Add(new LadderRungModel());
        return model;
    }

    private static void Place(UniversalLadderElement element, LadderCell[] lane)
    {
        if (element.Column < 0 || element.Column >= LadderRungModel.ColumnCount)
            throw new InvalidOperationException("Elemento fora das oito colunas do editor: coluna " + (element.Column + 1));
        LadderCell cell = lane[element.Column];
        cell.Kind = KindMap.First(k => k.Universal == element.Kind).Editor;
        cell.Address = element.Address ?? string.Empty;
        cell.Parameter = element.Parameter ?? string.Empty;
        cell.Mode = element.FunctionCode ?? string.Empty;
    }

    private static UniversalLadderElement? ToUniversal(LadderCell cell, int column)
    {
        if (cell.Kind == LadderElementKind.Empty) return null;
        return new UniversalLadderElement
        {
            Kind = KindMap.First(k => k.Editor == cell.Kind).Universal,
            Address = cell.Address,
            Parameter = cell.Parameter,
            FunctionCode = cell.Mode,
            Column = column
        };
    }
}
