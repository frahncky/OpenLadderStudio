using System;
using System.Collections.Generic;
using System.Globalization;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Core.Tests
{
    internal static class Tp02TargetCompilerSelfTest
    {
        private static int failures;

        private static int Main()
        {
            Console.WriteLine("Autoteste do compilador experimental TP02");
            Console.WriteLine();

            Check("STR X0001", Tp02TargetCompiler.EncodeBitInstruction("STR", "X", 1).ToHex() == "001000");
            Check("STR NOT X0001", Tp02TargetCompiler.EncodeBitInstruction("STR NOT", "X", 1).ToHex() == "001800");
            Check("AND NOT X0002", Tp02TargetCompiler.EncodeBitInstruction("AND NOT", "X", 2).ToHex() == "002900");
            Check("OUT Y0001", Tp02TargetCompiler.EncodeBitInstruction("OUT", "Y", 1).ToHex() == "204000");
            Check("AND STR", Tp02TargetCompiler.EncodeBitInstruction("AND STR", "X", 1).ToHex() == "000100");
            Check("OR STR", Tp02TargetCompiler.EncodeBitInstruction("OR STR", "X", 1).ToHex() == "000200");
            Check("NOP", Tp02TargetCompiler.EncodeNop().ToHex() == "000000");

            Check("TMR 0001", Tp02TargetCompiler.EncodeTimer(1).ToHex() == "006000");
            Check("CNT 0001", Tp02TargetCompiler.EncodeCounter(1).ToHex() == "006800");
            Check("literal 1000", Tp02TargetCompiler.EncodeLiteral16(1000).ToHex() == "876800");

            Check("operando especial X0001", Tp02TargetCompiler.EncodeSpecialBitOperand("X", 1).ToHex() == "C08000");
            Check("operando especial Y0009", Tp02TargetCompiler.EncodeSpecialBitOperand("Y", 9).ToHex() == "C88100");

            Check("F-00 END", Tp02TargetCompiler.EncodeFunctionPrefix("F-00").ToHex() == "007000");
            Check("F-13 ADD", Tp02TargetCompiler.EncodeFunctionPrefix("F-13").ToHex() == "0D7300");
            Check("F-13w ADD", Tp02TargetCompiler.EncodeFunctionPrefix("F-13w").ToHex() == "0D7700");
            Check("F-13d ADD", Tp02TargetCompiler.EncodeFunctionPrefix("F-13d").ToHex() == "0DF300");
            Check("F-50w STMR", Tp02TargetCompiler.EncodeFunctionPrefix("F-50w").ToHex() == "327700");
            Check("F-42 LB001", Tp02TargetCompiler.EncodeLabel(1).ToHex() == "2A7800");

            Tp02MachineWord[] jump = Tp02TargetCompiler.EncodeJump(1);
            Check("F-43 JMP LB001", jump.Length == 2 && jump[0].ToHex() == "2B7100" && jump[1].ToHex() == "800000");

            Check("checksum SCS do manual", Tp02TargetCompiler.BuildHostFrame(1, 5, "SCS", "Y00011") == ":01?5SCSY00011F7\r");
            Check("RBP do manual", Tp02TargetCompiler.BuildRbp(1, 0, 3, 5) == ":01?5RBP00000324\r");

            List<Tp02MachineWord> program = new List<Tp02MachineWord>();
            program.Add(Tp02TargetCompiler.EncodeBitInstruction("STR", "X", 1));
            program.Add(Tp02TargetCompiler.EncodeBitInstruction("AND", "X", 2));
            program.Add(Tp02TargetCompiler.EncodeBitInstruction("OUT", "Y", 1));
            Check("WBP dry-run de três passos", Tp02TargetCompiler.BuildWbpDryRun(1, 0, program, 5) == ":01?5WBP000003001000002100204000B5\r");

            Check("F-33 ambígua é recusada", ThrowsArgument(delegate { Tp02TargetCompiler.GetFunction("F-33"); }));
            Check("WBP dry-run não aceita mais de 100 passos", ThrowsArgumentOutOfRange(delegate
            {
                List<Tp02MachineWord> tooMany = new List<Tp02MachineWord>();
                int i;
                for (i = 0; i < 101; i++) tooMany.Add(Tp02TargetCompiler.EncodeNop());
                Tp02TargetCompiler.BuildWbpDryRun(1, 0, tooMany, 5);
            }));

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("Todas as verificações passaram.");
                return 0;
            }

            Console.WriteLine(failures.ToString(CultureInfo.InvariantCulture) + " verificação(ões) falharam.");
            return 1;
        }

        private static bool ThrowsArgument(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        private static bool ThrowsArgumentOutOfRange(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return true;
            }
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
