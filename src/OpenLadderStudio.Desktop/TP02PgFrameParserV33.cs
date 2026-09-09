using System;
using System.Globalization;

namespace ModernPC12
{
    internal static class TP02PgFrameParserV33
    {
        internal sealed class ParseResult
        {
            public byte[] Raw = new byte[0];
            public byte[] WithoutEcho = new byte[0];
            public byte[] Frame = new byte[0];
            public int EchoCount;
            public int RawSum;
            public int WithoutEchoSum;
            public string Detail = string.Empty;
            public bool IsValid;
        }

        public static ParseResult Parse(byte[] raw, byte[] transmitted)
        {
            ParseResult result = new ParseResult();
            result.Raw = raw == null ? new byte[0] : (byte[])raw.Clone();
            result.RawSum = Sum8(result.Raw);

            int offset = 0;
            if (transmitted != null && transmitted.Length > 0)
            {
                while (StartsWithAt(result.Raw, transmitted, offset))
                {
                    offset += transmitted.Length;
                    result.EchoCount++;
                }
            }

            result.WithoutEcho = Slice(result.Raw, offset, result.Raw.Length - offset);
            result.WithoutEchoSum = Sum8(result.WithoutEcho);

            if (result.WithoutEcho.Length >= 4 && result.WithoutEchoSum == 0xFF)
            {
                result.Frame = result.WithoutEcho;
                result.IsValid = true;
                result.Detail = "quadro completo sem eco; checksum FF";
                return result;
            }

            int bestStart = -1;
            int bestLength = -1;
            for (int start = 0; start < result.WithoutEcho.Length; start++)
            {
                int sum = 0;
                for (int end = start; end < result.WithoutEcho.Length; end++)
                {
                    sum = (sum + result.WithoutEcho[end]) & 0xFF;
                    int length = end - start + 1;
                    if (length >= 4 && sum == 0xFF && length > bestLength)
                    {
                        bestStart = start;
                        bestLength = length;
                    }
                }
            }

            if (bestStart >= 0)
            {
                result.Frame = Slice(result.WithoutEcho, bestStart, bestLength);
                result.IsValid = true;
                result.Detail = "subquadro contiguo isolado; checksum FF; inicio="
                    + bestStart.ToString(CultureInfo.InvariantCulture)
                    + "; tamanho=" + bestLength.ToString(CultureInfo.InvariantCulture);
                return result;
            }

            result.Detail = "nenhum quadro contiguo >=4 bytes com checksum FF";
            return result;
        }

        public static int Sum8(byte[] bytes)
        {
            int sum = 0;
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++) sum = (sum + bytes[i]) & 0xFF;
            }
            return sum;
        }

        private static bool StartsWithAt(byte[] source, byte[] pattern, int offset)
        {
            if (source == null || pattern == null || offset < 0) return false;
            if (offset + pattern.Length > source.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (source[offset + i] != pattern[i]) return false;
            }
            return true;
        }

        private static byte[] Slice(byte[] source, int offset, int count)
        {
            if (source == null || count <= 0 || offset < 0 || offset >= source.Length) return new byte[0];
            if (offset + count > source.Length) count = source.Length - offset;
            byte[] output = new byte[count];
            Buffer.BlockCopy(source, offset, output, 0, count);
            return output;
        }
    }
}
