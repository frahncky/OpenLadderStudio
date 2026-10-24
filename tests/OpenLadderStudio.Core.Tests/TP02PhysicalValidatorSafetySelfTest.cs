using System;
using System.Collections.Generic;
using ModernPC12;
using OpenLadderStudio.Core;

internal static class TP02PhysicalValidatorSafetySelfTest
{
    private static int failures;

    private static void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FALHA: " + message);
    }

    private static byte[] RawRead(int address, int count)
    {
        byte[] frame = new byte[] { 0x0A, 0x03, (byte)(address >> 8), (byte)address, (byte)count, 0x00 };
        frame[5] = Tp02PgProtocol.Checksum(frame, 5);
        return frame;
    }

    // Página 34 sintética: 80 pares HIGH/LOW; END (00 70) no par endAt, ou nenhum se endAt < 0.
    private static byte[] Page(int endAt)
    {
        byte[] f = new byte[Tp02Pg34Pager.PayloadLength + 3];
        f[0] = 0x00; f[1] = (byte)Tp02Pg34Pager.PayloadLength;
        for (int i = 0; i < 80; i++) { f[2 + 2 * i] = 0x00; f[3 + 2 * i] = 0x10; }
        if (endAt >= 0) { f[2 + 2 * endAt] = 0x00; f[3 + 2 * endAt] = 0x70; }
        f[f.Length - 1] = Tp02PgProtocol.Checksum(f, f.Length - 1);
        return f;
    }

    private static List<string> RunCampaign(byte[] page0, byte[] page50, bool b1, bool b2, Dictionary<int, byte[]> replies)
    {
        List<string> sent = new List<string>();
        TP02BancadaPg34Campaign.Run(page0, page50, b1, b2,
            delegate(byte[] request, string label)
            {
                Check(TP02PhysicalValidationSafety.IsAllowed(request), "campanha B1/B2 gerou quadro fora da allowlist: " + label);
                Check(!sent.Contains(label), "campanha B1/B2 repetiu " + label);
                sent.Add(label);
                int start = Tp02Pg34Pager.DecodeStartStep(request);
                byte[] r;
                return replies.TryGetValue(start, out r) ? r : new byte[0];
            },
            delegate(string name, string status, string detail) { });
        return sent;
    }

    private static void CampaignB1B2()
    {
        Dictionary<int, byte[]> full = new Dictionary<int, byte[]>();
        full[0xA0] = Page(-1); full[0xF0] = Page(-1); full[0x140] = Page(2); full[0x4F] = Page(-1);
        string all = string.Join(",", RunCampaign(Page(-1), Page(-1), true, true, full).ToArray());
        Check(all == "PG34-00A0,PG34-00F0,PG34-0140,PG34-004F", "B1+B2 completas: " + all);

        Check(RunCampaign(Page(-1), Page(-1), false, false, full).Count == 0, "B1/B2 desmarcadas não transmitem nada");
        string endP50 = string.Join(",", RunCampaign(Page(-1), Page(10), true, false, full).ToArray());
        Check(endP50 == string.Empty, "END em @0050 não envia @00A0: " + endP50);
        string inv = string.Join(",", RunCampaign(Page(-1), null, true, true, full).ToArray());
        Check(inv == string.Empty, "@0050 inválida bloqueia B1 e B2: " + inv);

        Dictionary<int, byte[]> silent = new Dictionary<int, byte[]>();
        silent[0xF0] = Page(-1);
        string stop = string.Join(",", RunCampaign(Page(-1), Page(-1), true, false, silent).ToArray());
        Check(stop == "PG34-00A0", "silêncio em @00A0 encerra B1 sem retry: " + stop);

        Dictionary<int, byte[]> bad = new Dictionary<int, byte[]>();
        byte[] corrupt = Page(-1); corrupt[10] ^= 0x01;
        bad[0xA0] = corrupt; bad[0xF0] = Page(-1);
        string badRun = string.Join(",", RunCampaign(Page(-1), Page(-1), true, false, bad).ToArray());
        Check(badRun == "PG34-00A0", "soma inválida em @00A0 encerra B1: " + badRun);

        string b2only = string.Join(",", RunCampaign(Page(-1), Page(5), false, true, full).ToArray());
        Check(b2only == "PG34-004F", "B2 sozinha envia só @004F uma vez: " + b2only);
    }

    public static int Main()
    {
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Hello), "HELLO deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.F0), "F0 deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Frame38), "38 deve ser permitido");

        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(0)), "PG34 pagina zero deve ser permitida");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(80)), "PG34 pagina 80 deve ser permitida");
        // P0.2 do roteiro de bancada: quadros exatos das fases B2/B3.
        byte[][] novos = new byte[][]
        {
            new byte[] { 0x34, 0x03, 0x00, 0x4F, 0xA0, 0xD9 },
            new byte[] { 0x34, 0x03, 0x00, 0xA0, 0xA0, 0x88 },
            new byte[] { 0x34, 0x03, 0x00, 0xF0, 0xA0, 0x38 },
            new byte[] { 0x34, 0x03, 0x01, 0x40, 0xA0, 0xE7 },
        };
        int[] novosStart = new int[] { 0x4F, 0xA0, 0xF0, 0x140 };
        for (int i = 0; i < novos.Length; i++)
        {
            string s = novosStart[i].ToString("X4");
            Check(TP02PhysicalValidationSafety.IsAllowed(novos[i]), "PG34 START " + s + " deve ser permitido");
            Check(TP02PhysicalValidationSafety.Same(novos[i], Tp02Pg34Pager.BuildReadRequest(novosStart[i])), "quadro PG34 START " + s + " deve coincidir com o pager");
            Check(!TP02PhysicalValidationSafety.IsAllowed(novos[i], true), "PG34 START " + s + " deve ser bloqueado em RUN");
        }
        Check(!TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(0x51)), "PG34 START 0051 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(0x4E)), "PG34 START 004E deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(0x123)), "PG34 START arbitrario deve ser bloqueado");
        int aceitos = 0;
        for (int start = 0; start < Tp02Pg34Pager.MaxProgramSteps; start++)
            if (TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(start))) aceitos++;
        Check(aceitos == 6, "exatamente 6 START de PG34 devem ser aceitos; aceitos=" + aceitos);
        byte[] pg34Len = new byte[] { 0x34, 0x03, 0x00, 0x4F, 0x50, 0x00 };
        pg34Len[5] = Tp02PgProtocol.Checksum(pg34Len, 5);
        Check(!TP02PhysicalValidationSafety.IsAllowed(pg34Len), "PG34 START 004F com LEN diferente de A0 deve ser bloqueado");

        // Após HELLO RUN: somente F0.
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.F0, true), "F0 apos HELLO RUN deve ser permitido");
        Check(!TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Hello, true), "HELLO apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Frame38, true), "38 apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(0), true), "34 apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.ReadClock(), true), "0A apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x03, 0x00, 0xFC }, true), "03 apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x33, 0x00, 0xCC }, true), "33 apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x14, 0x00, 0xEB }, true), "14 apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x0F, 0x00, 0xF0 }, true), "0F 00 F0 apos RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x33, 0x00, 0xCC }), "33 deve ser bloqueado");

        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.ReadClock()), "PG0A RTC deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.ReadScanTimes()), "PG0A scan deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.V, 1, 2)), "amostra V deve ser permitida");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.D, 1, 2)), "amostra D deve ser permitida");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.WC, 1, 2)), "amostra WC deve ser permitida");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.FL, 1, 20)), "amostra FL deve ser permitida");

        foreach (Tp02PgMemoryProtocol.Area area in Enum.GetValues(typeof(Tp02PgMemoryProtocol.Area)))
        {
            IList<byte[]> pages = Tp02PgMemoryProtocol.ReadAll(area);
            Check(pages.Count > 0, "ReadAll deve montar paginas para " + area);
            for (int i = 0; i < pages.Count; i++)
                Check(TP02PhysicalValidationSafety.IsAllowed(pages[i]), "pagina conhecida deve ser permitida: " + area + " #" + (i + 1));
        }

        // Quadro de leitura formalmente válido, mas não gerado por nenhum codec/varredura conhecida.
        Check(!TP02PhysicalValidationSafety.IsAllowed(RawRead(0x1234, 1)), "PG0A arbitrario deve ser bloqueado");

        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x02, 0x00, 0xFD }), "RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x01, 0x00, 0xFE }), "STOP deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x03, 0x00, 0xFC }), "Clear/prepare 03 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x04, 0x00, 0xFB }), "Clear 04 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x11, 0x00, 0xEE }), "Clear 11 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x0F, 0x00, 0xF0 }), "Clear 0F deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x14, 0x00, 0xEB }), "gate 14 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x12, 0x00, 0xED }), "EEPROM 12 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x13, 0x00, 0xEC }), "EEPROM 13 deve ser bloqueado");

        byte[] write09 = Tp02PgMemoryProtocol.BuildRegisterWrites(Tp02PgMemoryProtocol.Area.V,
            new int[] { 1 }, new ushort[] { 1 })[0];
        Check(!TP02PhysicalValidationSafety.IsAllowed(write09), "PG09 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.BuildSetReset(Tp02PgMemoryProtocol.Area.Y, 1, true)), "PG35 deve ser bloqueado");

        // Classificação do HELLO (ValidateHello depende só desta função para decidir STOP/RUN).
        TP02PhysicalValidationSafety.HelloState None = TP02PhysicalValidationSafety.HelloState.None;
        TP02PhysicalValidationSafety.HelloState Stop = TP02PhysicalValidationSafety.HelloState.Stop;
        TP02PhysicalValidationSafety.HelloState Run = TP02PhysicalValidationSafety.HelloState.Run;
        TP02PhysicalValidationSafety.HelloState Amb = TP02PhysicalValidationSafety.HelloState.Ambiguous;
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x80, 0x01, 0x09, 0x75 }) == Stop, "HELLO STOP exato");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0xC0, 0x01, 0x09, 0x35 }) == Run, "HELLO RUN exato");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x80, 0x01, 0x09, 0x75, 0xC0, 0x01, 0x09, 0x35 }) == Amb, "STOP depois RUN deve ser ambiguo");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0xC0, 0x01, 0x09, 0x35, 0x80, 0x01, 0x09, 0x75 }) == Amb, "RUN antes de STOP deve ser ambiguo");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x55, 0xC0, 0x01, 0x09, 0x35, 0xAA, 0x80, 0x01, 0x09, 0x75, 0x00 }) == Amb, "RUN e STOP com ruido devem ser ambiguos");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x00, 0xFF, 0xC0, 0x01, 0x09, 0x35, 0x13 }) == Run, "RUN com ruido");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x00, 0x80, 0x01, 0x09, 0x75, 0xFF }) == Stop, "STOP com ruido");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x80, 0x01, 0x09 }) == None, "STOP parcial nao classifica");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0xC0, 0x01 }) == None, "RUN parcial nao classifica");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x80, 0x01, 0x09, 0x35 }) == None, "cabeca STOP com cauda RUN nao classifica");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0xC0, 0x01, 0x09, 0x75 }) == None, "cabeca RUN com cauda STOP nao classifica");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[0]) == None, "HELLO vazio");
        Check(TP02PhysicalValidationSafety.ClassifyHello(null) == None, "HELLO nulo");
        Check(TP02PhysicalValidationSafety.ClassifyHello(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC }) == None, "HELLO ruido");
        Check(TP02PhysicalValidationSafety.ClassifyHello(TP02PhysicalValidationSafety.Hello) == None, "eco do HELLO nao classifica");
        byte[] ecoRun = new byte[TP02PhysicalValidationSafety.Hello.Length + 4];
        Array.Copy(TP02PhysicalValidationSafety.Hello, ecoRun, TP02PhysicalValidationSafety.Hello.Length);
        Array.Copy(TP02PhysicalValidationSafety.HelloRun, 0, ecoRun, TP02PhysicalValidationSafety.Hello.Length, 4);
        Check(TP02PhysicalValidationSafety.ClassifyHello(ecoRun) == Run, "eco do HELLO seguido de RUN deve ser RUN");

        CampaignB1B2();

        if (failures != 0)
        {
            Console.Error.WriteLine("TP02PhysicalValidatorSafetySelfTest: " + failures + " falha(s).");
            return 1;
        }
        Console.WriteLine("TP02PhysicalValidatorSafetySelfTest: OK");
        return 0;
    }
}
