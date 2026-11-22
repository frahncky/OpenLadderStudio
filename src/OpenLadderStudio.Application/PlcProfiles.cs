using OpenLadderStudio.Core;

namespace OpenLadderStudio.Application;

public enum PlcSupportLevel
{
    Implemented,
    Experimental,
    Planned
}

/// <summary>Um modelo de controlador que o usuário pode escolher como alvo do projeto.</summary>
public sealed record PlcDeviceProfile(
    string Id,
    string Manufacturer,
    string Family,
    string Model,
    string Protocol,
    PlcConnectionKind ConnectionKind,
    PlcSupportLevel SupportLevel,
    string Notes)
{
    public string DisplayName => Manufacturer + " " + Model;

    public PlcDeviceIdentity Identity => new(Manufacturer, Family, Model, ConnectionKind);

    public string SupportText => SupportLevel switch
    {
        PlcSupportLevel.Implemented => "Suportado",
        PlcSupportLevel.Experimental => "Experimental",
        _ => "Planejado"
    };
}

/// <summary>
/// Modelos conhecidos, com os mesmos identificadores do editor legado (o arquivo de escolha
/// é compartilhado). O ponto de partida é o PLC virtual: o produto não sabe que controlador
/// está na bancada, e a escolha do modelo real é do usuário.
/// </summary>
public static class PlcProfileCatalog
{
    public const string VirtualId = "openladder.simulator.plc";

    public static IReadOnlyList<PlcDeviceProfile> Profiles { get; } = new PlcDeviceProfile[]
    {
        new(VirtualId, "OpenLadder", "Simulação", "PLC virtual", "Execução local", PlcConnectionKind.Virtual, PlcSupportLevel.Implemented,
            "Executa o Ladder no simulador, sem hardware. Escrita e forçamento são liberados por não haver saída física."),
        new("weg.tp02.60mr", "WEG", "TP02", "TP02-60MR", "TP02 PG", PlcConnectionKind.Serial, PlcSupportLevel.Implemented,
            "Primeiro controlador suportado pelo OpenLadder Studio. Serial 19200 bps, 8O1."),
        new("generic.modbus.rtu", "Genérico", "Modbus", "Modbus RTU", "Modbus RTU", PlcConnectionKind.Serial, PlcSupportLevel.Experimental,
            "Base multi-fabricante para equipamentos que exponham mapa Modbus RTU."),
        new("generic.modbus.tcp", "Genérico", "Modbus", "Modbus TCP", "Modbus TCP", PlcConnectionKind.Tcp, PlcSupportLevel.Experimental,
            "Base multi-fabricante para equipamentos que exponham mapa Modbus TCP."),
        new("weg.tpw03", "WEG", "TPW-03", "TPW-03 com cartão RS-485", "Modbus RTU", PlcConnectionKind.Serial, PlcSupportLevel.Experimental,
            "A comunicação Modbus RTU depende do cartão opcional RS-485/RS-232. Monitoramento em leitura; leitura e transferência do programa Ladder não implementadas."),
        new("teco.sg2.20v", "TECO", "SG2", "SG2-20V com RS-485", "Modbus RTU", PlcConnectionKind.Serial, PlcSupportLevel.Experimental,
            "SG2-20VR-D, SG2-20VT-D e variantes 12D trazem RS-485 Modbus RTU integrado. Confirme taxa, paridade e mapa de endereços no manual antes de monitorar."),
        new("teco.sg2.10hr", "TECO", "SG2", "SG2-10HR-A", "Porta de programação proprietária", PlcConnectionKind.Serial, PlcSupportLevel.Planned,
            "Este modelo não expõe Modbus. Resta a porta de programação do SG2 Client, cujo protocolo é proprietário e ainda não foi implementado."),
        new("schneider.m221", "Schneider Electric", "Modicon", "M221", "Modbus / fabricante", PlcConnectionKind.Tcp, PlcSupportLevel.Planned,
            "Perfil reservado para implementação futura."),
        new("delta.dvp", "Delta", "DVP", "DVP Series", "Modbus / fabricante", PlcConnectionKind.Serial, PlcSupportLevel.Planned,
            "Perfil reservado para implementação futura."),
        new("siemens.s71200", "Siemens", "SIMATIC S7", "S7-1200", "S7", PlcConnectionKind.Tcp, PlcSupportLevel.Planned,
            "Perfil reservado para implementação futura."),
        new("mitsubishi.fx5u", "Mitsubishi", "MELSEC iQ-F", "FX5U", "MELSEC", PlcConnectionKind.Tcp, PlcSupportLevel.Planned,
            "Perfil reservado para implementação futura."),
        new("omron.cp1l", "Omron", "CP", "CP1L", "FINS / serial", PlcConnectionKind.Serial, PlcSupportLevel.Planned,
            "Perfil reservado para implementação futura."),
        new("rockwell.micro800", "Allen-Bradley", "Micro800", "Micro850", "CIP", PlcConnectionKind.Tcp, PlcSupportLevel.Planned,
            "Perfil reservado para implementação futura."),
    };

    public static PlcDeviceProfile Default => Find(VirtualId)!;

    public static PlcDeviceProfile? Find(string? id) =>
        Profiles.FirstOrDefault(p => string.Equals(p.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Driver de comunicação do modelo. O PLC virtual não usa driver (a simulação é do próprio
    /// editor), e modelo planejado não tem driver mesmo que o transporte coincida com um genérico.
    /// </summary>
    public static IPlcDriver? DriverFor(PlcDriverCatalog catalog, PlcDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.ConnectionKind == PlcConnectionKind.Virtual || profile.SupportLevel == PlcSupportLevel.Planned) return null;
        return catalog.FindDriver(profile.Identity);
    }

    /// <summary>"conexão, leitura de programa..." a partir dos recursos do driver.</summary>
    public static string DescribeCapabilities(PlcDriverCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var items = new List<string>();
        if (capabilities.Connect) items.Add("conexão");
        if (capabilities.Monitor) items.Add("monitoramento");
        if (capabilities.ReadProgram) items.Add("leitura de programa");
        if (capabilities.WriteProgram) items.Add("transferência de programa");
        if (capabilities.Run) items.Add("RUN");
        if (capabilities.Stop) items.Add("STOP");
        return items.Count == 0 ? "nenhum recurso ativo" : string.Join(", ", items);
    }
}

/// <summary>
/// Modelo escolhido, guardado no mesmo arquivo que o editor legado usa
/// (%APPDATA%\OpenLadder Studio\device.profile, só o identificador).
/// </summary>
public sealed class PlcProfileStore
{
    private readonly string path;

    public PlcProfileStore(string? path = null)
    {
        this.path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLadder Studio", "device.profile");
    }

    public PlcDeviceProfile Load()
    {
        try
        {
            return File.Exists(path) ? PlcProfileCatalog.Find(File.ReadAllText(path)) ?? PlcProfileCatalog.Default : PlcProfileCatalog.Default;
        }
        catch (IOException) { return PlcProfileCatalog.Default; }
        catch (UnauthorizedAccessException) { return PlcProfileCatalog.Default; }
    }

    public void Save(PlcDeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, profile.Id);
    }
}
