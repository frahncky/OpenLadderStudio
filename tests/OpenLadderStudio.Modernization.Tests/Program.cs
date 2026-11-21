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

// --- Verificação TP02 (pré-compilação DRY-RUN) pela camada Application ---
// Vetores iguais aos de tests/OpenLadderStudio.Core.Tests/Tp02LadderTargetCompilerSelfTest.cs,
// montados pelo texto de célula que o editor moderno aceita.
var verifier = new Tp02ProgramVerifier();
(string Name, string[] Series, string[] Parallel, string Hex)[] vectors =
{
    ("série STR/AND/OUT", new[] { "NO X0001", "NO X0002", "", "", "", "", "", "COIL Y0001" }, new string[8], "001000002100204000"),
    ("contato NF", new[] { "NO X0001", "NC X0002", "", "", "", "", "", "COIL Y0001" }, new string[8], "001000002900204000"),
    ("paralelo na primeira coluna", new[] { "NO X0001", "", "", "", "", "", "", "COIL Y0001" }, new[] { "NO X0002", "", "", "", "", "", "", "" }, "001000003100204000"),
    ("paralelo após condição", new[] { "NO X0001", "NO X0002", "", "", "", "", "", "COIL Y0001" }, new[] { "", "NO X0003", "", "", "", "", "", "" }, "001000001100003200000100204000"),
    ("TMR + preset", new[] { "NO X0001", "", "", "", "", "", "", "TMR V0001 1000" }, new string[8], "001000006000876800"),
    ("CNT + preset", new[] { "NO X0001", "", "", "", "", "", "", "CNT V0001 10" }, new string[8], "001000006800800A00"),
    ("SET F-23", new[] { "NO X0001", "", "", "", "", "", "", "SET Y0001" }, new string[8], "001000177100C88000"),
    ("RESET F-24", new[] { "NO X0001", "", "", "", "", "", "", "RST C0001" }, new string[8], "001000187100D08000"),
    ("F-13w ADD", new[] { "NO X0001", "", "", "", "", "", "", "FUN F-13w D0001,10,D0002" }, new string[8], "0010000D7700F00000800A00F00100"),
    ("F-43 JMP", new[] { "NO X0001", "", "", "", "", "", "", "FUN F-43 LB001" }, new string[8], "0010002B7100800000"),
    ("END", new[] { "", "", "", "", "", "", "", "END" }, new string[8], "007000"),
};
foreach (var vector in vectors)
{
    var program = new LadderProjectModel();
    var rung = new LadderRungModel();
    for (int column = 0; column < 8; column++)
    {
        rung.Series[column] = LadderCellText.Parse(vector.Series[column]);
        rung.Parallel[column] = LadderCellText.Parse(vector.Parallel[column]);
    }
    program.Rungs.Add(rung);
    var verified = verifier.Verify(program);
    Require(verified.Success, "Verificação TP02 falhou em " + vector.Name + ": " + string.Join(" | ", verified.Errors));
    Require(verified.MachineHex == vector.Hex, "Verificação TP02 divergente em " + vector.Name + ": " + verified.MachineHex);
    Require(verified.Report.Contains("Nenhum byte foi transmitido"), "Relatório deve declarar que nada foi transmitido.");
}

// Programa salvo e reaberto verifica igual ao original.
var saved = projects.Deserialize(projects.Serialize(model));
Require(verifier.Verify(saved).MachineHex == verifier.Verify(model).MachineHex, "Verificação muda após salvar e reabrir.");

var invalid = new LadderProjectModel();
invalid.Rungs.Add(new LadderRungModel());
invalid.Rungs.Add(new LadderRungModel());
invalid.Rungs[1].Series[0] = LadderCellText.Parse("NO X0001");
var rejected = verifier.Verify(invalid);
Require(!rejected.Success, "Linha sem saída na coluna 8 deve ser recusada.");
Require(rejected.Errors.Any(error => error.StartsWith("Rung 2")), "Erro deve apontar a linha 2: " + string.Join(" | ", rejected.Errors));
Require(rejected.Report.Contains("ERROS"), "Relatório deve listar os erros.");

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
