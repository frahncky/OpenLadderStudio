using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenLadderStudio.Core;
using Area = OpenLadderStudio.Core.Tp02PgMemoryProtocol.Area;

/// <summary>
/// Confere os construtores do Core contra os quadros que o PC12 v2.1 montou
/// no emulador (Data/pc12-golden-vectors.tsv, gerado por
/// scripts/export_pc12_golden_vectors.py). Offline; nenhum TX.
/// </summary>
internal static class Tp02Pc12GoldenVectorsSelfTest
{
    private const string Rejected = "REJEITADO";
    private const string DataFile = "pc12-golden-vectors.tsv";

    private static int checks;
    private static int failures;
    private static int divergences;
    private static int withoutApi;

    private sealed class Divergence
    {
        public readonly string OpenLadder;
        public readonly string Reason;
        public Divergence(string openLadder, string reason) { OpenLadder = openLadder; Reason = reason; }
    }

    // Diferenças conhecidas entre o OpenLadder e o PC12. Cada uma declara o
    // resultado exato do OpenLadder; se o PC12 passar a coincidir, o teste
    // falha para que a entrada seja removida.
    private static readonly Dictionary<string, Divergence> Known = BuildKnown();

    private static Dictionary<string, Divergence> BuildKnown()
    {
        Dictionary<string, Divergence> known = new Dictionary<string, Divergence>();
        const string flHex = "Anomalia nativa do ramo HEX do PC12: monta 21 bytes por FL e lê além do NUL. O OpenLadder envia os 20 bytes declarados.";
        known["FLBATCH HEX 1"] = new Divergence("bytes=26", flHex);
        known["FLBATCH HEX 9"] = new Divergence("bytes=210", flHex);
        known["FLBATCH HEX 10"] = new Divergence("bytes=233", flHex);
        known["FLBATCH HEX 11"] = new Divergence("bytes=233,26", flHex);
        const string ws = "WS039/WS040 ficam fora da seleção de escrita de sistema identificada; o OpenLadder bloqueia.";
        known["REG WS 39 4660"] = new Divergence(Rejected, ws);
        known["REG WS 39 32768"] = new Divergence(Rejected, ws);
        known["REG WS 40 43981"] = new Divergence(Rejected, ws);
        known["REG WS 40 65535"] = new Divergence(Rejected, ws);
        known["CLOCKW 59,34,12,25,7,8,26"] = new Divergence(Rejected,
            "Dia da semana 7 fora da faixa documentada 0..6; o OpenLadder recusa a escrita.");
        known["PAYLOAD 20 00 DF"] = new Divergence("REJEITA",
            "PENDENTE DE BANCADA: o PC12 aceita status 0x20 (só marca um indicador); o OpenLadder recusa todo status diferente de 00.");
        return known;
    }

    private static void Fail(string message)
    {
        failures++;
        Console.Error.WriteLine("FALHA: " + message);
    }

    private static string Hex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace('-', ' ');
    }

    private static byte[] Bytes(string[] parts, int first)
    {
        byte[] result = new byte[parts.Length - first];
        for (int i = first; i < parts.Length; i++) result[i - first] = Convert.ToByte(parts[i], 16);
        return result;
    }

    private static byte[] HexList(string csv)
    {
        return Bytes(csv.Split(','), 0);
    }

    private static List<int> Ints(string csv)
    {
        List<int> result = new List<int>();
        foreach (string part in csv.Split(',')) result.Add(int.Parse(part));
        return result;
    }

    private static string Word(Tp02MachineWord w)
    {
        return w.High.ToString("X2") + " " + w.Low.ToString("X2") + "|" + w.External.ToString("X2");
    }

    private static string Sizes(IList<byte[]> frames)
    {
        StringBuilder sb = new StringBuilder("bytes=");
        for (int i = 0; i < frames.Count; i++) sb.Append(i == 0 ? "" : ",").Append(frames[i].Length);
        return sb.ToString();
    }

    private static Area ParseArea(string text)
    {
        return (Area)Enum.Parse(typeof(Area), text);
    }

    private static Tp02Pg33DryRunInstruction AddD2D1K10()
    {
        return new Tp02Pg33DryRunInstruction(
            Tp02TargetCompiler.EncodeFunctionPrefix("F-13w"),
            Tp02TargetCompiler.EncodeFunctionOperand("D", 2),
            Tp02TargetCompiler.EncodeFunctionOperand("D", 1),
            Tp02TargetCompiler.EncodeLiteral16(10));
    }

    private static string Execute(string command)
    {
        string[] p = command.Split(' ');
        switch (p[0])
        {
            case "SR":
                return Hex(Tp02PgMemoryProtocol.BuildSetReset(ParseArea(p[1]), int.Parse(p[2]), p[3] == "1"));
            case "REG":
                return Hex(Tp02PgMemoryProtocol.BuildRegisterWrites(ParseArea(p[1]),
                    new int[] { int.Parse(p[2]) }, new ushort[] { ushort.Parse(p[3]) })[0]);
            case "REGS":
            {
                List<ushort> values = new List<ushort>();
                foreach (int v in Ints(p[3])) values.Add((ushort)v);
                IList<byte[]> frames = Tp02PgMemoryProtocol.BuildRegisterWrites(ParseArea(p[1]), Ints(p[2]), values);
                if (frames.Count != 1) return "quadros=" + frames.Count;
                return Hex(frames[0]);
            }
            case "BATCH":
            {
                int n = int.Parse(p[2]);
                List<int> numbers = new List<int>();
                List<ushort> values = new List<ushort>();
                for (int i = 1; i <= n; i++) { numbers.Add(i); values.Add(0); }
                return Sizes(Tp02PgMemoryProtocol.BuildRegisterWrites(ParseArea(p[1]), numbers, values));
            }
            case "FLBATCH":
            {
                // O Core tem um único caminho FL: ASCII e HEX do PC12 comparam com ele.
                int n = int.Parse(p[2]);
                List<int> numbers = new List<int>();
                List<byte[]> values = new List<byte[]>();
                for (int i = 1; i <= n; i++) { numbers.Add(i); values.Add(new byte[20]); }
                return Sizes(Tp02PgMemoryProtocol.BuildFileWrites(numbers, values));
            }
            case "READ":
                return Hex(Tp02PgMemoryProtocol.BuildRead(ParseArea(p[1]), int.Parse(p[2]), int.Parse(p[3])));
            case "MULTI":
            {
                List<byte[]> reads = new List<byte[]>();
                for (int i = 1; i < p.Length; i++)
                {
                    string[] q = p[i].Split(':');
                    reads.Add(Tp02PgMemoryProtocol.BuildRead(ParseArea(q[0]), int.Parse(q[1]), int.Parse(q[2])));
                }
                return Hex(Tp02PgMemoryProtocol.BuildMultipleRead(reads));
            }
            case "PAGES":
            {
                IList<byte[]> pages = Tp02PgMemoryProtocol.ReadAll(ParseArea(p[1]));
                return "n=" + pages.Count + " " + Hex(pages[0]) + " / " + Hex(pages[pages.Count - 1]);
            }
            case "CLOCKREAD": return Hex(Tp02PgMemoryProtocol.ReadClock());
            case "SCANREAD": return Hex(Tp02PgMemoryProtocol.ReadScanTimes());
            case "CLOCKW": return Hex(Tp02PgMemoryProtocol.BuildClockWrite(Ints(p[1]).ToArray()));
            case "SCANDEC":
            {
                ushort[] values = Tp02PgMemoryProtocol.DecodeScanTimes(Bytes(p, 1));
                return "valores=" + values[0] + "," + values[1] + "," + values[2];
            }
            case "PAYLOAD":
                try { Tp02PgMemoryProtocol.Payload(Bytes(p, 1), -1); return "ACEITA"; }
                catch (ArgumentException) { return "REJEITA"; }
            case "SHORT": return Hex(Tp02PgProtocol.BuildShortFrame(Convert.ToByte(p[1], 16)));
            case "P34": return Hex(Tp02Pg34Pager.BuildReadRequest(int.Parse(p[1])));
            case "OPD": return Word(Tp02TargetCompiler.EncodeFunctionOperand(p[1], int.Parse(p[2])));
            case "LIT": return Word(Tp02TargetCompiler.EncodeLiteral16(int.Parse(p[1])));
            case "SPB": return Word(Tp02TargetCompiler.EncodeSpecialBitOperand(p[1], int.Parse(p[2])));
            case "PFX": return Word(Tp02TargetCompiler.EncodeFunctionPrefix(p[1]));
            case "PG33RAW":
                return Hex(Tp02Pg33DryRunFrame.BuildCandidate(Convert.ToInt32(p[1], 16), HexList(p[2]), HexList(p[3])));
            case "PG33ADD":
            {
                List<Tp02Pg33DryRunInstruction> program = new List<Tp02Pg33DryRunInstruction>();
                for (int i = int.Parse(p[1]); i > 0; i--) program.Add(AddD2D1K10());
                IList<Tp02Pg33DryRunBlock> blocks = Tp02Pg33DryRunProgram.BuildBlocks(0, program);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < blocks.Count; i++)
                {
                    byte[] f = blocks[i].Frame;
                    if (i > 0) sb.Append(" ; ");
                    sb.Append("bytes=").Append(f.Length)
                      .Append(" LEN=").Append(f[1].ToString("X2"))
                      .Append(" start=").Append(blocks[i].StartStep.ToString("X4"))
                      .Append(" HL=").Append(f[5].ToString("X2"))
                      .Append(" chk=").Append(f[f.Length - 1].ToString("X2"));
                }
                return sb.ToString();
            }
            default:
                throw new InvalidOperationException("comando desconhecido no arquivo de vetores: " + p[0]);
        }
    }

    private static string Run(string command)
    {
        try { return Execute(command); }
        catch (ArgumentException) { return Rejected; }
    }

    private static string FindDataFile(string[] args)
    {
        if (args.Length > 0) return args[0];
        string relative = Path.Combine(Path.Combine(Path.Combine("tests", "OpenLadderStudio.Core.Tests"), "Data"), DataFile);
        foreach (string start in new string[] { Environment.CurrentDirectory, AppDomain.CurrentDomain.BaseDirectory })
        {
            DirectoryInfo dir = new DirectoryInfo(start);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        return relative;
    }

    private static void Verify(string line)
    {
        string[] cols = line.Split('\t');
        if (cols.Length != 4) { Fail("linha malformada: " + line); return; }
        string group = cols[0], command = cols[1], pc12 = cols[2], label = group + " / " + cols[3];
        if (command.StartsWith("SEM_API")) { withoutApi++; return; }

        checks++;
        string actual;
        try { actual = Run(command); }
        catch (Exception e) { Fail(label + ": exceção " + e.GetType().Name + ": " + e.Message); return; }

        Divergence known;
        if (Known.TryGetValue(command, out known))
        {
            divergences++;
            if (actual == pc12)
                Fail(label + ": agora coincide com o PC12; remova a divergência conhecida de " + command);
            else if (actual != known.OpenLadder)
                Fail(label + ": divergência conhecida mudou.\n  esperado OpenLadder: " + known.OpenLadder + "\n  obtido:              " + actual);
            return;
        }
        if (actual != pc12)
            Fail(label + " [" + command + "]\n  PC12      : " + pc12 + "\n  OpenLadder: " + actual);
    }

    public static int Main(string[] args)
    {
        string path = FindDataFile(args);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine("FALHA: arquivo de vetores não encontrado: " + path);
            return 1;
        }
        Dictionary<string, bool> seen = new Dictionary<string, bool>();
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            if (raw.Length == 0 || raw[0] == '#') continue;
            string[] cols = raw.Split('\t');
            if (cols.Length > 1) seen[cols[1]] = true;
            Verify(raw);
        }
        foreach (string command in Known.Keys)
            if (!seen.ContainsKey(command)) Fail("divergência conhecida sem vetor correspondente: " + command);
        if (checks < 300) Fail("poucos vetores conferidos (" + checks + "); arquivo truncado?");

        Console.WriteLine("Tp02Pc12GoldenVectorsSelfTest: " + checks + " vetores PC12; " + divergences
            + " divergências conhecidas; " + withoutApi + " sem API; " + failures + " falhas (offline; nenhum TX).");
        return failures == 0 ? 0 : 1;
    }
}
