using System;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Reconstrução OFFLINE do quadro 0x33 encontrado no caminho
    /// "Write PLC Program" do PC12.
    ///
    /// Evidência estática no pc12.exe:
    ///   TX[0] = 0x33
    ///   TX[1] = 3*N + 4
    ///   TX[2] = 0x00
    ///   TX[3..4] = endereço inicial de passo
    ///   TX[5] = 2*N
    ///   corpo = 2*N bytes do plano HIGH/LOW + N bytes externos
    ///   checksum fecha a soma do quadro em 0xFF.
    ///
    /// A geometria de 3 bytes por passo é compatível com a representação
    /// HIGH/LOW/external já recuperada do comando 34. A identificação de 0x33
    /// como escrita de programa é, neste ponto, uma reconstrução estática forte,
    /// ainda NÃO uma confirmação física de bancada.
    ///
    /// IMPORTANTE: esta classe não abre serial, não transmite quadros e não é
    /// ligada a nenhuma rotina de download. Serve somente para dry-run/testes.
    /// </summary>
    internal static class Tp02Pg33DryRunFrame
    {
        internal const int MaxStepsPerFrame = 80;
        internal const int MaxProgramSteps = 4000;
        internal const byte Command = 0x33;

        internal static byte[] BuildCandidate(
            int startStep,
            byte[] highLowPlane,
            byte[] externalPlane)
        {
            if (startStep < 0 || startStep >= MaxProgramSteps)
                throw new ArgumentOutOfRangeException("startStep");
            if (highLowPlane == null)
                throw new ArgumentNullException("highLowPlane");
            if (externalPlane == null)
                throw new ArgumentNullException("externalPlane");
            if ((highLowPlane.Length & 1) != 0)
                throw new ArgumentException("Plano HIGH/LOW deve ter quantidade par de bytes.", "highLowPlane");

            int steps = highLowPlane.Length / 2;
            if (steps < 1 || steps > MaxStepsPerFrame)
                throw new ArgumentOutOfRangeException("highLowPlane", "Quadro candidato PG33 aceita 1..80 passos.");
            if (externalPlane.Length != steps)
                throw new ArgumentException("Plano externo deve conter exatamente 1 byte por passo.", "externalPlane");
            if (startStep + steps > MaxProgramSteps)
                throw new ArgumentOutOfRangeException("startStep", "Quadro ultrapassa o limite de 4000 passos.");

            int bodyLength = highLowPlane.Length + externalPlane.Length; // 3*N
            int bytesAfterLengthBeforeChecksum = bodyLength + 4;         // 3*N + 4
            int frameLength = 2 + bytesAfterLengthBeforeChecksum + 1;    // cmd,len,...,chk

            byte[] frame = new byte[frameLength];
            frame[0] = Command;
            frame[1] = checked((byte)bytesAfterLengthBeforeChecksum);
            frame[2] = 0x00;
            frame[3] = (byte)((startStep >> 8) & 0xFF);
            frame[4] = (byte)(startStep & 0xFF);
            frame[5] = checked((byte)highLowPlane.Length);

            Buffer.BlockCopy(highLowPlane, 0, frame, 6, highLowPlane.Length);
            Buffer.BlockCopy(externalPlane, 0, frame, 6 + highLowPlane.Length, externalPlane.Length);
            frame[frame.Length - 1] = Checksum(frame, frame.Length - 1);
            return frame;
        }

        internal static bool HasValidChecksum(byte[] frame)
        {
            if (frame == null || frame.Length < 10) return false;
            int sum = 0;
            for (int i = 0; i < frame.Length; i++) sum = (sum + frame[i]) & 0xFF;
            return sum == 0xFF;
        }

        internal static bool HasCandidateGeometry(byte[] frame)
        {
            if (!HasValidChecksum(frame)) return false;
            if (frame[0] != Command || frame[2] != 0x00) return false;

            int stepsTimes2 = frame[5];
            if (stepsTimes2 == 0 || (stepsTimes2 & 1) != 0) return false;
            int steps = stepsTimes2 / 2;
            if (steps < 1 || steps > MaxStepsPerFrame) return false;

            int expectedAfterLength = (3 * steps) + 4;
            if (frame[1] != expectedAfterLength) return false;
            return frame.Length == expectedAfterLength + 3;
        }

        private static byte Checksum(byte[] bytes, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum = (sum + bytes[i]) & 0xFF;
            return (byte)((0xFF - sum) & 0xFF);
        }
    }
}
