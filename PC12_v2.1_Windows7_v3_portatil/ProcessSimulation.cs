using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModernPC12
{
    /// <summary>
    /// Atraso de primeira ordem. Usado para representar o tempo de resposta de sensores
    /// e de grandezas que não mudam em degrau.
    /// </summary>
    internal sealed class FirstOrderLag
    {
        private readonly double tau;
        private double value;

        public FirstOrderLag(double timeConstantSeconds)
        {
            tau = timeConstantSeconds <= 0.0 ? 0.001 : timeConstantSeconds;
        }

        public double Value { get { return value; } }

        public void Reset(double initial)
        {
            value = initial;
        }

        public double Update(double input, double dt)
        {
            if (dt <= 0.0) return value;
            double alpha = dt / (tau + dt);
            value += alpha * (input - value);
            return value;
        }
    }

    /// <summary>
    /// Limitador de taxa. Representa rampa de motor, curso de válvula e curso de pistão,
    /// que nunca alcançam o valor comandado em degrau.
    /// </summary>
    internal sealed class RateLimiter
    {
        private double value;

        public double Value { get { return value; } }

        public void Reset(double initial)
        {
            value = initial;
        }

        public double Update(double target, double riseRate, double fallRate, double dt)
        {
            if (dt <= 0.0) return value;
            if (target > value) value = Math.Min(target, value + (riseRate * dt));
            else if (target < value) value = Math.Max(target, value - (fallRate * dt));
            return value;
        }
    }

    /// <summary>
    /// Comparador com histerese. Evita que ruído gere chaveamento no limiar.
    /// </summary>
    internal sealed class HysteresisSwitch
    {
        private readonly double onLevel;
        private readonly double offLevel;
        private bool state;

        public HysteresisSwitch(double onLevel, double offLevel)
        {
            this.onLevel = onLevel;
            this.offLevel = offLevel;
        }

        public bool State { get { return state; } }

        public void Reset(bool initial)
        {
            state = initial;
        }

        public bool Update(double input)
        {
            if (state) { if (input <= offLevel) state = false; }
            else { if (input >= onLevel) state = true; }
            return state;
        }
    }

    internal enum SimIoDirection
    {
        PlcInput,
        PlcOutput
    }

    internal sealed class SimulatedIoPoint
    {
        public string Address;
        public string Name;
        public SimIoDirection Direction;

        /// <summary>Verdadeiro quando o valor é escrito pela planta; falso para botoeira de campo/operador.</summary>
        public bool DrivenByProcess;

        public SimulatedIoPoint(string address, string name, SimIoDirection direction, bool drivenByProcess)
        {
            Address = address;
            Name = name;
            Direction = direction;
            DrivenByProcess = drivenByProcess;
        }
    }

    internal sealed class SimulatedFault
    {
        public string Id;
        public string Name;
        public string Description;
        public bool Active;

        public SimulatedFault(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Contrato de planta virtual. A planta lê as saídas do PLC e escreve as entradas,
    /// em um relógio próprio, independente do tempo de varredura.
    /// </summary>
    internal interface ISimulatedProcess
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        IList<SimulatedIoPoint> Points { get; }
        IList<SimulatedFault> Faults { get; }

        void Reset();
        void Step(double dtSeconds, PlcProcessImage image);
        string StateSummary();
    }

    internal sealed class ConveyorBox
    {
        public int Id;
        public double Position;
    }

    /// <summary>
    /// Esteira transportadora com alimentador, dois sensores fotoelétricos, desviador pneumático
    /// e proteção térmica do motor.
    ///
    /// O realismo vem das imperfeições e não da equação ideal: rampa de aceleração do motor,
    /// tempo de curso do pistão, atraso de resposta dos sensores, histerese de comparação,
    /// jitter no intervalo de alimentação e falhas injetáveis.
    /// </summary>
    internal sealed class ConveyorProcess : ISimulatedProcess
    {
        public const string MotorOutput = "Y0001";
        public const string PusherOutput = "Y0002";
        public const string LampOutput = "Y0003";
        public const string EntrySensorInput = "X0001";
        public const string ExitSensorInput = "X0002";
        public const string PusherFeedbackInput = "X0003";
        public const string StartInput = "X0004";
        public const string StopInput = "X0005";
        public const string OverloadInput = "X0006";

        public const double BeltLength = 2.0;
        public const double BoxLength = 0.20;
        public const double EntrySensorPosition = 0.20;
        public const double ExitSensorPosition = 1.70;
        public const double PusherPosition = 1.78;

        /// <summary>Largura da placa do desviador. Define a janela física de captura da caixa.</summary>
        public const double PusherPlateWidth = 0.30;

        private const double NominalSpeed = 0.30;
        private const double Acceleration = 0.60;
        private const double Braking = 0.90;
        private const double PusherExtendRate = 1.0 / 0.35;
        private const double PusherRetractRate = 1.0 / 0.45;
        private const double SlipFactor = 0.35;
        private const double OverloadDelaySeconds = 8.0;
        private const double FeedIntervalSeconds = 4.0;

        private readonly List<SimulatedIoPoint> points = new List<SimulatedIoPoint>();
        private readonly List<SimulatedFault> faults = new List<SimulatedFault>();
        private readonly List<ConveyorBox> boxes = new List<ConveyorBox>();

        private readonly SimulatedFault slipFault = new SimulatedFault("belt.slip", "Esteira patinando", "Reduz a velocidade da correia e leva o motor à sobrecarga térmica.");
        private readonly SimulatedFault exitSensorFault = new SimulatedFault("sensor.exit.stuck", "Sensor de saída travado", "O sensor X0002 congela no último valor lido.");
        private readonly SimulatedFault pusherFault = new SimulatedFault("pusher.jam", "Desviador emperrado", "O pistão não completa o curso e o fim de curso X0003 nunca é atingido.");

        private readonly RateLimiter speed = new RateLimiter();
        private readonly RateLimiter pusherStroke = new RateLimiter();
        private readonly FirstOrderLag entrySensorLag = new FirstOrderLag(0.025);
        private readonly FirstOrderLag exitSensorLag = new FirstOrderLag(0.025);
        private readonly HysteresisSwitch entrySensorSwitch = new HysteresisSwitch(0.60, 0.40);
        private readonly HysteresisSwitch exitSensorSwitch = new HysteresisSwitch(0.60, 0.40);
        private readonly HysteresisSwitch pusherFeedbackSwitch = new HysteresisSwitch(0.97, 0.90);
        private readonly Random jitter = new Random(20260905);

        private SimBitRef motorBit;
        private SimBitRef pusherBit;
        private SimBitRef entryBit;
        private SimBitRef exitBit;
        private SimBitRef feedbackBit;
        private SimBitRef overloadBit;

        private double feedTimer;
        private double nextFeedInterval = FeedIntervalSeconds;
        private double overloadTimer;
        private double runningSeconds;
        private bool overloadTripped;
        private bool exitSensorHeld;
        private int nextBoxId = 1;
        private int divertedCount;
        private int lostCount;

        public ConveyorProcess()
        {
            points.Add(new SimulatedIoPoint(MotorOutput, "Motor da esteira", SimIoDirection.PlcOutput, true));
            points.Add(new SimulatedIoPoint(PusherOutput, "Desviador pneumático", SimIoDirection.PlcOutput, true));
            points.Add(new SimulatedIoPoint(LampOutput, "Sinaleiro de marcha", SimIoDirection.PlcOutput, true));
            points.Add(new SimulatedIoPoint(EntrySensorInput, "Sensor de entrada S1", SimIoDirection.PlcInput, true));
            points.Add(new SimulatedIoPoint(ExitSensorInput, "Sensor de saída S2", SimIoDirection.PlcInput, true));
            points.Add(new SimulatedIoPoint(PusherFeedbackInput, "Fim de curso do desviador", SimIoDirection.PlcInput, true));
            points.Add(new SimulatedIoPoint(StartInput, "Botoeira liga", SimIoDirection.PlcInput, false));
            points.Add(new SimulatedIoPoint(StopInput, "Botoeira para", SimIoDirection.PlcInput, false));
            points.Add(new SimulatedIoPoint(OverloadInput, "Relé térmico do motor", SimIoDirection.PlcInput, true));

            faults.Add(slipFault);
            faults.Add(exitSensorFault);
            faults.Add(pusherFault);

            SimAddress.TryParseBit(MotorOutput, out motorBit);
            SimAddress.TryParseBit(PusherOutput, out pusherBit);
            SimAddress.TryParseBit(EntrySensorInput, out entryBit);
            SimAddress.TryParseBit(ExitSensorInput, out exitBit);
            SimAddress.TryParseBit(PusherFeedbackInput, out feedbackBit);
            SimAddress.TryParseBit(OverloadInput, out overloadBit);

            Reset();
        }

        public string Id { get { return "conveyor.diverter"; } }
        public string DisplayName { get { return "Esteira com desviador"; } }

        public string Description
        {
            get
            {
                return "Esteira de " + BeltLength.ToString("0.0", CultureInfo.InvariantCulture) +
                       " m com alimentador, sensores fotoelétricos na entrada e na saída, desviador pneumático com fim de curso e proteção térmica.";
            }
        }

        public IList<SimulatedIoPoint> Points { get { return points.AsReadOnly(); } }
        public IList<SimulatedFault> Faults { get { return faults.AsReadOnly(); } }
        public IList<ConveyorBox> Boxes { get { return boxes.AsReadOnly(); } }

        public double BeltSpeed { get { return speed.Value; } }
        public double PusherStroke { get { return pusherStroke.Value; } }
        public bool OverloadTripped { get { return overloadTripped; } }
        public int DivertedCount { get { return divertedCount; } }
        public int LostCount { get { return lostCount; } }
        public double RunningSeconds { get { return runningSeconds; } }

        public void Reset()
        {
            boxes.Clear();
            speed.Reset(0.0);
            pusherStroke.Reset(0.0);
            entrySensorLag.Reset(0.0);
            exitSensorLag.Reset(0.0);
            entrySensorSwitch.Reset(false);
            exitSensorSwitch.Reset(false);
            pusherFeedbackSwitch.Reset(false);

            feedTimer = 0.0;
            nextFeedInterval = FeedIntervalSeconds;
            overloadTimer = 0.0;
            runningSeconds = 0.0;
            overloadTripped = false;
            exitSensorHeld = false;
            nextBoxId = 1;
            divertedCount = 0;
            lostCount = 0;
        }

        public void Step(double dt, PlcProcessImage image)
        {
            if (dt <= 0.0 || image == null) return;

            bool motorCommand = image.GetBit(motorBit);
            bool pusherCommand = image.GetBit(pusherBit);

            UpdateOverload(motorCommand, dt);
            UpdateSpeed(motorCommand, dt);
            MoveBoxes(dt);
            UpdatePusher(pusherCommand, dt);
            FeedBoxes(dt);

            image.SetBit(entryBit, ReadEntrySensor(dt));
            image.SetBit(exitBit, ReadExitSensor(dt));
            image.SetBit(feedbackBit, pusherFeedbackSwitch.Update(pusherStroke.Value));
            image.SetBit(overloadBit, overloadTripped);
        }

        private void UpdateOverload(bool motorCommand, double dt)
        {
            if (motorCommand && slipFault.Active && !overloadTripped)
            {
                overloadTimer += dt;
                if (overloadTimer >= OverloadDelaySeconds) overloadTripped = true;
            }
            else if (!motorCommand)
            {
                overloadTimer = Math.Max(0.0, overloadTimer - (dt * 0.5));
            }
        }

        private void UpdateSpeed(bool motorCommand, double dt)
        {
            bool energised = motorCommand && !overloadTripped;
            double target = energised ? NominalSpeed * (slipFault.Active ? SlipFactor : 1.0) : 0.0;
            speed.Update(target, Acceleration, Braking, dt);
            if (speed.Value > 0.001) runningSeconds += dt;
        }

        private void UpdatePusher(bool pusherCommand, double dt)
        {
            double target = pusherCommand ? 1.0 : 0.0;
            if (pusherFault.Active && target > 0.55) target = 0.55;
            pusherStroke.Update(target, PusherExtendRate, PusherRetractRate, dt);

            // A placa varre a esteira: qualquer caixa dentro da janela física sai enquanto o curso está avançado.
            if (pusherStroke.Value >= 0.60) DivertBoxes();
        }

        private void DivertBoxes()
        {
            double window = (BoxLength + PusherPlateWidth) / 2.0;
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                if (Math.Abs(boxes[i].Position - PusherPosition) <= window)
                {
                    boxes.RemoveAt(i);
                    divertedCount++;
                }
            }
        }

        private void MoveBoxes(double dt)
        {
            double step = speed.Value * dt;
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                boxes[i].Position += step;
                if (boxes[i].Position > BeltLength)
                {
                    boxes.RemoveAt(i);
                    lostCount++;
                }
            }
        }

        private void FeedBoxes(double dt)
        {
            feedTimer += dt;
            if (feedTimer < nextFeedInterval) return;

            feedTimer = 0.0;
            nextFeedInterval = FeedIntervalSeconds * (0.90 + (jitter.NextDouble() * 0.20));

            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i].Position < BoxLength) return;

            ConveyorBox box = new ConveyorBox();
            box.Id = nextBoxId++;
            box.Position = 0.0;
            boxes.Add(box);
        }

        private bool ReadEntrySensor(double dt)
        {
            double raw = Covers(EntrySensorPosition) ? 1.0 : 0.0;
            return entrySensorSwitch.Update(entrySensorLag.Update(raw, dt));
        }

        private bool ReadExitSensor(double dt)
        {
            if (exitSensorFault.Active) return exitSensorHeld;

            double raw = Covers(ExitSensorPosition) ? 1.0 : 0.0;
            exitSensorHeld = exitSensorSwitch.Update(exitSensorLag.Update(raw, dt));
            return exitSensorHeld;
        }

        private bool Covers(double sensorPosition)
        {
            for (int i = 0; i < boxes.Count; i++)
                if (Math.Abs(boxes[i].Position - sensorPosition) <= (BoxLength / 2.0)) return true;
            return false;
        }

        public string StateSummary()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Velocidade da correia: ").Append(speed.Value.ToString("0.000", CultureInfo.InvariantCulture)).Append(" m/s\r\n");
            text.Append("Curso do desviador: ").Append((pusherStroke.Value * 100.0).ToString("0", CultureInfo.InvariantCulture)).Append(" %\r\n");
            text.Append("Caixas na esteira: ").Append(boxes.Count.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Caixas desviadas: ").Append(divertedCount.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Caixas perdidas no fim da esteira: ").Append(lostCount.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Tempo de correia em movimento: ").Append(runningSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s\r\n");
            text.Append("Relé térmico: ").Append(overloadTripped ? "atuado" : "normal");
            return text.ToString();
        }
    }

    /// <summary>
    /// Programas de exemplo do simulador. São montados diretamente no modelo Ladder universal
    /// e usam apenas elementos que o editor já sabe inserir.
    /// </summary>
    internal static class SimulationSamples
    {
        public static UniversalLadderProgram BuildConveyorProgram()
        {
            UniversalLadderProgram program = new UniversalLadderProgram();
            program.Name = "Esteira com desviador (exemplo)";

            UniversalLadderRung start = new UniversalLadderRung();
            start.Series.Add(Contact(0, false, ConveyorProcess.StartInput));
            start.Parallel.Add(Contact(0, false, "C0001"));
            start.Series.Add(Contact(1, true, ConveyorProcess.StopInput));
            start.Series.Add(Contact(2, true, ConveyorProcess.OverloadInput));
            start.Series.Add(Coil("C0001"));
            program.Rungs.Add(start);

            UniversalLadderRung motor = new UniversalLadderRung();
            motor.Series.Add(Contact(0, false, "C0001"));
            motor.Series.Add(Contact(1, true, ConveyorProcess.PusherOutput));
            motor.Series.Add(Coil(ConveyorProcess.MotorOutput));
            program.Rungs.Add(motor);

            UniversalLadderRung arm = new UniversalLadderRung();
            arm.Series.Add(Contact(0, false, ConveyorProcess.ExitSensorInput));
            arm.Series.Add(Contact(1, false, "C0001"));
            arm.Series.Add(Contact(2, true, ConveyorProcess.PusherFeedbackInput));
            arm.Series.Add(Output(UniversalElementKind.Set, ConveyorProcess.PusherOutput, string.Empty, string.Empty));
            program.Rungs.Add(arm);

            UniversalLadderRung retract = new UniversalLadderRung();
            retract.Series.Add(Contact(0, false, ConveyorProcess.PusherFeedbackInput));
            retract.Series.Add(Output(UniversalElementKind.Reset, ConveyorProcess.PusherOutput, string.Empty, string.Empty));
            program.Rungs.Add(retract);

            UniversalLadderRung counter = new UniversalLadderRung();
            counter.Series.Add(Contact(0, false, ConveyorProcess.PusherFeedbackInput));
            counter.Series.Add(Output(UniversalElementKind.Counter, "V0002", "999", string.Empty));
            program.Rungs.Add(counter);

            UniversalLadderRung lamp = new UniversalLadderRung();
            lamp.Series.Add(Contact(0, false, "C0001"));
            lamp.Series.Add(Contact(1, false, "SC004"));
            lamp.Series.Add(Coil(ConveyorProcess.LampOutput));
            program.Rungs.Add(lamp);

            UniversalLadderRung hourMeter = new UniversalLadderRung();
            hourMeter.Series.Add(Contact(0, false, "C0001"));
            hourMeter.Series.Add(Output(UniversalElementKind.Timer, "V0001", "600", "RESET"));
            program.Rungs.Add(hourMeter);

            UniversalLadderRung end = new UniversalLadderRung();
            end.Series.Add(Output(UniversalElementKind.End, string.Empty, string.Empty, string.Empty));
            program.Rungs.Add(end);

            return program;
        }

        public static string DescribeConveyorProgram()
        {
            StringBuilder text = new StringBuilder();
            text.Append("1  X0004 ou C0001, com X0005 e X0006 normalmente fechados, selam C0001 (marcha).\r\n");
            text.Append("2  C0001 com Y0002 normalmente fechado aciona Y0001 (motor da esteira).\r\n");
            text.Append("3  X0002 e C0001, com X0003 normalmente fechado, dão SET em Y0002 (avança o desviador).\r\n");
            text.Append("4  X0003 dá RESET em Y0002 (recolhe o desviador no fim de curso).\r\n");
            text.Append("5  X0003 incrementa o contador V0002 (caixas desviadas).\r\n");
            text.Append("6  C0001 com SC004 pisca Y0003 (sinaleiro de marcha a 1 Hz).\r\n");
            text.Append("7  C0001 alimenta o temporizador retentivo V0001 (horímetro de marcha).\r\n");
            text.Append("8  END.");
            return text.ToString();
        }

        private static UniversalLadderElement Contact(int column, bool normallyClosed, string address)
        {
            UniversalLadderElement element = new UniversalLadderElement();
            element.Kind = normallyClosed ? UniversalElementKind.ContactNC : UniversalElementKind.ContactNO;
            element.Address = address;
            element.Column = column;
            return element;
        }

        private static UniversalLadderElement Coil(string address)
        {
            return Output(UniversalElementKind.Coil, address, string.Empty, string.Empty);
        }

        private static UniversalLadderElement Output(UniversalElementKind kind, string address, string parameter, string functionCode)
        {
            UniversalLadderElement element = new UniversalLadderElement();
            element.Kind = kind;
            element.Address = address;
            element.Parameter = parameter;
            element.FunctionCode = functionCode;
            element.Column = LadderScanEngine.ConditionColumns;
            return element;
        }
    }
}
