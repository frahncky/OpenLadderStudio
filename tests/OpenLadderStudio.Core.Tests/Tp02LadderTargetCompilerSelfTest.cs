using System;
using System.Collections.Generic;
using System.Globalization;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Core.Tests
{
    internal static class Tp02LadderTargetCompilerSelfTest
    {
        private static int failures;

        private static int Main()
        {
            Console.WriteLine("Autoteste Ladder -> TP02 (dry-run)");
            Console.WriteLine();

            TestSeries();
            TestParallel();
            TestTimerCounter();
            TestSetReset();
            TestFunctions();
            TestEnd();
            TestWbpChunks();
            TestRejectedCases();

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("Todas as verificações passaram.");
                return 0;
            }

            Console.WriteLine(failures.ToString(CultureInfo.InvariantCulture) + " verificação(ões) falharam.");
            return 1;
        }

        private static void TestSeries()
        {
            LadderProjectRung rung = NewRung();
            rung.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            rung.Series[1] = E(LadderProjectElementKind.ContactNormallyOpen, "X0002", "", "");
            rung.Series[7] = E(LadderProjectElementKind.Coil, "Y0001", "", "");
            Tp02LadderCompilationResult result = Compile(rung);
            Check("série compila", result.Success);
            Check("série gera 3 passos", result.Words.Count == 3);
            Check("série: STR/AND/OUT", Hex(result) == "001000002100204000");

            rung.Series[1] = E(LadderProjectElementKind.ContactNormallyClosed, "X0002", "", "");
            result = Compile(rung);
            Check("contato NF usa AND NOT", Hex(result) == "001000002900204000");
        }

        private static void TestParallel()
        {
            LadderProjectRung first = NewRung();
            first.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            first.Parallel[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0002", "", "");
            first.Series[7] = E(LadderProjectElementKind.Coil, "Y0001", "", "");
            Tp02LadderCompilationResult result = Compile(first);
            Check("paralelo na primeira coluna", Hex(result) == "001000003100204000");

            LadderProjectRung later = NewRung();
            later.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            later.Series[1] = E(LadderProjectElementKind.ContactNormallyOpen, "X0002", "", "");
            later.Parallel[1] = E(LadderProjectElementKind.ContactNormallyOpen, "X0003", "", "");
            later.Series[7] = E(LadderProjectElementKind.Coil, "Y0001", "", "");
            result = Compile(later);
            Check("paralelo após condição usa pilha Boolean", Hex(result) == "001000001100003200000100204000");
        }

        private static void TestTimerCounter()
        {
            LadderProjectRung timer = NewRung();
            timer.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            timer.Series[7] = E(LadderProjectElementKind.Timer, "V0001", "1000", "");
            Tp02LadderCompilationResult result = Compile(timer);
            Check("TMR + preset", Hex(result) == "001000006000876800");

            LadderProjectRung counter = NewRung();
            counter.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            counter.Series[7] = E(LadderProjectElementKind.Counter, "V0001", "10", "");
            result = Compile(counter);
            Check("CNT + preset", Hex(result) == "001000006800800A00");
        }

        private static void TestSetReset()
        {
            LadderProjectRung set = NewRung();
            set.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            set.Series[7] = E(LadderProjectElementKind.Set, "Y0001", "", "");
            Tp02LadderCompilationResult result = Compile(set);
            Check("SET F-23", Hex(result) == "001000177100C88000");

            LadderProjectRung reset = NewRung();
            reset.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            reset.Series[7] = E(LadderProjectElementKind.Reset, "C0001", "", "");
            result = Compile(reset);
            Check("RESET F-24", Hex(result) == "001000187100D08000");
        }

        private static void TestFunctions()
        {
            LadderProjectRung add = NewRung();
            add.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            add.Series[7] = E(LadderProjectElementKind.Function, "F-13w", "D0001,10,D0002", "");
            Tp02LadderCompilationResult result = Compile(add);
            Check("F-13w ADD com operandos", Hex(result) == "0010000D7700F00000800A00F00100");

            LadderProjectRung jump = NewRung();
            jump.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            jump.Series[7] = E(LadderProjectElementKind.Function, "F-43", "LB001", "");
            result = Compile(jump);
            Check("F-43 JMP", Hex(result) == "0010002B7100800000");
        }

        private static void TestEnd()
        {
            LadderProjectRung end = NewRung();
            end.Series[7] = E(LadderProjectElementKind.End, "F-00", "", "");
            Tp02LadderCompilationResult result = Compile(end);
            Check("END sem condição é permitido", result.Success && Hex(result) == "007000");
        }

        private static void TestWbpChunks()
        {
            LadderProjectRung rung = NewRung();
            rung.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            rung.Series[1] = E(LadderProjectElementKind.ContactNormallyOpen, "X0002", "", "");
            rung.Series[7] = E(LadderProjectElementKind.Coil, "Y0001", "", "");
            Tp02LadderCompilationResult result = Compile(rung);
            List<string> frames = Tp02LadderTargetCompiler.BuildWbpDryRunFrames(result, 1, 0, 5);
            Check("WBP dry-run do Ladder", frames.Count == 1 && frames[0] == ":01?5WBP000003001000002100204000B5\r");
        }

        private static void TestRejectedCases()
        {
            LadderProjectRung sc = NewRung();
            sc.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "SC001", "", "");
            sc.Series[7] = E(LadderProjectElementKind.Coil, "Y0001", "", "");
            Check("SC não confirmado é recusado", !Compile(sc).Success);

            LadderProjectRung presetD = NewRung();
            presetD.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            presetD.Series[7] = E(LadderProjectElementKind.Timer, "V0001", "D0001", "");
            Check("preset D não confirmado é recusado", !Compile(presetD).Success);

            LadderProjectRung badParallel = NewRung();
            badParallel.Parallel[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0002", "", "");
            badParallel.Series[7] = E(LadderProjectElementKind.Coil, "Y0001", "", "");
            Check("paralelo sem principal é recusado", !Compile(badParallel).Success);

            LadderProjectRung f33 = NewRung();
            f33.Series[0] = E(LadderProjectElementKind.ContactNormallyOpen, "X0001", "", "");
            f33.Series[7] = E(LadderProjectElementKind.Function, "F-33", "D0001", "");
            Check("F-33 ambígua continua recusada", !Compile(f33).Success);
        }

        private static Tp02LadderCompilationResult Compile(LadderProjectRung rung)
        {
            LadderProjectDocument document = new LadderProjectDocument();
            document.Rungs.Add(rung);
            return Tp02LadderTargetCompiler.Compile(document);
        }

        private static LadderProjectRung NewRung()
        {
            return new LadderProjectRung();
        }

        private static LadderProjectElement E(LadderProjectElementKind kind, string address, string parameter, string mode)
        {
            LadderProjectElement element = new LadderProjectElement();
            element.Kind = kind;
            element.Address = address;
            element.Parameter = parameter;
            element.Mode = mode;
            return element;
        }

        private static string Hex(Tp02LadderCompilationResult result)
        {
            return Tp02TargetCompiler.WordsToHex(result.Words);
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
