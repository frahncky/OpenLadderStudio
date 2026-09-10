using System;
using System.Globalization;
using OpenLadderStudio.Core;

internal static class Tp02Pg34DecoderSelfTest
{
    private static int failures;

    public static int Main()
    {
        byte[] frame34 = BuildComprehensiveFrame();
        byte[] frame38 = ParseHex("00 02 00 32 CB");

        Check(frame34.Length == 243, "fixture fisico deve ter 243 bytes");
        Check(frame34[1] == 0xF0, "fixture fisico deve ter LEN=F0");
        Check(frame34[frame34.Length - 1] == 0x98, "checksum da captura fisica deve ser 98h");
        Check(Sum8(frame34) == 0xFF, "fixture fisico deve fechar soma FF");

        Tp02Pg34DecodeResult decoded = Tp02Pg34Decoder.Decode(frame34, frame38);
        Check(decoded.IsValid, "quadro completo deve ser valido");

        if (decoded.IsValid)
        {
            Check(decoded.PayloadLength == 0xF0, "LEN deve ser F0");
            Check(decoded.StepCount == 26, "38/payload devem indicar 26 passos");
            Check(decoded.HasFrame38StepCountHint, "38 deve produzir hint estrutural");
            Check(decoded.StepCountFrom38Hint, "hint 38 consistente deve ser usado");
            Check(decoded.Frame38LastPairOffsetHint == 0x32, "offset hint do 38 deve ser 32h");
            Check(decoded.Frame38HintMatchesPayloadTail, "38 deve coincidir com cauda ativa");
            Check(decoded.PayloadTailStepCount == 26, "cauda do payload deve terminar no passo 25");
            Check(decoded.BooleanSteps == 26, "todos os 26 passos devem ser booleanos");
            Check(decoded.UnknownSteps == 0, "nao deve haver passo desconhecido");
            Check(decoded.BrawChecked == 26, "BRAW deve ser validado nos 26 passos booleanos");
            Check(decoded.BrawMismatches == 0, "BRAW deve fechar em todos os passos");

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

            Check(decoded.Steps.Count == expected.Length, "quantidade de passos decodificados");
            int limit = Math.Min(decoded.Steps.Count, expected.Length);
            for (int i = 0; i < limit; i++)
                Check(decoded.Steps[i].ToIl() == expected[i],
                    "passo " + i.ToString(CultureInfo.InvariantCulture)
                    + " esperado=" + expected[i] + " obtido=" + decoded.Steps[i].ToIl());

            if (decoded.Steps.Count > 24)
            {
                Check(decoded.Steps[24].High == 0x02, "X0018 HIGH=02");
                Check(decoded.Steps[24].Low == 0x11, "X0018 LOW=11");
                Check(decoded.Steps[24].Braw == 0x04, "X0018 BRAW=04");
            }
        }

        Check(Tp02Pg34Decoder.CalculateBooleanBraw(0x02, 0x11) == 0x04,
            "soma de nibbles de X0018");

        Tp02Pg34DecodeResult without38 = Tp02Pg34Decoder.Decode(frame34, null);
        Check(without38.IsValid, "decode sem 38 deve continuar valido");
        if (without38.IsValid)
        {
            Check(!without38.StepCountFrom38Hint, "sem 38 nao pode usar hint");
            Check(without38.StepCount == 26, "sem 38 deve detectar 26 passos pela cauda");
        }

        byte[] corruptBraw = (byte[])frame34.Clone();
        int brawFrameIndex = 2 + 0xA0 + 5; // passo 5: AND X0002
        corruptBraw[brawFrameIndex] = (byte)(corruptBraw[brawFrameIndex] + 1);
        RepairChecksum(corruptBraw);
        Tp02Pg34DecodeResult brawMismatch = Tp02Pg34Decoder.Decode(corruptBraw, frame38);
        Check(brawMismatch.IsValid, "quadro com checksum reparado deve ser estruturalmente valido");
        if (brawMismatch.IsValid)
            Check(brawMismatch.BrawMismatches == 1, "um BRAW alterado deve gerar uma divergencia");

        byte[] badChecksum = (byte[])frame34.Clone();
        badChecksum[badChecksum.Length - 1] ^= 0x01;
        Tp02Pg34DecodeResult checksumFailure = Tp02Pg34Decoder.Decode(badChecksum, frame38);
        Check(!checksumFailure.IsValid, "checksum quebrado deve invalidar o quadro");

        if (failures == 0)
        {
            Console.WriteLine("Tp02Pg34DecoderSelfTest: OK");
            return 0;
        }

        Console.Error.WriteLine("Tp02Pg34DecoderSelfTest: " + failures.ToString(CultureInfo.InvariantCulture) + " falha(s)");
        return 1;
    }

    private static byte[] BuildComprehensiveFrame()
    {
        // Captura física TP02-PG-Lab-20260910-135502.txt. A Região A possui
        // 26 pares HIGH/LOW; a Região B possui os 26 BRAW correspondentes.
        // O restante do payload de 240 bytes é zero.
        byte[] planeA = ParseHex(
            "00 10 20 40 00 19 20 41 01 10 00 21 20 42 01 10 00 29 20 43 " +
            "01 10 00 31 20 44 01 10 00 39 20 45 00 17 20 46 01 10 20 47 " +
            "01 17 21 40 02 10 21 41 02 11 21 42");

        byte[] braw = ParseHex(
            "01 06 0A 07 02 03 08 02 0B 09 02 04 0A 02 0C 0B 08 0C 02 0D 09 07 03 08 04 09");

        Check(planeA.Length == 52, "fixture plane A deve conter 52 bytes");
        Check(braw.Length == 26, "fixture BRAW deve conter 26 bytes");

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
