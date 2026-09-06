using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenLadderStudio.Core
{
    internal enum LadderProjectElementKind
    {
        Empty,
        ContactNormallyOpen,
        ContactNormallyClosed,
        Coil,
        Timer,
        Counter,
        Set,
        Reset,
        RisingEdge,
        FallingEdge,
        Function,
        End
    }

    internal sealed class LadderProjectElement
    {
        public LadderProjectElementKind Kind;
        public string Address;
        public string Parameter;
        public string Mode;

        public LadderProjectElement()
        {
            Kind = LadderProjectElementKind.Empty;
            Address = string.Empty;
            Parameter = string.Empty;
            Mode = string.Empty;
        }
    }

    internal sealed class LadderProjectRung
    {
        public const int ColumnCount = 8;

        public readonly LadderProjectElement[] Series;
        public readonly LadderProjectElement[] Parallel;

        public LadderProjectRung()
        {
            Series = new LadderProjectElement[ColumnCount];
            Parallel = new LadderProjectElement[ColumnCount];

            for (int column = 0; column < ColumnCount; column++)
            {
                Series[column] = new LadderProjectElement();
                Parallel[column] = new LadderProjectElement();
            }
        }
    }

    internal sealed class LadderProjectDocument
    {
        public readonly List<LadderProjectRung> Rungs = new List<LadderProjectRung>();
    }

    /// <summary>
    /// Le e grava o formato .pladder sem conhecer WinForms, arquivos ou fabricantes de PLC.
    /// A versao 1 continua aceita para manter os projetos ja existentes; novas gravacoes usam
    /// a versao 2, que preserva uma ramificacao paralela por coluna.
    /// </summary>
    internal static class LadderProjectCodec
    {
        public const string LegacyHeader = "PC12-LADDER|1";
        public const string CurrentHeader = "PC12-LADDER|2";

        public static string Serialize(LadderProjectDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");

            StringBuilder text = new StringBuilder();
            text.AppendLine(CurrentHeader);

            for (int rungIndex = 0; rungIndex < document.Rungs.Count; rungIndex++)
            {
                LadderProjectRung rung = document.Rungs[rungIndex];
                if (rung == null) throw new InvalidDataException("Rung nulo na posicao " + (rungIndex + 1).ToString() + ".");

                text.Append("RUNG");
                for (int column = 0; column < LadderProjectRung.ColumnCount; column++)
                {
                    text.Append('|');
                    text.Append(EncodeElement(rung.Series[column]));
                    text.Append('~');
                    text.Append(EncodeElement(rung.Parallel[column]));
                }
                text.AppendLine();
            }

            return text.ToString();
        }

        public static LadderProjectDocument Deserialize(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) throw new InvalidDataException("Arquivo vazio.");

            string[] lines = data.Replace("\r", string.Empty).Split('\n');
            string header = lines[0].Trim().TrimStart('\uFEFF');
            bool legacy;

            if (header == LegacyHeader) legacy = true;
            else if (header == CurrentHeader) legacy = false;
            else throw new InvalidDataException("Formato de projeto nao reconhecido.");

            LadderProjectDocument document = new LadderProjectDocument();

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (line.Length == 0) continue;

                string[] parts = line.Split('|');
                if (parts.Length != LadderProjectRung.ColumnCount + 1 || parts[0] != "RUNG")
                    throw new InvalidDataException("Rung invalido na linha " + (lineIndex + 1).ToString() + ".");

                LadderProjectRung rung = new LadderProjectRung();
                for (int column = 0; column < LadderProjectRung.ColumnCount; column++)
                {
                    string cell = parts[column + 1];
                    if (legacy)
                    {
                        rung.Series[column] = DecodeLegacyElement(cell, lineIndex + 1, column + 1);
                        continue;
                    }

                    string[] lanes = cell.Split('~');
                    if (lanes.Length < 1 || lanes.Length > 2)
                        throw InvalidCell(lineIndex + 1, column + 1, "quantidade de ramificacoes invalida");

                    rung.Series[column] = DecodeElement(lanes[0], lineIndex + 1, column + 1);
                    if (lanes.Length == 2)
                        rung.Parallel[column] = DecodeElement(lanes[1], lineIndex + 1, column + 1);
                }

                document.Rungs.Add(rung);
            }

            if (document.Rungs.Count == 0) document.Rungs.Add(new LadderProjectRung());
            return document;
        }

        private static string EncodeElement(LadderProjectElement element)
        {
            if (element == null || element.Kind == LadderProjectElementKind.Empty) return "EMPTY";
            if (element.Kind == LadderProjectElementKind.ContactNormallyOpen) return "NO:" + Escape(element.Address);
            if (element.Kind == LadderProjectElementKind.ContactNormallyClosed) return "NC:" + Escape(element.Address);
            if (element.Kind == LadderProjectElementKind.Coil) return "COIL:" + Escape(element.Address);
            if (element.Kind == LadderProjectElementKind.Timer) return "TMR:" + Escape(element.Address) + ":" + Escape(element.Parameter) + ":" + Escape(element.Mode);
            if (element.Kind == LadderProjectElementKind.Counter) return "CNT:" + Escape(element.Address) + ":" + Escape(element.Parameter);
            if (element.Kind == LadderProjectElementKind.Set) return "SET:" + Escape(element.Address);
            if (element.Kind == LadderProjectElementKind.Reset) return "RST:" + Escape(element.Address);
            if (element.Kind == LadderProjectElementKind.RisingEdge) return "EUP";
            if (element.Kind == LadderProjectElementKind.FallingEdge) return "EDN";
            if (element.Kind == LadderProjectElementKind.Function) return "FUN:" + Escape(element.Address) + ":" + Escape(element.Parameter);
            if (element.Kind == LadderProjectElementKind.End) return "END";
            throw new InvalidDataException("Tipo de elemento Ladder nao suportado.");
        }

        private static LadderProjectElement DecodeLegacyElement(string token, int line, int column)
        {
            if (string.IsNullOrEmpty(token) || token == "EMPTY") return new LadderProjectElement();

            int colon = token.IndexOf(':');
            if (colon <= 0) throw InvalidCell(line, column, "elemento legado incompleto");

            string kind = token.Substring(0, colon);
            string address = token.Substring(colon + 1);
            LadderProjectElement element = new LadderProjectElement();

            if (kind == "NO") element.Kind = LadderProjectElementKind.ContactNormallyOpen;
            else if (kind == "NC") element.Kind = LadderProjectElementKind.ContactNormallyClosed;
            else if (kind == "COIL") element.Kind = LadderProjectElementKind.Coil;
            else throw InvalidCell(line, column, "elemento legado desconhecido: " + kind);

            element.Address = address;
            return element;
        }

        private static LadderProjectElement DecodeElement(string token, int line, int column)
        {
            if (string.IsNullOrEmpty(token) || token == "EMPTY") return new LadderProjectElement();

            string[] fields = token.Split(':');
            string kind = fields[0];
            LadderProjectElement element = new LadderProjectElement();

            if (kind == "NO")
            {
                RequireFieldCount(fields, 2, 2, line, column);
                element.Kind = LadderProjectElementKind.ContactNormallyOpen;
                element.Address = Unescape(fields[1], line, column);
            }
            else if (kind == "NC")
            {
                RequireFieldCount(fields, 2, 2, line, column);
                element.Kind = LadderProjectElementKind.ContactNormallyClosed;
                element.Address = Unescape(fields[1], line, column);
            }
            else if (kind == "COIL")
            {
                RequireFieldCount(fields, 2, 2, line, column);
                element.Kind = LadderProjectElementKind.Coil;
                element.Address = Unescape(fields[1], line, column);
            }
            else if (kind == "TMR")
            {
                RequireFieldCount(fields, 2, 4, line, column);
                element.Kind = LadderProjectElementKind.Timer;
                element.Address = Unescape(fields[1], line, column);
                if (fields.Length > 2) element.Parameter = Unescape(fields[2], line, column);
                if (fields.Length > 3) element.Mode = Unescape(fields[3], line, column);
            }
            else if (kind == "CNT")
            {
                RequireFieldCount(fields, 2, 3, line, column);
                element.Kind = LadderProjectElementKind.Counter;
                element.Address = Unescape(fields[1], line, column);
                if (fields.Length > 2) element.Parameter = Unescape(fields[2], line, column);
            }
            else if (kind == "SET")
            {
                RequireFieldCount(fields, 2, 2, line, column);
                element.Kind = LadderProjectElementKind.Set;
                element.Address = Unescape(fields[1], line, column);
            }
            else if (kind == "RST")
            {
                RequireFieldCount(fields, 2, 2, line, column);
                element.Kind = LadderProjectElementKind.Reset;
                element.Address = Unescape(fields[1], line, column);
            }
            else if (kind == "EUP")
            {
                RequireFieldCount(fields, 1, 1, line, column);
                element.Kind = LadderProjectElementKind.RisingEdge;
            }
            else if (kind == "EDN")
            {
                RequireFieldCount(fields, 1, 1, line, column);
                element.Kind = LadderProjectElementKind.FallingEdge;
            }
            else if (kind == "FUN")
            {
                RequireFieldCount(fields, 2, 3, line, column);
                element.Kind = LadderProjectElementKind.Function;
                element.Address = Unescape(fields[1], line, column);
                if (fields.Length > 2) element.Parameter = Unescape(fields[2], line, column);
            }
            else if (kind == "END")
            {
                RequireFieldCount(fields, 1, 1, line, column);
                element.Kind = LadderProjectElementKind.End;
                element.Address = "F-00";
            }
            else
            {
                throw InvalidCell(line, column, "elemento desconhecido: " + kind);
            }

            return element;
        }

        private static void RequireFieldCount(string[] fields, int minimum, int maximum, int line, int column)
        {
            if (fields.Length < minimum || fields.Length > maximum)
                throw InvalidCell(line, column, "quantidade de campos invalida para " + fields[0]);
        }

        private static string Escape(string value)
        {
            // O til (~) e um separador estrutural da versao 2, mas
            // Uri.EscapeDataString o considera um caractere nao reservado.
            // Escape-o explicitamente para que o arquivo gerado possa ser lido
            // sem criar uma terceira ramificacao na celula.
            return Uri.EscapeDataString(value == null ? string.Empty : value).Replace("~", "%7E");
        }

        private static string Unescape(string value, int line, int column)
        {
            string encoded = value == null ? string.Empty : value;

            for (int index = 0; index < encoded.Length; index++)
            {
                if (encoded[index] != '%') continue;
                if (index + 2 >= encoded.Length ||
                    !IsHexDigit(encoded[index + 1]) ||
                    !IsHexDigit(encoded[index + 2]))
                {
                    throw InvalidCell(line, column, "sequencia de escape invalida");
                }

                index += 2;
            }

            try
            {
                return Uri.UnescapeDataString(encoded);
            }
            catch (UriFormatException)
            {
                throw InvalidCell(line, column, "sequencia de escape invalida");
            }
        }

        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'A' && value <= 'F') ||
                   (value >= 'a' && value <= 'f');
        }

        private static InvalidDataException InvalidCell(int line, int column, string reason)
        {
            return new InvalidDataException("Celula invalida na linha " + line.ToString() + ", coluna " + column.ToString() + ": " + reason + ".");
        }
    }
}
