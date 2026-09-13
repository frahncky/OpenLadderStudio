using System;
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

    public static int Main()
    {
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Hello), "HELLO deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.F0), "F0 deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(TP02PhysicalValidationSafety.Frame38), "38 deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(0)), "PG34 pagina zero deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02Pg34Pager.BuildReadRequest(80)), "PG34 pagina 80 deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.V, 1, 2)), "PG0A V deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.ReadClock()), "PG0A RTC deve ser permitido");
        Check(TP02PhysicalValidationSafety.IsAllowed(Tp02PgMemoryProtocol.ReadScanTimes()), "PG0A scan deve ser permitido");

        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x02, 0x00, 0xFD }), "RUN deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x01, 0x00, 0xFE }), "STOP deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x03, 0x00, 0xFC }), "Clear/prepare 03 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x04, 0x00, 0xFB }), "Clear 04 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x11, 0x00, 0xEE }), "Clear 11 deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x0F, 0x00, 0xF0 }), "Clear 0F deve ser bloqueado");
        Check(!TP02PhysicalValidationSafety.IsAllowed(new byte[] { 0x14, 0x00, 0xEB }), "gate 14 deve ser bloqueado");

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
