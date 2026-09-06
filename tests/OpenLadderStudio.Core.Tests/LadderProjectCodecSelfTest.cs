using System;
using System.Globalization;
using System.IO;
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
            TestCompatibilityFixtures();
            TestEmptyDocumentFallback();
            TestInvalidDocuments();

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("Todas as verificacoes passaram.");
                return 0;
            }

            Console.WriteLine(failures.ToString(CultureInfo.InvariantCulture) + " verificacao(oes) falharam.");
            return 1;
        }

        private static void TestVersionTwoRoundTrip()
        {
            Section("Formato atual e ramificacoes");

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
            second.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "Sinal ~ A | B : C % / a\u00e7\u00e3o", string.Empty, string.Empty);
            second.Series[1] = Element(LadderProjectElementKind.Timer, "V0003", "linha 1\r\nlinha 2", "modo:manual|~%");
            second.Series[6] = Element(LadderProjectElementKind.Function, "F-10W", "D0001:valor|alternativo~100%", string.Empty);
            second.Series[7] = Element(LadderProjectElementKind.End, "F-00", string.Empty, string.Empty);
            source.Rungs.Add(second);

            string encoded = LadderProjectCodec.Serialize(source);
            LadderProjectDocument restored = LadderProjectCodec.Deserialize(encoded);

            Check("cabecalho atual gravado", encoded.StartsWith(LadderProjectCodec.CurrentHeader + Environment.NewLine, StringComparison.Ordinal));
            Check("separador de ramificacao escapado", encoded.IndexOf("Sinal%20%7E%20A", StringComparison.Ordinal) >= 0);
            Check("separadores de campo e coluna escapados", encoded.IndexOf("%7C%20B%20%3A%20C%20%25%20%2F", StringComparison.Ordinal) >= 0);
            Check("texto Unicode escapado em UTF-8", encoded.IndexOf("a%C3%A7%C3%A3o", StringComparison.Ordinal) >= 0);
            Check("quebra de linha escapada", encoded.IndexOf("linha%201%0D%0Alinha%202", StringComparison.Ordinal) >= 0);
            Check("quantidade de rungs preservada", restored.Rungs.Count == source.Rungs.Count);
            Check("todos os elementos preservados", DocumentsEqual(source, restored));
            Check("ramificacao paralela preservada", restored.Rungs[0].Parallel[0].Kind == LadderProjectElementKind.ContactNormallyClosed);
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
            Check("ramificacoes legadas permanecem vazias", document.Rungs[0].Parallel[0].Kind == LadderProjectElementKind.Empty);
            Check("nova gravacao migra para versao 2", LadderProjectCodec.Serialize(document).StartsWith(LadderProjectCodec.CurrentHeader, StringComparison.Ordinal));
        }

        private static void TestCompatibilityFixtures()
        {
            Section("Fixtures de compatibilidade");

            LadderProjectDocument legacy = LadderProjectCodec.Deserialize(LoadFixture("valid-v1-basic.pladder"));
            Check("fixture v1 aberta", legacy.Rungs.Count == 1 && legacy.Rungs[0].Series[7].Kind == LadderProjectElementKind.Coil);

            LadderProjectDocument branched = LadderProjectCodec.Deserialize(LoadFixture("valid-v2-branched.pladder"));
            Check("fixture v2 com ramificacao aberta", branched.Rungs[0].Parallel[0].Kind == LadderProjectElementKind.ContactNormallyClosed);

            LadderProjectDocument escaped = LadderProjectCodec.Deserialize(LoadFixture("valid-v2-escaped.pladder"));
            Check("fixture v2 decodifica separadores", escaped.Rungs[0].Series[0].Address == "Sinal~A|B:C%/acao");
            Check("fixture v2 decodifica quebra de linha", escaped.Rungs[0].Series[1].Parameter == "linha 1\r\nlinha 2");

            Check("fixture com escape malformado recusada", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LoadFixture("invalid-v2-malformed-escape.pladder"));
            }));
            Check("fixture com til nao escapado recusada", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LoadFixture("invalid-v2-unescaped-tilde.pladder"));
            }));
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

            Check("cabecalho desconhecido recusado", ThrowsInvalidData(delegate { LadderProjectCodec.Deserialize("OUTRO|9\n"); }));
            Check("rung com colunas faltando recusado", ThrowsInvalidData(delegate { LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\nRUNG|EMPTY\n"); }));
            Check("terceira ramificacao recusada", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\nRUNG|EMPTY~EMPTY~EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY\n");
            }));
            Check("elemento desconhecido recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(LadderProjectCodec.CurrentHeader + "\nRUNG|XYZ:X0001~EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY\n");
            }));
            Check("percentual isolado recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(VersionTwoRow("NO:X%~EMPTY"));
            }));
            Check("escape hexadecimal curto recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(VersionTwoRow("NO:X%2~EMPTY"));
            }));
            Check("escape nao hexadecimal recusado", ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(VersionTwoRow("NO:X%2G~EMPTY"));
            }));
            Check("percentual corretamente escapado aceito", !ThrowsInvalidData(delegate
            {
                LadderProjectCodec.Deserialize(VersionTwoRow("NO:X%25~EMPTY"));
            }));
        }

        private static string VersionTwoRow(string firstCell)
        {
            return LadderProjectCodec.CurrentHeader + "\nRUNG|" + firstCell +
                   "|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY\n";
        }

        private static string LoadFixture(string fileName)
        {
            string relative = Path.Combine("..", "tests", "OpenLadderStudio.Core.Tests", "Fixtures", fileName);
            string path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative));
            if (!File.Exists(path)) throw new InvalidDataException("Fixture ausente: " + path);
            return File.ReadAllText(path);
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
