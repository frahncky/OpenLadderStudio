using System;
using System.Collections.Generic;

namespace OpenLadderStudio.Core
{
    /// <summary>Quadros curtos PG reconstruídos do PC12 v2.1. Não realiza I/O.</summary>
    internal static class Tp02PgProtocol
    {
        internal sealed class CommandInfo
        {
            internal readonly string Code;
            internal readonly string Frame;
            internal readonly string Function;
            internal readonly string Evidence;
            internal readonly bool TransmitAllowed;

            internal CommandInfo(string code, string frame, string function, string evidence, bool transmitAllowed)
            {
                Code = code;
                Frame = frame;
                Function = function;
                Evidence = evidence;
                TransmitAllowed = transmitAllowed;
            }
        }

        /// <summary>Resultado de um descritor do monitor Ladder, sem qualquer I/O.</summary>
        internal sealed class MonitorResult
        {
            internal readonly int Type;
            internal readonly int Consumed;
            internal readonly bool HasState;
            internal readonly bool IsOn;
            internal readonly bool HasValue;
            internal readonly uint Value;

            internal MonitorResult(int type, int consumed, bool hasState, bool isOn, bool hasValue, uint value)
            {
                Type = type;
                Consumed = consumed;
                HasState = hasState;
                IsOn = isOn;
                HasValue = hasValue;
                Value = value;
            }
        }

        internal static readonly byte[] ProgramMode = BuildShortFrame(0x01);
        internal static readonly byte[] Run = BuildShortFrame(0x02);
        internal static readonly byte[] Candidate03 = BuildShortFrame(0x03);
        internal static readonly byte[] Candidate04 = BuildShortFrame(0x04);
        internal static readonly byte[] ClearAllMemory = BuildShortFrame(0x0F);
        internal static readonly byte[] Candidate11 = BuildShortFrame(0x11);
        internal static readonly byte[] EepromToPlc = BuildShortFrame(0x12);
        internal static readonly byte[] PlcToEeprom = BuildShortFrame(0x13);
        internal static readonly byte[] AuthorizationGate = BuildShortFrame(0x14);
        internal static readonly byte[] Presence = BuildShortFrame(0xF0);

        internal static IList<CommandInfo> GetCommandCatalog()
        {
            return new List<CommandInfo>
            {
                new CommandInfo("HELLO", "CON-ICB<CR>", "Iniciar comunicação PG", "Confirmado no PC12 e no TP02", true),
                new CommandInfo("01", "01 00 FE", "Program/STOP", "PC12; validação física pendente", false),
                new CommandInfo("02", "02 00 FD", "RUN", "PC12; TX único com confirmação por HELLO", true),
                new CommandInfo("03", "03 00 FC", "Clear Program / preparar gravação", "Menu 321 e preflight do PG33; 5 chamadas", false),
                new CommandInfo("04", "04 00 FB", "Clear System", "Menu 309 -> handler 004AE346; exige STOP", false),
                new CommandInfo("09", "09 LEN [END QTD DADOS]... CHK", "Escrita V/D/WC, FL, WS/SC e RTC", "10 locais emulados; lotes de até 40 registradores", false),
                new CommandInfo("0A", "0A LEN [END QTD]... CHK", "Leitura V, D, WC, FILE, bits, sistema, RTC e monitor Ladder", "16 builders emulados; dispatcher 27 IDs; parser nativo dos tipos 0..7 e Q=1/2/4", false),
                new CommandInfo("0F", "0F 00 F0", "Apagar toda a memória", "Clear All Memory confirmado no PC12", false),
                new CommandInfo("11", "11 00 EE", "Clear Data", "Menu 310 -> handler 004AE491; exige STOP", false),
                new CommandInfo("12", "12 00 ED", "EEPROM PACK → PLC", "Diálogo 30 e seleção nativa emulados", false),
                new CommandInfo("13", "13 00 EC", "PLC → EEPROM PACK", "Diálogo 30 e seleção nativa emulados", false),
                new CommandInfo("14", "14 00 EB", "Autorizar operação protegida (gate de senha)", "5 builders: Compare/Read/Write/EEPROM; senha comparada localmente", false),
                new CommandInfo("33", "33 LEN ... CHK", "Gravar programa", "Construtor original emulado offline", false),
                new CommandInfo("34", "34 03 END QTD CHK", "Ler programa", "Confirmado no PC12 e em bancada", true),
                new CommandInfo("35", "35 03 END BIT CHK", "SET/RESET de X, Y e C", "5.632 quadros emulados; banco diferente do PG0A", false),
                new CommandInfo("37", "37 02 FF FF C8", "Atualizar BIOS/firmware", "Contexto BIOS Refresh no PC12", false),
                new CommandInfo("38", "38 00 C7", "Metadados/preâmbulo do programa", "Confirmado no fluxo de leitura", true),
                new CommandInfo("F0", "F0 00 0F", "Preflight/qualificação da sessão PG", "3 builders; wrappers com 24 callers; confirmado no PC12 e TP02", true)
            }.AsReadOnly();
        }

        internal static byte[] BuildShortFrame(byte command)
        {
            byte[] prefix = new byte[] { command, 0x00 };
            return new byte[] { command, 0x00, Checksum(prefix, prefix.Length) };
        }

        internal static byte Checksum(byte[] bytes, int count)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (count < 0 || count > bytes.Length) throw new ArgumentOutOfRangeException("count");
            int sum = 0;
            for (int i = 0; i < count; i++) sum = (sum + bytes[i]) & 0xFF;
            return (byte)((0xFF - sum) & 0xFF);
        }

        internal static bool HasValidChecksum(byte[] frame)
        {
            if (frame == null || frame.Length == 0) return false;
            int sum = 0;
            for (int i = 0; i < frame.Length; i++) sum = (sum + frame[i]) & 0xFF;
            return sum == 0xFF;
        }

        internal static bool IsBlocked(byte[] frame)
        {
            return Equal(frame, ClearAllMemory) || Equal(frame, ProgramMode)
                || Equal(frame, Candidate03) || Equal(frame, Candidate04)
                || Equal(frame, Candidate11) || Equal(frame, EepromToPlc)
                || Equal(frame, PlcToEeprom) || Equal(frame, AuthorizationGate);
        }

        /// <summary>
        /// Reproduz o parser de respostas do monitor Ladder em 004C4EF2..004C56B6.
        /// payload é apenas a área de dados da resposta, sem status/len/checksum.
        /// selector é o bit (tipos 1/2) ou byte esperado (tipos 5/6).
        /// width vale 1..3 nos tipos numéricos 4/7; o PC12 sempre consome 4 bytes nesses tipos.
        /// </summary>
        internal static MonitorResult DecodeMonitor(int type, int selector, int width, byte[] payload, int offset)
        {
            if (payload == null) throw new ArgumentNullException("payload");
            if (offset < 0 || offset > payload.Length) throw new ArgumentOutOfRangeException("offset");
            if (type < 0 || type > 7) throw new ArgumentOutOfRangeException("type");

            if (type == 0 || type == 3)
                return new MonitorResult(type, 0, false, false, false, 0);

            if (type == 1 || type == 2)
            {
                if (selector < 0 || selector > 7) throw new ArgumentOutOfRangeException("selector");
                Require(payload, offset, 1);
                bool set = (payload[offset] & (1 << selector)) != 0;
                return new MonitorResult(type, 1, true, type == 1 ? set : !set, false, 0);
            }

            if (type == 5 || type == 6)
            {
                if (selector < 0 || selector > 255) throw new ArgumentOutOfRangeException("selector");
                Require(payload, offset, 2);
                bool equal = payload[offset + 1] == selector;
                return new MonitorResult(type, 2, true, type == 5 ? equal : !equal, false, 0);
            }

            if (width < 1 || width > 3) throw new ArgumentOutOfRangeException("width");
            Require(payload, offset, 4);
            uint value;
            if (type == 4)
            {
                if (width == 1) value = payload[offset];
                else if (width == 2) value = (uint)(payload[offset] | (payload[offset + 1] << 8));
                else value = (uint)(payload[offset] | (payload[offset + 1] << 8)
                    | (payload[offset + 2] << 16) | (payload[offset + 3] << 24));
            }
            else
            {
                if (width == 1) value = payload[offset + 1];
                else
                {
                    uint low = (uint)((payload[offset] << 8) | payload[offset + 1]);
                    if (width == 2) value = low;
                    else
                    {
                        uint high = (uint)((payload[offset + 2] << 8) | payload[offset + 3]);
                        value = low | (high << 16);
                    }
                }
            }
            return new MonitorResult(type, 4, false, false, true, value);
        }

        private static void Require(byte[] payload, int offset, int count)
        {
            if (offset + count > payload.Length)
                throw new ArgumentException("Resposta do monitor incompleta para o tipo solicitado.");
        }

        internal static byte[] Copy(byte[] frame)
        {
            if (frame == null) throw new ArgumentNullException("frame");
            return (byte[])frame.Clone();
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
