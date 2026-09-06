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
            if (onLevel >= offLevel)
            {
                if (state) { if (input <= offLevel) state = false; }
                else { if (input >= onLevel) state = true; }
            }
            else
            {
                // Limiar invertido: liga abaixo de onLevel e desliga acima de offLevel.
                if (state) { if (input >= offLevel) state = false; }
                else { if (input <= onLevel) state = true; }
            }
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

    /// <summary>Papel visual de uma primitiva. A cor concreta é escolhida pela interface.</summary>
    internal enum SimTone
    {
        Neutral,
        Structure,
        Active,
        Info,
        Warning,
        Danger,
        Cargo,
        Muted,
        Dark
    }

    internal enum SimTextAlign
    {
        Left,
        Center,
        Right
    }

    internal enum SimShapeKind
    {
        Rectangle,
        Ellipse,
        Line,
        Text,
        Belt,
        Level,
        Lamp
    }

    /// <summary>
    /// Primitiva de desenho do sinóptico. Em <see cref="SimShapeKind.Line"/> os campos
    /// W e H são o segundo ponto do segmento, e não largura e altura.
    /// </summary>
    internal sealed class SimShape
    {
        public SimShapeKind Kind;
        public double X;
        public double Y;
        public double W;
        public double H;
        public SimTone Fill = SimTone.Neutral;
        public SimTone Stroke = SimTone.Structure;
        public string Text = string.Empty;
        public double Value;
        public bool On;
        public bool Dashed;
        public bool Bold;
        public SimTextAlign Align = SimTextAlign.Left;
    }

    /// <summary>
    /// Sinóptico de uma planta, descrito em coordenadas próprias e sem depender de WinForms.
    /// A interface escala a cena para o tamanho disponível e escolhe as cores de cada papel.
    /// </summary>
    internal sealed class SimScene
    {
        public const double Width = 1000.0;
        public const double Height = 320.0;

        private readonly List<SimShape> shapes = new List<SimShape>();

        public IList<SimShape> Shapes { get { return shapes.AsReadOnly(); } }

        public void Clear()
        {
            shapes.Clear();
        }

        public SimScene Rect(double x, double y, double w, double h, SimTone fill, SimTone stroke)
        {
            return Rect(x, y, w, h, fill, stroke, string.Empty);
        }

        public SimScene Rect(double x, double y, double w, double h, SimTone fill, SimTone stroke, string text)
        {
            SimShape shape = new SimShape();
            shape.Kind = SimShapeKind.Rectangle;
            shape.X = x; shape.Y = y; shape.W = w; shape.H = h;
            shape.Fill = fill; shape.Stroke = stroke; shape.Text = text ?? string.Empty;
            shapes.Add(shape);
            return this;
        }

        public SimScene Ellipse(double x, double y, double w, double h, SimTone fill, SimTone stroke)
        {
            SimShape shape = new SimShape();
            shape.Kind = SimShapeKind.Ellipse;
            shape.X = x; shape.Y = y; shape.W = w; shape.H = h;
            shape.Fill = fill; shape.Stroke = stroke;
            shapes.Add(shape);
            return this;
        }

        public SimScene Line(double x1, double y1, double x2, double y2, SimTone tone)
        {
            return Line(x1, y1, x2, y2, tone, false);
        }

        public SimScene Line(double x1, double y1, double x2, double y2, SimTone tone, bool dashed)
        {
            SimShape shape = new SimShape();
            shape.Kind = SimShapeKind.Line;
            shape.X = x1; shape.Y = y1; shape.W = x2; shape.H = y2;
            shape.Stroke = tone; shape.Dashed = dashed;
            shapes.Add(shape);
            return this;
        }

        public SimScene Label(double x, double y, string text, SimTone tone)
        {
            return Label(x, y, text, tone, SimTextAlign.Left, false);
        }

        public SimScene Label(double x, double y, string text, SimTone tone, SimTextAlign align, bool bold)
        {
            SimShape shape = new SimShape();
            shape.Kind = SimShapeKind.Text;
            shape.X = x; shape.Y = y;
            shape.Text = text ?? string.Empty;
            shape.Fill = tone; shape.Align = align; shape.Bold = bold;
            shapes.Add(shape);
            return this;
        }

        /// <summary>Correia com estrias que se deslocam conforme a fase informada.</summary>
        public SimScene Belt(double x, double y, double w, double h, double phase)
        {
            SimShape shape = new SimShape();
            shape.Kind = SimShapeKind.Belt;
            shape.X = x; shape.Y = y; shape.W = w; shape.H = h;
            shape.Value = phase;
            shape.Fill = SimTone.Structure; shape.Stroke = SimTone.Structure;
            shapes.Add(shape);
            return this;
        }

        /// <summary>Recipiente com preenchimento proporcional, de baixo para cima.</summary>
        public SimScene Level(double x, double y, double w, double h, double fraction, SimTone tone)
        {
            SimShape shape = new SimShape();
            shape.Kind = SimShapeKind.Level;
            shape.X = x; shape.Y = y; shape.W = w; shape.H = h;
            shape.Value = fraction < 0.0 ? 0.0 : (fraction > 1.0 ? 1.0 : fraction);
            shape.Fill = tone; shape.Stroke = SimTone.Structure;
            shapes.Add(shape);
            return this;
        }

        public SimScene Lamp(double x, double y, double diameter, bool on, SimTone tone)
        {
            SimShape shape = new SimShape();
            shape.Kind = SimShapeKind.Lamp;
            shape.X = x; shape.Y = y; shape.W = diameter; shape.H = diameter;
            shape.On = on; shape.Fill = tone;
            shapes.Add(shape);
            return this;
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

        /// <summary>Descreve o sinóptico no estado atual.</summary>
        void BuildScene(SimScene scene, PlcProcessImage image);

        /// <summary>Programa Ladder de exemplo que comanda esta planta.</summary>
        UniversalLadderProgram BuildSampleProgram();

        /// <summary>Explicação rung a rung do programa de exemplo.</summary>
        string DescribeSampleProgram();
    }

    /// <summary>
    /// Base comum das plantas: registro de pontos de I/O, falhas injetáveis e resolução
    /// de endereços, para que cada processo trate apenas da própria física.
    /// </summary>
    internal abstract class SimulatedProcessBase : ISimulatedProcess
    {
        private readonly List<SimulatedIoPoint> points = new List<SimulatedIoPoint>();
        private readonly List<SimulatedFault> faults = new List<SimulatedFault>();

        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }

        public IList<SimulatedIoPoint> Points { get { return points.AsReadOnly(); } }
        public IList<SimulatedFault> Faults { get { return faults.AsReadOnly(); } }

        protected SimBitRef Output(string address, string name)
        {
            return Register(address, name, SimIoDirection.PlcOutput, true);
        }

        /// <summary>Entrada escrita pela planta: sensor, fim de curso, relé de proteção.</summary>
        protected SimBitRef Sensor(string address, string name)
        {
            return Register(address, name, SimIoDirection.PlcInput, true);
        }

        /// <summary>Entrada de campo: botoeira ou chave que o operador aciona.</summary>
        protected SimBitRef Button(string address, string name)
        {
            return Register(address, name, SimIoDirection.PlcInput, false);
        }

        protected SimulatedFault Fault(string id, string name, string description)
        {
            SimulatedFault fault = new SimulatedFault(id, name, description);
            faults.Add(fault);
            return fault;
        }

        private SimBitRef Register(string address, string name, SimIoDirection direction, bool drivenByProcess)
        {
            points.Add(new SimulatedIoPoint(address, name, direction, drivenByProcess));
            SimBitRef bit;
            SimAddress.TryParseBit(address, out bit);
            return bit;
        }

        protected void ClearFaults()
        {
            for (int i = 0; i < faults.Count; i++) faults[i].Active = false;
        }

        public abstract void Reset();
        public abstract void Step(double dtSeconds, PlcProcessImage image);
        public abstract string StateSummary();
        public abstract void BuildScene(SimScene scene, PlcProcessImage image);
        public abstract UniversalLadderProgram BuildSampleProgram();
        public abstract string DescribeSampleProgram();
    }

    /// <summary>
    /// Montagem de rungs no modelo Ladder universal. Cada rung tem sete colunas de condição
    /// e a última coluna reservada à saída, como no editor.
    /// </summary>
    internal sealed class RungBuilder
    {
        private readonly UniversalLadderRung rung = new UniversalLadderRung();

        public RungBuilder NO(int column, string address)
        {
            rung.Series.Add(Contact(column, address, false));
            return this;
        }

        public RungBuilder NC(int column, string address)
        {
            rung.Series.Add(Contact(column, address, true));
            return this;
        }

        public RungBuilder ParallelNO(int column, string address)
        {
            rung.Parallel.Add(Contact(column, address, false));
            return this;
        }

        public RungBuilder ParallelNC(int column, string address)
        {
            rung.Parallel.Add(Contact(column, address, true));
            return this;
        }

        public UniversalLadderRung Out(UniversalLadderElement output)
        {
            rung.Series.Add(output);
            return rung;
        }

        private static UniversalLadderElement Contact(int column, string address, bool normallyClosed)
        {
            UniversalLadderElement element = new UniversalLadderElement();
            element.Kind = normallyClosed ? UniversalElementKind.ContactNC : UniversalElementKind.ContactNO;
            element.Address = address;
            element.Column = column;
            return element;
        }
    }

    internal static class LadderBuild
    {
        public static RungBuilder Rung()
        {
            return new RungBuilder();
        }

        public static UniversalLadderElement Coil(string address)
        {
            return Output(UniversalElementKind.Coil, address, string.Empty, string.Empty);
        }

        public static UniversalLadderElement Set(string address)
        {
            return Output(UniversalElementKind.Set, address, string.Empty, string.Empty);
        }

        public static UniversalLadderElement Reset(string address)
        {
            return Output(UniversalElementKind.Reset, address, string.Empty, string.Empty);
        }

        /// <summary>Temporizador. O preset é contado em décimos de segundo.</summary>
        public static UniversalLadderElement Timer(string variable, int preset, bool retentive)
        {
            return Output(UniversalElementKind.Timer, variable, preset.ToString(CultureInfo.InvariantCulture), retentive ? "RESET" : string.Empty);
        }

        public static UniversalLadderElement Counter(string variable, int preset)
        {
            return Output(UniversalElementKind.Counter, variable, preset.ToString(CultureInfo.InvariantCulture), string.Empty);
        }

        public static UniversalLadderElement End()
        {
            return Output(UniversalElementKind.End, string.Empty, string.Empty, string.Empty);
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

    /// <summary>
    /// Plantas disponíveis no simulador. É o ponto de extensão da biblioteca de processos.
    /// </summary>
    internal static class SimulatedProcessCatalog
    {
        public static IList<ISimulatedProcess> Create()
        {
            List<ISimulatedProcess> list = new List<ISimulatedProcess>();
            list.Add(new ConveyorProcess());
            list.Add(new SiloProcess());
            list.Add(new StarDeltaProcess());
            list.Add(new TrafficLightProcess());
            list.Add(new FreightElevatorProcess());
            list.Add(new PressProcess());
            return list;
        }

        public static string DefaultId
        {
            get { return "conveyor.diverter"; }
        }
    }
}
