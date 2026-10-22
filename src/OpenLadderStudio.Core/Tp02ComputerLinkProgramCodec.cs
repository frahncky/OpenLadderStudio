using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenLadderStudio.Core
{
    internal enum Tp02ComputerLinkState
    {
        Unknown = -1,
        Stop = 0,
        Run = 1,
        Error = 2
    }

    internal sealed class Tp02ComputerLinkResponse
    {
        public bool ChecksumOk;
        public bool IsError;
        public string ErrorCode = string.Empty;
        public string Command = string.Empty;
        public string Data = string.Empty;
        public string Clean = string.Empty;
    }

    /// <summary>
    /// Codec do protocolo Computer Link/Host Protocol documentado do WEG TP02.
    ///
    /// Esta classe nao abre porta serial. Ela apenas monta e valida quadros ASCII.
    /// O formato de comunicacao documentado usa dois ':' consecutivos no inicio e CR no final.
    /// O checksum e o complemento de dois da soma ASCII do corpo, de modo que
    /// corpo + dois digitos de checksum fecha modulo 256 em zero.
    /// </summary>
    internal static class Tp02ComputerLinkProgramCodec
    {
        public const int MaxProgramAddress = 4000;
        public const int MaxStepsPerFrame = 100;

        public static string ChecksumAscii(string body)
        {
            if (body == null) throw new ArgumentNullException("body");
            int sum = 0;
            int i;
            for (i = 0; i < body.Length; i++) sum = (sum + (byte)body[i]) & 0xFF;
            return ((-sum) & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
        }

        public static string BuildFrame(int station, int responseCode, string command, string data)
        {
            if (station < 0 || station > 99) throw new ArgumentOutOfRangeException("station");
            if (responseCode < 0 || responseCode > 15) throw new ArgumentOutOfRangeException("responseCode");
            if (string.IsNullOrEmpty(command) || command.Length != 3)
                throw new ArgumentException("Comando TP02 deve ter tres caracteres.", "command");

            string body = station.ToString("00", CultureInfo.InvariantCulture)
                + "?"
                + responseCode.ToString("X1", CultureInfo.InvariantCulture)
                + command.ToUpperInvariant()
                + (data ?? string.Empty);

            return "::" + body + ChecksumAscii(body) + "\r";
        }

        public static string BuildPsr(int station, int responseCode)
        {
            return BuildFrame(station, responseCode, "PSR", string.Empty);
        }

        public static string BuildRbp(int station, int start, int count, int responseCode)
        {
            ValidateProgramRange(start, count);
            string countField = CountField(count);
            return BuildFrame(station, responseCode, "RBP",
                start.ToString("0000", CultureInfo.InvariantCulture) + countField);
        }

        public static string BuildWbp(int station, int start, IList<Tp02MachineWord> words, int responseCode)
        {
            if (words == null) throw new ArgumentNullException("words");
            ValidateProgramRange(start, words.Count);

            StringBuilder data = new StringBuilder(6 + words.Count * 6);
            data.Append(start.ToString("0000", CultureInfo.InvariantCulture));
            data.Append(CountField(words.Count));
            int i;
            for (i = 0; i < words.Count; i++) data.Append(words[i].ToHex());
            return BuildFrame(station, responseCode, "WBP", data.ToString());
        }

        public static List<string> BuildWbpProgramFrames(int station, int start, IList<Tp02MachineWord> words, int responseCode)
        {
            if (words == null) throw new ArgumentNullException("words");
            if (words.Count < 1) throw new ArgumentOutOfRangeException("words", "Programa sem passos.");
            if (start < 0 || start > MaxProgramAddress) throw new ArgumentOutOfRangeException("start");
            if (start + words.Count - 1 > MaxProgramAddress)
                throw new ArgumentOutOfRangeException("words", "Programa ultrapassa o passo 4000.");

            List<string> frames = new List<string>();
            int offset = 0;
            while (offset < words.Count)
            {
                int count = Math.Min(MaxStepsPerFrame, words.Count - offset);
                List<Tp02MachineWord> block = new List<Tp02MachineWord>(count);
                int i;
                for (i = 0; i < count; i++) block.Add(words[offset + i]);
                frames.Add(BuildWbp(station, start + offset, block, responseCode));
                offset += count;
            }
            return frames;
        }

        public static List<Tp02MachineWord> ParseMachineHex(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            StringBuilder hex = new StringBuilder(text.Length);
            int i;
            for (i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (Uri.IsHexDigit(c)) hex.Append(char.ToUpperInvariant(c));
                else if (char.IsWhiteSpace(c) || c == ',' || c == ';' || c == '-' || c == ':') { }
                else throw new FormatException("Caractere invalido no codigo de maquina: '" + c + "'.");
            }

            if (hex.Length == 0 || (hex.Length % 6) != 0)
                throw new FormatException("Codigo de maquina deve conter multiplos de 6 hex (3 bytes por passo).");

            List<Tp02MachineWord> words = new List<Tp02MachineWord>(hex.Length / 6);
            for (i = 0; i < hex.Length; i += 6)
            {
                byte high = byte.Parse(hex.ToString(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte low = byte.Parse(hex.ToString(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte external = byte.Parse(hex.ToString(i + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                words.Add(new Tp02MachineWord(high, low, external));
            }
            return words;
        }

        public static Tp02ComputerLinkResponse ParseResponse(string frame, string expectedCommand)
        {
            Tp02ComputerLinkResponse result = new Tp02ComputerLinkResponse();
            string clean = (frame ?? string.Empty).TrimEnd('\r', '\n');
            while (clean.StartsWith(":")) clean = clean.Substring(1);
            result.Clean = clean;

            if (clean.Length < 6) return result;
            result.ChecksumOk = VerifyChecksum(clean);

            int percent = clean.IndexOf('%');
            if (percent >= 0)
            {
                result.IsError = true;
                if (clean.Length >= 4) result.ErrorCode = clean.Substring(clean.Length - 4, 2).ToUpperInvariant();
                return result;
            }

            int marker = clean.IndexOf('#');
            if (marker < 0) return result;

            string command = (expectedCommand ?? string.Empty).ToUpperInvariant();
            int commandIndex = command.Length == 0
                ? -1
                : clean.IndexOf(command, marker, StringComparison.OrdinalIgnoreCase);
            if (commandIndex < 0) return result;

            result.Command = command;
            int dataStart = commandIndex + command.Length;
            int dataLength = clean.Length - dataStart - 2;
            if (dataLength < 0) dataLength = 0;
            result.Data = clean.Substring(dataStart, dataLength);
            return result;
        }

        public static bool TryGetPsrState(string frame, out Tp02ComputerLinkState state)
        {
            state = Tp02ComputerLinkState.Unknown;
            Tp02ComputerLinkResponse response = ParseResponse(frame, "PSR");
            if (!response.ChecksumOk || response.IsError || response.Command != "PSR" || response.Data.Length < 1)
                return false;

            if (response.Data[0] == '0') state = Tp02ComputerLinkState.Stop;
            else if (response.Data[0] == '1') state = Tp02ComputerLinkState.Run;
            else if (response.Data[0] == '2') state = Tp02ComputerLinkState.Error;
            else return false;
            return true;
        }

        public static bool IsSuccessfulResponse(string frame, string command, out string errorCode)
        {
            Tp02ComputerLinkResponse response = ParseResponse(frame, command);
            errorCode = response.ErrorCode;
            return response.ChecksumOk && !response.IsError && response.Command == (command ?? string.Empty).ToUpperInvariant();
        }

        private static string CountField(int count)
        {
            if (count < 1 || count > MaxStepsPerFrame) throw new ArgumentOutOfRangeException("count");
            return count == 100 ? "00" : count.ToString("00", CultureInfo.InvariantCulture);
        }

        private static void ValidateProgramRange(int start, int count)
        {
            if (start < 0 || start > MaxProgramAddress) throw new ArgumentOutOfRangeException("start");
            if (count < 1 || count > MaxStepsPerFrame) throw new ArgumentOutOfRangeException("count");
            if (start + count - 1 > MaxProgramAddress)
                throw new ArgumentOutOfRangeException("count", "Bloco ultrapassa o passo 4000.");
        }

        private static bool VerifyChecksum(string clean)
        {
            if (string.IsNullOrEmpty(clean) || clean.Length < 3) return false;
            int checksum;
            if (!int.TryParse(clean.Substring(clean.Length - 2, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out checksum)) return false;

            int sum = 0;
            int i;
            for (i = 0; i < clean.Length - 2; i++) sum = (sum + (byte)clean[i]) & 0xFF;
            return ((sum + checksum) & 0xFF) == 0;
        }
    }
}