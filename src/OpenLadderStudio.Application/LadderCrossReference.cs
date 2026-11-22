using System.Text.RegularExpressions;

namespace OpenLadderStudio.Application;

/// <summary>Um uso de endereço no programa: onde está e em que elemento.</summary>
public sealed record LadderAddressUse(string Address, LadderCellPosition Position, string KindName)
{
    /// <summary>"L3 C2 · Contato NA" (e "paralelo" quando está no ramo paralelo).</summary>
    public string Where => "L" + (Position.Rung + 1) + " C" + (Position.Column + 1)
        + (Position.Lane == 1 ? " paralelo" : string.Empty) + " · " + KindName;
}

/// <summary>
/// Referência cruzada do programa: cada endereço com todos os lugares onde aparece, para o
/// painel de projeto navegar até eles. Endereços dos parâmetros de função (ex.: D0001 em
/// "D0001,10,D0002") entram também; constantes e o código da função (F-nn) não.
/// </summary>
public static class LadderCrossReference
{
    private static readonly Regex AddressToken = new(@"^[A-Z]{1,2}\d{1,5}$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<LadderAddressUse> Build(LadderProjectModel project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var uses = new List<LadderAddressUse>();
        for (int rung = 0; rung < project.Rungs.Count; rung++)
        {
            for (int lane = 0; lane < 2; lane++)
            {
                for (int column = 0; column < LadderRungModel.ColumnCount; column++)
                {
                    var position = new LadderCellPosition(rung, column, lane);
                    LadderCell cell = LadderEditCommands.CellAt(project, position);
                    if (cell.IsEmpty) continue;
                    string kind = LadderCellProperties.Describe(cell).KindName;
                    foreach (string address in AddressesOf(cell))
                        uses.Add(new LadderAddressUse(address, position, kind));
                }
            }
        }
        return uses
            .OrderBy(u => u.Address, StringComparer.Ordinal)
            .ThenBy(u => u.Position.Rung).ThenBy(u => u.Position.Lane).ThenBy(u => u.Position.Column)
            .ToList();
    }

    private static IEnumerable<string> AddressesOf(LadderCell cell)
    {
        if (cell.Kind == LadderElementKind.Function)
        {
            foreach (string token in (cell.Parameter ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string upper = token.ToUpperInvariant();
                if (AddressToken.IsMatch(upper)) yield return upper;
            }
            yield break;
        }
        string address = (cell.Address ?? string.Empty).Trim().ToUpperInvariant();
        if (address.Length > 0) yield return address;
    }
}
