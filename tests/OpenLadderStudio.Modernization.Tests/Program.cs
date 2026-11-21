using OpenLadderStudio.Application;
using OpenLadderStudio.Core;
using OpenLadderStudio.Drivers.Modbus;
using OpenLadderStudio.Drivers.TP02;

var tp02 = new Tp02Driver();
var modbus = new ModbusDriver();
var catalog = new PlcDriverCatalog(new IPlcDriver[] { tp02, modbus });
var device = new PlcDeviceIdentity("WEG", "TP02", "TP02-60MR", PlcConnectionKind.Serial);

Require(ReferenceEquals(tp02, catalog.FindDriver(device)), "TP02 deve selecionar o driver específico antes do genérico.");
Require(tp02.Capabilities.ReadProgram, "TP02 deve declarar leitura de programa.");
Require(!tp02.Capabilities.Stop, "STOP deve permanecer bloqueado até validação física independente.");
Require(!modbus.Capabilities.WriteProgram, "Modbus genérico não deve declarar download de programa.");

var frame = tp02.BuildStaticRegisterRead(Tp02Pc12StaticReadFrames.Area.V, 0);
Require(frame.SequenceEqual(new byte[] { 0x0A, 0x03, 0x50, 0x00, 0x80, 0x22 }), "Quadro estático V0000 divergente.");

// --- .pladder pela camada Application (codec do núcleo, sem formato duplicado) ---
var projects = new LadderProjectService();
var model = projects.CreateNew();
Require(model.Rungs.Count == 1, "Projeto novo deve ter uma linha.");
Require(projects.Serialize(model) == "PC12-LADDER|2" + Environment.NewLine + "RUNG" + string.Concat(Enumerable.Repeat("|EMPTY~EMPTY", 8)) + Environment.NewLine,
    "Projeto novo deve sair em versão 2, igual ao editor legado.");

string[] samples = { "NO X0", "NC X1", "COIL Y0", "TMR T0 K10 1", "CNT C0 K5", "SET M0", "RST M0", "EUP", "EDN", "FUN F-08 K2", "END" };
foreach (string sample in samples)
    Require(LadderCellText.Format(LadderCellText.Parse(sample)) == sample, "Texto de célula não é simétrico: " + sample);
Require(LadderCellText.Parse("no x0").Address == "X0", "Mnemônico deve aceitar minúsculas.");
Require(LadderCellText.Parse("  ").IsEmpty, "Célula em branco deve ser vazia.");
RequireThrows<FormatException>(() => LadderCellText.Parse("XYZ X0"), "Mnemônico desconhecido deve ser recusado.");
RequireThrows<FormatException>(() => LadderCellText.Parse("END X0"), "END não leva endereço.");

model.Rungs[0].Comment = "Partida ~ motor | 100% ç";
for (int column = 0; column < 8; column++)
    model.Rungs[0].Series[column] = LadderCellText.Parse(samples[column]);
model.Rungs[0].Parallel[0] = LadderCellText.Parse("NO X~2");
var second = new LadderRungModel();
second.Series[7] = LadderCellText.Parse("END");
model.Rungs.Add(second);

string path = Path.Combine(Path.GetTempPath(), "openladder-roundtrip-" + Guid.NewGuid().ToString("N") + LadderProjectService.FileExtension);
try
{
    projects.Save(model, path);
    byte[] raw = File.ReadAllBytes(path);
    Require(raw.Length > 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF, "Arquivo deve sair em UTF-8 com BOM, como no editor legado.");
    var reopened = projects.Open(path);
    Require(projects.Serialize(reopened) == projects.Serialize(model), "Round-trip de arquivo divergente.");
    Require(reopened.Rungs.Count == 2, "Quantidade de linhas divergente após reabrir.");
    Require(reopened.Rungs[0].Comment == model.Rungs[0].Comment, "Comentário divergente após reabrir.");
    Require(reopened.Rungs[0].Parallel[0].Address == "X~2", "Via paralela divergente após reabrir.");
    for (int column = 0; column < 8; column++)
        Require(LadderCellText.Format(reopened.Rungs[0].Series[column]) == samples[column], "Coluna " + (column + 1) + " divergente após reabrir.");
    Require(reopened.Rungs[1].Series[7].Kind == LadderElementKind.End, "END divergente após reabrir.");
    Require(File.ReadAllText(path).StartsWith("PC12-LADDER|3"), "Projeto com comentário deve sair em versão 3.");
    Require(!File.Exists(path + ".tmp"), "Arquivo temporário não deve sobrar.");
}
finally
{
    File.Delete(path);
}

// Arquivo legado (versão 1) continua abrindo pela trilha moderna.
var legacy = projects.Deserialize("PC12-LADDER|1\nRUNG|NO:X0|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|COIL:Y0\n");
Require(legacy.Rungs[0].Series[7].Kind == LadderElementKind.Coil, "Versão 1 deve continuar legível.");
RequireThrows<InvalidDataException>(() => projects.Deserialize("LIXO"), "Cabeçalho desconhecido deve ser recusado.");

Console.WriteLine("ModernizationSelfTest: OK");

static void RequireThrows<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException(message);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
