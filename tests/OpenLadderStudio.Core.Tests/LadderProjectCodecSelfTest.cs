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
            second.Series[0] = Element(LadderProjectElementKind.ContactNormallyOpen, "Sinal com espaco / acao", string.Empty, string.Empty);
            second.Series[6] = Element(LadderProjectElementKind.Function, "F-10W", "D0001:valor", string.Empty);
            second.Series[7] = Element(LadderProjectElementKind.End, "F-00", string.Empty, string.Empty);
            source.Rungs.Add(second);

            string encoded = LadderProjectCodec.Serialize(source);
            LadderProjectDocument restored = LadderProjectCodec.Deserialize(encoded);

            Check("cabecalho atual gravado", encoded.StartsWith(LadderProjectCodec.CurrentHeader + Environment.NewLine, StringComparison.Ordinal));
            Check("caracteres reservados escapados", encoded.IndexOf("Sinal%20com%20espaco%20%2F%20acao", StringComparison.Ordinal) >= 0);
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
