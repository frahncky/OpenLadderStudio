using ModernPC12;
using OpenLadderStudio.Application;

namespace OpenLadderStudio.WinForms;

public enum LadderDiagramTool
{
    Select,
    ContactNO,
    ContactNC,
    ParallelNO,
    ParallelNC,
    Coil,
    Timer,
    Counter,
    Set,
    Reset,
    RisingEdge,
    FallingEdge,
    Function,
    End,
    Erase
}

public sealed class LadderCellEventArgs : EventArgs
{
    public LadderCellEventArgs(int rung, int column, int lane)
    {
        Rung = rung;
        Column = column;
        Lane = lane;
    }

    public int Rung { get; }

    /// <summary>Coluna 0..7 (C1..C8).</summary>
    public int Column { get; }

    /// <summary>0 = via série, 1 = via paralela.</summary>
    public int Lane { get; }
}

public sealed class LadderMoveRequestEventArgs : EventArgs
{
    public LadderMoveRequestEventArgs(int fromRung, int fromColumn, int lane, int toRung, int toColumn)
    {
        FromRung = fromRung;
        FromColumn = fromColumn;
        Lane = lane;
        ToRung = toRung;
        ToColumn = toColumn;
    }

    public int FromRung { get; }
    public int FromColumn { get; }
    public int Lane { get; }
    public int ToRung { get; }
    public int ToColumn { get; }
}

public sealed class LadderCommentRequestEventArgs : EventArgs
{
    public LadderCommentRequestEventArgs(int rung, string text)
    {
        Rung = rung;
        Text = text;
    }

    public int Rung { get; }
    public string Text { get; }
}

/// <summary>
/// Diagrama Ladder da trilha moderna: o mesmo LadderCanvas do editor legado (símbolos,
/// zoom, calha e cabeçalho congelados, comentário no lugar), alimentado pelo modelo do
/// editor moderno. Não altera o projeto: cada gesto vira um evento, e quem hospeda aplica
/// a alteração (com desfazer e marcação de não salvo).
/// </summary>
public sealed class LadderDiagram : UserControl
{
    private readonly LadderCanvas canvas = new() { Dock = DockStyle.Fill };
    private LadderDiagramTool tool = LadderDiagramTool.Select;
    private bool readOnly;

    public LadderDiagram()
    {
        canvas.Rungs = new List<LadderRung> { new LadderRung() };
        canvas.SelectedRung = 0;
        canvas.SelectedColumn = 0;
        Controls.Add(canvas);

        canvas.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        canvas.ElementAction += (_, _) =>
        {
            if (!readOnly && tool != LadderDiagramTool.Select) PlaceRequested?.Invoke(this, CurrentCell());
        };
        canvas.ElementDoubleClick += (_, _) =>
        {
            if (!readOnly) EditRequested?.Invoke(this, CurrentCell());
        };
        canvas.ToolReleased += (_, _) => ActiveTool = LadderDiagramTool.Select;
        canvas.AddRungRequested += (_, _) =>
        {
            if (!readOnly) AddRungRequested?.Invoke(this, EventArgs.Empty);
        };
        canvas.CommentEdited += (_, e) =>
        {
            if (!readOnly) CommentRequested?.Invoke(this, new LadderCommentRequestEventArgs(e.Rung, e.Text ?? string.Empty));
        };
        canvas.ElementMoved += (_, e) =>
        {
            if (!readOnly) MoveRequested?.Invoke(this, new LadderMoveRequestEventArgs(e.FromRung, e.FromColumn, e.FromLane, e.ToRung, e.ToColumn));
        };
        canvas.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.None && !readOnly && !canvas.IsEditingComment)
            {
                ClearRequested?.Invoke(this, CurrentCell());
                e.Handled = true;
            }
        };
    }

    public event EventHandler SelectionChanged;

    /// <summary>Clique numa célula com uma ferramenta de elemento armada.</summary>
    public event EventHandler<LadderCellEventArgs> PlaceRequested;

    /// <summary>Duplo clique numa célula: editar o elemento.</summary>
    public event EventHandler<LadderCellEventArgs> EditRequested;

    /// <summary>Delete sobre a célula selecionada.</summary>
    public event EventHandler<LadderCellEventArgs> ClearRequested;

    public event EventHandler<LadderMoveRequestEventArgs> MoveRequested;
    public event EventHandler<LadderCommentRequestEventArgs> CommentRequested;
    public event EventHandler AddRungRequested;
    public event EventHandler ToolChanged;

    public LadderDiagramTool ActiveTool
    {
        get => tool;
        set
        {
            if (readOnly) value = LadderDiagramTool.Select;
            if (tool == value) return;
            tool = value;
            // Com ferramenta armada o clique coloca elemento; o arrasto só vale no modo ponteiro.
            canvas.DragEnabled = tool == LadderDiagramTool.Select;
            canvas.Cursor = tool == LadderDiagramTool.Select ? Cursors.Default : Cursors.Cross;
            ToolChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Somente leitura (simulação): nenhum gesto vira alteração, e o arrasto fica desligado.</summary>
    public bool ReadOnly
    {
        get => readOnly;
        set
        {
            readOnly = value;
            if (readOnly)
            {
                if (canvas.IsEditingComment) canvas.EndEditComment(false, false);
                ActiveTool = LadderDiagramTool.Select;
            }
            canvas.DragEnabled = !readOnly && tool == LadderDiagramTool.Select;
            canvas.ShowGuide = !readOnly;
            canvas.Invalidate();
        }
    }

    public int SelectedRung => canvas.SelectedRung;
    public int SelectedColumn => canvas.SelectedColumn;
    public int SelectedLane => canvas.SelectedLane;

    public int ZoomPercent
    {
        get => canvas.ZoomPercent;
        set => canvas.SetZoomPercent(value);
    }

    public bool IsEditingComment => canvas.IsEditingComment;

    /// <summary>Redesenha a partir do modelo; a seleção é mantida dentro dos limites.</summary>
    public void ShowProject(LadderProjectModel project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (canvas.IsEditingComment) canvas.EndEditComment(false, false);
        var rungs = new List<LadderRung>(project.Rungs.Count);
        foreach (LadderRungModel source in project.Rungs)
        {
            var rung = new LadderRung { Comment = source.Comment };
            for (int column = 0; column < LadderRungModel.ColumnCount; column++)
            {
                Copy(source.Series[column], rung.Elements[column]);
                Copy(source.Parallel[column], rung.Parallel[column]);
            }
            rungs.Add(rung);
        }
        if (rungs.Count == 0) rungs.Add(new LadderRung());
        canvas.Rungs = rungs;
        canvas.SelectedRung = Math.Clamp(canvas.SelectedRung, 0, rungs.Count - 1);
        canvas.SelectedColumn = Math.Clamp(canvas.SelectedColumn, 0, LadderRung.ColumnCount - 1);
        canvas.Invalidate();
    }

    public void Select(int rung, int column, int lane)
    {
        if (canvas.Rungs == null || canvas.Rungs.Count == 0) return;
        canvas.SelectedRung = Math.Clamp(rung, 0, canvas.Rungs.Count - 1);
        canvas.SelectedColumn = Math.Clamp(column, 0, LadderRung.ColumnCount - 1);
        canvas.SelectedLane = lane == 1 ? 1 : 0;
        canvas.ScrollToRung(canvas.SelectedRung);
        canvas.Invalidate();
    }

    /// <summary>Destaque da última varredura (true = linha energizada); null limpa.</summary>
    public void SetRungPower(bool[] power) => canvas.SetRungPower(power);

    public void BeginEditComment(int rung)
    {
        if (!readOnly) canvas.BeginEditComment(rung);
    }

    public void Repaint() => canvas.Invalidate();

    public new bool Focus() => canvas.Focus();

    private LadderCellEventArgs CurrentCell() => new(canvas.SelectedRung, canvas.SelectedColumn, canvas.SelectedLane);

    private static void Copy(LadderCell source, LadderElement target)
    {
        target.Type = source.Kind switch
        {
            LadderElementKind.ContactNormallyOpen => LadderElementType.ContactNO,
            LadderElementKind.ContactNormallyClosed => LadderElementType.ContactNC,
            LadderElementKind.Coil => LadderElementType.Coil,
            LadderElementKind.Timer => LadderElementType.Timer,
            LadderElementKind.Counter => LadderElementType.Counter,
            LadderElementKind.Set => LadderElementType.Set,
            LadderElementKind.Reset => LadderElementType.Reset,
            LadderElementKind.RisingEdge => LadderElementType.EdgeUp,
            LadderElementKind.FallingEdge => LadderElementType.EdgeDown,
            LadderElementKind.Function => LadderElementType.Function,
            LadderElementKind.End => LadderElementType.End,
            _ => LadderElementType.Empty
        };
        target.Address = source.Address;
        target.Parameter = source.Parameter;
        target.Mode = source.Mode;
    }
}
