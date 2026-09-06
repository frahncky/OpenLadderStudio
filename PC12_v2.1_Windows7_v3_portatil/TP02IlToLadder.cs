using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    internal static class TP02IlToLadderProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppBranding.Install();
            Application.Run(new TP02IlToLadderForm());
        }
    }

    internal sealed class TP02IlInstruction
    {
        public int Step;
        public string Operation = string.Empty;
        public string Operand = string.Empty;
        public string Source = string.Empty;
    }

    internal sealed class TP02LadderBuildRung
    {
        public readonly List<LadderProjectElement> Conditions = new List<LadderProjectElement>();
        public LadderProjectElement Output;
    }

    internal sealed class TP02IlToLadderForm : Form
    {
        private Color Navy { get { return OpenLadderPalette.Fore; } }
        private Color Accent { get { return OpenLadderPalette.Accent; } }
        private Color Canvas { get { return OpenLadderPalette.Shell; } }
        private Color TextPrimary { get { return OpenLadderPalette.Fore; } }
        private Color TextSecondary { get { return OpenLadderPalette.Muted; } }
        private Color Success { get { return OpenLadderPalette.Ok; } }
        private Color Warning { get { return OpenLadderPalette.Warning; } }

        private readonly List<TP02IlInstruction> instructions = new List<TP02IlInstruction>();
        private readonly List<TP02LadderBuildRung> rungs = new List<TP02LadderBuildRung>();
        private TextBox previewBox;
        private Label statusLabel;
        private string currentIlPath = string.Empty;
        private string generatedProject = string.Empty;

        public TP02IlToLadderForm()
        {
            Text = "TP02 IL to Ladder";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1050, 680);
            Size = new Size(1320, 820);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 74;
            header.BackColor = OpenLadderPalette.Chrome;
            Controls.Add(header);

            header.Controls.Add(NewLabel("RECONSTRUÇÃO SEGURA — IL → LADDER", 15.0f, FontStyle.Bold, Navy, 22, 12));
            header.Controls.Add(NewLabel("Converte somente IL verificada e um subconjunto estrutural comprovável para o formato .pladder do Studio.", 8.8f, FontStyle.Regular, TextSecondary, 24, 43));

            Label safe = new Label();
            safe.Text = "OFF-LINE • NÃO ESCREVE NO PLC";
            safe.Dock = DockStyle.Right;
            safe.Width = 285;
            safe.TextAlign = ContentAlignment.MiddleCenter;
            safe.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            safe.ForeColor = Success;
            header.Controls.Add(safe);

            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 82;
            bar.BackColor = Canvas;
            Controls.Add(bar);

            Button open = ButtonAt("ABRIR IL VERIFICADA", 16, 12, 165, true);
            open.Click += delegate { OpenIl(); };
            bar.Controls.Add(open);

            Button analyze = ButtonAt("ANALISAR", 193, 12, 110, false);
            analyze.Click += delegate { Analyze(); };
            bar.Controls.Add(analyze);

            Button save = ButtonAt("SALVAR .PLADDER", 315, 12, 145, false);
            save.Click += delegate { SavePladder(); };
            bar.Controls.Add(save);

            statusLabel = NewLabel("Suporte inicial: STR, STR NOT, AND, AND NOT, OUT e END. UNKNOWN/RAW bloqueiam a conversão.", 8.5f, FontStyle.Regular, Warning, 16, 57);
            bar.Controls.Add(statusLabel);

            previewBox = new TextBox();
            previewBox.Dock = DockStyle.Fill;
            previewBox.Multiline = true;
            previewBox.ReadOnly = true;
            previewBox.WordWrap = false;
            previewBox.ScrollBars = ScrollBars.Both;
            previewBox.Font = new Font("Consolas", 9.4f);
            previewBox.BackColor = OpenLadderPalette.Canvas;
            previewBox.ForeColor = OpenLadderPalette.Fore;
            Controls.Add(previewBox);
            previewBox.BringToFront();
            DockOrder.Apply(this, previewBox, bar, header);

        }

        private void OpenIl()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "IL verificada (*.verified.il.txt)|*.verified.il.txt|Texto (*.txt)|*.txt|Todos (*.*)|*.*";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            currentIlPath = dlg.FileName;
            ParseIl(currentIlPath);
            Analyze();
        }

        private void ParseIl(string path)
        {
            instructions.Clear();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            Regex rx = new Regex(@"^\s*(\d{4})\s*:\s*([^;]+)");
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";")) continue;
                Match m = rx.Match(line);
                if (!m.Success) continue;
                int step;
                if (!int.TryParse(m.Groups[1].Value, out step)) continue;
                string body = m.Groups[2].Value.Trim();
                TP02IlInstruction ins = ParseInstructionBody(step, body, line);
                instructions.Add(ins);
            }
        }

        private static TP02IlInstruction ParseInstructionBody(int step, string body, string source)
        {
            TP02IlInstruction x = new TP02IlInstruction();
            x.Step = step;
            x.Source = source;
            string upper = body.ToUpperInvariant().Trim();
            string[] multi = new string[] { "STR NOT", "AND NOT", "OR NOT" };
            int i;
            for (i = 0; i < multi.Length; i++)
            {
                if (upper == multi[i] || upper.StartsWith(multi[i] + " "))
                {
                    x.Operation = multi[i];
                    x.Operand = upper.Length > multi[i].Length ? upper.Substring(multi[i].Length).Trim() : string.Empty;
                    return x;
                }
            }
            int space = upper.IndexOf(' ');
            if (space < 0)
            {
                x.Operation = upper;
                return x;
            }
            x.Operation = upper.Substring(0, space).Trim();
            x.Operand = upper.Substring(space + 1).Trim();
            return x;
        }

        private void Analyze()
        {
            rungs.Clear();
            generatedProject = string.Empty;
            if (instructions.Count == 0)
            {
                previewBox.Text = "Nenhuma instrução IL reconhecida.";
                statusLabel.Text = "Nenhuma instrução carregada.";
                return;
            }

            List<string> errors = new List<string>();
            TP02LadderBuildRung current = null;
            int i;
            for (i = 0; i < instructions.Count; i++)
            {
                TP02IlInstruction ins = instructions[i];
                string op = ins.Operation;

                if (op == "UNKNOWN" || ins.Operand.StartsWith("RAW=", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Passo " + ins.Step.ToString("0000") + ": instrução/operando não comprovado: " + ins.Source);
                    continue;
                }

                if (op == "STR" || op == "STR NOT")
                {
                    if (current != null && current.Output == null)
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": novo STR antes de fechar o linha anterior com OUT.");
                    current = new TP02LadderBuildRung();
                    current.Conditions.Add(NewElement(
                        op == "STR NOT" ? LadderProjectElementKind.ContactNormallyClosed : LadderProjectElementKind.ContactNormallyOpen,
                        ins.Operand));
                    rungs.Add(current);
                    continue;
                }

                if (op == "AND" || op == "AND NOT")
                {
                    if (current == null)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": " + op + " sem STR inicial.");
                        continue;
                    }
                    if (current.Conditions.Count >= 7)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": mais de 7 condições em série; o formato gráfico atual usa 7 colunas de condição.");
                        continue;
                    }
                    current.Conditions.Add(NewElement(
                        op == "AND NOT" ? LadderProjectElementKind.ContactNormallyClosed : LadderProjectElementKind.ContactNormallyOpen,
                        ins.Operand));
                    continue;
                }

                if (op == "OUT")
                {
                    if (current == null)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": OUT sem linha iniciado por STR.");
                        continue;
                    }
                    if (current.Output != null)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": linha já possui saída.");
                        continue;
                    }
                    current.Output = NewElement(LadderProjectElementKind.Coil, ins.Operand);
                    current = null;
                    continue;
                }

                if (op == "END")
                {
                    TP02LadderBuildRung end = new TP02LadderBuildRung();
                    end.Output = NewElement(LadderProjectElementKind.End, "F-00");
                    rungs.Add(end);
                    current = null;
                    continue;
                }

                errors.Add("Passo " + ins.Step.ToString("0000") + ": operação ainda não suportada pela reconstrução segura: " + op + ".");
            }

            if (current != null && current.Output == null) errors.Add("A última linha não foi fechada com OUT.");

            StringBuilder report = new StringBuilder();
            report.AppendLine("TP02 — IL VERIFICADA → LADDER");
            report.AppendLine(new string('=', 84));
            report.AppendLine("Arquivo: " + currentIlPath);
            report.AppendLine("Instruções reconhecidas: " + instructions.Count.ToString());
            report.AppendLine("Linhas candidatos: " + rungs.Count.ToString());
            report.AppendLine("Erros/bloqueios: " + errors.Count.ToString());
            report.AppendLine();

            if (errors.Count > 0)
            {
                report.AppendLine("CONVERSÃO BLOQUEADA");
                report.AppendLine(new string('-', 84));
                for (i = 0; i < errors.Count; i++) report.AppendLine("• " + errors[i]);
                report.AppendLine();
                report.AppendLine("Nenhum .pladder será gerado enquanto houver instruções UNKNOWN, RAW ou estruturas não suportadas.");
                previewBox.Text = report.ToString();
                statusLabel.Text = "Conversão bloqueada por " + errors.Count.ToString() + " item(ns).";
                generatedProject = string.Empty;
                return;
            }

            generatedProject = BuildPladder(rungs);
            report.AppendLine("CONVERSÃO LIBERADA");
            report.AppendLine(new string('-', 84));
            report.AppendLine("O projeto abaixo é compatível com PC12-LADDER|2 e pode ser aberto no Ladder Studio.");
            report.AppendLine();
            report.Append(generatedProject);
            previewBox.Text = report.ToString();
            statusLabel.Text = "Projeto Ladder reconstruído em memória. Use SALVAR .PLADDER.";
        }

        private static string BuildPladder(List<TP02LadderBuildRung> source)
        {
            LadderProjectDocument document = new LadderProjectDocument();
            for (int rungIndex = 0; rungIndex < source.Count; rungIndex++)
            {
                TP02LadderBuildRung sourceRung = source[rungIndex];
                LadderProjectRung targetRung = new LadderProjectRung();
                for (int column = 0; column < 7 && column < sourceRung.Conditions.Count; column++)
                    targetRung.Series[column] = sourceRung.Conditions[column];

                if (sourceRung.Output != null) targetRung.Series[7] = sourceRung.Output;
                document.Rungs.Add(targetRung);
            }

            return LadderProjectCodec.Serialize(document);
        }

        private void SavePladder()
        {
            if (string.IsNullOrEmpty(generatedProject))
            {
                MessageBox.Show("Não existe projeto seguro para salvar. Corrija os bloqueios mostrados na análise.", "IL → Ladder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Projeto Ladder moderno (*.pladder)|*.pladder";
            dlg.DefaultExt = "pladder";
            dlg.AddExtension = true;
            dlg.FileName = string.IsNullOrEmpty(currentIlPath) ? "TP02_reconstruido.pladder" : Path.GetFileNameWithoutExtension(currentIlPath).Replace(".verified.il", string.Empty) + ".pladder";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, generatedProject, Encoding.UTF8);
            statusLabel.Text = "Projeto salvo: " + dlg.FileName;
        }

        private static LadderProjectElement NewElement(LadderProjectElementKind kind, string address)
        {
            LadderProjectElement element = new LadderProjectElement();
            element.Kind = kind;
            element.Address = address == null ? string.Empty : address.Trim().ToUpperInvariant();
            return element;
        }

        private Button ButtonAt(string text, int left, int top, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 36);
            b.FlatStyle = FlatStyle.Flat;
            b.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            if (primary)
            {
                b.BackColor = Accent;
                b.ForeColor = OpenLadderPalette.OnAccent;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = OpenLadderPalette.Chrome;
                b.ForeColor = Navy;
                b.FlatAppearance.BorderColor = OpenLadderPalette.Border;
            }
            return b;
        }

        private Label NewLabel(string text, float size, FontStyle style, Color color, int left, int top)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.Location = new Point(left, top);
            return l;
        }
    }
}
