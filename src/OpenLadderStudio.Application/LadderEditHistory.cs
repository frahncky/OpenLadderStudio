namespace OpenLadderStudio.Application;

/// <summary>
/// Desfazer e refazer do editor. Como no editor legado, cada estado é o texto .pladder
/// produzido pelo codec do núcleo: o histórico não tem formato próprio e restaura
/// exatamente o que seria gravado em disco. Guarda no máximo <see cref="Capacity"/> estados.
/// </summary>
public sealed class LadderEditHistory
{
    public const int DefaultCapacity = 30;

    private readonly LadderProjectService projects;
    private readonly LinkedList<string> undo = new();
    private readonly Stack<string> redo = new();

    public LadderEditHistory(LadderProjectService projects, int capacity = DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(projects);
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.projects = projects;
        Capacity = capacity;
    }

    public int Capacity { get; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public int UndoCount => undo.Count;

    /// <summary>Registra o estado anterior a uma alteração. Uma alteração nova descarta o refazer.</summary>
    public void Record(LadderProjectModel before)
    {
        ArgumentNullException.ThrowIfNull(before);
        string state = projects.Serialize(before);
        if (undo.Last?.Value == state) return;
        undo.AddLast(state);
        while (undo.Count > Capacity) undo.RemoveFirst();
        redo.Clear();
    }

    public LadderProjectModel Undo(LadderProjectModel current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (undo.Last is null) throw new InvalidOperationException("Nada para desfazer.");
        string state = undo.Last.Value;
        undo.RemoveLast();
        redo.Push(projects.Serialize(current));
        return projects.Deserialize(state);
    }

    public LadderProjectModel Redo(LadderProjectModel current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (redo.Count == 0) throw new InvalidOperationException("Nada para refazer.");
        string state = redo.Pop();
        undo.AddLast(projects.Serialize(current));
        while (undo.Count > Capacity) undo.RemoveFirst();
        return projects.Deserialize(state);
    }

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
    }
}
