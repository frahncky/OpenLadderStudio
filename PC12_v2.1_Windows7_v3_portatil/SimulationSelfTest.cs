using System;
using System.Globalization;

namespace ModernPC12
{
    /// <summary>
    /// Verificação automática do motor de varredura e da planta virtual.
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
        private static ConveyorProcess plant;

        private static SimBitRef start;
        private static SimBitRef stop;
        private static SimBitRef motor;
        private static SimBitRef pusher;
        private static SimBitRef lamp;
        private static SimBitRef latch;
        private static SimBitRef feedback;
        private static SimBitRef overload;
        private static SimBitRef counter;
        private static SimBitRef timer;

        private static int Main()
        {
            // StudioDiagnostics não é instalado aqui de propósito: ele exibe caixa de
            // diálogo em falha, o que travaria a execução do autoteste no CI.
            engine = new LadderScanEngine();
            plant = new ConveyorProcess();

            SimAddress.TryParseBit(ConveyorProcess.StartInput, out start);
            SimAddress.TryParseBit(ConveyorProcess.StopInput, out stop);
            SimAddress.TryParseBit(ConveyorProcess.MotorOutput, out motor);
            SimAddress.TryParseBit(ConveyorProcess.PusherOutput, out pusher);
            SimAddress.TryParseBit(ConveyorProcess.LampOutput, out lamp);
            SimAddress.TryParseBit(ConveyorProcess.PusherFeedbackInput, out feedback);
            SimAddress.TryParseBit(ConveyorProcess.OverloadInput, out overload);
            SimAddress.TryParseBit("C0001", out latch);
            SimAddress.TryParseBit("V0002", out counter);
            SimAddress.TryParseBit("V0001", out timer);

            Console.WriteLine("Autoteste da simulação do OpenLadder Studio");
            Console.WriteLine();

            TestAddressParsing();
            TestProgramLoad();
            TestLatchAndStop();
            TestConveyorCycle();
            TestClockContact();
            TestRetentiveTimer();
            TestForcing();
            TestPusherJam();
            TestSlipOverload();

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

        private static void Press(SimBitRef button, double seconds)
        {
            engine.Field.Set(button, true);
            Run(seconds);
            engine.Field.Set(button, false);
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
            Check("C2048 está dentro do limite", SimAddress.TryParseBit("C2048", out reference) && reference.Index == 2048);
            Check("C2049 é recusado", !SimAddress.TryParseBit("C2049", out reference));
            Check("X0385 é recusado", !SimAddress.TryParseBit("X0385", out reference));
            Check("X0000 é recusado", !SimAddress.TryParseBit("X0000", out reference));
            Check("prefixo desconhecido é recusado", !SimAddress.TryParseBit("Z0001", out reference));
            Check("D2048 é um registrador válido", SimAddress.TryParseWord("D2048", out word) && word == 2048);
            Check("D2049 é recusado", !SimAddress.TryParseWord("D2049", out word));
        }

        private static void TestProgramLoad()
        {
            Section("Carga do programa de exemplo");
            engine.Load(SimulationSamples.BuildConveyorProgram());

            Check("programa com 8 rungs", engine.RungCount == 8);
            Check("carga sem avisos de compilação", engine.Diagnostics.Count == 0);

            for (int i = 0; i < engine.Diagnostics.Count; i++)
                Console.WriteLine("        aviso: " + engine.Diagnostics[i]);
        }

        private static void TestLatchAndStop()
        {
            Section("Partida com selo e parada");
            Restart();
            Run(0.2);
            Check("máquina parada antes de qualquer comando", !engine.Image.GetBit(latch));

            Press(start, 0.3);
            Run(0.2);
            Check("o selo mantém C0001 depois de soltar a botoeira", engine.Image.GetBit(latch));
            Check("motor energizado com a marcha", engine.Image.GetBit(motor));
            Check("correia acelerou", plant.BeltSpeed > 0.05);

            Press(stop, 0.2);
            Run(0.2);
            Check("a botoeira de parada derruba o selo", !engine.Image.GetBit(latch));
            Check("motor desligado depois da parada", !engine.Image.GetBit(motor));

            Run(2.0);
            Check("correia freou até parar", plant.BeltSpeed < 0.001);
        }

        private static void TestConveyorCycle()
        {
            Section("Ciclo da esteira com desviador");
            Restart();
            Press(start, 0.2);
            Run(18.0);

            Check("caixas foram desviadas", plant.DivertedCount >= 2);
            Check("nenhuma caixa passou direto pelo fim da esteira", plant.LostCount == 0);
            Check("o contador V0002 acompanha as caixas desviadas", engine.Image.GetVariableValue(counter.Index) == plant.DivertedCount);

            bool returnedHome = false;
            for (int i = 0; i < 300; i++)
            {
                plant.Step(StepSeconds, engine.Image);
                engine.Execute(StepMs);
                if (plant.PusherStroke < 0.05 && !engine.Image.GetBit(pusher)) returnedHome = true;
            }
            Check("o desviador volta ao repouso entre ciclos", returnedHome);
        }

        private static void TestClockContact()
        {
            Section("Contato especial de pulso");
            Restart();
            Press(start, 0.2);

            int changes = 0;
            bool last = engine.Image.GetBit(lamp);
            for (int i = 0; i < 300; i++)
            {
                plant.Step(StepSeconds, engine.Image);
                engine.Execute(StepMs);
                bool now = engine.Image.GetBit(lamp);
                if (now != last) changes++;
                last = now;
            }

            Check("o sinaleiro pisca com SC004 (" + changes.ToString(CultureInfo.InvariantCulture) + " transições em 3 s)", changes >= 4 && changes <= 8);
        }

        private static void TestRetentiveTimer()
        {
            Section("Temporizador retentivo");
            Restart();
            Press(start, 0.2);
            Run(2.0);

            int afterRun = engine.Image.GetVariableValue(timer.Index);
            Check("o horímetro contou cerca de 2,2 s (" + afterRun.ToString(CultureInfo.InvariantCulture) + " décimos)", afterRun >= 20 && afterRun <= 24);

            Press(stop, 0.2);
            Run(2.0);
            int afterStop = engine.Image.GetVariableValue(timer.Index);
            Check("o acumulado é preservado com o rung desligado", afterStop == afterRun);

            Press(start, 0.2);
            Run(1.0);
            Check("a contagem é retomada na religação", engine.Image.GetVariableValue(timer.Index) > afterStop);
        }

        private static void TestForcing()
        {
            Section("Forçamento");
            Restart();
            Press(start, 0.2);
            Run(0.5);
            Check("motor ligado antes do forçamento", engine.Image.GetBit(motor));

            engine.Forces.Force(stop, true);
            Run(0.2);
            Check("forçar a botoeira de parada derruba o selo", !engine.Image.GetBit(latch));
            Check("o forçamento aparece na tabela", engine.Forces.Count == 1);

            engine.Forces.Release(stop);
            Press(start, 0.2);
            Run(0.2);
            Check("liberado o forçamento, a entrada volta ao valor de campo", engine.Image.GetBit(latch));

            engine.Forces.Force(lamp, true);
            Run(0.2);
            Check("saída forçada sobrescreve a lógica depois da varredura", engine.Image.GetBit(lamp));
            engine.Forces.ReleaseAll();
        }

        private static void TestPusherJam()
        {
            Section("Falha: desviador emperrado");
            Restart();
            Press(start, 0.2);
            SetFault("pusher.jam", true);
            Run(20.0);

            Check("o fim de curso nunca é atingido", !engine.Image.GetBit(feedback));
            Check("o desviador permanece comandado, porque o RESET depende do fim de curso", engine.Image.GetBit(pusher));
            Check("o intertravamento do programa desliga o motor", !engine.Image.GetBit(motor));
        }

        private static void TestSlipOverload()
        {
            Section("Falha: esteira patinando e sobrecarga térmica");
            Restart();
            Press(start, 0.2);
            SetFault("belt.slip", true);
            Run(4.0);

            Check("o relé térmico ainda não atuou antes do atraso", !engine.Image.GetBit(overload));
            Check("a correia patinando roda mais devagar", plant.BeltSpeed > 0.0 && plant.BeltSpeed < 0.15);

            Run(6.0);
            Check("o relé térmico atua depois da sobrecarga prolongada", engine.Image.GetBit(overload));
            Check("o contato NF do térmico derruba a marcha", !engine.Image.GetBit(latch));
            Check("motor desligado pela proteção", !engine.Image.GetBit(motor));
        }
    }
}
