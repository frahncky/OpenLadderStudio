using System;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Geometria de paginação do Read PLC Program (comando 34).
    ///
    /// O PC12 monta 34 03 [step_hi] [step_lo] A0 chk e usa o campo de
    /// 16 bits como contador inicial de passos do programa. A resposta física
    /// conhecida tem LEN=F0, Região A de 160 bytes (80 pares HIGH/LOW) e
    /// Região B de 80 bytes. Por isso cada página cobre 80 passos físicos.
    ///
    /// Esta classe somente constrói/valida quadros de LEITURA. Não contém
    /// qualquer primitiva de escrita, download, erase ou RUN/STOP remoto.
    /// </summary>
    internal static class Tp02Pg34Pager
    {
        internal const int PayloadLength = 0xF0;
        internal const int PlaneASize = 0xA0;
        internal const int StepsPerPage = 80;
        internal const int MaxProgramSteps = 4000;

        internal static byte[] BuildReadRequest(int startStep)
        {
            if (startStep < 0 || startStep >= MaxProgramSteps)
                throw new ArgumentOutOfRangeException("startStep");

            byte hi = (byte)((startStep >> 8) & 0xFF);
            byte lo = (byte)(startStep & 0xFF);
            byte[] frame = new byte[] { 0x34, 0x03, hi, lo, 0xA0, 0x00 };
            frame[5] = Checksum(frame, 5);
            return frame;
        }

        internal static int NextStartStep(int startStep)
        {
            if (startStep < 0 || startStep >= MaxProgramSteps)
                throw new ArgumentOutOfRangeException("startStep");

            int next = startStep + StepsPerPage;
            return next > MaxProgramSteps ? MaxProgramSteps : next;
        }

        internal static bool IsValidPageFrame(byte[] frame)
        {
            if (frame == null || frame.Length != PayloadLength + 3) return false;
            if (frame[1] != PayloadLength) return false;
            return Sum8(frame) == 0xFF;
        }

        /// <summary>
        /// Procura F-00 END (00 70) nos 80 pares HIGH/LOW da página.
        /// Retorna o índice local 0..79. O índice global é baseStep+localStep.
        /// </summary>
        internal static bool TryFindEnd(byte[] frame, out int localStep)
        {
            localStep = -1;
            if (!IsValidPageFrame(frame)) return false;

            int payloadOffset = 2;
            for (int i = 0; i < StepsPerPage; i++)
            {
                byte high = frame[payloadOffset + (2 * i)];
                byte low = frame[payloadOffset + (2 * i) + 1];
                if (high == 0x00 && low == 0x70)
                {
                    localStep = i;
                    return true;
                }
            }
            return false;
        }

        internal static int GlobalStep(int baseStep, int localStep)
        {
            if (baseStep < 0 || baseStep >= MaxProgramSteps)
                throw new ArgumentOutOfRangeException("baseStep");
            if (localStep < 0 || localStep >= StepsPerPage)
                throw new ArgumentOutOfRangeException("localStep");
            return baseStep + localStep;
        }

        private static byte Checksum(byte[] bytes, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum = (sum + bytes[i]) & 0xFF;
            return (byte)((0xFF - sum) & 0xFF);
        }

        private static int Sum8(byte[] bytes)
        {
            int sum = 0;
            if (bytes != null)
                for (int i = 0; i < bytes.Length; i++) sum = (sum + bytes[i]) & 0xFF;
            return sum;
        }
    }
}
