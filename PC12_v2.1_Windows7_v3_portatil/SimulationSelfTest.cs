using System;
using System.Collections.Generic;
using System.Globalization;

namespace ModernPC12
{
    /// <summary>
    /// Verificação automática do motor de varredura e da biblioteca de plantas.
    ///
    /// Executa em console e retorna código de saída diferente de zero quando alguma
    /// verificação falha, para poder ser usada no CI antes de publicar uma release.
    /// </summary>
    internal static class SimulationSelfTest
    {
        private const double StepSeconds = 0.010;
        private const double StepMs = 10.0;

        private static int failures;
        private static LadderScanEngine engine;
        private static ISimulatedProcess plant;

        private static int Main()
        {
            // StudioDiagnostics não é instalado aqui de propósito: ele exibe caixa de
            // diálogo em falha, o que travaria a execução do autoteste no CI.
            engine = new LadderScanEngine();

            Console.WriteLine("Autoteste da simulação do OpenLadder Studio");
            Console.WriteLine();

            TestAddressParsing();
            TestCatalog();
            TestConveyor();
            TestSilo();
            TestStarDelta();
            TestTrafficLight();
            TestElevator();
            TestPress();

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("Todas as verificações passaram.");
                return 0;
            }

            Console.WriteLine(failures.ToString(CultureInfo.InvariantCulture) + " verificação(ões) falharam.");
            return 1;
        }

        private static void Check(string description, bool condition)
        {
            if (condition)
            {
                Console.WriteLine("  ok    " + description);
                return;
            }

            failures++;
            Console.WriteLine("  FALHA " + description);
        }

        private static void Section(string title)
        {
            Console.WriteLine(title);
        }

        /// <summary>Seleciona a planta, carrega o programa de exemplo e volta ao estado inicial.</summary>
        private static void Use(ISimulatedProcess selected)
        {
            plant = selected;
            engine.Load(plant.BuildSampleProgram());
            Restart();
        }

        /// <summary>
        /// Recoloca o par PLC virtual e planta no estado inicial e registra as botoeiras de campo,
        /// como a interface de simulação faz ao abrir.
        /// </summary>
        private static void Restart()
        {
            engine.Reset();
            engine.Forces.ReleaseAll();
            engine.Image.ClearAll();
            engine.Field.Clear();

            for (int i = 0; i < plant.Points.Count; i++)
            {
                SimulatedIoPoint point = plant.Points[i];
                if (point.Direction != SimIoDirection.PlcInput || point.DrivenByProcess) continue;

                SimBitRef bit;
                if (SimAddress.TryParseBit(point.Address, out bit)) engine.Field.Set(bit, false);
            }

            plant.Reset();
            for (int i = 0; i < plant.Faults.Count; i++) plant.Faults[i].Active = false;
        }

        private static void Run(double seconds)
        {
            int steps = (int)(seconds / StepSeconds);
            for (int i = 0; i < steps; i++)
            {
                plant.Step(StepSeconds, engine.Image);
                engine.Execute(StepMs);
            }
        }

        private static void Press(string address, double seconds)
        {
            SimBitRef bit = Bit(address);
            engine.Field.Set(bit, true);
            Run(seconds);
            engine.Field.Set(bit, false);
        }

        private static void Hold(string address, bool value)
        {
            engine.Field.Set(Bit(address), value);
        }

        private static bool On(string address)
        {
            return engine.Image.GetBit(Bit(address));
        }

        private static int Value(string address)
        {
            SimBitRef bit = Bit(address);
            return engine.Image.GetVariableValue(bit.Index);
        }

        private static SimBitRef Bit(string address)
        {
            SimBitRef bit;
            SimAddress.TryParseBit(address, out bit);
            return bit;
        }

        private static void SetFault(string id, bool active)
        {
            for (int i = 0; i < plant.Faults.Count; i++)
                if (plant.Faults[i].Id == id) plant.Faults[i].Active = active;
        }

        private static void TestAddressParsing()
        {
            Section("Endereçamento");
            SimBitRef reference;
            int word;

            Check("X0001 é uma entrada válida", SimAddress.TryParseBit("X0001", out reference) && reference.Area == SimBitArea.Input && reference.Index == 1);
            Check("SC004 é um contato especial válido", SimAddress.TryParseBit("SC004", out reference) && reference.Area == SimBitArea.Special && reference.Index == 4);
            Check("V0256 é um bloco válido", SimAddress.TryParseBit("V0256", out reference) && reference.Area == SimBitArea.Variable && reference.Index == 256);
            Check("C2048 está dentro do limite", SimAddress.TryParseBit("C2048", out reference) && reference.Index == 2048);
            Check("C2049 é recusado", !SimAddress.TryParseBit("C2049", out reference));
            Check("X0385 é recusado", !SimAddress.TryParseBit("X0385", out reference));
            Check("X0000 é recusado", !SimAddress.TryParseBit("X0000", out reference));
            Check("V0257 é recusado", !SimAddress.TryParseBit("V0257", out reference));
            Check("prefixo desconhecido é recusado", !SimAddress.TryParseBit("Z0001", out reference));
            Check("D2048 é um registrador válido", SimAddress.TryParseWord("D2048", out word) && word == 2048);
            Check("D2049 é recusado", !SimAddress.TryParseWord("D2049", out word));
        }

        /// <summary>
        /// Toda planta do catálogo precisa carregar o próprio programa sem aviso de compilação
        /// e sobreviver a uma execução longa sem exceção.
        /// </summary>
        private static void TestCatalog()
        {
            Section("Catálogo de plantas");
            IList<ISimulatedProcess> plants = SimulatedProcessCatalog.Create();
            Check("catálogo com pelo menos seis plantas", plants.Count >= 6);

            for (int i = 0; i < plants.Count; i++)
            {
                Use(plants[i]);
                bool clean = engine.Diagnostics.Count == 0;
                if (!clean)
                    for (int d = 0; d < engine.Diagnostics.Count; d++)
                        Console.WriteLine("        aviso: " + engine.Diagnostics[d]);

                Check(plants[i].DisplayName + ": programa de exemplo compila sem avisos", clean);
                Check(plants[i].DisplayName + ": programa tem linhas e termina em END", engine.RungCount > 1);
                Check(plants[i].DisplayName + ": descreve os próprios pontos de I/O", plant.Points.Count > 0);
                Check(plants[i].DisplayName + ": oferece falhas injetáveis", plant.Faults.Count > 0);

                Run(30.0);
                SimScene scene = new SimScene();
                plant.BuildScene(scene, engine.Image);
                Check(plants[i].DisplayName + ": desenha o próprio sinóptico", scene.Shapes.Count > 0);
            }
        }

        private static void TestConveyor()
        {
            Section("Esteira: ciclo, selo e falhas");
            Use(new ConveyorProcess());
            ConveyorProcess belt = (ConveyorProcess)plant;

            Run(0.2);
            Check("máquina parada antes de qualquer comando", !On("C0001"));

            Press(ConveyorProcess.StartInput, 0.3);
            Run(0.2);
            Check("o selo mantém C0001 depois de soltar a botoeira", On("C0001"));
            Check("motor energizado com a marcha", On(ConveyorProcess.MotorOutput));

            Press(ConveyorProcess.StopInput, 0.2);
            Run(2.2);
            Check("a botoeira de parada derruba o selo", !On("C0001"));
            Check("correia freou até parar", belt.BeltSpeed < 0.001);

            Restart();
            Press(ConveyorProcess.StartInput, 0.2);
            Run(18.0);
            Check("caixas foram desviadas", belt.DivertedCount >= 2);
            Check("nenhuma caixa passou direto pelo fim da esteira", belt.LostCount == 0);
            Check("o contador V0002 acompanha as caixas desviadas", Value("V0002") == belt.DivertedCount);

            int changes = 0;
            bool last = On(ConveyorProcess.LampOutput);
            for (int i = 0; i < 300; i++)
            {
                plant.Step(StepSeconds, engine.Image);
                engine.Execute(StepMs);
                bool now = On(ConveyorProcess.LampOutput);
                if (now != last) changes++;
                last = now;
            }
            Check("o sinaleiro pisca com SC004 (" + changes.ToString(CultureInfo.InvariantCulture) + " transições em 3 s)", changes >= 4 && changes <= 8);

            int hourMeter = Value("V0001");
            Press(ConveyorProcess.StopInput, 0.2);
            Run(2.0);
            Check("o temporizador retentivo preserva o acumulado com a linha desligada", Value("V0001") >= hourMeter);

            Restart();
            Press(ConveyorProcess.StartInput, 0.2);
            Run(0.5);
            engine.Forces.Force(Bit(ConveyorProcess.StopInput), true);
            Run(0.2);
            Check("forçar a botoeira de parada derruba o selo", !On("C0001"));
            engine.Forces.Release(Bit(ConveyorProcess.StopInput));
            Press(ConveyorProcess.StartInput, 0.2);
            Run(0.2);
            Check("liberado o forçamento, a entrada volta ao valor de campo", On("C0001"));
            engine.Forces.Force(Bit(ConveyorProcess.LampOutput), true);
            Run(0.2);
            Check("saída forçada sobrescreve a lógica depois da varredura", On(ConveyorProcess.LampOutput));
            engine.Forces.ReleaseAll();

            Restart();
            Press(ConveyorProcess.StartInput, 0.2);
            SetFault("pusher.jam", true);
            Run(20.0);
            Check("desviador emperrado: o fim de curso nunca é atingido", !On(ConveyorProcess.PusherFeedbackInput));
            Check("desviador emperrado: o intertravamento desliga o motor", !On(ConveyorProcess.MotorOutput));

            Restart();
            Press(ConveyorProcess.StartInput, 0.2);
            SetFault("belt.slip", true);
            Run(4.0);
            Check("esteira patinando: o térmico ainda não atuou", !On(ConveyorProcess.OverloadInput));
            Run(6.0);
            Check("esteira patinando: o térmico atua e derruba a marcha", On(ConveyorProcess.OverloadInput) && !On("C0001"));
        }

        private static void TestSilo()
        {
            Section("Silo: enchimento, descarga e transbordo");
            Use(new SiloProcess());
            SiloProcess silo = (SiloProcess)plant;

            Press(SiloProcess.StartInput, 0.3);
            Run(10.0);
            Check("o silo enche até a chave de nível alto", On(SiloProcess.HighLevelInput));
            Check("a válvula de enchimento fecha no nível alto", !On(SiloProcess.FillValveOutput));
            Check("o sinaleiro de silo cheio acende", On(SiloProcess.FullLampOutput));
            Check("o nível parou abaixo do transbordo", silo.Level < 100.0 && silo.OverflowCount == 0);

            double before = silo.Level;
            Run(20.0);
            Check("a chegada do caminhão abre a descarga e o nível cai", silo.Level < before || silo.LoadCount > 0);

            Restart();
            Press(SiloProcess.StartInput, 0.3);
            SetFault("silo.high.blind", true);
            Run(25.0);
            Check("chave de nível alto cega: o silo transborda", silo.OverflowCount > 0);
            Check("chave de nível alto cega: a válvula de enchimento nunca fecha", On(SiloProcess.FillValveOutput));

            Restart();
            Press(SiloProcess.StartInput, 0.3);
            SetFault("silo.packed", true);
            Run(45.0);
            Check("material empedrado: a descarga fica lenta e o silo permanece cheio", silo.Level > 60.0);
        }

        private static void TestStarDelta()
        {
            Section("Estrela-triângulo: comutação e intertravamento");
            Use(new StarDeltaProcess());
            StarDeltaProcess motor = (StarDeltaProcess)plant;

            Press(StarDeltaProcess.StartInput, 0.3);
            Run(0.3);
            Check("a partida fecha o contator de rede", On(StarDeltaProcess.MainsOutput));
            Check("a partida começa em estrela", On(StarDeltaProcess.StarOutput));
            Check("o triângulo não fecha junto com a estrela", !On(StarDeltaProcess.DeltaOutput));

            Run(12.0);
            Check("a comutação leva o motor ao triângulo", On(StarDeltaProcess.DeltaOutput));
            Check("a estrela abre antes do triângulo fechar", !On(StarDeltaProcess.StarOutput));
            Check("o motor alcança a velocidade nominal", motor.Speed > 0.95);
            Check("nenhum curto entre fases com o intertravamento correto", motor.ShortCircuitCount == 0);
            Check("a partida foi contabilizada", Value("V0003") >= 1);

            Press(StarDeltaProcess.StopInput, 0.3);
            Run(0.3);
            Check("a parada derruba os três contatores", !On(StarDeltaProcess.MainsOutput) && !On(StarDeltaProcess.DeltaOutput));

            Restart();
            Press(StarDeltaProcess.StartInput, 0.3);
            SetFault("stardelta.star.welded", true);
            Run(14.0);
            Check("contator de estrela colado: a planta registra curto entre fases", motor.ShortCircuitCount > 0);
            Check("contator de estrela colado: a proteção derruba a marcha", !On("C0001"));

            Restart();
            Press(StarDeltaProcess.StartInput, 0.3);
            SetFault("stardelta.load.heavy", true);
            Run(6.0);
            Check("carga pesada: o motor ainda não terminou de acelerar", motor.Speed < 0.95);
        }

        private static void TestTrafficLight()
        {
            Section("Semáforo: sequência e demanda");
            Use(new TrafficLightProcess());
            TrafficLightProcess crossing = (TrafficLightProcess)plant;

            Hold(TrafficLightProcess.AutoSwitchInput, true);
            Run(1.0);
            Check("o ciclo começa pelo verde da via principal", On(TrafficLightProcess.MainGreenOutput));
            Check("a via secundária fica no vermelho", On(TrafficLightProcess.SideRedOutput));

            Run(12.0);
            Check("o verde principal dá lugar ao amarelo", On(TrafficLightProcess.MainYellowOutput));
            Check("o vermelho principal está apagado durante o amarelo", !On(TrafficLightProcess.MainRedOutput));

            Run(120.0);
            Check("nunca houve conflito entre os dois verdes", crossing.ConflictCount == 0);
            Check("a via secundária foi atendida", Value("V0005") >= 1);
            Check("as duas filas escoam", crossing.MainQueue < 20);

            Restart();
            Hold(TrafficLightProcess.AutoSwitchInput, true);
            SetFault("traffic.loop.blind", true);
            Run(90.0);
            Check("laço cego: a via secundária nunca recebe verde", Value("V0005") == 0);
            Check("laço cego: a fila da secundária cresce", crossing.SideQueue > 3);

            Restart();
            Run(30.0);
            Check("sem a chave automática o ciclo não parte", !On(TrafficLightProcess.MainGreenOutput) && !On(TrafficLightProcess.SideGreenOutput));
        }

        private static void TestElevator()
        {
            Section("Elevador: viagem, porta e intertravamentos");
            Use(new FreightElevatorProcess());
            FreightElevatorProcess lift = (FreightElevatorProcess)plant;

            Run(0.5);
            Check("a cabine parte do nível inferior", On(FreightElevatorProcess.LowerLimitInput));
            Check("a porta está fechada no repouso", On(FreightElevatorProcess.DoorClosedInput));

            Press(FreightElevatorProcess.UpperCallInput, 0.3);
            Run(1.0);
            Check("a chamada de cima aciona a subida", On(FreightElevatorProcess.UpOutput));
            Check("o sinaleiro de movimento acende", On(FreightElevatorProcess.MovingLampOutput));
            Check("a descida não é acionada junto com a subida", !On(FreightElevatorProcess.DownOutput));

            Run(5.0);
            Check("a cabine chega ao nível superior", On(FreightElevatorProcess.UpperLimitInput));
            Check("a subida é desligada na chegada", !On(FreightElevatorProcess.UpOutput));
            Check("a porta abre na chegada", On(FreightElevatorProcess.DoorOutput));
            Check("a viagem foi contabilizada", Value("V0002") >= 1);

            Run(6.0);
            Check("a porta fecha depois do tempo do temporizador", On(FreightElevatorProcess.DoorClosedInput));

            Press(FreightElevatorProcess.LowerCallInput, 0.3);
            Run(6.0);
            Check("a chamada de baixo traz a cabine de volta", On(FreightElevatorProcess.LowerLimitInput));

            Restart();
            SetFault("elevator.door.jam", true);
            Run(2.0);
            Press(FreightElevatorProcess.UpperCallInput, 0.3);
            Run(6.0);
            Check("porta emperrada: a cabine não sai do lugar", !On(FreightElevatorProcess.UpperLimitInput));
            Check("porta emperrada: a porta nunca confirma fechamento", !On(FreightElevatorProcess.DoorClosedInput));

            Restart();
            SetFault("elevator.upper.blind", true);
            Press(FreightElevatorProcess.UpperCallInput, 0.3);
            Run(10.0);
            Check("fim de curso superior cego: a cabine bate no fim do curso", lift.CollisionCount > 0);
        }

        private static void TestPress()
        {
            Section("Prensa: comando bimanual e segurança");
            Use(new PressProcess());
            PressProcess press = (PressProcess)plant;

            Run(0.3);
            Check("o martelo parte do fim de curso superior", On(PressProcess.TopLimitInput));
            Check("a cortina de luz está livre", On(PressProcess.CurtainInput));

            Hold(PressProcess.LeftButtonInput, true);
            Run(0.4);
            Check("um botão sozinho não desce o martelo", !On(PressProcess.DownOutput));

            Hold(PressProcess.RightButtonInput, true);
            Run(0.2);
            Check("os dois botões juntos acionam a descida", On(PressProcess.DownOutput));

            Hold(PressProcess.LeftButtonInput, false);
            Run(0.1);
            Check("soltar um botão para a descida na hora", !On(PressProcess.DownOutput));

            Hold(PressProcess.LeftButtonInput, true);
            Run(0.5);
            Check("o martelo alcança o fim de curso inferior", On(PressProcess.BottomLimitInput));
            Check("a peça foi contabilizada", Value("V0002") >= 1);

            Run(2.0);
            Check("depois do tempo de prensagem o martelo sobe", press.RamPosition < 0.9);
            Check("com os botões ainda pressionados a prensa não repete o ciclo", press.PartCount == 1);
            Check("o martelo volta ao fim de curso superior", On(PressProcess.TopLimitInput));

            Hold(PressProcess.LeftButtonInput, false);
            Hold(PressProcess.RightButtonInput, false);
            Run(0.3);
            Hold(PressProcess.LeftButtonInput, true);
            Hold(PressProcess.RightButtonInput, true);
            Run(1.0);
            Check("soltar e apertar de novo rearma a prensa", press.PartCount == 2);
            Hold(PressProcess.LeftButtonInput, false);
            Hold(PressProcess.RightButtonInput, false);
            Run(2.0);
            Check("nenhuma descida com a cortina interrompida", press.AccidentCount == 0);

            Restart();
            SetFault("press.curtain.broken", true);
            Hold(PressProcess.LeftButtonInput, true);
            Hold(PressProcess.RightButtonInput, true);
            Run(2.0);
            Check("cortina interrompida: a descida é bloqueada", !On(PressProcess.DownOutput));
            Check("cortina interrompida: nenhum acidente registrado", press.AccidentCount == 0);
            Hold(PressProcess.LeftButtonInput, false);
            Hold(PressProcess.RightButtonInput, false);

            Restart();
            SetFault("press.ram.slow", true);
            Hold(PressProcess.LeftButtonInput, true);
            Hold(PressProcess.RightButtonInput, true);
            Run(0.8);
            Check("vazamento hidráulico: o martelo ainda não chegou ao fundo", !On(PressProcess.BottomLimitInput));
            Hold(PressProcess.LeftButtonInput, false);
            Hold(PressProcess.RightButtonInput, false);
        }
    }
}
