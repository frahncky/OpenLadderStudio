using System;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Modelo OFFLINE do quadro 0x33 encontrado no caminho
    /// "Write PLC Program" do PC12.
    ///
    /// A geometria foi reconstruída estaticamente no pc12.exe e conferida
    /// executando o construtor original 0x004B7958 dentro do Unicorn, com a
    /// rotina TX interceptada antes de qualquer I/O.
    ///
    /// A análise posterior do helper 0x004BCA65 esclareceu a unidade do corpo:
    /// cada instrução lógica ocupa 1..4 PALAVRAS DE MÁQUINA e o helper acrescenta
    /// uma palavra HIGH/LOW/EXTERNAL para cada passo adicional. O limite de 20
    /// observado em +0x7A pertence às instruções lógicas do bloco, não às palavras.
    /// Portanto um bloco pode conter até 80 palavras de máquina.
    ///
    /// Para W palavras de máquina:
    ///   TX[0] = 0x33
    ///   TX[1] = 3*W + 4
    ///   TX[2] = 0x00
    ///   TX[3..4] = endereço inicial de passo do bloco
    ///   TX[5] = 2*W
    ///   corpo = 2*W bytes HIGH/LOW + W bytes EXTERNAL
    ///   checksum fecha a soma do quadro em 0xFF.
    ///
    /// O maior quadro possível no modelo reconstruído contém 80 palavras:
    ///   TX[1] = 0xF4, TX[5] = 0xA0, total = 247 bytes.
    ///
    /// IMPORTANTE: esta classe não abre serial, não transmite quadros e não é
    /// ligada a nenhuma rotina de download. Serve somente para dry-run/testes.
    /// </summary>
    internal static class Tp02Pg33DryRunFrame
    {
        internal const int MaxMachineWordsPerFrame = 80;
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

            int machineWords = highLowPlane.Length / 2;
            if (machineWords < 1 || machineWords > MaxMachineWordsPerFrame)
                throw new ArgumentOutOfRangeException("highLowPlane", "Quadro candidato PG33 aceita 1..80 palavras de máquina.");
            if (externalPlane.Length != machineWords)
                throw new ArgumentException("Plano EXTERNAL deve conter exatamente 1 byte por palavra de máquina.", "externalPlane");

            // O quadro bruto recebe um endereço inicial e os planos já expandidos.
            // A regra de no máximo 20 instruções lógicas por bloco pertence ao
            // orquestrador, que também conhece os spans de 1..4 passos.
            int bodyLength = highLowPlane.Length + externalPlane.Length; // 3*W
            int bytesAfterLengthBeforeChecksum = bodyLength + 4;         // 3*W + 4
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

            int highLowBytes = frame[5];
            if (highLowBytes == 0 || (highLowBytes & 1) != 0) return false;
            int machineWords = highLowBytes / 2;
            if (machineWords < 1 || machineWords > MaxMachineWordsPerFrame) return false;

            int expectedAfterLength = (3 * machineWords) + 4;
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
