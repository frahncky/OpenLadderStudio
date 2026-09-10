using System;
using System.Globalization;
using OpenLadderStudio.Core;

internal static class Tp02Pg34DecoderSelfTest
{
    private static int failures;

    public static int Main()
    {
        RunBooleanMatrixTest();
        RunMixedVariableInstructionTest();
        RunStructuralFailureTests();

        if (failures == 0)
        {
            Console.WriteLine("Tp02Pg34DecoderSelfTest: OK");
            return 0;
        }

        Console.Error.WriteLine("Tp02Pg34DecoderSelfTest: " + failures.ToString(CultureInfo.InvariantCulture) + " falha(s)");
        return 1;
    }

    private static void RunBooleanMatrixTest()
    {
        byte[] frame34 = BuildBooleanFrame();
        byte[] frame38 = ParseHex("00 02 00 32 CB");

        Check(frame34.Length == 243, "fixture booleana deve ter 243 bytes");
        Check(frame34[1] == 0xF0, "fixture booleana deve ter LEN=F0");
        Check(frame34[frame34.Length - 1] == 0x98, "checksum da captura booleana deve ser 98h");
        Check(Sum8(frame34) == 0xFF, "fixture booleana deve fechar soma FF");

        Tp02Pg34DecodeResult decoded = Tp02Pg34Decoder.Decode(frame34, frame38);
        Check(decoded.IsValid, "quadro booleano completo deve ser valido");

        if (decoded.IsValid)
        {
            Check(decoded.PayloadLength == 0xF0, "LEN booleano deve ser F0");
            Check(decoded.StepCount == 26, "38/payload booleanos devem indicar 26 passos");
            Check(decoded.HasFrame38StepCountHint, "38 booleano deve produzir hint estrutural");
            Check(decoded.StepCountFrom38Hint, "hint 38 booleano consistente deve ser usado");
            Check(decoded.Frame38LastPairOffsetHint == 0x32, "offset hint booleano deve ser 32h");
            Check(decoded.Frame38HintMatchesPayloadTail, "38 booleano deve coincidir com cauda ativa");
            Check(decoded.PayloadTailStepCount == 26, "cauda booleana deve terminar no passo 25");
            Check(decoded.BooleanSteps == 26, "todos os 26 passos devem ser booleanos");
            Check(decoded.UnknownSteps == 0, "matriz booleana nao deve ter passo desconhecido");
            Check(decoded.BrawChecked == 26, "BRAW deve ser validado nos 26 passos booleanos");
            Check(decoded.BrawMismatches == 0, "BRAW booleano deve fechar em todos os passos");

            string[] expected = new string[]
            {
                "STR X0001",
                "OUT Y0001",
                "STR NOT X0002",
                "OUT Y0002",
                "STR X0009",
                "AND X0002",
                "OUT Y0003",
                "STR X0009",
                "AND NOT X0002",
                "OUT Y0004",
                "STR X0009",
                "OR X0002",
                "OUT Y0005",
                "STR X0009",
                "OR NOT X0002",
                "OUT Y0006",
                "STR X0008",
                "OUT Y0007",
                "STR X0009",
                "OUT Y0008",
                "STR X0016",
                "OUT Y0009",
                "STR X0017",
                "OUT Y0010",
                "STR X0018",
                "OUT Y0011",
            };

            CheckIl(decoded, expected, "booleano");

            if (decoded.Steps.Count > 24)
            {
                Check(decoded.Steps[24].High == 0x02, "X0018 HIGH=02");
                Check(decoded.Steps[24].Low == 0x11, "X0018 LOW=11");
                Check(decoded.Steps[24].Braw == 0x04, "X0018 BRAW=04");
            }
        }

        Check(Tp02Pg34Decoder.CalculateBraw(0x02, 0x11) == 0x04,
            "BRAW de X0018 deve ser 04");

        Tp02Pg34DecodeResult without38 = Tp02Pg34Decoder.Decode(frame34, null);
        Check(without38.IsValid, "decode booleano sem 38 deve continuar valido");
        if (without38.IsValid)
        {
            Check(!without38.StepCountFrom38Hint, "sem 38 nao pode usar hint");
            Check(without38.StepCount == 26, "sem 38 deve detectar 26 passos pela cauda");
        }

        byte[] corruptBraw = (byte[])frame34.Clone();
        int brawFrameIndex = 2 + 0xA0 + 5;
        corruptBraw[brawFrameIndex] = (byte)(corruptBraw[brawFrameIndex] + 1);
        RepairChecksum(corruptBraw);
        Tp02Pg34DecodeResult brawMismatch = Tp02Pg34Decoder.Decode(corruptBraw, frame38);
        Check(brawMismatch.IsValid, "quadro booleano com checksum reparado deve ser estruturalmente valido");
        if (brawMismatch.IsValid)
            Check(brawMismatch.BrawMismatches == 1, "um BRAW booleano alterado deve gerar uma divergencia");
    }

    private static void RunMixedVariableInstructionTest()
    {
        byte[] frame34 = BuildMixedVariableFrame();
        byte[] frame38 = ParseHex("00 02 00 2C D1");

        Check(frame34.Length == 243, "fixture mista deve ter 243 bytes");
        Check(frame34[1] == 0xF0, "fixture mista deve ter LEN=F0");
        Check(frame34[frame34.Length - 1] == 0x52, "checksum da captura mista deve ser 52h");
        Check(Sum8(frame34) == 0xFF, "fixture mista deve fechar soma FF");

        Tp02Pg34DecodeResult decoded = Tp02Pg34Decoder.Decode(frame34, frame38);
        Check(decoded.IsValid, "quadro misto completo deve ser valido");

        if (decoded.IsValid)
        {
            Check(decoded.StepCount == 23, "38/payload mistos devem indicar 23 passos");
            Check(decoded.PayloadTailStepCount == 23, "cauda mista deve terminar no passo 22");
            Check(decoded.Frame38LastPairOffsetHint == 0x2C, "offset hint misto deve ser 2Ch");
            Check(decoded.Frame38HintMatchesPayloadTail, "38 misto deve coincidir com cauda ativa");
            Check(decoded.StepCountFrom38Hint, "hint 38 misto consistente deve ser usado");
            Check(decoded.BooleanSteps == 10, "captura mista deve ter 10 passos booleanos");
            Check(decoded.UnknownSteps == 0, "captura mista nao deve ter passo desconhecido");
            Check(decoded.BrawChecked == 23, "BRAW deve ser checado nos 23 passos mistos");
            Check(decoded.BrawMismatches == 0, "BRAW misto deve fechar nos 23 passos");

            string[] expected = new string[]
            {
                "STR X0001",
                "TMR V0001",
                "K1000",
                "OUT C0001",
                "STR X0002",
                "STR X0003",
                "CNT V0002",
                "K10",
                "OUT C0002",
                "STR X0004",
                "F-23 SET",
                "ARG Y0001",
                "STR X0005",
                "F-24 RST",
                "ARG Y0001",
                "STR X0006",
                "F-13w ADD",
                "ARG D0002",
                "ARG D0001",
                "K10",
                "STR X0007",
                "OUT Y0002",
                "F-00 END",
            };

            CheckIl(decoded, expected, "misto");

            Check(decoded.Steps[1].High == 0x00 && decoded.Steps[1].Low == 0x60,
                "TMR V0001 deve ser 00 60");
            Check(decoded.Steps[2].High == 0x87 && decoded.Steps[2].Low == 0x68,
                "K1000 deve ser 87 68");
            Check(decoded.Steps[6].High == 0x01 && decoded.Steps[6].Low == 0x68,
                "CNT V0002 deve ser 01 68");
            Check(decoded.Steps[7].High == 0x80 && decoded.Steps[7].Low == 0x0A,
                "K10 deve ser 80 0A");
            Check(decoded.Steps[10].High == 0x17 && decoded.Steps[10].Low == 0x71,
                "F-23 SET deve ser 17 71");
            Check(decoded.Steps[13].High == 0x18 && decoded.Steps[13].Low == 0x71,
                "F-24 RST deve ser 18 71");
            Check(decoded.Steps[16].High == 0x0D && decoded.Steps[16].Low == 0x77,
                "F-13w ADD deve ser 0D 77");
            Check(decoded.Steps[22].High == 0x00 && decoded.Steps[22].Low == 0x70,
                "F-00 END deve ser 00 70");
        }

        Check(Tp02Pg34Decoder.CalculateBraw(0x87, 0x68) == 0x0D,
            "BRAW K1000 deve aplicar soma de nibbles modulo 16");
        Check(Tp02Pg34Decoder.CalculateBraw(0x18, 0x71) == 0x01,
            "BRAW F-24 deve aplicar modulo 16");
        Check(Tp02Pg34Decoder.CalculateBraw(0xF0, 0x01) == 0x00,
            "BRAW D0002 deve aplicar modulo 16");
    }

    private static void RunStructuralFailureTests()
    {
        byte[] frame34 = BuildMixedVariableFrame();
        byte[] badChecksum = (byte[])frame34.Clone();
        badChecksum[badChecksum.Length - 1] ^= 0x01;
        Tp02Pg34DecodeResult checksumFailure = Tp02Pg34Decoder.Decode(badChecksum, ParseHex("00 02 00 2C D1"));
        Check(!checksumFailure.IsValid, "checksum quebrado deve invalidar o quadro");
    }

    private static void CheckIl(Tp02Pg34DecodeResult decoded, string[] expected, string label)
    {
        Check(decoded.Steps.Count == expected.Length, "quantidade de passos " + label + " decodificados");
        int limit = Math.Min(decoded.Steps.Count, expected.Length);
        for (int i = 0; i < limit; i++)
            Check(decoded.Steps[i].ToIl() == expected[i],
                label + " passo " + i.ToString(CultureInfo.InvariantCulture)
                + " esperado=" + expected[i] + " obtido=" + decoded.Steps[i].ToIl());
    }

    private static byte[] BuildBooleanFrame()
    {
        byte[] planeA = ParseHex(
            "00 10 20 40 00 19 20 41 01 10 00 21 20 42 01 10 00 29 20 43 " +
            "01 10 00 31 20 44 01 10 00 39 20 45 00 17 20 46 01 10 20 47 " +
            "01 17 21 40 02 10 21 41 02 11 21 42");

        byte[] braw = ParseHex(
            "01 06 0A 07 02 03 08 02 0B 09 02 04 0A 02 0C 0B 08 0C 02 0D 09 07 03 08 04 09");

        Check(planeA.Length == 52, "fixture booleana plane A deve conter 52 bytes");
        Check(braw.Length == 26, "fixture booleana BRAW deve conter 26 bytes");
        return BuildFrame(planeA, braw);
    }

    private static byte[] BuildMixedVariableFrame()
    {
        // Captura física TP02-PG-Lab-20260910-183546.txt.
        // Programa: TMR, CNT com reset, SET, RST, F-13w ADD, OUT e END.
        byte[] planeA = ParseHex(
            "00 10 00 60 87 68 40 40 00 11 00 12 01 68 80 0A 40 41 00 13 " +
            "17 71 C8 80 00 14 18 71 C8 80 00 15 0D 77 F0 01 F0 00 80 0A " +
            "00 16 20 41 00 70");

        byte[] braw = ParseHex(
            "01 06 0D 08 02 03 0F 02 09 04 00 0C 05 01 0C 06 0B 00 0F 02 07 07 07");

        Check(planeA.Length == 46, "fixture mista plane A deve conter 46 bytes");
        Check(braw.Length == 23, "fixture mista BRAW deve conter 23 bytes");
        return BuildFrame(planeA, braw);
    }

    private static byte[] BuildFrame(byte[] planeA, byte[] braw)
    {
        byte[] frame = new byte[243];
        frame[0] = 0x00;
        frame[1] = 0xF0;
        Buffer.BlockCopy(planeA, 0, frame, 2, planeA.Length);
        Buffer.BlockCopy(braw, 0, frame, 2 + 0xA0, braw.Length);
        RepairChecksum(frame);
        return frame;
    }

    private static void RepairChecksum(byte[] frame)
    {
        int sum = 0;
        for (int i = 0; i < frame.Length - 1; i++)
            sum = (sum + frame[i]) & 0xFF;
        frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);
    }

    private static int Sum8(byte[] bytes)
    {
        int sum = 0;
        for (int i = 0; i < bytes.Length; i++)
            sum = (sum + bytes[i]) & 0xFF;
        return sum;
    }

    private static byte[] ParseHex(string hex)
    {
        string[] parts = hex.Split(new char[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        byte[] bytes = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            bytes[i] = byte.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }

    private static void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FALHA: " + message);
    }
}
