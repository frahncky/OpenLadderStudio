using System;
using System.Collections.Generic;
using OpenLadderStudio.Core;
using Area = OpenLadderStudio.Core.Tp02PgMemoryProtocol.Area;

internal static class Tp02PgMemoryProtocolSelfTest
{
    private static int checks;
    private static int failures;

    private static void Check(bool value, string name)
    {
        checks++;
        if (value) return;
        failures++;
        Console.Error.WriteLine("FALHA: " + name);
    }

    private static byte[] Bytes(string hex)
    {
        string[] parts = hex.Split(' ');
        byte[] result = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++) result[i] = Convert.ToByte(parts[i], 16);
        return result;
    }

    private static void Frame(byte[] actual, string expected, string name)
    {
        Check(BitConverter.ToString(actual).Replace('-', ' ') == expected, name);
        Check(Tp02PgProtocol.HasValidChecksum(actual) && actual.Length == actual[1] + 3, name + " tamanho/checksum");
    }

    private static void Reject(Action action, string name)
    {
        try { action(); Check(false, name); }
        catch (ArgumentException) { Check(true, name); }
    }

    private static void Pages(Area area, int count, int step, string first, string last)
    {
        IList<byte[]> pages = Tp02PgMemoryProtocol.ReadAll(area);
        Check(pages.Count == count, area + " quantidade de páginas");
        Frame(pages[0], first, area + " primeira página nativa");
        Frame(pages[count - 1], last, area + " última página nativa");
        for (int i = 1; i < pages.Count; i++)
        {
            int previous = pages[i - 1][2] * 256 + pages[i - 1][3];
            int current = pages[i][2] * 256 + pages[i][3];
            Check(current - previous == step, area + " continuidade de endereço " + i);
            Check(Tp02PgProtocol.HasValidChecksum(pages[i]), area + " checksum " + i);
        }
    }

    private static void Reads()
    {
        // Vetores capturados dos construtores x86: docs/data/pc12-memory-variants-emulation.txt.
        Pages(Area.V, 16, 64, "0A 03 50 00 80 22", "0A 03 53 C0 80 5F");
        Pages(Area.D, 32, 64, "0A 03 90 00 80 E2", "0A 03 97 C0 80 1B");
        Pages(Area.WC, 16, 57, "0A 03 70 00 72 10", "0A 03 73 57 72 B6");
        Pages(Area.FL, 13, 10, "0A 03 80 00 C8 AA", "0A 03 80 78 C8 32");
        IList<byte[]> ws = Tp02PgMemoryProtocol.ReadAll(Area.WS);
        Check(ws.Count == 2, "WS apenas duas consultas nativas");
        Frame(ws[0], "0A 03 60 00 AC E6", "WS primeira");
        Frame(ws[1], "0A 03 60 AC AC 3A", "WS segunda");
        Check(Tp02PgMemoryProtocol.CreateSweep(ws[1], 40).Count == 1, "WS segunda não gera terceira");
        Check(Tp02PgMemoryProtocol.CreateSweep(ws[0], 1).Count == 1, "WS respeita limite solicitado");
        Frame(Tp02PgMemoryProtocol.BuildRead(Area.WS, 39, 2), "0A 03 60 26 02 6A", "WS039 individual");
        Frame(Tp02PgMemoryProtocol.BuildRead(Area.WS, 40, 2), "0A 03 60 27 02 69", "WS040 individual");
        Reject(delegate { Tp02PgMemoryProtocol.CreateSweep(Tp02PgMemoryProtocol.ReadScanTimes(), 40); }, "não inventar passo WS para scan time");

        IList<byte[]> tail = Tp02PgMemoryProtocol.CreateSweep(Bytes("0A 03 53 FF 80 20"), 40);
        Check(tail.Count == 1, "fim V encerra a varredura sem retorno a zero");
        Frame(tail[0], "0A 03 53 FF 02 9E", "última palavra V com Q reduzido");
        Check(Tp02PgMemoryProtocol.CreateSweep(Bytes("0A 03 50 00 80 22"), 3).Count == 3, "limite de leituras");
        Reject(delegate { Tp02PgMemoryProtocol.CreateSweep(Bytes("0A 03 FF FF 02 F2"), 40); }, "endereço desconhecido não sofre wrap");
        Reject(delegate { Tp02PgMemoryProtocol.CreateSweep(Bytes("0A 03 50 00 01 A1"), 40); }, "página não divide registrador");
        Reject(delegate { Tp02PgMemoryProtocol.CreateSweep(Bytes("0A 03 50 00 00 A2"), 40); }, "página vazia rejeitada");
        Reject(delegate { Tp02PgMemoryProtocol.CreateSweep(Bytes("0A 03 50 00 80 23"), 40); }, "checksum de pedido inválido");
        Reject(delegate { Tp02PgMemoryProtocol.ReadAll((Area)99); }, "área inválida");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRead(Area.V, 0, 2); }, "número inicia em 1");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRead(Area.WC, 913, 2); }, "limite WC");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRead(Area.V, 1024, 4); }, "não ler além de V1024");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRead(Area.FL, 1, 21); }, "FL leitura em arquivos completos");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRead(Area.Y, 384, 2); }, "não ultrapassar o último byte Y");

        byte[] multiple = Tp02PgMemoryProtocol.BuildMultipleRead(new byte[][] {
            Tp02PgMemoryProtocol.BuildRead(Area.X, 8, 1), Tp02PgMemoryProtocol.BuildRead(Area.X, 9, 1),
            Tp02PgMemoryProtocol.BuildRead(Area.Y, 384, 1) });
        Frame(multiple, "0A 09 20 00 01 20 01 01 00 2F 01 79", "leitura múltipla nativa X8/X9/Y384");
        Frame(Tp02PgMemoryProtocol.BuildMultipleRead(new byte[][] {
            Tp02PgMemoryProtocol.BuildRead(Area.C, 1, 1), Tp02PgMemoryProtocol.BuildRead(Area.C, 2048, 1),
            Tp02PgMemoryProtocol.BuildRead(Area.SC, 1, 1), Tp02PgMemoryProtocol.BuildRead(Area.SC, 128, 1) }),
            "0A 0C 10 00 01 10 FF 01 A0 00 01 A0 0F 01 77", "leitura múltipla C/SC nos limites");
        Reject(delegate { Tp02PgMemoryProtocol.BuildMultipleRead(new byte[][] {
            Tp02PgMemoryProtocol.BuildRead(Area.V, 1, 128), Tp02PgMemoryProtocol.BuildRead(Area.D, 1, 128) }); }, "limite da resposta múltipla");
        List<byte[]> descriptors = new List<byte[]>();
        for (int i = 0; i < 86; i++) descriptors.Add(Tp02PgMemoryProtocol.BuildRead(Area.X, 1, 1));
        Reject(delegate { Tp02PgMemoryProtocol.BuildMultipleRead(descriptors); }, "limite dos descritores");
        Check(Tp02PgMemoryProtocol.ReadAll(Area.X).Count == 3, "X cobre 48 bytes");
        Check(Tp02PgMemoryProtocol.ReadAll(Area.C).Count == 16, "C cobre 256 bytes");
        Check(Tp02PgMemoryProtocol.ReadAll(Area.SC).Count == 1, "SC cobre 16 bytes");
    }

    private static void Writes()
    {
        Frame(Tp02PgMemoryProtocol.BuildRegisterWrites(Area.D, new int[] { 1, 257, 2048 },
            new ushort[] { 0x1234, 0x8000, 0xFFFF })[0],
            "09 0F 90 00 02 12 34 91 00 02 80 00 97 FF 02 FF FF 66", "D esparso nativo preserva cada endereço");
        int[] numbers = new int[41]; ushort[] values = new ushort[41];
        for (int i = 0; i < 41; i++) { numbers[i] = i + 1; values[i] = 0x1234; }
        numbers[40] = 1024;
        IList<byte[]> frames = Tp02PgMemoryProtocol.BuildRegisterWrites(Area.V, numbers, values);
        Check(frames.Count == 2 && frames[0].Length == 203, "41 registradores divididos em 40+1");
        Frame(frames[1], "09 05 53 FF 02 12 34 57", "retomada preserva V1024");
        Frame(Tp02PgMemoryProtocol.BuildRegisterWrites(Area.WS, new int[] { 43 }, new ushort[] { 0 })[0],
            "09 05 60 2A 02 00 00 65", "saída Remote I/O nativa WS43");
        int known = 0;
        for (int n = 1; n <= 128; n++)
        {
            try { Tp02PgMemoryProtocol.BuildRegisterWrites(Area.WS, new int[] { n }, new ushort[] { 0 }); known++; }
            catch (ArgumentException) { }
        }
        Check(known == 45, "seleção de 45 WS identificados");
        Check(Tp02PgMemoryProtocol.BuildRegisterWrites(Area.WS, new int[] { 4, 12 }, new ushort[] { 0, 1 }).Count == 2,
            "WS preserva quadros individuais");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRegisterWrites(Area.WS, new int[] { 39 }, new ushort[] { 1 }); }, "WS de credencial fora da escrita comum");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRegisterWrites(Area.X, new int[] { 1 }, new ushort[] { 1 }); }, "não escrever bit como palavra");
        Reject(delegate { Tp02PgMemoryProtocol.BuildRegisterWrites(Area.D, new int[] { 2049 }, new ushort[] { 1 }); }, "limite de escrita D");

        Frame(Tp02PgMemoryProtocol.BuildSetReset(Area.X, 1, true), "35 03 50 00 80 F7", "SET X1 nativo");
        Frame(Tp02PgMemoryProtocol.BuildSetReset(Area.X, 8, true), "35 03 50 00 87 F0", "SET X8 nativo");
        Frame(Tp02PgMemoryProtocol.BuildSetReset(Area.X, 9, true), "35 03 50 01 80 F6", "SET X9 nativo");
        Frame(Tp02PgMemoryProtocol.BuildSetReset(Area.Y, 1, true), "35 03 10 00 80 37", "SET Y1 nativo");
        Frame(Tp02PgMemoryProtocol.BuildSetReset(Area.C, 2048, false), "35 03 30 FF 07 91", "RESET C2048 nativo");
        Reject(delegate { Tp02PgMemoryProtocol.BuildSetReset(Area.SC, 1, true); }, "não extrapolar PG35 para SC");
        Reject(delegate { Tp02PgMemoryProtocol.BuildSetReset(Area.X, 385, true); }, "limite X");
        IList<byte[]> coils = Tp02PgMemoryProtocol.BuildSystemCoils(0xFF, 0x7F);
        Frame(coils[0], "09 04 A0 00 01 FF 52", "SC001-008");
        Frame(coils[1], "09 04 A0 02 01 7F D0", "SC017-023");
        Reject(delegate { Tp02PgMemoryProtocol.BuildSystemCoils(0, 0x80); }, "não incluir SC024");

        byte[] data = new byte[20];
        Frame(Tp02PgMemoryProtocol.BuildFileWrite(1, data),
            "09 17 80 00 14 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 4B", "FL exatamente vinte bytes");
        Reject(delegate { Tp02PgMemoryProtocol.BuildFileWrite(1, new byte[21]); }, "não reproduzir byte excedente HEX do PC12");
        Reject(delegate { Tp02PgMemoryProtocol.BuildFileWrite(131, data); }, "limite FL");
        List<int> files = new List<int>(); List<byte[]> contents = new List<byte[]>();
        for (int i = 1; i <= 11; i++) { files.Add(i); contents.Add(data); }
        frames = Tp02PgMemoryProtocol.BuildFileWrites(files, contents);
        Check(frames.Count == 2 && frames[0].Length == 233 && frames[1].Length == 26 && frames[1][3] == 10,
            "FL divide 11 arquivos em 10+1 sem perder o endereço");
        data[0] = 0xFF;
        Check(frames[0][5] == 0, "quadro FL não compartilha buffer de entrada");
    }

    private static void ClockAndResponses()
    {
        Frame(Tp02PgMemoryProtocol.ReadClock(), "0A 03 53 F9 0E 98", "RTC leitura nativa");
        Frame(Tp02PgMemoryProtocol.ReadScanTimes(), "0A 03 60 00 06 8C", "scan leitura nativa");
        Frame(Tp02PgMemoryProtocol.BuildClockWrite(new int[] { 58, 57, 23, 31, 3, 12, 99 }),
            "09 11 53 F9 0E 00 3A 00 39 00 17 00 1F 00 03 00 0C 00 63 70", "RTC escrita nativa binária, não BCD");
        Frame(Tp02PgMemoryProtocol.BuildClockWrite(new int[] { 0, 0, 0, 1, 0, 1, 0 }),
            "09 11 53 F9 0E 00 00 00 00 00 00 00 01 00 00 00 01 00 00 89", "RTC domingo e ano zero");
        Reject(delegate { Tp02PgMemoryProtocol.BuildClockWrite(new int[] { 0, 0, 0, 1, 7, 1, 26 }); }, "dia da semana 7 fora da faixa documentada");
        Reject(delegate { Tp02PgMemoryProtocol.BuildClockWrite(new int[] { 0, 0, 0, 31, 1, 4, 26 }); }, "abril não tem 31 dias");
        Reject(delegate { Tp02PgMemoryProtocol.BuildClockWrite(new int[] { 0, 0, 0, 1, 1, 1, 2026 }); }, "não inferir nem truncar século");
        // Cabeçalho/checksum sintéticos; payloads provenientes dos parsers nativos emulados.
        byte[] scan = Bytes("00 06 04 D2 01 00 0F FF 14");
        ushort[] values = Tp02PgMemoryProtocol.DecodeScanTimes(scan);
        Check(values[0] == 1234 && values[1] == 256 && values[2] == 4095, "ordem scan atual, mínimo, máximo");
        byte[] rtc = Bytes("00 0E 00 3B 00 22 00 0C 00 19 00 07 00 08 00 1A 46");
        values = Tp02PgMemoryProtocol.DecodeClock(rtc);
        Check(values.Length == 7 && values[0] == 59 && values[4] == 7 && values[6] == 26,
            "leitura preserva campo RTC bruto inclusive valor fora da faixa de escrita");
        Reject(delegate { Tp02PgMemoryProtocol.DecodeClock(scan); }, "RTC não aceita resposta scan");
        Reject(delegate { Tp02PgMemoryProtocol.DecodeScanTimes(Bytes("00 06 04 D2 01 00 0F FF 15")); }, "checksum RX inválido");
        Reject(delegate { Tp02PgMemoryProtocol.DecodeScanTimes(Bytes("00 06 04 D2 01 00 0F FF")); }, "RX truncado");
        Reject(delegate { Tp02PgMemoryProtocol.DecodeScanTimes(Bytes("00 06 04 D2 01 00 0F FF 14 00")); }, "RX com bytes excedentes");
        Reject(delegate { Tp02PgMemoryProtocol.Payload(Bytes("01 00 FE"), -1); }, "status de erro não vira dados");
        Reject(delegate { Tp02PgMemoryProtocol.DecodeWords(scan, 0); }, "quantidade de palavras inválida");
        byte[] payload = Tp02PgMemoryProtocol.Payload(scan, 6);
        payload[0] = 0;
        Check(scan[2] == 4, "payload é cópia defensiva");
    }

    public static int Main()
    {
        Reads(); Writes(); ClockAndResponses();
        Console.WriteLine("Tp02PgMemoryProtocolSelfTest: " + checks + " verificações; " + failures + " falhas (offline; nenhum TX).");
        return failures == 0 ? 0 : 1;
    }
}
