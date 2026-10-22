using System;
using OpenLadderStudio.Core;

namespace OpenLadderStudio.Core.Tests
{
    internal static class Tp02Pc12StaticReadFramesSelfTest
    {
        private static int failures;

        private static int Main()
        {
            Check("V0000", Hex(Tp02Pc12StaticReadFrames.Build(Tp02Pc12StaticReadFrames.Area.V, 0)) == "0A0350008022");
            Check("D0000", Hex(Tp02Pc12StaticReadFrames.Build(Tp02Pc12StaticReadFrames.Area.D, 0)) == "0A03900080E2");
            Check("WC000", Hex(Tp02Pc12StaticReadFrames.Build(Tp02Pc12StaticReadFrames.Area.WC, 0)) == "0A0370007210");
            Check("V1000", Hex(Tp02Pc12StaticReadFrames.Build(Tp02Pc12StaticReadFrames.Area.V, 0x100)) == "0A0351008021");
            Check("checksum V", Tp02Pc12StaticReadFrames.HasChecksumFF(Tp02Pc12StaticReadFrames.Build(Tp02Pc12StaticReadFrames.Area.V, 0)));
            Check("resposta V/D", Tp02Pc12StaticReadFrames.ExpectedResponseLength(Tp02Pc12StaticReadFrames.Area.D) == 131);
            Check("resposta WC", Tp02Pc12StaticReadFrames.ExpectedResponseLength(Tp02Pc12StaticReadFrames.Area.WC) == 117);
            Check("endereco invalido", Throws(delegate { Tp02Pc12StaticReadFrames.Build(Tp02Pc12StaticReadFrames.Area.V, 0x1000); }));
            return failures == 0 ? 0 : 1;
        }

        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", String.Empty); }

        private static bool Throws(Action action)
        {
            try { action(); return false; }
            catch (ArgumentOutOfRangeException) { return true; }
        }

        private static void Check(string name, bool passed)
        {
            Console.WriteLine((passed ? "ok " : "FALHA ") + name);
            if (!passed) failures++;
        }
    }
}
