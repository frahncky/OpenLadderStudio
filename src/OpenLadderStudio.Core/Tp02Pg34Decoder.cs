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
        public string DisplayText = string.Empty;
        public bool IsBoolean;
        public bool IsEmpty;
        public bool BrawCheckApplicable;
        public byte ExpectedBraw;
        public bool BrawMatches;

        public string ToIl()
        {
            if (IsEmpty) return "NOP";
            if (!string.IsNullOrEmpty(DisplayText)) return DisplayText;
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
                    // O 38 acompanha o offset do ultimo par HIGH/LOW nas capturas
                    // conhecidas, inclusive com instrucoes de varios passos. Ainda
                    // assim, divergencia futura nao deve truncar silenciosamente o 34.
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
                else if (!step.IsEmpty && step.Instruction == "UNKNOWN") result.UnknownSteps++;

                if (step.BrawCheckApplicable)
                {
                    result.BrawChecked++;
                    if (!step.BrawMatches) result.BrawMismatches++;
                }
            }

            result.IsValid = true;
            return result;
        }

        /// <summary>
        /// Regra fisicamente observada no programa booleano de 26 passos e no
        /// programa misto de 23 passos (TMR/CNT/SET/RST/ADDw/operandos/END):
        /// soma dos quatro nibbles de HIGH/LOW reduzida ao nibble baixo.
        /// </summary>
        internal static byte CalculateBraw(byte high, byte low)
        {
            int sum = (high >> 4) + (high & 0x0F) + (low >> 4) + (low & 0x0F);
            return (byte)(sum & 0x0F);
        }

        // Mantido para compatibilidade com autotestes/chamadores anteriores.
        internal static byte CalculateBooleanBraw(byte high, byte low)
        {
            return CalculateBraw(high, low);
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
            }
            else if ((high & 0x80) == 0 && TryDecodeTimerCounter(high, low, out instruction, out number))
            {
                step.Instruction = instruction;
                step.Device = "V";
                step.DeviceNumber = number;
            }
            else if (TryDecodeConfirmedFunction(high, low, out instruction))
            {
                step.Instruction = instruction;
                step.DisplayText = instruction;
            }
            else if (TryDecodeLiteral(high, low, out number))
            {
                step.Instruction = "K";
                step.DisplayText = "K" + number.ToString(CultureInfo.InvariantCulture);
            }
            else if (TryDecodeSpecialBitOperand(high, low, out device, out number))
            {
                step.Instruction = "ARG";
                step.Device = device;
                step.DeviceNumber = number;
            }
            else if (TryDecodeConfirmedDOperand(high, low, out number))
            {
                step.Instruction = "ARG";
                step.Device = "D";
                step.DeviceNumber = number;
            }
            else if (high == 0x00 && low == 0x01)
            {
                step.Instruction = "AND STR";
            }
            else if (high == 0x00 && low == 0x02)
            {
                step.Instruction = "OR STR";
            }

            // A captura de 2026-09-10 18:35 mostrou a mesma regra BRAW em todos
            // os 23 passos ativos, inclusive nao booleanos. A checagem continua
            // diagnostica: divergencia e reportada, mas nao invalida o quadro 34.
            step.BrawCheckApplicable = true;
            step.ExpectedBraw = CalculateBraw(high, low);
            step.BrawMatches = step.ExpectedBraw == braw;
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

            // HIGH com bit 7 pertence a outras classes (por exemplo literal).
            if ((high & 0x80) != 0) return false;

            int group = high & 0x1F;
            int bit = low & 0x07;
            number = (group * 8) + bit + 1;
            return number > 0;
        }

        private static bool TryDecodeTimerCounter(byte high, byte low, out string instruction, out int number)
        {
            instruction = string.Empty;
            number = 0;

            int opcode = low & 0x78;
            if (opcode == 0x60) instruction = "TMR";
            else if (opcode == 0x68) instruction = "CNT";
            else return false;

            int index = (high & 0x7F) | ((low & 0x07) << 7);
            number = index + 1;
            return true;
        }

        private static bool TryDecodeConfirmedFunction(byte high, byte low, out string instruction)
        {
            instruction = string.Empty;
            if (high == 0x00 && low == 0x70)
            {
                instruction = "F-00 END";
                return true;
            }
            if (high == 0x17 && low == 0x71)
            {
                instruction = "F-23 SET";
                return true;
            }
            if (high == 0x18 && low == 0x71)
            {
                instruction = "F-24 RST";
                return true;
            }
            if (high == 0x0D && low == 0x77)
            {
                instruction = "F-13w ADD";
                return true;
            }
            return false;
        }

        private static bool TryDecodeLiteral(byte high, byte low, out int value)
        {
            value = 0;
            if (high < 0x80 || high > 0x9F || (low & 0x80) != 0) return false;

            int highNibble = (high & 0x1E) >> 1;
            int lowByte = ((high & 0x01) << 7) | (low & 0x7F);
            value = (highNibble << 8) | lowByte;
            return true;
        }

        private static bool TryDecodeSpecialBitOperand(byte high, byte low, out string device, out int number)
        {
            device = string.Empty;
            number = 0;
            if ((low & 0x80) == 0) return false;

            int deviceBase = high & 0xF8;
            if (deviceBase == 0xC0) device = "X";
            else if (deviceBase == 0xC8) device = "Y";
            else if (deviceBase == 0xD0) device = "C";
            else return false;

            int bit = high & 0x07;
            int group = low & 0x7F;
            number = (group * 8) + bit + 1;
            return true;
        }

        private static bool TryDecodeConfirmedDOperand(byte high, byte low, out int number)
        {
            number = 0;
            if ((high & 0xF8) != 0xF0 || (low & 0x80) != 0) return false;

            int h = (high & 0x0E) >> 1;
            int l = ((high & 0x01) << 7) | (low & 0x7F);
            int index = (h << 8) | l;
            number = index + 1;
            return true;
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
