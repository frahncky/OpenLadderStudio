using System;
using System.Collections.Generic;

namespace ModernPC12
{
    internal static class TP02PgReadbackSelfTest
    {
        private static int failures;

        private static void Check(bool condition, string message)
        {
            if (condition) return;
            failures++;
            Console.Error.WriteLine("FALHA: " + message);
        }

        private static int Sum8(byte[] bytes)
        {
            int sum = 0;
            if (bytes != null)
                for (int i = 0; i < bytes.Length; i++)
                    sum = (sum + bytes[i]) & 0xFF;
            return sum;
        }

        private static byte[] Build34Request(int startStep)
        {
            byte[] frame = new byte[]
            {
                0x34, 0x03,
                (byte)((startStep >> 8) & 0xFF),
                (byte)(startStep & 0xFF),
                0xA0, 0x00
            };
            int sum = 0;
            for (int i = 0; i < frame.Length - 1; i++) sum = (sum + frame[i]) & 0xFF;
            frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);
            return frame;
        }

        private static void TestPhysicalMixedProgramBraw()
        {
            // Captura fisica TP02-PG-Lab-20260910-183546:
            // 38 = 00 02 00 2C D1 -> 23 passos.
            // Os 23 pares HIGH/LOW e os 23 BRAW abaixo vieram do RX RAW do 34.
            byte[] highLow = new byte[]
            {
                0x00,0x10, 0x00,0x60, 0x87,0x68, 0x40,0x40,
                0x00,0x11, 0x00,0x12, 0x01,0x68, 0x80,0x0A,
                0x40,0x41, 0x00,0x13, 0x17,0x71, 0xC8,0x80,
                0x00,0x14, 0x18,0x71, 0xC8,0x80, 0x00,0x15,
                0x0D,0x77, 0xF0,0x01, 0xF0,0x00, 0x80,0x0A,
                0x00,0x16, 0x20,0x41, 0x00,0x70
            };

            byte[] expected = new byte[]
            {
                0x01,0x06,0x0D,0x08,0x02,0x03,0x0F,0x02,
                0x09,0x04,0x00,0x0C,0x05,0x01,0x0C,0x06,
                0x0B,0x00,0x0F,0x02,0x07,0x07,0x07
            };

            Check(highLow.Length == expected.Length * 2, "fixture fisico inconsistente");
            for (int i = 0; i < expected.Length; i++)
            {
                byte actual = TP02PgReadback.CalculateBraw(highLow[2 * i], highLow[(2 * i) + 1]);
                Check(actual == expected[i], "BRAW fisico divergiu no passo " + i.ToString());
            }
        }

        private static void TestWriteThenReadback()
        {
            Pg33MachineWord[] words = new Pg33MachineWord[4000];
            bool[] valid = new bool[4000];

            // External fica deliberadamente 00. O readback PG34 deve calcular
            // BRAW a partir de HIGH/LOW, e nao copiar o External do PG33.
            words[0] = new Pg33MachineWord(0x00, 0x10, 0x00); // STR X001
            words[1] = new Pg33MachineWord(0x20, 0x40, 0x00); // OUT Y001
            words[2] = new Pg33MachineWord(0x00, 0x70, 0x00); // END
            valid[0] = valid[1] = valid[2] = true;

            byte[] r38 = TP02PgReadback.Build38(words, valid, 2, null);
            byte[] expected38 = new byte[] { 0x00, 0x02, 0x00, 0x04, 0xF9 };
            Check(r38.Length == expected38.Length, "38 com tamanho inesperado");
            for (int i = 0; i < Math.Min(r38.Length, expected38.Length); i++)
                Check(r38[i] == expected38[i], "38 divergiu no byte " + i.ToString());
            Check(Sum8(r38) == 0xFF, "checksum do 38 nao fecha FF");

            byte[] request34 = Build34Request(0);
            byte[] r34 = TP02PgReadback.Build34(request34, words, valid, 2, null);
            Check(r34.Length == 243, "34 deve ter 243 bytes totais");
            Check(r34[0] == 0x00 && r34[1] == 0xF0, "cabecalho 34 invalido");
            Check(Sum8(r34) == 0xFF, "checksum do 34 nao fecha FF");

            Check(r34[2] == 0x00 && r34[3] == 0x10, "word 0 HL incorreto");
            Check(r34[4] == 0x20 && r34[5] == 0x40, "word 1 HL incorreto");
            Check(r34[6] == 0x00 && r34[7] == 0x70, "word 2 HL incorreto");

            int b = 2 + 0xA0;
            Check(r34[b + 0] == 0x01, "BRAW STR X001 deve ser 01");
            Check(r34[b + 1] == 0x06, "BRAW OUT Y001 deve ser 06");
            Check(r34[b + 2] == 0x07, "BRAW END deve ser 07");
        }

        private static void TestSecondPage()
        {
            Pg33MachineWord[] words = new Pg33MachineWord[4000];
            bool[] valid = new bool[4000];
            words[80] = new Pg33MachineWord(0x00, 0x10, 0x00);
            words[81] = new Pg33MachineWord(0x00, 0x70, 0x00);
            valid[80] = valid[81] = true;

            byte[] request34 = Build34Request(80);
            byte[] r34 = TP02PgReadback.Build34(request34, words, valid, 81, null);
            Check(r34[2] == 0x00 && r34[3] == 0x10, "pagina 2 nao iniciou no step 80");
            Check(r34[4] == 0x00 && r34[5] == 0x70, "pagina 2 nao trouxe END no step 81");
            Check(Sum8(r34) == 0xFF, "checksum pagina 2 nao fecha FF");
        }

        private static List<Pg33MachineWord> ReadCanonicalLikeV153(
            Pg33MachineWord[] words,
            bool[] valid,
            int highest,
            out int pages,
            out int endStep)
        {
            List<Pg33MachineWord> result = new List<Pg33MachineWord>();
            pages = 0;
            endStep = -1;

            for (int start = 0; start < 4000; start += 80)
            {
                byte[] response = TP02PgReadback.Build34(Build34Request(start), words, valid, highest, null);
                Check(response.Length == 243, "V156: resposta PG34 deve ter 243 bytes");
                Check(Sum8(response) == 0xFF, "V156: checksum PG34 nao fecha FF em start=" + start.ToString());
                if (response.Length < 243) return result;

                pages++;
                for (int local = 0; local < 80; local++)
                {
                    byte high = response[2 + (2 * local)];
                    byte low = response[2 + (2 * local) + 1];
                    byte braw = response[2 + 0xA0 + local];
                    result.Add(new Pg33MachineWord(high, low, braw));
                    if (high == 0x00 && low == 0x70)
                    {
                        endStep = start + local;
                        return result;
                    }
                }
            }
            return result;
        }

        private static Pg33MachineWord[] CloneWords(Pg33MachineWord[] source)
        {
            Pg33MachineWord[] copy = new Pg33MachineWord[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static bool[] CloneValid(bool[] source)
        {
            bool[] copy = new bool[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static void TestV156Boundary323ToThreeWordRoundTrip()
        {
            Pg33MachineWord[] words = new Pg33MachineWord[4000];
            bool[] valid = new bool[4000];
            int highest = TP02PgV156Scenario.SeedBoundary323(words, valid);
            Check(highest == 322, "V156 seed deve terminar em 0322");

            int pagesBefore;
            int endBefore;
            List<Pg33MachineWord> before = ReadCanonicalLikeV153(
                words, valid, highest, out pagesBefore, out endBefore);
            Check(pagesBefore == 5, "V156 backup deve usar 5 paginas PG34");
            Check(before.Count == 323, "V156 backup deve conter 323 words");
            Check(endBefore == 322, "V156 backup deve encontrar END em 0322");
            Check(before[0].High == 0x00 && before[0].Low == 0x10 && before[0].External == 0x01,
                "V156 backup word 0000 deve ser STR X0001/BRAW 01");
            Check(before[1].High == 0x40 && before[1].Low == 0x40 && before[1].External == 0x08,
                "V156 backup word 0001 deve ser OUT C0001/BRAW 08");
            Check(before[322].High == 0x00 && before[322].Low == 0x70 && before[322].External == 0x07,
                "V156 backup word 0322 deve ser END/BRAW 07");

            Pg33MachineWord[] backupWords = CloneWords(words);
            bool[] backupValid = CloneValid(valid);

            byte[] pg33 = TP02PgV156Scenario.BuildMinimalC0001Pg33();
            Check(Sum8(pg33) == 0xFF, "V156 PG33 minimo deve fechar checksum FF");

            int startStep;
            Pg33MachineWord[] decoded;
            string reason;
            bool decodedOk = TP02PgEmulatorProgram.TryDecodePg33(
                pg33, out startStep, out decoded, out reason);
            Check(decodedOk, "V156 decoder PG33 recusou quadro minimo: " + reason);
            if (!decodedOk) return;

            Check(startStep == 0, "V156 PG33 minimo deve iniciar em 0000");
            Check(decoded.Length == 3, "V156 PG33 minimo deve possuir 3 words");
            for (int i = 0; i < decoded.Length; i++)
            {
                words[startStep + i] = decoded[i];
                valid[startStep + i] = true;
            }
            highest = Math.Max(highest, startStep + decoded.Length - 1);

            // Reproduz a memoria fisica observada: words antigos depois do primeiro
            // END podem continuar presentes. A leitura logica deve parar no END.
            Check(valid[3] && words[3].High == 0x40 && words[3].Low == 0x40,
                "V156 deve preservar residuo bruto depois do novo END");

            byte[] r38 = TP02PgReadback.Build38(words, valid, highest, null);
            byte[] expected38 = new byte[] { 0x00, 0x02, 0x00, 0x04, 0xF9 };
            Check(r38.Length == expected38.Length, "V156 PG38 pos-write com tamanho inesperado");
            for (int i = 0; i < Math.Min(r38.Length, expected38.Length); i++)
                Check(r38[i] == expected38[i], "V156 PG38 pos-write divergiu no byte " + i.ToString());

            int pagesAfter;
            int endAfter;
            List<Pg33MachineWord> after = ReadCanonicalLikeV153(
                words, valid, highest, out pagesAfter, out endAfter);
            Check(pagesAfter == 1, "V156 readback novo deve terminar na primeira pagina");
            Check(after.Count == 3, "V156 readback novo deve ter 3 words logicas");
            Check(endAfter == 2, "V156 readback novo deve encontrar END em 0002");
            Check(after[0].High == 0x00 && after[0].Low == 0x10 && after[0].External == 0x01,
                "V156 word 0000 novo deve ser STR X0001/BRAW 01");
            Check(after[1].High == 0x40 && after[1].Low == 0x40 && after[1].External == 0x08,
                "V156 word 0001 novo deve ser OUT C0001/BRAW 08");
            Check(after[2].High == 0x00 && after[2].Low == 0x70 && after[2].External == 0x07,
                "V156 word 0002 novo deve ser END/BRAW 07");

            Check(backupValid[322]
                && backupWords[322].High == 0x00 && backupWords[322].Low == 0x70,
                "V156 backup original de 323 words deve permanecer preservado");
        }

        internal static int RunV156Scenario(bool verbose)
        {
            failures = 0;
            TestV156Boundary323ToThreeWordRoundTrip();
            if (failures != 0)
            {
                Console.Error.WriteLine("TP02 v1.56 emulator roundtrip: " + failures.ToString() + " falha(s).");
                return 1;
            }
            if (verbose)
            {
                Console.WriteLine("TP02 v1.56 emulator roundtrip: PASS");
                Console.WriteLine("backup=323 words / 5 paginas / END=0322");
                Console.WriteLine("write=PG33 3 words / STR X0001 / OUT C0001 / END");
                Console.WriteLine("readback=3 words / 1 pagina / END=0002 / stale tail preservado");
            }
            return 0;
        }

        internal static int RunAll(bool verbose)
        {
            failures = 0;
            TestPhysicalMixedProgramBraw();
            TestWriteThenReadback();
            TestSecondPage();
            TestV156Boundary323ToThreeWordRoundTrip();

            if (failures != 0)
            {
                Console.Error.WriteLine("TP02 PG readback self-test: " + failures.ToString() + " falha(s).");
                return 1;
            }

            if (verbose) Console.WriteLine("TP02 PG readback self-test: PASS (inclui cenario v1.56 323->3)");
            return 0;
        }

        public static int Main()
        {
            return RunAll(true);
        }
    }
}
