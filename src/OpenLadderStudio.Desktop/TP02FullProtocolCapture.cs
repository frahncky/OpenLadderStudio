using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    internal static class TP02FullProtocolCaptureProgram
    {
        private enum CampaignMode { Safe, Lab, DestructiveLab }

        private sealed class CaptureEntry
        {
            internal int Sequence;
            internal string Label;
            internal string Tx;
            internal string Rx;
            internal long ElapsedMs;
            internal string Parsed;
            internal string Note;
        }

        private sealed class PortProfile
        {
            internal string Name;
            internal bool Dtr;
            internal bool Rts;
        }

        private static readonly byte[] Hello = new byte[] { 0x43, 0x4F, 0x4E, 0x2D, 0x49, 0x43, 0x42, 0x0D };
        private static readonly byte[] HelloStop = new byte[] { 0x80, 0x01, 0x09, 0x75 };
        private static readonly byte[] HelloRun = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
        private static readonly byte[] F0 = new byte[] { 0xF0, 0x00, 0x0F };
        private static readonly byte[] Frame38 = new byte[] { 0x38, 0x00, 0xC7 };
        private static readonly byte[] Stop = new byte[] { 0x01, 0x00, 0xFE };
        private static readonly byte[] Run = new byte[] { 0x02, 0x00, 0xFD };
        private static readonly byte[] ClearProgram = new byte[] { 0x03, 0x00, 0xFC };
        private static readonly byte[] ClearSystem = new byte[] { 0x04, 0x00, 0xFB };
        private static readonly byte[] ClearAll = new byte[] { 0x0F, 0x00, 0xF0 };
        private static readonly byte[] ClearData = new byte[] { 0x11, 0x00, 0xEE };
        private static readonly byte[] EepromToPlc = new byte[] { 0x12, 0x00, 0xED };
        private static readonly byte[] PlcToEeprom = new byte[] { 0x13, 0x00, 0xEC };
        private static readonly byte[] Auth = new byte[] { 0x14, 0x00, 0xEB };

        private static readonly List<CaptureEntry> Entries = new List<CaptureEntry>();
        private static string SessionDir = string.Empty;
        private static int Sequence;
        private static CampaignMode Mode;
        private static bool EepromEnabled;
        private static string PortName = string.Empty;
        private static PortProfile ActiveProfile;

        private static int Main(string[] args)
        {
            try
            {
                if (HasArg(args, "--self-test")) return SelfTest();
                PortName = ArgValue(args, "--port");
                string modeText = ArgValue(args, "--mode");
                EepromEnabled = HasArg(args, "--eeprom");
                if (string.IsNullOrEmpty(PortName)) PortName = ChoosePort();
                Mode = ParseMode(modeText);
                ConfirmMode(Mode);
                CreateSession();
                Log("TP02 FULL PROTOCOL CAPTURE iniciado.");
                Log("Porta=" + PortName + " | modo=" + Mode.ToString().ToUpperInvariant());
                Log("BIOS 37=PERMANENTEMENTE BLOQUEADO; PG33=NAO RETRANSMITIDO por esta campanha.");

                SerialPort port = null;
                try
                {
                    string state;
                    port = AcquirePort(PortName, out state);
                    Log("Enlace qualificado em " + ActiveProfile.Name + " | estado=" + state + ".");
                    CaptureF0(port, "BASE-F0");
                    SnapshotBaseline(port, state);
                    if (Mode != CampaignMode.Safe)
                    {
                        if (!string.Equals(state, "STOP", StringComparison.Ordinal))
                            throw new InvalidOperationException("Modo LAB exige TP02 inicialmente em STOP. Coloque a chave em STOP e execute novamente.");
                        RunReversibleLab(port);
                    }
                    if (Mode == CampaignMode.DestructiveLab) RunDestructiveLab(port);
                    WriteReport("COMPLETED", string.Empty);
                    Log("Campanha concluida. Pasta: " + SessionDir);
                    Console.WriteLine();
                    Console.WriteLine("RESULTADO: CAMPANHA CONCLUIDA");
                    Console.WriteLine("Pasta: " + SessionDir);
                    return 0;
                }
                finally { ClosePort(port); }
            }
            catch (Exception ex)
            {
                try { WriteReport("FAILED", ex.ToString()); } catch { }
                Console.Error.WriteLine("FALHA: " + ex.Message);
                if (!string.IsNullOrEmpty(SessionDir)) Console.Error.WriteLine("Evidencias: " + SessionDir);
                return 1;
            }
        }

        private static CampaignMode ParseMode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Console.WriteLine("Modo da campanha:");
                Console.WriteLine("  1 = SAFE (somente leitura)");
                Console.WriteLine("  2 = LAB (escritas reversiveis + RUN/STOP em bancada isolada)");
                Console.WriteLine("  3 = DESTRUCTIVE (clears; deixa PLC alterado; opcional EEPROM)");
                Console.Write("Escolha [1]: ");
                text = Console.ReadLine();
                if (string.IsNullOrEmpty(text)) text = "1";
            }
            text = text.Trim().ToUpperInvariant();
            if (text == "1" || text == "SAFE") return CampaignMode.Safe;
            if (text == "2" || text == "LAB") return CampaignMode.Lab;
            if (text == "3" || text == "DESTRUCTIVE" || text == "DESTRUCTIVE_LAB") return CampaignMode.DestructiveLab;
            throw new ArgumentException("Modo invalido. Use SAFE, LAB ou DESTRUCTIVE.");
        }

        private static void ConfirmMode(CampaignMode mode)
        {
            if (mode == CampaignMode.Safe) return;
            Console.WriteLine();
            Console.WriteLine("ATENCAO: este modo transmite comandos que alteram o TP02.");
            Console.WriteLine("Use somente em bancada isolada, sem processo/carga conectados e com PLC em STOP.");
            Console.WriteLine("O teste LAB pode colocar o PLC brevemente em RUN para capturar 02/01.");
            if (mode == CampaignMode.DestructiveLab)
            {
                Console.WriteLine("DESTRUCTIVE executa Clear Data/System/Program/All e NAO faz restore automatico.");
                Console.WriteLine("O backup completo e salvo ANTES, mas a restauracao fica para procedimento posterior controlado.");
            }
            string token = mode == CampaignMode.Lab ? "LAB-ISOLADO" : "APAGAR-TP02";
            Console.Write("Digite " + token + " para continuar: ");
            string answer = (Console.ReadLine() ?? string.Empty).Trim().ToUpperInvariant();
            if (answer != token) throw new InvalidOperationException("Confirmacao de bancada nao recebida.");
        }

        private static string ChoosePort()
        {
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);
            if (ports.Length == 0) throw new InvalidOperationException("Nenhuma porta COM encontrada.");
            Console.WriteLine("Portas COM:");
            for (int i = 0; i < ports.Length; i++) Console.WriteLine("  " + (i + 1).ToString(CultureInfo.InvariantCulture) + " = " + ports[i]);
            Console.Write("Escolha [1]: ");
            string text = Console.ReadLine();
            int index = 1;
            if (!string.IsNullOrEmpty(text) && !int.TryParse(text.Trim(), out index)) throw new ArgumentException("Selecao de COM invalida.");
            if (index < 1 || index > ports.Length) throw new ArgumentOutOfRangeException("index");
            return ports[index - 1];
        }

        private static void CreateSession()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenLadder Studio", "TP02 Full Protocol Capture");
            SessionDir = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(SessionDir);
        }

        private static SerialPort AcquirePort(string portName, out string state)
        {
            List<PortProfile> profiles = BuildProfiles(portName);
            Exception last = null;
            for (int p = 0; p < profiles.Count; p++)
            {
                PortProfile profile = profiles[p];
                SerialPort port = null;
                try
                {
                    port = OpenPort(portName, profile.Dtr, profile.Rts);
                    Thread.Sleep(p == 0 ? 250 : 450);
                    for (int attempt = 1; attempt <= 4; attempt++)
                    {
                        byte[] raw = ExchangeRaw(port, Hello, 700, 90,
                            "ACQUIRE-" + (p + 1).ToString("00", CultureInfo.InvariantCulture) + "-HELLO-" + attempt.ToString(CultureInfo.InvariantCulture), "session");
                        if (Contains(raw, HelloStop)) { ActiveProfile = profile; state = "STOP"; return port; }
                        if (Contains(raw, HelloRun)) { ActiveProfile = profile; state = "RUN"; return port; }
                        Thread.Sleep(120);
                    }
                }
                catch (Exception ex) { last = ex; }
                ClosePort(port);
            }
            state = string.Empty;
            throw new IOException("Nenhum perfil DTR/RTS confirmou HELLO. " + (last == null ? string.Empty : last.Message));
        }

        private static List<PortProfile> BuildProfiles(string portName)
        {
            List<PortProfile> result = new List<PortProfile>();
            PortProfile cached = ReadCachedProfile(portName);
            if (cached != null) AddProfile(result, cached.Name, cached.Dtr, cached.Rts);
            AddProfile(result, "19200 8O1 DTR=off RTS=off", false, false);
            AddProfile(result, "19200 8O1 DTR=on RTS=on", true, true);
            AddProfile(result, "19200 8O1 DTR=on RTS=off", true, false);
            AddProfile(result, "19200 8O1 DTR=off RTS=on", false, true);
            return result;
        }

        private static void AddProfile(List<PortProfile> list, string name, bool dtr, bool rts)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Dtr == dtr && list[i].Rts == rts) return;
            PortProfile p = new PortProfile(); p.Name = name; p.Dtr = dtr; p.Rts = rts; list.Add(p);
        }

        private static PortProfile ReadCachedProfile(string portName)
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string path = Path.Combine(root, "OpenLadderStudio", "tp02-pg-last-profile.txt");
                if (!File.Exists(path)) return null;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                string cachedPort = string.Empty, name = "perfil lembrado";
                bool dtr = false, rts = false, haveDtr = false, haveRts = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    int sep = lines[i].IndexOf('='); if (sep <= 0) continue;
                    string key = lines[i].Substring(0, sep).Trim().ToUpperInvariant();
                    string value = lines[i].Substring(sep + 1).Trim();
                    if (key == "PORT") cachedPort = value;
                    else if (key == "DTR") { dtr = value == "1"; haveDtr = true; }
                    else if (key == "RTS") { rts = value == "1"; haveRts = true; }
                    else if (key == "NAME") name = value;
                }
                if (!string.Equals(cachedPort, portName, StringComparison.OrdinalIgnoreCase) || !haveDtr || !haveRts) return null;
                PortProfile p = new PortProfile(); p.Name = "CACHE " + name; p.Dtr = dtr; p.Rts = rts; return p;
            }
            catch { return null; }
        }

        private static SerialPort OpenPort(string portName, bool dtr, bool rts)
        {
            SerialPort port = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
            port.Handshake = Handshake.None; port.DtrEnable = dtr; port.RtsEnable = rts; port.ReadTimeout = 100; port.WriteTimeout = 1500;
            port.Open(); port.DiscardInBuffer(); port.DiscardOutBuffer(); return port;
        }

        private static void ClosePort(SerialPort port)
        {
            if (port == null) return;
            try { if (port.IsOpen) port.Close(); } catch { }
            try { port.Dispose(); } catch { }
        }

        private static void SnapshotBaseline(SerialPort port, string state)
        {
            Log("BASELINE: programa + memoria completa + RTC + scan time.");
            if (string.Equals(state, "STOP", StringComparison.Ordinal)) BackupProgram(port, "00-baseline-program");
            else Log("BASELINE PROGRAM: pulado porque PLC esta RUN; SAFE nao altera estado.");
            SnapshotAreas(port, "00-baseline-memory", AllAreas());
            CaptureClock(port, "00-baseline-rtc");
            CaptureScan(port, "00-baseline-scan");
        }

        private static void CaptureF0(SerialPort port, string label) { ExchangeRaw(port, F0, 1800, 120, label, "status"); }

        private static void BackupProgram(SerialPort port, string prefix)
        {
            Log(prefix + ": iniciando PG38/PG34 multipagina.");
            ExchangeRaw(port, Frame38, 2200, 140, prefix + "-38", "program-read");
            StringBuilder index = new StringBuilder(); index.AppendLine("start,end_found,end_global,file");
            bool foundEnd = false;
            for (int start = 0; start < Tp02Pg34Pager.MaxProgramSteps; start += Tp02Pg34Pager.StepsPerPage)
            {
                byte[] request = Tp02Pg34Pager.BuildReadRequest(start);
                byte[] raw = ExchangeRaw(port, request, 5000, 180, prefix + "-34-" + start.ToString("0000", CultureInfo.InvariantCulture), "program-read");
                byte[] frame = FindFrame(raw, Tp02Pg34Pager.PayloadLength);
                if (frame == null) throw new InvalidDataException("PG34 sem frame LEN=F0 valido em START=" + start.ToString(CultureInfo.InvariantCulture));
                string binName = SafeName(prefix + "-page-" + start.ToString("0000", CultureInfo.InvariantCulture) + ".bin");
                File.WriteAllBytes(Path.Combine(SessionDir, binName), frame);
                int local; bool hasEnd = Tp02Pg34Pager.TryFindEnd(frame, out local);
                string global = hasEnd ? (start + local).ToString(CultureInfo.InvariantCulture) : string.Empty;
                index.AppendLine(start.ToString(CultureInfo.InvariantCulture) + "," + (hasEnd ? "1" : "0") + "," + global + "," + binName);
                if (hasEnd) { foundEnd = true; break; }
            }
            File.WriteAllText(Path.Combine(SessionDir, SafeName(prefix + "-index.csv")), index.ToString(), Encoding.UTF8);
            if (!foundEnd) throw new InvalidDataException("Backup de programa percorreu 4000 passos sem localizar F-00 END.");
        }

        private static Tp02PgMemoryProtocol.Area[] AllAreas()
        {
            return new Tp02PgMemoryProtocol.Area[] { Tp02PgMemoryProtocol.Area.X, Tp02PgMemoryProtocol.Area.Y, Tp02PgMemoryProtocol.Area.C,
                Tp02PgMemoryProtocol.Area.SC, Tp02PgMemoryProtocol.Area.V, Tp02PgMemoryProtocol.Area.D,
                Tp02PgMemoryProtocol.Area.WC, Tp02PgMemoryProtocol.Area.FL, Tp02PgMemoryProtocol.Area.WS };
        }

        private static void SnapshotAreas(SerialPort port, string prefix, Tp02PgMemoryProtocol.Area[] areas)
        {
            for (int a = 0; a < areas.Length; a++)
            {
                Tp02PgMemoryProtocol.Area area = areas[a];
                IList<byte[]> plan = Tp02PgMemoryProtocol.ReadAll(area);
                for (int i = 0; i < plan.Count; i++)
                {
                    byte[] request = plan[i];
                    string label = prefix + "-0A-" + area.ToString() + "-" + (i + 1).ToString("00", CultureInfo.InvariantCulture);
                    byte[] raw = ExchangeRaw(port, request, 4200, 140, label, "memory-read");
                    byte[] frame = FindFrame(raw, request[4]);
                    if (frame == null) throw new InvalidDataException(label + ": resposta PG0A valida nao localizada.");
                    File.WriteAllBytes(Path.Combine(SessionDir, SafeName(label + "-frame.bin")), frame);
                    Thread.Sleep(35);
                }
            }
        }

        private static byte[] ReadOne(SerialPort port, byte[] request, string label)
        {
            byte[] raw = ExchangeRaw(port, request, 3200, 120, label, "memory-read");
            byte[] frame = FindFrame(raw, request[4]);
            if (frame == null) throw new InvalidDataException(label + ": frame de leitura nao localizado.");
            return frame;
        }

        private static ushort ReadWord(SerialPort port, Tp02PgMemoryProtocol.Area area, int number, string label)
        {
            byte[] frame = ReadOne(port, Tp02PgMemoryProtocol.BuildRead(area, number, 2), label);
            return Tp02PgMemoryProtocol.DecodeWords(frame, 1)[0];
        }

        private static byte ReadByte(SerialPort port, Tp02PgMemoryProtocol.Area area, int number, string label)
        {
            byte[] frame = ReadOne(port, Tp02PgMemoryProtocol.BuildRead(area, number, 1), label);
            return Tp02PgMemoryProtocol.Payload(frame, 1)[0];
        }

        private static ushort[] CaptureClock(SerialPort port, string label)
        {
            byte[] frame = ReadOne(port, Tp02PgMemoryProtocol.ReadClock(), label);
            ushort[] v = Tp02PgMemoryProtocol.DecodeClock(frame);
            Log(label + ": sec=" + v[0] + " min=" + v[1] + " h=" + v[2] + " day=" + v[3] + " dow=" + v[4] + " mon=" + v[5] + " year=" + v[6]);
            return v;
        }

        private static ushort[] CaptureScan(SerialPort port, string label)
        {
            byte[] frame = ReadOne(port, Tp02PgMemoryProtocol.ReadScanTimes(), label);
            ushort[] v = Tp02PgMemoryProtocol.DecodeScanTimes(frame);
            Log(label + ": scan-now=" + v[0] + " min=" + v[1] + " max=" + v[2]);
            return v;
        }

        private static void RunReversibleLab(SerialPort port)
        {
            Log("LAB: iniciando comandos reversiveis e captura de respostas reais.");
            ExchangeRaw(port, Stop, 2200, 140, "10-stop-idempotent", "control");
            ExchangeRaw(port, Auth, 2200, 140, "11-auth-preflight", "auth");
            TestScratchRegister(port); TestRtcSameValue(port); TestScratchCoil(port); TestRunStop(port);
            CaptureF0(port, "19-post-lab-f0");
        }

        private static void TestScratchRegister(SerialPort port)
        {
            ushort original = ReadWord(port, Tp02PgMemoryProtocol.Area.V, 1024, "12-v1024-before");
            ushort test = (ushort)(original ^ 0xA55A);
            IList<byte[]> writes = Tp02PgMemoryProtocol.BuildRegisterWrites(Tp02PgMemoryProtocol.Area.V,
                new List<int>(new int[] { 1024 }), new List<ushort>(new ushort[] { test }));
            ExchangeRaw(port, writes[0], 2200, 140, "12-v1024-write-test", "scratch-v1024");
            ushort after = ReadWord(port, Tp02PgMemoryProtocol.Area.V, 1024, "12-v1024-readback-test");
            if (after != test) throw new InvalidDataException("V1024 nao refletiu escrita 09 de teste.");
            IList<byte[]> restore = Tp02PgMemoryProtocol.BuildRegisterWrites(Tp02PgMemoryProtocol.Area.V,
                new List<int>(new int[] { 1024 }), new List<ushort>(new ushort[] { original }));
            ExchangeRaw(port, restore[0], 2200, 140, "12-v1024-restore", "scratch-v1024");
            ushort restored = ReadWord(port, Tp02PgMemoryProtocol.Area.V, 1024, "12-v1024-readback-restore");
            if (restored != original) throw new InvalidDataException("V1024 nao foi restaurado ao valor original.");
            Log("V1024 reversivel: original=" + original.ToString("X4", CultureInfo.InvariantCulture) + " test=" + test.ToString("X4", CultureInfo.InvariantCulture) + " restore=OK.");
        }

        private static void TestRtcSameValue(SerialPort port)
        {
            ushort[] v = CaptureClock(port, "13-rtc-before-write-same");
            int[] fields = new int[7]; for (int i = 0; i < fields.Length; i++) fields[i] = v[i];
            byte[] write = Tp02PgMemoryProtocol.BuildClockWrite(fields);
            ExchangeRaw(port, write, 2200, 140, "13-rtc-write-same", "rtc-same");
            CaptureClock(port, "13-rtc-after-write-same");
        }

        private static void TestScratchCoil(SerialPort port)
        {
            byte originalByte = ReadByte(port, Tp02PgMemoryProtocol.Area.C, 2048, "14-c2048-before");
            bool original = (originalByte & 0x80) != 0;
            ExchangeRaw(port, Tp02PgMemoryProtocol.BuildSetReset(Tp02PgMemoryProtocol.Area.C, 2048, true), 2200, 140, "14-c2048-set", "scratch-c2048");
            byte setByte = ReadByte(port, Tp02PgMemoryProtocol.Area.C, 2048, "14-c2048-read-set");
            if ((setByte & 0x80) == 0) throw new InvalidDataException("C2048 nao ficou ON apos PG35 SET.");
            ExchangeRaw(port, Tp02PgMemoryProtocol.BuildSetReset(Tp02PgMemoryProtocol.Area.C, 2048, false), 2200, 140, "14-c2048-reset", "scratch-c2048");
            byte resetByte = ReadByte(port, Tp02PgMemoryProtocol.Area.C, 2048, "14-c2048-read-reset");
            if ((resetByte & 0x80) != 0) throw new InvalidDataException("C2048 nao ficou OFF apos PG35 RESET.");
            ExchangeRaw(port, Tp02PgMemoryProtocol.BuildSetReset(Tp02PgMemoryProtocol.Area.C, 2048, original), 2200, 140, "14-c2048-restore", "scratch-c2048");
            byte restoredByte = ReadByte(port, Tp02PgMemoryProtocol.Area.C, 2048, "14-c2048-read-restore");
            if (((restoredByte & 0x80) != 0) != original) throw new InvalidDataException("C2048 nao foi restaurado.");
            Log("C2048 reversivel: original=" + (original ? "ON" : "OFF") + " restore=OK.");
        }

        private static void TestRunStop(SerialPort port)
        {
            Log("RUN/STOP: saidas devem estar fisicamente desconectadas da carga.");
            ExchangeRaw(port, Run, 2200, 140, "15-run", "control"); Thread.Sleep(180);
            string state = HelloState(port, "15-run-verify");
            if (state != "RUN") Log("AVISO: RUN nao confirmado por HELLO; tentando STOP de seguranca.");
            ExchangeRaw(port, Stop, 2200, 140, "16-stop", "control"); Thread.Sleep(180);
            state = HelloState(port, "16-stop-verify");
            if (state != "STOP")
            {
                ExchangeRaw(port, Stop, 2200, 140, "16-stop-safety-retry", "control"); Thread.Sleep(200);
                state = HelloState(port, "16-stop-safety-verify2");
            }
            if (state != "STOP") throw new InvalidOperationException("CRITICO: STOP nao confirmado apos teste RUN. Coloque a chave fisica em STOP.");
            Log("RUN/STOP: retorno final STOP confirmado.");
        }

        private static string HelloState(SerialPort port, string label)
        {
            for (int i = 1; i <= 3; i++)
            {
                byte[] raw = ExchangeRaw(port, Hello, 900, 100, label + "-" + i.ToString(CultureInfo.InvariantCulture), "session");
                if (Contains(raw, HelloStop)) return "STOP";
                if (Contains(raw, HelloRun)) return "RUN";
                Thread.Sleep(100);
            }
            return string.Empty;
        }

        private static void RunDestructiveLab(SerialPort port)
        {
            Log("DESTRUCTIVE: backup baseline ja salvo. Restauracao automatica NAO sera executada.");
            if (EepromEnabled)
            {
                ExchangeRaw(port, PlcToEeprom, 5000, 250, "20-plc-to-eeprom", "eeprom");
                ExchangeRaw(port, EepromToPlc, 5000, 250, "21-eeprom-to-plc", "eeprom");
                CaptureF0(port, "21-post-eeprom-f0");
            }
            else Log("EEPROM 12/13 pulado. Use --eeprom somente com EEPROM PACK instalado na bancada.");

            ExchangeRaw(port, ClearData, 3500, 200, "30-clear-data", "destructive");
            SnapshotAreas(port, "30-after-clear-data", new Tp02PgMemoryProtocol.Area[] { Tp02PgMemoryProtocol.Area.V, Tp02PgMemoryProtocol.Area.D, Tp02PgMemoryProtocol.Area.WC, Tp02PgMemoryProtocol.Area.FL });
            ExchangeRaw(port, ClearSystem, 3500, 200, "31-clear-system", "destructive");
            SnapshotAreas(port, "31-after-clear-system", new Tp02PgMemoryProtocol.Area[] { Tp02PgMemoryProtocol.Area.SC, Tp02PgMemoryProtocol.Area.WS });
            ExchangeRaw(port, ClearProgram, 3500, 200, "32-clear-program", "destructive");
            BackupProgram(port, "32-after-clear-program");
            ExchangeRaw(port, ClearAll, 4000, 250, "33-clear-all", "destructive");
            BackupProgram(port, "33-after-clear-all-program");
            SnapshotAreas(port, "33-after-clear-all-memory", AllAreas());
            CaptureClock(port, "33-after-clear-all-rtc"); CaptureScan(port, "33-after-clear-all-scan");
            File.WriteAllText(Path.Combine(SessionDir, "RESTORE_REQUIRED.txt"),
                "A campanha DESTRUCTIVE concluiu comandos de limpeza. O TP02 foi alterado.\r\nO backup anterior esta nesta pasta, mas esta ferramenta NAO executa restore automatico.\r\nUse o fluxo de gravacao/restore controlado do OpenLadder/PC12 apos revisar as evidencias.\r\n", Encoding.UTF8);
        }

        private static byte[] ExchangeRaw(SerialPort port, byte[] request, int timeoutMs, int quietMs, string label, string context)
        {
            EnsureAllowed(request, context);
            if (port == null || !port.IsOpen) throw new InvalidOperationException("Porta serial fechada.");
            port.DiscardInBuffer(); int seq = ++Sequence;
            string stem = seq.ToString("000", CultureInfo.InvariantCulture) + "-" + SafeName(label);
            File.WriteAllBytes(Path.Combine(SessionDir, stem + "-tx.bin"), request);
            File.WriteAllText(Path.Combine(SessionDir, stem + "-tx.hex"), ToHex(request) + Environment.NewLine, Encoding.ASCII);
            Log(label + " TX: " + ToHex(request)); Stopwatch sw = Stopwatch.StartNew();
            port.Write(request, 0, request.Length); byte[] raw = ReadBurst(port, timeoutMs, quietMs); sw.Stop();
            File.WriteAllBytes(Path.Combine(SessionDir, stem + "-rx.bin"), raw);
            File.WriteAllText(Path.Combine(SessionDir, stem + "-rx.hex"), ToHex(raw) + Environment.NewLine, Encoding.ASCII);
            string parsed = DescribeFrames(raw);
            Log(label + " RX(" + sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms): " + (raw.Length == 0 ? "[]" : ToHex(raw)) + (string.IsNullOrEmpty(parsed) ? string.Empty : " | frames=" + parsed));
            CaptureEntry e = new CaptureEntry(); e.Sequence = seq; e.Label = label; e.Tx = ToHex(request); e.Rx = ToHex(raw); e.ElapsedMs = sw.ElapsedMilliseconds; e.Parsed = parsed; e.Note = context; Entries.Add(e);
            return raw;
        }

        private static void EnsureAllowed(byte[] frame, string context)
        {
            if (frame == null || frame.Length == 0) throw new InvalidOperationException("Quadro vazio bloqueado.");
            if (Same(frame, Hello)) return;
            byte cmd = frame[0];
            if (cmd == 0x37) throw new InvalidOperationException("BIOS 37 e permanentemente bloqueado nesta campanha.");
            if (cmd == 0x33) throw new InvalidOperationException("PG33 nao e retransmitido pela campanha completa; ja possui evidencia fisica separada.");
            bool safe = Same(frame, F0) || Same(frame, Frame38) || IsValidPg34Request(frame) || IsKnownRead(frame);
            if (safe) return;
            if (Mode == CampaignMode.Safe) throw new InvalidOperationException("SAFE bloqueou opcode 0x" + cmd.ToString("X2"));
            if (Same(frame, Stop) || Same(frame, Run) || Same(frame, Auth)) return;
            if (cmd == 0x09 && (context == "scratch-v1024" || context == "rtc-same")) return;
            if (cmd == 0x35 && IsScratchCoilFrame(frame)) return;
            if (Mode == CampaignMode.Lab) throw new InvalidOperationException("LAB bloqueou opcode/contexto 0x" + cmd.ToString("X2") + "/" + context);
            if (Mode == CampaignMode.DestructiveLab)
            {
                if (Same(frame, ClearProgram) || Same(frame, ClearSystem) || Same(frame, ClearAll) || Same(frame, ClearData)) return;
                if (Same(frame, EepromToPlc) || Same(frame, PlcToEeprom))
                {
                    if (!EepromEnabled) throw new InvalidOperationException("EEPROM bloqueado sem --eeprom.");
                    return;
                }
            }
            throw new InvalidOperationException("Quadro fora da allowlist do modo: " + ToHex(frame));
        }

        private static bool IsValidPg34Request(byte[] frame)
        {
            if (frame == null || frame.Length != 6 || frame[0] != 0x34 || frame[1] != 0x03 || frame[4] != 0xA0) return false;
            if (!Tp02PgProtocol.HasValidChecksum(frame)) return false;
            int start = (frame[2] << 8) | frame[3];
            return start >= 0 && start < Tp02Pg34Pager.MaxProgramSteps && (start % Tp02Pg34Pager.StepsPerPage) == 0;
        }

        private static bool IsKnownRead(byte[] frame)
        {
            if (frame == null || frame.Length != 6 || frame[0] != 0x0A || frame[1] != 0x03 || !Tp02PgProtocol.HasValidChecksum(frame)) return false;
            if (Same(frame, Tp02PgMemoryProtocol.ReadClock()) || Same(frame, Tp02PgMemoryProtocol.ReadScanTimes())) return true;
            foreach (Tp02PgMemoryProtocol.Area area in Enum.GetValues(typeof(Tp02PgMemoryProtocol.Area)))
            {
                IList<byte[]> requests = Tp02PgMemoryProtocol.ReadAll(area);
                for (int i = 0; i < requests.Count; i++) if (Same(frame, requests[i])) return true;
            }
            if (Same(frame, Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.V, 1024, 2))) return true;
            if (Same(frame, Tp02PgMemoryProtocol.BuildRead(Tp02PgMemoryProtocol.Area.C, 2048, 1))) return true;
            return false;
        }

        private static bool IsScratchCoilFrame(byte[] frame)
        {
            byte[] set = Tp02PgMemoryProtocol.BuildSetReset(Tp02PgMemoryProtocol.Area.C, 2048, true);
            byte[] reset = Tp02PgMemoryProtocol.BuildSetReset(Tp02PgMemoryProtocol.Area.C, 2048, false);
            return Same(frame, set) || Same(frame, reset);
        }

        private static byte[] ReadBurst(SerialPort port, int timeoutMs, int quietMs)
        {
            List<byte> bytes = new List<byte>(); DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs); DateTime last = DateTime.MinValue;
            while (DateTime.UtcNow < deadline)
            {
                int count = port.BytesToRead;
                if (count > 0)
                {
                    byte[] chunk = new byte[count]; int got = port.Read(chunk, 0, chunk.Length);
                    for (int i = 0; i < got; i++) bytes.Add(chunk[i]); last = DateTime.UtcNow;
                }
                else if (bytes.Count > 0 && last != DateTime.MinValue && (DateTime.UtcNow - last).TotalMilliseconds >= quietMs) break;
                Thread.Sleep(10);
            }
            return bytes.ToArray();
        }

        private static byte[] FindFrame(byte[] raw, int expectedLen)
        {
            if (raw == null) return null; int total = expectedLen + 3;
            for (int i = 0; i <= raw.Length - total; i++)
            {
                if (raw[i + 1] != (byte)expectedLen) continue;
                int sum = 0; for (int j = 0; j < total; j++) sum = (sum + raw[i + j]) & 0xFF;
                if (sum != 0xFF) continue;
                byte[] frame = new byte[total]; Buffer.BlockCopy(raw, i, frame, 0, total); return frame;
            }
            return null;
        }

        private static string DescribeFrames(byte[] raw)
        {
            if (raw == null || raw.Length < 3) return string.Empty; List<string> frames = new List<string>();
            for (int i = 0; i <= raw.Length - 3; i++)
            {
                int len = raw[i + 1]; int total = len + 3; if (i + total > raw.Length) continue;
                int sum = 0; for (int j = 0; j < total; j++) sum = (sum + raw[i + j]) & 0xFF;
                if (sum != 0xFF) continue;
                byte[] frame = new byte[total]; Buffer.BlockCopy(raw, i, frame, 0, total);
                frames.Add("@" + i.ToString(CultureInfo.InvariantCulture) + ":" + ToHex(frame)); i += total - 1;
            }
            return string.Join(" ; ", frames.ToArray());
        }

        private static bool Contains(byte[] raw, byte[] sequence)
        {
            if (raw == null || sequence == null || sequence.Length == 0 || raw.Length < sequence.Length) return false;
            for (int i = 0; i <= raw.Length - sequence.Length; i++)
            {
                bool ok = true; for (int j = 0; j < sequence.Length; j++) if (raw[i + j] != sequence[j]) { ok = false; break; }
                if (ok) return true;
            }
            return false;
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true;
        }

        private static void WriteReport(string result, string failure)
        {
            if (string.IsNullOrEmpty(SessionDir)) return;
            StringBuilder txt = new StringBuilder();
            txt.AppendLine("TP02 FULL PROTOCOL CAPTURE"); txt.AppendLine("Timestamp=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            txt.AppendLine("Result=" + result); txt.AppendLine("Port=" + PortName); txt.AppendLine("Mode=" + Mode.ToString().ToUpperInvariant());
            txt.AppendLine("Profile=" + (ActiveProfile == null ? string.Empty : ActiveProfile.Name)); txt.AppendLine("EEPROM=" + (EepromEnabled ? "ENABLED" : "DISABLED"));
            txt.AppendLine("BIOS37=NEVER_SENT"); txt.AppendLine("PG33=NOT_RETRANSMITTED_EXISTING_PHYSICAL_EVIDENCE");
            if (!string.IsNullOrEmpty(failure)) txt.AppendLine("Failure=" + failure.Replace("\r", " ").Replace("\n", " | ")); txt.AppendLine();
            for (int i = 0; i < Entries.Count; i++)
            {
                CaptureEntry e = Entries[i];
                txt.AppendLine(e.Sequence.ToString("000", CultureInfo.InvariantCulture) + " | " + e.Label + " | " + e.ElapsedMs.ToString(CultureInfo.InvariantCulture) + "ms | TX=" + e.Tx + " | RX=" + e.Rx + " | frames=" + e.Parsed + " | context=" + e.Note);
            }
            File.WriteAllText(Path.Combine(SessionDir, "protocol-report.txt"), txt.ToString(), Encoding.UTF8);
            StringBuilder csv = new StringBuilder(); csv.AppendLine("sequence,label,elapsed_ms,tx,rx,parsed_frames,context");
            for (int i = 0; i < Entries.Count; i++)
            {
                CaptureEntry e = Entries[i];
                csv.AppendLine(e.Sequence.ToString(CultureInfo.InvariantCulture) + "," + Csv(e.Label) + "," + e.ElapsedMs.ToString(CultureInfo.InvariantCulture) + "," + Csv(e.Tx) + "," + Csv(e.Rx) + "," + Csv(e.Parsed) + "," + Csv(e.Note));
            }
            File.WriteAllText(Path.Combine(SessionDir, "responses.csv"), csv.ToString(), Encoding.UTF8);
            StringBuilder readme = new StringBuilder();
            readme.AppendLine("Esta pasta contem TX/RX bruto byte a byte da campanha TP02.");
            readme.AppendLine("SAFE: somente CON-ICB/F0/38/34/0A.");
            readme.AppendLine("LAB: adiciona 01/02/09/14/35 com testes reversiveis em V1024/C2048/RTC.");
            readme.AppendLine("DESTRUCTIVE: adiciona 03/04/0F/11 e, com --eeprom, 12/13. Nao restaura automaticamente.");
            readme.AppendLine("37 BIOS nunca e transmitido. 33 PG33 nao e repetido nesta ferramenta.");
            File.WriteAllText(Path.Combine(SessionDir, "README.txt"), readme.ToString(), Encoding.UTF8);
        }

        private static string Csv(string value) { if (value == null) value = string.Empty; return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\""; }
        private static void Log(string text)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + text; Console.WriteLine(line);
            if (!string.IsNullOrEmpty(SessionDir)) File.AppendAllText(Path.Combine(SessionDir, "session.log"), line + Environment.NewLine, Encoding.UTF8);
        }
        private static string SafeName(string value)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < value.Length; i++) { char c = value[i]; if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.') b.Append(c); else b.Append('_'); }
            return b.ToString();
        }
        private static string ToHex(byte[] data) { if (data == null || data.Length == 0) return string.Empty; return BitConverter.ToString(data).Replace('-', ' '); }
        private static bool HasArg(string[] args, string name) { for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true; return false; }
        private static string ArgValue(string[] args, string name)
        {
            string prefix = name + "=";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return args[i].Substring(prefix.Length).Trim();
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) return args[i + 1].Trim();
            }
            return string.Empty;
        }

        private static int SelfTest()
        {
            Mode = CampaignMode.Safe;
            EnsureAllowed(Hello, "session"); EnsureAllowed(F0, "status"); EnsureAllowed(Frame38, "program-read"); EnsureAllowed(Tp02Pg34Pager.BuildReadRequest(0), "program-read"); EnsureAllowed(Tp02PgMemoryProtocol.ReadClock(), "memory-read");
            bool blocked37 = false; try { EnsureAllowed(new byte[] { 0x37, 0x02, 0xFF, 0xFF, 0xC8 }, "bios"); } catch { blocked37 = true; }
            if (!blocked37) throw new Exception("Self-test: BIOS 37 nao foi bloqueado.");
            bool blockedRun = false; try { EnsureAllowed(Run, "control"); } catch { blockedRun = true; }
            if (!blockedRun) throw new Exception("Self-test: SAFE permitiu RUN.");
            Mode = CampaignMode.Lab; EnsureAllowed(Run, "control"); EnsureAllowed(Stop, "control"); EnsureAllowed(Auth, "auth");
            byte[] scratch09 = Tp02PgMemoryProtocol.BuildRegisterWrites(Tp02PgMemoryProtocol.Area.V, new List<int>(new int[] { 1024 }), new List<ushort>(new ushort[] { 0x1234 }))[0];
            EnsureAllowed(scratch09, "scratch-v1024"); EnsureAllowed(Tp02PgMemoryProtocol.BuildSetReset(Tp02PgMemoryProtocol.Area.C, 2048, true), "scratch-c2048");
            bool blockedClear = false; try { EnsureAllowed(ClearAll, "destructive"); } catch { blockedClear = true; }
            if (!blockedClear) throw new Exception("Self-test: LAB permitiu Clear All.");
            Mode = CampaignMode.DestructiveLab; EnsureAllowed(ClearProgram, "destructive"); EnsureAllowed(ClearSystem, "destructive"); EnsureAllowed(ClearData, "destructive"); EnsureAllowed(ClearAll, "destructive");
            EepromEnabled = false; bool blockedEeprom = false; try { EnsureAllowed(PlcToEeprom, "eeprom"); } catch { blockedEeprom = true; }
            if (!blockedEeprom) throw new Exception("Self-test: EEPROM sem flag nao foi bloqueado.");
            EepromEnabled = true; EnsureAllowed(PlcToEeprom, "eeprom"); EnsureAllowed(EepromToPlc, "eeprom");
            Console.WriteLine("TP02 Full Protocol Capture self-test: PASS");
            Console.WriteLine("modes=SAFE+LAB+DESTRUCTIVE; bios37=BLOCKED; pg33=BLOCKED; hardware=NOT USED");
            return 0;
        }
    }
}
