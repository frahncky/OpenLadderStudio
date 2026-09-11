using System;
using System.Collections.Generic;
using OpenLadderStudio.Core;

internal static class Tp02Pg33DryRunProgramSelfTest
{
    private static int failures;

    private static void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FALHA: " + message);
    }

    private static void ExpectArgument(Action action, string message)
    {
        bool threw = false;
        try { action(); }
        catch (ArgumentException) { threw = true; }
        Check(threw, message);
    }

    private static Tp02Pg33DryRunInstruction Instruction(int i, int span)
    {
        Tp02MachineWord[] words = new Tp02MachineWord[span];
        for (int w = 0; w < span; w++)
        {
            int seed = (i * 4) + w;
            words[w] = new Tp02MachineWord(
                (byte)(0x10 + (seed & 0x3F)),
                (byte)(0x20 + (seed & 0x3F)),
                (byte)(0x30 + (seed & 0x3F)));
        }
        return new Tp02Pg33DryRunInstruction(words);
    }

    public static int Main()
    {
        // Quatro instruções lógicas com spans 1+2+3+4 = 10 palavras/passos.
        List<Tp02Pg33DryRunInstruction> four = new List<Tp02Pg33DryRunInstruction>();
        four.Add(Instruction(0, 1));
        four.Add(Instruction(1, 2));
        four.Add(Instruction(2, 3));
        four.Add(Instruction(3, 4));

        IList<Tp02Pg33DryRunBlock> oneBlock = Tp02Pg33DryRunProgram.BuildBlocks(0x0100, four);
        Check(oneBlock.Count == 1, "4 instruções devem formar um bloco");
        Check(oneBlock[0].StartStep == 0x0100, "início do primeiro bloco");
        Check(oneBlock[0].NextStep == 0x010A, "cursor deve avançar 10 passos");
        Check(oneBlock[0].InstructionCount == 4, "contador lógico deve ser 4");
        Check(oneBlock[0].MachineWordCount == 10, "4 instruções expandem para 10 palavras");
        Check(oneBlock[0].Frame[0] == 0x33, "quadro usa comando 33");
        Check(oneBlock[0].Frame[1] == 0x22, "LEN=3*10+4=0x22");
        Check(oneBlock[0].Frame[3] == 0x01 && oneBlock[0].Frame[4] == 0x00,
            "endereço inicial 0x0100 no quadro");
        Check(oneBlock[0].Frame[5] == 0x14, "HIGH/LOW tem 2*10=0x14 bytes");
        Check(oneBlock[0].Frame.Length == 37, "quadro de 10 palavras tem 37 bytes");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(oneBlock[0].Frame),
            "geometria do primeiro bloco");

        // 21 instruções: o primeiro quadro para exatamente em 20 instruções,
        // embora essas 20 instruções ocupem 50 palavras/passos.
        List<Tp02Pg33DryRunInstruction> twentyOne = new List<Tp02Pg33DryRunInstruction>();
        for (int i = 0; i < 21; i++)
            twentyOne.Add(Instruction(i, (i % 4) + 1));

        IList<Tp02Pg33DryRunBlock> twoBlocks = Tp02Pg33DryRunProgram.BuildBlocks(0x0100, twentyOne);
        Check(twoBlocks.Count == 2, "21 instruções devem formar 20+1");
        Check(twoBlocks[0].InstructionCount == 20, "primeiro bloco com 20 instruções");
        Check(twoBlocks[0].MachineWordCount == 50, "primeiro bloco expande para 50 palavras");
        Check(twoBlocks[0].StartStep == 0x0100, "primeiro bloco começa em 0x0100");
        Check(twoBlocks[0].NextStep == 0x0132, "20 instruções mistas avançam 50 passos");
        Check(twoBlocks[0].Frame.Length == 157, "quadro de 50 palavras tem 157 bytes");
        Check(twoBlocks[0].Frame[1] == 0x9A, "LEN para 50 palavras=0x9A");
        Check(twoBlocks[0].Frame[5] == 0x64, "plano HIGH/LOW de 50 palavras=0x64 bytes");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(twoBlocks[0].Frame),
            "geometria do bloco de 20 instruções");

        Check(twoBlocks[1].InstructionCount == 1, "segundo bloco com 1 instrução");
        Check(twoBlocks[1].MachineWordCount == 1, "última instrução span=1");
        Check(twoBlocks[1].StartStep == 0x0132,
            "segundo bloco começa no cursor real, não em 0x0114");
        Check(twoBlocks[1].NextStep == 0x0133, "última instrução avança um passo");
        Check(twoBlocks[1].Frame[3] == 0x01 && twoBlocks[1].Frame[4] == 0x32,
            "cabeçalho do segundo bloco usa 0x0132");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(twoBlocks[1].Frame),
            "geometria do segundo bloco");

        // Ordem dos planos: todas as palavras HIGH/LOW contíguas primeiro;
        // depois um EXTERNAL para cada palavra expandida.
        byte[] f = twoBlocks[0].Frame;
        Check(f[6] == 0x10 && f[7] == 0x20,
            "primeira palavra HIGH/LOW preservada");
        Check(f[8] == 0x14 && f[9] == 0x24 && f[10] == 0x15 && f[11] == 0x25,
            "segunda instrução preserva suas duas palavras consecutivas");
        int externalOffset = 6 + 100;
        Check(f[externalOffset] == 0x30 && f[externalOffset + 1] == 0x34 && f[externalOffset + 2] == 0x35,
            "EXTERNAL segue a mesma ordem das palavras expandidas");

        // Pior caso estrutural: 20 instruções de 4 passos = 80 palavras.
        List<Tp02Pg33DryRunInstruction> worst = new List<Tp02Pg33DryRunInstruction>();
        for (int i = 0; i < 20; i++) worst.Add(Instruction(i, 4));
        IList<Tp02Pg33DryRunBlock> worstBlocks = Tp02Pg33DryRunProgram.BuildBlocks(0x0200, worst);
        Check(worstBlocks.Count == 1, "20 instruções de 4 passos ainda formam um bloco");
        Check(worstBlocks[0].InstructionCount == 20, "pior caso mantém 20 instruções");
        Check(worstBlocks[0].MachineWordCount == 80, "pior caso contém 80 palavras");
        Check(worstBlocks[0].NextStep == 0x0250, "80 palavras avançam 0x50 passos");
        Check(worstBlocks[0].Frame[1] == 0xF4, "LEN máximo reconstruído = F4h");
        Check(worstBlocks[0].Frame[5] == 0xA0, "HIGH/LOW máximo = A0h bytes");
        Check(worstBlocks[0].Frame.Length == 247, "quadro máximo reconstruído = 247 bytes");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(worstBlocks[0].Frame),
            "geometria do pior caso");

        // Exatamente até 4000 é permitido no final da última instrução.
        List<Tp02Pg33DryRunInstruction> exactEnd = new List<Tp02Pg33DryRunInstruction>();
        exactEnd.Add(Instruction(0, 4));
        IList<Tp02Pg33DryRunBlock> exact = Tp02Pg33DryRunProgram.BuildBlocks(3996, exactEnd);
        Check(exact[0].NextStep == 4000, "última instrução pode terminar exatamente em 4000");

        ExpectArgument(
            delegate { new Tp02Pg33DryRunInstruction(new Tp02MachineWord[0]); },
            "instrução sem palavras deve falhar");
        ExpectArgument(
            delegate
            {
                new Tp02Pg33DryRunInstruction(
                    new Tp02MachineWord(0, 0, 0),
                    new Tp02MachineWord(0, 0, 0),
                    new Tp02MachineWord(0, 0, 0),
                    new Tp02MachineWord(0, 0, 0),
                    new Tp02MachineWord(0, 0, 0));
            },
            "instrução com mais de 4 palavras deve falhar");
        ExpectArgument(
            delegate { Tp02Pg33DryRunProgram.BuildBlocks(0, new List<Tp02Pg33DryRunInstruction>()); },
            "programa vazio deve falhar");

        List<Tp02Pg33DryRunInstruction> overflow = new List<Tp02Pg33DryRunInstruction>();
        overflow.Add(Instruction(0, 4));
        ExpectArgument(
            delegate { Tp02Pg33DryRunProgram.BuildBlocks(3997, overflow); },
            "instrução que ultrapassa 4000 deve falhar");

        if (failures != 0)
        {
            Console.Error.WriteLine("Tp02Pg33DryRunProgramSelfTest: " + failures + " falha(s).");
            return 1;
        }

        Console.WriteLine("Tp02Pg33DryRunProgramSelfTest: OK (dry-run; nenhum TX)");
        return 0;
    }
}
