namespace OpenLadderStudio.Application;

/// <summary>Campos editáveis de um elemento, com os nomes que o painel de propriedades mostra.</summary>
public sealed record LadderCellFields(
    string KindName,
    string? AddressLabel,
    string Address,
    string? ParameterLabel,
    string Parameter,
    bool CanBeRetentive,
    bool Retentive);

/// <summary>
/// Painel de propriedades do diagrama: descreve o elemento selecionado e monta o elemento
/// alterado. A montagem passa pelo mesmo <see cref="LadderCellText.Parse"/> da grade, então
/// as regras e as mensagens de erro são as mesmas nas duas formas de editar.
/// </summary>
public static class LadderCellProperties
{
    public static LadderCellFields Describe(LadderCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return cell.Kind switch
        {
            LadderElementKind.Empty => new("Célula vazia", null, "", null, "", false, false),
            LadderElementKind.ContactNormallyOpen => Bit(cell, "Contato NA"),
            LadderElementKind.ContactNormallyClosed => Bit(cell, "Contato NF"),
            LadderElementKind.Coil => Bit(cell, "Bobina"),
            LadderElementKind.Set => Bit(cell, "SET (F-23)"),
            LadderElementKind.Reset => Bit(cell, "RESET (F-24)"),
            LadderElementKind.Timer => new("Temporizador", "Registrador (V)", cell.Address, "Preset (0,1 s)", cell.Parameter,
                true, cell.Mode.Equals("RESET", StringComparison.OrdinalIgnoreCase)),
            LadderElementKind.Counter => new("Contador", "Registrador (V)", cell.Address, "Preset", cell.Parameter, false, false),
            LadderElementKind.Function => new("Função especial", "Código (F-nn)", cell.Address, "Parâmetros", cell.Parameter, false, false),
            LadderElementKind.RisingEdge => new("Borda de subida (F-05)", null, "", null, "", false, false),
            LadderElementKind.FallingEdge => new("Borda de descida (F-06)", null, "", null, "", false, false),
            _ => new("Fim do programa (F-00)", null, "", null, "", false, false),
        };
    }

    /// <summary>
    /// Elemento do mesmo tipo com os campos novos. Lança FormatException com a mesma mensagem
    /// da grade quando o campo é inválido.
    /// </summary>
    public static LadderCell With(LadderCell cell, string address, string parameter, bool retentive)
    {
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.IsEmpty) return new LadderCell();
        LadderCellFields fields = Describe(cell);
        if (fields.AddressLabel is null) return LadderCellText.Parse(LadderCellText.Format(cell));

        string token = LadderCellText.Format(cell).Split(' ')[0];
        address = Clean(address, "endereço");
        parameter = Clean(parameter, "parâmetro");
        if (address.Length == 0) throw new FormatException(fields.KindName + " precisa de " + fields.AddressLabel.ToLowerInvariant() + ".");

        var text = token + " " + address;
        if (fields.ParameterLabel is not null && parameter.Length > 0) text += " " + parameter;
        if (fields.CanBeRetentive && retentive)
        {
            if (parameter.Length == 0) throw new FormatException("Temporizador retentivo precisa de preset.");
            text += " RESET";
        }
        return LadderCellText.Parse(text);
    }

    private static LadderCellFields Bit(LadderCell cell, string name) =>
        new(name, "Endereço", cell.Address, null, "", false, false);

    private static string Clean(string? value, string what)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Contains(' '))
            throw new FormatException("O " + what + " não pode ter espaço: " + text + ".");
        return text;
    }
}
