using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    // Reconstrução IL -> Ladder do TP02. Extraída sem mudança de regra de TP02IlToLadder.cs
    // (Desktop) para o núcleo, para que o editor legado e a trilha moderna usem a mesma
    // conversão. O namespace foi mantido para não tocar o código legado que a consome.
    // C# 5: este arquivo também é compilado pelo csc do .NET Framework.

    internal sealed class TP02IlInstruction
    {
        public int Step;
        public string Operation = string.Empty;
        public string Operand = string.Empty;
        public string Source = string.Empty;
    }

    internal sealed class TP02LadderBuildRung
    {
        public readonly List<LadderProjectElement> Conditions = new List<LadderProjectElement>();
        public LadderProjectElement Output;
    }

    internal sealed class TP02IlConversion
    {
        public readonly List<TP02LadderBuildRung> Rungs = new List<TP02LadderBuildRung>();
        public readonly List<string> Errors = new List<string>();
        public bool Success { get { return Errors.Count == 0; } }
    }

    internal static class TP02IlToLadderConverter
    {
        /// <summary>Lê as linhas "NNNN: OPERAÇÃO OPERANDO" de uma IL verificada; ignora vazias e comentários.</summary>
        public static List<TP02IlInstruction> Parse(string[] lines)
        {
            List<TP02IlInstruction> instructions = new List<TP02IlInstruction>();
            Regex rx = new Regex(@"^\s*(\d{4})\s*:\s*([^;]+)");
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";")) continue;
                Match m = rx.Match(line);
                if (!m.Success) continue;
                int step;
                if (!int.TryParse(m.Groups[1].Value, out step)) continue;
                string body = m.Groups[2].Value.Trim();
                TP02IlInstruction ins = ParseInstructionBody(step, body, line);
                instructions.Add(ins);
            }
            return instructions;
        }

        public static TP02IlInstruction ParseInstructionBody(int step, string body, string source)
        {
            TP02IlInstruction x = new TP02IlInstruction();
            x.Step = step;
            x.Source = source;
            string upper = body.ToUpperInvariant().Trim();
            string[] multi = new string[] { "STR NOT", "AND NOT", "OR NOT" };
            int i;
            for (i = 0; i < multi.Length; i++)
            {
                if (upper == multi[i] || upper.StartsWith(multi[i] + " "))
                {
                    x.Operation = multi[i];
                    x.Operand = upper.Length > multi[i].Length ? upper.Substring(multi[i].Length).Trim() : string.Empty;
                    return x;
                }
            }
            int space = upper.IndexOf(' ');
            if (space < 0)
            {
                x.Operation = upper;
                return x;
            }
            x.Operation = upper.Substring(0, space).Trim();
            x.Operand = upper.Substring(space + 1).Trim();
            return x;
        }

        /// <summary>
        /// Reconstrói linhas Ladder a partir da IL. Só aceita STR, STR NOT, AND, AND NOT, OUT e END;
        /// qualquer outra coisa (ou UNKNOWN/RAW) entra em Errors e bloqueia a conversão.
        /// </summary>
        public static TP02IlConversion Convert(IList<TP02IlInstruction> instructions)
        {
            TP02IlConversion result = new TP02IlConversion();
            List<TP02LadderBuildRung> rungs = result.Rungs;
            List<string> errors = result.Errors;
            TP02LadderBuildRung current = null;
            int i;
            for (i = 0; i < instructions.Count; i++)
            {
                TP02IlInstruction ins = instructions[i];
                string op = ins.Operation;

                if (op == "UNKNOWN" || ins.Operand.StartsWith("RAW=", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Passo " + ins.Step.ToString("0000") + ": instrução/operando não comprovado: " + ins.Source);
                    continue;
                }

                if (op == "STR" || op == "STR NOT")
                {
                    if (current != null && current.Output == null)
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": novo STR antes de fechar o linha anterior com OUT.");
                    current = new TP02LadderBuildRung();
                    current.Conditions.Add(NewElement(
                        op == "STR NOT" ? LadderProjectElementKind.ContactNormallyClosed : LadderProjectElementKind.ContactNormallyOpen,
                        ins.Operand));
                    rungs.Add(current);
                    continue;
                }

                if (op == "AND" || op == "AND NOT")
                {
                    if (current == null)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": " + op + " sem STR inicial.");
                        continue;
                    }
                    if (current.Conditions.Count >= 7)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": mais de 7 condições em série; o formato gráfico atual usa 7 colunas de condição.");
                        continue;
                    }
                    current.Conditions.Add(NewElement(
                        op == "AND NOT" ? LadderProjectElementKind.ContactNormallyClosed : LadderProjectElementKind.ContactNormallyOpen,
                        ins.Operand));
                    continue;
                }

                if (op == "OUT")
                {
                    if (current == null)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": OUT sem linha iniciado por STR.");
                        continue;
                    }
                    if (current.Output != null)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": linha já possui saída.");
                        continue;
                    }
                    current.Output = NewElement(LadderProjectElementKind.Coil, ins.Operand);
                    current = null;
                    continue;
                }

                if (op == "END")
                {
                    TP02LadderBuildRung end = new TP02LadderBuildRung();
                    end.Output = NewElement(LadderProjectElementKind.End, "F-00");
                    rungs.Add(end);
                    current = null;
                    continue;
                }

                errors.Add("Passo " + ins.Step.ToString("0000") + ": operação ainda não suportada pela reconstrução segura: " + op + ".");
            }

            if (current != null && current.Output == null) errors.Add("A última linha não foi fechada com OUT.");
            return result;
        }

        public static LadderProjectDocument BuildDocument(List<TP02LadderBuildRung> source)
        {
            LadderProjectDocument document = new LadderProjectDocument();
            for (int rungIndex = 0; rungIndex < source.Count; rungIndex++)
            {
                TP02LadderBuildRung sourceRung = source[rungIndex];
                LadderProjectRung targetRung = new LadderProjectRung();
                for (int column = 0; column < 7 && column < sourceRung.Conditions.Count; column++)
                    targetRung.Series[column] = sourceRung.Conditions[column];

                if (sourceRung.Output != null) targetRung.Series[7] = sourceRung.Output;
                document.Rungs.Add(targetRung);
            }
            return document;
        }

        public static string BuildPladder(List<TP02LadderBuildRung> source)
        {
            return LadderProjectCodec.Serialize(BuildDocument(source));
        }

        private static LadderProjectElement NewElement(LadderProjectElementKind kind, string address)
        {
            LadderProjectElement element = new LadderProjectElement();
            element.Kind = kind;
            element.Address = address == null ? string.Empty : address.Trim().ToUpperInvariant();
            return element;
        }
    }
}
