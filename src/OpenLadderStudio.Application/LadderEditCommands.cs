namespace OpenLadderStudio.Application;

public readonly record struct LadderCellPosition(int Rung, int Column, int Lane);

/// <summary>
/// Regras de edição do diagrama, independentes de tela: onde cada elemento é colocado e quais
/// movimentos são aceitos. Mesmas convenções do editor legado e do compilador TP02: a coluna 8
/// é reservada à saída, e o ramo paralelo só recebe contato.
/// Nenhum método registra desfazer: quem chama registra antes de alterar.
/// </summary>
public static class LadderEditCommands
{
    public const int OutputColumn = LadderRungModel.ColumnCount - 1;

    public static bool IsOutput(LadderElementKind kind) => kind is
        LadderElementKind.Coil or LadderElementKind.Timer or LadderElementKind.Counter or
        LadderElementKind.Set or LadderElementKind.Reset or LadderElementKind.Function or LadderElementKind.End;

    public static bool IsContact(LadderElementKind kind) =>
        kind is LadderElementKind.ContactNormallyOpen or LadderElementKind.ContactNormallyClosed;

    /// <summary>
    /// Onde o elemento vai parar se for colocado na célula clicada: saída vai para a coluna 8 da
    /// via série da mesma linha; condição nunca vai para a coluna 8; paralelo só aceita contato.
    /// Devolve a mensagem de recusa em <paramref name="error"/> quando não há lugar válido.
    /// </summary>
    public static LadderCellPosition? Target(LadderElementKind kind, LadderCellPosition clicked, bool parallel, out string error)
    {
        error = string.Empty;
        if (kind == LadderElementKind.Empty) return clicked;
        if (IsOutput(kind))
        {
            if (parallel)
            {
                error = "O ramo paralelo aceita somente contato NA/NF.";
                return null;
            }
            return new LadderCellPosition(clicked.Rung, OutputColumn, 0);
        }
        if (parallel && !IsContact(kind))
        {
            error = "O ramo paralelo aceita somente contato NA/NF.";
            return null;
        }
        if (clicked.Column == OutputColumn)
        {
            error = "A coluna 8 é reservada à saída. Coloque a condição nas colunas 1 a 7.";
            return null;
        }
        return new LadderCellPosition(clicked.Rung, clicked.Column, parallel ? 1 : 0);
    }

    public static LadderCell CellAt(LadderProjectModel project, LadderCellPosition position)
    {
        ArgumentNullException.ThrowIfNull(project);
        LadderRungModel rung = project.Rungs[position.Rung];
        return (position.Lane == 1 ? rung.Parallel : rung.Series)[position.Column];
    }

    public static void Put(LadderProjectModel project, LadderCellPosition position, LadderCell value)
    {
        ArgumentNullException.ThrowIfNull(value);
        LadderCell target = CellAt(project, position);
        target.Kind = value.Kind;
        target.Address = value.Address;
        target.Parameter = value.Parameter;
        target.Mode = value.Mode;
    }

    public static void Clear(LadderProjectModel project, LadderCellPosition position) => CellAt(project, position).Clear();

    /// <summary>
    /// Move o elemento para outra célula da mesma via. Recusa destino ocupado, saída fora da
    /// coluna 8 e condição na coluna 8, sempre com a mensagem em <paramref name="error"/>.
    /// </summary>
    public static bool Move(LadderProjectModel project, LadderCellPosition from, int toRung, int toColumn, out string error)
    {
        ArgumentNullException.ThrowIfNull(project);
        error = string.Empty;
        if (toRung < 0 || toRung >= project.Rungs.Count || toColumn < 0 || toColumn > OutputColumn)
        {
            error = "Destino fora do diagrama.";
            return false;
        }
        LadderCell source = CellAt(project, from);
        if (source.IsEmpty)
        {
            error = "Não há elemento para mover.";
            return false;
        }
        var to = new LadderCellPosition(toRung, toColumn, from.Lane);
        if (to == from) return false;
        if (IsOutput(source.Kind) != (toColumn == OutputColumn))
        {
            error = IsOutput(source.Kind)
                ? "Saída fica sempre na coluna 8."
                : "A coluna 8 é reservada à saída.";
            return false;
        }
        if (!CellAt(project, to).IsEmpty)
        {
            error = "A célula de destino já tem um elemento.";
            return false;
        }
        Put(project, to, source);
        source.Clear();
        return true;
    }
}
