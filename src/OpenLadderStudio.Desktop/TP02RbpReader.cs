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
    /// Leitor de programa TP02 pela interface Computer Link documentada (RBP).
    /// Le em blocos de ate 100 passos e, no modo automatico, para ao encontrar F-00 END (007000).
    /// </summary>
    internal static class TP02RbpReaderProgram
    {
        private sealed class Options
        {
            public string Port = string.Empty;
            public string Output = string.Empty;
            public int Station = 1;
            public int ResponseCode = 5;
            public int Start;
            public int Count;
            public bool CountSpecified;
            public int Baud = 19200;
            public int DataBits = 7;
            public Parity Parity = Parity.None;
            public StopBits StopBits = StopBits.One;
            public int TimeoutMs = 2500;
        }

        private static int Main(string[] args)
        {
            try
            {
                Options options = Parse(args);
                if (string.IsNullOrEmpty(options.Port))
                {
                    PrintUsage();
                    return 2;
                }

                using (SerialPort port = new SerialPort(options.Port, options.Baud, options.Parity, options.DataBits, options.StopBits))
                {
                    port.Handshake = Handshake.None;
                    port.DtrEnable = false;
                    port.RtsEnable = false;
                    port.ReadTimeout = 100;
                    port.WriteTimeout = options.TimeoutMs;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    Console.WriteLine("OpenLadder - TP02 Computer Link RBP Reader");
                    Console.WriteLine(new string('=', 58));
                    Console.WriteLine("COM            : " + Describe(port));
                    Console.WriteLine("Estacao        : " + options.Station.ToString("00", CultureInfo.InvariantCulture));
                    Console.WriteLine("Resp. code     : " + options.ResponseCode.ToString("X1", CultureInfo.InvariantCulture));
                    Console.WriteLine("Inicio         : " + options.Start.ToString("0000", CultureInfo.InvariantCulture));
                    Console.WriteLine("Modo           : " + (options.CountSpecified
                        ? "COUNT=" + options.Count.ToString(CultureInfo.InvariantCulture)
                        : "ATE F-00 END (007000) OU PASSO 4000"));
                    Console.WriteLine();
                    Console.WriteLine("ATENCAO: Computer Link pela MMI exige PG/COM em LOW (pino 4 ligado ao pino 5).");
                    Console.WriteLine();

                    TryPrintState(port, options);

                    List<Tp02MachineWord> words = ReadProgram(port, options);
                    if (words.Count == 0)
                        throw new InvalidDataException("Nenhum passo de programa foi recebido.");

                    string outputPath = ResolveOutput(options);
                    SaveWords(outputPath, words);

                    Console.WriteLine();
                    Console.WriteLine("LEITURA CONCLUIDA");
                    Console.WriteLine("Passos         : " + words.Count.ToString(CultureInfo.InvariantCulture));
                    Console.WriteLine("Ultimo         : " + words[words.Count - 1].ToHex());
                    Console.WriteLine("Arquivo        : " + outputPath);
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERRO: " + ex.Message);
                return 1;
            }
        }

        private static void TryPrintState(SerialPort port, Options options)
        {
            try
            {
                string response = Exchange(port,
                    Tp02ComputerLinkProgramCodec.BuildPsr(options.Station, options.ResponseCode),
                    options.TimeoutMs, "PSR");
                Tp02ComputerLinkState state;
                if (Tp02ComputerLinkProgramCodec.TryGetPsrState(response, out state))
                    Console.WriteLine("Estado PLC     : " + state.ToString().ToUpperInvariant());
                else
                    Console.WriteLine("Estado PLC     : resposta PSR nao reconhecida; RBP ainda sera tentado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Estado PLC     : PSR indisponivel (" + ex.Message + "); RBP ainda sera tentado.");
            }
            Console.WriteLine();
        }

        private static List<Tp02MachineWord> ReadProgram(SerialPort port, Options options)
        {
            List<Tp02MachineWord> result = new List<Tp02MachineWord>();
            int address = options.Start;
            int remaining = options.CountSpecified ? options.Count : (Tp02ComputerLinkProgramCodec.MaxProgramAddress - options.Start + 1);
            bool endFound = false;
            int blockIndex = 0;

            while (remaining > 0 && address <= Tp02ComputerLinkProgramCodec.MaxProgramAddress)
            {
                int capacity = Tp02ComputerLinkProgramCodec.MaxProgramAddress - address + 1;
                int count = Math.Min(Tp02ComputerLinkProgramCodec.MaxStepsPerFrame, Math.Min(remaining, capacity));
                if (count <= 0) break;

                string frame = Tp02ComputerLinkProgramCodec.BuildRbp(
                    options.Station, address, count, options.ResponseCode);
                string response = Exchange(port, frame, options.TimeoutMs,
                    "RBP " + blockIndex.ToString(CultureInfo.InvariantCulture));

                Tp02ComputerLinkResponse parsed = Tp02ComputerLinkProgramCodec.ParseResponse(response, "RBP");
                if (!parsed.ChecksumOk || parsed.IsError || parsed.Command != "RBP")
                    throw new InvalidDataException("Resposta RBP invalida no bloco "
                        + blockIndex.ToString(CultureInfo.InvariantCulture)
                        + (parsed.IsError && !string.IsNullOrEmpty(parsed.ErrorCode) ? "; erro=" + parsed.ErrorCode : string.Empty));

                string data = NormalizeHex(parsed.Data);
                int expectedChars = count * 6;
                if (data.Length != expectedChars)
                    throw new InvalidDataException("RBP bloco " + blockIndex.ToString(CultureInfo.InvariantCulture)
                        + " retornou " + data.Length.ToString(CultureInfo.InvariantCulture)
                        + " caracteres hex; esperado=" + expectedChars.ToString(CultureInfo.InvariantCulture) + ".");

                int i;
                for (i = 0; i < data.Length; i += 6)
                {
                    string wordHex = data.Substring(i, 6);
                    List<Tp02MachineWord> one = Tp02ComputerLinkProgramCodec.ParseMachineHex(wordHex);
                    Tp02MachineWord word = one[0];
                    result.Add(word);

                    if (!options.CountSpecified && string.Equals(wordHex, "007000", StringComparison.OrdinalIgnoreCase))
                    {
                        endFound = true;
                        break;
                    }
                }

                Console.WriteLine("BLOCO OK       : addr=" + address.ToString("0000", CultureInfo.InvariantCulture)
                    + " passos=" + count.ToString(CultureInfo.InvariantCulture)
                    + (endFound ? "  END encontrado" : string.Empty));

                if (endFound) break;
                address += count;
                remaining -= count;
                blockIndex++;
            }

            if (!options.CountSpecified && !endFound)
                Console.WriteLine("AVISO          : F-00 END nao encontrado ate o limite solicitado/4000.");

            return result;
        }

        private static string Exchange(SerialPort port, string frame, int timeoutMs, string label)
        {
            port.DiscardInBuffer();
            Console.WriteLine("TX " + label.PadRight(10) + " " + Escape(frame));
            port.Write(frame);
            string response = ReadUntilCarriageReturn(port, timeoutMs);
            if (string.IsNullOrEmpty(response))
                throw new TimeoutException(label + ": nenhuma resposta do TP02.");
            Console.WriteLine("RX " + label.PadRight(10) + " " + Escape(response));
            return response;
        }

        private static string ReadUntilCarriageReturn(SerialPort port, int timeoutMs)
        {
            StringBuilder received = new StringBuilder();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int value;
                try { value = port.ReadByte(); }
                catch (TimeoutException) { continue; }
                if (value < 0) continue;
                received.Append((char)value);
                if (value == 13) return received.ToString();
            }
            return received.ToString();
        }

        private static void SaveWords(string path, IList<Tp02MachineWord> words)
        {
            StringBuilder text = new StringBuilder(words.Count * 8);
            int i;
            for (i = 0; i < words.Count; i++) text.AppendLine(words[i].ToHex());
            File.WriteAllText(path, text.ToString(), Encoding.ASCII);
        }

        private static string ResolveOutput(Options options)
        {
            if (!string.IsNullOrEmpty(options.Output)) return Path.GetFullPath(options.Output);
            string name = "TP02-program-st"
                + options.Station.ToString("00", CultureInfo.InvariantCulture)
                + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + ".hex";
            return Path.Combine(Environment.CurrentDirectory, name);
        }

        private static string NormalizeHex(string value)
        {
            StringBuilder sb = new StringBuilder();
            string text = value ?? string.Empty;
            int i;
            for (i = 0; i < text.Length; i++)
                if (Uri.IsHexDigit(text[i])) sb.Append(char.ToUpperInvariant(text[i]));
            return sb.ToString();
        }

        private static Options Parse(string[] args)
        {
            Options o = new Options();
            int i;
            for (i = 0; i < args.Length; i++)
            {
                string a = args[i] == null ? string.Empty : args[i].Trim();
                if (a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase)) { o.Port = a.Substring(7); continue; }
                if (a.StartsWith("--out=", StringComparison.OrdinalIgnoreCase)) { o.Output = a.Substring(6).Trim('"'); continue; }
                if (a.StartsWith("--station=", StringComparison.OrdinalIgnoreCase)) { o.Station = ParseInt(a.Substring(10), "station"); continue; }
                if (a.StartsWith("--response=", StringComparison.OrdinalIgnoreCase)) { o.ResponseCode = ParseHexInt(a.Substring(11), "response"); continue; }
                if (a.StartsWith("--start=", StringComparison.OrdinalIgnoreCase)) { o.Start = ParseInt(a.Substring(8), "start"); continue; }
                if (a.StartsWith("--count=", StringComparison.OrdinalIgnoreCase)) { o.Count = ParseInt(a.Substring(8), "count"); o.CountSpecified = true; continue; }
                if (a.StartsWith("--baud=", StringComparison.OrdinalIgnoreCase)) { o.Baud = ParseInt(a.Substring(7), "baud"); continue; }
                if (a.StartsWith("--databits=", StringComparison.OrdinalIgnoreCase)) { o.DataBits = ParseInt(a.Substring(11), "databits"); continue; }
                if (a.StartsWith("--timeout=", StringComparison.OrdinalIgnoreCase)) { o.TimeoutMs = ParseInt(a.Substring(10), "timeout"); continue; }
                if (a.StartsWith("--parity=", StringComparison.OrdinalIgnoreCase)) { o.Parity = (Parity)Enum.Parse(typeof(Parity), a.Substring(9), true); continue; }
                if (a.StartsWith("--stopbits=", StringComparison.OrdinalIgnoreCase))
                {
                    string s = a.Substring(11);
                    o.StopBits = s == "2" || s.Equals("Two", StringComparison.OrdinalIgnoreCase) ? StopBits.Two : StopBits.One;
                    continue;
                }
                if (!a.StartsWith("--", StringComparison.Ordinal) && string.IsNullOrEmpty(o.Port)) { o.Port = a; continue; }
                throw new ArgumentException("Argumento desconhecido: " + a);
            }

            if (o.Station < 1 || o.Station > 99) throw new ArgumentOutOfRangeException("station");
            if (o.ResponseCode < 0 || o.ResponseCode > 15) throw new ArgumentOutOfRangeException("response");
            if (o.Start < 0 || o.Start > Tp02ComputerLinkProgramCodec.MaxProgramAddress) throw new ArgumentOutOfRangeException("start");
            if (o.CountSpecified && (o.Count < 1 || o.Start + o.Count - 1 > Tp02ComputerLinkProgramCodec.MaxProgramAddress))
                throw new ArgumentOutOfRangeException("count");
            if (o.DataBits != 7 && o.DataBits != 8) throw new ArgumentOutOfRangeException("databits");
            if (o.TimeoutMs < 100 || o.TimeoutMs > 30000) throw new ArgumentOutOfRangeException("timeout");
            return o;
        }

        private static int ParseInt(string value, string name)
        {
            int n;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                throw new ArgumentException("Valor invalido para " + name + ": " + value);
            return n;
        }

        private static int ParseHexInt(string value, string name)
        {
            int n;
            if (!int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n))
                throw new ArgumentException("Valor hexadecimal invalido para " + name + ": " + value);
            return n;
        }

        private static string Describe(SerialPort port)
        {
            string parity = port.Parity == Parity.None ? "N" : port.Parity == Parity.Odd ? "O" : "E";
            return port.PortName + " " + port.BaudRate.ToString(CultureInfo.InvariantCulture)
                + " " + port.DataBits.ToString(CultureInfo.InvariantCulture) + parity
                + (port.StopBits == StopBits.Two ? "2" : "1");
        }

        private static string Escape(string frame)
        {
            return (frame ?? string.Empty).Replace("\r", "<CR>").Replace("\n", "<LF>");
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Uso:");
            Console.WriteLine("  OpenLadderTP02Rbp.exe COM3");
            Console.WriteLine("  OpenLadderTP02Rbp.exe --port=COM3 --out=programa.hex");
            Console.WriteLine("  OpenLadderTP02Rbp.exe --port=COM3 --start=0 --count=100");
            Console.WriteLine();
            Console.WriteLine("Sem --count, le a partir de --start ate encontrar F-00 END (007000) ou o passo 4000.");
            Console.WriteLine("Padrao Computer Link: station=1, response=5, 19200 7N1.");
        }
    }
}