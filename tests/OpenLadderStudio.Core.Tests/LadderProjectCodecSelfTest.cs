using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Core.Tests
{
    internal static class LadderProjectCodecSelfTest
    {
        private static int failures;

        private static int Main()
        {
            Console.WriteLine("Autoteste do formato .pladder do OpenLadder Studio");
            Console.WriteLine();

            TestVersionTwoRoundTrip();
            TestLegacyImport();
            TestEmptyDocumentFallback();
            TestInvalidDocuments();
            TestRungCommentRoundTrip();
            TestCommentFreeProjectStaysVersionTwo();
            TestOlderProjectsHaveEmptyComments();
            TestInvalidComments();
            TestCommentNormalization();
            TestCommentDoesNotChangePlcPayload();

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("Todas as verificações passaram.");
                return 0;
            }

            Console.WriteLine(failures.ToString(CultureInfo.InvariantCulture) + " verificação(ões) falharam.");
            return 1;
        }

        private static void TestVersionTwoRoundTrip()
        {
            Section("Formato atual e ramificações");

            LadderProjectDocument source = new LadderProjectDocument();
            LadderProjectRung first = new LadderProjectRung();
            first.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "X0001", string.Empty, string.Empty);
            first.Parallel[0] = Element(LadderProjectElementKind.ContactNormallyClosed, "C0001", string.Empty, string.Empty);
            first.Series[1] = Element(LadderProjectElementKind.Timer, "V0001", "25", "RESET");
            first.Series[2] = Element(LadderProjectElementKind.Counter, "V0002", "10", string.Empty);
            first.Series[3] = Element(LadderProjectElementKind.Set, "C0002", string.Empty, string.Empty);
            first.Series[4] = Element(LadderProjectElementKind.Reset, "C0003", string.Empty, string.Empty);
            first.Series[5] = Element(LadderProjectElementKind.RisingEdge, string.Empty, string.Empty, string.Empty);
            first.Series[6] = Element(LadderProjectElementKind.FallingEdge, string.Empty, string.Empty, string.Empty);
            first.Series[7] = Element(LadderProjectElementKind.Coil, "Y0001", string.Empty, string.Empty);
            source.Rungs.Add(first);

            LadderProjectRung second = new LadderProjectRung();
            second.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "Sinal com espaco / acao", string.Empty, string.Empty);
            second.Series[6] = Element(LadderProjectElementKind.Function, "F-10W", "D0001:valor", string.Empty);
            second.Series[7] = Element(LadderProjectElementKind.End, "F-00", string.Empty, string.Empty);
            source.Rungs.Add(second);

            // Os tres separadores do formato dentro do conteudo: | separa colunas,
            // ~ separa a via serie da paralela e : separa os campos do elemento.
            // O til passava sem escape (e "unreserved" na RFC 3986), gravava um
            // separador a mais e o arquivo era recusado na leitura seguinte.
            LadderProjectRung third = new LadderProjectRung();
            third.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "A~B|C:D", string.Empty, string.Empty);
            third.Parallel[0] = Element(LadderProjectElementKind.ContactNormallyClosed, "PARALELO", string.Empty, string.Empty);
            source.Rungs.Add(third);

            string encoded = LadderProjectCodec.Serialize(source);
            LadderProjectDocument restored = LadderProjectCodec.Deserialize(encoded);

            Check("cabeçalho atual gravado", encoded.StartsWith(LadderProjectCodec.CurrentHeader + Environment.NewLine, StringComparison.Ordinal));
            Check("caracteres reservados escapados", encoded.IndexOf("Sinal%20com%20espaco%20%2F%20acao", StringComparison.Ordinal) >= 0);
            Check("quantidade de rungs preservada", restored.Rungs.Count == source.Rungs.Count);
            Check("todos os elementos preservados", DocumentsEqual(source, restored));
            Check("ramificação paralela preservada", restored.Rungs[0].Parallel[0].Kind == LadderProjectElementKind.ContactNormallyClosed);
            Check("til escapado como %7E", encoded.IndexOf("A%7EB%7CC%3AD", StringComparison.Ordinal) >= 0);
            Check("separadores no conteúdo sobrevivem à ida e volta", restored.Rungs[2].Series[0].Address == "A~B|C:D");
            Check("til no conteúdo não cria via extra", restored.Rungs[2].Parallel[0].Address == "PARALELO");
        }

        private static void TestLegacyImport()
        {
            Section("Compatibilidade PC12-LADDER|1");

            string legacy =
                LadderProjectCodec.LegacyHeader + "\r\n" +
                "RUNG|NO:X0001|NC:C0001|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|COIL:Y0001\r\n";

            LadderProjectDocument document = LadderProjectCodec.Deserialize(legacy);

            Check("arquivo legado aberto", document.Rungs.Count == 1);
            Check("contato NA legado convertido", document.Rungs[0].Series[0].Kind == LadderProjectElementKind.ContactNormallyOpen);
            Check("contato NF legado convertido", document.Rungs[0].Series[1].Kind == LadderProjectElementKind.ContactNormallyClosed);
            Check("bobina legada convertida", document.Rungs[0].Series[7].Kind == LadderProjectElementKind.Coil);
            Check("ramificações legadas permanecem vazias", document.Rungs[0].Parallel[0].Kind == LadderProjectElementKind.Empty);
            Check("nova gravação migra para versão 2", LadderProjectCodec.Serialize(document).StartsWith(LadderProjectCodec.CurrentHeader, StringComparison.Ordinal));
        }

        private static void TestEmptyDocumentFallback()
        {
            Section("Projeto sem linhas");

            LadderProjectDocument document = LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\n");
            Check("editor recebe ao menos um rung", document.Rungs.Count == 1);
        }

        private static void TestInvalidDocuments()
        {
            Section("Recusa de arquivos corrompidos");

            Check("cabeçalho desconhecido recusado", ThrowsInvalidData(delegate { LadderProjectCodec.Deserialize("OUTRO|9\n"); }));
            Check("rung com colunas faltando recusado", ThrowsInvalidData(delegate { LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\nRUNG|EMPTY\n"); }));
            Check("terceira ramificação recusada", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\nRUNG|EMPTY~EMPTY~EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY\n");
            }));
            Check("elemento desconhecido recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\nRUNG|XYZ:X0001~EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY\n");
            }));
        }

        // Comentários de teste: quebra de linha CRLF e LF, acentos, aspas, os três
        // separadores do formato, sequências que parecem escape, texto que imita um
        // cabeçalho ou uma linha RUNG, espaços nas pontas e um caractere fora do BMP.
        private static string Emoji()
        {
            return new string(new char[] { (char)0xD83D, (char)0xDD27 });
        }

        private static string CommentWithAccents()
        {
            return "Partida do motor M1 — intertravamento com a proteção térmica.\r\n" +
                   "Segunda linha: \"aspas\", 'apóstrofo' e acentuação: ç ã õ é ê í ó ú à.";
        }

        private static string CommentWithSeparators()
        {
            return "Separadores | ~ : % %7E %25 e EMPTY~EMPTY|RUNG|COMMENT|x\n" +
                   LadderProjectCodec.CurrentHeader + "\tcom tab " + Emoji();
        }

        private static string CommentWithEdges()
        {
            return "  começa e termina com espaço  \n";
        }

        private static LadderProjectDocument CommentedDocument()
        {
            LadderProjectDocument document = new LadderProjectDocument();

            LadderProjectRung first = new LadderProjectRung();
            first.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "X0001", string.Empty, string.Empty);
            first.Series[1] = Element(LadderProjectElementKind.ContactNormallyOpen, "X0002", string.Empty, string.Empty);
            first.Series[7] = Element(LadderProjectElementKind.Coil, "Y0001", string.Empty, string.Empty);
            first.Comment = CommentWithAccents();
            document.Rungs.Add(first);

            LadderProjectRung second = new LadderProjectRung();
            second.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "X0003", string.Empty, string.Empty);
            second.Parallel[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "Y0002", string.Empty, string.Empty);
            second.Series[1] = Element(LadderProjectElementKind.ContactNormallyClosed, "X0004", string.Empty, string.Empty);
            second.Series[7] = Element(LadderProjectElementKind.Coil, "Y0002", string.Empty, string.Empty);
            document.Rungs.Add(second);

            LadderProjectRung third = new LadderProjectRung();
            third.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "Y0002", string.Empty, string.Empty);
            third.Series[7] = Element(LadderProjectElementKind.Timer, "V0001", "25", string.Empty);
            third.Comment = CommentWithSeparators();
            document.Rungs.Add(third);

            LadderProjectRung fourth = new LadderProjectRung();
            fourth.Series[7] = Element(LadderProjectElementKind.End, "F-00", string.Empty, string.Empty);
            fourth.Comment = CommentWithEdges();
            document.Rungs.Add(fourth);

            return document;
        }

        private static LadderProjectDocument WithoutComments(LadderProjectDocument source)
        {
            LadderProjectDocument copy = LadderProjectCodec.Deserialize(LadderProjectCodec.Serialize(source));
            for (int rung = 0; rung < copy.Rungs.Count; rung++) copy.Rungs[rung].Comment = string.Empty;
            return copy;
        }

        private static void TestRungCommentRoundTrip()
        {
            Section("Comentário por linha (versão 3)");

            LadderProjectDocument source = CommentedDocument();
            string encoded = LadderProjectCodec.Serialize(source);
            LadderProjectDocument restored = LadderProjectCodec.Deserialize(encoded);

            Check("projeto comentado grava cabeçalho 3", encoded.StartsWith(LadderProjectCodec.CommentHeader + Environment.NewLine, StringComparison.Ordinal));
            Check("comentário com quebra de linha e acentos sobrevive", restored.Rungs[0].Comment == CommentWithAccents());
            Check("rung sem comentário volta com texto vazio", restored.Rungs[1].Comment.Length == 0);
            Check("separadores, escape aparente e emoji sobrevivem", restored.Rungs[2].Comment == CommentWithSeparators());
            Check("espaços nas pontas e quebra final sobrevivem", restored.Rungs[3].Comment == CommentWithEdges());
            Check("elementos preservados junto com os comentários", DocumentsEqual(source, restored));

            // Uma linha por cabeçalho, por rung e por comentário: nenhuma quebra de linha
            // nem separador do conteúdo vaza para a estrutura do arquivo.
            int lineBreaks = encoded.Split('\n').Length - 1;
            Check("comentário não cria linhas extras no arquivo", lineBreaks == 1 + 4 + 3);
            Check("comentário gravado antes do seu RUNG", encoded.Split('\n')[1].StartsWith(LadderProjectCodec.CommentTag + "|", StringComparison.Ordinal)
                && encoded.Split('\n')[2].StartsWith("RUNG|", StringComparison.Ordinal));
            Check("acento gravado como UTF-8 escapado", encoded.IndexOf("prote%C3%A7%C3%A3o", StringComparison.Ordinal) >= 0);
            Check("nenhum caractere fora de ASCII no arquivo", IsAscii(encoded));

            string again = LadderProjectCodec.Serialize(restored);
            Check("segunda gravação idêntica à primeira", again == encoded);

            LadderProjectRung rung = new LadderProjectRung();
            rung.Comment = null;
            Check("comentário nulo vira texto vazio", rung.Comment != null && rung.Comment.Length == 0);
            Check("rung novo nasce sem comentário", new LadderProjectRung().Comment.Length == 0);
        }

        private static void TestCommentFreeProjectStaysVersionTwo()
        {
            Section("Projeto sem comentário continua na versão 2");

            LadderProjectDocument document = new LadderProjectDocument();
            LadderProjectRung first = new LadderProjectRung();
            first.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "X0001", string.Empty, string.Empty);
            first.Series[7] = Element(LadderProjectElementKind.Coil, "Y0001", string.Empty, string.Empty);
            document.Rungs.Add(first);
            LadderProjectRung second = new LadderProjectRung();
            second.Series[7] = Element(LadderProjectElementKind.End, "F-00", string.Empty, string.Empty);
            document.Rungs.Add(second);

            // Texto exato que a versão anterior do codec gravava para este projeto.
            string nl = Environment.NewLine;
            string expected =
                "PC12-LADDER|2" + nl +
                "RUNG|NO:X0001~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|COIL:Y0001~EMPTY" + nl +
                "RUNG|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|END~EMPTY" + nl;
            Check("gravação sem comentário idêntica à versão 2 anterior", LadderProjectCodec.Serialize(document) == expected);

            first.Comment = "   ";
            Check("comentário só com espaços vira vazio e segue na versão 2", LadderProjectCodec.Serialize(document) == expected);
            first.Comment = string.Empty;
            Check("apagar o comentário volta para a versão 2", LadderProjectCodec.Serialize(document) == expected);

            LadderProjectDocument commented = CommentedDocument();
            string withComments = LadderProjectCodec.Serialize(commented);
            string withoutComments = LadderProjectCodec.Serialize(WithoutComments(commented));
            Check("linhas RUNG da versão 3 iguais às da versão 2", RungLines(withComments) == RungLines(withoutComments));
            Check("documento sem comentário grava versão 2", withoutComments.StartsWith(LadderProjectCodec.CurrentHeader + Environment.NewLine, StringComparison.Ordinal));
        }

        private static void TestOlderProjectsHaveEmptyComments()
        {
            Section("Projeto antigo abre sem comentário");

            string legacy =
                LadderProjectCodec.LegacyHeader + "\r\n" +
                "RUNG|NO:X0001|NC:C0001|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|COIL:Y0001\r\n";
            LadderProjectDocument fromLegacy = LadderProjectCodec.Deserialize(legacy);
            Check("versão 1 abre com comentário vazio", fromLegacy.Rungs.Count == 1 && fromLegacy.Rungs[0].Comment.Length == 0);

            string versionTwo =
                LadderProjectCodec.CurrentHeader + "\n" +
                "RUNG|NO:X0001~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|COIL:Y0001~EMPTY\n" +
                "RUNG|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|END~EMPTY\n";
            LadderProjectDocument fromTwo = LadderProjectCodec.Deserialize(versionTwo);
            Check("versão 2 abre com comentários vazios", fromTwo.Rungs.Count == 2 && fromTwo.Rungs[0].Comment.Length == 0 && fromTwo.Rungs[1].Comment.Length == 0);
            Check("versão 2 regravada continua versão 2", LadderProjectCodec.Serialize(fromTwo).StartsWith(LadderProjectCodec.CurrentHeader + Environment.NewLine, StringComparison.Ordinal));
        }

        private static void TestInvalidComments()
        {
            Section("Recusa de comentários malformados");

            string rung = "RUNG|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|END~EMPTY\n";
            Check("COMMENT em arquivo versão 2 recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\nCOMMENT|texto\n" + rung);
            }));
            Check("COMMENT sem rung seguinte recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CommentHeader + "\n" + rung + "COMMENT|sobrando\n");
            }));
            Check("dois COMMENT seguidos recusados", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CommentHeader + "\nCOMMENT|a\nCOMMENT|b\n" + rung);
            }));
            Check("COMMENT com separador cru recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CommentHeader + "\nCOMMENT|a|b\n" + rung);
            }));
            Check("versão 3 continua recusando rung inválido", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CommentHeader + "\nRUNG|EMPTY\n");
            }));

            LadderProjectDocument tooLong = new LadderProjectDocument();
            LadderProjectRung longRung = new LadderProjectRung();
            longRung.Comment = new string('a', LadderProjectCodec.MaxCommentLength + 1);
            tooLong.Rungs.Add(longRung);
            Check("comentário acima do limite é cortado na atribuição", longRung.Comment.Length == LadderProjectCodec.MaxCommentLength);
            Check("comentário cortado grava sem recusar", !ThrowsInvalidData(delegate { LadderProjectCodec.Serialize(tooLong); }));

            // Um surrogate isolado não tem UTF-8 válido: a atribuição já o corrige, então a
            // gravação nunca escreve um texto diferente do que está no rung.
            LadderProjectDocument loneDocument = new LadderProjectDocument();
            LadderProjectRung loneRung = new LadderProjectRung();
            loneRung.Comment = "a" + new string((char)0xD83D, 1) + "b";
            loneDocument.Rungs.Add(loneRung);
            Check("surrogate isolado vira U+FFFD na atribuição", loneRung.Comment == "a" + new string((char)0xFFFD, 1) + "b");
            Check("surrogate corrigido grava sem recusar", !ThrowsInvalidData(delegate { LadderProjectCodec.Serialize(loneDocument); }));

            LadderProjectDocument atLimit = new LadderProjectDocument();
            LadderProjectRung limitRung = new LadderProjectRung();
            limitRung.Comment = new string('a', LadderProjectCodec.MaxCommentLength);
            atLimit.Rungs.Add(limitRung);
            Check("comentário no limite gravado e lido", LadderProjectCodec.Deserialize(LadderProjectCodec.Serialize(atLimit)).Rungs[0].Comment == limitRung.Comment);

            // Arquivo de outra ferramenta com comentário longo: a leitura ajusta, para que a
            // primeira edição no app não estoure na gravação.
            string rungText = "RUNG|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY\n";
            string foreign = LadderProjectCodec.CommentHeader + "\nCOMMENT|" + new string('b', LadderProjectCodec.MaxCommentLength + 500) + "\n" + rungText;
            bool readable = true;
            LadderProjectDocument adjusted = null;
            try { adjusted = LadderProjectCodec.Deserialize(foreign); }
            catch (InvalidDataException) { readable = false; }
            Check("comentário longo em arquivo externo abre", readable && adjusted != null && adjusted.Rungs.Count == 1);
            if (readable && adjusted != null && adjusted.Rungs.Count == 1)
            {
                Check("comentário longo lido é cortado no limite", adjusted.Rungs[0].Comment.Length == LadderProjectCodec.MaxCommentLength);
                Check("comentário lido sempre regrava", LadderProjectCodec.Deserialize(LadderProjectCodec.Serialize(adjusted)).Rungs[0].Comment == adjusted.Rungs[0].Comment);
            }
        }

        private static void TestCommentNormalization()
        {
            Section("Normalização do texto vindo da interface");

            Check("nulo vira vazio", LadderProjectCodec.NormalizeComment(null).Length == 0);
            Check("só espaços e quebras vira vazio", LadderProjectCodec.NormalizeComment("  \r\n\t ").Length == 0);
            Check("texto válido passa intacto", LadderProjectCodec.NormalizeComment(CommentWithAccents()) == CommentWithAccents());
            Check("par de surrogates preservado", LadderProjectCodec.NormalizeComment(CommentWithSeparators()) == CommentWithSeparators());

            string lone = "a" + new string((char)0xD83D, 1) + "b" + new string((char)0xDD27, 1);
            string fixedLone = LadderProjectCodec.NormalizeComment(lone);
            Check("surrogate isolado vira U+FFFD", fixedLone == "a" + new string((char)0xFFFD, 1) + "b" + new string((char)0xFFFD, 1));

            string longText = new string('x', LadderProjectCodec.MaxCommentLength - 1) + Emoji() + "fim";
            string cut = LadderProjectCodec.NormalizeComment(longText);
            Check("texto longo cortado no limite", cut.Length <= LadderProjectCodec.MaxCommentLength);
            Check("corte não parte par de surrogates", cut.Length == 0 || !char.IsHighSurrogate(cut[cut.Length - 1]));

            LadderProjectDocument document = new LadderProjectDocument();
            LadderProjectRung rung = new LadderProjectRung();
            rung.Comment = LadderProjectCodec.NormalizeComment(lone + new string('y', 5000));
            document.Rungs.Add(rung);
            Check("texto normalizado sempre grava", LadderProjectCodec.Deserialize(LadderProjectCodec.Serialize(document)).Rungs[0].Comment == rung.Comment);
        }

        private static void TestCommentDoesNotChangePlcPayload()
        {
            Section("Comentário não altera o programa enviado ao PLC");

            LadderProjectDocument commented = CommentedDocument();
            LadderProjectDocument plain = WithoutComments(commented);

            // Mesmo caminho da transferência: o editor serializa, a transferência lê o
            // texto de volta e compila o documento.
            LadderProjectDocument commentedFromText = LadderProjectCodec.Deserialize(LadderProjectCodec.Serialize(commented));
            LadderProjectDocument plainFromText = LadderProjectCodec.Deserialize(LadderProjectCodec.Serialize(plain));
            Check("comentários chegam ao documento compilado", commentedFromText.Rungs[0].Comment == CommentWithAccents());

            Tp02LadderCompilationResult withComments = Tp02LadderTargetCompiler.Compile(commentedFromText);
            Tp02LadderCompilationResult withoutComments = Tp02LadderTargetCompiler.Compile(plainFromText);
            Tp02LadderCompilationResult direct = Tp02LadderTargetCompiler.Compile(commented);

            string hexWith = Tp02TargetCompiler.WordsToHex(withComments.Words);
            string hexWithout = Tp02TargetCompiler.WordsToHex(withoutComments.Words);

            Check("programa de teste compila", withComments.Success && withoutComments.Success);
            Check("programa de teste não é vazio", withComments.Words.Count > 3 && hexWith.StartsWith("001000002100204000", StringComparison.Ordinal));
            Check("palavras de máquina idênticas", hexWith == hexWithout);
            Check("documento em memória gera as mesmas palavras", Tp02TargetCompiler.WordsToHex(direct.Words) == hexWithout);
            Check("rastro de compilação idêntico", TraceText(withComments) == TraceText(withoutComments));
            Check("erros e avisos idênticos", string.Join("\n", withComments.Errors.ToArray()) == string.Join("\n", withoutComments.Errors.ToArray())
                && string.Join("\n", withComments.Warnings.ToArray()) == string.Join("\n", withoutComments.Warnings.ToArray()));
            Check("relatório de compilação idêntico", withComments.BuildReport() == withoutComments.BuildReport());

            List<string> wbpWith = Tp02LadderTargetCompiler.BuildWbpDryRunFrames(withComments, 1, 0, 5);
            List<string> wbpWithout = Tp02LadderTargetCompiler.BuildWbpDryRunFrames(withoutComments, 1, 0, 5);
            Check("quadros WBP dry-run idênticos", string.Join("|", wbpWith.ToArray()) == string.Join("|", wbpWithout.ToArray()));

            List<string> linkWith = Tp02ComputerLinkProgramCodec.BuildWbpProgramFrames(1, 0, withComments.Words, 5);
            List<string> linkWithout = Tp02ComputerLinkProgramCodec.BuildWbpProgramFrames(1, 0, withoutComments.Words, 5);
            Check("quadros WBP do Computer Link idênticos", linkWith.Count > 0 && string.Join("|", linkWith.ToArray()) == string.Join("|", linkWithout.ToArray()));
            Check("nenhum texto de comentário nos quadros", string.Join("|", linkWith.ToArray()).IndexOf("Partida", StringComparison.Ordinal) < 0);
        }

        private static string TraceText(Tp02LadderCompilationResult result)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < result.Trace.Count; i++)
            {
                Tp02CompilationTrace row = result.Trace[i];
                text.Append(row.Rung.ToString(CultureInfo.InvariantCulture)).Append(';')
                    .Append(row.Step.ToString(CultureInfo.InvariantCulture)).Append(';')
                    .Append(row.BooleanText).Append(';')
                    .Append(row.MachineHex).Append('\n');
            }
            return text.ToString();
        }

        private static string RungLines(string encoded)
        {
            StringBuilder text = new StringBuilder();
            string[] lines = encoded.Replace("\r", string.Empty).Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(LadderProjectCodec.CommentTag + "|", StringComparison.Ordinal)) continue;
                text.Append(lines[i]).Append('\n');
            }
            return text.ToString();
        }

        private static bool IsAscii(string text)
        {
            for (int i = 0; i < text.Length; i++) if (text[i] > 127) return false;
            return true;
        }

        private static LadderProjectElement Element(LadderProjectElementKind kind, string address, string parameter, string mode)
        {
            LadderProjectElement element = new LadderProjectElement();
            element.Kind = kind;
            element.Address = address;
            element.Parameter = parameter;
            element.Mode = mode;
            return element;
        }

        private static bool DocumentsEqual(LadderProjectDocument left, LadderProjectDocument right)
        {
            if (left.Rungs.Count != right.Rungs.Count) return false;

            for (int rung = 0; rung < left.Rungs.Count; rung++)
            {
                if (left.Rungs[rung].Comment != right.Rungs[rung].Comment) return false;
                for (int column = 0; column < LadderProjectRung.ColumnCount; column++)
                {
                    if (!ElementsEqual(left.Rungs[rung].Series[column], right.Rungs[rung].Series[column])) return false;
                    if (!ElementsEqual(left.Rungs[rung].Parallel[column], right.Rungs[rung].Parallel[column])) return false;
                }
            }

            return true;
        }

        private static bool ElementsEqual(LadderProjectElement left, LadderProjectElement right)
        {
            return left.Kind == right.Kind &&
                   left.Address == right.Address &&
                   left.Parameter == right.Parameter &&
                   left.Mode == right.Mode;
        }

        private static bool ThrowsInvalidData(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (InvalidDataException)
            {
                return true;
            }
        }

        private static void Section(string title)
        {
            Console.WriteLine(title);
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
    }
}
