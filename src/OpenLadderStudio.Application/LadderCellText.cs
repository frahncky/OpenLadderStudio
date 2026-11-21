namespace OpenLadderStudio.Application;

/// <summary>
/// Forma textual de uma célula para edição em grade: mnemônico seguido dos campos separados
/// por espaço, por exemplo "NO X0", "COIL Y0", "TMR T0 K10", "CNT C0 K5", "END".
/// Vazio representa célula vazia. Não é o formato de arquivo (esse é do codec no núcleo).
/// </summary>
public static class LadderCellText
{
    private static readonly (string Token, LadderElementKind Kind, int MinFields, int MaxFields)[] Table =
    {
        ("NO", LadderElementKind.ContactNormallyOpen, 1, 1),
        ("NC", LadderElementKind.ContactNormallyClosed, 1, 1),
        ("COIL", LadderElementKind.Coil, 1, 1),
        ("TMR", LadderElementKind.Timer, 1, 3),
        ("CNT", LadderElementKind.Counter, 1, 2),
        ("SET", LadderElementKind.Set, 1, 1),
        ("RST", LadderElementKind.Reset, 1, 1),
        ("EUP", LadderElementKind.RisingEdge, 0, 0),
        ("EDN", LadderElementKind.FallingEdge, 0, 0),
        ("FUN", LadderElementKind.Function, 1, 2),
        ("END", LadderElementKind.End, 0, 0),
    };

    public static string Mnemonics => string.Join(", ", Table.Select(entry => entry.Token));

    public static string Format(LadderCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.IsEmpty) return string.Empty;
        var entry = Table.First(item => item.Kind == cell.Kind);
        var parts = new List<string> { entry.Token };
        if (entry.MaxFields >= 1) parts.Add(cell.Address);
        if (entry.MaxFields >= 2 && (cell.Parameter.Length > 0 || cell.Mode.Length > 0)) parts.Add(cell.Parameter);
        if (entry.MaxFields >= 3 && cell.Mode.Length > 0) parts.Add(cell.Mode);
        return string.Join(" ", parts);
    }

    /// <summary>Converte o texto digitado em célula. Lança FormatException com mensagem para o usuário.</summary>
    public static LadderCell Parse(string? text)
    {
        var cell = new LadderCell();
        if (string.IsNullOrWhiteSpace(text)) return cell;

        string[] parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string token = parts[0].ToUpperInvariant();
        var match = Table.FirstOrDefault(item => item.Token == token);
        if (match.Token is null)
            throw new FormatException("Elemento desconhecido: " + parts[0] + ". Use " + Mnemonics + ".");

        int fields = parts.Length - 1;
        if (fields < match.MinFields || fields > match.MaxFields)
            throw new FormatException(match.MaxFields == 0
                ? match.Token + " não leva endereço."
                : match.Token + " espera de " + match.MinFields + " a " + match.MaxFields + " campo(s).");

        cell.Kind = match.Kind;
        if (match.Kind == LadderElementKind.End) cell.Address = "F-00";
        if (fields >= 1) cell.Address = parts[1].ToUpperInvariant();
        if (fields >= 2) cell.Parameter = parts[2].ToUpperInvariant();
        if (fields >= 3) cell.Mode = parts[3];
        return cell;
    }
}
