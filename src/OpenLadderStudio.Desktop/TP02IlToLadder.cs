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
            instructions.AddRange(TP02IlToLadderConverter.Parse(File.ReadAllLines(path, Encoding.UTF8)));
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

            TP02IlConversion conversion = TP02IlToLadderConverter.Convert(instructions);
            rungs.AddRange(conversion.Rungs);
            List<string> errors = conversion.Errors;
            int i;

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
            return TP02IlToLadderConverter.BuildPladder(source);
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
