using System;
using System.Collections.Generic;
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
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(320)) == "34 03 01 40 A0 E7",
            "request cruza 0x00FF em ordem HIGH,LOW");
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(1440)) == "34 03 05 A0 A0 83",
            "ultima pagina da capacidade 1.5K");
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(3760)) == "34 03 0E B0 A0 6A",
            "regressao v1.53: start=3760 deve usar START_H=0E START_L=B0");
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(3840)) == "34 03 0F 00 A0 19",
            "regressao v1.53: start=3840 deve usar START_H=0F START_L=00");
        Check(Hex(Tp02Pg34Pager.BuildReadRequest(3920)) == "34 03 0F 50 A0 C9",
            "ultima pagina da capacidade 4K");

        byte[] boundary = Tp02Pg34Pager.BuildReadRequest(320);
        Check(Tp02Pg34Pager.DecodeStartStep(boundary) == 320,
            "decode START preserva 16 bits");
        Check(Tp02Pg34Pager.DecodeStartStep(Tp02Pg34Pager.BuildReadRequest(3760)) == 3760,
            "decode preserva start=3760");
        Check(Tp02Pg34Pager.DecodeStartStep(Tp02Pg34Pager.BuildReadRequest(3840)) == 3840,
            "decode preserva start=3840");

        IList<byte[]> plan4k = Tp02Pg34Pager.BuildReadPlan(4000);
        Check(plan4k.Count == 50, "plano 4K contém 50 páginas");
        Check(Tp02Pg34Pager.DecodeStartStep(plan4k[0]) == 0, "plano 4K inicia em zero");
        Check(Tp02Pg34Pager.DecodeStartStep(plan4k[3]) == 240, "plano antes da fronteira");
        Check(Tp02Pg34Pager.DecodeStartStep(plan4k[4]) == 320, "plano após fronteira 0x00FF");
        Check(Tp02Pg34Pager.DecodeStartStep(plan4k[47]) == 3760, "plano 4K preserva pagina 3760");
        Check(Tp02Pg34Pager.DecodeStartStep(plan4k[48]) == 3840, "plano 4K preserva pagina 3840");
        Check(Tp02Pg34Pager.DecodeStartStep(plan4k[49]) == 3920, "plano 4K termina em 3920");

        IList<byte[]> plan15k = Tp02Pg34Pager.BuildReadPlan(1500);
        Check(plan15k.Count == 19, "plano 1.5K contém 19 páginas");
        Check(Tp02Pg34Pager.DecodeStartStep(plan15k[18]) == 1440, "plano 1.5K termina em 1440");

        Check(Tp02Pg34Pager.NextStartStep(0) == 80, "next 0 -> 80");
        Check(Tp02Pg34Pager.NextStartStep(80) == 160, "next 80 -> 160");
        Check(Tp02Pg34Pager.NextStartStep(240) == 320, "next cruza 0x00FF");
        Check(Tp02Pg34Pager.NextStartStep(3920) == 4000, "ultima pagina -> limite 4000");

        byte[] page = BuildPageWithEnd(10);
        int local;
        Check(Tp02Pg34Pager.IsValidPageFrame(page), "fixture LEN/checksum valido");
        Check(Tp02Pg34Pager.TryFindEnd(page, out local), "END deve ser localizado");
        Check(local == 10, "END local=10");
        Check(Tp02Pg34Pager.GlobalStep(80, local) == 90, "END global=90");
        Check(Tp02Pg34Pager.GlobalStep(320, local) == 330, "END global após fronteira");

        byte[] broken = (byte[])page.Clone();
        broken[broken.Length - 1] ^= 0x01;
        Check(!Tp02Pg34Pager.IsValidPageFrame(broken), "checksum adulterado deve falhar");
        Check(!Tp02Pg34Pager.TryFindEnd(broken, out local), "nao procurar END em quadro invalido");

        bool threw = false;
        try { Tp02Pg34Pager.BuildReadRequest(4000); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "startStep=4000 deve ser rejeitado");

        threw = false;
        try { Tp02Pg34Pager.DecodeStartStep(new byte[] { 0x34, 0x03, 0x40, 0x01, 0xA0, 0xE7 }); }
        catch (ArgumentException) { threw = true; }
        Check(threw, "quadro LOW,HIGH fora da faixa deve ser rejeitado");

        threw = false;
        try { Tp02Pg34Pager.BuildReadPlan(4001); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "plano acima de 4K rejeitado");

        if (failures != 0)
        {
            Console.Error.WriteLine("Tp02Pg34PagerSelfTest: " + failures + " falha(s).");
            return 1;
        }

        Console.WriteLine("Tp02Pg34PagerSelfTest: OK");
        return 0;
    }
}
