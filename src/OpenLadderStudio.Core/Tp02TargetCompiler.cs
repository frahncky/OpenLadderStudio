using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenLadderStudio.Core
{
    /// <summary>
    /// Palavra de programa TP02: HIGH, LOW e EXT (3 bytes / 6 hex).
    /// O mapa foi reconstruído por análise estáica do PC12 2.1 original.
    /// </summary>
    internal struct Tp02MachineWord
    {
        public byte High;
        public byte Low;
        public byte External;

        public Tp02MachineWord(byte high, byte low, byte external)
        {
            High = high;
            Low = low;
            External = external;
        }

        public string ToHex()
        {
            return High.ToString("X2", CultureInfo.InvariantCulture) +
                   Low.ToString("X2", CultureInfo.InvariantCulture) +
                   External.ToString("X2", CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return ToHex();
        }
    }

    internal sealed class Tp02FunctionSpec
    {
        public readonly string Key;
        public readonly string Mnemonic;
        public readonly Tp02MachineWord Prefix;
        public readonly int Steps;
        public readonly string[] OperandModes;

        public Tp02FunctionSpec(string key, string mnemonic, Tp02MachineWord prefix, int steps, string[] operandModes)
        {
            Key = key;
            Mnemonic = mnemonic;
            Prefix = prefix;
            Steps = steps;
            OperandModes = operandModes == null ? new string[0] : operandModes;
        }
    }

    /// <summary>
    /// Compilador experimental do formato de máquina WEG/TECO TP02.
    ///
    /// Evidência:
    /// - Host Protocol, RBP/WBP e checksum: documentação WEG;
    /// - opcodes e endereçamento: análise estática reproduzível do PC12 2.1;
    /// - transmissão WBP real: deliberadamente ausente até validação em hardware.
    ///
    /// Esta classe nunca abre porta serial. BuildWbpDryRun apenas monta o quadro.
    /// </summary>
    internal static class Tp02TargetCompiler
    {
        private static readonly Dictionary<string, byte> BitOpcodes = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, byte> BitDeviceBase = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, byte> FunctionDeviceBase = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, byte> SpecialBitDeviceBase = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Tp02FunctionSpec> Functions = new Dictionary<string, Tp02FunctionSpec>(StringComparer.OrdinalIgnoreCase);

        static Tp02TargetCompiler()
        {
            BitOpcodes["STR"] = 0x10;
            BitOpcodes["STR NOT"] = 0x18;
            BitOpcodes["AND"] = 0x20;
            BitOpcodes["AND NOT"] = 0x28;
            BitOpcodes["OR"] = 0x30;
            BitOpcodes["OR NOT"] = 0x38;
            BitOpcodes["OUT"] = 0x40;

            BitDeviceBase["X"] = 0x00;
            BitDeviceBase["Y"] = 0x20;
            BitDeviceBase["C"] = 0x40;

            // Forma normal de operando das funções de aplicação do PC12.
            FunctionDeviceBase["Y"] = 0xC0;
            FunctionDeviceBase["C"] = 0xC8;
            FunctionDeviceBase["X"] = 0xD0;
            FunctionDeviceBase["WY"] = 0xD8;
            FunctionDeviceBase["WX"] = 0xE0;
            FunctionDeviceBase["V"] = 0xE8;
            FunctionDeviceBase["D"] = 0xF0;
            FunctionDeviceBase["WC"] = 0xF8;

            // Modo alternativo de bit individual usado por SET/RST e funções específicas.
            SpecialBitDeviceBase["X"] = 0xC0;
            SpecialBitDeviceBase["Y"] = 0xC8;
            SpecialBitDeviceBase["C"] = 0xD0;

            AddFunction("F-00", "End", 0x00, 0x70, 0x00, 1);
            AddFunction("F-01", "MCS", 0x01, 0x70, 0x00, 1);
            AddFunction("F-02", "MCR", 0x02, 0x70, 0x00, 1);
            AddFunction("F-03", "JCS", 0x03, 0x70, 0x00, 1);
            AddFunction("F-04", "JCR", 0x04, 0x70, 0x00, 1);
            AddFunction("F-05", "-|^|-", 0x05, 0x70, 0x00, 1);
            AddFunction("F-06", "-|v|-", 0x06, 0x70, 0x00, 1);
            AddFunction("F-07", "SKIP", 0x07, 0x70, 0x00, 1);
            AddFunction("F-08", "ENDS", 0x08, 0x70, 0x00, 1);
            AddFunction("F-09", "SWAP", 0x09, 0x71, 0x00, 2, "normal");
            AddFunction("F-09w", "SWAP", 0x09, 0x75, 0x00, 2, "normal");
            AddFunction("F-10", "SFR", 0x0A, 0x71, 0x00, 2, "normal");
            AddFunction("F-10w", "SFR", 0x0A, 0x75, 0x00, 2, "normal");
            AddFunction("F-11", "XFER", 0x0B, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-11w", "XFER", 0x0B, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-11d", "XFER", 0x0B, 0xF2, 0x00, 3, "normal", "normal");
            AddFunction("F-12", "BCD", 0x0C, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-12w", "BCD", 0x0C, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-12d", "BCD", 0x0C, 0xF2, 0x00, 3, "normal", "normal");
            AddFunction("F-13", "ADD", 0x0D, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-13w", "ADD", 0x0D, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-13d", "ADD", 0x0D, 0xF3, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-14", "SUB", 0x0E, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-14w", "SUB", 0x0E, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-14d", "SUB", 0x0E, 0xF3, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-15", "CMP", 0x0F, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-15w", "CMP", 0x0F, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-15d", "CMP", 0x0F, 0xF2, 0x00, 3, "normal", "normal");
            AddFunction("F-16w", "CNT", 0x10, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-17", "BIN", 0x11, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-17w", "BIN", 0x11, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-17d", "BIN", 0x11, 0xF2, 0x00, 3, "normal", "normal");
            AddFunction("F-18w", "MUL", 0x12, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-18d", "MUL", 0x12, 0xF3, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-19w", "DIV", 0x13, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-19d", "DIV", 0x13, 0xF3, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-20", "ANL", 0x14, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-20w", "ANL", 0x14, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-21", "ORL", 0x15, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-21w", "ORL", 0x15, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-22", "XRL", 0x16, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-22w", "XRL", 0x16, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-23", "SET", 0x17, 0x71, 0x00, 2, "bit");
            AddFunction("F-24", "RST", 0x18, 0x71, 0x00, 2, "bit");
            AddFunction("F-25w", "INC", 0x19, 0x75, 0x00, 2, "normal");
            AddFunction("F-26w", "DEC", 0x1A, 0x75, 0x00, 2, "normal");
            AddFunction("F-27", "BITS", 0x1B, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-27w", "BITS", 0x1B, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-30w", "IDXT", 0x1E, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-31w", "IDXF", 0x1F, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-32w", "nTRF", 0x20, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-33:2", "TEXT", 0x21, 0x71, 0x00, 2, "normal");
            AddFunction("F-33:3", "TEXT", 0x21, 0x72, 0x00, 3, "normal", "normal");
            AddFunction("F-33w:2", "TEXT", 0x21, 0x75, 0x00, 2, "normal");
            AddFunction("F-33w:3", "TEXT", 0x21, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-34", "SCLK", 0x22, 0x70, 0x00, 1);
            AddFunction("F-39", "DCOD", 0x27, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-40", "ECOD", 0x28, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-41", "INV", 0x29, 0x70, 0x00, 1);
            AddFunction("F-42", "LB", 0x2A, 0x78, 0x00, 1, "label-embedded");
            AddFunction("F-43", "JMP", 0x2B, 0x71, 0x00, 2, "label-word");
            AddFunction("F-44", "CALL", 0x2C, 0x71, 0x00, 2, "label-word");
            AddFunction("F-45", "RET", 0x2D, 0x70, 0x00, 1);
            AddFunction("F-46", "FOR", 0x2E, 0x71, 0x00, 2, "normal");
            AddFunction("F-47", "NEXT", 0x2F, 0x70, 0x00, 1);
            AddFunction("F-48", "IORF", 0x30, 0x71, 0x00, 2, "normal");
            AddFunction("F-49", "IORB", 0x31, 0x71, 0x00, 2, "bit");
            AddFunction("F-50w", "STMR", 0x32, 0x77, 0x00, 4, "bit", "normal", "normal");
            AddFunction("F-51", "ZRST", 0x33, 0x72, 0x00, 3, "bit", "normal");
            AddFunction("F-51w", "ZRST", 0x33, 0x76, 0x00, 3, "normal", "normal");
            AddFunction("F-52", "TENK", 0x34, 0x73, 0x00, 4, "normal", "bit", "bit");
            AddFunction("F-53", "HEXK", 0x35, 0x73, 0x00, 4, "normal", "bit", "bit");
            AddFunction("F-54", "MTRX", 0x36, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-54w", "MTRX", 0x36, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-54d", "MTRX", 0x36, 0xF3, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-55", "DSW", 0x37, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-55w", "DSW", 0x37, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-56", "SEGO", 0x38, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-57w", "BCMP", 0x39, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-58", "RXD", 0x3A, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-59", "TXD", 0x3B, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-60", "TSET", 0x3C, 0x73, 0x00, 4, "bit", "normal", "normal");
            AddFunction("F-61", "TRST", 0x3D, 0x73, 0x00, 4, "bit", "normal", "normal");
            AddFunction("F-62", "ASCI", 0x3E, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-63", "HEX", 0x3F, 0x73, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-64w", "RTD", 0x40, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-65w", "TMJ", 0x41, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-66w", "TMK", 0x42, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-67w", "TMT", 0x43, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-68w", "PID", 0x44, 0x77, 0x00, 4, "normal", "normal", "normal");
            AddFunction("F-71w", "INVC", 0x47, 0x77, 0x00, 4, "normal", "normal", "normal");
        }

        private static void AddFunction(string key, string mnemonic, byte high, byte low, byte external, int steps, params string[] operandModes)
        {
            Functions[key] = new Tp02FunctionSpec(key, mnemonic, new Tp02MachineWord(high, low, external), steps, operandModes);
        }

        public static Tp02MachineWord EncodeBitInstruction(string operation, string device, int number)
        {
            if (string.Equals(operation, "AND STR", StringComparison.OrdinalIgnoreCase))
                return new Tp02MachineWord(0x00, 0x01, 0x00);
            if (string.Equals(operation, "OR STR", StringComparison.OrdinalIgnoreCase))
                return new Tp02MachineWord(0x00, 0x02, 0x00);

            byte opcode;
            byte deviceBase;
            if (!BitOpcodes.TryGetValue(operation ?? string.Empty, out opcode))
                throw new ArgumentException("Instrução bit TP02 não suportada: " + operation, "operation");
            if (!BitDeviceBase.TryGetValue(device ?? string.Empty, out deviceBase))
                throw new ArgumentException("Dispositivo bit TP02 não suportado: " + device, "device");
            if (number < 1)
                throw new ArgumentOutOfRangeException("number", "Endereços TP02 são baseados em 1.");

            int k = number - 1;
            int bit = k & 0x07;
            int group = k >> 3;

            byte high = (byte)(deviceBase | (group & 0x1F));
            byte low = (byte)(opcode | bit);
            byte external = (byte)((group >> 1) & 0xF0);
            return new Tp02MachineWord(high, low, external);
        }

        public static Tp02MachineWord EncodeNop()
        {
            return new Tp02MachineWord(0x00, 0x00, 0x00);
        }

        public static Tp02MachineWord EncodeTimer(int number)
        {
            if (number < 1) throw new ArgumentOutOfRangeException("number");
            int i = number - 1;
            return new Tp02MachineWord((byte)(i & 0x7F), (byte)(0x60 | ((i >> 7) & 0x07)), 0x00);
        }

        public static Tp02MachineWord EncodeCounter(int number)
        {
            if (number < 1) throw new ArgumentOutOfRangeException("number");
            int i = number - 1;
            return new Tp02MachineWord((byte)(i & 0x7F), (byte)(0x68 | ((i >> 7) & 0x07)), 0x00);
        }

        public static Tp02MachineWord EncodeLiteral16(int value)
        {
            if (value < 0 || value > 0xFFFF) throw new ArgumentOutOfRangeException("value");
            int highValue = (value >> 8) & 0xFF;
            int lowValue = value & 0xFF;

            byte high = (byte)(0x80 | ((highValue & 0x0F) << 1) | ((lowValue >> 7) & 0x01));
            byte low = (byte)(lowValue & 0x7F);
            byte external = (byte)(highValue & 0xF0);
            return new Tp02MachineWord(high, low, external);
        }

        public static Tp02MachineWord EncodeFunctionOperand(string device, int number)
        {
            byte deviceBase;
            if (!FunctionDeviceBase.TryGetValue(device ?? string.Empty, out deviceBase))
                throw new ArgumentException("Operando de função TP02 não suportado: " + device, "device");
            if (number < 1) throw new ArgumentOutOfRangeException("number");

            string upper = device.ToUpperInvariant();
            int index = number - 1;
            if (upper == "X" || upper == "Y" || upper == "C") index /= 8;
            if (index > 0x7FF) throw new ArgumentOutOfRangeException("number", "Índice excede o intervalo reconstruído.");

            int h = (index >> 8) & 0xFF;
            int l = index & 0xFF;
            int external = 0;
            if ((h >> 2) == 1)
            {
                h &= ~0x04;
                external |= 0x10;
            }

            byte high = (byte)(deviceBase | ((h << 1) & 0x0E) | ((l >> 7) & 0x01));
            byte low = (byte)(l & 0x7F);

            // Dois escapes observados explicitamente no encoder original do PC12.
            if (upper == "D" && (number == 0x851 || number == 0x857)) external |= 0x20;

            return new Tp02MachineWord(high, low, (byte)external);
        }

        public static Tp02MachineWord EncodeSpecialBitOperand(string device, int number)
        {
            byte deviceBase;
            if (!SpecialBitDeviceBase.TryGetValue(device ?? string.Empty, out deviceBase))
                throw new ArgumentException("Operando especial aceita somente X/Y/C.", "device");
            if (number < 1) throw new ArgumentOutOfRangeException("number");

            int index = number - 1;
            int bit = index & 0x07;
            int group = index >> 3;
            if (group > 0x7FF) throw new ArgumentOutOfRangeException("number");

            byte high = (byte)(deviceBase | bit);
            byte low = (byte)(0x80 | (group & 0x7F));
            byte external = (byte)(((group >> 7) & 0x0F) << 4);
            return new Tp02MachineWord(high, low, external);
        }

        public static bool TryGetFunction(string key, out Tp02FunctionSpec spec)
        {
            return Functions.TryGetValue(key ?? string.Empty, out spec);
        }

        public static Tp02FunctionSpec GetFunction(string key)
        {
            if (string.Equals(key, "F-33", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("F-33 possui duas formas no PC12; use F-33:2 ou F-33:3.", "key");
            if (string.Equals(key, "F-33w", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("F-33w possui duas formas no PC12; use F-33w:2 ou F-33w:3.", "key");

            Tp02FunctionSpec spec;
            if (!Functions.TryGetValue(key ?? string.Empty, out spec))
                throw new ArgumentException("Função TP02 ainda não mapeada: " + key, "key");
            return spec;
        }

        public static Tp02MachineWord EncodeFunctionPrefix(string key)
        {
            return GetFunction(key).Prefix;
        }

        public static Tp02MachineWord EncodeLabel(int label)
        {
            if (label < 1 || label > 128) throw new ArgumentOutOfRangeException("label");
            int i = label - 1;
            return new Tp02MachineWord(
                0x2A,
                (byte)(0x78 | (i & 0x07) | ((i & 0x08) << 4)),
                (byte)(i & 0x70));
        }

        public static Tp02MachineWord[] EncodeJump(int label)
        {
            if (label < 1 || label > 256) throw new ArgumentOutOfRangeException("label");
            return new Tp02MachineWord[]
            {
                new Tp02MachineWord(0x2B, 0x71, 0x00),
                new Tp02MachineWord(0x80, (byte)(label - 1), 0x00)
            };
        }

        public static Tp02MachineWord[] EncodeCall(int label)
        {
            if (label < 1 || label > 256) throw new ArgumentOutOfRangeException("label");
            return new Tp02MachineWord[]
            {
                new Tp02MachineWord(0x2C, 0x71, 0x00),
                new Tp02MachineWord(0x80, (byte)(label - 1), 0x00)
            };
        }

        public static string WordsToHex(IList<Tp02MachineWord> words)
        {
            if (words == null) throw new ArgumentNullException("words");
            StringBuilder builder = new StringBuilder(words.Count * 6);
            int i;
            for (i = 0; i < words.Count; i++) builder.Append(words[i].ToHex());
            return builder.ToString();
        }

        public static string ChecksumAscii(string body)
        {
            if (body == null) throw new ArgumentNullException("body");
            byte[] bytes = Encoding.ASCII.GetBytes(body);
            int sum = 0;
            int i;
            for (i = 0; i < bytes.Length; i++) sum += bytes[i];
            int checksum = (-sum) & 0xFF;
            return checksum.ToString("X2", CultureInfo.InvariantCulture);
        }

        public static string BuildHostFrame(int station, int responseCode, string command, string data)
        {
            if (station < 0 || station > 99) throw new ArgumentOutOfRangeException("station");
            if (responseCode < 0 || responseCode > 15) throw new ArgumentOutOfRangeException("responseCode");
            if (string.IsNullOrEmpty(command) || command.Length != 3) throw new ArgumentException("Comando TP02 deve ter três caracteres.", "command");

            string body = station.ToString("00", CultureInfo.InvariantCulture) +
                          "?" + responseCode.ToString("X1", CultureInfo.InvariantCulture) +
                          command.ToUpperInvariant() + (data ?? string.Empty);
            return ":" + body + ChecksumAscii(body) + "\r";
        }

        public static string BuildRbp(int station, int start, int count, int responseCode)
        {
            if (start < 0 || start > 4000) throw new ArgumentOutOfRangeException("start");
            if (count < 1 || count > 100) throw new ArgumentOutOfRangeException("count");
            string countField = count == 100 ? "00" : count.ToString("00", CultureInfo.InvariantCulture);
            string data = start.ToString("0000", CultureInfo.InvariantCulture) + countField;
            return BuildHostFrame(station, responseCode, "RBP", data);
        }

        /// <summary>
        /// Gera o quadro WBP para inspeção. Nenhum byte é transmitido por esta classe.
        /// </summary>
        public static string BuildWbpDryRun(int station, int start, IList<Tp02MachineWord> words, int responseCode)
        {
            if (start < 0 || start > 4000) throw new ArgumentOutOfRangeException("start");
            if (words == null) throw new ArgumentNullException("words");
            if (words.Count < 1 || words.Count > 100) throw new ArgumentOutOfRangeException("words", "WBP envia de 1 a 100 passos por quadro.");

            string countField = words.Count == 100 ? "00" : words.Count.ToString("00", CultureInfo.InvariantCulture);
            string data = start.ToString("0000", CultureInfo.InvariantCulture) + countField + WordsToHex(words);
            return BuildHostFrame(station, responseCode, "WBP", data);
        }
    }
}
