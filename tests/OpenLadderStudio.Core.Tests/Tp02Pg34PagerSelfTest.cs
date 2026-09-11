using System;
using OpenLadderStudio.Core;

internal static class Tp02Pg34PagerSelfTest
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

    private static byte[] BuildPageWithEnd(int localStep)
    {
        byte[] frame = new byte[Tp02Pg34Pager.PayloadLength + 3];
        frame[0] = 0x00;
        frame[1] = 0xF0;
        int p = 2 + (2 * localStep);
        frame[p] = 0x00;
        frame[p + 1] = 0x70;

        int sum = 0;
        for (int i = 0; i < frame.Length - 1; i++) sum = (sum + frame[i]) & 0xFF;
        frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);
        return frame;
    }

    public static int Main()
    {
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(0)) == "34 03 00 00 A0 28",
            "request pagina 0");
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(80)) == "34 03 00 50 A0 D8",
            "request pagina 1");
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(160)) == "34 03 00 A0 A0 88",
            "request pagina 2");
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(240)) == "34 03 00 F0 A0 38",
            "request pagina 3");
        Check(Tp02Pg34Pager.NextStartStep(0) == 80, "next 0 -> 80");
        Check(Tp02Pg34Pager.NextStartStep(80) == 160, "next 80 -> 160");
        Check(Tp02Pg34Pager.NextStartStep(3920) == 4000, "ultima pagina -> limite 4000");

        byte[] page = BuildPageWithEnd(10);
        int local;
        Check(Tp02Pg34Pager.IsValidPageFrame(page), "fixture LEN/checksum valido");
        Check(Tp02Pg34Pager.TryFindEnd(page, out local), "END deve ser localizado");
        Check(local == 10, "END local=10");
        Check(Tp02Pg34Pager.GlobalStep(80, local) == 90, "END global=90");

        byte[] broken = (byte[])page.Clone();
        broken[broken.Length - 1] ^= 0x01;
        Check(!Tp02Pg34Pager.IsValidPageFrame(broken), "checksum adulterado deve falhar");
        Check(!Tp02Pg34Pager.TryFindEnd(broken, out local), "nao procurar END em quadro invalido");

        bool threw = false;
        try { Tp02Pg34Pager.BuildReadRequest(4000); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "startStep=4000 deve ser rejeitado");

        if (failures != 0)
        {
            Console.Error.WriteLine("Tp02Pg34PagerSelfTest: " + failures + " falha(s).");
            return 1;
        }

        Console.WriteLine("Tp02Pg34PagerSelfTest: OK");
        return 0;
    }
}
