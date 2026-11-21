using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModernPC12
{
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
    internal sealed class ConveyorProcess : SimulatedProcessBase
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
        public const double PusherPlateWidth = 0.30;

        private const double NominalSpeed = 0.30;
        private const double Acceleration = 0.60;
        private const double Braking = 0.90;
        private const double PusherExtendRate = 1.0 / 0.35;
        private const double PusherRetractRate = 1.0 / 0.45;
        private const double SlipFactor = 0.35;
        private const double OverloadDelaySeconds = 8.0;
        private const double FeedIntervalSeconds = 4.0;

        private readonly List<ConveyorBox> boxes = new List<ConveyorBox>();

        private readonly SimBitRef motorBit;
        private readonly SimBitRef pusherBit;
        private readonly SimBitRef lampBit;
        private readonly SimBitRef entryBit;
        private readonly SimBitRef exitBit;
        private readonly SimBitRef feedbackBit;
        private readonly SimBitRef overloadBit;

        private readonly SimulatedFault slipFault;
        private readonly SimulatedFault exitSensorFault;
        private readonly SimulatedFault pusherFault;

        private readonly RateLimiter speed = new RateLimiter();
        private readonly RateLimiter pusherStroke = new RateLimiter();
        private readonly FirstOrderLag entrySensorLag = new FirstOrderLag(0.025);
        private readonly FirstOrderLag exitSensorLag = new FirstOrderLag(0.025);
        private readonly HysteresisSwitch entrySensorSwitch = new HysteresisSwitch(0.60, 0.40);
        private readonly HysteresisSwitch exitSensorSwitch = new HysteresisSwitch(0.60, 0.40);
        private readonly HysteresisSwitch pusherFeedbackSwitch = new HysteresisSwitch(0.97, 0.90);
        private readonly Random jitter = new Random(20260905);

        private double feedTimer;
        private double nextFeedInterval = FeedIntervalSeconds;
        private double overloadTimer;
        private double runningSeconds;
        private double beltPhase;
        private bool overloadTripped;
        private bool exitSensorHeld;
        private int nextBoxId = 1;
        private int divertedCount;
        private int lostCount;

        public ConveyorProcess()
        {
            motorBit = Output(MotorOutput, "Motor da esteira");
            pusherBit = Output(PusherOutput, "Desviador pneumático");
            lampBit = Output(LampOutput, "Sinaleiro de marcha");
            entryBit = Sensor(EntrySensorInput, "Sensor de entrada S1");
            exitBit = Sensor(ExitSensorInput, "Sensor de saída S2");
            feedbackBit = Sensor(PusherFeedbackInput, "Fim de curso do desviador");
            Button(StartInput, "Botoeira liga");
            Button(StopInput, "Botoeira para");
            overloadBit = Sensor(OverloadInput, "Relé térmico do motor");

            slipFault = Fault("belt.slip", "Esteira patinando", "Reduz a velocidade da correia e leva o motor à sobrecarga térmica.");
            exitSensorFault = Fault("sensor.exit.stuck", "Sensor de saída travado", "O sensor X0002 congela no último valor lido.");
            pusherFault = Fault("pusher.jam", "Desviador emperrado", "O pistão não completa o curso e o fim de curso X0003 nunca é atingido.");

            Reset();
        }

        public override string Id { get { return "conveyor.diverter"; } }
        public override string DisplayName { get { return "Esteira com desviador"; } }

        public override string Description
        {
            get
            {
                return "Esteira de " + BeltLength.ToString("0.0", CultureInfo.InvariantCulture) +
                       " m com alimentador, sensores fotoelétricos na entrada e na saída, desviador pneumático com fim de curso e proteção térmica.";
            }
        }

        public IList<ConveyorBox> Boxes { get { return boxes.AsReadOnly(); } }
        public double BeltSpeed { get { return speed.Value; } }
        public double PusherStroke { get { return pusherStroke.Value; } }
        public bool OverloadTripped { get { return overloadTripped; } }
        public int DivertedCount { get { return divertedCount; } }
        public int LostCount { get { return lostCount; } }

        public override void Reset()
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
            beltPhase = 0.0;
            overloadTripped = false;
            exitSensorHeld = false;
            nextBoxId = 1;
            divertedCount = 0;
            lostCount = 0;
        }

        public override void Step(double dt, PlcProcessImage image)
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
            beltPhase += speed.Value * dt * 120.0;
            if (beltPhase > 100000.0) beltPhase = 0.0;
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

        public override string StateSummary()
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

        public override void BuildScene(SimScene scene, PlcProcessImage image)
        {
            double left = 90.0;
            double right = 930.0;
            double beltY = 236.0;
            double beltHeight = 14.0;
            double scale = (right - left) / BeltLength;

            scene.Line(left - 24.0, beltY + beltHeight + 26.0, right + 24.0, beltY + beltHeight + 26.0, SimTone.Structure);
            scene.Belt(left, beltY, right - left, beltHeight, beltPhase);
            scene.Ellipse(left - 14.0, beltY - 2.0, 18.0, 18.0, SimTone.Structure, SimTone.Structure);
            scene.Ellipse(right - 4.0, beltY - 2.0, 18.0, 18.0, SimTone.Structure, SimTone.Structure);

            DrawSensor(scene, left, beltY, scale, EntrySensorPosition, "S1  " + EntrySensorInput, image.GetBit(entryBit));
            DrawSensor(scene, left, beltY, scale, ExitSensorPosition, "S2  " + ExitSensorInput, image.GetBit(exitBit));

            double boxWidth = BoxLength * scale;
            for (int i = 0; i < boxes.Count; i++)
            {
                double centre = left + (boxes[i].Position * scale);
                scene.Rect(centre - (boxWidth / 2.0), beltY - 26.0, boxWidth, 26.0, SimTone.Cargo, SimTone.Cargo);
            }

            double pusherX = left + (PusherPosition * scale);
            double plateWidth = PusherPlateWidth * scale;
            double home = beltY - 104.0;
            double travel = 74.0;
            double plateY = home + (pusherStroke.Value * travel);
            scene.Line(pusherX, home - 12.0, pusherX, plateY, SimTone.Structure);
            scene.Rect(pusherX - (plateWidth / 2.0), plateY, plateWidth, 12.0,
                image.GetBit(pusherBit) ? SimTone.Info : SimTone.Structure, SimTone.Structure);
            scene.Label(pusherX, home - 30.0, "Desviador  " + PusherOutput, SimTone.Muted, SimTextAlign.Center, false);
            scene.Lamp(pusherX + (plateWidth / 2.0) + 14.0, home + travel, 12.0, image.GetBit(feedbackBit), SimTone.Active);
            scene.Label(pusherX + (plateWidth / 2.0) + 32.0, home + travel - 6.0, PusherFeedbackInput, SimTone.Muted);
            scene.Label(pusherX, beltY + 42.0, "desviadas: " + divertedCount.ToString(CultureInfo.InvariantCulture), SimTone.Muted, SimTextAlign.Center, false);

            bool running = image.GetBit(motorBit);
            scene.Rect(left - 58.0, beltY - 8.0, 32.0, 32.0, running ? SimTone.Active : SimTone.Structure, SimTone.Structure, "M");
            scene.Label(left - 58.0, beltY + 30.0, MotorOutput, SimTone.Muted);

            scene.Lamp(right - 18.0, 22.0, 20.0, image.GetBit(lampBit), SimTone.Warning);
            scene.Label(right - 28.0, 24.0, "Sinaleiro  " + LampOutput, SimTone.Muted, SimTextAlign.Right, false);

            scene.Label(30.0, 22.0, "Correia " + speed.Value.ToString("0.000", CultureInfo.InvariantCulture) + " m/s   ·   curso " +
                (pusherStroke.Value * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %   ·   perdidas " +
                lostCount.ToString(CultureInfo.InvariantCulture), SimTone.Muted);

            if (overloadTripped) scene.Label(30.0, 44.0, "RELÉ TÉRMICO ATUADO", SimTone.Danger, SimTextAlign.Left, true);
        }

        private static void DrawSensor(SimScene scene, double left, double beltY, double scale, double position, string label, bool active)
        {
            double x = left + (position * scale);
            SimTone tone = active ? SimTone.Active : SimTone.Muted;
            scene.Line(x, beltY - 40.0, x, beltY, tone, true);
            scene.Rect(x - 5.0, beltY - 50.0, 10.0, 10.0, tone, tone);
            scene.Label(x, beltY + 22.0, label, active ? SimTone.Neutral : SimTone.Muted, SimTextAlign.Center, false);
        }

        public override UniversalLadderProgram BuildSampleProgram()
        {
            UniversalLadderProgram program = new UniversalLadderProgram();
            program.Name = "Esteira com desviador (exemplo)";

            program.Rungs.Add(LadderBuild.Rung().NO(0, StartInput).ParallelNO(0, "C0001").NC(1, StopInput).NC(2, OverloadInput).Out(LadderBuild.Coil("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").NC(1, PusherOutput).Out(LadderBuild.Coil(MotorOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, ExitSensorInput).NO(1, "C0001").NC(2, PusherFeedbackInput).Out(LadderBuild.Set(PusherOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, PusherFeedbackInput).Out(LadderBuild.Reset(PusherOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, PusherFeedbackInput).Out(LadderBuild.Counter("V0002", 999)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").NO(1, "SC004").Out(LadderBuild.Coil(LampOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Timer("V0001", 600, true)));
            program.Rungs.Add(LadderBuild.Rung().Out(LadderBuild.End()));
            return program;
        }

        public override string DescribeSampleProgram()
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
    }

    /// <summary>
    /// Silo com enchimento por válvula superior, descarga em caminhão e chaves de nível.
    /// O nível é um integrador com vazões distintas de entrada e saída; as chaves têm
    /// histerese, e o transbordo é contado para tornar visível uma lógica malfeita.
    /// </summary>
    internal sealed class SiloProcess : SimulatedProcessBase
    {
        public const string FillValveOutput = "Y0001";
        public const string DischargeValveOutput = "Y0002";
        public const string FullLampOutput = "Y0003";
        public const string LowLevelInput = "X0001";
        public const string HighLevelInput = "X0002";
        public const string TruckInput = "X0003";
        public const string StartInput = "X0004";
        public const string StopInput = "X0005";

        private const double FillRate = 7.0;
        private const double DischargeRate = 11.0;
        private const double ValveStrokeSeconds = 0.8;
        private const double TruckIntervalSeconds = 26.0;
        private const double TruckStaySeconds = 14.0;

        private readonly SimBitRef fillBit;
        private readonly SimBitRef dischargeBit;
        private readonly SimBitRef fullLampBit;
        private readonly SimBitRef lowBit;
        private readonly SimBitRef highBit;
        private readonly SimBitRef truckBit;

        private readonly SimulatedFault stuckValveFault;
        private readonly SimulatedFault blindHighFault;
        private readonly SimulatedFault packedFault;

        private readonly RateLimiter fillStroke = new RateLimiter();
        private readonly RateLimiter dischargeStroke = new RateLimiter();
        private readonly HysteresisSwitch lowSwitch = new HysteresisSwitch(15.0, 20.0);
        private readonly HysteresisSwitch highSwitch = new HysteresisSwitch(90.0, 85.0);
        private readonly Random jitter = new Random(20260906);

        private double level;
        private double truckTimer;
        private bool truckPresent;
        private int overflowCount;
        private int loadCount;
        private bool lastTruckPresent;

        public SiloProcess()
        {
            fillBit = Output(FillValveOutput, "Válvula de enchimento");
            dischargeBit = Output(DischargeValveOutput, "Válvula de descarga");
            fullLampBit = Output(FullLampOutput, "Sinaleiro de silo cheio");
            lowBit = Sensor(LowLevelInput, "Chave de nível baixo");
            highBit = Sensor(HighLevelInput, "Chave de nível alto");
            truckBit = Sensor(TruckInput, "Caminhão posicionado");
            Button(StartInput, "Botoeira liga");
            Button(StopInput, "Botoeira para");

            stuckValveFault = Fault("silo.fill.stuck", "Válvula de enchimento travada aberta", "A válvula não fecha e o silo continua enchendo mesmo com o comando desligado.");
            blindHighFault = Fault("silo.high.blind", "Chave de nível alto cega", "X0002 nunca atua; sem outra proteção o silo transborda.");
            packedFault = Fault("silo.packed", "Material empedrado", "A vazão de descarga cai para um quinto do normal.");

            Reset();
        }

        public override string Id { get { return "silo.loading"; } }
        public override string DisplayName { get { return "Silo com enchimento e descarga"; } }

        public override string Description
        {
            get
            {
                return "Silo com válvula de enchimento, descarga em caminhão e chaves de nível baixo e alto. " +
                       "O nível é integrado com vazões distintas e as válvulas levam tempo para abrir e fechar.";
            }
        }

        public double Level { get { return level; } }
        public int OverflowCount { get { return overflowCount; } }
        public int LoadCount { get { return loadCount; } }
        public bool TruckPresent { get { return truckPresent; } }

        public override void Reset()
        {
            level = 40.0;
            fillStroke.Reset(0.0);
            dischargeStroke.Reset(0.0);
            lowSwitch.Reset(false);
            highSwitch.Reset(false);
            truckTimer = 0.0;
            truckPresent = false;
            lastTruckPresent = false;
            overflowCount = 0;
            loadCount = 0;
        }

        public override void Step(double dt, PlcProcessImage image)
        {
            if (dt <= 0.0 || image == null) return;

            double fillTarget = image.GetBit(fillBit) ? 1.0 : 0.0;
            if (stuckValveFault.Active) fillTarget = 1.0;
            fillStroke.Update(fillTarget, 1.0 / ValveStrokeSeconds, 1.0 / ValveStrokeSeconds, dt);
            dischargeStroke.Update(image.GetBit(dischargeBit) ? 1.0 : 0.0, 1.0 / ValveStrokeSeconds, 1.0 / ValveStrokeSeconds, dt);

            UpdateTruck(dt);

            double inflow = FillRate * fillStroke.Value;
            double outflow = truckPresent ? DischargeRate * dischargeStroke.Value * (packedFault.Active ? 0.20 : 1.0) : 0.0;
            level += (inflow - outflow) * dt;

            if (level > 100.0)
            {
                level = 100.0;
                overflowCount++;
            }
            if (level < 0.0) level = 0.0;

            image.SetBit(lowBit, lowSwitch.Update(level));
            image.SetBit(highBit, blindHighFault.Active ? false : highSwitch.Update(level));
            image.SetBit(truckBit, truckPresent);
        }

        private void UpdateTruck(double dt)
        {
            truckTimer += dt;
            if (!truckPresent && truckTimer >= TruckIntervalSeconds)
            {
                truckPresent = true;
                truckTimer = 0.0;
            }
            else if (truckPresent && truckTimer >= TruckStaySeconds * (0.9 + (jitter.NextDouble() * 0.2)))
            {
                truckPresent = false;
                truckTimer = 0.0;
            }

            if (lastTruckPresent && !truckPresent) loadCount++;
            lastTruckPresent = truckPresent;
        }

        public override string StateSummary()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Nível do silo: ").Append(level.ToString("0.0", CultureInfo.InvariantCulture)).Append(" %\r\n");
            text.Append("Válvula de enchimento: ").Append((fillStroke.Value * 100.0).ToString("0", CultureInfo.InvariantCulture)).Append(" % aberta\r\n");
            text.Append("Válvula de descarga: ").Append((dischargeStroke.Value * 100.0).ToString("0", CultureInfo.InvariantCulture)).Append(" % aberta\r\n");
            text.Append("Caminhão: ").Append(truckPresent ? "posicionado" : "ausente").Append("\r\n");
            text.Append("Caminhões atendidos: ").Append(loadCount.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Passagens por transbordo: ").Append(overflowCount.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        public override void BuildScene(SimScene scene, PlcProcessImage image)
        {
            double siloX = 340.0;
            double siloY = 60.0;
            double siloW = 200.0;
            double siloH = 170.0;

            scene.Level(siloX, siloY, siloW, siloH, level / 100.0, SimTone.Cargo);
            scene.Label(siloX + (siloW / 2.0), siloY - 26.0, "SILO", SimTone.Muted, SimTextAlign.Center, true);
            scene.Label(siloX + (siloW / 2.0), siloY + (siloH / 2.0) - 8.0,
                level.ToString("0.0", CultureInfo.InvariantCulture) + " %", SimTone.Neutral, SimTextAlign.Center, true);

            // Alimentação superior.
            scene.Line(siloX + (siloW / 2.0), 18.0, siloX + (siloW / 2.0), siloY, SimTone.Structure);
            scene.Rect(siloX + (siloW / 2.0) - 22.0, 24.0, 44.0, 22.0,
                image.GetBit(fillBit) ? SimTone.Active : SimTone.Structure, SimTone.Structure, "V1");
            scene.Label(siloX + (siloW / 2.0) + 34.0, 26.0, FillValveOutput + "  " + (fillStroke.Value * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %", SimTone.Muted);

            // Chaves de nível na lateral do silo.
            DrawLevelSwitch(scene, siloX + siloW + 12.0, siloY + siloH - (siloH * 0.175), "LSL  " + LowLevelInput, image.GetBit(lowBit));
            DrawLevelSwitch(scene, siloX + siloW + 12.0, siloY + siloH - (siloH * 0.90), "LSH  " + HighLevelInput, image.GetBit(highBit));

            // Descarga e caminhão.
            scene.Line(siloX + (siloW / 2.0), siloY + siloH, siloX + (siloW / 2.0), siloY + siloH + 34.0, SimTone.Structure);
            scene.Rect(siloX + (siloW / 2.0) - 22.0, siloY + siloH + 10.0, 44.0, 22.0,
                image.GetBit(dischargeBit) ? SimTone.Active : SimTone.Structure, SimTone.Structure, "V2");
            scene.Label(siloX + (siloW / 2.0) + 34.0, siloY + siloH + 12.0, DischargeValveOutput, SimTone.Muted);

            if (truckPresent)
            {
                double truckX = siloX + (siloW / 2.0) - 90.0;
                scene.Rect(truckX, siloY + siloH + 44.0, 180.0, 34.0, SimTone.Structure, SimTone.Neutral, "CAMINHÃO");
                scene.Ellipse(truckX + 24.0, siloY + siloH + 74.0, 20.0, 20.0, SimTone.Dark, SimTone.Structure);
                scene.Ellipse(truckX + 134.0, siloY + siloH + 74.0, 20.0, 20.0, SimTone.Dark, SimTone.Structure);
            }
            else
            {
                scene.Label(siloX + (siloW / 2.0), siloY + siloH + 56.0, "aguardando caminhão", SimTone.Muted, SimTextAlign.Center, false);
            }

            scene.Lamp(900.0, 30.0, 22.0, image.GetBit(fullLampBit), SimTone.Warning);
            scene.Label(890.0, 32.0, "Silo cheio  " + FullLampOutput, SimTone.Muted, SimTextAlign.Right, false);

            scene.Label(30.0, 22.0, "Caminhões atendidos: " + loadCount.ToString(CultureInfo.InvariantCulture), SimTone.Muted);
            if (overflowCount > 0) scene.Label(30.0, 44.0, "TRANSBORDO", SimTone.Danger, SimTextAlign.Left, true);
        }

        private static void DrawLevelSwitch(SimScene scene, double x, double y, string label, bool active)
        {
            SimTone tone = active ? SimTone.Active : SimTone.Muted;
            scene.Rect(x, y - 6.0, 14.0, 12.0, tone, tone);
            scene.Label(x + 22.0, y - 8.0, label, active ? SimTone.Neutral : SimTone.Muted);
        }

        public override UniversalLadderProgram BuildSampleProgram()
        {
            UniversalLadderProgram program = new UniversalLadderProgram();
            program.Name = "Silo com enchimento e descarga (exemplo)";

            program.Rungs.Add(LadderBuild.Rung().NO(0, StartInput).ParallelNO(0, "C0001").NC(1, StopInput).Out(LadderBuild.Coil("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").NC(1, HighLevelInput).Out(LadderBuild.Coil(FillValveOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").NO(1, TruckInput).NC(2, LowLevelInput).Out(LadderBuild.Set(DischargeValveOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, LowLevelInput).Out(LadderBuild.Reset(DischargeValveOutput)));
            program.Rungs.Add(LadderBuild.Rung().NC(0, TruckInput).Out(LadderBuild.Reset(DischargeValveOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, HighLevelInput).Out(LadderBuild.Coil(FullLampOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, LowLevelInput).Out(LadderBuild.Counter("V0002", 999)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Timer("V0001", 600, true)));
            program.Rungs.Add(LadderBuild.Rung().Out(LadderBuild.End()));
            return program;
        }

        public override string DescribeSampleProgram()
        {
            StringBuilder text = new StringBuilder();
            text.Append("1  X0004 ou C0001, com X0005 normalmente fechado, selam C0001 (marcha).\r\n");
            text.Append("2  C0001 com a chave de nível alto normalmente fechada mantém Y0001 aberta:\r\n");
            text.Append("   o silo trabalha como pulmão e a banda de enchimento vem da histerese da chave.\r\n");
            text.Append("3  C0001 com caminhão posicionado e nível baixo normalmente fechado dá SET em Y0002.\r\n");
            text.Append("4  A chave de nível baixo dá RESET em Y0002 (silo vazio).\r\n");
            text.Append("5  A saída do caminhão também dá RESET em Y0002.\r\n");
            text.Append("6  A chave de nível alto acende Y0003 (sinaleiro de silo cheio).\r\n");
            text.Append("7  A chave de nível baixo incrementa V0002 (esvaziamentos).\r\n");
            text.Append("8  C0001 alimenta o temporizador retentivo V0001 (horímetro).\r\n");
            text.Append("9  END.\r\n\r\n");
            text.Append("Sem a chave de nível alto a linha 2 perde a única proteção contra transbordo: ");
            text.Append("injetando a falha da chave cega, a planta passa a contar transbordo.");
            return text.ToString();
        }
    }

    /// <summary>
    /// Partida estrela-triângulo com três contatores, relé térmico e relé de corrente.
    ///
    /// Os contatores têm tempo de fechamento e de abertura diferentes; se estrela e triângulo
    /// ficam fechados ao mesmo tempo, a planta registra um curto-circuito entre fases, que é
    /// exatamente o que o intertravamento do programa deve impedir.
    /// </summary>
    internal sealed class StarDeltaProcess : SimulatedProcessBase
    {
        public const string MainsOutput = "Y0001";
        public const string StarOutput = "Y0002";
        public const string DeltaOutput = "Y0003";
        public const string StartInput = "X0001";
        public const string StopInput = "X0002";
        public const string ThermalInput = "X0003";
        public const string CurrentRelayInput = "X0004";
        public const string StarTimer = "V0001";
        public const string DeadTimeTimer = "V0002";

        private const double CloseRate = 1.0 / 0.030;
        private const double OpenRate = 1.0 / 0.050;
        private const double StarSpeedLimit = 0.72;
        private const double ThermalLimitSeconds = 6.0;

        private readonly SimBitRef mainsBit;
        private readonly SimBitRef starBit;
        private readonly SimBitRef deltaBit;
        private readonly SimBitRef thermalBit;
        private readonly SimBitRef currentBit;

        private readonly SimulatedFault weldedStarFault;
        private readonly SimulatedFault lowThermalFault;
        private readonly SimulatedFault heavyLoadFault;

        private readonly RateLimiter mainsState = new RateLimiter();
        private readonly RateLimiter starState = new RateLimiter();
        private readonly RateLimiter deltaState = new RateLimiter();
        private readonly HysteresisSwitch currentSwitch = new HysteresisSwitch(2.9, 2.4);

        private double speed;
        private double current;
        private double thermalTimer;
        private bool thermalTripped;
        private int shortCircuitCount;
        private int startCount;
        private bool lastDeltaClosed;

        public StarDeltaProcess()
        {
            mainsBit = Output(MainsOutput, "K1 contator de rede");
            starBit = Output(StarOutput, "K2 contator de estrela");
            deltaBit = Output(DeltaOutput, "K3 contator de triângulo");
            Button(StartInput, "Botoeira liga");
            Button(StopInput, "Botoeira para");
            thermalBit = Sensor(ThermalInput, "Relé térmico");
            currentBit = Sensor(CurrentRelayInput, "Relé de corrente acima do ajuste");

            weldedStarFault = Fault("stardelta.star.welded", "Contator de estrela colado", "K2 não abre; com o triângulo fechando ocorre curto entre fases.");
            lowThermalFault = Fault("stardelta.thermal.low", "Térmico com ajuste baixo", "A proteção atua muito antes do fim da partida.");
            heavyLoadFault = Fault("stardelta.load.heavy", "Carga pesada", "A inércia triplica e o motor demora muito mais para acelerar.");

            Reset();
        }

        public override string Id { get { return "motor.stardelta"; } }
        public override string DisplayName { get { return "Partida estrela-triângulo"; } }

        public override string Description
        {
            get
            {
                return "Motor trifásico com partida estrela-triângulo: contator de rede, de estrela e de triângulo, " +
                       "relé térmico e relé de corrente que indica o fim da aceleração em estrela.";
            }
        }

        public double Speed { get { return speed; } }
        public double Current { get { return current; } }
        public bool ThermalTripped { get { return thermalTripped; } }
        public int ShortCircuitCount { get { return shortCircuitCount; } }
        public int StartCount { get { return startCount; } }

        public override void Reset()
        {
            mainsState.Reset(0.0);
            starState.Reset(0.0);
            deltaState.Reset(0.0);
            currentSwitch.Reset(false);
            speed = 0.0;
            current = 0.0;
            thermalTimer = 0.0;
            thermalTripped = false;
            shortCircuitCount = 0;
            startCount = 0;
            lastDeltaClosed = false;
        }

        public override void Step(double dt, PlcProcessImage image)
        {
            if (dt <= 0.0 || image == null) return;

            mainsState.Update(image.GetBit(mainsBit) ? 1.0 : 0.0, CloseRate, OpenRate, dt);
            starState.Update(image.GetBit(starBit) || weldedStarFault.Active ? 1.0 : 0.0, CloseRate, OpenRate, dt);
            deltaState.Update(image.GetBit(deltaBit) ? 1.0 : 0.0, CloseRate, OpenRate, dt);

            bool mains = mainsState.Value > 0.5;
            bool star = starState.Value > 0.5;
            bool delta = deltaState.Value > 0.5;

            if (mains && star && delta)
            {
                // Curto entre fases: o disjuntor abre e o motor perde a alimentação.
                shortCircuitCount++;
                thermalTripped = true;
            }

            UpdateMotor(mains, star, delta, dt);
            UpdateThermal(dt);

            if (delta && !lastDeltaClosed) startCount++;
            lastDeltaClosed = delta;

            image.SetBit(thermalBit, thermalTripped);
            image.SetBit(currentBit, currentSwitch.Update(current));
        }

        private void UpdateMotor(bool mains, bool star, bool delta, double dt)
        {
            bool energised = mains && (star || delta) && !thermalTripped;
            double inertia = heavyLoadFault.Active ? 3.0 : 1.0;

            if (!energised)
            {
                speed = Math.Max(0.0, speed - (dt * 0.35 / inertia));
                current = Math.Max(0.0, current - (dt * 8.0));
                return;
            }

            // Em estrela o torque é um terço do nominal: acelera menos e não passa do limite.
            double ceiling = delta ? 1.0 : StarSpeedLimit;
            double torque = delta ? 1.0 : 0.34;
            double gap = ceiling - speed;
            if (gap > 0.0) speed += Math.Min(gap, dt * torque * 0.55 / inertia);

            // Corrente de partida alta que decai conforme o motor acelera.
            double baseCurrent = delta ? 6.5 : 2.6;
            current = 0.9 + (baseCurrent * (1.0 - (speed / Math.Max(0.05, ceiling))));
            if (current < 0.9) current = 0.9;
        }

        private void UpdateThermal(double dt)
        {
            double limit = lowThermalFault.Active ? ThermalLimitSeconds * 0.35 : ThermalLimitSeconds;
            if (current > 2.6 && !thermalTripped)
            {
                thermalTimer += dt;
                if (thermalTimer >= limit) thermalTripped = true;
            }
            else
            {
                thermalTimer = Math.Max(0.0, thermalTimer - (dt * 0.4));
            }
        }

        public override string StateSummary()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Velocidade do motor: ").Append((speed * 100.0).ToString("0", CultureInfo.InvariantCulture)).Append(" % da nominal\r\n");
            text.Append("Corrente: ").Append(current.ToString("0.0", CultureInfo.InvariantCulture)).Append(" x In\r\n");
            text.Append("K1 rede: ").Append(Describe(mainsState.Value)).Append("\r\n");
            text.Append("K2 estrela: ").Append(Describe(starState.Value)).Append("\r\n");
            text.Append("K3 triângulo: ").Append(Describe(deltaState.Value)).Append("\r\n");
            text.Append("Partidas concluídas: ").Append(startCount.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Curtos entre fases: ").Append(shortCircuitCount.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Relé térmico: ").Append(thermalTripped ? "atuado" : "normal");
            return text.ToString();
        }

        private static string Describe(double state)
        {
            if (state > 0.9) return "fechado";
            if (state < 0.1) return "aberto";
            return "em transição";
        }

        public override void BuildScene(SimScene scene, PlcProcessImage image)
        {
            scene.Line(120.0, 52.0, 760.0, 52.0, SimTone.Structure);
            scene.Label(40.0, 44.0, "REDE", SimTone.Muted);

            DrawContactor(scene, 200.0, 60.0, "K1", MainsOutput, mainsState.Value);
            DrawContactor(scene, 380.0, 60.0, "K2", StarOutput, starState.Value);
            DrawContactor(scene, 560.0, 60.0, "K3", DeltaOutput, deltaState.Value);

            bool running = speed > 0.02;
            scene.Ellipse(390.0, 190.0, 90.0, 90.0, running ? SimTone.Active : SimTone.Structure, SimTone.Structure);
            scene.Label(435.0, 226.0, "M", SimTone.Dark, SimTextAlign.Center, true);
            scene.Label(435.0, 292.0, "motor trifásico", SimTone.Muted, SimTextAlign.Center, false);

            DrawBar(scene, 560.0, 200.0, "Velocidade", speed, (speed * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %", SimTone.Active);
            DrawBar(scene, 560.0, 244.0, "Corrente", Math.Min(1.0, current / 7.0),
                current.ToString("0.0", CultureInfo.InvariantCulture) + " x In", current > 2.6 ? SimTone.Warning : SimTone.Info);

            scene.Lamp(120.0, 200.0, 22.0, image.GetBit(thermalBit), SimTone.Danger);
            scene.Label(150.0, 202.0, "Térmico  " + ThermalInput, SimTone.Muted);
            scene.Lamp(120.0, 240.0, 22.0, image.GetBit(currentBit), SimTone.Info);
            scene.Label(150.0, 242.0, "Corrente alta  " + CurrentRelayInput, SimTone.Muted);

            scene.Label(940.0, 22.0, "Partidas: " + startCount.ToString(CultureInfo.InvariantCulture), SimTone.Muted, SimTextAlign.Right, false);
            if (shortCircuitCount > 0)
                scene.Label(30.0, 292.0, "CURTO ENTRE FASES: " + shortCircuitCount.ToString(CultureInfo.InvariantCulture) + " ocorrência(s)", SimTone.Danger, SimTextAlign.Left, true);
        }

        private static void DrawContactor(SimScene scene, double x, double y, string tag, string address, double state)
        {
            bool closed = state > 0.9;
            bool moving = state > 0.1 && state <= 0.9;
            SimTone tone = closed ? SimTone.Active : moving ? SimTone.Warning : SimTone.Structure;

            scene.Line(x + 30.0, 52.0, x + 30.0, y, SimTone.Structure);
            scene.Rect(x, y, 60.0, 56.0, tone, SimTone.Structure, tag);
            scene.Label(x + 30.0, y + 64.0, address, SimTone.Muted, SimTextAlign.Center, false);
            scene.Line(x + 30.0, y + 56.0, x + 30.0, y + 86.0, closed ? SimTone.Active : SimTone.Structure, !closed);
        }

        private static void DrawBar(SimScene scene, double x, double y, string label, double fraction, string value, SimTone tone)
        {
            scene.Label(x, y - 18.0, label, SimTone.Muted);
            scene.Rect(x, y, 300.0, 18.0, SimTone.Dark, SimTone.Structure);
            double width = 300.0 * (fraction < 0.0 ? 0.0 : (fraction > 1.0 ? 1.0 : fraction));
            if (width > 1.0) scene.Rect(x, y, width, 18.0, tone, tone);
            scene.Label(x + 310.0, y, value, SimTone.Neutral);
        }

        public override UniversalLadderProgram BuildSampleProgram()
        {
            UniversalLadderProgram program = new UniversalLadderProgram();
            program.Name = "Partida estrela-triângulo (exemplo)";

            program.Rungs.Add(LadderBuild.Rung().NO(0, StartInput).ParallelNO(0, "C0001").NC(1, StopInput).NC(2, ThermalInput).Out(LadderBuild.Coil("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Coil(MainsOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").NC(1, StarTimer).NC(2, DeltaOutput).Out(LadderBuild.Coil(StarOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Timer(StarTimer, 50, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, StarTimer).NC(1, StarOutput).Out(LadderBuild.Timer(DeadTimeTimer, 2, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").NO(1, DeadTimeTimer).NC(2, CurrentRelayInput).Out(LadderBuild.Set(DeltaOutput)));
            program.Rungs.Add(LadderBuild.Rung().NC(0, "C0001").Out(LadderBuild.Reset(DeltaOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, DeltaOutput).Out(LadderBuild.Counter("V0003", 999)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Timer("V0004", 600, true)));
            program.Rungs.Add(LadderBuild.Rung().Out(LadderBuild.End()));
            return program;
        }

        public override string DescribeSampleProgram()
        {
            StringBuilder text = new StringBuilder();
            text.Append("1  X0001 ou C0001, com X0002 e X0003 normalmente fechados, selam C0001 (marcha).\r\n");
            text.Append("2  C0001 fecha Y0001 (K1, contator de rede).\r\n");
            text.Append("3  C0001, com V0001 e Y0003 normalmente fechados, mantém Y0002 (K2, estrela).\r\n");
            text.Append("4  C0001 alimenta V0001 (5,0 s de tempo mínimo em estrela).\r\n");
            text.Append("5  Vencido V0001 e com Y0002 já desligado, V0002 conta 0,2 s de tempo morto.\r\n");
            text.Append("6  C0001 com V0002 e X0004 normalmente fechado dá SET em Y0003 (K3, triângulo).\r\n");
            text.Append("7  A queda da marcha dá RESET em Y0003.\r\n");
            text.Append("8  Y0003 incrementa V0003 (partidas concluídas).\r\n");
            text.Append("9  C0001 alimenta o temporizador retentivo V0004 (horímetro).\r\n");
            text.Append("10 END.\r\n\r\n");
            text.Append("O tempo morto da linha 5 existe porque o contator de estrela leva cerca de 50 ms para abrir. ");
            text.Append("Fechando o triângulo antes disso, os dois ficam fechados ao mesmo tempo e a planta registra curto entre fases. ");
            text.Append("O contato de X0004 na linha 6 exige ainda que a corrente já tenha caído.");
            return text.ToString();
        }
    }

    /// <summary>
    /// Cruzamento semafórico com via principal e via secundária sob demanda.
    ///
    /// As filas de veículos crescem com jitter e escoam durante o verde, e o laço detector
    /// da via secundária só pede passagem quando há carro esperando. A planta conta conflitos:
    /// se os dois verdes ficarem acesos ao mesmo tempo, a lógica está errada.
    /// </summary>
    internal sealed class TrafficLightProcess : SimulatedProcessBase
    {
        public const string MainGreenOutput = "Y0001";
        public const string MainYellowOutput = "Y0002";
        public const string MainRedOutput = "Y0003";
        public const string SideGreenOutput = "Y0004";
        public const string SideYellowOutput = "Y0005";
        public const string SideRedOutput = "Y0006";
        public const string SideLoopInput = "X0001";
        public const string AutoSwitchInput = "X0002";

        private const double MainArrivalSeconds = 3.5;
        private const double SideArrivalSeconds = 7.0;
        private const double DepartureSeconds = 1.8;

        private readonly SimBitRef mainGreenBit;
        private readonly SimBitRef mainYellowBit;
        private readonly SimBitRef mainRedBit;
        private readonly SimBitRef sideGreenBit;
        private readonly SimBitRef sideYellowBit;
        private readonly SimBitRef sideRedBit;
        private readonly SimBitRef loopBit;

        private readonly SimulatedFault rushHourFault;
        private readonly SimulatedFault blindLoopFault;
        private readonly SimulatedFault lampFault;

        private readonly Random jitter = new Random(20260907);

        private double mainArrival;
        private double sideArrival;
        private double mainDeparture;
        private double sideDeparture;
        private int mainQueue;
        private int sideQueue;
        private int mainServed;
        private int sideServed;
        private int conflictCount;
        private double sideWaitSeconds;
        private double worstSideWait;

        public TrafficLightProcess()
        {
            mainGreenBit = Output(MainGreenOutput, "Verde da via principal");
            mainYellowBit = Output(MainYellowOutput, "Amarelo da via principal");
            mainRedBit = Output(MainRedOutput, "Vermelho da via principal");
            sideGreenBit = Output(SideGreenOutput, "Verde da via secundária");
            sideYellowBit = Output(SideYellowOutput, "Amarelo da via secundária");
            sideRedBit = Output(SideRedOutput, "Vermelho da via secundária");
            loopBit = Sensor(SideLoopInput, "Laço detector da via secundária");
            Button(AutoSwitchInput, "Chave de operação automática");

            rushHourFault = Fault("traffic.rush", "Hora de pico", "Triplica a chegada de veículos nas duas vias.");
            blindLoopFault = Fault("traffic.loop.blind", "Laço detector cego", "X0001 nunca atua e a via secundária deixa de ser atendida.");
            lampFault = Fault("traffic.lamp.burnt", "Lâmpada verde principal queimada", "O verde da via principal não acende, embora o comando continue ativo.");

            Reset();
        }

        public override string Id { get { return "traffic.intersection"; } }
        public override string DisplayName { get { return "Cruzamento semafórico"; } }

        public override string Description
        {
            get
            {
                return "Cruzamento com via principal e via secundária sob demanda, laço detector, filas de veículos " +
                       "que crescem e escoam, e contagem de conflito entre os dois verdes.";
            }
        }

        public int MainQueue { get { return mainQueue; } }
        public int SideQueue { get { return sideQueue; } }
        public int ConflictCount { get { return conflictCount; } }
        public double WorstSideWait { get { return worstSideWait; } }

        public override void Reset()
        {
            mainArrival = 0.0;
            sideArrival = 0.0;
            mainDeparture = 0.0;
            sideDeparture = 0.0;
            mainQueue = 2;
            sideQueue = 0;
            mainServed = 0;
            sideServed = 0;
            conflictCount = 0;
            sideWaitSeconds = 0.0;
            worstSideWait = 0.0;
        }

        public override void Step(double dt, PlcProcessImage image)
        {
            if (dt <= 0.0 || image == null) return;

            bool mainGreen = image.GetBit(mainGreenBit);
            bool sideGreen = image.GetBit(sideGreenBit);
            if (mainGreen && sideGreen) conflictCount++;

            double factor = rushHourFault.Active ? 0.34 : 1.0;
            mainQueue += Arrivals(ref mainArrival, MainArrivalSeconds * factor, dt);
            sideQueue += Arrivals(ref sideArrival, SideArrivalSeconds * factor, dt);

            if (mainGreen && mainQueue > 0) mainQueue -= Departures(ref mainDeparture, dt, ref mainServed);
            else mainDeparture = 0.0;

            if (sideGreen && sideQueue > 0) sideQueue -= Departures(ref sideDeparture, dt, ref sideServed);
            else sideDeparture = 0.0;

            if (mainQueue < 0) mainQueue = 0;
            if (sideQueue < 0) sideQueue = 0;

            if (sideQueue > 0 && !sideGreen)
            {
                sideWaitSeconds += dt;
                if (sideWaitSeconds > worstSideWait) worstSideWait = sideWaitSeconds;
            }
            else if (sideQueue == 0 || sideGreen)
            {
                sideWaitSeconds = 0.0;
            }

            image.SetBit(loopBit, !blindLoopFault.Active && sideQueue > 0);
        }

        private int Arrivals(ref double timer, double interval, double dt)
        {
            timer += dt;
            if (timer < interval * (0.75 + (jitter.NextDouble() * 0.5))) return 0;
            timer = 0.0;
            return 1;
        }

        private int Departures(ref double timer, double dt, ref int served)
        {
            timer += dt;
            if (timer < DepartureSeconds) return 0;
            timer = 0.0;
            served++;
            return 1;
        }

        public override string StateSummary()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Fila da via principal: ").Append(mainQueue.ToString(CultureInfo.InvariantCulture)).Append(" veículo(s)\r\n");
            text.Append("Fila da via secundária: ").Append(sideQueue.ToString(CultureInfo.InvariantCulture)).Append(" veículo(s)\r\n");
            text.Append("Veículos escoados na principal: ").Append(mainServed.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Veículos escoados na secundária: ").Append(sideServed.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Maior espera na secundária: ").Append(worstSideWait.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s\r\n");
            text.Append("Conflitos entre verdes: ").Append(conflictCount.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        public override void BuildScene(SimScene scene, PlcProcessImage image)
        {
            // Cruzamento visto de cima.
            scene.Rect(0.0, 130.0, 1000.0, 90.0, SimTone.Dark, SimTone.Structure);
            scene.Rect(430.0, 0.0, 140.0, 320.0, SimTone.Dark, SimTone.Structure);
            scene.Line(0.0, 175.0, 420.0, 175.0, SimTone.Muted, true);
            scene.Line(580.0, 175.0, 1000.0, 175.0, SimTone.Muted, true);

            bool mainGreenOn = image.GetBit(mainGreenBit) && !lampFault.Active;
            DrawHead(scene, 330.0, 18.0, "PRINCIPAL  " + MainGreenOutput + "/" + MainYellowOutput + "/" + MainRedOutput,
                mainGreenOn, image.GetBit(mainYellowBit), image.GetBit(mainRedBit));
            DrawHead(scene, 630.0, 192.0, "SECUNDÁRIA  " + SideGreenOutput + "/" + SideYellowOutput + "/" + SideRedOutput,
                image.GetBit(sideGreenBit), image.GetBit(sideYellowBit), image.GetBit(sideRedBit));

            for (int i = 0; i < mainQueue && i < 10; i++)
                scene.Rect(390.0 - (i * 38.0), 152.0, 30.0, 20.0, SimTone.Info, SimTone.Structure);
            for (int i = 0; i < sideQueue && i < 6; i++)
                scene.Rect(468.0, 250.0 + (i * 30.0), 22.0, 24.0, SimTone.Cargo, SimTone.Structure);

            scene.Lamp(60.0, 250.0, 20.0, image.GetBit(loopBit), SimTone.Warning);
            scene.Label(88.0, 252.0, "Laço  " + SideLoopInput, SimTone.Muted);

            scene.Label(30.0, 22.0, "Fila principal: " + mainQueue.ToString(CultureInfo.InvariantCulture) +
                "   ·   fila secundária: " + sideQueue.ToString(CultureInfo.InvariantCulture) +
                "   ·   maior espera " + worstSideWait.ToString("0.0", CultureInfo.InvariantCulture) + " s", SimTone.Muted);

            if (conflictCount > 0)
                scene.Label(30.0, 296.0, "CONFLITO ENTRE VERDES: " + conflictCount.ToString(CultureInfo.InvariantCulture) + " varredura(s)", SimTone.Danger, SimTextAlign.Left, true);
        }

        private static void DrawHead(SimScene scene, double x, double y, string label, bool green, bool yellow, bool red)
        {
            scene.Rect(x, y, 34.0, 92.0, SimTone.Dark, SimTone.Structure);
            scene.Lamp(x + 7.0, y + 6.0, 20.0, red, SimTone.Danger);
            scene.Lamp(x + 7.0, y + 34.0, 20.0, yellow, SimTone.Warning);
            scene.Lamp(x + 7.0, y + 62.0, 20.0, green, SimTone.Active);
            scene.Label(x + 17.0, y + 96.0, label, SimTone.Muted, SimTextAlign.Center, false);
        }

        public override UniversalLadderProgram BuildSampleProgram()
        {
            UniversalLadderProgram program = new UniversalLadderProgram();
            program.Name = "Cruzamento semafórico (exemplo)";

            // Cascata de fases: cada temporizador arma a fase seguinte e apaga a própria.
            program.Rungs.Add(LadderBuild.Rung().NO(0, AutoSwitchInput).NC(1, "C0001").NC(2, "C0002").NC(3, "C0003").NC(4, "C0004").Out(LadderBuild.Set("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Timer("V0001", 120, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0001").Out(LadderBuild.Set("C0002")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0001").Out(LadderBuild.Reset("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0002").Out(LadderBuild.Timer("V0002", 30, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0002").NO(1, SideLoopInput).Out(LadderBuild.Set("C0003")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0002").NC(1, SideLoopInput).Out(LadderBuild.Set("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0002").Out(LadderBuild.Reset("C0002")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0003").Out(LadderBuild.Timer("V0003", 80, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0003").Out(LadderBuild.Set("C0004")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0003").Out(LadderBuild.Reset("C0003")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0004").Out(LadderBuild.Timer("V0004", 30, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0004").Out(LadderBuild.Reset("C0004")));

            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Coil(MainGreenOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0002").Out(LadderBuild.Coil(MainYellowOutput)));
            program.Rungs.Add(LadderBuild.Rung().NC(0, "C0001").NC(1, "C0002").Out(LadderBuild.Coil(MainRedOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0003").Out(LadderBuild.Coil(SideGreenOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0004").Out(LadderBuild.Coil(SideYellowOutput)));
            program.Rungs.Add(LadderBuild.Rung().NC(0, "C0003").NC(1, "C0004").Out(LadderBuild.Coil(SideRedOutput)));

            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0003").Out(LadderBuild.Counter("V0005", 999)));
            program.Rungs.Add(LadderBuild.Rung().Out(LadderBuild.End()));
            return program;
        }

        public override string DescribeSampleProgram()
        {
            StringBuilder text = new StringBuilder();
            text.Append("As fases são estados em C0001 a C0004, e cada temporizador arma a fase seguinte.\r\n\r\n");
            text.Append("1  Com a chave automática e nenhuma fase ativa, C0001 é armado (verde principal).\r\n");
            text.Append("2  C0001 alimenta V0001 (12,0 s de verde principal).\r\n");
            text.Append("3  V0001 arma C0002 (amarelo principal) e a linha 4 apaga C0001.\r\n");
            text.Append("5  C0002 alimenta V0002 (3,0 s de amarelo).\r\n");
            text.Append("6  V0002 com o laço ocupado arma C0003 (verde da secundária).\r\n");
            text.Append("7  V0002 sem carro no laço volta direto para C0001: a secundária só é atendida sob demanda.\r\n");
            text.Append("9  C0003 alimenta V0003 (8,0 s) e a linha 10 arma C0004 (amarelo da secundária).\r\n");
            text.Append("12 C0004 alimenta V0004 (3,0 s) e a linha 13 encerra o ciclo.\r\n");
            text.Append("14 a 19 traduzem as fases em lâmpadas; os vermelhos são o complemento das fases da via.\r\n");
            text.Append("20 C0003 incrementa V0005 (ciclos com atendimento da secundária).\r\n");
            text.Append("21 END.");
            return text.ToString();
        }
    }

    /// <summary>
    /// Elevador de carga de dois níveis, com porta motorizada e sensor de sobrepeso.
    ///
    /// A cabine é um integrador com rampa; a porta tem curso próprio e precisa estar fechada
    /// para o movimento. Bater no fim do curso mecânico é contado como colisão.
    /// </summary>
    internal sealed class FreightElevatorProcess : SimulatedProcessBase
    {
        public const string UpOutput = "Y0001";
        public const string DownOutput = "Y0002";
        public const string DoorOutput = "Y0003";
        public const string MovingLampOutput = "Y0004";
        public const string LowerLimitInput = "X0001";
        public const string UpperLimitInput = "X0002";
        public const string DoorClosedInput = "X0003";
        public const string OverloadInput = "X0004";
        public const string LowerCallInput = "X0005";
        public const string UpperCallInput = "X0006";

        private const double TravelRate = 1.0 / 4.0;
        private const double DoorRate = 1.0 / 1.5;
        private const double OverloadLimit = 850.0;

        private readonly SimBitRef upBit;
        private readonly SimBitRef downBit;
        private readonly SimBitRef doorBit;
        private readonly SimBitRef lowerBit;
        private readonly SimBitRef upperBit;
        private readonly SimBitRef doorClosedBit;
        private readonly SimBitRef overloadBit;

        private readonly SimulatedFault doorJamFault;
        private readonly SimulatedFault blindUpperFault;
        private readonly SimulatedFault heavyLoadFault;

        private readonly RateLimiter cabin = new RateLimiter();
        private readonly RateLimiter door = new RateLimiter();
        private readonly Random jitter = new Random(20260908);

        private double load;
        private int tripCount;
        private int collisionCount;
        private bool lastUpper;

        public FreightElevatorProcess()
        {
            upBit = Output(UpOutput, "Motor sobe");
            downBit = Output(DownOutput, "Motor desce");
            doorBit = Output(DoorOutput, "Comando de abrir a porta");
            Output(MovingLampOutput, "Sinaleiro de cabine em movimento");
            lowerBit = Sensor(LowerLimitInput, "Fim de curso do nível inferior");
            upperBit = Sensor(UpperLimitInput, "Fim de curso do nível superior");
            doorClosedBit = Sensor(DoorClosedInput, "Porta fechada");
            overloadBit = Sensor(OverloadInput, "Sobrepeso na cabine");
            Button(LowerCallInput, "Chamada do nível inferior");
            Button(UpperCallInput, "Chamada do nível superior");

            doorJamFault = Fault("elevator.door.jam", "Porta emperrada", "A porta não fecha por completo e X0003 nunca atua; a cabine fica travada.");
            blindUpperFault = Fault("elevator.upper.blind", "Fim de curso superior cego", "X0002 nunca atua e a cabine bate no fim do curso mecânico.");
            heavyLoadFault = Fault("elevator.load.heavy", "Carga acima do limite", "O peso embarcado passa do limite e o sensor de sobrepeso atua.");

            Reset();
        }

        public override string Id { get { return "elevator.freight"; } }
        public override string DisplayName { get { return "Elevador de carga de dois níveis"; } }

        public override string Description
        {
            get
            {
                return "Plataforma de carga entre dois níveis, com porta motorizada, fins de curso e sensor de sobrepeso. " +
                       "A cabine só se move com a porta fechada.";
            }
        }

        public double Position { get { return cabin.Value; } }
        public double DoorOpening { get { return door.Value; } }
        public double Load { get { return load; } }
        public int TripCount { get { return tripCount; } }
        public int CollisionCount { get { return collisionCount; } }

        public override void Reset()
        {
            cabin.Reset(0.0);
            door.Reset(0.0);
            load = 320.0;
            tripCount = 0;
            collisionCount = 0;
            lastUpper = false;
        }

        public override void Step(double dt, PlcProcessImage image)
        {
            if (dt <= 0.0 || image == null) return;

            double doorTarget = image.GetBit(doorBit) ? 1.0 : 0.0;
            if (doorJamFault.Active && doorTarget < 0.12) doorTarget = 0.12;
            door.Update(doorTarget, DoorRate, DoorRate, dt);

            bool closed = door.Value <= 0.05;
            bool up = image.GetBit(upBit) && closed;
            bool down = image.GetBit(downBit) && closed;

            if (up && down)
            {
                // Comando contraditório: o motor não tem para onde ir.
                up = false;
                down = false;
            }

            if (up) cabin.Update(1.0, TravelRate, TravelRate, dt);
            else if (down) cabin.Update(0.0, TravelRate, TravelRate, dt);

            if (up && cabin.Value >= 0.999 && blindUpperFault.Active) collisionCount++;

            bool atUpper = !blindUpperFault.Active && cabin.Value >= 0.97;
            image.SetBit(upperBit, atUpper);
            image.SetBit(lowerBit, cabin.Value <= 0.03);
            image.SetBit(doorClosedBit, closed);

            if (atUpper && !lastUpper)
            {
                tripCount++;
                load = heavyLoadFault.Active ? 900.0 + (jitter.NextDouble() * 200.0) : 180.0 + (jitter.NextDouble() * 500.0);
            }
            lastUpper = atUpper;

            image.SetBit(overloadBit, load > OverloadLimit);
        }

        public override string StateSummary()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Posição da cabine: ").Append((cabin.Value * 100.0).ToString("0", CultureInfo.InvariantCulture)).Append(" % do percurso\r\n");
            text.Append("Abertura da porta: ").Append((door.Value * 100.0).ToString("0", CultureInfo.InvariantCulture)).Append(" %\r\n");
            text.Append("Carga embarcada: ").Append(load.ToString("0", CultureInfo.InvariantCulture)).Append(" kg\r\n");
            text.Append("Viagens ao nível superior: ").Append(tripCount.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Colisões no fim do curso: ").Append(collisionCount.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        public override void BuildScene(SimScene scene, PlcProcessImage image)
        {
            double shaftX = 400.0;
            double shaftTop = 30.0;
            double shaftBottom = 280.0;
            double cabinH = 62.0;
            double travel = shaftBottom - shaftTop - cabinH;

            scene.Rect(shaftX, shaftTop, 200.0, shaftBottom - shaftTop, SimTone.Dark, SimTone.Structure);
            scene.Line(shaftX - 40.0, shaftTop + 6.0, shaftX, shaftTop + 6.0, SimTone.Structure);
            scene.Label(shaftX - 46.0, shaftTop, "nível superior", SimTone.Muted, SimTextAlign.Right, false);
            scene.Line(shaftX - 40.0, shaftBottom - 6.0, shaftX, shaftBottom - 6.0, SimTone.Structure);
            scene.Label(shaftX - 46.0, shaftBottom - 14.0, "nível inferior", SimTone.Muted, SimTextAlign.Right, false);

            double cabinY = shaftBottom - cabinH - (cabin.Value * travel);
            scene.Rect(shaftX + 10.0, cabinY, 180.0, cabinH, SimTone.Dark, SimTone.Neutral);
            scene.Rect(shaftX + 40.0, cabinY + 16.0, 120.0, cabinH - 30.0, SimTone.Cargo, SimTone.Cargo);

            // Folhas da porta abrindo para os lados.
            double leaf = 82.0 * (1.0 - door.Value);
            scene.Rect(shaftX + 14.0, cabinY + 6.0, leaf, cabinH - 12.0, SimTone.Structure, SimTone.Neutral);
            scene.Rect(shaftX + 186.0 - leaf, cabinY + 6.0, leaf, cabinH - 12.0, SimTone.Structure, SimTone.Neutral);

            scene.Lamp(shaftX + 220.0, shaftTop + 4.0, 18.0, image.GetBit(upperBit), SimTone.Active);
            scene.Label(shaftX + 246.0, shaftTop + 4.0, "Superior  " + UpperLimitInput, SimTone.Muted);
            scene.Lamp(shaftX + 220.0, shaftBottom - 22.0, 18.0, image.GetBit(lowerBit), SimTone.Active);
            scene.Label(shaftX + 246.0, shaftBottom - 22.0, "Inferior  " + LowerLimitInput, SimTone.Muted);
            scene.Lamp(shaftX + 220.0, 140.0, 18.0, image.GetBit(doorClosedBit), SimTone.Info);
            scene.Label(shaftX + 246.0, 140.0, "Porta fechada  " + DoorClosedInput, SimTone.Muted);
            scene.Lamp(shaftX + 220.0, 176.0, 18.0, image.GetBit(overloadBit), SimTone.Danger);
            scene.Label(shaftX + 246.0, 176.0, "Sobrepeso  " + OverloadInput, SimTone.Muted);

            scene.Rect(120.0, 120.0, 60.0, 40.0, image.GetBit(upBit) ? SimTone.Active : SimTone.Structure, SimTone.Structure, "SOBE");
            scene.Label(150.0, 164.0, UpOutput, SimTone.Muted, SimTextAlign.Center, false);
            scene.Rect(120.0, 190.0, 60.0, 40.0, image.GetBit(downBit) ? SimTone.Active : SimTone.Structure, SimTone.Structure, "DESCE");
            scene.Label(150.0, 234.0, DownOutput, SimTone.Muted, SimTextAlign.Center, false);

            scene.Label(30.0, 22.0, "Viagens: " + tripCount.ToString(CultureInfo.InvariantCulture) +
                "   ·   carga " + load.ToString("0", CultureInfo.InvariantCulture) + " kg", SimTone.Muted);
            if (collisionCount > 0)
                scene.Label(30.0, 296.0, "CABINE BATENDO NO FIM DO CURSO", SimTone.Danger, SimTextAlign.Left, true);
        }

        public override UniversalLadderProgram BuildSampleProgram()
        {
            UniversalLadderProgram program = new UniversalLadderProgram();
            program.Name = "Elevador de carga (exemplo)";

            program.Rungs.Add(LadderBuild.Rung().NO(0, UpperCallInput).NC(1, UpperLimitInput).NO(2, DoorClosedInput).NC(3, DownOutput).Out(LadderBuild.Set(UpOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, UpperLimitInput).NO(1, UpOutput).Out(LadderBuild.Set("C0002")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, UpperLimitInput).Out(LadderBuild.Reset(UpOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, LowerCallInput).NC(1, LowerLimitInput).NO(2, DoorClosedInput).NC(3, UpOutput).Out(LadderBuild.Set(DownOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, LowerLimitInput).NO(1, DownOutput).Out(LadderBuild.Set("C0002")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, LowerLimitInput).Out(LadderBuild.Reset(DownOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, OverloadInput).Out(LadderBuild.Reset(UpOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, OverloadInput).Out(LadderBuild.Reset(DownOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0002").Out(LadderBuild.Coil(DoorOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0002").Out(LadderBuild.Timer("V0001", 40, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0001").Out(LadderBuild.Reset("C0002")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, UpOutput).ParallelNO(0, DownOutput).Out(LadderBuild.Coil(MovingLampOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, UpperLimitInput).Out(LadderBuild.Counter("V0002", 999)));
            program.Rungs.Add(LadderBuild.Rung().Out(LadderBuild.End()));
            return program;
        }

        public override string DescribeSampleProgram()
        {
            StringBuilder text = new StringBuilder();
            text.Append("1  Chamada de cima, cabine fora do nível superior, porta fechada e sem comando de descida dão SET em Y0001.\r\n");
            text.Append("2  Chegando em cima com o motor ainda ligado, C0002 arma o ciclo de porta.\r\n");
            text.Append("3  O fim de curso superior dá RESET em Y0001.\r\n");
            text.Append("4 a 6 repetem a mesma lógica para a descida.\r\n");
            text.Append("7 e 8  O sobrepeso derruba os dois comandos de movimento.\r\n");
            text.Append("9  C0002 abre a porta (Y0003).\r\n");
            text.Append("10 C0002 alimenta V0001 (4,0 s de porta aberta).\r\n");
            text.Append("11 V0001 encerra o ciclo de porta, que volta a fechar.\r\n");
            text.Append("12 Y0001 ou Y0002 acendem Y0004 (cabine em movimento).\r\n");
            text.Append("13 O fim de curso superior incrementa V0002 (viagens).\r\n");
            text.Append("14 END.");
            return text.ToString();
        }
    }

    /// <summary>
    /// Prensa hidráulica com comando bimanual, cortina de luz e fins de curso.
    ///
    /// A descida não tem selo: soltar um dos botões para o martelo na hora, que é o
    /// comportamento exigido de um comando bimanual. Se o martelo descer com a cortina
    /// interrompida, a planta conta um acidente.
    /// </summary>
    internal sealed class PressProcess : SimulatedProcessBase
    {
        public const string DownOutput = "Y0001";
        public const string UpOutput = "Y0002";
        public const string CycleLampOutput = "Y0003";
        public const string LeftButtonInput = "X0001";
        public const string RightButtonInput = "X0002";
        public const string CurtainInput = "X0003";
        public const string TopLimitInput = "X0004";
        public const string BottomLimitInput = "X0005";

        private const double DescentRate = 1.0 / 0.55;
        private const double AscentRate = 1.0 / 0.80;

        private readonly SimBitRef downBit;
        private readonly SimBitRef upBit;
        private readonly SimBitRef curtainBit;
        private readonly SimBitRef topBit;
        private readonly SimBitRef bottomBit;

        private readonly SimulatedFault curtainFault;
        private readonly SimulatedFault slowRamFault;
        private readonly SimulatedFault blindBottomFault;

        private readonly RateLimiter ram = new RateLimiter();

        private int partCount;
        private int accidentCount;
        private bool lastBottom;

        public PressProcess()
        {
            downBit = Output(DownOutput, "Descida do martelo");
            upBit = Output(UpOutput, "Subida do martelo");
            Output(CycleLampOutput, "Sinaleiro de ciclo");
            Button(LeftButtonInput, "Botão bimanual esquerdo");
            Button(RightButtonInput, "Botão bimanual direito");
            curtainBit = Sensor(CurtainInput, "Cortina de luz livre");
            topBit = Sensor(TopLimitInput, "Fim de curso superior");
            bottomBit = Sensor(BottomLimitInput, "Fim de curso inferior");

            curtainFault = Fault("press.curtain.broken", "Operador na zona de risco", "A cortina de luz é interrompida: X0003 cai a zero.");
            slowRamFault = Fault("press.ram.slow", "Vazamento hidráulico", "O martelo desce e sobe a um terço da velocidade.");
            blindBottomFault = Fault("press.bottom.blind", "Fim de curso inferior cego", "X0005 nunca atua e o ciclo não fecha.");

            Reset();
        }

        public override string Id { get { return "press.twohand"; } }
        public override string DisplayName { get { return "Prensa com comando bimanual"; } }

        public override string Description
        {
            get
            {
                return "Prensa hidráulica com comando bimanual, cortina de luz e fins de curso. " +
                       "A planta registra acidente sempre que o martelo desce com a cortina interrompida.";
            }
        }

        public double RamPosition { get { return ram.Value; } }
        public int PartCount { get { return partCount; } }
        public int AccidentCount { get { return accidentCount; } }
        public bool CurtainClear { get { return !curtainFault.Active; } }

        public override void Reset()
        {
            ram.Reset(0.0);
            partCount = 0;
            accidentCount = 0;
            lastBottom = false;
        }

        public override void Step(double dt, PlcProcessImage image)
        {
            if (dt <= 0.0 || image == null) return;

            bool down = image.GetBit(downBit);
            bool up = image.GetBit(upBit);
            double factor = slowRamFault.Active ? 0.34 : 1.0;
            double before = ram.Value;

            if (down && !up) ram.Update(1.0, DescentRate * factor, DescentRate * factor, dt);
            else if (up && !down) ram.Update(0.0, AscentRate * factor, AscentRate * factor, dt);

            bool descending = ram.Value > before + 1e-9;
            if (descending && curtainFault.Active) accidentCount++;

            bool atBottom = !blindBottomFault.Active && ram.Value >= 0.97;
            if (atBottom && !lastBottom) partCount++;
            lastBottom = atBottom;

            image.SetBit(curtainBit, !curtainFault.Active);
            image.SetBit(topBit, ram.Value <= 0.03);
            image.SetBit(bottomBit, atBottom);
        }

        public override string StateSummary()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Posição do martelo: ").Append((ram.Value * 100.0).ToString("0", CultureInfo.InvariantCulture)).Append(" % do curso\r\n");
            text.Append("Cortina de luz: ").Append(curtainFault.Active ? "interrompida" : "livre").Append("\r\n");
            text.Append("Peças prensadas: ").Append(partCount.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            text.Append("Descidas com a cortina interrompida: ").Append(accidentCount.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        public override void BuildScene(SimScene scene, PlcProcessImage image)
        {
            double frameX = 330.0;
            double top = 30.0;
            double table = 250.0;

            scene.Rect(frameX - 30.0, top, 20.0, table - top + 30.0, SimTone.Structure, SimTone.Structure);
            scene.Rect(frameX + 350.0, top, 20.0, table - top + 30.0, SimTone.Structure, SimTone.Structure);
            scene.Rect(frameX - 30.0, top, 400.0, 22.0, SimTone.Structure, SimTone.Structure);
            scene.Rect(frameX - 30.0, table, 400.0, 30.0, SimTone.Structure, SimTone.Neutral);
            scene.Label(frameX + 170.0, table + 38.0, "mesa", SimTone.Muted, SimTextAlign.Center, false);

            double ramTravel = table - top - 70.0;
            double ramY = top + 22.0 + (ram.Value * ramTravel);
            scene.Rect(frameX + 100.0, top + 22.0, 140.0, ramY - top - 22.0 + 4.0, SimTone.Dark, SimTone.Structure);
            scene.Rect(frameX + 60.0, ramY, 220.0, 44.0, image.GetBit(downBit) ? SimTone.Warning : SimTone.Structure, SimTone.Neutral, "MARTELO");

            // Peça sobre a mesa.
            scene.Rect(frameX + 130.0, table - 16.0, 80.0, 16.0, SimTone.Cargo, SimTone.Cargo);

            // Feixe da cortina de luz na frente da zona de prensagem.
            bool clear = image.GetBit(curtainBit);
            scene.Line(frameX - 10.0, table - 60.0, frameX + 360.0, table - 60.0, clear ? SimTone.Active : SimTone.Danger, !clear);
            scene.Label(frameX + 380.0, table - 70.0, "Cortina  " + CurtainInput, clear ? SimTone.Muted : SimTone.Danger);

            scene.Lamp(120.0, 90.0, 20.0, image.GetBit(topBit), SimTone.Info);
            scene.Label(148.0, 92.0, "Superior  " + TopLimitInput, SimTone.Muted);
            scene.Lamp(120.0, 126.0, 20.0, image.GetBit(bottomBit), SimTone.Info);
            scene.Label(148.0, 128.0, "Inferior  " + BottomLimitInput, SimTone.Muted);
            scene.Lamp(120.0, 162.0, 20.0, image.GetBit(downBit), SimTone.Warning);
            scene.Label(148.0, 164.0, "Descida  " + DownOutput, SimTone.Muted);
            scene.Lamp(120.0, 198.0, 20.0, image.GetBit(upBit), SimTone.Active);
            scene.Label(148.0, 200.0, "Subida  " + UpOutput, SimTone.Muted);

            scene.Label(30.0, 22.0, "Peças prensadas: " + partCount.ToString(CultureInfo.InvariantCulture), SimTone.Muted);
            if (accidentCount > 0)
                scene.Label(30.0, 296.0, "DESCIDA COM A CORTINA INTERROMPIDA", SimTone.Danger, SimTextAlign.Left, true);
        }

        public override UniversalLadderProgram BuildSampleProgram()
        {
            UniversalLadderProgram program = new UniversalLadderProgram();
            program.Name = "Prensa com comando bimanual (exemplo)";

            program.Rungs.Add(LadderBuild.Rung().NO(0, LeftButtonInput).NO(1, RightButtonInput).NO(2, CurtainInput)
                .NC(3, UpOutput).NC(4, BottomLimitInput).NC(5, "C0002").Out(LadderBuild.Coil(DownOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, BottomLimitInput).Out(LadderBuild.Set("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, BottomLimitInput).Out(LadderBuild.Set("C0002")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "C0001").Out(LadderBuild.Timer("V0001", 8, false)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0001").Out(LadderBuild.Set(UpOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, "V0001").Out(LadderBuild.Reset("C0001")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, TopLimitInput).Out(LadderBuild.Reset(UpOutput)));
            program.Rungs.Add(LadderBuild.Rung().NC(0, LeftButtonInput).NC(1, RightButtonInput).Out(LadderBuild.Reset("C0002")));
            program.Rungs.Add(LadderBuild.Rung().NO(0, DownOutput).ParallelNO(0, UpOutput).Out(LadderBuild.Coil(CycleLampOutput)));
            program.Rungs.Add(LadderBuild.Rung().NO(0, BottomLimitInput).Out(LadderBuild.Counter("V0002", 999)));
            program.Rungs.Add(LadderBuild.Rung().Out(LadderBuild.End()));
            return program;
        }

        public override string DescribeSampleProgram()
        {
            StringBuilder text = new StringBuilder();
            text.Append("1  Os dois botões, a cortina livre, Y0002 e o fim de curso inferior normalmente fechados,\r\n");
            text.Append("   e C0002 normalmente fechado, acionam Y0001. A linha não tem selo de propósito:\r\n");
            text.Append("   soltar um botão para a descida na hora.\r\n");
            text.Append("2  O fim de curso inferior dá SET em C0001 (prensagem em andamento).\r\n");
            text.Append("3  O fim de curso inferior dá SET em C0002 (bloqueio de repetição).\r\n");
            text.Append("4  C0001 alimenta V0001 (0,8 s de permanência sob pressão).\r\n");
            text.Append("5  V0001 dá SET em Y0002 (subida).\r\n");
            text.Append("6  V0001 encerra C0001.\r\n");
            text.Append("7  O fim de curso superior dá RESET em Y0002.\r\n");
            text.Append("8  Soltar os dois botões dá RESET em C0002 e rearma a prensa.\r\n");
            text.Append("9  Y0001 ou Y0002 acendem Y0003 (ciclo em andamento).\r\n");
            text.Append("10 O fim de curso inferior incrementa V0002 (peças prensadas).\r\n");
            text.Append("11 END.\r\n\r\n");
            text.Append("As linhas 3 e 8 são a anti-repetição exigida de um comando bimanual: manter os botões pressionados ");
            text.Append("não inicia um segundo ciclo; é preciso soltar os dois e apertar de novo.\r\n\r\n");
            text.Append("O contato X0003 na linha 1 é a proteção. Removendo-o e injetando a falha da cortina, ");
            text.Append("a planta passa a contar descidas com a zona de risco ocupada.");
            return text.ToString();
        }
    }
}
