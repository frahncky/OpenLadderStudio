using System.Globalization;

namespace OpenLadderStudio.Application;

/// <summary>Tipo de área. Os nomes são gravados no arquivo e precisam bater com o legado.</summary>
public enum PlcMemoryAreaKind
{
    Coil,
    DiscreteInput,
    HoldingRegister,
    InputRegister,
    VendorSpecific
}

/// <summary>Uma área de memória do controlador: faixa de endereços com nome e prefixo.</summary>
public sealed record PlcMemoryArea(string Name, PlcMemoryAreaKind Kind, int StartAddress, int Length, string Prefix, string Notes)
{
    public const int MaxAddress = 65535;
    public const int MaxLength = 2000;

    /// <summary>Último endereço da área.</summary>
    public int EndAddress => StartAddress + Length - 1;

    public static string KindName(PlcMemoryAreaKind kind) => kind switch
    {
        PlcMemoryAreaKind.Coil => "Coils (bits de saída)",
        PlcMemoryAreaKind.DiscreteInput => "Entradas discretas",
        PlcMemoryAreaKind.HoldingRegister => "Holding registers",
        PlcMemoryAreaKind.InputRegister => "Input registers",
        _ => "Específica do fabricante"
    };
}

/// <summary>
/// Mapa de memória por modelo de controlador, no mesmo arquivo do editor legado
/// (%APPDATA%\OpenLadder Studio\memorymaps\&lt;id&gt;.map, uma área por linha separada por tab),
/// então os dois editores mostram e gravam o mesmo mapa.
/// </summary>
public sealed class PlcMemoryMapStore
{
    private const string Header = "# OpenLadder Studio - mapa de memória por controlador";
    private readonly string root;

    public PlcMemoryMapStore(string? root = null)
    {
        this.root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLadder Studio", "memorymaps");
    }

    public string FileFor(PlcDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string safe = string.IsNullOrEmpty(profile.Id) ? "default" : profile.Id.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        return Path.Combine(root, safe + ".map");
    }

    /// <summary>Mapa salvo do modelo; sem arquivo (ou arquivo sem área válida), o padrão.</summary>
    public IReadOnlyList<PlcMemoryArea> Load(PlcDeviceProfile profile)
    {
        string file = FileFor(profile);
        try
        {
            if (!File.Exists(file)) return Defaults(profile);
            var result = new List<PlcMemoryArea>();
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                string[] p = line.Split('\t');
                // Observação vazia: o Trim leva o último tab e sobram 5 campos (o legado gravava assim).
                if (p.Length < 5) continue;
                if (!Enum.TryParse(p[1], true, out PlcMemoryAreaKind kind) || !Enum.IsDefined(kind)) continue;
                if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)) continue;
                if (!int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int length)) continue;
                start = Math.Clamp(start, 0, PlcMemoryArea.MaxAddress);
                length = Math.Clamp(length, 1, Math.Max(1, PlcMemoryArea.MaxAddress + 1 - start));
                result.Add(new PlcMemoryArea(Unescape(p[0]), kind, start, length, Unescape(p[4]), p.Length > 5 ? Unescape(p[5]) : string.Empty));
            }
            return result.Count == 0 ? Defaults(profile) : result;
        }
        catch (IOException) { return Defaults(profile); }
        catch (UnauthorizedAccessException) { return Defaults(profile); }
    }

    /// <summary>Grava o mapa do modelo. Valida antes de escrever: área inválida não chega ao arquivo.</summary>
    public void Save(PlcDeviceProfile profile, IEnumerable<PlcMemoryArea> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var list = areas.ToList();
        foreach (PlcMemoryArea area in list) Validate(area);
        var lines = new List<string> { Header };
        lines.AddRange(list.Select(a => string.Join("\t",
            Escape(a.Name), a.Kind.ToString(), a.StartAddress.ToString(CultureInfo.InvariantCulture),
            a.Length.ToString(CultureInfo.InvariantCulture), Escape(a.Prefix), Escape(a.Notes))));
        string file = FileFor(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file, lines);
    }

    /// <summary>Áreas Modbus de partida para os modelos que falam Modbus; os demais começam vazios.</summary>
    public static IReadOnlyList<PlcMemoryArea> Defaults(PlcDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.SupportLevel == PlcSupportLevel.Planned || !profile.Protocol.StartsWith("Modbus ", StringComparison.OrdinalIgnoreCase)
            || profile.Protocol.Contains('/'))
            return Array.Empty<PlcMemoryArea>();
        const string note = "Área Modbus configurável.";
        return new[]
        {
            new PlcMemoryArea("Coils", PlcMemoryAreaKind.Coil, 0, 16, "C", note),
            new PlcMemoryArea("Discrete Inputs", PlcMemoryAreaKind.DiscreteInput, 0, 16, "DI", note),
            new PlcMemoryArea("Holding Registers", PlcMemoryAreaKind.HoldingRegister, 0, 10, "HR", note),
            new PlcMemoryArea("Input Registers", PlcMemoryAreaKind.InputRegister, 0, 10, "IR", note),
        };
    }

    /// <summary>Lança FormatException com a mensagem para o usuário quando a área é inválida.</summary>
    public static void Validate(PlcMemoryArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        string name = string.IsNullOrWhiteSpace(area.Name) ? "sem nome" : "'" + area.Name.Trim() + "'";
        if (string.IsNullOrWhiteSpace(area.Name)) throw new FormatException("Toda área precisa de nome.");
        if (area.StartAddress < 0 || area.StartAddress > PlcMemoryArea.MaxAddress)
            throw new FormatException("Área " + name + ": endereço inicial deve ficar entre 0 e " + PlcMemoryArea.MaxAddress + ".");
        if (area.Length < 1 || area.Length > PlcMemoryArea.MaxLength)
            throw new FormatException("Área " + name + ": tamanho deve ficar entre 1 e " + PlcMemoryArea.MaxLength + ".");
        if ((long)area.StartAddress + area.Length > PlcMemoryArea.MaxAddress + 1L)
            throw new FormatException("Área " + name + " passa do endereço " + PlcMemoryArea.MaxAddress + ".");
    }

    /// <summary>Pares de áreas do mesmo tipo com endereços em comum (aviso, não erro).</summary>
    public static IReadOnlyList<string> Overlaps(IReadOnlyList<PlcMemoryArea> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var warnings = new List<string>();
        for (int i = 0; i < areas.Count; i++)
            for (int j = i + 1; j < areas.Count; j++)
                if (areas[i].Kind == areas[j].Kind && areas[i].StartAddress <= areas[j].EndAddress && areas[j].StartAddress <= areas[i].EndAddress)
                    warnings.Add("'" + areas[i].Name + "' e '" + areas[j].Name + "' usam endereços em comum.");
        return warnings;
    }

    private static string Escape(string? value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

    private static string Unescape(string? value) => (value ?? string.Empty).Replace("\\\\", "\\");
}
