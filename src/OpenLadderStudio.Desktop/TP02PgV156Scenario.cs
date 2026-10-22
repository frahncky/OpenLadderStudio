using System;
using System.Globalization;

namespace ModernPC12
{
    /// <summary>
    /// Cenario de laboratorio para reproduzir, sem PLC fisico, o caso observado
    /// na bancada da v1.56: programa atual de 323 words, HELLO/F0 tardios e
    /// substituicao por um programa minimo de 3 words.
    /// </summary>
    internal static class TP02PgV156Scenario
    {
        internal static bool SeedBoundary323Enabled;
        internal static bool DisableUnknownAck;
        internal static int HelloRespondOnAttempt = 1;
        internal static int F0RespondOnAttempt = 1;
        internal static string SelfTestMode = string.Empty;

        private static int helloSeen;
        private static int f0Seen;

        internal static bool TryApplyArgument(string argument)
        {
            string a = argument == null ? string.Empty : argument.Trim();
            if (a.Length == 0) return false;

            if (a.Equals("--scenario=v156", StringComparison.OrdinalIgnoreCase))
            {
                SeedBoundary323Enabled = true;
                DisableUnknownAck = true;
                HelloRespondOnAttempt = 5;
                F0RespondOnAttempt = 4;
                return true;
            }
            if (a.Equals("--seed=boundary323", StringComparison.OrdinalIgnoreCase))
            {
                SeedBoundary323Enabled = true;
                return true;
            }
            if (a.Equals("--self-test-v156", StringComparison.OrdinalIgnoreCase))
            {
                SelfTestMode = "V156";
                return true;
            }
            if (a.Equals("--self-test-all", StringComparison.OrdinalIgnoreCase))
            {
                SelfTestMode = "ALL";
                return true;
            }
            if (a.StartsWith("--hello-on=", StringComparison.OrdinalIgnoreCase))
            {
                HelloRespondOnAttempt = ParseAttempt(a.Substring("--hello-on=".Length), "HELLO");
                return true;
            }
            if (a.StartsWith("--f0-on=", StringComparison.OrdinalIgnoreCase))
            {
                F0RespondOnAttempt = ParseAttempt(a.Substring("--f0-on=".Length), "F0");
                return true;
            }
            return false;
        }

        private static int ParseAttempt(string text, string label)
        {
            int value;
            if (!Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || value < 1 || value > 8)
                throw new ArgumentOutOfRangeException(label, "Tentativa deve ficar entre 1 e 8.");
            return value;
        }

        internal static bool ShouldRespondHello(out string detail)
        {
            helloSeen++;
            bool respond = helloSeen >= HelloRespondOnAttempt;
            detail = "tentativa=" + helloSeen.ToString(CultureInfo.InvariantCulture)
                + "/responder_em=" + HelloRespondOnAttempt.ToString(CultureInfo.InvariantCulture)
                + (respond ? " -> RESPONDER" : " -> SILENCIO SIMULADO");
            return respond;
        }

        internal static bool ShouldRespondF0(out string detail)
        {
            f0Seen++;
            bool respond = f0Seen >= F0RespondOnAttempt;
            detail = "tentativa=" + f0Seen.ToString(CultureInfo.InvariantCulture)
                + "/responder_em=" + F0RespondOnAttempt.ToString(CultureInfo.InvariantCulture)
                + (respond ? " -> RESPONDER" : " -> SILENCIO SIMULADO");
            return respond;
        }

        internal static int SeedProgramIfRequested(Pg33MachineWord[] words, bool[] valid)
        {
            if (!SeedBoundary323Enabled) return -1;
            return SeedBoundary323(words, valid);
        }

        internal static int SeedBoundary323(Pg33MachineWord[] words, bool[] valid)
        {
            if (words == null) throw new ArgumentNullException("words");
            if (valid == null) throw new ArgumentNullException("valid");
            if (words.Length < 323 || valid.Length < 323)
                throw new ArgumentException("Banco PG precisa ter pelo menos 323 posicoes.");

            Array.Clear(valid, 0, valid.Length);
            for (int step = 0; step < 322; step++)
            {
                if ((step & 1) == 0)
                    words[step] = new Pg33MachineWord(0x00, 0x10, 0x00); // STR X0001
                else
                    words[step] = new Pg33MachineWord(0x40, 0x40, 0x00); // OUT C0001
                valid[step] = true;
            }
            words[322] = new Pg33MachineWord(0x00, 0x70, 0x00); // F-00 END
            valid[322] = true;
            return 322;
        }

        internal static byte[] BuildMinimalC0001Pg33()
        {
            Pg33MachineWord[] words = new Pg33MachineWord[]
            {
                new Pg33MachineWord(0x00, 0x10, 0x00), // STR X0001
                new Pg33MachineWord(0x40, 0x40, 0x00), // OUT C0001
                new Pg33MachineWord(0x00, 0x70, 0x00)  // F-00 END
            };
            return BuildPg33(0, words);
        }

        internal static byte[] BuildPg33(int startStep, Pg33MachineWord[] words)
        {
            if (words == null || words.Length < 1 || words.Length > 80)
                throw new ArgumentOutOfRangeException("words");
            if (startStep < 0 || startStep + words.Length > 4000)
                throw new ArgumentOutOfRangeException("startStep");

            int len = (3 * words.Length) + 4;
            byte[] frame = new byte[len + 3];
            frame[0] = 0x33;
            frame[1] = checked((byte)len);
            frame[2] = 0x00;
            frame[3] = (byte)((startStep >> 8) & 0xFF);
            frame[4] = (byte)(startStep & 0xFF);
            frame[5] = checked((byte)(2 * words.Length));

            int p = 6;
            for (int i = 0; i < words.Length; i++)
            {
                frame[p++] = words[i].High;
                frame[p++] = words[i].Low;
            }
            for (int i = 0; i < words.Length; i++)
                frame[p++] = words[i].External;

            int sum = 0;
            for (int i = 0; i < frame.Length - 1; i++) sum = (sum + frame[i]) & 0xFF;
            frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);
            return frame;
        }

        internal static string Describe()
        {
            if (!SeedBoundary323Enabled && HelloRespondOnAttempt == 1 && F0RespondOnAttempt == 1)
                return "desativado";

            return "seed=" + (SeedBoundary323Enabled ? "boundary323" : "nenhum")
                + " hello_on=" + HelloRespondOnAttempt.ToString(CultureInfo.InvariantCulture)
                + " f0_on=" + F0RespondOnAttempt.ToString(CultureInfo.InvariantCulture)
                + " unknown_ack=" + (DisableUnknownAck ? "OFF" : "padrao");
        }
    }
}
