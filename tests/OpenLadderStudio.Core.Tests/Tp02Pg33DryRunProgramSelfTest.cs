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

    private static Tp02Pg33DryRunRecord Record(int i, int span)
    {
        return new Tp02Pg33DryRunRecord(
            new Tp02MachineWord(
                (byte)(0x10 + (i & 0x3F)),
                (byte)(0x20 + (i & 0x3F)),
                (byte)(0x30 + (i & 0x3F))),
            span);
    }

    public static int Main()
    {
        // Um bloco: spans 1+2+3+4 = 10 passos, mas somente 4 registros.
        List<Tp02Pg33DryRunRecord> four = new List<Tp02Pg33DryRunRecord>();
        four.Add(Record(0, 1));
        four.Add(Record(1, 2));
        four.Add(Record(2, 3));
        four.Add(Record(3, 4));

        IList<Tp02Pg33DryRunBlock> oneBlock = Tp02Pg33DryRunProgram.BuildBlocks(0x0100, four);
        Check(oneBlock.Count == 1, "4 registros devem formar um bloco");
        Check(oneBlock[0].StartStep == 0x0100, "início do primeiro bloco");
        Check(oneBlock[0].NextStep == 0x010A, "cursor deve avançar 10 passos");
        Check(oneBlock[0].RecordCount == 4, "contador de registros deve ser 4");
        Check(oneBlock[0].Frame[0] == 0x33, "quadro usa comando 33");
        Check(oneBlock[0].Frame[1] == 0x10, "LEN=3*4+4=0x10");
        Check(oneBlock[0].Frame[3] == 0x01 && oneBlock[0].Frame[4] == 0x00,
            "endereço inicial 0x0100 no quadro");
        Check(oneBlock[0].Frame[5] == 0x08, "HIGH/LOW tem 2*4 bytes");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(oneBlock[0].Frame),
            "geometria do primeiro bloco");

        // 21 registros: o primeiro quadro deve parar exatamente em 20, mesmo
        // com spans mistos. 1+2+3+4 repetidos 5 vezes = 50 = 0x32 passos.
        List<Tp02Pg33DryRunRecord> twentyOne = new List<Tp02Pg33DryRunRecord>();
        for (int i = 0; i < 21; i++)
            twentyOne.Add(Record(i, (i % 4) + 1));

        IList<Tp02Pg33DryRunBlock> twoBlocks = Tp02Pg33DryRunProgram.BuildBlocks(0x0100, twentyOne);
        Check(twoBlocks.Count == 2, "21 registros devem formar 20+1");
        Check(twoBlocks[0].RecordCount == 20, "primeiro bloco com 20 registros");
        Check(twoBlocks[0].StartStep == 0x0100, "primeiro bloco começa em 0x0100");
        Check(twoBlocks[0].NextStep == 0x0132, "20 registros mistos avançam 50 passos");
        Check(twoBlocks[0].Frame.Length == 67, "quadro máximo PG33 tem 67 bytes");
        Check(twoBlocks[0].Frame[1] == 0x40, "LEN máximo PG33=0x40");
        Check(twoBlocks[0].Frame[5] == 0x28, "plano HIGH/LOW máximo=0x28 bytes");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(twoBlocks[0].Frame),
            "geometria do bloco de 20");

        Check(twoBlocks[1].RecordCount == 1, "segundo bloco com 1 registro");
        Check(twoBlocks[1].StartStep == 0x0132,
            "segundo bloco começa no cursor real, não em 0x0114");
        Check(twoBlocks[1].NextStep == 0x0133, "último registro span=1");
        Check(twoBlocks[1].Frame[3] == 0x01 && twoBlocks[1].Frame[4] == 0x32,
            "cabeçalho do segundo bloco usa 0x0132");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(twoBlocks[1].Frame),
            "geometria do segundo bloco");

        // Ordem dos planos: HIGH/LOW contíguos primeiro; EXTERNAL depois.
        byte[] f = twoBlocks[0].Frame;
        Check(f[6] == 0x10 && f[7] == 0x20 && f[8] == 0x11 && f[9] == 0x21,
            "HIGH/LOW preservados no início do plano A");
        int externalOffset = 6 + 40;
        Check(f[externalOffset] == 0x30 && f[externalOffset + 1] == 0x31,
            "EXTERNAL começa depois dos 40 bytes HIGH/LOW");

        // Exatamente até 4000 é permitido no final do último registro.
        List<Tp02Pg33DryRunRecord> exactEnd = new List<Tp02Pg33DryRunRecord>();
        exactEnd.Add(Record(0, 4));
        IList<Tp02Pg33DryRunBlock> exact = Tp02Pg33DryRunProgram.BuildBlocks(3996, exactEnd);
        Check(exact[0].NextStep == 4000, "último registro pode terminar exatamente em 4000");

        ExpectArgument(
            delegate { new Tp02Pg33DryRunRecord(new Tp02MachineWord(0, 0, 0), 0); },
            "span zero deve falhar");
        ExpectArgument(
            delegate { Tp02Pg33DryRunProgram.BuildBlocks(0, new List<Tp02Pg33DryRunRecord>()); },
            "programa vazio deve falhar");

        List<Tp02Pg33DryRunRecord> overflow = new List<Tp02Pg33DryRunRecord>();
        overflow.Add(Record(0, 4));
        ExpectArgument(
            delegate { Tp02Pg33DryRunProgram.BuildBlocks(3997, overflow); },
            "registro que ultrapassa 4000 deve falhar");

        if (failures != 0)
        {
            Console.Error.WriteLine("Tp02Pg33DryRunProgramSelfTest: " + failures + " falha(s).");
            return 1;
        }

        Console.WriteLine("Tp02Pg33DryRunProgramSelfTest: OK (dry-run; nenhum TX)");
        return 0;
    }
}
