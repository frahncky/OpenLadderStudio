namespace OpenLadderStudio.Application;

/// <summary>Tipos de elemento Ladder expostos à interface moderna (espelho do núcleo).</summary>
public enum LadderElementKind
{
    Empty,
    ContactNormallyOpen,
    ContactNormallyClosed,
    Coil,
    Timer,
    Counter,
    Set,
    Reset,
    RisingEdge,
    FallingEdge,
    Function,
    End
}

public sealed class LadderCell
{
    public LadderElementKind Kind { get; set; } = LadderElementKind.Empty;
    public string Address { get; set; } = string.Empty;
    public string Parameter { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;

    public bool IsEmpty => Kind == LadderElementKind.Empty;

    public void Clear()
    {
        Kind = LadderElementKind.Empty;
        Address = string.Empty;
        Parameter = string.Empty;
        Mode = string.Empty;
    }
}

public sealed class LadderRungModel
{
    public const int ColumnCount = 8;

    public LadderRungModel()
    {
        Series = Enumerable.Range(0, ColumnCount).Select(_ => new LadderCell()).ToArray();
        Parallel = Enumerable.Range(0, ColumnCount).Select(_ => new LadderCell()).ToArray();
    }

    public LadderCell[] Series { get; }
    public LadderCell[] Parallel { get; }
    public string Comment { get; set; } = string.Empty;
}

public sealed class LadderProjectModel
{
    public List<LadderRungModel> Rungs { get; } = new();
}
