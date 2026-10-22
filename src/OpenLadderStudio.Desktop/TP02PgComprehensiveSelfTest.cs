using System;

namespace ModernPC12
{
    /// <summary>
    /// Matriz ampla de testes offline do emulador PG do WEG TP02.
    /// Nao abre porta COM e nao envia nenhum byte ao PLC fisico.
    /// </summary>
    internal static class TP02PgComprehensiveSelfTest
    {
        private static int checks;
        private static int failures;

        private static void Check(bool condition, string message)
        {
            checks++;
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

        private static byte[] Clone(byte[] source)
        {
            if (source == null) return null;
            byte[] copy = new byte[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static Pg33MachineWord[] MakeWords(int count, int seed)
        {
            Pg33MachineWord[] words = new Pg33MachineWord[count];
            for (int i = 0; i < count; i++)
            {
                byte high = (byte)((seed + (i * 17) + 3) & 0xFF);
                byte low = (byte)(((seed * 3) + (i * 29) + 7) & 0xFF);
                byte external = (byte)(((seed * 5) + (i * 11) + 13) & 0xFF);
                words[i] = new Pg33MachineWord(high, low, external);
            }
            return words;
        }

        private static void CheckWordsEqual(Pg33MachineWord[] expected, Pg33MachineWord[] actual, string label)
        {
            Check(expected != null && actual != null, label + ": arrays nulos");
            if (expected == null || actual == null) return;
            Check(expected.Length == actual.Length, label + ": quantidade de words divergente");
            int n = Math.Min(expected.Length, actual.Length);
            for (int i = 0; i < n; i++)
            {
                Check(expected[i].High == actual[i].High, label + ": HIGH divergente em " + i.ToString());
                Check(expected[i].Low == actual[i].Low, label + ": LOW divergente em " + i.ToString());
                Check(expected[i].External == actual[i].External, label + ": EXTERNAL divergente em " + i.ToString());
            }
        }

        private static void ExpectArgumentOutOfRange(Action action, string label)
        {
            bool threw = false;
            try { action(); }
            catch (ArgumentOutOfRangeException) { threw = true; }
            catch (Exception ex)
            {
                failures++;
                checks++;
                Console.Error.WriteLine("FALHA: " + label + ": excecao inesperada " + ex.GetType().Name);
                return;
            }
            Check(threw, label + ": deveria lancar ArgumentOutOfRangeException");
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

        private static void TestLegacySuite()
        {
            Check(TP02PgReadbackSelfTest.RunAll(false) == 0, "suite legada do readback/v1.56 falhou");
        }

        private static void TestBrawExhaustive()
        {
            for (int high = 0; high <= 0xFF; high++)
            {
                for (int low = 0; low <= 0xFF; low++)
                {
                    byte expected = (byte)(((high >> 4) + (high & 0x0F) + (low >> 4) + (low & 0x0F)) & 0x0F);
                    byte actual = TP02PgReadback.CalculateBraw((byte)high, (byte)low);
                    Check(actual == expected, "BRAW exaustivo divergiu em " + high.ToString("X2") + " " + low.ToString("X2"));
                }
            }
        }

        private static void TestPg33RoundTrips()
        {
            int[] counts = new int[] { 1, 2, 3, 7, 16, 79, 80 };
            for (int c = 0; c < counts.Length; c++)
            {
                int count = counts[c];
                int[] starts = new int[] { 0, 1, 79, 80, 4000 - count };
                for (int s = 0; s < starts.Length; s++)
                {
                    int start = starts[s];
                    Pg33MachineWord[] expected = MakeWords(count, (count * 19) + start);
                    byte[] frame = TP02PgV156Scenario.BuildPg33(start, expected);
                    Check(Sum8(frame) == 0xFF, "PG33 checksum nao fecha FF count=" + count.ToString() + " start=" + start.ToString());

                    int decodedStart;
                    Pg33MachineWord[] decoded;
                    string reason;
                    bool ok = TP02PgEmulatorProgram.TryDecodePg33(frame, out decodedStart, out decoded, out reason);
                    Check(ok, "PG33 valido recusado count=" + count.ToString() + " start=" + start.ToString() + " reason=" + reason);
                    if (!ok) continue;
                    Check(decodedStart == start, "PG33 start divergente");
                    CheckWordsEqual(expected, decoded, "PG33 roundtrip count=" + count.ToString() + " start=" + start.ToString());
                }
            }

            // Fuzz deterministico: 500 quadros validos cobrindo toda a faixa 1..80.
            for (int i = 0; i < 500; i++)
            {
                int count = ((i * 37) % 80) + 1;
                int maxStart = 4000 - count;
                int start = (i * 997) % (maxStart + 1);
                Pg33MachineWord[] expected = MakeWords(count, (i * 23) + 5);
                byte[] frame = TP02PgV156Scenario.BuildPg33(start, expected);
                int decodedStart;
                Pg33MachineWord[] decoded;
                string reason;
                bool ok = TP02PgEmulatorProgram.TryDecodePg33(frame, out decodedStart, out decoded, out reason);
                Check(ok, "PG33 fuzz valido recusado i=" + i.ToString() + " reason=" + reason);
                if (!ok) continue;
                Check(decodedStart == start, "PG33 fuzz start divergente i=" + i.ToString());
                CheckWordsEqual(expected, decoded, "PG33 fuzz i=" + i.ToString());
                Check(Sum8(frame) == 0xFF, "PG33 fuzz checksum divergente i=" + i.ToString());
            }
        }

        private static void TestPg33InvalidFrames()
        {
            int start;
            Pg33MachineWord[] words;
            string reason;

            Check(!TP02PgEmulatorProgram.TryDecodePg33(null, out start, out words, out reason), "PG33 null deveria falhar");
            Check(!TP02PgEmulatorProgram.TryDecodePg33(new byte[9], out start, out words, out reason), "PG33 curto deveria falhar");

            byte[] valid3 = TP02PgV156Scenario.BuildPg33(0, MakeWords(3, 9));

            byte[] wrongOpcode = Clone(valid3);
            wrongOpcode[0] = 0x32;
            Check(!TP02PgEmulatorProgram.TryDecodePg33(wrongOpcode, out start, out words, out reason), "PG33 opcode incorreto deveria falhar");

            byte[] reserved = Clone(valid3);
            reserved[2] = 0x01;
            Check(!TP02PgEmulatorProgram.TryDecodePg33(reserved, out start, out words, out reason), "PG33 TX[2] != 00 deveria falhar");

            byte[] zeroWords = Clone(valid3);
            zeroWords[5] = 0x00;
            Check(!TP02PgEmulatorProgram.TryDecodePg33(zeroWords, out start, out words, out reason), "PG33 0 words deveria falhar");

            byte[] oddPlane = Clone(valid3);
            oddPlane[5] = 0x05;
            Check(!TP02PgEmulatorProgram.TryDecodePg33(oddPlane, out start, out words, out reason), "PG33 2*W impar deveria falhar");

            byte[] tooManyWords = Clone(valid3);
            tooManyWords[5] = 0xA2;
            Check(!TP02PgEmulatorProgram.TryDecodePg33(tooManyWords, out start, out words, out reason), "PG33 81 words deveria falhar");

            byte[] badLen = Clone(valid3);
            badLen[1]++;
            Check(!TP02PgEmulatorProgram.TryDecodePg33(badLen, out start, out words, out reason), "PG33 LEN inconsistente deveria falhar");

            byte[] shortTotal = new byte[valid3.Length - 1];
            Array.Copy(valid3, shortTotal, shortTotal.Length);
            Check(!TP02PgEmulatorProgram.TryDecodePg33(shortTotal, out start, out words, out reason), "PG33 comprimento total inconsistente deveria falhar");

            byte[] outOfRange = TP02PgV156Scenario.BuildPg33(3999, MakeWords(1, 3));
            outOfRange[3] = 0x0F;
            outOfRange[4] = 0xA0; // 4000
            Check(!TP02PgEmulatorProgram.TryDecodePg33(outOfRange, out start, out words, out reason), "PG33 start=4000 deveria falhar");

            byte[] overflow = TP02PgV156Scenario.BuildPg33(3920, MakeWords(80, 4));
            overflow[3] = 0x0F;
            overflow[4] = 0x6E; // 3950 + 80 > 4000
            Check(!TP02PgEmulatorProgram.TryDecodePg33(overflow, out start, out words, out reason), "PG33 faixa acima de 3999 deveria falhar");
        }

        private static void TestPg33BuilderLimits()
        {
            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.BuildPg33(0, null); }, "BuildPg33 null");
            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.BuildPg33(0, new Pg33MachineWord[0]); }, "BuildPg33 0 words");
            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.BuildPg33(0, new Pg33MachineWord[81]); }, "BuildPg33 81 words");
            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.BuildPg33(-1, MakeWords(1, 1)); }, "BuildPg33 start negativo");
            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.BuildPg33(3999, MakeWords(2, 1)); }, "BuildPg33 overflow");

            byte[] edge = TP02PgV156Scenario.BuildPg33(3920, MakeWords(80, 11));
            Check(edge != null && Sum8(edge) == 0xFF, "BuildPg33 limite 3920+80 deveria ser valido");
        }

        private static void TestReadback38Boundaries()
        {
            int[] counts = new int[] { 1, 2, 3, 79, 80, 81, 323 };
            for (int c = 0; c < counts.Length; c++)
            {
                int count = counts[c];
                Pg33MachineWord[] words = new Pg33MachineWord[4000];
                bool[] valid = new bool[4000];
                for (int i = 0; i < count; i++)
                {
                    words[i] = new Pg33MachineWord(0x12, (byte)(i & 0xFF), 0x00);
                    valid[i] = true;
                }
                byte[] r38 = TP02PgReadback.Build38(words, valid, count - 1, null);
                int expectedOffset = 2 * (Math.Min(count, 80) - 1);
                Check(r38.Length == 5, "PG38 tamanho invalido count=" + count.ToString());
                if (r38.Length == 5)
                {
                    Check(r38[3] == (byte)expectedOffset, "PG38 offset invalido count=" + count.ToString());
                    Check(Sum8(r38) == 0xFF, "PG38 checksum invalido count=" + count.ToString());
                }
            }

            Pg33MachineWord[] endWords = new Pg33MachineWord[4000];
            bool[] endValid = new bool[4000];
            for (int i = 0; i < 10; i++)
            {
                endWords[i] = new Pg33MachineWord(0x11, (byte)i, 0x00);
                endValid[i] = true;
            }
            endWords[2] = new Pg33MachineWord(0x00, 0x70, 0x00);
            byte[] end38 = TP02PgReadback.Build38(endWords, endValid, 9, null);
            Check(end38.Length == 5 && end38[3] == 0x04, "PG38 deve parar no primeiro END");

            Pg33MachineWord[] gapWords = new Pg33MachineWord[4000];
            bool[] gapValid = new bool[4000];
            gapWords[0] = new Pg33MachineWord(0x01, 0x01, 0x00);
            gapWords[1] = new Pg33MachineWord(0x01, 0x02, 0x00);
            gapWords[3] = new Pg33MachineWord(0x01, 0x03, 0x00);
            gapValid[0] = gapValid[1] = gapValid[3] = true;
            byte[] gap38 = TP02PgReadback.Build38(gapWords, gapValid, 3, null);
            Check(gap38.Length == 5 && gap38[3] == 0x02, "PG38 deve parar na primeira lacuna");

            byte[] fallback = new byte[] { 0x00, 0x02, 0x00, 0x0A, 0xF3 };
            byte[] fallback38 = TP02PgReadback.Build38(null, null, -1, fallback);
            Check(fallback38.Length == fallback.Length, "PG38 fallback tamanho divergente");
            Check(!Object.ReferenceEquals(fallback38, fallback), "PG38 fallback deve ser clone");
            for (int i = 0; i < fallback.Length && i < fallback38.Length; i++)
                Check(fallback38[i] == fallback[i], "PG38 fallback byte divergente");
        }

        private static void TestReadback34AllPages()
        {
            Pg33MachineWord[] words = new Pg33MachineWord[4000];
            bool[] valid = new bool[4000];
            for (int i = 0; i < 4000; i++)
            {
                words[i] = new Pg33MachineWord((byte)((i * 7) & 0xFF), (byte)((i * 13) & 0xFF), (byte)((i * 17) & 0xFF));
                valid[i] = true;
            }

            for (int start = 0; start < 4000; start += 80)
            {
                byte[] r34 = TP02PgReadback.Build34(Build34Request(start), words, valid, 3999, null);
                Check(r34.Length == 243, "PG34 tamanho deve ser 243 start=" + start.ToString());
                Check(Sum8(r34) == 0xFF, "PG34 checksum invalido start=" + start.ToString());
                if (r34.Length != 243) continue;

                for (int local = 0; local < 80; local++)
                {
                    int step = start + local;
                    Check(r34[2 + (2 * local)] == words[step].High, "PG34 HIGH divergente step=" + step.ToString());
                    Check(r34[2 + (2 * local) + 1] == words[step].Low, "PG34 LOW divergente step=" + step.ToString());
                    Check(r34[2 + 0xA0 + local] == TP02PgReadback.CalculateBraw(words[step].High, words[step].Low), "PG34 BRAW divergente step=" + step.ToString());
                }
            }

            Pg33MachineWord[] sparseWords = new Pg33MachineWord[4000];
            bool[] sparseValid = new bool[4000];
            sparseWords[0] = new Pg33MachineWord(0x12, 0x34, 0xAA);
            sparseWords[2] = new Pg33MachineWord(0x56, 0x78, 0xBB);
            sparseValid[0] = sparseValid[2] = true;
            byte[] sparse = TP02PgReadback.Build34(Build34Request(0), sparseWords, sparseValid, 2, null);
            Check(sparse[2] == 0x12 && sparse[3] == 0x34, "PG34 sparse step0 ausente");
            Check(sparse[4] == 0x00 && sparse[5] == 0x00, "PG34 sparse lacuna deveria zerar");
            Check(sparse[6] == 0x56 && sparse[7] == 0x78, "PG34 sparse step2 ausente");

            // PG34 representa memoria bruta: dados validos depois do END permanecem legiveis.
            Pg33MachineWord[] staleWords = new Pg33MachineWord[4000];
            bool[] staleValid = new bool[4000];
            staleWords[0] = new Pg33MachineWord(0x00, 0x70, 0x00);
            staleWords[1] = new Pg33MachineWord(0xAA, 0x55, 0x00);
            staleValid[0] = staleValid[1] = true;
            byte[] stale = TP02PgReadback.Build34(Build34Request(0), staleWords, staleValid, 1, null);
            Check(stale[2] == 0x00 && stale[3] == 0x70, "PG34 END bruto ausente");
            Check(stale[4] == 0xAA && stale[5] == 0x55, "PG34 deve preservar tail bruto apos END");

            byte[] fallbackPayload = new byte[240];
            fallbackPayload[0] = 0xDE;
            fallbackPayload[1] = 0xAD;
            fallbackPayload[160] = 0x0B;
            byte[] fallback0 = TP02PgReadback.Build34(Build34Request(0), null, null, -1, fallbackPayload);
            Check(fallback0[2] == 0xDE && fallback0[3] == 0xAD, "PG34 fallback pagina 0 divergente");
            Check(fallback0[162] == 0x0B, "PG34 fallback BRAW divergente");
            Check(Sum8(fallback0) == 0xFF, "PG34 fallback checksum invalido");

            byte[] fallback80 = TP02PgReadback.Build34(Build34Request(80), null, null, -1, fallbackPayload);
            Check(fallback80[2] == 0x00 && fallback80[3] == 0x00, "PG34 fallback pagina >0 deveria zerar");

            byte[] nullRequest = TP02PgReadback.Build34(null, null, null, -1, fallbackPayload);
            Check(nullRequest[2] == 0xDE && nullRequest[3] == 0xAD, "PG34 request invalido deve cair na pagina 0");
        }

        private static void TestScenarioControls()
        {
            // --self-test-all ja foi parseado pelo executavel antes de chegar aqui.
            Check(TP02PgV156Scenario.SelfTestMode == "ALL", "SelfTestMode deveria ser ALL");

            TP02PgV156Scenario.HelloRespondOnAttempt = 5;
            string detail;
            for (int i = 1; i <= 4; i++)
                Check(!TP02PgV156Scenario.ShouldRespondHello(out detail), "HELLO deveria ficar mudo na tentativa " + i.ToString());
            Check(TP02PgV156Scenario.ShouldRespondHello(out detail), "HELLO deveria responder na 5a tentativa");
            Check(TP02PgV156Scenario.ShouldRespondHello(out detail), "HELLO deveria continuar respondendo apos a 5a");

            TP02PgV156Scenario.F0RespondOnAttempt = 4;
            for (int i = 1; i <= 3; i++)
                Check(!TP02PgV156Scenario.ShouldRespondF0(out detail), "F0 deveria ficar mudo na tentativa " + i.ToString());
            Check(TP02PgV156Scenario.ShouldRespondF0(out detail), "F0 deveria responder na 4a tentativa");
            Check(TP02PgV156Scenario.ShouldRespondF0(out detail), "F0 deveria continuar respondendo apos a 4a");

            Check(TP02PgV156Scenario.TryApplyArgument("--scenario=v156"), "--scenario=v156 deveria ser reconhecido");
            Check(TP02PgV156Scenario.SeedBoundary323Enabled, "cenario v1.56 deveria habilitar seed");
            Check(TP02PgV156Scenario.DisableUnknownAck, "cenario v1.56 deveria desabilitar ACK generico");
            Check(TP02PgV156Scenario.HelloRespondOnAttempt == 5, "cenario v1.56 HELLO deveria ser 5");
            Check(TP02PgV156Scenario.F0RespondOnAttempt == 4, "cenario v1.56 F0 deveria ser 4");
            Check(TP02PgV156Scenario.Describe().IndexOf("boundary323", StringComparison.Ordinal) >= 0, "Describe deveria indicar boundary323");

            Check(TP02PgV156Scenario.TryApplyArgument("--hello-on=8"), "--hello-on=8 deveria ser reconhecido");
            Check(TP02PgV156Scenario.HelloRespondOnAttempt == 8, "hello-on=8 nao aplicado");
            Check(TP02PgV156Scenario.TryApplyArgument("--f0-on=8"), "--f0-on=8 deveria ser reconhecido");
            Check(TP02PgV156Scenario.F0RespondOnAttempt == 8, "f0-on=8 nao aplicado");
            Check(!TP02PgV156Scenario.TryApplyArgument("--nao-existe"), "argumento desconhecido nao deveria ser reconhecido");

            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.TryApplyArgument("--hello-on=0"); }, "hello-on=0");
            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.TryApplyArgument("--hello-on=9"); }, "hello-on=9");
            ExpectArgumentOutOfRange(delegate { TP02PgV156Scenario.TryApplyArgument("--f0-on=x"); }, "f0-on=x");
        }

        private static void TestSeedBoundary323()
        {
            Pg33MachineWord[] words = new Pg33MachineWord[4000];
            bool[] valid = new bool[4000];
            int highest = TP02PgV156Scenario.SeedBoundary323(words, valid);
            Check(highest == 322, "seed boundary323 highest deveria ser 322");
            for (int i = 0; i < 322; i++)
            {
                Check(valid[i], "seed boundary323 valid=false step=" + i.ToString());
                if ((i & 1) == 0)
                    Check(words[i].High == 0x00 && words[i].Low == 0x10, "seed STR divergente step=" + i.ToString());
                else
                    Check(words[i].High == 0x40 && words[i].Low == 0x40, "seed OUT C divergente step=" + i.ToString());
            }
            Check(valid[322] && words[322].High == 0x00 && words[322].Low == 0x70, "seed END deveria estar em 0322");
            Check(!valid[323], "seed nao deveria marcar step 323");
        }

        internal static int RunAll(bool verbose)
        {
            checks = 0;
            failures = 0;

            TestLegacySuite();
            TestBrawExhaustive();
            TestPg33RoundTrips();
            TestPg33InvalidFrames();
            TestPg33BuilderLimits();
            TestReadback38Boundaries();
            TestReadback34AllPages();
            TestSeedBoundary323();
            TestScenarioControls();

            if (failures != 0)
            {
                Console.Error.WriteLine("TP02 emulator full matrix: " + failures.ToString() + " falha(s) em " + checks.ToString() + " verificacoes.");
                return 1;
            }

            if (verbose)
            {
                Console.WriteLine("TP02 emulator full matrix: PASS");
                Console.WriteLine("checks=" + checks.ToString());
                Console.WriteLine("coverage=legacy+BRAW65536+PG33_limits+PG33_fuzz500+PG34_50pages+PG38+END+sparse+fallback+v156_gates");
                Console.WriteLine("hardware=NAO UTILIZADO / COM=NAO ABERTA");
            }
            return 0;
        }
    }
}
