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
        internal static bool SeedSemantic323Enabled;
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
            if (a.Equals("--seed=semantic323", StringComparison.OrdinalIgnoreCase))
            {
                SeedSemantic323Enabled = true;
                return true;
            }
            if (a.Equals("--pg38=total16", StringComparison.OrdinalIgnoreCase))
            {
                TP02PgReadback.Pg38Total16 = true;
                return true;
            }
            if (a.Equals("--pg38=page", StringComparison.OrdinalIgnoreCase))
            {
                TP02PgReadback.Pg38Total16 = false;
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
            if (a.StartsWith("--f0-run=", StringComparison.OrdinalIgnoreCase))
            {
                F0RunMode = ParseMode(a.Substring("--f0-run=".Length), F0RunModes, "f0-run");
                return true;
            }
            if (a.StartsWith("--pg34-unaligned=", StringComparison.OrdinalIgnoreCase))
            {
                Pg34UnalignedMode = ParseMode(a.Substring("--pg34-unaligned=".Length), Pg34UnalignedModes, "pg34-unaligned");
                return true;
            }
            if (a.StartsWith("--seed-file=", StringComparison.OrdinalIgnoreCase))
            {
                SeedFile = a.Substring("--seed-file=".Length).Trim('"');
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

        // ------------------------------------------------------------------
        // Roteiro de bancada 2026-10-24 (fases A3 e B2). Tudo abaixo é
        // SINTÉTICO / HIPÓTESE: nenhuma destas respostas foi capturada no
        // firmware. O padrão preserva o comportamento histórico do emulador.
        // ------------------------------------------------------------------

        /// <summary>F0 com HELLO RUN: stop (padrão histórico), bit6, silent ou nak.</summary>
        internal static string F0RunMode = "stop";

        /// <summary>34 com START fora de múltiplo de 0x50: word (padrão), align50, align00, nak ou silent.</summary>
        internal static string Pg34UnalignedMode = "word";

        /// <summary>Arquivo HHLLEE (uma palavra por linha) carregado como programa do PLC.</summary>
        internal static string SeedFile = string.Empty;

        /// <summary>NAK SINTÉTICO: S com bit7 e código 0x10 em rx[2]; o código real é desconhecido.</summary>
        internal static readonly byte[] SyntheticNak = new byte[] { 0x80, 0x01, 0x10, 0x6E };

        private static readonly string[] F0RunModes = new string[] { "stop", "bit6", "silent", "nak" };
        private static readonly string[] Pg34UnalignedModes = new string[] { "word", "align50", "align00", "nak", "silent" };

        private static string ParseMode(string text, string[] allowed, string label)
        {
            string value = (text ?? string.Empty).Trim().ToLowerInvariant();
            for (int i = 0; i < allowed.Length; i++)
                if (allowed[i] == value) return value;
            throw new ArgumentOutOfRangeException(label, "Modo desconhecido: " + text + ".");
        }

        /// <summary>
        /// Resposta ao F0. Em STOP é sempre a resposta histórica. Com HELLO RUN:
        /// stop = histórica (bit6 não reflete RUN), bit6 = 40 02 10 22 8B,
        /// silent = sem resposta (null), nak = NAK sintético. HIPÓTESE.
        /// </summary>
        internal static byte[] SelectF0Response(bool helloRun, byte[] stopResponse)
        {
            if (!helloRun || F0RunMode == "stop") return stopResponse;
            if (F0RunMode == "silent") return null;
            if (F0RunMode == "nak") return (byte[])SyntheticNak.Clone();

            byte[] frame = (byte[])stopResponse.Clone();
            frame[0] = (byte)(frame[0] | 0x40);
            int sum = 0;
            for (int i = 0; i < frame.Length - 1; i++) sum = (sum + frame[i]) & 0xFF;
            frame[frame.Length - 1] = (byte)((0xFF - sum) & 0xFF);
            return frame;
        }

        /// <summary>
        /// Resposta ao 34. START múltiplo de 0x50 segue sempre o modelo por palavra.
        /// START fora de múltiplo: word (modelo atual), align50/align00 (firmware
        /// arredonda), nak (NAK sintético) ou silent (null). HIPÓTESE.
        /// </summary>
        internal static byte[] Build34(byte[] request, Pg33MachineWord[] words, bool[] valid,
            int highestProgramStep, byte[] fallbackPage0000)
        {
            int start = request != null && request.Length >= 6 && request[0] == 0x34 ? (request[2] << 8) | request[3] : 0;
            if (start % 0x50 == 0 || Pg34UnalignedMode == "word")
                return TP02PgReadback.Build34(request, words, valid, highestProgramStep, fallbackPage0000);
            if (Pg34UnalignedMode == "silent") return null;
            if (Pg34UnalignedMode == "nak") return (byte[])SyntheticNak.Clone();

            // align00 = arredonda para baixo (0x4F -> 0x00); align50 = para cima (0x4F -> 0x50).
            int aligned = Pg34UnalignedMode == "align00" ? (start / 0x50) * 0x50 : ((start + 0x4F) / 0x50) * 0x50;
            byte[] substitute = new byte[] { 0x34, 0x03, (byte)((aligned >> 8) & 0xFF), (byte)(aligned & 0xFF), 0xA0, 0x00 };
            int sum = 0;
            for (int i = 0; i < 5; i++) sum = (sum + substitute[i]) & 0xFF;
            substitute[5] = (byte)((0xFF - sum) & 0xFF);
            return TP02PgReadback.Build34(substitute, words, valid, highestProgramStep, fallbackPage0000);
        }

        /// <summary>
        /// Carrega um programa no formato do OpenLadderTP02ProjectExport: uma palavra
        /// HHLLEE por linha, linhas vazias ignoradas. Devolve o maior passo carregado.
        /// </summary>
        internal static int LoadSeedFile(string path, Pg33MachineWord[] words, bool[] valid)
        {
            if (words == null) throw new ArgumentNullException("words");
            if (valid == null) throw new ArgumentNullException("valid");
            string[] lines = System.IO.File.ReadAllLines(path);
            Array.Clear(valid, 0, valid.Length);
            int step = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace(" ", string.Empty).Trim();
                if (line.Length == 0) continue;
                byte[] b = new byte[3];
                if (line.Length != 6
                    || !Byte.TryParse(line.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b[0])
                    || !Byte.TryParse(line.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b[1])
                    || !Byte.TryParse(line.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b[2]))
                    throw new System.IO.InvalidDataException("Linha " + (i + 1).ToString(CultureInfo.InvariantCulture) + " não é HHLLEE: " + lines[i]);
                if (step >= words.Length || step >= valid.Length)
                    throw new System.IO.InvalidDataException("Programa maior que o banco do emulador.");
                words[step] = new Pg33MachineWord(b[0], b[1], b[2]);
                valid[step] = true;
                step++;
            }
            if (step == 0) throw new System.IO.InvalidDataException("Arquivo de programa vazio.");
            return step - 1;
        }

        internal static int SeedProgramIfRequested(Pg33MachineWord[] words, bool[] valid)
        {
            if (SeedFile.Length > 0) return LoadSeedFile(SeedFile, words, valid);
            if (SeedSemantic323Enabled) return SeedSemantic323(words, valid);
            if (!SeedBoundary323Enabled) return -1;
            return SeedBoundary323(words, valid);
        }

        internal static int SeedBoundary323(Pg33MachineWord[] words, bool[] valid)
        {
            if (words == null) throw new ArgumentNullException("words");
            if (valid == null) throw new ArgumentNullException("valid");
            if (words.Length < 323 || valid.Length < 323)
                throw new ArgumentException("Banco PG precisa ter pelo menos 323 posições.");

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

        internal static int SeedSemantic323(Pg33MachineWord[] words, bool[] valid)
        {
            if (words == null) throw new ArgumentNullException("words");
            if (valid == null) throw new ArgumentNullException("valid");
            if (words.Length < 323 || valid.Length < 323)
                throw new ArgumentException("Banco PG precisa ter pelo menos 323 posições.");

            Array.Clear(valid, 0, valid.Length);
            for (int rung = 0; rung < 161; rung++)
            {
                int contactStep = rung * 2;
                int coilStep = contactStep + 1;
                int coilIndex = rung;
                byte high = (byte)(0x40 | ((coilIndex >> 3) & 0x1F));
                byte low = (byte)(0x40 | (coilIndex & 0x07));

                words[contactStep] = new Pg33MachineWord(0x00, 0x10, 0x00); // STR X0001
                words[coilStep] = new Pg33MachineWord(high, low, 0x00); // OUT C0001..C0161
                valid[contactStep] = true;
                valid[coilStep] = true;
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
            if (!SeedBoundary323Enabled && !SeedSemantic323Enabled && SeedFile.Length == 0
                && HelloRespondOnAttempt == 1 && F0RespondOnAttempt == 1
                && !TP02PgReadback.Pg38Total16 && F0RunMode == "stop" && Pg34UnalignedMode == "word")
                return "desativado";

            return "seed=" + (SeedFile.Length > 0 ? "arquivo:" + System.IO.Path.GetFileName(SeedFile)
                    : (SeedSemantic323Enabled ? "semantic323" : (SeedBoundary323Enabled ? "boundary323" : "nenhum")))
                + " hello_on=" + HelloRespondOnAttempt.ToString(CultureInfo.InvariantCulture)
                + " f0_on=" + F0RespondOnAttempt.ToString(CultureInfo.InvariantCulture)
                + " unknown_ack=" + (DisableUnknownAck ? "OFF" : "padrao")
                + " pg38=" + (TP02PgReadback.Pg38Total16 ? "total16 (HIPÓTESE)" : "pagina")
                + " f0_run=" + F0RunMode + (F0RunMode == "stop" ? string.Empty : " (SINTÉTICO)")
                + " pg34_unaligned=" + Pg34UnalignedMode + (Pg34UnalignedMode == "word" ? string.Empty : " (SINTÉTICO)");
        }
    }
}
