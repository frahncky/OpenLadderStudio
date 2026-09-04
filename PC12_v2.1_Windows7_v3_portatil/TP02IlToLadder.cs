using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class TP02IlToLadderProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
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
        public readonly List<string> Conditions = new List<string>();
        public string Output = string.Empty;
    }

    internal sealed class TP02IlToLadderForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);

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
            header.BackColor = Color.White;
            Controls.Add(header);

            header.Controls.Add(NewLabel("RECONSTRUÇÃO SEGURA — IL → LADDER", 15.0f, FontStyle.Bold, Navy, 22, 12));
            header.Controls.Add(NewLabel("Converte somente IL verificada e um subconjunto estrutural comprovável para o formato .pladder do Studio.", 8.8f, FontStyle.Regular, TextSecondary, 24, 43));

            Label safe = new Label();
            safe.Text = "OFFLINE • NÃO ESCREVE NO PLC";
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
            previewBox.BackColor = Color.FromArgb(20, 28, 36);
            previewBox.ForeColor = Color.FromArgb(220, 233, 245);
            Controls.Add(previewBox);
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
                    if (current != null && string.IsNullOrEmpty(current.Output))
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": novo STR antes de fechar o rung anterior com OUT.");
                    current = new TP02LadderBuildRung();
                    current.Conditions.Add((op == "STR NOT" ? "NC:" : "NO:") + Encode(ins.Operand));
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
                    current.Conditions.Add((op == "AND NOT" ? "NC:" : "NO:") + Encode(ins.Operand));
                    continue;
                }

                if (op == "OUT")
                {
                    if (current == null)
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": OUT sem rung iniciado por STR.");
                        continue;
                    }
                    if (!string.IsNullOrEmpty(current.Output))
                    {
                        errors.Add("Passo " + ins.Step.ToString("0000") + ": rung já possui saída.");
                        continue;
                    }
                    current.Output = "COIL:" + Encode(ins.Operand);
                    current = null;
                    continue;
                }

                if (op == "END")
                {
                    TP02LadderBuildRung end = new TP02LadderBuildRung();
                    end.Output = "END";
                    rungs.Add(end);
                    current = null;
                    continue;
                }

                errors.Add("Passo " + ins.Step.ToString("0000") + ": operação ainda não suportada pela reconstrução segura: " + op + ".");
            }

            if (current != null && string.IsNullOrEmpty(current.Output)) errors.Add("Último rung não foi fechado com OUT.");

            StringBuilder report = new StringBuilder();
            report.AppendLine("TP02 — IL VERIFICADA → LADDER");
            report.AppendLine(new string('=', 84));
            report.AppendLine("Arquivo: " + currentIlPath);
            report.AppendLine("Instruções reconhecidas: " + instructions.Count.ToString());
            report.AppendLine("Rungs candidatos: " + rungs.Count.ToString());
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
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PC12-LADDER|2");
            int r;
            for (r = 0; r < source.Count; r++)
            {
                TP02LadderBuildRung rung = source[r];
                sb.Append("RUNG");
                int c;
                for (c = 0; c < 8; c++)
                {
                    string main = "EMPTY";
                    if (c < 7 && c < rung.Conditions.Count) main = rung.Conditions[c];
                    if (c == 7 && !string.IsNullOrEmpty(rung.Output)) main = rung.Output;
                    sb.Append('|').Append(main).Append("~EMPTY");
                }
                sb.AppendLine();
            }
            return sb.ToString();
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

        private static string Encode(string value)
        {
            return Uri.EscapeDataString(value == null ? string.Empty : value.Trim().ToUpperInvariant());
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
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Navy;
                b.FlatAppearance.BorderColor = Color.FromArgb(194, 205, 216);
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
