using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenLadderStudio.Core
{
    internal sealed class Tp02CompilationTrace
    {
        public int Rung;
        public int Step;
        public string BooleanText = string.Empty;
        public string MachineHex = string.Empty;
    }

    internal sealed class Tp02LadderCompilationResult
    {
        public readonly List<Tp02MachineWord> Words = new List<Tp02MachineWord>();
        public readonly List<Tp02CompilationTrace> Trace = new List<Tp02CompilationTrace>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool Success { get { return Errors.Count == 0; } }

        public string BuildReport()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("OpenLadder Studio - pré-compilação TP02 (DRY-RUN)");
            text.AppendLine(new string('=', 66));
            text.AppendLine("Nenhum byte foi transmitido ao PLC.");
            text.AppendLine();

            if (Errors.Count > 0)
            {
                text.AppendLine("ERROS");
                int i;
                for (i = 0; i < Errors.Count; i++) text.AppendLine("- " + Errors[i]);
                text.AppendLine();
            }

            if (Warnings.Count > 0)
            {
                text.AppendLine("AVISOS");
                int i;
                for (i = 0; i < Warnings.Count; i++) text.AppendLine("- " + Warnings[i]);
                text.AppendLine();
            }

            text.AppendLine("BOOLEAN / IL -> PALAVRAS TP02");
            int t;
            for (t = 0; t < Trace.Count; t++)
            {
                Tp02CompilationTrace row = Trace[t];
                text.Append("R");
                text.Append(row.Rung.ToString("000", CultureInfo.InvariantCulture));
                text.Append("  ");
                text.Append(row.Step.ToString("0000", CultureInfo.InvariantCulture));
                text.Append("  ");
                text.Append(row.MachineHex.PadRight(8));
                text.Append("  ");
                text.AppendLine(row.BooleanText);
            }

            text.AppendLine();
            text.AppendLine("Total: " + Words.Count.ToString(CultureInfo.InvariantCulture) + " passo(s).");
            return text.ToString();
        }
    }

    /// <summary>
    /// Ponte entre o formato Ladder universal do OpenLadder Studio e o encoder TP02.
    ///
    /// Escopo deliberadamente conservador:
    /// - série de contatos NA/NF;
    /// - um contato paralelo por coluna (ramo em torno da própria coluna);
    /// - OUT, TMR, CNT, SET, RESET, bordas F-05/F-06, END e F-xx mapeadas;
    /// - geração de quadros WBP apenas em dry-run.
    ///
    /// A classe não conhece SerialPort e nunca transmite ao controlador.
    /// </summary>
    internal static class Tp02LadderTargetCompiler
    {
        public static Tp02LadderCompilationResult Compile(LadderProjectDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");

            Tp02LadderCompilationResult result = new Tp02LadderCompilationResult();
            int rung;
            for (rung = 0; rung < document.Rungs.Count; rung++)
            {
                LadderProjectRung source = document.Rungs[rung];
                if (source == null)
                {
                    result.Errors.Add("Rung " + (rung + 1).ToString(CultureInfo.InvariantCulture) + ": rung nulo.");
                    continue;
                }
                CompileRung(source, rung + 1, result);
            }

            if (result.Words.Count > 4001)
                result.Errors.Add("Programa excede os 4001 endereços de passo (0000-4000) do TP02-40MR/60MR.");

            return result;
        }

        public static List<string> BuildWbpDryRunFrames(Tp02LadderCompilationResult result, int station, int start, int responseCode)
        {
            if (result == null) throw new ArgumentNullException("result");
            if (!result.Success) throw new InvalidOperationException("O programa contém erros e não pode gerar WBP dry-run.");
            if (start < 0 || start > 4000) throw new ArgumentOutOfRangeException("start");
            if (result.Words.Count == 0) throw new InvalidOperationException("Programa sem passos compilados.");
            if (start + result.Words.Count - 1 > 4000) throw new ArgumentOutOfRangeException("start", "O programa ultrapassa o passo 4000.");

            List<string> frames = new List<string>();
            int offset = 0;
            while (offset < result.Words.Count)
            {
                int count = Math.Min(100, result.Words.Count - offset);
                List<Tp02MachineWord> block = new List<Tp02MachineWord>(count);
                int i;
                for (i = 0; i < count; i++) block.Add(result.Words[offset + i]);
                frames.Add(Tp02TargetCompiler.BuildWbpDryRun(station, start + offset, block, responseCode));
                offset += count;
            }
            return frames;
        }

        private static void CompileRung(LadderProjectRung rung, int rungNumber, Tp02LadderCompilationResult result)
        {
            bool hasAny = false;
            int c;
            for (c = 0; c < LadderProjectRung.ColumnCount; c++)
            {
                if (!IsEmpty(rung.Series[c]) || !IsEmpty(rung.Parallel[c])) hasAny = true;
            }
            if (!hasAny) return;

            LadderProjectElement terminal = rung.Series[LadderProjectRung.ColumnCount - 1];
            if (!IsOutput(terminal))
            {
                result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ": a coluna 8 precisa conter uma saída/instrução terminal.");
                return;
            }
            if (!IsEmpty(rung.Parallel[LadderProjectRung.ColumnCount - 1]))
            {
                result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ": a coluna de saída não aceita ramo paralelo.");
                return;
            }

            for (c = 0; c < LadderProjectRung.ColumnCount - 1; c++)
            {
                LadderProjectElement series = rung.Series[c];
                LadderProjectElement parallel = rung.Parallel[c];
                if (IsEmpty(series) && IsEmpty(parallel)) continue;
                if (!IsContact(series))
                {
                    result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ", coluna " + (c + 1).ToString(CultureInfo.InvariantCulture) + ": somente contato NA/NF pode aparecer antes da saída.");
                    return;
                }
                if (!IsEmpty(parallel) && !IsContact(parallel))
                {
                    result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ", coluna " + (c + 1).ToString(CultureInfo.InvariantCulture) + ": ramo paralelo aceita somente contato NA/NF.");
                    return;
                }
                if (IsEmpty(series) && !IsEmpty(parallel))
                {
                    result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ", coluna " + (c + 1).ToString(CultureInfo.InvariantCulture) + ": contato paralelo sem contato principal não possui topologia definida.");
                    return;
                }
            }

            bool hasCondition = false;
            for (c = 0; c < LadderProjectRung.ColumnCount - 1; c++)
            {
                LadderProjectElement series = rung.Series[c];
                LadderProjectElement parallel = rung.Parallel[c];
                if (IsEmpty(series)) continue;

                if (!hasCondition)
                {
                    if (!EmitContact(series, "STR", "STR NOT", rungNumber, result)) return;
                    hasCondition = true;
                    if (!IsEmpty(parallel) && !EmitContact(parallel, "OR", "OR NOT", rungNumber, result)) return;
                }
                else if (IsEmpty(parallel))
                {
                    if (!EmitContact(series, "AND", "AND NOT", rungNumber, result)) return;
                }
                else
                {
                    // A coluna representa (principal OR paralelo). O TP02 usa a pilha
                    // booleana: STR principal, OR paralelo, AND STR com o resultado anterior.
                    if (!EmitContact(series, "STR", "STR NOT", rungNumber, result)) return;
                    if (!EmitContact(parallel, "OR", "OR NOT", rungNumber, result)) return;
                    AddWord(rungNumber, "AND STR", Tp02TargetCompiler.EncodeBitInstruction("AND STR", "X", 1), result);
                }
            }

            if (!hasCondition && terminal.Kind != LadderProjectElementKind.End)
            {
                result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ": saída sem condição de entrada ainda não é emitida automaticamente para o TP02.");
                return;
            }

            EmitTerminal(terminal, rungNumber, result);
        }

        private static bool EmitContact(LadderProjectElement element, string normalOp, string notOp, int rungNumber, Tp02LadderCompilationResult result)
        {
            string device;
            int number;
            if (!TryParseDeviceAddress(element.Address, out device, out number))
            {
                result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ": endereço de contato inválido: " + element.Address + ".");
                return false;
            }
            if (device != "X" && device != "Y" && device != "C")
            {
                result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ": família " + device + " ainda não foi mapeada para contato Boolean básico.");
                return false;
            }

            string op = element.Kind == LadderProjectElementKind.ContactNormallyClosed ? notOp : normalOp;
            try
            {
                AddWord(rungNumber, op + " " + FormatAddress(device, number), Tp02TargetCompiler.EncodeBitInstruction(op, device, number), result);
                return true;
            }
            catch (Exception ex)
            {
                result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
                return false;
            }
        }

        private static void EmitTerminal(LadderProjectElement terminal, int rungNumber, Tp02LadderCompilationResult result)
        {
            try
            {
                if (terminal.Kind == LadderProjectElementKind.Coil)
                {
                    string device;
                    int number;
                    RequireAddress(terminal.Address, out device, out number);
                    if (device != "Y" && device != "C") throw new ArgumentException("OUT aceita Y ou C, não " + device + ".");
                    AddWord(rungNumber, "OUT " + FormatAddress(device, number), Tp02TargetCompiler.EncodeBitInstruction("OUT", device, number), result);
                    return;
                }

                if (terminal.Kind == LadderProjectElementKind.Timer || terminal.Kind == LadderProjectElementKind.Counter)
                {
                    string device;
                    int number;
                    RequireAddress(terminal.Address, out device, out number);
                    if (device != "V") throw new ArgumentException("TMR/CNT exige identificador Vxxxx.");
                    if (!string.IsNullOrEmpty(terminal.Mode)) throw new ArgumentException("Modo adicional de TMR/CNT ainda não foi validado no encoder TP02: " + terminal.Mode + ".");

                    int preset;
                    if (!TryParseLiteral(terminal.Parameter, out preset))
                        throw new ArgumentException("Preset TMR/CNT precisa ser literal decimal/hex nesta etapa; registrador D ainda não foi validado.");

                    if (terminal.Kind == LadderProjectElementKind.Timer)
                    {
                        AddWord(rungNumber, "TMR " + FormatAddress("V", number), Tp02TargetCompiler.EncodeTimer(number), result);
                        AddWord(rungNumber, "K " + preset.ToString(CultureInfo.InvariantCulture), Tp02TargetCompiler.EncodeLiteral16(preset), result);
                    }
                    else
                    {
                        AddWord(rungNumber, "CNT " + FormatAddress("V", number), Tp02TargetCompiler.EncodeCounter(number), result);
                        AddWord(rungNumber, "K " + preset.ToString(CultureInfo.InvariantCulture), Tp02TargetCompiler.EncodeLiteral16(preset), result);
                    }
                    return;
                }

                if (terminal.Kind == LadderProjectElementKind.Set || terminal.Kind == LadderProjectElementKind.Reset)
                {
                    string device;
                    int number;
                    RequireAddress(terminal.Address, out device, out number);
                    if (device != "X" && device != "Y" && device != "C") throw new ArgumentException("SET/RESET requer X/Y/C no formato reconstruído.");
                    string key = terminal.Kind == LadderProjectElementKind.Set ? "F-23" : "F-24";
                    AddWord(rungNumber, key + " " + FormatAddress(device, number), Tp02TargetCompiler.EncodeFunctionPrefix(key), result);
                    AddWord(rungNumber, "  " + FormatAddress(device, number), Tp02TargetCompiler.EncodeSpecialBitOperand(device, number), result);
                    return;
                }

                if (terminal.Kind == LadderProjectElementKind.RisingEdge)
                {
                    AddWord(rungNumber, "F-05 -|^|-", Tp02TargetCompiler.EncodeFunctionPrefix("F-05"), result);
                    return;
                }
                if (terminal.Kind == LadderProjectElementKind.FallingEdge)
                {
                    AddWord(rungNumber, "F-06 -|v|-", Tp02TargetCompiler.EncodeFunctionPrefix("F-06"), result);
                    return;
                }
                if (terminal.Kind == LadderProjectElementKind.End)
                {
                    AddWord(rungNumber, "F-00 END", Tp02TargetCompiler.EncodeFunctionPrefix("F-00"), result);
                    return;
                }
                if (terminal.Kind == LadderProjectElementKind.Function)
                {
                    EmitFunction(terminal.Address, terminal.Parameter, rungNumber, result);
                    return;
                }

                throw new ArgumentException("Saída Ladder ainda não suportada pelo destino TP02: " + terminal.Kind.ToString() + ".");
            }
            catch (Exception ex)
            {
                result.Errors.Add("Rung " + rungNumber.ToString(CultureInfo.InvariantCulture) + ": " + ex.Message);
            }
        }

        private static void EmitFunction(string key, string parameterText, int rungNumber, Tp02LadderCompilationResult result)
        {
            key = (key ?? string.Empty).Trim();
            if (key.Length == 0) throw new ArgumentException("Função F-xx sem código.");

            Tp02FunctionSpec spec = Tp02TargetCompiler.GetFunction(key);
            string[] operands = SplitOperands(parameterText);

            if (string.Equals(key, "F-42", StringComparison.OrdinalIgnoreCase))
            {
                if (operands.Length != 1) throw new ArgumentException("F-42 LB exige um label.");
                int label = ParseLabel(operands[0]);
                AddWord(rungNumber, "F-42 LB" + label.ToString("000", CultureInfo.InvariantCulture), Tp02TargetCompiler.EncodeLabel(label), result);
                return;
            }
            if (string.Equals(key, "F-43", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "F-44", StringComparison.OrdinalIgnoreCase))
            {
                if (operands.Length != 1) throw new ArgumentException(key + " exige um label.");
                int label = ParseLabel(operands[0]);
                Tp02MachineWord[] words = string.Equals(key, "F-43", StringComparison.OrdinalIgnoreCase)
                    ? Tp02TargetCompiler.EncodeJump(label)
                    : Tp02TargetCompiler.EncodeCall(label);
                AddWord(rungNumber, key + " " + spec.Mnemonic + " LB" + label.ToString("000", CultureInfo.InvariantCulture), words[0], result);
                AddWord(rungNumber, "  LB" + label.ToString("000", CultureInfo.InvariantCulture), words[1], result);
                return;
            }

            if (operands.Length != spec.OperandModes.Length)
                throw new ArgumentException(key + " " + spec.Mnemonic + " exige " + spec.OperandModes.Length.ToString(CultureInfo.InvariantCulture) + " operando(s); foram informados " + operands.Length.ToString(CultureInfo.InvariantCulture) + ".");

            AddWord(rungNumber, key + " " + spec.Mnemonic, spec.Prefix, result);
            int i;
            for (i = 0; i < operands.Length; i++)
            {
                string mode = spec.OperandModes[i];
                string token = operands[i];
                Tp02MachineWord word;
                if (mode == "bit")
                {
                    string device;
                    int number;
                    RequireAddress(token, out device, out number);
                    word = Tp02TargetCompiler.EncodeSpecialBitOperand(device, number);
                }
                else if (mode == "normal")
                {
                    int literal;
                    if (TryParseLiteral(token, out literal)) word = Tp02TargetCompiler.EncodeLiteral16(literal);
                    else
                    {
                        string device;
                        int number;
                        RequireAddress(token, out device, out number);
                        word = Tp02TargetCompiler.EncodeFunctionOperand(device, number);
                    }
                }
                else
                {
                    throw new ArgumentException("Modo de operando não tratado automaticamente: " + mode + ".");
                }
                AddWord(rungNumber, "  " + token.ToUpperInvariant(), word, result);
            }
        }

        private static string[] SplitOperands(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new string[0];
            return text.Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static int ParseLabel(string value)
        {
            string text = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (text.StartsWith("LB", StringComparison.Ordinal)) text = text.Substring(2);
            int label;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out label) || label < 1 || label > 256)
                throw new ArgumentException("Label inválido: " + value + ".");
            return label;
        }

        private static bool TryParseLiteral(string value, out int number)
        {
            number = 0;
            string text = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (text.Length == 0) return false;
            if (text.StartsWith("0X", StringComparison.Ordinal))
                return int.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number) && number >= 0 && number <= 0xFFFF;
            if (text.StartsWith("H", StringComparison.Ordinal))
                return int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number) && number >= 0 && number <= 0xFFFF;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number >= 0 && number <= 0xFFFF;
        }

        private static bool TryParseDeviceAddress(string value, out string device, out int number)
        {
            device = string.Empty;
            number = 0;
            string text = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (text.Length < 2) return false;

            string[] prefixes = new string[] { "WY", "WX", "WC", "SC", "X", "Y", "C", "V", "D" };
            int i;
            for (i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!text.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string tail = text.Substring(prefix.Length);
                int parsed;
                if (tail.Length == 0 || !int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < 1) return false;
                device = prefix;
                number = parsed;
                return true;
            }
            return false;
        }

        private static void RequireAddress(string value, out string device, out int number)
        {
            if (!TryParseDeviceAddress(value, out device, out number))
                throw new ArgumentException("Endereço TP02 inválido: " + value + ".");
        }

        private static string FormatAddress(string device, int number)
        {
            if (device == "SC") return device + number.ToString("000", CultureInfo.InvariantCulture);
            return device + number.ToString("0000", CultureInfo.InvariantCulture);
        }

        private static bool IsEmpty(LadderProjectElement element)
        {
            return element == null || element.Kind == LadderProjectElementKind.Empty;
        }

        private static bool IsContact(LadderProjectElement element)
        {
            return element != null && (element.Kind == LadderProjectElementKind.ContactNormallyOpen || element.Kind == LadderProjectElementKind.ContactNormallyClosed);
        }

        private static bool IsOutput(LadderProjectElement element)
        {
            if (element == null) return false;
            return element.Kind == LadderProjectElementKind.Coil ||
                   element.Kind == LadderProjectElementKind.Timer ||
                   element.Kind == LadderProjectElementKind.Counter ||
                   element.Kind == LadderProjectElementKind.Set ||
                   element.Kind == LadderProjectElementKind.Reset ||
                   element.Kind == LadderProjectElementKind.RisingEdge ||
                   element.Kind == LadderProjectElementKind.FallingEdge ||
                   element.Kind == LadderProjectElementKind.Function ||
                   element.Kind == LadderProjectElementKind.End;
        }

        private static void AddWord(int rungNumber, string booleanText, Tp02MachineWord word, Tp02LadderCompilationResult result)
        {
            Tp02CompilationTrace trace = new Tp02CompilationTrace();
            trace.Rung = rungNumber;
            trace.Step = result.Words.Count;
            trace.BooleanText = booleanText;
            trace.MachineHex = word.ToHex();
            result.Words.Add(word);
            result.Trace.Add(trace);
        }
    }
}
