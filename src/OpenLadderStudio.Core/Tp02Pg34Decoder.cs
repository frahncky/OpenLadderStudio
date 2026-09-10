using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenLadderStudio.Core
{
    internal sealed class Tp02Pg34Step
    {
        public int Index;
        public byte High;
        public byte Low;
        public byte Braw;
        public string Instruction = "UNKNOWN";
        public string Device = string.Empty;
        public int DeviceNumber;
        public bool IsBoolean;
        public bool IsEmpty;
        public bool BrawCheckApplicable;
        public byte ExpectedBraw;
        public bool BrawMatches;

        public string ToIl()
        {
            if (IsEmpty) return "NOP";
            if (Instruction == "UNKNOWN")
                return "UNKNOWN " + High.ToString("X2", CultureInfo.InvariantCulture)
                    + Low.ToString("X2", CultureInfo.InvariantCulture)
                    + " B=" + Braw.ToString("X2", CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(Device)) return Instruction;
            return Instruction + " " + Device
                + DeviceNumber.ToString("0000", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class Tp02Pg34DecodeResult
    {
        public bool IsValid;
        public string Error = string.Empty;
        public byte Flags;
        public int PayloadLength;
        public int StepCount;
        public bool HasFrame38StepCountHint;
        public bool StepCountFrom38Hint;
        public int Frame38LastPairOffsetHint = -1;
        public int PayloadTailStepCount;
        public bool Frame38HintMatchesPayloadTail;
        public int BooleanSteps;
        public int UnknownSteps;
        public int BrawChecked;
        public int BrawMismatches;
        public readonly List<Tp02Pg34Step> Steps = new List<Tp02Pg34Step>();
    }

    internal static class Tp02Pg34Decoder
    {
        private const int PlaneASize = 0xA0;
        private const int MaxSteps = 80;

        public static Tp02Pg34DecodeResult Decode(byte[] frame34, byte[] frame38)
        {
            Tp02Pg34DecodeResult result = new Tp02Pg34DecodeResult();

            if (frame34 == null || frame34.Length < 3)
                return Fail(result, "quadro 34 ausente ou curto");

            int payloadLength = frame34[1];
            if (frame34.Length != payloadLength + 3)
                return Fail(result, "tamanho do quadro 34 nao coincide com LEN");
            if (Sum8(frame34) != 0xFF)
                return Fail(result, "checksum do quadro 34 diferente de FF");
            if (payloadLength != 0xF0)
                return Fail(result, "geometria conhecida exige LEN=F0 (240 bytes)");

            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(frame34, 2, payload, 0, payloadLength);

            result.Flags = frame34[0];
            result.PayloadLength = payloadLength;
            result.PayloadTailStepCount = DetectPayloadTailStepCount(payload);

            int hintCount;
            int hintOffset;
            if (TryGetStepCountHintFrom38(frame38, out hintCount, out hintOffset))
            {
                result.HasFrame38StepCountHint = true;
                result.Frame38LastPairOffsetHint = hintOffset;
                result.Frame38HintMatchesPayloadTail =
                    result.PayloadTailStepCount == 0 || result.PayloadTailStepCount == hintCount;

                if (result.Frame38HintMatchesPayloadTail)
                {
                    result.StepCountFrom38Hint = true;
                    result.StepCount = hintCount;
                }
                else
                {
                    // A relacao do 38 com o ultimo par ativo ainda e evidencia forte,
                    // nao uma regra universal. Em divergencia, nao truncar o payload.
                    result.StepCount = result.PayloadTailStepCount;
                }
            }
            else
            {
                result.StepCount = result.PayloadTailStepCount;
                result.Frame38HintMatchesPayloadTail = true;
            }

            if (result.StepCount < 0 || result.StepCount > MaxSteps)
                return Fail(result, "quantidade de passos fora da geometria de 80 passos");

            for (int i = 0; i < result.StepCount; i++)
            {
                Tp02Pg34Step step = DecodeStep(
                    i,
                    payload[2 * i],
                    payload[(2 * i) + 1],
                    payload[PlaneASize + i]);

                result.Steps.Add(step);
                if (step.IsBoolean) result.BooleanSteps++;
                else if (!step.IsEmpty) result.UnknownSteps++;

                if (step.BrawCheckApplicable)
                {
                    result.BrawChecked++;
                    if (!step.BrawMatches) result.BrawMismatches++;
                }
            }

            result.IsValid = true;
            return result;
        }

        internal static byte CalculateBooleanBraw(byte high, byte low)
        {
            int sum = (high >> 4) + (high & 0x0F) + (low >> 4) + (low & 0x0F);
            return (byte)sum;
        }

        private static Tp02Pg34Step DecodeStep(int index, byte high, byte low, byte braw)
        {
            Tp02Pg34Step step = new Tp02Pg34Step();
            step.Index = index;
            step.High = high;
            step.Low = low;
            step.Braw = braw;

            if (high == 0x00 && low == 0x00 && braw == 0x00)
            {
                step.IsEmpty = true;
                step.Instruction = "NOP";
                return step;
            }

            string instruction = DecodeBooleanInstruction(low);
            string device;
            int number;
            if (!string.IsNullOrEmpty(instruction) && TryDecodeBitDevice(high, low, out device, out number))
            {
                step.Instruction = instruction;
                step.Device = device;
                step.DeviceNumber = number;
                step.IsBoolean = true;
                step.BrawCheckApplicable = true;
                step.ExpectedBraw = CalculateBooleanBraw(high, low);
                step.BrawMatches = step.ExpectedBraw == braw;
                return step;
            }

            // TMR/CNT sao conhecidos por analise estatica, mas o formato completo do
            // operando e a regra de BRAW ainda nao foram validados fisicamente.
            int opcode = low & 0x78;
            if (opcode == 0x60) step.Instruction = "TMR";
            else if (opcode == 0x68) step.Instruction = "CNT";
            else if (low == 0x01) step.Instruction = "AND STR";
            else if (low == 0x02) step.Instruction = "OR STR";

            return step;
        }

        private static string DecodeBooleanInstruction(byte low)
        {
            switch (low & 0x78)
            {
                case 0x10: return "STR";
                case 0x18: return "STR NOT";
                case 0x20: return "AND";
                case 0x28: return "AND NOT";
                case 0x30: return "OR";
                case 0x38: return "OR NOT";
                case 0x40: return "OUT";
                default: return string.Empty;
            }
        }

        private static bool TryDecodeBitDevice(byte high, byte low, out string device, out int number)
        {
            device = string.Empty;
            number = 0;

            int deviceBase = high & 0x60;
            if (deviceBase == 0x00) device = "X";
            else if (deviceBase == 0x20) device = "Y";
            else if (deviceBase == 0x40) device = "C";
            else return false;

            int group = high & 0x1F;
            int bit = low & 0x07;
            number = (group * 8) + bit + 1;
            return number > 0;
        }

        private static int DetectPayloadTailStepCount(byte[] payload)
        {
            for (int i = MaxSteps - 1; i >= 0; i--)
            {
                byte high = payload[2 * i];
                byte low = payload[(2 * i) + 1];
                byte braw = payload[PlaneASize + i];
                if (high != 0x00 || low != 0x00 || braw != 0x00)
                    return i + 1;
            }
            return 0;
        }

        private static bool TryGetStepCountHintFrom38(byte[] frame38, out int stepCount, out int offset)
        {
            stepCount = 0;
            offset = -1;
            if (frame38 == null || frame38.Length != 5) return false;
            if (Sum8(frame38) != 0xFF) return false;
            if (frame38[0] != 0x00 || frame38[1] != 0x02 || frame38[2] != 0x00) return false;

            int value = frame38[3];
            if ((value & 0x01) != 0 || value > 0x9E) return false;

            int count = (value / 2) + 1;
            if (count < 1 || count > MaxSteps) return false;

            offset = value;
            stepCount = count;
            return true;
        }

        private static Tp02Pg34DecodeResult Fail(Tp02Pg34DecodeResult result, string error)
        {
            result.IsValid = false;
            result.Error = error;
            return result;
        }

        private static int Sum8(byte[] bytes)
        {
            int sum = 0;
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                    sum = (sum + bytes[i]) & 0xFF;
            }
            return sum;
        }
    }
}
