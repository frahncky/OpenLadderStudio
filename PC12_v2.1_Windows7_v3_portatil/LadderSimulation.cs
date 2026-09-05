using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModernPC12
{
    internal enum SimBitArea
    {
        Input,
        Output,
        Auxiliary,
        Special,
        Variable
    }

    internal struct SimBitRef
    {
        public SimBitArea Area;
        public int Index;
        public bool Valid;

        public static SimBitRef Invalid
        {
            get
            {
                SimBitRef r = new SimBitRef();
                r.Valid = false;
                return r;
            }
        }

        public int Key
        {
            get { return ((int)Area * 100000) + Index; }
        }
    }

    internal static class SimAddress
    {
        public const int InputCount = 384;
        public const int OutputCount = 384;
        public const int AuxiliaryCount = 2048;
        public const int SpecialCount = 128;
        public const int VariableCount = 256;
        public const int WordCount = 2048;

        public static bool TryParseBit(string text, out SimBitRef result)
        {
            result = SimBitRef.Invalid;
            if (string.IsNullOrEmpty(text)) return false;

            string value = text.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (value.Length < 2) return false;

            string prefix;
            string digits;
            if (value.StartsWith("SC")) { prefix = "SC"; digits = value.Substring(2); }
            else { prefix = value.Substring(0, 1); digits = value.Substring(1); }

            int number;
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return false;
            if (number < 1) return false;

            SimBitArea area;
            int limit;
            if (prefix == "X") { area = SimBitArea.Input; limit = InputCount; }
            else if (prefix == "Y") { area = SimBitArea.Output; limit = OutputCount; }
            else if (prefix == "C") { area = SimBitArea.Auxiliary; limit = AuxiliaryCount; }
            else if (prefix == "SC") { area = SimBitArea.Special; limit = SpecialCount; }
            else if (prefix == "V") { area = SimBitArea.Variable; limit = VariableCount; }
            else return false;

            if (number > limit) return false;

            result.Area = area;
            result.Index = number;
            result.Valid = true;
            return true;
        }

        public static bool TryParseWord(string text, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(text)) return false;

            string value = text.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (!value.StartsWith("D") || value.Length < 2) return false;

            int number;
            if (!int.TryParse(value.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return false;
            if (number < 1 || number > WordCount) return false;

            index = number;
            return true;
        }

        public static string Format(SimBitRef reference)
        {
            if (!reference.Valid) return "?";
            if (reference.Area == SimBitArea.Special) return "SC" + reference.Index.ToString("000", CultureInfo.InvariantCulture);
            return Prefix(reference.Area) + reference.Index.ToString("0000", CultureInfo.InvariantCulture);
        }

        public static string Prefix(SimBitArea area)
        {
            if (area == SimBitArea.Input) return "X";
            if (area == SimBitArea.Output) return "Y";
            if (area == SimBitArea.Auxiliary) return "C";
            if (area == SimBitArea.Special) return "SC";
            return "V";
        }

        public static string AreaText(SimBitArea area)
        {
            if (area == SimBitArea.Input) return "Entrada";
            if (area == SimBitArea.Output) return "Saída";
            if (area == SimBitArea.Auxiliary) return "Auxiliar";
            if (area == SimBitArea.Special) return "Especial";
            return "TMR/CNT";
        }

        public static int Capacity(SimBitArea area)
        {
            if (area == SimBitArea.Input) return InputCount;
            if (area == SimBitArea.Output) return OutputCount;
            if (area == SimBitArea.Auxiliary) return AuxiliaryCount;
            if (area == SimBitArea.Special) return SpecialCount;
            return VariableCount;
        }
    }

    /// <summary>
    /// Imagem de processo do PLC virtual. Guarda bits por área e palavras de dados.
    /// Não conhece interface gráfica nem protocolo de comunicação.
    /// </summary>
    internal sealed class PlcProcessImage
    {
        private readonly bool[] inputs = new bool[SimAddress.InputCount + 1];
        private readonly bool[] outputs = new bool[SimAddress.OutputCount + 1];
        private readonly bool[] auxiliary = new bool[SimAddress.AuxiliaryCount + 1];
        private readonly bool[] special = new bool[SimAddress.SpecialCount + 1];
        private readonly bool[] variableDone = new bool[SimAddress.VariableCount + 1];
        private readonly int[] variableValue = new int[SimAddress.VariableCount + 1];
        private readonly int[] words = new int[SimAddress.WordCount + 1];

        public bool GetBit(SimBitRef reference)
        {
            if (!reference.Valid) return false;
            bool[] bank = BankOf(reference.Area);
            if (reference.Index < 1 || reference.Index >= bank.Length) return false;
            return bank[reference.Index];
        }

        public void SetBit(SimBitRef reference, bool value)
        {
            if (!reference.Valid) return;
            bool[] bank = BankOf(reference.Area);
            if (reference.Index < 1 || reference.Index >= bank.Length) return;
            bank[reference.Index] = value;
        }

        public bool GetBit(SimBitArea area, int index)
        {
            bool[] bank = BankOf(area);
            if (index < 1 || index >= bank.Length) return false;
            return bank[index];
        }

        public void SetBit(SimBitArea area, int index, bool value)
        {
            bool[] bank = BankOf(area);
            if (index < 1 || index >= bank.Length) return;
            bank[index] = value;
        }

        public int GetWord(int index)
        {
            if (index < 1 || index >= words.Length) return 0;
            return words[index];
        }

        public void SetWord(int index, int value)
        {
            if (index < 1 || index >= words.Length) return;
            words[index] = value;
        }

        public int GetVariableValue(int index)
        {
            if (index < 1 || index >= variableValue.Length) return 0;
            return variableValue[index];
        }

        public void SetVariableValue(int index, int value)
        {
            if (index < 1 || index >= variableValue.Length) return;
            variableValue[index] = value;
        }

        public void ClearAll()
        {
            Array.Clear(inputs, 0, inputs.Length);
            ClearLogic();
        }

        /// <summary>
        /// Zera tudo que pertence ao PLC, preservando as entradas escritas pelo processo e pelo operador.
        /// </summary>
        public void ClearLogic()
        {
            Array.Clear(outputs, 0, outputs.Length);
            Array.Clear(auxiliary, 0, auxiliary.Length);
            Array.Clear(special, 0, special.Length);
            Array.Clear(variableDone, 0, variableDone.Length);
            Array.Clear(variableValue, 0, variableValue.Length);
            Array.Clear(words, 0, words.Length);
        }

        private bool[] BankOf(SimBitArea area)
        {
            if (area == SimBitArea.Input) return inputs;
            if (area == SimBitArea.Output) return outputs;
            if (area == SimBitArea.Auxiliary) return auxiliary;
            if (area == SimBitArea.Special) return special;
            return variableDone;
        }
    }

    /// <summary>
    /// Botoeiras e chaves de campo. Representam as entradas que nenhuma planta escreve.
    /// São reafirmadas em toda varredura, como faria a leitura física das entradas, para que
    /// liberar um forçamento devolva o valor real do campo em vez de congelar o último valor.
    /// </summary>
    internal sealed class SimFieldInputs
    {
        private readonly Dictionary<int, bool> values = new Dictionary<int, bool>();
        private readonly Dictionary<int, SimBitRef> references = new Dictionary<int, SimBitRef>();

        public void Set(SimBitRef reference, bool value)
        {
            if (!reference.Valid) return;
            values[reference.Key] = value;
            references[reference.Key] = reference;
        }

        public bool Get(SimBitRef reference)
        {
            bool value;
            return reference.Valid && values.TryGetValue(reference.Key, out value) && value;
        }

        public void Clear()
        {
            values.Clear();
            references.Clear();
        }

        public void Apply(PlcProcessImage image)
        {
            foreach (KeyValuePair<int, SimBitRef> item in references)
                image.SetBit(item.Value, values[item.Key]);
        }
    }

    /// <summary>
    /// Tabela de forçamento. Entradas forçadas sobrescrevem o processo antes da varredura;
    /// saídas e auxiliares forçados sobrescrevem a lógica depois da varredura.
    /// </summary>
    internal sealed class SimForceTable
    {
        private readonly Dictionary<int, bool> values = new Dictionary<int, bool>();
        private readonly Dictionary<int, SimBitRef> references = new Dictionary<int, SimBitRef>();

        public int Count { get { return values.Count; } }

        public void Force(SimBitRef reference, bool value)
        {
            if (!reference.Valid) return;
            values[reference.Key] = value;
            references[reference.Key] = reference;
        }

        public void Release(SimBitRef reference)
        {
            if (!reference.Valid) return;
            values.Remove(reference.Key);
            references.Remove(reference.Key);
        }

        public void ReleaseAll()
        {
            values.Clear();
            references.Clear();
        }

        public bool IsForced(SimBitRef reference)
        {
            return reference.Valid && values.ContainsKey(reference.Key);
        }

        public bool TryGet(SimBitRef reference, out bool value)
        {
            value = false;
            return reference.Valid && values.TryGetValue(reference.Key, out value);
        }

        public void Apply(PlcProcessImage image, SimBitArea area)
        {
            foreach (KeyValuePair<int, SimBitRef> item in references)
            {
                if (item.Value.Area != area) continue;
                image.SetBit(item.Value, values[item.Key]);
            }
        }

        public IList<string> Describe()
        {
            List<string> list = new List<string>();
            foreach (KeyValuePair<int, SimBitRef> item in references)
                list.Add(SimAddress.Format(item.Value) + " = " + (values[item.Key] ? "1" : "0"));
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }

    /// <summary>
    /// Contatos especiais do PLC virtual. A correspondência com o mapa real do TP02
    /// ainda depende da pesquisa registrada em docs/TP02_OPCODE_RESEARCH.md.
    /// </summary>
    internal static class SpecialContacts
    {
        public const int AlwaysOn = 1;
        public const int AlwaysOff = 2;
        public const int Clock100ms = 3;
        public const int Clock1s = 4;
        public const int Clock1min = 5;
        public const int FirstScan = 6;

        public static void Update(PlcProcessImage image, double totalMs, bool firstScan)
        {
            image.SetBit(SimBitArea.Special, AlwaysOn, true);
            image.SetBit(SimBitArea.Special, AlwaysOff, false);
            image.SetBit(SimBitArea.Special, Clock100ms, Phase(totalMs, 100.0));
            image.SetBit(SimBitArea.Special, Clock1s, Phase(totalMs, 1000.0));
            image.SetBit(SimBitArea.Special, Clock1min, Phase(totalMs, 60000.0));
            image.SetBit(SimBitArea.Special, FirstScan, firstScan);
        }

        public static string Describe(int index)
        {
            if (index == AlwaysOn) return "sempre ligado";
            if (index == AlwaysOff) return "sempre desligado";
            if (index == Clock100ms) return "pulso de 0,1 s";
            if (index == Clock1s) return "pulso de 1 s";
            if (index == Clock1min) return "pulso de 1 min";
            if (index == FirstScan) return "primeira varredura";
            return string.Empty;
        }

        private static bool Phase(double totalMs, double periodMs)
        {
            double position = totalMs % periodMs;
            return position < (periodMs / 2.0);
        }
    }

    internal sealed class CompiledCondition
    {
        public UniversalElementKind Kind;
        public SimBitRef Bit;
        public bool Resolved;
        public bool LastValue;
    }

    internal sealed class CompiledOutput
    {
        public UniversalElementKind Kind;
        public SimBitRef Bit;
        public int VariableIndex;
        public int PresetLiteral = -1;
        public int PresetWord;
        public bool Retentive;
        public string FunctionCode = string.Empty;
        public bool PreviousPower;
        public bool Executable = true;
    }

    internal sealed class CompiledRung
    {
        public readonly CompiledCondition[] Series = new CompiledCondition[LadderScanEngine.ConditionColumns];
        public readonly CompiledCondition[] Parallel = new CompiledCondition[LadderScanEngine.ConditionColumns];
        public CompiledOutput Output;
        public int Number;
        public bool LastPower;
        public bool Reached;
    }

    internal sealed class LadderScanResult
    {
        public int RungsExecuted;
        public bool EndReached;
        public double DurationMs;
    }

    /// <summary>
    /// Motor de varredura do PLC virtual.
    ///
    /// Ciclo: aplicar forçamentos de entrada, atualizar contatos especiais, resolver os rungs
    /// na ordem do programa e aplicar forçamentos de saída. Não depende de WinForms nem de driver.
    /// </summary>
    internal sealed class LadderScanEngine
    {
        /// <summary>Colunas de condição do rung. A última coluna do editor é reservada à saída.</summary>
        public const int ConditionColumns = 7;

        /// <summary>Base de tempo dos temporizadores: uma unidade de preset equivale a 100 ms.</summary>
        public const double TimeBaseMs = 100.0;

        public const int MaxCounterValue = 65535;

        private readonly PlcProcessImage image = new PlcProcessImage();
        private readonly SimForceTable forces = new SimForceTable();
        private readonly SimFieldInputs field = new SimFieldInputs();
        private readonly List<CompiledRung> rungs = new List<CompiledRung>();
        private readonly List<string> diagnostics = new List<string>();
        private readonly double[] timerElapsedMs = new double[SimAddress.VariableCount + 1];

        private string programName = "Sem programa";
        private double totalMs;
        private long scanCount;
        private bool firstScan = true;

        public PlcProcessImage Image { get { return image; } }
        public SimForceTable Forces { get { return forces; } }
        public SimFieldInputs Field { get { return field; } }
        public IList<CompiledRung> Rungs { get { return rungs.AsReadOnly(); } }
        public IList<string> Diagnostics { get { return diagnostics.AsReadOnly(); } }
        public string ProgramName { get { return programName; } }
        public long ScanCount { get { return scanCount; } }
        public double TotalMilliseconds { get { return totalMs; } }
        public int RungCount { get { return rungs.Count; } }

        public bool Load(UniversalLadderProgram program)
        {
            rungs.Clear();
            diagnostics.Clear();
            Reset();

            if (program == null)
            {
                diagnostics.Add("Nenhum programa fornecido ao simulador.");
                programName = "Sem programa";
                return false;
            }

            programName = string.IsNullOrEmpty(program.Name) ? "Sem nome" : program.Name;

            bool hasEnd = false;
            for (int i = 0; i < program.Rungs.Count; i++)
            {
                CompiledRung compiled = CompileRung(program.Rungs[i], i + 1);
                rungs.Add(compiled);
                if (compiled.Output != null && compiled.Output.Kind == UniversalElementKind.End) hasEnd = true;
            }

            if (rungs.Count == 0) diagnostics.Add("O programa não possui rungs.");
            else if (!hasEnd) diagnostics.Add("Programa sem END: todos os rungs serão executados em cada varredura.");

            return rungs.Count > 0;
        }

        private CompiledRung CompileRung(UniversalLadderRung source, int number)
        {
            CompiledRung rung = new CompiledRung();
            rung.Number = number;
            if (source == null) return rung;

            CompileLane(source.Series, rung, number, false);
            CompileLane(source.Parallel, rung, number, true);

            if (rung.Output != null && (rung.Output.Kind == UniversalElementKind.RisingEdge || rung.Output.Kind == UniversalElementKind.FallingEdge))
                diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": borda sem bobina associada; o pulso é calculado mas não escreve em memória.");

            return rung;
        }

        private void CompileLane(IList<UniversalLadderElement> lane, CompiledRung rung, int number, bool parallel)
        {
            if (lane == null) return;

            for (int i = 0; i < lane.Count; i++)
            {
                UniversalLadderElement element = lane[i];
                if (element == null) continue;

                int column = element.Column;
                if (column < 0 || column > ConditionColumns) column = Math.Min(Math.Max(i, 0), ConditionColumns);

                if (IsOutputKind(element.Kind))
                {
                    if (parallel)
                    {
                        diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": saída em ramo paralelo não é executada.");
                        continue;
                    }
                    if (rung.Output != null)
                    {
                        diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": mais de uma saída; apenas a primeira é executada.");
                        continue;
                    }
                    rung.Output = CompileOutput(element, number);
                    continue;
                }

                if (column >= ConditionColumns)
                {
                    diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": condição na coluna de saída foi ignorada.");
                    continue;
                }

                CompiledCondition condition = new CompiledCondition();
                condition.Kind = element.Kind;
                SimBitRef bit;
                condition.Resolved = SimAddress.TryParseBit(element.Address, out bit);
                condition.Bit = bit;
                if (!condition.Resolved)
                    diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ", coluna " + (column + 1).ToString(CultureInfo.InvariantCulture) +
                                    ": endereço \"" + (element.Address ?? string.Empty) + "\" não reconhecido; o bit é lido como 0.");

                if (parallel) rung.Parallel[column] = condition;
                else rung.Series[column] = condition;
            }
        }

        private CompiledOutput CompileOutput(UniversalLadderElement element, int number)
        {
            CompiledOutput output = new CompiledOutput();
            output.Kind = element.Kind;
            output.FunctionCode = element.FunctionCode ?? string.Empty;

            if (element.Kind == UniversalElementKind.End) return output;

            if (element.Kind == UniversalElementKind.Function)
            {
                output.Executable = false;
                diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": função " +
                                (string.IsNullOrEmpty(element.Address) ? output.FunctionCode : element.Address) +
                                " ainda não é executada pelo simulador.");
                return output;
            }

            if (element.Kind == UniversalElementKind.RisingEdge || element.Kind == UniversalElementKind.FallingEdge)
                return output;

            SimBitRef bit;
            if (!SimAddress.TryParseBit(element.Address, out bit))
            {
                output.Executable = false;
                diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": saída com endereço \"" +
                                (element.Address ?? string.Empty) + "\" não reconhecido.");
                return output;
            }

            output.Bit = bit;

            if (element.Kind == UniversalElementKind.Timer || element.Kind == UniversalElementKind.Counter)
            {
                if (bit.Area != SimBitArea.Variable)
                {
                    output.Executable = false;
                    diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": TMR/CNT exigem identificador V0001–V0256.");
                    return output;
                }

                output.VariableIndex = bit.Index;
                output.Retentive = string.Equals(element.FunctionCode, "RESET", StringComparison.OrdinalIgnoreCase);

                int literal;
                int word;
                if (int.TryParse(element.Parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out literal))
                {
                    output.PresetLiteral = Math.Max(0, literal);
                }
                else if (SimAddress.TryParseWord(element.Parameter, out word))
                {
                    output.PresetWord = word;
                }
                else
                {
                    output.Executable = false;
                    diagnostics.Add("Rung " + number.ToString(CultureInfo.InvariantCulture) + ": preset \"" +
                                    (element.Parameter ?? string.Empty) + "\" inválido.");
                }
            }

            return output;
        }

        public void Reset()
        {
            image.ClearLogic();
            Array.Clear(timerElapsedMs, 0, timerElapsedMs.Length);
            totalMs = 0.0;
            scanCount = 0;
            firstScan = true;

            for (int i = 0; i < rungs.Count; i++)
            {
                rungs[i].LastPower = false;
                rungs[i].Reached = false;
                if (rungs[i].Output != null) rungs[i].Output.PreviousPower = false;
            }
        }

        public LadderScanResult Execute(double elapsedMs)
        {
            if (elapsedMs < 0.0) elapsedMs = 0.0;

            LadderScanResult result = new LadderScanResult();
            result.DurationMs = elapsedMs;

            totalMs += elapsedMs;
            scanCount++;

            field.Apply(image);
            forces.Apply(image, SimBitArea.Input);
            SpecialContacts.Update(image, totalMs, firstScan);

            for (int i = 0; i < rungs.Count; i++)
            {
                CompiledRung rung = rungs[i];
                rung.Reached = true;

                if (rung.Output != null && rung.Output.Kind == UniversalElementKind.End)
                {
                    result.EndReached = true;
                    result.RungsExecuted = i + 1;
                    break;
                }

                bool power = SolvePower(rung);
                rung.LastPower = power;
                ApplyOutput(rung, power, elapsedMs);
                result.RungsExecuted = i + 1;
            }

            for (int i = result.RungsExecuted; i < rungs.Count; i++) rungs[i].Reached = false;

            forces.Apply(image, SimBitArea.Output);
            forces.Apply(image, SimBitArea.Auxiliary);
            forces.Apply(image, SimBitArea.Variable);

            firstScan = false;
            return result;
        }

        private bool SolvePower(CompiledRung rung)
        {
            bool power = true;
            for (int c = 0; c < ConditionColumns; c++)
            {
                CompiledCondition series = rung.Series[c];
                CompiledCondition parallel = rung.Parallel[c];

                if (series == null && parallel == null) continue;

                bool value;
                if (parallel == null) value = Evaluate(series);
                else if (series == null) value = Evaluate(parallel);
                else value = Evaluate(series) | Evaluate(parallel);

                if (!value) power = false;
            }
            return power;
        }

        private bool Evaluate(CompiledCondition condition)
        {
            if (condition == null) return true;

            bool state = condition.Resolved && image.GetBit(condition.Bit);
            bool value = condition.Kind == UniversalElementKind.ContactNC ? !state : state;
            condition.LastValue = value;
            return value;
        }

        private void ApplyOutput(CompiledRung rung, bool power, double elapsedMs)
        {
            CompiledOutput output = rung.Output;
            if (output == null || !output.Executable) return;

            switch (output.Kind)
            {
                case UniversalElementKind.Coil:
                    image.SetBit(output.Bit, power);
                    break;

                case UniversalElementKind.Set:
                    if (power) image.SetBit(output.Bit, true);
                    break;

                case UniversalElementKind.Reset:
                    if (power) ApplyReset(output.Bit);
                    break;

                case UniversalElementKind.Timer:
                    ApplyTimer(output, power, elapsedMs);
                    break;

                case UniversalElementKind.Counter:
                    ApplyCounter(output, power);
                    break;

                case UniversalElementKind.RisingEdge:
                case UniversalElementKind.FallingEdge:
                    break;
            }

            output.PreviousPower = power;
        }

        private void ApplyReset(SimBitRef bit)
        {
            image.SetBit(bit, false);
            if (bit.Area == SimBitArea.Variable)
            {
                timerElapsedMs[bit.Index] = 0.0;
                image.SetVariableValue(bit.Index, 0);
            }
        }

        private void ApplyTimer(CompiledOutput output, bool power, double elapsedMs)
        {
            int index = output.VariableIndex;
            int preset = ResolvePreset(output);
            double limitMs = preset * TimeBaseMs;

            if (power)
            {
                timerElapsedMs[index] += elapsedMs;
                if (timerElapsedMs[index] > limitMs) timerElapsedMs[index] = limitMs;
            }
            else if (!output.Retentive)
            {
                timerElapsedMs[index] = 0.0;
            }

            bool done = (output.Retentive || power) && timerElapsedMs[index] >= limitMs;
            image.SetVariableValue(index, (int)(timerElapsedMs[index] / TimeBaseMs));
            image.SetBit(SimBitArea.Variable, index, done);
        }

        private void ApplyCounter(CompiledOutput output, bool power)
        {
            int index = output.VariableIndex;
            int preset = ResolvePreset(output);

            if (power && !output.PreviousPower)
            {
                int value = image.GetVariableValue(index);
                if (value < MaxCounterValue) image.SetVariableValue(index, value + 1);
            }

            image.SetBit(SimBitArea.Variable, index, image.GetVariableValue(index) >= preset && preset > 0);
        }

        private int ResolvePreset(CompiledOutput output)
        {
            if (output.PresetLiteral >= 0) return output.PresetLiteral;
            if (output.PresetWord > 0) return Math.Max(0, image.GetWord(output.PresetWord));
            return 0;
        }

        public int TimerValue(int variableIndex)
        {
            return image.GetVariableValue(variableIndex);
        }

        public static bool IsOutputKind(UniversalElementKind kind)
        {
            return kind == UniversalElementKind.Coil || kind == UniversalElementKind.Set || kind == UniversalElementKind.Reset ||
                   kind == UniversalElementKind.Timer || kind == UniversalElementKind.Counter || kind == UniversalElementKind.Function ||
                   kind == UniversalElementKind.RisingEdge || kind == UniversalElementKind.FallingEdge || kind == UniversalElementKind.End;
        }

        public string DescribeProgram()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Programa: ").Append(programName).Append("\r\n");
            text.Append("Rungs: ").Append(rungs.Count.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            if (diagnostics.Count == 0) text.Append("Sem avisos de compilação.");
            else
            {
                text.Append("Avisos:\r\n");
                for (int i = 0; i < diagnostics.Count; i++) text.Append("  - ").Append(diagnostics[i]).Append("\r\n");
            }
            return text.ToString();
        }
    }
}
