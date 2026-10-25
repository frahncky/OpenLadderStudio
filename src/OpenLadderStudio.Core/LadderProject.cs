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

        private string comment = string.Empty;

        /// <summary>
        /// Comentário livre da linha. Nunca é nulo e é sempre gravável: a atribuição passa por
        /// LadderProjectCodec.NormalizeComment (só espaços vira vazio, excesso é cortado).
        /// É documentação do projeto, não lógica: nenhum compilador nem caminho de
        /// transferência lê este campo, e o programa enviado ao PLC é o mesmo com
        /// ou sem comentário.
        /// </summary>
        public string Comment
        {
            get { return comment; }
            set { comment = LadderProjectCodec.NormalizeComment(value); }
        }

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
    /// A versão 3 é a versão 2 acrescida de uma linha COMMENT antes de cada rung comentado.
    /// Só é gravada quando algum rung tem comentário: projeto sem comentário continua saindo
    /// em versão 2, byte a byte igual ao que as versões anteriores do app gravavam e liam.
    /// As versões anteriores recusam a versão 3 pelo cabeçalho ("Formato de projeto não
    /// reconhecido"); o leitor delas não tem como ignorar uma linha desconhecida.
    /// </summary>
    internal static class LadderProjectCodec
    {
        public const string LegacyHeader = "PC12-LADDER|1";
        public const string CurrentHeader = "PC12-LADDER|2";
        public const string CommentHeader = "PC12-LADDER|3";
        public const string CommentTag = "COMMENT";

        // Limite do comentário de uma linha. Uri.EscapeDataString do .NET Framework
        // recusa entradas muito longas, e o comentário de um rung não é lugar de texto
        // extenso; o limite mantém a gravação previsível.
        public const int MaxCommentLength = 1000;

        private const char ReplacementCharacter = (char)0xFFFD;

        /// <summary>
        /// Normaliza o texto vindo da interface antes de virar comentário: nulo ou só
        /// espaços vira vazio, surrogate isolado vira U+FFFD (não teria codificação
        /// UTF-8 válida) e o excesso além de MaxCommentLength é cortado sem partir um
        /// par de surrogates. O codec em si não normaliza: grava e lê exatamente o texto.
        /// </summary>
        public static string NormalizeComment(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return string.Empty;

            StringBuilder clean = new StringBuilder(Math.Min(text.Length, MaxCommentLength));
            for (int i = 0; i < text.Length && clean.Length < MaxCommentLength; i++)
            {
                char current = text[i];
                if (char.IsHighSurrogate(current))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        if (clean.Length + 2 > MaxCommentLength) break;
                        clean.Append(current);
                        clean.Append(text[i + 1]);
                        i++;
                        continue;
                    }
                    clean.Append(ReplacementCharacter);
                    continue;
                }
                if (char.IsLowSurrogate(current))
                {
                    clean.Append(ReplacementCharacter);
                    continue;
                }
                clean.Append(current);
            }

            return clean.ToString();
        }

        public static string Serialize(LadderProjectDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");

            bool hasComments = false;
            for (int rungIndex = 0; rungIndex < document.Rungs.Count; rungIndex++)
            {
                LadderProjectRung rung = document.Rungs[rungIndex];
                if (rung != null && rung.Comment.Length > 0) { hasComments = true; break; }
            }

            StringBuilder text = new StringBuilder();
            text.AppendLine(hasComments ? CommentHeader : CurrentHeader);

            for (int rungIndex = 0; rungIndex < document.Rungs.Count; rungIndex++)
            {
                LadderProjectRung rung = document.Rungs[rungIndex];
                if (rung == null) throw new InvalidDataException("Rung nulo na posição " + (rungIndex + 1).ToString() + ".");

                if (rung.Comment.Length > 0)
                {
                    if (rung.Comment.Length > MaxCommentLength)
                        throw new InvalidDataException("Comentário da linha " + (rungIndex + 1).ToString() + " excede " + MaxCommentLength.ToString() + " caracteres.");
                    text.Append(CommentTag);
                    text.Append('|');
                    text.Append(EscapeComment(rung.Comment, rungIndex + 1));
                    text.AppendLine();
                }

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
            bool acceptsComments = false;

            if (header == LegacyHeader) legacy = true;
            else if (header == CurrentHeader) legacy = false;
            else if (header == CommentHeader) { legacy = false; acceptsComments = true; }
            else throw new InvalidDataException("Formato de projeto não reconhecido.");

            LadderProjectDocument document = new LadderProjectDocument();
            string pendingComment = null;
            int pendingCommentLine = 0;

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (line.Length == 0) continue;

                string[] parts = line.Split('|');

                // COMMENT só existe na versão 3 e vale para o RUNG seguinte. Nas versões 1
                // e 2 a linha continua recusada como antes, porque é exatamente assim que
                // as versões anteriores do app a tratariam.
                if (acceptsComments && parts[0] == CommentTag)
                {
                    if (parts.Length != 2)
                        throw new InvalidDataException("Comentário inválido na linha " + (lineIndex + 1).ToString() + ".");
                    if (pendingComment != null)
                        throw new InvalidDataException("Comentário duplicado na linha " + (lineIndex + 1).ToString() + ": falta o rung do comentário anterior.");
                    pendingComment = UnescapeComment(parts[1], lineIndex + 1);
                    pendingCommentLine = lineIndex + 1;
                    continue;
                }

                if (parts.Length != LadderProjectRung.ColumnCount + 1 || parts[0] != "RUNG")
                    throw new InvalidDataException("Rung inválido na linha " + (lineIndex + 1).ToString() + ".");

                LadderProjectRung rung = new LadderProjectRung();
                if (pendingComment != null)
                {
                    // Invariante: todo comentário em memória é gravável. Arquivo escrito por
                    // outra ferramenta, com texto longo ou surrogate solto, é ajustado aqui.
                    rung.Comment = NormalizeComment(pendingComment);
                    pendingComment = null;
                }
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
                        throw InvalidCell(lineIndex + 1, column + 1, "quantidade de ramificações inválida");

                    rung.Series[column] = DecodeElement(lanes[0], lineIndex + 1, column + 1);
                    if (lanes.Length == 2)
                        rung.Parallel[column] = DecodeElement(lanes[1], lineIndex + 1, column + 1);
                }

                document.Rungs.Add(rung);
            }

            if (pendingComment != null)
                throw new InvalidDataException("Comentário sem rung na linha " + pendingCommentLine.ToString() + ".");

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
            throw new InvalidDataException("Tipo de elemento Ladder não suportado.");
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
                throw InvalidCell(line, column, "quantidade de campos inválida para " + fields[0]);
        }

        private static string Escape(string value)
        {
            // O til separa a via série da via paralela dentro da célula, mas é um
            // caractere "unreserved" na RFC 3986: Uri.EscapeDataString o deixa
            // passar. Um til no conteúdo gravava um separador a mais, e o arquivo
            // era recusado na leitura seguinte com "quantidade de ramificações
            // inválida" — ou seja, o projeto salvava e depois não abria.
            // Uri.UnescapeDataString devolve %7E como til, então o par continua
            // simétrico e arquivos já gravados seguem legíveis.
            return Uri.EscapeDataString(value == null ? string.Empty : value).Replace("~", "%7E");
        }

        private static string Unescape(string value, int line, int column)
        {
            try
            {
                return Uri.UnescapeDataString(value == null ? string.Empty : value);
            }
            catch (UriFormatException)
            {
                throw InvalidCell(line, column, "sequência de escape inválida");
            }
        }

        private static string EscapeComment(string comment, int rung)
        {
            // Mesmo escape das células: quebra de linha, barra vertical, til, dois-pontos,
            // aspas, por cento e acentos saem como %XX (UTF-8), então o comentário nunca
            // produz um separador nem uma linha a mais no arquivo.
            try
            {
                return Escape(comment);
            }
            catch (UriFormatException)
            {
                throw new InvalidDataException("Comentário da linha " + rung.ToString() + " contém caracteres inválidos.");
            }
        }

        private static string UnescapeComment(string value, int line)
        {
            try
            {
                return Uri.UnescapeDataString(value == null ? string.Empty : value);
            }
            catch (UriFormatException)
            {
                throw new InvalidDataException("Comentário inválido na linha " + line.ToString() + ": sequência de escape inválida.");
            }
        }

        private static InvalidDataException InvalidCell(int line, int column, string reason)
        {
            return new InvalidDataException("Célula inválida na linha " + line.ToString() + ", coluna " + column.ToString() + ": " + reason + ".");
        }
    }
}
