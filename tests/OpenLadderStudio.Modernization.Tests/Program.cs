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

// --- Desfazer/refazer pela camada Application (estados em texto .pladder, como no legado) ---
var history = new LadderEditHistory(projects);
var edited = projects.CreateNew();
string original = projects.Serialize(edited);
Require(!history.CanUndo && !history.CanRedo, "Histórico novo deve estar vazio.");

history.Record(edited);
edited.Rungs[0].Series[0] = LadderCellText.Parse("NO X0001");
string afterFirst = projects.Serialize(edited);
history.Record(edited);
edited.Rungs[0].Comment = "Partida";
string afterSecond = projects.Serialize(edited);

edited = history.Undo(edited);
Require(projects.Serialize(edited) == afterFirst, "Desfazer deve voltar ao estado anterior.");
edited = history.Undo(edited);
Require(projects.Serialize(edited) == original, "Desfazer duas vezes deve voltar ao projeto novo.");
Require(!history.CanUndo && history.CanRedo, "Após desfazer tudo, só refazer fica disponível.");
edited = history.Redo(edited);
edited = history.Redo(edited);
Require(projects.Serialize(edited) == afterSecond, "Refazer deve reaplicar as duas alterações.");

history.Undo(edited);
history.Record(edited);
Require(!history.CanRedo, "Alteração nova deve descartar o refazer.");

history.Clear();
history.Record(edited);
history.Record(edited);
Require(history.UndoCount == 1, "Estado repetido não deve ocupar o histórico.");

var bounded = new LadderEditHistory(projects);
var sequence = projects.CreateNew();
for (int i = 0; i < LadderEditHistory.DefaultCapacity + 10; i++)
{
    bounded.Record(sequence);
    sequence.Rungs[0].Comment = "Estado " + i;
}
Require(bounded.UndoCount == LadderEditHistory.DefaultCapacity, "Histórico deve guardar no máximo 30 estados, como o legado.");
while (bounded.CanUndo) sequence = bounded.Undo(sequence);
Require(sequence.Rungs[0].Comment == "Estado 9", "Estado mais antigo mantido deve ser o 11º: " + sequence.Rungs[0].Comment);
RequireThrows<InvalidOperationException>(() => bounded.Undo(sequence), "Desfazer sem histórico deve ser recusado.");

// --- Simulação pela camada Application (mesmo LadderScanEngine do simulador legado) ---
static LadderProjectModel Program(params (string[] Series, string[] Parallel)[] rungs)
{
    var built = new LadderProjectModel();
    foreach (var (series, parallel) in rungs)
    {
        var rung = new LadderRungModel();
        for (int column = 0; column < 8; column++)
        {
            rung.Series[column] = LadderCellText.Parse(column < series.Length ? series[column] : "");
            rung.Parallel[column] = LadderCellText.Parse(column < parallel.Length ? parallel[column] : "");
        }
        built.Rungs.Add(rung);
    }
    return built;
}

// Partida e parada com selo: X0001 liga, Y0001 sela, X0002 (NF) desliga.
var seal = Program(
    (new[] { "NO X0001", "NC X0002", "", "", "", "", "", "COIL Y0001" }, new[] { "NO Y0001" }),
    (new[] { "NO X0003", "", "", "", "", "", "", "TMR V0001 10" }, Array.Empty<string>()),
    (new[] { "NO V0001", "", "", "", "", "", "", "COIL Y0002" }, Array.Empty<string>()),
    (new[] { "", "", "", "", "", "", "", "END" }, Array.Empty<string>()));
var sim = new LadderSimulationSession();
Require(sim.Load(seal), "Programa deve carregar no simulador: " + string.Join(" | ", sim.Diagnostics));
Require(string.Join(",", sim.Points.Select(point => point.Address)) == "X0001,X0002,X0003,Y0001,Y0002,V0001",
    "Endereços do programa divergentes: " + string.Join(",", sim.Points.Select(point => point.Address)));

sim.Step();
Require(!sim.GetBit("Y0001") && !sim.IsRungPowered(0), "Saída deve começar desligada.");
sim.SetInput("X0001", true);
sim.Step();
Require(sim.GetBit("Y0001") && sim.IsRungPowered(0), "Partida deve ligar Y0001.");
sim.SetInput("X0001", false);
sim.Advance(100);
Require(sim.GetBit("Y0001"), "Selo deve manter Y0001 ligada após soltar a partida.");
sim.SetInput("X0002", true);
sim.Step();
Require(!sim.GetBit("Y0001"), "Parada (NF) deve desligar Y0001.");
sim.SetInput("X0002", false);
sim.Step();
Require(!sim.GetBit("Y0001"), "Sem partida, Y0001 continua desligada após soltar a parada.");

// Temporizador: preset 10 = 1 s (base de 100 ms do motor).
sim.SetInput("X0003", true);
sim.Advance(900);
Require(!sim.GetBit("Y0002"), "Temporizador não pode terminar antes de 1 s.");
sim.Advance(200);
Require(sim.GetBit("V0001") && sim.GetBit("Y0002"), "Temporizador deve terminar após 1 s e ligar Y0002.");
Require(sim.GetValue("V0001") >= 10, "Valor do temporizador deve chegar ao preset: " + sim.GetValue("V0001"));

// Forçamento sobrepõe o campo e, ao liberar, o campo volta a valer.
sim.Force("X0001", true);
sim.Step();
Require(sim.IsForced("X0001") && sim.GetBit("Y0001") && sim.ForcedCount == 1, "Forçar X0001 deve ligar Y0001.");
sim.Release("X0001");
sim.SetInput("X0002", true);
sim.Step();
Require(!sim.IsForced("X0001") && !sim.GetBit("Y0001"), "Liberar o forçamento devolve o valor do campo.");

sim.Reset();
Require(sim.ScanCount == 0 && sim.ElapsedMs == 0 && !sim.GetInput("X0002") && sim.ForcedCount == 0, "Reiniciar deve zerar tempo, campo e forçamentos.");
sim.Step();
Require(!sim.GetBit("Y0001") && !sim.GetBit("Y0002"), "Após reiniciar as saídas começam desligadas.");

RequireThrows<ArgumentException>(() => sim.SetInput("Y0001", true), "Saída não pode ser acionada como entrada de campo.");
RequireThrows<ArgumentException>(() => sim.SetInput("X0", true), "Endereço inválido deve ser recusado.");
RequireThrows<InvalidOperationException>(() => new LadderSimulationSession().Step(), "Simular sem programa deve ser recusado.");
Require(sim.Advance(5) == 0 && sim.Advance(5) == 1, "Sobra de tempo deve acumular até completar um passo de 10 ms.");

// --- Plantas simuladas pela camada Application (mesmas plantas do simulador legado) ---
var plants = LadderSimulationSession.AvailablePlants;
Require(plants.Count == 6, "Devem existir as seis plantas do legado: " + plants.Count);
Require(plants.Any(plant => plant.Id == LadderSimulationSession.DefaultPlantId), "Planta padrão deve estar no catálogo.");

void Press(LadderSimulationSession session, string address, double seconds)
{
    session.SetInput(address, true);
    session.Advance(seconds * 1000);
    session.SetInput(address, false);
}

void Run(LadderSimulationSession session, double seconds)
{
    for (double left = seconds * 1000; left > 0; left -= LadderSimulationSession.MaxAdvanceMs)
        session.Advance(Math.Min(left, LadderSimulationSession.MaxAdvanceMs));
}

// Esteira: mesmo roteiro de TestConveyor em SimulationSelfTest.cs (legado), mas com o
// programa de exemplo passando pelo modelo do editor e pelo arquivo .pladder.
var conveyorSample = projects.Deserialize(projects.Serialize(
    LadderSimulationSession.CreateSampleProject("conveyor.diverter", out string conveyorText)));
Require(conveyorText.Length > 0, "Exemplo da esteira deve vir com explicação.");
var plantSim = new LadderSimulationSession();
plantSim.AttachPlant("conveyor.diverter");
Require(plantSim.Load(conveyorSample), "Exemplo da esteira deve carregar: " + string.Join(" | ", plantSim.Diagnostics));
Require(plantSim.PlantName.Length > 0 && plantSim.PlantState.Length > 0, "Planta ligada deve ter nome e estado.");
var entry = plantSim.Points.Single(point => point.Address == "X0001");
var start = plantSim.Points.Single(point => point.Address == "X0004");
Require(entry.DrivenByPlant && entry.Name.Length > 0, "Sensor de entrada deve vir da planta, com nome.");
Require(!start.DrivenByPlant && start.Name.Length > 0, "Botoeira de marcha é do operador, com nome.");
RequireThrows<ArgumentException>(() => plantSim.SetInput("X0001", true), "Sensor da planta só pode ser forçado.");

Run(plantSim, 0.2);
Require(!plantSim.GetBit("C0001"), "Esteira parada antes de qualquer comando.");
Press(plantSim, "X0004", 0.3);
Run(plantSim, 0.2);
Require(plantSim.GetBit("C0001") && plantSim.GetBit("Y0001"), "Marcha deve selar C0001 e ligar o motor.");
Press(plantSim, "X0005", 0.2);
Run(plantSim, 2.2);
Require(!plantSim.GetBit("C0001"), "Botoeira de parada deve derrubar o selo.");

plantSim.Reset();
Press(plantSim, "X0004", 0.2);
Run(plantSim, 18.0);
Require(plantSim.GetValue("V0002") >= 2, "Caixas devem ser desviadas e contadas em V0002: " + plantSim.GetValue("V0002"));

Require(plantSim.Faults.Count > 0, "Esteira oferece falhas injetáveis.");
string faultId = plantSim.Faults[0].Id;
plantSim.SetFault(faultId, true);
Require(plantSim.Faults[0].Active, "Falha deve ficar ativa.");
plantSim.Reset();
Require(!plantSim.Faults[0].Active, "Reiniciar deve limpar as falhas, como no legado.");
RequireThrows<ArgumentException>(() => plantSim.SetFault("nao-existe", true), "Falha desconhecida deve ser recusada.");
RequireThrows<ArgumentException>(() => plantSim.AttachPlant("nao-existe"), "Planta desconhecida deve ser recusada.");

// Todas as plantas: exemplo convertido, gravado e relido roda sem erro por 5 s.
foreach (var info in plants)
{
    var sample = projects.Deserialize(projects.Serialize(LadderSimulationSession.CreateSampleProject(info.Id, out _)));
    var session = new LadderSimulationSession();
    session.AttachPlant(info.Id);
    Require(session.Load(sample), "Exemplo de " + info.Name + " não carregou: " + string.Join(" | ", session.Diagnostics));
    Run(session, 5.0);
    Require(session.ScanCount == 500, "Planta " + info.Name + " deve varrer 500 vezes em 5 s: " + session.ScanCount);
}

plantSim.AttachPlant(null);
Require(plantSim.PlantId is null && plantSim.Faults.Count == 0 && !plantSim.Points.Any(point => point.DrivenByPlant), "Desligar a planta deve remover sensores e falhas.");

// --- Ferramentas TP02 offline (WBP dry-run, HEX, IL -> Ladder) ---
var offline = new Tp02OfflineTools();
var series = new LadderProjectModel();
var seriesRung = new LadderRungModel();
seriesRung.Series[0] = LadderCellText.Parse("NO X0001");
seriesRung.Series[1] = LadderCellText.Parse("NO X0002");
seriesRung.Series[7] = LadderCellText.Parse("COIL Y0001");
series.Rungs.Add(seriesRung);

// Mesmo vetor de Tp02LadderTargetCompilerSelfTest.TestWbpChunks (estação 1, início 0, RI 5).
var wbp = offline.PreviewWbp(series, 1, 0, 5);
Require(wbp.Success && wbp.StepCount == 3, "Pré-visualização WBP deve compilar 3 passos.");
Require(wbp.Frames.Count == 1 && wbp.Frames[0] == ":01?5WBP000003001000002100204000B5\r", "Quadro WBP divergente do vetor legado: " + string.Join("|", wbp.Frames));
Require(wbp.Report.Contains("WBP[00] :01?5WBP000003001000002100204000B5<CR>") && wbp.Report.Contains("Nenhum byte foi transmitido"), "Relatório WBP divergente do legado.");
RequireThrows<ArgumentOutOfRangeException>(() => offline.PreviewWbp(series, 100, 0, 5), "Estação acima de 99 deve ser recusada.");
RequireThrows<ArgumentOutOfRangeException>(() => offline.PreviewWbp(series, 1, 4001, 5), "Início acima de 4000 deve ser recusado.");
RequireThrows<ArgumentOutOfRangeException>(() => offline.PreviewWbp(series, 1, 0, 16), "RI acima de 15 deve ser recusado.");
var wbpRejected = offline.PreviewWbp(invalid);
Require(!wbpRejected.Success && wbpRejected.Frames.Count == 0 && wbpRejected.Report.Contains("ERROS"), "Programa com erro não gera WBP.");

string hexText = offline.ExportHex(series);
Require(string.Concat(hexText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)) == "001000002100204000"
    && hexText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length == 3, "HEX deve ter uma palavra por linha: " + hexText);
RequireThrows<InvalidOperationException>(() => offline.ExportHex(invalid), "HEX de programa com erro deve ser recusado.");

var il = offline.ImportIl("; IL verificada\r\n0000: STR X0001\r\n0001: AND NOT X0002\r\n0002: OUT Y0001\r\n0003: END\r\n", "teste.verified.il.txt");
Require(il.Success && il.InstructionCount == 4 && il.Project is not null, "IL simples deve converter: " + il.Report);
Require(LadderCellText.Format(il.Project!.Rungs[0].Series[0]) == "NO X0001"
    && LadderCellText.Format(il.Project.Rungs[0].Series[1]) == "NC X0002"
    && LadderCellText.Format(il.Project.Rungs[0].Series[7]) == "COIL Y0001"
    && il.Project.Rungs[1].Series[7].Kind == LadderElementKind.End, "Linhas reconstruídas da IL divergentes.");
// Ida e volta: IL -> Ladder -> compilador reproduz as mesmas palavras (vetor NF + END).
Require(offline.ExportHex(il.Project) .Replace(Environment.NewLine, "") == "001000002900204000007000", "IL reconstruída não compila para as mesmas palavras.");

var blocked = offline.ImportIl("0000: STR X0001\n0001: OR X0002\n0002: OUT Y0001\n");
Require(!blocked.Success && blocked.Project is null && blocked.Errors.Any(e => e.Contains("OR")), "OR ainda não é suportado e deve bloquear: " + blocked.Report);
var unknown = offline.ImportIl("0000: STR RAW=12\n0001: OUT Y0001\n");
Require(!unknown.Success && unknown.Report.Contains("CONVERSÃO BLOQUEADA"), "Operando RAW deve bloquear.");
var unclosed = offline.ImportIl("0000: STR X0001\n0001: AND X0002\n");
Require(!unclosed.Success && unclosed.Errors.Any(e => e.Contains("não foi fechada")), "Linha sem OUT deve bloquear.");
Require(!offline.ImportIl("texto qualquer").Success, "Texto sem IL deve ser recusado.");

// --- Regras de edição do diagrama (LadderEditCommands) ---
var clicked = new LadderCellPosition(0, 2, 0);
Require(LadderEditCommands.Target(LadderElementKind.Coil, clicked, false, out _) == new LadderCellPosition(0, 7, 0), "Bobina vai para a coluna 8.");
Require(LadderEditCommands.Target(LadderElementKind.End, clicked, false, out _) == new LadderCellPosition(0, 7, 0), "END vai para a coluna 8.");
Require(LadderEditCommands.Target(LadderElementKind.ContactNormallyOpen, clicked, true, out _) == new LadderCellPosition(0, 2, 1), "Contato paralelo vai para a via paralela da coluna clicada.");
Require(LadderEditCommands.Target(LadderElementKind.ContactNormallyOpen, new LadderCellPosition(0, 7, 0), false, out string colError) is null && colError.Contains("coluna 8"), "Condição na coluna 8 deve ser recusada.");
Require(LadderEditCommands.Target(LadderElementKind.Coil, clicked, true, out string parError) is null && parError.Contains("paralelo"), "Saída no ramo paralelo deve ser recusada.");
Require(LadderEditCommands.Target(LadderElementKind.Timer, clicked, true, out _) is null, "Temporizador no ramo paralelo deve ser recusado.");

var editModel = projects.CreateNew();
editModel.Rungs.Add(new LadderRungModel());
LadderEditCommands.Put(editModel, new LadderCellPosition(0, 0, 0), LadderCellText.Parse("NO X0001"));
LadderEditCommands.Put(editModel, new LadderCellPosition(0, 7, 0), LadderCellText.Parse("COIL Y0001"));
LadderEditCommands.Put(editModel, new LadderCellPosition(0, 3, 0), LadderCellText.Parse("NC X0002"));
Require(LadderEditCommands.Move(editModel, new LadderCellPosition(0, 0, 0), 1, 2, out _), "Contato deve mover para célula livre de outra linha.");
Require(editModel.Rungs[0].Series[0].IsEmpty && LadderCellText.Format(editModel.Rungs[1].Series[2]) == "NO X0001", "Origem deve ficar vazia e destino com o contato.");
Require(!LadderEditCommands.Move(editModel, new LadderCellPosition(1, 2, 0), 0, 3, out string busy) && busy.Contains("já tem"), "Destino ocupado deve ser recusado.");
Require(!LadderEditCommands.Move(editModel, new LadderCellPosition(0, 7, 0), 0, 4, out string outMove) && outMove.Contains("coluna 8"), "Saída não sai da coluna 8.");
Require(!LadderEditCommands.Move(editModel, new LadderCellPosition(0, 3, 0), 1, 7, out _), "Condição não entra na coluna 8.");
Require(LadderEditCommands.Move(editModel, new LadderCellPosition(0, 7, 0), 1, 7, out _), "Saída move entre linhas pela coluna 8.");
Require(!LadderEditCommands.Move(editModel, new LadderCellPosition(0, 0, 0), 1, 1, out string emptyMove) && emptyMove.Length > 0, "Célula vazia não move.");
Require(!LadderEditCommands.Move(editModel, new LadderCellPosition(0, 3, 0), 5, 1, out _), "Destino fora do diagrama deve ser recusado.");
LadderEditCommands.Clear(editModel, new LadderCellPosition(0, 3, 0));
Require(editModel.Rungs[0].Series[3].IsEmpty, "Apagar deve esvaziar a célula.");

// --- Referência cruzada (painel de projeto) ---
var xrefModel = new LadderProjectService().CreateNew();
LadderEditCommands.Put(xrefModel, new LadderCellPosition(0, 0, 0), LadderCellText.Parse("NO X0001"));
LadderEditCommands.Put(xrefModel, new LadderCellPosition(0, 0, 1), LadderCellText.Parse("NC Y0001"));
LadderEditCommands.Put(xrefModel, new LadderCellPosition(0, 7, 0), LadderCellText.Parse("COIL Y0001"));
LadderEditCommands.Put(xrefModel, new LadderCellPosition(0, 1, 0), LadderCellText.Parse("FUN F-13w D0001,10,D0002"));
var xref = LadderCrossReference.Build(xrefModel);
Require(string.Join(" ", xref.Select(u => u.Address)) == "D0001 D0002 X0001 Y0001 Y0001", "Referência cruzada: endereços ordenados, com os parâmetros da função e sem constantes: " + string.Join(" ", xref.Select(u => u.Address)));
Require(xref[3].Where == "L1 C8 · Bobina" && xref[4].Where == "L1 C1 paralelo · Contato NF", "Referência cruzada: série antes do paralelo e descrição do lugar: " + xref[3].Where + " / " + xref[4].Where);

// --- Painel de propriedades (LadderCellProperties) ---
var timer = LadderCellText.Parse("TMR V0001 600");
var timerFields = LadderCellProperties.Describe(timer);
Require(timerFields.AddressLabel is not null && timerFields.ParameterLabel is not null && timerFields.CanBeRetentive && !timerFields.Retentive, "Temporizador: endereço, preset e retentivo.");
Require(LadderCellText.Format(LadderCellProperties.With(timer, "v0002", "900", true)) == "TMR V0002 900 RESET", "Temporizador retentivo deve sair com RESET.");
Require(LadderCellProperties.Describe(LadderCellText.Parse("TMR V0001 600 RESET")).Retentive, "RESET deve aparecer como retentivo.");
RequireThrows<FormatException>(() => LadderCellProperties.With(timer, "V0001", "", true), "Retentivo sem preset deve ser recusado.");
RequireThrows<FormatException>(() => LadderCellProperties.With(LadderCellText.Parse("NO X0001"), "  ", "", false), "Endereço vazio deve ser recusado.");
RequireThrows<FormatException>(() => LadderCellProperties.With(LadderCellText.Parse("NO X0001"), "X 1", "", false), "Endereço com espaço deve ser recusado.");
Require(LadderCellText.Format(LadderCellProperties.With(LadderCellText.Parse("NC X0001"), "x0007", "ignorado", false)) == "NC X0007", "Contato não leva parâmetro.");
Require(LadderCellText.Format(LadderCellProperties.With(LadderCellText.Parse("FUN F-13W D0001,10,D0002"), "F-13W", "D0003,5,D0004", false)) == "FUN F-13W D0003,5,D0004", "Função troca parâmetros.");
Require(LadderCellProperties.Describe(LadderCellText.Parse("END")).AddressLabel is null && LadderCellProperties.With(LadderCellText.Parse("END"), "x", "y", false).Kind == LadderElementKind.End, "END não tem campos.");
Require(LadderCellProperties.With(new LadderCell(), "X0001", "", false).IsEmpty, "Célula vazia continua vazia.");

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
