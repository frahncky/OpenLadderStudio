using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace ModernPC12
{
    /// <summary>
    /// Emulador PG mínimo do WEG TP02 para engenharia reversa do PC12.
    /// Deve ser ligado a uma porta COM VIRTUAL pareada com a porta usada pelo PC12.
    /// Nunca use a mesma porta física do PLC para este executável.
    /// </summary>
    internal static class TP02PgEmulatorProgram
    {
        private static readonly byte[] HelloRequest = new byte[]
        {
            0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D
        };

        private static readonly byte[] HelloResponse80 = new byte[]
        {
            0x80, 0x01, 0x09, 0x75
        };

        private static readonly byte[] HelloResponseC0 = new byte[]
        {
            0xC0, 0x01, 0x09, 0x35
        };

        private static readonly byte[] F0Response = new byte[]
        {
            0x00, 0x02, 0x10, 0x22, 0xCB
        };

        private static readonly byte[] Response38 = new byte[]
        {
            0x00, 0x02, 0x00, 0x0A, 0xF3
        };

        private static readonly byte[] Response14 = new byte[]
        {
            0x00, 0x00, 0xFF
        };

        private static readonly byte[] ProgramPage0000 = new byte[]
        {
            0x00, 0x18, 0x20, 0x41, 0x00, 0x1A, 0x20, 0x44, 0x00, 0x14, 0x20, 0x45, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x09, 0x07, 0x0B, 0x0A, 0x05, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] System6000 = new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x09, 0x76, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] Memory = new byte[65536];
        private static readonly List<byte> RxBuffer = new List<byte>();
        private static readonly object LogSync = new object();

        private static SerialPort Port;
        private static bool StopRequested;
        private static bool AutoAckUnknown = true;
        private static bool FastMode;
        private static bool HelloC0;
        private static string CaptureDirectory;
        private static string LogPath;
        private static int UnknownCounter;
        private static int BinaryCounter;

        [STAThread]
        private static void Main(string[] args)
        {
            Console.Title = "OpenLadder TP02 PG Emulator";
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            string portName = ParseArguments(args);
            if (string.IsNullOrEmpty(portName))
                portName = AskPort();

            if (string.IsNullOrEmpty(portName))
            {
                Console.WriteLine("Nenhuma porta selecionada.");
                return;
            }

            SeedMemory();
            PrepareCaptureDirectory();

            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                StopRequested = true;
            };

            PrintHeader(portName);

            try
            {
                Port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                Port.Handshake = Handshake.None;
                Port.DtrEnable = true;
                Port.RtsEnable = false;
                Port.ReadTimeout = 100;
                Port.WriteTimeout = 1000;
                Port.Open();
                Port.DiscardInBuffer();
                Port.DiscardOutBuffer();

                Log("INFO", "Porta " + portName + " aberta em 19200 8O1, DTR=on, RTS=off.");
                Log("INFO", "Aguardando o PC12. Ctrl+C encerra o emulador.");

                while (!StopRequested)
                {
                    int available = 0;
                    try { available = Port.BytesToRead; }
                    catch { break; }

                    if (available <= 0)
                    {
                        Thread.Sleep(2);
                        continue;
                    }

                    byte[] chunk = new byte[Math.Min(available, 512)];
                    int read = Port.Read(chunk, 0, chunk.Length);
                    if (read <= 0) continue;

                    for (int i = 0; i < read; i++)
                        RxBuffer.Add(chunk[i]);

                    ProcessRxBuffer();
                }
            }
            catch (Exception ex)
            {
                Log("ERRO", ex.ToString());
            }
            finally
            {
                if (Port != null)
                {
                    try { if (Port.IsOpen) Port.Close(); } catch { }
                    try { Port.Dispose(); } catch { }
                }
            }

            Log("FIM", "Emulador encerrado.");
            Console.WriteLine();
            Console.WriteLine("Capturas: " + CaptureDirectory);
        }

        private static string ParseArguments(string[] args)
        {
            string port = string.Empty;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i] == null ? string.Empty : args[i].Trim();
                if (a.Length == 0) continue;

                if (a.Equals("--auto-ack", StringComparison.OrdinalIgnoreCase))
                {
                    AutoAckUnknown = true;
                    continue;
                }
                if (a.Equals("--no-auto-ack", StringComparison.OrdinalIgnoreCase))
                {
                    AutoAckUnknown = false;
                    continue;
                }
                if (a.Equals("--fast", StringComparison.OrdinalIgnoreCase))
                {
                    FastMode = true;
                    continue;
                }
                if (a.Equals("--hello=c0", StringComparison.OrdinalIgnoreCase))
                {
                    HelloC0 = true;
                    continue;
                }
                if (a.Equals("--hello=80", StringComparison.OrdinalIgnoreCase))
                {
                    HelloC0 = false;
                    continue;
                }
                if (!a.StartsWith("--", StringComparison.Ordinal) && port.Length == 0)
                    port = a;
            }
            return port;
        }

        private static string AskPort()
        {
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);

            Console.WriteLine("Portas COM encontradas:");
            if (ports.Length == 0)
            {
                Console.WriteLine("  nenhuma");
            }
            else
            {
                for (int i = 0; i < ports.Length; i++)
                    Console.WriteLine("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + ports[i]);
            }

            Console.WriteLine();
            Console.Write("Porta VIRTUAL ligada ao PC12 (ex.: COM11): ");
            string answer = Console.ReadLine();
            return answer == null ? string.Empty : answer.Trim();
        }

        private static void PrintHeader(string portName)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine(" OpenLadder - WEG TP02 PG Emulator");
            Console.WriteLine("============================================================");
            Console.WriteLine(" Porta     : " + portName);
            Console.WriteLine(" Serial    : 19200 8O1 / DTR on / RTS off");
            Console.WriteLine(" HELLO RX  : " + (HelloC0 ? "C0 01 09 35" : "80 01 09 75"));
            Console.WriteLine(" Auto-ACK  : " + (AutoAckUnknown ? "ON" : "OFF"));
            Console.WriteLine(" Timing    : " + (FastMode ? "FAST" : "TP02 aproximado"));
            Console.WriteLine(" ATENCAO   : use uma COM virtual pareada, nao a COM fisica do PLC.");
            Console.WriteLine("============================================================");
            Console.WriteLine();
        }

        private static void PrepareCaptureDirectory()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            CaptureDirectory = Path.Combine(root, "tp02-emulator-captures");
            Directory.CreateDirectory(CaptureDirectory);
            LogPath = Path.Combine(
                CaptureDirectory,
                "TP02-Emulator-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
        }

        private static void SeedMemory()
        {
            Array.Clear(Memory, 0, Memory.Length);

            CopyToMemory(0x6000, System6000);

            // Padroes observados no TP02 real: região de texto preenchida com 0x20.
            for (int address = 0x62B0; address < 0x6810; address += 2)
            {
                Memory[address] = 0x00;
                if (address + 1 < Memory.Length) Memory[address + 1] = 0x20;
            }

            for (int address = 0x64C0; address < 0x6810; address++)
                Memory[address] = 0x20;
        }

        private static void CopyToMemory(int address, byte[] data)
        {
            if (data == null) return;
            for (int i = 0; i < data.Length && address + i < Memory.Length; i++)
                Memory[address + i] = data[i];
        }

        private static void ProcessRxBuffer()
        {
            while (RxBuffer.Count > 0)
            {
                if (CouldBeHelloPrefix())
                {
                    if (RxBuffer.Count < HelloRequest.Length)
                        return;

                    if (StartsWith(RxBuffer, HelloRequest))
                    {
                        byte[] hello = Take(HelloRequest.Length);
                        HandleHello(hello);
                        continue;
                    }

                    byte noise = RxBuffer[0];
                    RxBuffer.RemoveAt(0);
                    Log("RX NOISE", noise.ToString("X2", CultureInfo.InvariantCulture));
                    continue;
                }

                if (RxBuffer.Count < 2)
                    return;

                int payloadLength = RxBuffer[1];
                int totalLength = payloadLength + 3;
                if (totalLength < 3 || totalLength > 258)
                {
                    byte noise = RxBuffer[0];
                    RxBuffer.RemoveAt(0);
                    Log("RX NOISE", noise.ToString("X2", CultureInfo.InvariantCulture));
                    continue;
                }

                if (RxBuffer.Count < totalLength)
                    return;

                byte[] frame = Take(totalLength);
                HandleBinaryFrame(frame);
            }
        }

        private static bool CouldBeHelloPrefix()
        {
            if (RxBuffer.Count == 0 || RxBuffer[0] != HelloRequest[0])
                return false;

            int n = Math.Min(RxBuffer.Count, HelloRequest.Length);
            for (int i = 0; i < n; i++)
                if (RxBuffer[i] != HelloRequest[i])
                    return false;

            return true;
        }

        private static bool StartsWith(List<byte> buffer, byte[] pattern)
        {
            if (buffer.Count < pattern.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
                if (buffer[i] != pattern[i]) return false;
            return true;
        }

        private static byte[] Take(int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
                result[i] = RxBuffer[i];
            RxBuffer.RemoveRange(0, count);
            return result;
        }

        private static void HandleHello(byte[] request)
        {
            LogFrame("PC12 -> EMU HELLO", request, false);
            SleepFor(220);

            byte[] response = HelloC0 ? HelloResponseC0 : HelloResponse80;
            Send("EMU -> PC12 HELLO", response);
        }

        private static void HandleBinaryFrame(byte[] frame)
        {
            BinaryCounter++;
            bool checksumOk = Sum8(frame) == 0xFF;
            byte command = frame[0];
            int payloadLength = frame[1];

            LogFrame(
                "PC12 -> EMU #" + BinaryCounter.ToString(CultureInfo.InvariantCulture)
                + " CMD=0x" + command.ToString("X2", CultureInfo.InvariantCulture)
                + " LEN=" + payloadLength.ToString(CultureInfo.InvariantCulture)
                + (checksumOk ? " CHK=OK" : " CHK=ERRO"),
                frame,
                true);

            if (!checksumOk)
            {
                Log("BLOQUEIO", "Frame ignorado porque a soma modulo 256 nao fecha em FF.");
                return;
            }

            switch (command)
            {
                case 0xF0:
                    SleepFor(220);
                    Send("EMU -> PC12 F0", F0Response);
                    break;

                case 0x38:
                    SleepFor(260);
                    Send("EMU -> PC12 38", Response38);
                    break;

                case 0x34:
                    SleepFor(350);
                    Send("EMU -> PC12 34", BuildProgramReadResponse(frame));
                    break;

                case 0x0A:
                    SleepFor(330);
                    Send("EMU -> PC12 0A", BuildMemoryReadResponse(frame));
                    break;

                case 0x14:
                    SleepFor(240);
                    Send("EMU -> PC12 14", Response14);
                    break;

                default:
                    SaveUnknownFrame(frame);
                    if (AutoAckUnknown)
                    {
                        SleepFor(150);
                        byte[] ack = BuildResponse(new byte[0]);
                        Send(
                            "EMU -> PC12 ACK GENERICO para CMD=0x"
                            + command.ToString("X2", CultureInfo.InvariantCulture),
                            ack);
                    }
                    else
                    {
                        Log(
                            "SEM RESPOSTA",
                            "CMD desconhecido 0x"
                            + command.ToString("X2", CultureInfo.InvariantCulture)
                            + " foi apenas capturado.");
                    }
                    break;
            }
        }

        private static byte[] BuildProgramReadResponse(byte[] request)
        {
            int requestedAddress = 0;
            int requestedHint = 0;
            if (request.Length >= 6)
            {
                requestedAddress = (request[2] << 8) | request[3];
                requestedHint = request[4];
            }

            byte[] payload = new byte[0xF0];
            if (requestedAddress == 0)
            {
                Buffer.BlockCopy(ProgramPage0000, 0, payload, 0, Math.Min(payload.Length, ProgramPage0000.Length));
            }

            Log(
                "DECOD 34",
                "addr=0x" + requestedAddress.ToString("X4", CultureInfo.InvariantCulture)
                + " hint=0x" + requestedHint.ToString("X2", CultureInfo.InvariantCulture)
                + " -> resposta de 240 bytes.");

            return BuildResponse(payload);
        }

        private static byte[] BuildMemoryReadResponse(byte[] request)
        {
            if (request.Length < 6)
                return BuildResponse(new byte[0]);

            int address = (request[2] << 8) | request[3];
            int count = request[4];
            byte[] payload = new byte[count];

            for (int i = 0; i < count; i++)
            {
                int p = address + i;
                payload[i] = p >= 0 && p < Memory.Length ? Memory[p] : (byte)0x00;
            }

            Log(
                "DECOD 0A",
                "addr=0x" + address.ToString("X4", CultureInfo.InvariantCulture)
                + " count=" + count.ToString(CultureInfo.InvariantCulture));

            return BuildResponse(payload);
        }

        private static byte[] BuildResponse(byte[] payload)
        {
            if (payload == null) payload = new byte[0];
            if (payload.Length > 255)
                throw new ArgumentOutOfRangeException("payload", "Payload maximo de 255 bytes.");

            byte[] frame = new byte[payload.Length + 3];
            frame[0] = 0x00;
            frame[1] = (byte)payload.Length;
            if (payload.Length > 0)
                Buffer.BlockCopy(payload, 0, frame, 2, payload.Length);
            frame[frame.Length - 1] = ChecksumFor(frame, frame.Length - 1);
            return frame;
        }

        private static byte ChecksumFor(byte[] frame, int countWithoutChecksum)
        {
            int sum = 0;
            for (int i = 0; i < countWithoutChecksum; i++)
                sum = (sum + frame[i]) & 0xFF;
            return (byte)((0xFF - sum) & 0xFF);
        }

        private static byte Sum8(byte[] frame)
        {
            int sum = 0;
            for (int i = 0; i < frame.Length; i++)
                sum = (sum + frame[i]) & 0xFF;
            return (byte)sum;
        }

        private static void Send(string label, byte[] frame)
        {
            if (Port == null || !Port.IsOpen) return;
            Port.Write(frame, 0, frame.Length);
            LogFrame(label, frame, true);
        }

        private static void SleepFor(int milliseconds)
        {
            if (!FastMode && milliseconds > 0)
                Thread.Sleep(milliseconds);
        }

        private static void SaveUnknownFrame(byte[] frame)
        {
            try
            {
                UnknownCounter++;
                string name = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)
                    + "-unknown-"
                    + UnknownCounter.ToString("000", CultureInfo.InvariantCulture)
                    + "-cmd-"
                    + frame[0].ToString("X2", CultureInfo.InvariantCulture)
                    + ".bin";
                string path = Path.Combine(CaptureDirectory, name);
                File.WriteAllBytes(path, frame);
                Log(
                    "DESCONHECIDO",
                    "CMD=0x" + frame[0].ToString("X2", CultureInfo.InvariantCulture)
                    + " salvo em " + name
                    + ". Candidato a comando ainda nao documentado/escrita.");
            }
            catch (Exception ex)
            {
                Log("ERRO CAPTURA", ex.Message);
            }
        }

        private static void LogFrame(string label, byte[] frame, bool showChecksum)
        {
            string suffix = showChecksum
                ? " soma=0x" + Sum8(frame).ToString("X2", CultureInfo.InvariantCulture)
                : string.Empty;
            Log(label, "[" + ToHex(frame) + "]" + suffix);
        }

        private static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static void Log(string kind, string text)
        {
            string line = "["
                + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "] "
                + kind.PadRight(24)
                + " "
                + text;

            lock (LogSync)
            {
                Console.WriteLine(line);
                if (!string.IsNullOrEmpty(LogPath))
                {
                    try
                    {
                        File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                    }
                    catch { }
                }
            }
        }
    }
}
