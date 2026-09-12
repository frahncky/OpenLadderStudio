using System;

namespace OpenLadderStudio.Core
{
    /// <summary>Quadros curtos PG reconstruídos do PC12 v2.1. Não realiza I/O.</summary>
    internal static class Tp02PgProtocol
    {
        internal static readonly byte[] ProgramMode = BuildShortFrame(0x01);
        internal static readonly byte[] Run = BuildShortFrame(0x02);
        internal static readonly byte[] Candidate03 = BuildShortFrame(0x03);
        internal static readonly byte[] Candidate04 = BuildShortFrame(0x04);
        internal static readonly byte[] ClearAllMemory = BuildShortFrame(0x0F);
        internal static readonly byte[] Candidate11 = BuildShortFrame(0x11);
        internal static readonly byte[] Presence = BuildShortFrame(0xF0);

        internal static byte[] BuildShortFrame(byte command)
        {
            byte[] prefix = new byte[] { command, 0x00 };
            return new byte[] { command, 0x00, Checksum(prefix, prefix.Length) };
        }

        internal static byte Checksum(byte[] bytes, int count)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (count < 0 || count > bytes.Length) throw new ArgumentOutOfRangeException("count");
            int sum = 0;
            for (int i = 0; i < count; i++) sum = (sum + bytes[i]) & 0xFF;
            return (byte)((0xFF - sum) & 0xFF);
        }

        internal static bool HasValidChecksum(byte[] frame)
        {
            if (frame == null || frame.Length == 0) return false;
            int sum = 0;
            for (int i = 0; i < frame.Length; i++) sum = (sum + frame[i]) & 0xFF;
            return sum == 0xFF;
        }

        internal static bool IsBlocked(byte[] frame)
        {
            return Equal(frame, ClearAllMemory) || Equal(frame, ProgramMode)
                || Equal(frame, Candidate03) || Equal(frame, Candidate04)
                || Equal(frame, Candidate11);
        }

        internal static byte[] Copy(byte[] frame)
        {
            if (frame == null) throw new ArgumentNullException("frame");
            return (byte[])frame.Clone();
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
