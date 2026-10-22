using System;
using System.Globalization;

namespace ModernPC12
{
    /// <summary>
    /// Ponte de laboratorio entre o programa recebido por PG33 e as respostas
    /// de leitura PG38/PG34.
    ///
    /// Evidencia fisica (2026-09-10):
    /// - PG34: 240 bytes = 160 bytes HIGH/LOW + 80 bytes BRAW;
    /// - PG38 byte de estado observado = offset do ultimo par HIGH/LOW
    ///   (2 * (passos - 1)) para paginas parciais;
    /// - BRAW observado em todos os 23 passos ativos do programa misto = soma
    ///   dos quatro nibbles de HIGH/LOW, reduzida ao nibble baixo.
    ///
    /// O byte External transmitido pelo PG33 e preservado no banco de escrita,
    /// mas NAO e copiado para o BRAW. O readback usa a regra fisica de BRAW.
    /// </summary>
    internal static class TP02PgReadback
    {
        private const int StepsPerPage = 80;
        private const int PlaneASize = 0xA0;
        private const int PayloadLength = 0xF0;
        private const int MaxProgramSteps = 4000;

        internal static byte[] Build38(
            Pg33MachineWord[] words,
            bool[] valid,
            int highestProgramStep,
            byte[] fallback)
        {
            if (words == null || valid == null || highestProgramStep < 0)
                return CloneOrEmpty(fallback);

            int count = CountContiguousFrom(words, valid, 0, highestProgramStep);
            if (count <= 0)
                return CloneOrEmpty(fallback);

            // O campo observado no 38 e um offset HIGH/LOW dentro da pagina
            // de 80 passos. Para programas maiores, a primeira pagina e cheia.
            int pageCount = Math.Min(count, StepsPerPage);
            int lastPairOffset = 2 * (pageCount - 1);

            byte[] frame = new byte[]
            {
                0x00,
                0x02,
                0x00,
                (byte)lastPairOffset,
                0x00
            };
            frame[4] = Checksum(frame, 4);
            return frame;
        }

        internal static byte[] Build34(
            byte[] request,
            Pg33MachineWord[] words,
            bool[] valid,
            int highestProgramStep,
            byte[] fallbackPage0000)
        {
            int startStep = DecodeStartStep(request);

            if (words == null || valid == null || highestProgramStep < 0)
                return BuildFallback34(startStep, fallbackPage0000);

            byte[] payload = new byte[PayloadLength];
            for (int local = 0; local < StepsPerPage; local++)
            {
                int step = startStep + local;
                if (step < 0 || step >= MaxProgramSteps || step >= valid.Length || step >= words.Length)
                    break;
                if (!valid[step])
                    continue;

                Pg33MachineWord word = words[step];
                payload[2 * local] = word.High;
                payload[(2 * local) + 1] = word.Low;
                payload[PlaneASize + local] = CalculateBraw(word.High, word.Low);
            }

            return BuildResponse(payload);
        }

        internal static byte CalculateBraw(byte high, byte low)
        {
            int sum = (high >> 4)
                + (high & 0x0F)
                + (low >> 4)
                + (low & 0x0F);
            return (byte)(sum & 0x0F);
        }

        private static int CountContiguousFrom(
            Pg33MachineWord[] words,
            bool[] valid,
            int start,
            int highest)
        {
            if (start < 0) start = 0;
            int max = Math.Min(highest, Math.Min(words.Length, valid.Length) - 1);
            int count = 0;
            for (int i = start; i <= max; i++)
            {
                if (!valid[i]) break;
                count++;

                // END e fisicamente representado por HIGH=00 LOW=70.
                // O readback considera o programa concluido nesse passo.
                if (words[i].High == 0x00 && words[i].Low == 0x70)
                    break;
            }
            return count;
        }

        private static int DecodeStartStep(byte[] request)
        {
            if (request == null || request.Length < 6 || request[0] != 0x34)
                return 0;

            int start = (request[2] << 8) | request[3];
            if (start < 0 || start >= MaxProgramSteps)
                return 0;
            return start;
        }

        private static byte[] BuildFallback34(int startStep, byte[] fallbackPage0000)
        {
            byte[] payload = new byte[PayloadLength];
            if (startStep == 0 && fallbackPage0000 != null)
            {
                Buffer.BlockCopy(
                    fallbackPage0000,
                    0,
                    payload,
                    0,
                    Math.Min(payload.Length, fallbackPage0000.Length));
            }
            return BuildResponse(payload);
        }

        private static byte[] BuildResponse(byte[] payload)
        {
            if (payload == null) payload = new byte[0];
            if (payload.Length > 255)
                throw new ArgumentOutOfRangeException("payload");

            byte[] frame = new byte[payload.Length + 3];
            frame[0] = 0x00;
            frame[1] = (byte)payload.Length;
            if (payload.Length > 0)
                Buffer.BlockCopy(payload, 0, frame, 2, payload.Length);
            frame[frame.Length - 1] = Checksum(frame, frame.Length - 1);
            return frame;
        }

        private static byte Checksum(byte[] bytes, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++)
                sum = (sum + bytes[i]) & 0xFF;
            return (byte)((0xFF - sum) & 0xFF);
        }

        private static byte[] CloneOrEmpty(byte[] value)
        {
            if (value == null) return new byte[0];
            byte[] copy = new byte[value.Length];
            Buffer.BlockCopy(value, 0, copy, 0, value.Length);
            return copy;
        }
    }
}
