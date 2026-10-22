using System;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Produz somente em memória os frames de leitura encontrados na análise estática do PC12 2.1.
    /// Esta classe não abre uma porta serial e não transmite bytes. Os frames ainda precisam ser
    /// confirmados por uma captura rotulada antes de serem usados contra um PLC real.
    /// </summary>
    public static class Tp02Pc12StaticReadFrames
    {
        public enum Area { V, D, WC }

        public static byte[] Build(Area area, int startAddress)
        {
            if (startAddress < 0 || startAddress > 0x0fff)
                throw new ArgumentOutOfRangeException("startAddress", "O endereço deve estar entre 0x000 e 0xFFF.");

            byte commandBase;
            byte responseDataLength;
            switch (area)
            {
                case Area.V: commandBase = 0x50; responseDataLength = 0x80; break;
                case Area.D: commandBase = 0x90; responseDataLength = 0x80; break;
                case Area.WC: commandBase = 0x70; responseDataLength = 0x72; break;
                default: throw new ArgumentOutOfRangeException("area");
            }

            byte[] frame = new byte[6];
            frame[0] = 0x0a;
            frame[1] = 0x03;
            frame[2] = (byte)(commandBase | ((startAddress >> 8) & 0x0f));
            frame[3] = (byte)startAddress;
            frame[4] = responseDataLength;
            frame[5] = ChecksumToFF(frame, 0, 5);
            return frame;
        }

        public static int ExpectedResponseLength(Area area)
        {
            switch (area)
            {
                case Area.V:
                case Area.D: return 0x80 + 3;
                case Area.WC: return 0x72 + 3;
                default: throw new ArgumentOutOfRangeException("area");
            }
        }

        public static bool HasChecksumFF(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            int sum = 0;
            for (int index = 0; index < bytes.Length; index++) sum += bytes[index];
            return (sum & 0xff) == 0xff;
        }

        private static byte ChecksumToFF(byte[] bytes, int offset, int count)
        {
            int sum = 0;
            for (int index = offset; index < offset + count; index++) sum += bytes[index];
            return (byte)(0xff - (sum & 0xff));
        }
    }
}
