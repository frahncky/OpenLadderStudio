using System;

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

        private static void TestPhysicalMixedProgramBraw()
        {
            // Captura física TP02-PG-Lab-20260910-183546:
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

            Check(highLow.Length == expected.Length * 2, "fixture físico inconsistente");
            for (int i = 0; i < expected.Length; i++)
            {
                byte actual = TP02PgReadback.CalculateBraw(highLow[2 * i], highLow[(2 * i) + 1]);
                Check(actual == expected[i], "BRAW físico divergiu no passo " + i.ToString());
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

            byte[] request34 = new byte[] { 0x34,0x03,0x00,0x00,0xA0,0x28 };
            byte[] r34 = TP02PgReadback.Build34(request34, words, valid, 2, null);
            Check(r34.Length == 243, "34 deve ter 243 bytes totais");
            Check(r34[0] == 0x00 && r34[1] == 0xF0, "cabecalho 34 invalido");
            Check(Sum8(r34) == 0xFF, "checksum do 34 nao fecha FF");

            // HIGH/LOW plane.
            Check(r34[2] == 0x00 && r34[3] == 0x10, "word 0 HL incorreto");
            Check(r34[4] == 0x20 && r34[5] == 0x40, "word 1 HL incorreto");
            Check(r34[6] == 0x00 && r34[7] == 0x70, "word 2 HL incorreto");

            // BRAW plane começa no payload +0xA0 => frame index 2+0xA0.
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

            byte[] request34 = new byte[] { 0x34,0x03,0x00,0x50,0xA0,0xD8 };
            byte[] r34 = TP02PgReadback.Build34(request34, words, valid, 81, null);
            Check(r34[2] == 0x00 && r34[3] == 0x10, "pagina 2 nao iniciou no step 80");
            Check(r34[4] == 0x00 && r34[5] == 0x70, "pagina 2 nao trouxe END no step 81");
            Check(Sum8(r34) == 0xFF, "checksum pagina 2 nao fecha FF");
        }

        public static int Main()
        {
            TestPhysicalMixedProgramBraw();
            TestWriteThenReadback();
            TestSecondPage();

            if (failures != 0)
            {
                Console.Error.WriteLine("TP02 PG readback self-test: " + failures.ToString() + " falha(s).");
                return 1;
            }

            Console.WriteLine("TP02 PG readback self-test: PASS");
            return 0;
        }
    }
}
