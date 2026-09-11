using System;
using OpenLadderStudio.Core;

internal static class Tp02Pg33DryRunFrameSelfTest
{
    private static int failures;

    private static void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FALHA: " + message);
    }

    private static string Hex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace('-', ' ');
    }

    private static void ExpectThrow(Action action, string message)
    {
        bool threw = false;
        try { action(); }
        catch (ArgumentException) { threw = true; }
        Check(threw, message);
    }

    public static int Main()
    {
        byte[] one = Tp02Pg33DryRunFrame.BuildCandidate(
            0,
            new byte[] { 0x00, 0x10 },
            new byte[] { 0x01 });

        Check(Hex(one) == "33 07 00 00 00 02 00 10 01 B2",
            "fixture candidato de 1 passo");
        Check(Tp02Pg33DryRunFrame.HasValidChecksum(one),
            "checksum do quadro de 1 passo");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(one),
            "geometria do quadro de 1 passo");

        byte[] two = Tp02Pg33DryRunFrame.BuildCandidate(
            0x0050,
            new byte[] { 0x00, 0x10, 0x20, 0x41 },
            new byte[] { 0x01, 0x07 });
        Check(two[0] == 0x33, "comando 33");
        Check(two[1] == 0x0A, "LEN=3*2+4");
        Check(two[2] == 0x00, "flag 00");
        Check(two[3] == 0x00 && two[4] == 0x50, "start step 0x0050");
        Check(two[5] == 0x04, "plano A tem 2*N bytes");
        Check(two[6] == 0x00 && two[7] == 0x10 && two[8] == 0x20 && two[9] == 0x41,
            "plano HIGH/LOW preservado");
        Check(two[10] == 0x01 && two[11] == 0x07,
            "plano externo preservado");
        Check(Tp02Pg33DryRunFrame.HasValidChecksum(two), "checksum de 2 passos");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(two), "geometria de 2 passos");

        byte[] hl80 = new byte[160];
        byte[] ext80 = new byte[80];
        byte[] max = Tp02Pg33DryRunFrame.BuildCandidate(3920, hl80, ext80);
        Check(max[1] == 0xF4, "LEN candidato de 80 passos = F4");
        Check(max[5] == 0xA0, "plano A de 80 passos = A0 bytes");
        Check(max.Length == 247, "quadro de 80 passos = 247 bytes incluindo checksum");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(max), "geometria máxima válida");

        byte[] broken = (byte[])two.Clone();
        broken[broken.Length - 1] ^= 0x01;
        Check(!Tp02Pg33DryRunFrame.HasValidChecksum(broken),
            "checksum adulterado deve falhar");
        Check(!Tp02Pg33DryRunFrame.HasCandidateGeometry(broken),
            "geometria rejeita checksum adulterado");

        ExpectThrow(
            delegate { Tp02Pg33DryRunFrame.BuildCandidate(0, new byte[] { 0x00 }, new byte[] { 0x01 }); },
            "plano A ímpar deve falhar");
        ExpectThrow(
            delegate { Tp02Pg33DryRunFrame.BuildCandidate(0, new byte[] { 0x00, 0x10 }, new byte[0]); },
            "plano externo incompatível deve falhar");
        ExpectThrow(
            delegate { Tp02Pg33DryRunFrame.BuildCandidate(3999, new byte[] { 0x00, 0x10, 0x20, 0x40 }, new byte[] { 0x01, 0x06 }); },
            "quadro que passa de 4000 deve falhar");

        if (failures != 0)
        {
            Console.Error.WriteLine("Tp02Pg33DryRunFrameSelfTest: " + failures + " falha(s).");
            return 1;
        }

        Console.WriteLine("Tp02Pg33DryRunFrameSelfTest: OK (dry-run; nenhum TX)");
        return 0;
    }
}
