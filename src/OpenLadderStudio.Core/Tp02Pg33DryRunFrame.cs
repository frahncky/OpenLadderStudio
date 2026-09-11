using System;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Modelo OFFLINE do quadro 0x33 encontrado no caminho
    /// "Write PLC Program" do PC12.
    ///
    /// A geometria foi primeiro reconstruída estaticamente no pc12.exe e depois
    /// conferida executando o construtor original 0x004B7958 dentro do Unicorn,
    /// com a rotina TX interceptada antes de qualquer I/O. Os casos de 1, 2, 3
    /// e 20 registros produziram, byte a byte, o mesmo quadro deste modelo.
    /// Isso é confirmação dinâmica OFFLINE do construtor do PC12, não confirmação
    /// física do comando em um PLC TP02.
    ///
    /// Formato confirmado no construtor:
    ///   TX[0] = 0x33
    ///   TX[1] = 3*N + 4
    ///   TX[2] = 0x00
    ///   TX[3..4] = endereço inicial de passo do primeiro registro do bloco
    ///   TX[5] = 2*N
    ///   corpo = 2*N bytes HIGH/LOW + N bytes EXTERNAL
    ///   checksum fecha a soma do quadro em 0xFF.
    ///
    /// N é o número de REGISTROS de código de máquina acumulados no bloco,
    /// não necessariamente a diferença entre endereços de passo. O PC12
    /// incrementa separadamente o cursor de passo em 1..4 conforme a instrução
    /// e encerra a coleta do bloco quando o contador de registros chega a 20.
    /// A transição de sucesso também foi emulada offline e inicia o próximo bloco
    /// no cursor real de passos, não em startStep + N.
    ///
    /// IMPORTANTE: esta classe não abre serial, não transmite quadros e não é
    /// ligada a nenhuma rotina de download. Serve somente para dry-run/testes.
    /// </summary>
    internal static class Tp02Pg33DryRunFrame
    {
        internal const int MaxRecordsPerFrame = 20;
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

            int records = highLowPlane.Length / 2;
            if (records < 1 || records > MaxRecordsPerFrame)
                throw new ArgumentOutOfRangeException("highLowPlane", "Quadro candidato PG33 aceita 1..20 registros de código de máquina.");
            if (externalPlane.Length != records)
                throw new ArgumentException("Plano EXTERNAL deve conter exatamente 1 byte por registro.", "externalPlane");

            // Não validar startStep + records: instruções do TP02 podem consumir
            // 1..4 passos de endereço por registro. O span real pertence ao
            // empacotador/orquestrador, não ao formato bruto deste quadro.
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

            int recordsTimes2 = frame[5];
            if (recordsTimes2 == 0 || (recordsTimes2 & 1) != 0) return false;
            int records = recordsTimes2 / 2;
            if (records < 1 || records > MaxRecordsPerFrame) return false;

            int expectedAfterLength = (3 * records) + 4;
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
