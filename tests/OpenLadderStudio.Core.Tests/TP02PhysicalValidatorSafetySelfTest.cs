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

    public static int Main()
    {
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Hello), "HELLO deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.F0), "F0 deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Frame38), "38 deve ser permitido");

        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(0)), "PG34 pagina zero deve ser permitida");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(80)), "PG34 pagina 80 deve ser permitida");
        Check(!TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(160)), "PG34 fora da campanha P0/P80 deve ser bloqueado");

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

        if (failures != 0)
        {
            Console.Error.WriteLine("TP02PhysicalValidatorSafetySelfTest: " + failures + " falha(s).");
            return 1;
        }
        Console.WriteLine("TP02PhysicalValidatorSafetySelfTest: OK");
        return 0;
    }
}
