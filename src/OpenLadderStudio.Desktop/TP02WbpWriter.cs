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
    /// Gravador de programa TP02 pela interface Computer Link documentada (WBP/RBP).
    ///
    /// Padrao e DRY-RUN. A transmissao real exige --write, PSR valido em STOP e
    /// leitura RBP de volta igual ao bloco transmitido.
    /// </summary>
    internal static class TP02WbpWriterProgram
    {
        private sealed class Options
        {
            public string Port = string.Empty;
            public string File = string.Empty;
            public bool Write;
            public int Station = 1;
            public int ResponseCode = 5;
            public int Start;
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
                if (string.IsNullOrEmpty(options.File))
                {
                    PrintUsage();
                    return 2;
                }

                string source = File.ReadAllText(options.File, Encoding.ASCII);
                List<Tp02MachineWord> words = Tp02ComputerLinkProgramCodec.ParseMachineHex(source);
                if (options.Start + words.Count - 1 > Tp02ComputerLinkProgramCodec.MaxProgramAddress)
                    throw new ArgumentOutOfRangeException("start", "Programa ultrapassa o passo 4000.");

                Console.WriteLine("OpenLadder - TP02 Computer Link WBP Writer");
                Console.WriteLine(new string('=', 58));
                Console.WriteLine("Arquivo        : " + Path.GetFullPath(options.File));
                Console.WriteLine("Passos         : " + words.Count.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("Faixa          : " + options.Start.ToString("0000", CultureInfo.InvariantCulture)
                    + ".." + (options.Start + words.Count - 1).ToString("0000", CultureInfo.InvariantCulture));
                Console.WriteLine("Estacao        : " + options.Station.ToString("00", CultureInfo.InvariantCulture));
                Console.WriteLine("Resp. code     : " + options.ResponseCode.ToString("X1", CultureInfo.InvariantCulture));
                Console.WriteLine("Modo           : " + (options.Write ? "WRITE REAL + RBP VERIFY" : "DRY-RUN"));
                Console.WriteLine();

                List<string> frames = Tp02ComputerLinkProgramCodec.BuildWbpProgramFrames(
                    options.Station, options.Start, words, options.ResponseCode);

                int i;
                for (i = 0; i < frames.Count; i++)
                    Console.WriteLine("WBP[" + i.ToString(CultureInfo.InvariantCulture) + "] " + Escape(frames[i]));

                if (!options.Write)
                {
                    Console.WriteLine();
                    Console.WriteLine("DRY-RUN concluido. Nenhum byte foi transmitido.");
                    Console.WriteLine("Para escrever, use --write somente com o TP02 em condicao segura.");
                    return 0;
                }

                if (string.IsNullOrEmpty(options.Port))
                    throw new ArgumentException("--port=COMx e obrigatorio com --write.");

                Console.WriteLine();
                Console.WriteLine("ATENCAO: Computer Link pela MMI exige PG/COM em LOW (pino 4 ligado ao pino 5).");
                Console.WriteLine("A ferramenta recusara WBP se PSR nao confirmar STOP.");
                Console.WriteLine();

                return WriteAndVerify(options, words);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERRO: " + ex.Message);
                return 1;
            }
        }

        private static int WriteAndVerify(Options options, IList<Tp02MachineWord> words)
        {
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

                Console.WriteLine("COM aberta: " + Describe(port));

                string psr = Exchange(port,
                    Tp02ComputerLinkProgramCodec.BuildPsr(options.Station, options.ResponseCode),
                    options.TimeoutMs, "PSR");

                Tp02ComputerLinkState state;
                if (!Tp02ComputerLinkProgramCodec.TryGetPsrState(psr, out state))
                    throw new InvalidDataException("PSR recebido, mas o estado nao pôde ser validado.");

                Console.WriteLine("Estado PLC     : " + state.ToString().ToUpperInvariant());
                if (state != Tp02ComputerLinkState.Stop)
                    throw new InvalidOperationException("WBP BLOQUEADO: o TP02 precisa estar em STOP/PROGRAM.");

                int offset = 0;
                int blockIndex = 0;
                while (offset < words.Count)
                {
                    int count = Math.Min(Tp02ComputerLinkProgramCodec.MaxStepsPerFrame, words.Count - offset);
                    List<Tp02MachineWord> block = new List<Tp02MachineWord>(count);
                    int i;
                    for (i = 0; i < count; i++) block.Add(words[offset + i]);
                    int address = options.Start + offset;

                    string wbpFrame = Tp02ComputerLinkProgramCodec.BuildWbp(
                        options.Station, address, block, options.ResponseCode);
                    string wbpResponse = Exchange(port, wbpFrame, options.TimeoutMs,
                        "WBP bloco " + blockIndex.ToString(CultureInfo.InvariantCulture));

                    string errorCode;
                    if (!Tp02ComputerLinkProgramCodec.IsSuccessfulResponse(wbpResponse, "WBP", out errorCode))
                        throw new InvalidDataException("TP02 nao confirmou WBP do bloco "
                            + blockIndex.ToString(CultureInfo.InvariantCulture)
                            + (string.IsNullOrEmpty(errorCode) ? "." : "; erro=" + errorCode + "."));

                    string rbpFrame = Tp02ComputerLinkProgramCodec.BuildRbp(
                        options.Station, address, count, options.ResponseCode);
                    string rbpResponse = Exchange(port, rbpFrame, options.TimeoutMs,
                        "RBP verify " + blockIndex.ToString(CultureInfo.InvariantCulture));

                    Tp02ComputerLinkResponse parsed = Tp02ComputerLinkProgramCodec.ParseResponse(rbpResponse, "RBP");
                    if (!parsed.ChecksumOk || parsed.IsError || parsed.Command != "RBP")
                        throw new InvalidDataException("RBP de verificacao invalido no bloco "
                            + blockIndex.ToString(CultureInfo.InvariantCulture) + ".");

                    string expected = WordsToHex(block);
                    string actual = NormalizeHex(parsed.Data);
                    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("VERIFY FALHOU no bloco "
                            + blockIndex.ToString(CultureInfo.InvariantCulture)
                            + " addr=" + address.ToString("0000", CultureInfo.InvariantCulture)
                            + ". Esperado=" + expected + " recebido=" + actual + ".");

                    Console.WriteLine("VERIFY OK      : bloco " + blockIndex.ToString(CultureInfo.InvariantCulture)
                        + " addr=" + address.ToString("0000", CultureInfo.InvariantCulture)
                        + " passos=" + count.ToString(CultureInfo.InvariantCulture));

                    offset += count;
                    blockIndex++;
                }

                Console.WriteLine();
                Console.WriteLine("GRAVACAO CONFIRMADA: todos os blocos WBP foram relidos por RBP sem diferencas.");
                return 0;
            }
        }

        private static string Exchange(SerialPort port, string frame, int timeoutMs, string label)
        {
            port.DiscardInBuffer();
            Console.WriteLine("TX " + label.PadRight(14) + " " + Escape(frame));
            port.Write(frame);
            string response = ReadUntilCarriageReturn(port, timeoutMs);
            if (string.IsNullOrEmpty(response)) throw new TimeoutException(label + ": nenhuma resposta do TP02.");
            Console.WriteLine("RX " + label.PadRight(14) + " " + Escape(response));
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

        private static string WordsToHex(IList<Tp02MachineWord> words)
        {
            StringBuilder sb = new StringBuilder(words.Count * 6);
            int i;
            for (i = 0; i < words.Count; i++) sb.Append(words[i].ToHex());
            return sb.ToString();
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
                if (a.Equals("--write", StringComparison.OrdinalIgnoreCase)) { o.Write = true; continue; }
                if (a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase)) { o.Port = a.Substring(7); continue; }
                if (a.StartsWith("--file=", StringComparison.OrdinalIgnoreCase)) { o.File = a.Substring(7).Trim('"'); continue; }
                if (a.StartsWith("--station=", StringComparison.OrdinalIgnoreCase)) { o.Station = ParseInt(a.Substring(10), "station"); continue; }
                if (a.StartsWith("--response=", StringComparison.OrdinalIgnoreCase)) { o.ResponseCode = ParseHexInt(a.Substring(11), "response"); continue; }
                if (a.StartsWith("--start=", StringComparison.OrdinalIgnoreCase)) { o.Start = ParseInt(a.Substring(8), "start"); continue; }
                if (a.StartsWith("--baud=", StringComparison.OrdinalIgnoreCase)) { o.Baud = ParseInt(a.Substring(7), "baud"); continue; }
                if (a.StartsWith("--databits=", StringComparison.OrdinalIgnoreCase)) { o.DataBits = ParseInt(a.Substring(11), "databits"); continue; }
                if (a.StartsWith("--timeout=", StringComparison.OrdinalIgnoreCase)) { o.TimeoutMs = ParseInt(a.Substring(10), "timeout"); continue; }
                if (a.StartsWith("--parity=", StringComparison.OrdinalIgnoreCase))
                {
                    o.Parity = (Parity)Enum.Parse(typeof(Parity), a.Substring(9), true);
                    continue;
                }
                if (a.StartsWith("--stopbits=", StringComparison.OrdinalIgnoreCase))
                {
                    string s = a.Substring(11);
                    o.StopBits = s == "2" || s.Equals("Two", StringComparison.OrdinalIgnoreCase) ? StopBits.Two : StopBits.One;
                    continue;
                }
                if (!a.StartsWith("--", StringComparison.Ordinal) && string.IsNullOrEmpty(o.File)) { o.File = a.Trim('"'); continue; }
                throw new ArgumentException("Argumento desconhecido: " + a);
            }

            if (o.Station < 1 || o.Station > 99) throw new ArgumentOutOfRangeException("station");
            if (o.ResponseCode < 0 || o.ResponseCode > 15) throw new ArgumentOutOfRangeException("response");
            if (o.Start < 0 || o.Start > 4000) throw new ArgumentOutOfRangeException("start");
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
            Console.WriteLine("  OpenLadderTP02Wbp.exe programa.hex");
            Console.WriteLine("  OpenLadderTP02Wbp.exe --file=programa.hex --port=COM3 --write");
            Console.WriteLine();
            Console.WriteLine("Padrao Computer Link: station=1, response=5, start=0, 19200 7N1.");
            Console.WriteLine("Sem --write, a ferramenta apenas mostra os quadros WBP e nao abre a COM.");
        }
    }
}
