using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace ModernPC12
{
    internal struct Pg33MachineWord
    {
        public byte High;
        public byte Low;
        public byte External;

        public Pg33MachineWord(byte high, byte low, byte external)
        {
            High = high;
            Low = low;
            External = external;
        }

        public override string ToString()
        {
            return High.ToString("X2", CultureInfo.InvariantCulture) + " "
                + Low.ToString("X2", CultureInfo.InvariantCulture) + " "
                + External.ToString("X2", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Emulador PG de laboratorio do WEG TP02 para engenharia reversa controlada
    /// do PC12 original. Use somente em uma porta COM virtual pareada.
    ///
    /// O comando 0x33 (Write PLC Program) foi reconstruido estaticamente no
    /// pc12.exe e confirmado por emulacao Unicorn offline do construtor original.
    /// O ACK 00 00 FF usado aqui para 0x33 e SINTETICO: o parser generico do PC12
    /// o aceita, mas o payload exato devolvido pelo TP02 fisico ainda precisa ser
    /// confirmado em captura real.
    /// </summary>
    internal static class TP02PgEmulatorProgram
    {
        private const int MaxProgramSteps = 4000;
        private const int MaxPg33Words = 80;

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

        // Tambem e usado como ACK sintetico PG33 no emulador.
        private static readonly byte[] EmptySuccessResponse = new byte[]
        {
            0x00, 0x00, 0xFF
        };

        // Fixture real capturado de uma resposta 34 na pagina inicial.
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
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] Memory = new byte[65536];
        private static readonly Pg33MachineWord[] ProgramWords = new Pg33MachineWord[MaxProgramSteps];
        private static readonly bool[] ProgramWordValid = new bool[MaxProgramSteps];
        private static readonly List<byte> RxBuffer = new List<byte>();
        private static readonly object LogSync = new object();

        private static SerialPort Port;
        private static bool StopRequested;
        private static bool AutoAckUnknown = true;
        private static bool Pg33SyntheticAck = true;
        private static bool FastMode;
        private static bool HelloC0;
        private static string CaptureDirectory;
        private static string LogPath;
        private static string RawPath;
        private static string Pg33DumpPath;
        private static int UnknownCounter;
        private static int BinaryCounter;
        private static int Pg33FrameCounter;
        private static int HighestProgramStep = -1;

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

                    byte[] received = new byte[read];
                    Buffer.BlockCopy(chunk, 0, received, 0, read);
                    SaveRawChunk(received);
                    LogFrame("PC12 RAW", received, false);

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

            Log("FIM", "Emulador encerrado. PG33 frames=" + Pg33FrameCounter.ToString(CultureInfo.InvariantCulture)
                + " highestStep=" + HighestProgramStep.ToString(CultureInfo.InvariantCulture) + ".");
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
                if (a.Equals("--pg33-ack", StringComparison.OrdinalIgnoreCase))
                {
                    Pg33SyntheticAck = true;
                    continue;
                }
                if (a.Equals("--no-pg33-ack", StringComparison.OrdinalIgnoreCase))
                {
                    Pg33SyntheticAck = false;
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
            Console.WriteLine(" Porta       : " + portName);
            Console.WriteLine(" Serial      : 19200 8O1 / DTR on / RTS off");
            Console.WriteLine(" HELLO RX    : " + (HelloC0 ? "C0 01 09 35" : "80 01 09 75"));
            Console.WriteLine(" PG33        : Write Program CONFIRMADO offline");
            Console.WriteLine(" PG33 ACK    : " + (Pg33SyntheticAck ? "00 00 FF SINTETICO" : "SEM RESPOSTA"));
            Console.WriteLine(" Unknown ACK : " + (AutoAckUnknown ? "ON" : "OFF"));
            Console.WriteLine(" Timing      : " + (FastMode ? "FAST" : "TP02 aproximado"));
            Console.WriteLine(" ATENCAO     : use COM virtual; nao use a COM fisica do PLC.");
            Console.WriteLine("============================================================");
            Console.WriteLine();
        }

        private static void PrepareCaptureDirectory()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            CaptureDirectory = Path.Combine(root, "tp02-emulator-captures");
            Directory.CreateDirectory(CaptureDirectory);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            LogPath = Path.Combine(CaptureDirectory, "TP02-Emulator-" + stamp + ".txt");
            RawPath = Path.Combine(CaptureDirectory, "TP02-Emulator-" + stamp + "-raw.bin");
            Pg33DumpPath = Path.Combine(CaptureDirectory, "TP02-Emulator-" + stamp + "-pg33-program.bin");
        }

        private static void SeedMemory()
        {
            Array.Clear(Memory, 0, Memory.Length);
            Array.Clear(ProgramWordValid, 0, ProgramWordValid.Length);

            CopyToMemory(0x6000, System6000);

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
                    Send("EMU -> PC12 14", EmptySuccessResponse);
                    break;

                case 0x33:
                    HandlePg33WriteProgram(frame);
                    break;

                default:
                    SaveUnknownFrame(frame);
                    if (AutoAckUnknown)
                    {
                        SleepFor(150);
                        Send(
                            "EMU -> PC12 ACK GENERICO para CMD=0x"
                            + command.ToString("X2", CultureInfo.InvariantCulture),
                            EmptySuccessResponse);
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

        private static void HandlePg33WriteProgram(byte[] frame)
        {
            int startStep;
            Pg33MachineWord[] words;
            string reason;

            if (!TryDecodePg33(frame, out startStep, out words, out reason))
            {
                Log("PG33 INVALIDO", reason);
                SaveNamedFrame(frame, "pg33-invalid");
                return;
            }

            Pg33FrameCounter++;

            for (int i = 0; i < words.Length; i++)
            {
                int step = startStep + i;
                ProgramWords[step] = words[i];
                ProgramWordValid[step] = true;
                if (step > HighestProgramStep) HighestProgramStep = step;
            }

            SaveProgramDump();

            string preview = BuildWordPreview(words, 12);
            Log(
                "PG33 WRITE PROGRAM",
                "frame=" + Pg33FrameCounter.ToString(CultureInfo.InvariantCulture)
                + " start=0x" + startStep.ToString("X4", CultureInfo.InvariantCulture)
                + " words=" + words.Length.ToString(CultureInfo.InvariantCulture)
                + " next=0x" + (startStep + words.Length).ToString("X4", CultureInfo.InvariantCulture)
                + " | " + preview);

            if (LooksLikeW1A(words))
                Log("PG33 W1A", "Assinatura STR X001 / OUT Y001 / END reconhecida apos reconstruir os planos HL+EX.");

            if (Pg33SyntheticAck)
            {
                SleepFor(150);
                Send("EMU -> PC12 ACK SINTETICO PG33 (nao confirmado no PLC fisico)", EmptySuccessResponse);
            }
            else
            {
                Log("PG33 SEM ACK", "Quadro valido armazenado; resposta desabilitada por --no-pg33-ack.");
            }
        }

        private static bool TryDecodePg33(
            byte[] frame,
            out int startStep,
            out Pg33MachineWord[] words,
            out string reason)
        {
            startStep = 0;
            words = new Pg33MachineWord[0];
            reason = string.Empty;

            if (frame == null || frame.Length < 10)
            {
                reason = "quadro curto";
                return false;
            }
            if (frame[0] != 0x33)
            {
                reason = "opcode diferente de 0x33";
                return false;
            }
            if (frame[2] != 0x00)
            {
                reason = "TX[2] esperado 00, recebido " + frame[2].ToString("X2", CultureInfo.InvariantCulture);
                return false;
            }

            int highLowBytes = frame[5];
            if (highLowBytes <= 0 || (highLowBytes & 1) != 0)
            {
                reason = "TX[5] precisa ser 2*W e portanto par";
                return false;
            }

            int wordCount = highLowBytes / 2;
            if (wordCount < 1 || wordCount > MaxPg33Words)
            {
                reason = "quantidade de words fora de 1..80";
                return false;
            }

            int expectedLen = (3 * wordCount) + 4;
            if (frame[1] != expectedLen)
            {
                reason = "LEN inconsistente: recebido " + frame[1].ToString(CultureInfo.InvariantCulture)
                    + ", esperado " + expectedLen.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            if (frame.Length != expectedLen + 3)
            {
                reason = "comprimento total inconsistente";
                return false;
            }

            startStep = (frame[3] << 8) | frame[4];
            if (startStep < 0 || startStep >= MaxProgramSteps || startStep + wordCount > MaxProgramSteps)
            {
                reason = "faixa de passos fora de 0..3999";
                return false;
            }

            int highLowStart = 6;
            int externalStart = highLowStart + highLowBytes;
            int checksumIndex = frame.Length - 1;
            if (externalStart + wordCount != checksumIndex)
            {
                reason = "planos HIGH/LOW e EXTERNAL nao fecham na posicao do checksum";
                return false;
            }

            Pg33MachineWord[] decoded = new Pg33MachineWord[wordCount];
            for (int i = 0; i < wordCount; i++)
            {
                decoded[i] = new Pg33MachineWord(
                    frame[highLowStart + (2 * i)],
                    frame[highLowStart + (2 * i) + 1],
                    frame[externalStart + i]);
            }

            words = decoded;
            return true;
        }

        private static bool LooksLikeW1A(Pg33MachineWord[] words)
        {
            if (words == null || words.Length < 3) return false;
            return words[0].High == 0x00 && words[0].Low == 0x10 && words[0].External == 0x00
                && words[1].High == 0x20 && words[1].Low == 0x40 && words[1].External == 0x00
                && words[2].High == 0x00 && words[2].Low == 0x70 && words[2].External == 0x00;
        }

        private static string BuildWordPreview(Pg33MachineWord[] words, int max)
        {
            if (words == null || words.Length == 0) return "(sem words)";
            StringBuilder sb = new StringBuilder();
            int n = Math.Min(words.Length, max);
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append("#");
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                sb.Append("=");
                sb.Append(words[i].ToString());
            }
            if (words.Length > n) sb.Append(" | ...");
            return sb.ToString();
        }

        private static void SaveProgramDump()
        {
            try
            {
                if (HighestProgramStep < 0 || string.IsNullOrEmpty(Pg33DumpPath)) return;

                using (FileStream stream = new FileStream(Pg33DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    for (int step = 0; step <= HighestProgramStep; step++)
                    {
                        Pg33MachineWord word = ProgramWords[step];
                        if (!ProgramWordValid[step])
                        {
                            stream.WriteByte(0x00);
                            stream.WriteByte(0x00);
                            stream.WriteByte(0x00);
                        }
                        else
                        {
                            stream.WriteByte(word.High);
                            stream.WriteByte(word.Low);
                            stream.WriteByte(word.External);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ERRO PG33 DUMP", ex.Message);
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
                + " -> fixture de 240 bytes."
                + (Pg33FrameCounter > 0 ? " ATENCAO: readback 34 ainda nao deriva do banco PG33 escrito." : string.Empty));

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

        private static void SaveRawChunk(byte[] data)
        {
            if (data == null || data.Length == 0 || string.IsNullOrEmpty(RawPath)) return;
            try
            {
                using (FileStream stream = new FileStream(RawPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Log("ERRO RAW", ex.Message);
            }
        }

        private static void SaveUnknownFrame(byte[] frame)
        {
            UnknownCounter++;
            SaveNamedFrame(frame, "unknown-" + UnknownCounter.ToString("000", CultureInfo.InvariantCulture)
                + "-cmd-" + frame[0].ToString("X2", CultureInfo.InvariantCulture));
            Log(
                "DESCONHECIDO",
                "CMD=0x" + frame[0].ToString("X2", CultureInfo.InvariantCulture)
                + " capturado. Candidato a comando ainda nao classificado.");
        }

        private static void SaveNamedFrame(byte[] frame, string suffix)
        {
            try
            {
                string name = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)
                    + "-" + suffix + ".bin";
                string path = Path.Combine(CaptureDirectory, name);
                File.WriteAllBytes(path, frame);
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
                + kind.PadRight(32)
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
