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

        internal static readonly byte[] ProgramMode = BuildShortFrame(0x01);
        internal static readonly byte[] Run = BuildShortFrame(0x02);
        internal static readonly byte[] Candidate03 = BuildShortFrame(0x03);
        internal static readonly byte[] Candidate04 = BuildShortFrame(0x04);
        internal static readonly byte[] ClearAllMemory = BuildShortFrame(0x0F);
        internal static readonly byte[] Candidate11 = BuildShortFrame(0x11);
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
                new CommandInfo("09", "09 LEN END ... CHK", "Escrita/configuração de áreas variáveis", "8 construtores; LEN 04, 05 e 11h", false),
                new CommandInfo("0A", "0A 03 END QTD CHK", "Leitura V, D, WC, FILE e sistema", "14 construtores de leitura parametrizada", false),
                new CommandInfo("0F", "0F 00 F0", "Apagar toda a memória", "Clear All Memory confirmado no PC12", false),
                new CommandInfo("11", "11 00 EE", "Clear Data", "Menu 310 -> handler 004AE491; exige STOP", false),
                new CommandInfo("13", "13 00 EC", "Senha/validação", "Contexto de senha no PC12", false),
                new CommandInfo("14", "14 00 EB", "Consulta/tratamento de senha", "Contexto de senha no PC12", false),
                new CommandInfo("33", "33 LEN ... CHK", "Gravar programa", "Construtor original emulado offline", false),
                new CommandInfo("34", "34 03 END QTD CHK", "Ler programa", "Confirmado no PC12 e em bancada", true),
                new CommandInfo("35", "35 03 DATA[3] CHK", "Troca de dados do monitor", "Handler assíncrono de comunicação; efeito parcial", false),
                new CommandInfo("37", "37 02 FF FF C8", "Atualizar BIOS/firmware", "Contexto BIOS Refresh no PC12", false),
                new CommandInfo("38", "38 00 C7", "Metadados/preâmbulo do programa", "Confirmado no fluxo de leitura", true),
                new CommandInfo("F0", "F0 00 0F", "Status/preflight da conexão", "Confirmado no PC12 e no TP02", true)
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
                || Equal(frame, Candidate11);
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
