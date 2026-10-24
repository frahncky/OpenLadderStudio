using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    /// <summary>
    /// Ensaio offline do roteiro docs/tp02-bancada-firmware-2026-10-24.md (fases A1,
    /// A2, A3, B1 e B2). Não abre porta COM: cada quadro passa pela mesma barreira
    /// do validador físico (TP02PhysicalValidationSafety.IsAllowed) e, se liberado,
    /// é respondido em processo pelas rotinas do emulador TP02 PG (TP02PgReadback e
    /// TP02PgV156Scenario). As respostas são do EMULADOR, inclusive as SINTÉTICAS.
    /// O log segue o formato "ROTULO TX: ..." / "ROTULO RX: ..." do validador para
    /// alimentar AnalyzeTp02BancadaFirmware.ps1.
    /// </summary>
    internal static class TP02BancadaEnsaioProgram
    {
        private sealed class VirtualPlc
        {
            internal readonly Pg33MachineWord[] Words = new Pg33MachineWord[4000];
            internal readonly bool[] Valid = new bool[4000];
            internal int Highest = -1;
            internal bool Run;
        }

        private static readonly byte[] HelloStop = new byte[] { 0x80, 0x01, 0x09, 0x75 };
        private static readonly byte[] HelloRun = new byte[] { 0xC0, 0x01, 0x09, 0x35 };
        private static readonly byte[] F0Stop = new byte[] { 0x00, 0x02, 0x10, 0x22, 0xCB };

        private static string outDir;
        private static StringBuilder log;
        private static int failures;
        private static readonly StringBuilder summary = new StringBuilder();

        [STAThread]
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            string programs = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--out=", StringComparison.OrdinalIgnoreCase)) outDir = args[i].Substring(6).Trim('"');
                else if (args[i].StartsWith("--programs=", StringComparison.OrdinalIgnoreCase)) programs = args[i].Substring(11).Trim('"');
            }
            if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(programs))
            {
                Console.WriteLine("Uso: OpenLadderTP02BancadaEnsaio.exe --programs=<pasta com *.tp02.hex> --out=<pasta de logs>");
                return 2;
            }
            Directory.CreateDirectory(outDir);

            string pb = Path.Combine(programs, "PB-2-passos.tp02.hex");
            string pc = Path.Combine(programs, "PC-91-passos.tp02.hex");
            string pd = Path.Combine(programs, "PD-323-passos.tp02.hex");
            string pe = Path.Combine(programs, "PE-82-passos-2w-slot79.tp02.hex");

            summary.AppendLine("ENSAIO OFF-LINE DO ROTEIRO DE BANCADA TP02 (2026-10-24)");
            summary.AppendLine("COM=NÃO ABERTA | PLC=NÃO UTILIZADO | respostas = emulador (SINTÉTICAS onde indicado)");
            summary.AppendLine();

            // A1: sessão base (PB carregado, STOP).
            Scenario("A1-sessao-base", pb, false, "stop", "word", false, delegate(VirtualPlc plc, List<string> tx)
            {
                Hello(plc, tx, "HELLO");
                Exchange(plc, tx, TP02PhysicalValidationSafety.F0, "F0");
            });

            // A2: 38 para PB, PC, PD e PE, nos dois modelos (página e total16).
            string[][] a2 = new string[][]
            {
                new string[] { "PB", pb }, new string[] { "PC", pc }, new string[] { "PD", pd }, new string[] { "PE", pe }
            };
            for (int i = 0; i < a2.Length; i++)
            {
                for (int m = 0; m < 2; m++)
                {
                    bool total16 = m == 1;
                    Scenario("A2-38-" + a2[i][0] + (total16 ? "-total16" : "-pagina"), a2[i][1], false, "stop", "word", total16,
                        delegate(VirtualPlc plc, List<string> tx)
                        {
                            Hello(plc, tx, "HELLO");
                            Exchange(plc, tx, TP02PhysicalValidationSafety.F0, "F0");
                            Exchange(plc, tx, TP02PhysicalValidationSafety.Frame38, "38");
                        });
                }
            }

            // A3: seletor em RUN. Só F0 pode sair; o resto deve ser barrado pelo validador.
            string[] f0Modes = new string[] { "bit6", "stop", "silent", "nak" };
            for (int i = 0; i < f0Modes.Length; i++)
            {
                Scenario("A3-F0-RUN-" + f0Modes[i], pd, true, f0Modes[i], "word", false, delegate(VirtualPlc plc, List<string> tx)
                {
                    Hello(plc, tx, "HELLO");
                    Exchange(plc, tx, TP02PhysicalValidationSafety.F0, "F0-RUN");
                    // Tentativas proibidas em RUN: nenhuma pode ser transmitida.
                    Exchange(plc, tx, TP02PhysicalValidationSafety.Frame38, "38-EM-RUN");
                    Exchange(plc, tx, TP02PhysicalValidationSafety.Pg34Pages[0], "PG34-EM-RUN");
                    Exchange(plc, tx, TP02PhysicalValidationSafety.Hello, "HELLO-EM-RUN");
                    Exchange(plc, tx, Hex("33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 D9"), "PG33-EM-RUN");
                    Exchange(plc, tx, Hex("03 00 FC"), "CLEAR-PROGRAM-EM-RUN");
                    Exchange(plc, tx, Hex("0F 00 F0"), "CLEAR-ALL-EM-RUN");
                    Exchange(plc, tx, Hex("02 00 FD"), "RUN-REMOTO-EM-RUN");
                    if (tx.Count != 2 || tx[0] != "HELLO" || tx[1] != "F0-RUN")
                        Fail("A3: em RUN só HELLO e F0 deveriam sair; saíram " + string.Join(", ", tx.ToArray()));
                });
            }

            // B1: paginação >80 pela rotina real do validador (TP02BancadaPg34Campaign).
            // PC: END na página @0050, então @00A0 não sai. PD: as cinco páginas saem.
            Scenario("B1-paginacao-PC", pc, false, "stop", "word", false, delegate(VirtualPlc plc, List<string> tx)
            {
                RealCampaign(plc, tx, true, false);
                Expect(tx, "B1-PC", "HELLO", "F0", "38", "PG34-P0", "PG34-P80");
            });
            Scenario("B1-paginacao-PD", pd, false, "stop", "word", false, delegate(VirtualPlc plc, List<string> tx)
            {
                RealCampaign(plc, tx, true, false);
                Expect(tx, "B1-PD", "HELLO", "F0", "38", "PG34-P0", "PG34-P80", "PG34-00A0", "PG34-00F0", "PG34-0140");
            });
            // Fases não marcadas na tela: com PD, só @0000/@0050 saem.
            Scenario("B1-B2-desmarcadas-PD", pd, false, "stop", "word", false, delegate(VirtualPlc plc, List<string> tx)
            {
                RealCampaign(plc, tx, false, false);
                Expect(tx, "B1/B2 desmarcadas", "HELLO", "F0", "38", "PG34-P0", "PG34-P80");
            });

            // B2: 34 @004F com o PE, nos cinco modelos de firmware, pela rotina real. 004E deve ser barrado.
            string[] b2Modes = new string[] { "word", "align50", "align00", "nak", "silent" };
            for (int i = 0; i < b2Modes.Length; i++)
            {
                Scenario("B2-START-004F-" + b2Modes[i], pe, false, "stop", b2Modes[i], false, delegate(VirtualPlc plc, List<string> tx)
                {
                    RealCampaign(plc, tx, true, true);
                    Exchange(plc, tx, Hex("34 03 00 4E A0 DA"), "PG34-004E-FORA-DA-LISTA");
                    Expect(tx, "B2", "HELLO", "F0", "38", "PG34-P0", "PG34-P80", "PG34-004F");
                });
            }

            summary.AppendLine();
            summary.AppendLine(failures == 0 ? "RESULTADO: PASS (barreira do validador respeitada em todas as fases)" : "RESULTADO: FALHA (" + failures.ToString(CultureInfo.InvariantCulture) + ")");
            File.WriteAllText(Path.Combine(outDir, "ensaio-resumo.txt"), summary.ToString(), new UTF8Encoding(true));
            Console.Write(summary.ToString());
            return failures == 0 ? 0 : 1;
        }

        private delegate void Phase(VirtualPlc plc, List<string> transmitted);

        private static void Scenario(string name, string seedPath, bool run, string f0Mode, string pg34Mode, bool total16, Phase phase)
        {
            VirtualPlc plc = new VirtualPlc();
            plc.Highest = TP02PgV156Scenario.LoadSeedFile(seedPath, plc.Words, plc.Valid);
            plc.Run = run;
            TP02PgV156Scenario.F0RunMode = f0Mode;
            TP02PgV156Scenario.Pg34UnalignedMode = pg34Mode;
            TP02PgReadback.Pg38Total16 = total16;

            log = new StringBuilder();
            Line("ENSAIO OFF-LINE " + name + " | COM=NÃO ABERTA | resposta=emulador em processo");
            Line("Programa: " + Path.GetFileName(seedPath) + " (" + (plc.Highest + 1).ToString(CultureInfo.InvariantCulture)
                + " palavras, END=" + plc.Highest.ToString("X4", CultureInfo.InvariantCulture) + ")");
            Line("Emulador: HELLO=" + (run ? "C0 (RUN)" : "80 (STOP)") + " f0_run=" + f0Mode + " pg34_unaligned=" + pg34Mode
                + " pg38=" + (total16 ? "total16" : "pagina") + ((f0Mode != "stop" || pg34Mode != "word" || total16) ? " (SINTÉTICO/HIPÓTESE)" : string.Empty));

            List<string> transmitted = new List<string>();
            phase(plc, transmitted);

            File.WriteAllText(Path.Combine(outDir, name + ".log"), log.ToString(), new UTF8Encoding(true));
            summary.AppendLine(name.PadRight(28) + " TX=" + string.Join(", ", transmitted.ToArray()));
            TP02PgV156Scenario.F0RunMode = "stop";
            TP02PgV156Scenario.Pg34UnalignedMode = "word";
            TP02PgReadback.Pg38Total16 = false;
        }

        private static bool plcInRunForGate;

        private static void Hello(VirtualPlc plc, List<string> tx, string label)
        {
            plcInRunForGate = false;
            byte[] rx = Exchange(plc, tx, TP02PhysicalValidationSafety.Hello, label);
            // Mesma regra do validador: HELLO C0 01 09 35 coloca a sessão em RUN.
            if (rx != null && Same(rx, HelloRun)) plcInRunForGate = true;
        }

        private static byte[] Exchange(VirtualPlc plc, List<string> tx, byte[] request, string label)
        {
            if (!TP02PhysicalValidationSafety.IsAllowed(request, plcInRunForGate))
            {
                Line("BLOQUEIO DE SEGURANÇA: " + label + " fora da allowlist" + (plcInRunForGate ? " em RUN" : string.Empty) + ": " + ToHex(request));
                return null;
            }
            tx.Add(label);
            Line(label + " TX: " + ToHex(request));
            byte[] rx = Respond(plc, request);
            Line(label + " RX: " + (rx == null || rx.Length == 0 ? "[]" : ToHex(rx)));
            return rx;
        }

        private static byte[] Respond(VirtualPlc plc, byte[] request)
        {
            if (Same(request, TP02PhysicalValidationSafety.Hello)) return plc.Run ? HelloRun : HelloStop;
            if (Same(request, TP02PhysicalValidationSafety.F0)) return TP02PgV156Scenario.SelectF0Response(plc.Run, F0Stop);
            if (Same(request, TP02PhysicalValidationSafety.Frame38))
                return TP02PgReadback.Build38(plc.Words, plc.Valid, plc.Highest, new byte[] { 0x00, 0x02, 0x00, 0x0A, 0xF3 });
            if (request.Length == 6 && request[0] == 0x34)
                return TP02PgV156Scenario.Build34(request, plc.Words, plc.Valid, plc.Highest, null);
            return null;
        }

        /// <summary>
        /// Sequência da campanha em STOP do validador (HELLO, F0, 38, @0000, @0050) seguida da
        /// mesma rotina B1/B2 que o validador chama (TP02BancadaPg34Campaign.Run).
        /// </summary>
        private static void RealCampaign(VirtualPlc plc, List<string> tx, bool b1, bool b2)
        {
            Hello(plc, tx, "HELLO");
            Exchange(plc, tx, TP02PhysicalValidationSafety.F0, "F0");
            Exchange(plc, tx, TP02PhysicalValidationSafety.Frame38, "38");
            byte[] page0 = TP02BancadaPg34Campaign.FindPage(Exchange(plc, tx, Tp02Pg34Pager.BuildReadRequest(0), "PG34-P0"));
            byte[] page50 = TP02BancadaPg34Campaign.FindPage(Exchange(plc, tx, Tp02Pg34Pager.BuildReadRequest(80), "PG34-P80"));
            TP02BancadaPg34Campaign.Run(page0, page50, b1, b2,
                delegate(byte[] request, string label) { return Exchange(plc, tx, request, label); },
                delegate(string name, string status, string detail) { Line(status + " " + name + " | " + detail); });
        }

        private static void Expect(List<string> tx, string phase, params string[] labels)
        {
            if (string.Join(",", tx.ToArray()) != string.Join(",", labels))
                Fail(phase + ": esperado " + string.Join(", ", labels) + "; saíram " + string.Join(", ", tx.ToArray()));
        }

        private static void Fail(string message)
        {
            failures++;
            Line("FALHA DO ENSAIO: " + message);
            Console.Error.WriteLine("FALHA: " + message);
        }

        private static void Line(string text)
        {
            log.Append("[").Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append("] ").AppendLine(text);
        }

        private static byte[] Hex(string text)
        {
            string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] b = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++) b[i] = Byte.Parse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return b;
        }

        private static bool Same(byte[] a, byte[] b)
        {
            return TP02PhysicalValidationSafety.Same(a, b);
        }

        private static string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            return BitConverter.ToString(data).Replace('-', ' ');
        }
    }
}
