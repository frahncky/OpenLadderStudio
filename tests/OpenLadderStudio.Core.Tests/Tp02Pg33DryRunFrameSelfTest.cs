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
            "fixture candidato de 1 registro");
        Check(Tp02Pg33DryRunFrame.HasValidChecksum(one),
            "checksum do quadro de 1 registro");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(one),
            "geometria do quadro de 1 registro");

        byte[] two = Tp02Pg33DryRunFrame.BuildCandidate(
            0x0050,
            new byte[] { 0x00, 0x10, 0x20, 0x41 },
            new byte[] { 0x01, 0x07 });
        Check(two[0] == 0x33, "comando 33");
        Check(two[1] == 0x0A, "LEN=3*2+4");
        Check(two[2] == 0x00, "flag 00");
        Check(two[3] == 0x00 && two[4] == 0x50, "start step 0x0050");
        Check(two[5] == 0x04, "plano HIGH/LOW tem 2*N bytes");
        Check(two[6] == 0x00 && two[7] == 0x10 && two[8] == 0x20 && two[9] == 0x41,
            "plano HIGH/LOW preservado");
        Check(two[10] == 0x01 && two[11] == 0x07,
            "plano EXTERNAL preservado");
        Check(Tp02Pg33DryRunFrame.HasValidChecksum(two), "checksum de 2 registros");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(two), "geometria de 2 registros");

        byte[] hl20 = new byte[40];
        byte[] ext20 = new byte[20];
        byte[] max = Tp02Pg33DryRunFrame.BuildCandidate(3920, hl20, ext20);
        Check(max[1] == 0x40, "LEN de 20 registros = 40h");
        Check(max[5] == 0x28, "plano HIGH/LOW de 20 registros = 28h bytes");
        Check(max.Length == 67, "quadro de 20 registros = 67 bytes incluindo checksum");
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(max), "geometria máxima válida");

        // O número de registros não é o span de passos. O construtor bruto não
        // deve inferir start+N: instruções podem consumir 1..4 passos.
        byte[] nearEnd = Tp02Pg33DryRunFrame.BuildCandidate(
            3999,
            new byte[] { 0x00, 0x10, 0x20, 0x40 },
            new byte[] { 0x01, 0x06 });
        Check(Tp02Pg33DryRunFrame.HasCandidateGeometry(nearEnd),
            "frame bruto perto do limite não infere span de passo");

        byte[] broken = (byte[])two.Clone();
        broken[broken.Length - 1] ^= 0x01;
        Check(!Tp02Pg33DryRunFrame.HasValidChecksum(broken),
            "checksum adulterado deve falhar");
        Check(!Tp02Pg33DryRunFrame.HasCandidateGeometry(broken),
            "geometria rejeita checksum adulterado");

        ExpectThrow(
            delegate { Tp02Pg33DryRunFrame.BuildCandidate(0, new byte[] { 0x00 }, new byte[] { 0x01 }); },
            "plano HIGH/LOW ímpar deve falhar");
        ExpectThrow(
            delegate { Tp02Pg33DryRunFrame.BuildCandidate(0, new byte[] { 0x00, 0x10 }, new byte[0]); },
            "plano EXTERNAL incompatível deve falhar");
        ExpectThrow(
            delegate { Tp02Pg33DryRunFrame.BuildCandidate(0, new byte[42], new byte[21]); },
            "mais de 20 registros deve falhar");
        ExpectThrow(
            delegate { Tp02Pg33DryRunFrame.BuildCandidate(4000, new byte[] { 0x00, 0x10 }, new byte[] { 0x01 }); },
            "startStep=4000 deve falhar");

        if (failures != 0)
        {
            Console.Error.WriteLine("Tp02Pg33DryRunFrameSelfTest: " + failures + " falha(s).");
            return 1;
        }

        Console.WriteLine("Tp02Pg33DryRunFrameSelfTest: OK (dry-run; nenhum TX)");
        return 0;
    }
}
