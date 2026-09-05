using System;
using System.Collections.Generic;
using System.IO;

namespace ModernPC12
{
    internal enum PlcTransportKind
    {
        Serial,
        Tcp,
        EthernetIndustrial,
        VendorSpecific,
        Virtual
    }

    internal enum PlcSupportLevel
    {
        Implemented,
        Experimental,
        Planned
    }

    internal sealed class PlcDriverCapabilities
    {
        public bool Connect;
        public bool MonitorBits;
        public bool ReadRegisters;
        public bool WriteRegisters;
        public bool ReadProgram;
        public bool UploadProgram;
        public bool DownloadProgram;
        public bool OnlineEdit;

        public string Summary()
        {
            List<string> items = new List<string>();
            if (Connect) items.Add("conexão");
            if (MonitorBits) items.Add("monitoramento I/O");
            if (ReadRegisters) items.Add("leitura de registradores");
            if (WriteRegisters) items.Add("escrita de registradores");
            if (ReadProgram) items.Add("leitura de programa");
            if (UploadProgram) items.Add("upload");
            if (DownloadProgram) items.Add("download");
            if (OnlineEdit) items.Add("edição online");
            return items.Count == 0 ? "sem recursos ativos" : string.Join(", ", items.ToArray());
        }
    }

    internal sealed class PlcDeviceProfile
    {
        public string Id;
        public string Manufacturer;
        public string Family;
        public string Model;
        public string Protocol;
        public PlcTransportKind Transport;
        public string DriverId;
        public PlcSupportLevel SupportLevel;
        public string Notes;

        public override string ToString()
        {
            return Manufacturer + " " + Model;
        }
    }

    internal interface IPlcDriver
    {
        string Id { get; }
        string DisplayName { get; }
        PlcSupportLevel SupportLevel { get; }
        PlcDriverCapabilities Capabilities { get; }
        bool Supports(PlcDeviceProfile profile);
        string DescribeConnection(PlcDeviceProfile profile);
    }

    internal interface IPlcProgramCompiler
    {
        string TargetId { get; }
        PlcCompilationResult Compile(UniversalLadderProgram program, PlcDeviceProfile target);
    }

    internal sealed class PlcCompilationResult
    {
        public bool Success;
        public string Message;
        public byte[] Payload;

        public static PlcCompilationResult NotSupported(string message)
        {
            PlcCompilationResult r = new PlcCompilationResult();
            r.Success = false;
            r.Message = message;
            r.Payload = new byte[0];
            return r;
        }
    }

    internal enum UniversalElementKind
    {
        ContactNO,
        ContactNC,
        Coil,
        Set,
        Reset,
        Timer,
        Counter,
        RisingEdge,
        FallingEdge,
        Function,
        End
    }

    internal sealed class UniversalLadderElement
    {
        public UniversalElementKind Kind;
        public string Address;
        public string Parameter;
        public string FunctionCode;

        /// <summary>Coluna de origem no rung. Preserva a posição das condições e dos ramos paralelos.</summary>
        public int Column;

        public UniversalLadderElement()
        {
            Address = string.Empty;
            Parameter = string.Empty;
            FunctionCode = string.Empty;
        }
    }

    internal sealed class UniversalLadderRung
    {
        public readonly List<UniversalLadderElement> Series = new List<UniversalLadderElement>();
        public readonly List<UniversalLadderElement> Parallel = new List<UniversalLadderElement>();
    }

    internal sealed class UniversalLadderProgram
    {
        public string Name;
        public readonly List<UniversalLadderRung> Rungs = new List<UniversalLadderRung>();

        public UniversalLadderProgram()
        {
            Name = "Sem nome";
        }
    }

    internal abstract class PlcDriverBase : IPlcDriver
    {
        private readonly string id;
        private readonly string displayName;
        private readonly PlcSupportLevel supportLevel;
        private readonly PlcDriverCapabilities capabilities;

        protected PlcDriverBase(string id, string displayName, PlcSupportLevel supportLevel, PlcDriverCapabilities capabilities)
        {
            this.id = id;
            this.displayName = displayName;
            this.supportLevel = supportLevel;
            this.capabilities = capabilities;
        }

        public string Id { get { return id; } }
        public string DisplayName { get { return displayName; } }
        public PlcSupportLevel SupportLevel { get { return supportLevel; } }
        public PlcDriverCapabilities Capabilities { get { return capabilities; } }

        public bool Supports(PlcDeviceProfile profile)
        {
            return profile != null && string.Equals(profile.DriverId, Id, StringComparison.OrdinalIgnoreCase);
        }

        public abstract string DescribeConnection(PlcDeviceProfile profile);
    }

    internal sealed class WegTp02Driver : PlcDriverBase
    {
        public WegTp02Driver()
            : base("weg.tp02.serial", "WEG TP02 Serial", PlcSupportLevel.Implemented, CreateCapabilities())
        {
        }

        private static PlcDriverCapabilities CreateCapabilities()
        {
            PlcDriverCapabilities c = new PlcDriverCapabilities();
            c.Connect = true;
            c.MonitorBits = true;
            c.ReadRegisters = true;
            c.ReadProgram = true;
            return c;
        }

        public override string DescribeConnection(PlcDeviceProfile profile)
        {
            return "Serial TP02: 19200 bps, 8O1, estação configurável. É o perfil que o PC12 original força ao abrir a porta. Operações modernas atuais em modo seguro de leitura.";
        }
    }

    internal sealed class GenericModbusRtuDriver : PlcDriverBase
    {
        public GenericModbusRtuDriver()
            : base("generic.modbus.rtu", "Modbus RTU genérico", PlcSupportLevel.Experimental, CreateCapabilities())
        {
        }

        private static PlcDriverCapabilities CreateCapabilities()
        {
            PlcDriverCapabilities c = new PlcDriverCapabilities();
            c.Connect = true;
            c.MonitorBits = true;
            c.ReadRegisters = true;
            return c;
        }

        public override string DescribeConnection(PlcDeviceProfile profile)
        {
            return "Modbus RTU por porta serial. A camada de driver está preparada para coils, discrete inputs, input registers e holding registers.";
        }
    }

    internal sealed class GenericModbusTcpDriver : PlcDriverBase
    {
        public GenericModbusTcpDriver()
            : base("generic.modbus.tcp", "Modbus TCP genérico", PlcSupportLevel.Experimental, CreateCapabilities())
        {
        }

        private static PlcDriverCapabilities CreateCapabilities()
        {
            PlcDriverCapabilities c = new PlcDriverCapabilities();
            c.Connect = true;
            c.MonitorBits = true;
            c.ReadRegisters = true;
            return c;
        }

        public override string DescribeConnection(PlcDeviceProfile profile)
        {
            return "Modbus TCP sobre Ethernet, normalmente porta 502. A programação Ladder permanece dependente do compilador específico do fabricante.";
        }
    }

    /// <summary>
    /// PLC virtual do OpenLadder Studio. Executa o modelo Ladder universal em um motor de varredura
    /// interno, sem hardware. É o único driver em que escrita e transferência de programa são seguras,
    /// porque nenhuma saída física é acionada.
    /// </summary>
    internal sealed class SimulatedPlcDriver : PlcDriverBase
    {
        public const string DriverId = "openladder.simulator";
        public const string ProfileId = "openladder.simulator.plc";

        public SimulatedPlcDriver()
            : base(DriverId, "PLC virtual OpenLadder", PlcSupportLevel.Implemented, CreateCapabilities())
        {
        }

        private static PlcDriverCapabilities CreateCapabilities()
        {
            PlcDriverCapabilities c = new PlcDriverCapabilities();
            c.Connect = true;
            c.MonitorBits = true;
            c.ReadRegisters = true;
            c.WriteRegisters = true;
            c.ReadProgram = true;
            c.DownloadProgram = true;
            return c;
        }

        public override string DescribeConnection(PlcDeviceProfile profile)
        {
            return "Execução local, sem porta serial nem rede. O programa é carregado no motor de varredura interno e as entradas vêm de uma planta virtual ou da tabela de forçamento.";
        }
    }

    internal sealed class PlannedVendorDriver : PlcDriverBase
    {
        private readonly string description;

        public PlannedVendorDriver(string id, string displayName, string description)
            : base(id, displayName, PlcSupportLevel.Planned, new PlcDriverCapabilities())
        {
            this.description = description;
        }

        public override string DescribeConnection(PlcDeviceProfile profile)
        {
            return description;
        }
    }

    internal static class PlcDriverRegistry
    {
        private static readonly List<IPlcDriver> drivers = BuildDrivers();
        private static readonly List<PlcDeviceProfile> profiles = BuildProfiles();

        private static List<IPlcDriver> BuildDrivers()
        {
            List<IPlcDriver> list = new List<IPlcDriver>();
            list.Add(new SimulatedPlcDriver());
            list.Add(new WegTp02Driver());
            list.Add(new GenericModbusRtuDriver());
            list.Add(new GenericModbusTcpDriver());
            list.Add(new PlannedVendorDriver("siemens.s7", "Siemens S7", "Driver planejado para famílias S7. Exige implementação e validação do protocolo e do formato de programa."));
            list.Add(new PlannedVendorDriver("schneider.modicon", "Schneider Modicon", "Perfil de fabricante planejado. Comunicação Modbus poderá reutilizar a camada genérica quando aplicável."));
            list.Add(new PlannedVendorDriver("mitsubishi.melsec", "Mitsubishi MELSEC", "Driver planejado para famílias FX/Q/iQ-F. Programação depende do protocolo e compilador específicos."));
            list.Add(new PlannedVendorDriver("omron.fins", "Omron FINS", "Driver planejado para famílias Omron compatíveis. Requer implementação e validação FINS."));
            list.Add(new PlannedVendorDriver("delta.dvp", "Delta DVP", "Perfil planejado. Alguns modelos poderão usar Modbus para monitoramento, mas download Ladder é específico."));
            list.Add(new PlannedVendorDriver("teco.sg2.programming", "TECO SG2 (porta de programação)", "Driver planejado para a porta de programação usada pelo SG2 Client. O protocolo é proprietário e exige pesquisa e validação em hardware, como foi feito no TP02/RBP."));
            list.Add(new PlannedVendorDriver("rockwell.cip", "Allen-Bradley / Rockwell", "Driver planejado. Requer camada EtherNet/IP/CIP e compilação específica do controlador."));
            return list;
        }

        private static List<PlcDeviceProfile> BuildProfiles()
        {
            List<PlcDeviceProfile> list = new List<PlcDeviceProfile>();
            list.Add(Profile(SimulatedPlcDriver.ProfileId, "OpenLadder", "Simulação", "PLC virtual", "Execução local", PlcTransportKind.Virtual, SimulatedPlcDriver.DriverId, PlcSupportLevel.Implemented, "Executa o Ladder no simulador, sem hardware. Escrita e forçamento são liberados por não haver saída física."));
            list.Add(Profile("weg.tp02.60mr", "WEG", "TP02", "TP02-60MR", "TP02 ASCII", PlcTransportKind.Serial, "weg.tp02.serial", PlcSupportLevel.Implemented, "Primeiro controlador suportado pelo OpenLadder Studio."));
            list.Add(Profile("generic.modbus.rtu", "Genérico", "Modbus", "Modbus RTU", "Modbus RTU", PlcTransportKind.Serial, "generic.modbus.rtu", PlcSupportLevel.Experimental, "Base multi-fabricante para equipamentos que exponham mapa Modbus RTU."));
            list.Add(Profile("generic.modbus.tcp", "Genérico", "Modbus", "Modbus TCP", "Modbus TCP", PlcTransportKind.Tcp, "generic.modbus.tcp", PlcSupportLevel.Experimental, "Base multi-fabricante para equipamentos que exponham mapa Modbus TCP."));
            list.Add(Profile("weg.tpw03", "WEG", "TPW-03", "TPW-03 com cartão RS-485", "Modbus RTU", PlcTransportKind.Serial, "generic.modbus.rtu", PlcSupportLevel.Experimental, "A comunicação Modbus-RTU depende do cartão opcional RS-485/RS-232. Monitoramento em leitura; leitura e download de programa Ladder não implementados."));
            list.Add(Profile("teco.sg2.20v", "TECO", "SG2", "SG2-20V com RS-485", "Modbus RTU", PlcTransportKind.Serial, "generic.modbus.rtu", PlcSupportLevel.Experimental, "SG2-20VR-D, SG2-20VT-D e variantes 12D trazem RS-485 Modbus-RTU integrado. Confirme baud rate, paridade e mapa de endereços no manual do equipamento antes de monitorar."));
            list.Add(Profile("teco.sg2.10hr", "TECO", "SG2", "SG2-10HR-A", "Porta de programação proprietária", PlcTransportKind.VendorSpecific, "teco.sg2.programming", PlcSupportLevel.Planned, "Este modelo não expõe Modbus: a folha de dados do SG2-10HR-A marca comunicação como N/A. Resta a porta de programação do SG2 Client, cujo protocolo é proprietário e ainda não foi implementado."));
            list.Add(Profile("schneider.m221", "Schneider Electric", "Modicon", "M221", "Modbus / fabricante", PlcTransportKind.Tcp, "schneider.modicon", PlcSupportLevel.Planned, "Perfil de dispositivo reservado para implementação futura."));
            list.Add(Profile("delta.dvp", "Delta", "DVP", "DVP Series", "Modbus / fabricante", PlcTransportKind.Serial, "delta.dvp", PlcSupportLevel.Planned, "Perfil de dispositivo reservado para implementação futura."));
            list.Add(Profile("siemens.s71200", "Siemens", "SIMATIC S7", "S7-1200", "S7", PlcTransportKind.EthernetIndustrial, "siemens.s7", PlcSupportLevel.Planned, "Perfil de dispositivo reservado para implementação futura."));
            list.Add(Profile("mitsubishi.fx5u", "Mitsubishi", "MELSEC iQ-F", "FX5U", "MELSEC", PlcTransportKind.EthernetIndustrial, "mitsubishi.melsec", PlcSupportLevel.Planned, "Perfil de dispositivo reservado para implementação futura."));
            list.Add(Profile("omron.cp1l", "Omron", "CP", "CP1L", "FINS / serial", PlcTransportKind.VendorSpecific, "omron.fins", PlcSupportLevel.Planned, "Perfil de dispositivo reservado para implementação futura."));
            list.Add(Profile("rockwell.micro800", "Allen-Bradley", "Micro800", "Micro850", "CIP", PlcTransportKind.EthernetIndustrial, "rockwell.cip", PlcSupportLevel.Planned, "Perfil de dispositivo reservado para implementação futura."));
            return list;
        }

        private static PlcDeviceProfile Profile(string id, string manufacturer, string family, string model, string protocol, PlcTransportKind transport, string driverId, PlcSupportLevel level, string notes)
        {
            PlcDeviceProfile p = new PlcDeviceProfile();
            p.Id = id;
            p.Manufacturer = manufacturer;
            p.Family = family;
            p.Model = model;
            p.Protocol = protocol;
            p.Transport = transport;
            p.DriverId = driverId;
            p.SupportLevel = level;
            p.Notes = notes;
            return p;
        }

        public static IList<IPlcDriver> Drivers { get { return drivers.AsReadOnly(); } }
        public static IList<PlcDeviceProfile> Profiles { get { return profiles.AsReadOnly(); } }

        public static IPlcDriver FindDriver(string id)
        {
            for (int i = 0; i < drivers.Count; i++)
                if (string.Equals(drivers[i].Id, id, StringComparison.OrdinalIgnoreCase)) return drivers[i];
            return null;
        }

        public static PlcDeviceProfile FindProfile(string id)
        {
            for (int i = 0; i < profiles.Count; i++)
                if (string.Equals(profiles[i].Id, id, StringComparison.OrdinalIgnoreCase)) return profiles[i];
            return null;
        }

        public static PlcDeviceProfile DefaultProfile
        {
            get { return FindProfile("weg.tp02.60mr"); }
        }
    }

    internal static class PlcProfileStore
    {
        private static string SettingsDirectory
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "OpenLadder Studio");
            }
        }

        private static string SettingsFile
        {
            get { return Path.Combine(SettingsDirectory, "device.profile"); }
        }

        public static PlcDeviceProfile Load()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return PlcDriverRegistry.DefaultProfile;
                string id = File.ReadAllText(SettingsFile).Trim();
                PlcDeviceProfile profile = PlcDriverRegistry.FindProfile(id);
                return profile ?? PlcDriverRegistry.DefaultProfile;
            }
            catch
            {
                return PlcDriverRegistry.DefaultProfile;
            }
        }

        public static void Save(PlcDeviceProfile profile)
        {
            if (profile == null) return;
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsFile, profile.Id);
        }
    }
}
