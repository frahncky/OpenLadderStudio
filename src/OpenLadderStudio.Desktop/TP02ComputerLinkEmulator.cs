using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    /// <summary>
    /// Emulador minimo do TP02 Computer Link para teste fim a fim de PSR/RBP/WBP.
    /// Use somente com um par de portas COM virtuais.
    /// </summary>
    internal static class TP02ComputerLinkEmulatorProgram
    {
        private static readonly Tp02MachineWord[] Program = new Tp02MachineWord[Tp02ComputerLinkProgramCodec.MaxProgramAddress + 1];
        private static int station = 1;
        private static string memoryPath = string.Empty;

        private static int Main(string[] args)
        {
            try
            {
                string portName = string.Empty;
                int i;
                for (i = 0; i < args.Length; i++)
                {
                    string a = args[i] == null ? string.Empty : args[i].Trim();
                    if (a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase)) { portName = a.Substring(7); continue; }
                    if (a.StartsWith("--station=", StringComparison.OrdinalIgnoreCase))
                    {
                        station = int.Parse(a.Substring(10), CultureInfo.InvariantCulture);
                        continue;
                    }
                    if (a.StartsWith("--memory=", StringComparison.OrdinalIgnoreCase)) { memoryPath = a.Substring(9).Trim('"'); continue; }
                    if (!a.StartsWith("--", StringComparison.Ordinal) && string.IsNullOrEmpty(portName)) { portName = a; continue; }
                    throw new ArgumentException("Argumento desconhecido: " + a);
                }

                if (string.IsNullOrEmpty(portName))
                {
                    Console.WriteLine("Uso: OpenLadderTP02HostEmulator.exe COM11 [--station=1] [--memory=arquivo.hex]");
                    return 2;
                }
                if (station < 1 || station > 99) throw new ArgumentOutOfRangeException("station");

                InitializeProgram();
                LoadMemoryIfPresent();

                Console.WriteLine("OpenLadder - TP02 Computer Link Emulator");
                Console.WriteLine(new string('=', 58));
                Console.WriteLine("Porta          : " + portName);
                Console.WriteLine("Serial         : 19200 7N1");
                Console.WriteLine("Estacao        : " + station.ToString("00", CultureInfo.InvariantCulture));
                Console.WriteLine("Estado         : STOP/PROGRAM");
                Console.WriteLine("Comandos       : PSR, RBP, WBP");
                Console.WriteLine("ATENCAO        : use COM virtual; nunca conecte este emulador ao PLC fisico.");
                Console.WriteLine();

                using (SerialPort port = new SerialPort(portName, 19200, Parity.None, 7, StopBits.One))
                {
                    port.Handshake = Handshake.None;
                    port.DtrEnable = false;
                    port.RtsEnable = false;
                    port.ReadTimeout = 200;
                    port.WriteTimeout = 1000;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    Console.WriteLine("Aguardando cliente. Ctrl+C encerra.");
                    while (true)
                    {
                        string request = ReadFrame(port);
                        if (string.IsNullOrEmpty(request)) continue;
                        Console.WriteLine("RX " + Escape(request));

                        string response = Handle(request);
                        if (string.IsNullOrEmpty(response))
                        {
                            Console.WriteLine("   sem resposta");
                            continue;
                        }

                        port.Write(response);
                        Console.WriteLine("TX " + Escape(response));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERRO: " + ex.Message);
                return 1;
            }
        }

        private static void InitializeProgram()
        {
            int i;
            for (i = 0; i < Program.Length; i++) Program[i] = new Tp02MachineWord(0x00, 0x00, 0x00);
            Program[0] = new Tp02MachineWord(0x00, 0x10, 0x00); // STR X0001
            Program[1] = new Tp02MachineWord(0x20, 0x40, 0x00); // OUT Y0001
            Program[2] = new Tp02MachineWord(0x00, 0x70, 0x00); // F-00 END
        }

        private static void LoadMemoryIfPresent()
        {
            if (string.IsNullOrEmpty(memoryPath))
                memoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tp02-host-emulator-memory.hex");
            else
                memoryPath = Path.GetFullPath(memoryPath);

            if (!File.Exists(memoryPath))
            {
                PersistMemory();
                return;
            }

            string text = File.ReadAllText(memoryPath, Encoding.ASCII);
            List<Tp02MachineWord> words = Tp02ComputerLinkProgramCodec.ParseMachineHex(text);
            int count = Math.Min(words.Count, Program.Length);
            int i;
            for (i = 0; i < count; i++) Program[i] = words[i];
        }

        private static string ReadFrame(SerialPort port)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                int value;
                try { value = port.ReadByte(); }
                catch (TimeoutException)
                {
                    if (sb.Length == 0) return string.Empty;
                    continue;
                }
                if (value < 0) continue;
                sb.Append((char)value);
                if (value == 13) return sb.ToString();
            }
        }

        private static string Handle(string frame)
        {
            string clean = (frame ?? string.Empty).TrimEnd('\r', '\n');
            if (!clean.StartsWith("::", StringComparison.Ordinal)) return string.Empty;
            clean = clean.Substring(2);
            if (clean.Length < 9) return string.Empty;
            if (!VerifyChecksum(clean)) return BuildError(ExtractResponseCode(clean), "???", "03");

            int requestStation;
            if (!int.TryParse(clean.Substring(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out requestStation))
                return string.Empty;
            if (requestStation != station && requestStation != 0) return string.Empty;
            if (clean[2] != '?') return string.Empty;

            char responseCode = clean[3];
            string command = clean.Substring(4, 3).ToUpperInvariant();
            string payload = clean.Substring(7, clean.Length - 9);

            if (command == "PSR")
                return BuildResponse(responseCode, command, "0");

            if (command == "RBP")
                return HandleRbp(responseCode, payload);

            if (command == "WBP")
                return HandleWbp(responseCode, payload);

            return BuildError(responseCode, command, "01");
        }

        private static string HandleRbp(char responseCode, string payload)
        {
            if (payload.Length != 6) return BuildError(responseCode, "RBP", "01");
            int address;
            int count;
            if (!TryParseAddressCount(payload, out address, out count)) return BuildError(responseCode, "RBP", "01");
            if (address < 0 || address + count - 1 >= Program.Length) return BuildError(responseCode, "RBP", "04");

            StringBuilder data = new StringBuilder(count * 6);
            int i;
            for (i = 0; i < count; i++) data.Append(Program[address + i].ToHex());
            return BuildResponse(responseCode, "RBP", data.ToString());
        }

        private static string HandleWbp(char responseCode, string payload)
        {
            if (payload.Length < 12) return BuildError(responseCode, "WBP", "01");
            int address;
            int count;
            if (!TryParseAddressCount(payload.Substring(0, 6), out address, out count)) return BuildError(responseCode, "WBP", "01");
            if (address < 0 || address + count - 1 >= Program.Length) return BuildError(responseCode, "WBP", "04");

            string machine = payload.Substring(6);
            if (machine.Length != count * 6) return BuildError(responseCode, "WBP", "01");

            List<Tp02MachineWord> words;
            try { words = Tp02ComputerLinkProgramCodec.ParseMachineHex(machine); }
            catch { return BuildError(responseCode, "WBP", "01"); }
            if (words.Count != count) return BuildError(responseCode, "WBP", "01");

            int i;
            for (i = 0; i < count; i++) Program[address + i] = words[i];
            PersistMemory();
            Console.WriteLine("   WBP gravou addr=" + address.ToString("0000", CultureInfo.InvariantCulture)
                + " passos=" + count.ToString(CultureInfo.InvariantCulture));
            return BuildResponse(responseCode, "WBP", string.Empty);
        }

        private static bool TryParseAddressCount(string payload, out int address, out int count)
        {
            address = 0;
            count = 0;
            if (payload == null || payload.Length != 6) return false;
            if (!int.TryParse(payload.Substring(0, 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out address)) return false;
            int encodedCount;
            if (!int.TryParse(payload.Substring(4, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out encodedCount)) return false;
            count = encodedCount == 0 ? 100 : encodedCount;
            return count >= 1 && count <= 100;
        }

        private static string BuildResponse(char responseCode, string command, string data)
        {
            string body = station.ToString("00", CultureInfo.InvariantCulture)
                + "#" + responseCode + command + (data ?? string.Empty);
            return "::" + body + Tp02ComputerLinkProgramCodec.ChecksumAscii(body) + "\r";
        }

        private static string BuildError(char responseCode, string command, string errorCode)
        {
            string cmd = string.IsNullOrEmpty(command) || command.Length != 3 ? "ERR" : command.ToUpperInvariant();
            string body = station.ToString("00", CultureInfo.InvariantCulture)
                + "%" + responseCode + cmd + errorCode;
            return "::" + body + Tp02ComputerLinkProgramCodec.ChecksumAscii(body) + "\r";
        }

        private static char ExtractResponseCode(string clean)
        {
            return clean != null && clean.Length > 3 ? clean[3] : '5';
        }

        private static bool VerifyChecksum(string clean)
        {
            if (string.IsNullOrEmpty(clean) || clean.Length < 3) return false;
            int checksum;
            if (!int.TryParse(clean.Substring(clean.Length - 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out checksum)) return false;
            int sum = 0;
            int i;
            for (i = 0; i < clean.Length - 2; i++) sum = (sum + (byte)clean[i]) & 0xFF;
            return ((sum + checksum) & 0xFF) == 0;
        }

        private static void PersistMemory()
        {
            StringBuilder text = new StringBuilder(Program.Length * 8);
            int i;
            for (i = 0; i < Program.Length; i++) text.AppendLine(Program[i].ToHex());
            File.WriteAllText(memoryPath, text.ToString(), Encoding.ASCII);
        }

        private static string Escape(string frame)
        {
            return (frame ?? string.Empty).Replace("\r", "<CR>").Replace("\n", "<LF>");
        }
    }
}