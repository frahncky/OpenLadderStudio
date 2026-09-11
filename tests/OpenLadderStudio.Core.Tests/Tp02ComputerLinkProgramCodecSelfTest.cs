using System;
using System.Collections.Generic;
using System.Globalization;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Core.Tests
{
    internal static class Tp02ComputerLinkProgramCodecSelfTest
    {
        private static int failures;

        private static int Main()
        {
            Console.WriteLine("Autoteste TP02 Computer Link RBP/WBP");
            Console.WriteLine();

            Check("SCS do manual usa prefixo ::",
                Tp02ComputerLinkProgramCodec.BuildFrame(1, 5, "SCS", "Y00011") == "::01?5SCSY00011F7\r");

            Check("RBP oficial 0000/3",
                Tp02ComputerLinkProgramCodec.BuildRbp(1, 0, 3, 5) == "::01?5RBP00000324\r");

            Tp02ComputerLinkResponse rbpOfficial = Tp02ComputerLinkProgramCodec.ParseResponse(
                "::01#5RBP5E150920400620C10FA2\r", "RBP");
            Check("RBP oficial: checksum e 3 words",
                rbpOfficial.ChecksumOk
                && !rbpOfficial.IsError
                && rbpOfficial.Command == "RBP"
                && rbpOfficial.Data == "5E150920400620C10F");

            Tp02ComputerLinkState psrState;
            Check("PSR STOP parseado",
                Tp02ComputerLinkProgramCodec.TryGetPsrState("::01#5PSR022\r", out psrState)
                && psrState == Tp02ComputerLinkState.Stop);

            string wbpErrorCode;
            Check("WBP sucesso parseado",
                Tp02ComputerLinkProgramCodec.IsSuccessfulResponse("::01#5WBP5E\r", "WBP", out wbpErrorCode)
                && string.IsNullOrEmpty(wbpErrorCode));

            Check("WBP erro 02 detectado",
                !Tp02ComputerLinkProgramCodec.IsSuccessfulResponse("::01%5WBP02FA\r", "WBP", out wbpErrorCode)
                && wbpErrorCode == "02");

            List<Tp02MachineWord> three = new List<Tp02MachineWord>();
            three.Add(new Tp02MachineWord(0x00, 0x10, 0x00));
            three.Add(new Tp02MachineWord(0x00, 0x21, 0x00));
            three.Add(new Tp02MachineWord(0x20, 0x40, 0x00));
            Check("WBP 3 passos",
                Tp02ComputerLinkProgramCodec.BuildWbp(1, 0, three, 5)
                == "::01?5WBP000003001000002100204000B5\r");

            List<Tp02MachineWord> hundred = new List<Tp02MachineWord>();
            int i;
            for (i = 0; i < 100; i++) hundred.Add(new Tp02MachineWord(0, 0, 0));
            string hundredFrame = Tp02ComputerLinkProgramCodec.BuildWbp(1, 0, hundred, 5);
            Check("WBP 100 passos codifica quantidade como 00", hundredFrame.IndexOf("WBP000000", StringComparison.Ordinal) >= 0);

            List<Tp02MachineWord> parsed = Tp02ComputerLinkProgramCodec.ParseMachineHex("001000 002100 204000");
            Check("parser 3 bytes por passo", parsed.Count == 3 && parsed[0].ToHex() == "001000" && parsed[2].ToHex() == "204000");

            List<Tp02MachineWord> twoHundredOne = new List<Tp02MachineWord>();
            for (i = 0; i < 201; i++) twoHundredOne.Add(new Tp02MachineWord(0, 0, 0));
            List<string> chunks = Tp02ComputerLinkProgramCodec.BuildWbpProgramFrames(1, 10, twoHundredOne, 5);
            Check("WBP pagina 100/100/1", chunks.Count == 3
                && chunks[0].IndexOf("WBP001000", StringComparison.Ordinal) >= 0
                && chunks[1].IndexOf("WBP011000", StringComparison.Ordinal) >= 0
                && chunks[2].IndexOf("WBP021001", StringComparison.Ordinal) >= 0);

            Check("RBP rejeita faixa acima de 4000", ThrowsRange(delegate
            {
                Tp02ComputerLinkProgramCodec.BuildRbp(1, 3999, 3, 5);
            }));

            Check("WBP rejeita mais de 100 passos por quadro", ThrowsRange(delegate
            {
                List<Tp02MachineWord> words = new List<Tp02MachineWord>();
                int j;
                for (j = 0; j < 101; j++) words.Add(new Tp02MachineWord(0, 0, 0));
                Tp02ComputerLinkProgramCodec.BuildWbp(1, 0, words, 5);
            }));

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("Todas as verificacoes passaram.");
                return 0;
            }

            Console.WriteLine(failures.ToString(CultureInfo.InvariantCulture) + " verificacao(oes) falharam.");
            return 1;
        }

        private static bool ThrowsRange(Action action)
        {
            try { action(); return false; }
            catch (ArgumentOutOfRangeException) { return true; }
        }

        private static void Check(string text, bool ok)
        {
            if (ok) Console.WriteLine("  ok    " + text);
            else { failures++; Console.WriteLine("  FALHA " + text); }
        }
    }
}