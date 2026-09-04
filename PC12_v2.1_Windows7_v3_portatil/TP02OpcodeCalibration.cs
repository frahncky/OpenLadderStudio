using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class TP02CalibrationProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02OpcodeCalibrationForm());
        }
    }

    internal sealed class TP02CalibrationSample
    {
        public string FilePath = string.Empty;
        public string Instruction = "UNKNOWN";
        public string Operand = string.Empty;
        public int Step = -1;
        public string Word = string.Empty;
    }

    internal sealed class TP02OpcodeCalibrationForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);

        private readonly List<TP02CalibrationSample> samples = new List<TP02CalibrationSample>();
        private DataGridView grid;
        private ComboBox instructionCombo;
        private TextBox operandBox;
        private NumericUpDown stepBox;
        private TextBox reportBox;
        private Label statusLabel;
        private string lastReport = string.Empty;

        public TP02OpcodeCalibrationForm()
        {
            Text = "TP02 Opcode Calibration Lab";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 700);
            Size = new Size(1380, 850);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
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

            header.Controls.Add(NewLabel("CALIBRAÇÃO AUTOMÁTICA DE OPCODES TP02", 15.0f, FontStyle.Bold, Navy, 22, 12));
            header.Controls.Add(NewLabel("Aprende máscaras de opcode e de operando comparando dumps RBP controlados.", 8.8f, FontStyle.Regular, TextSecondary, 24, 43));

            Label safe = new Label();
            safe.Text = "OFFLINE • NÃO ESCREVE NO PLC";
            safe.Dock = DockStyle.Right;
            safe.Width = 280;
            safe.TextAlign = ContentAlignment.MiddleCenter;
            safe.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            safe.ForeColor = Success;
            header.Controls.Add(safe);

            Panel commands = new Panel();
            commands.Dock = DockStyle.Top;
            commands.Height = 96;
            commands.BackColor = Canvas;
            Controls.Add(commands);

            Button add = ButtonAt("ADICIONAR DUMP", 16, 12, 135, true);
            add.Click += delegate { AddSample(); };
            commands.Controls.Add(add);

            Button remove = ButtonAt("REMOVER", 163, 12, 95, false);
            remove.Click += delegate { RemoveSelected(); };
            commands.Controls.Add(remove);

            Button infer = ButtonAt("INFERIR MÁSCARAS", 270, 12, 145, false);
            infer.Click += delegate { InferPatterns(); };
            commands.Controls.Add(infer);

            Button export = ButtonAt("SALVAR PADRÕES", 427, 12, 130, false);
            export.Click += delegate { SavePatterns(); };
            commands.Controls.Add(export);

            Button guide = ButtonAt("ROTEIRO DE TESTES", 569, 12, 140, false);
            guide.Click += delegate { ShowTestGuide(); };
            commands.Controls.Add(guide);

            statusLabel = NewLabel("Adicione pelo menos dois dumps obtidos de programas que diferem em apenas um item.", 8.5f, FontStyle.Regular, TextSecondary, 730, 23);
            commands.Controls.Add(statusLabel);

            commands.Controls.Add(NewLabel("Instrução", 8.0f, FontStyle.Bold, TextSecondary, 16, 58));
            instructionCombo = new ComboBox();
            instructionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            instructionCombo.Location = new Point(86, 55);
            instructionCombo.Size = new Size(130, 25);
            instructionCombo.Items.AddRange(new object[] { "UNKNOWN", "STR", "STR NOT", "AND", "AND NOT", "OR", "OR NOT", "AND STR", "OR STR", "OUT", "TMR", "CNT", "FUN", "END" });
            instructionCombo.SelectedItem = "STR";
            commands.Controls.Add(instructionCombo);

            commands.Controls.Add(NewLabel("Operando", 8.0f, FontStyle.Bold, TextSecondary, 230, 58));
            operandBox = new TextBox();
            operandBox.CharacterCasing = CharacterCasing.Upper;
            operandBox.Text = "X0001";
            operandBox.Location = new Point(296, 55);
            operandBox.Size = new Size(105, 25);
            commands.Controls.Add(operandBox);

            commands.Controls.Add(NewLabel("Passo", 8.0f, FontStyle.Bold, TextSecondary, 418, 58));
            stepBox = new NumericUpDown();
            stepBox.Minimum = 0;
            stepBox.Maximum = 4000;
            stepBox.Value = 0;
            stepBox.Location = new Point(465, 55);
            stepBox.Size = new Size(76, 25);
            commands.Controls.Add(stepBox);

            Label hint = NewLabel("Ex.: salve STR X0001 e STR X0002 em dumps separados, no mesmo passo. Depois repita STR X0001 e AND X0001.", 8.3f, FontStyle.Regular, Warning, 569, 59);
            commands.Controls.Add(hint);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 330;
            split.BackColor = Canvas;
            Controls.Add(split);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.AutoGenerateColumns = false;
            AddColumn("Arquivo", "File", 300);
            AddColumn("Instrução", "Instruction", 110);
            AddColumn("Operando", "Operand", 110);
            AddColumn("Passo", "Step", 70);
            AddColumn("WORD", "Word", 100);
            AddColumn("Bits", "Bits", 210);
            split.Panel1.Controls.Add(grid);

            reportBox = new TextBox();
            reportBox.Dock = DockStyle.Fill;
            reportBox.Multiline = true;
            reportBox.ReadOnly = true;
            reportBox.WordWrap = false;
            reportBox.ScrollBars = ScrollBars.Both;
            reportBox.Font = new Font("Consolas", 9.2f);
            reportBox.BackColor = Color.FromArgb(20, 28, 36);
            reportBox.ForeColor = Color.FromArgb(220, 233, 245);
            split.Panel2.Controls.Add(reportBox);

            header.BringToFront();
            commands.BringToFront();
        }

        private void AddSample()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Dump RBP (*.rbpdump)|*.rbpdump|Texto (*.txt)|*.txt|Todos (*.*)|*.*";
            dlg.Title = "Selecione um dump de calibração";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int step = (int)stepBox.Value;
            string word = FindWordAtStep(dlg.FileName, step);
            if (string.IsNullOrEmpty(word))
            {
                MessageBox.Show("O passo " + step.ToString("0000") + " não foi encontrado nesse dump.", "Calibração TP02", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TP02CalibrationSample s = new TP02CalibrationSample();
            s.FilePath = dlg.FileName;
            s.Instruction = instructionCombo.SelectedItem == null ? "UNKNOWN" : instructionCombo.SelectedItem.ToString();
            s.Operand = (operandBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            s.Step = step;
            s.Word = word;
            samples.Add(s);
            RefreshGrid();
            statusLabel.Text = samples.Count.ToString() + " amostra(s) carregadas.";
        }

        private void RemoveSelected()
        {
            if (grid.SelectedRows.Count == 0) return;
            int index = grid.SelectedRows[0].Index;
            if (index < 0 || index >= samples.Count) return;
            samples.RemoveAt(index);
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            grid.Rows.Clear();
            int i;
            for (i = 0; i < samples.Count; i++)
            {
                TP02CalibrationSample s = samples[i];
                grid.Rows.Add(Path.GetFileName(s.FilePath), s.Instruction, s.Operand, s.Step.ToString("0000"), s.Word, ToBinary24(ParseWord(s.Word)));
            }
        }

        private void InferPatterns()
        {
            if (samples.Count < 2)
            {
                MessageBox.Show("Adicione pelo menos dois dumps controlados.", "Calibração TP02", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("TP02 OPCODE CALIBRATION — INFERÊNCIA");
            sb.AppendLine(new string('=', 92));
            sb.AppendLine("Amostras: " + samples.Count.ToString());
            sb.AppendLine();

            Dictionary<string, List<TP02CalibrationSample>> byInstruction = new Dictionary<string, List<TP02CalibrationSample>>(StringComparer.OrdinalIgnoreCase);
            int i;
            for (i = 0; i < samples.Count; i++)
            {
                string key = samples[i].Instruction;
                if (!byInstruction.ContainsKey(key)) byInstruction[key] = new List<TP02CalibrationSample>();
                byInstruction[key].Add(samples[i]);
            }

            sb.AppendLine("1) PADRÕES POR INSTRUÇÃO");
            sb.AppendLine(new string('-', 92));
            foreach (KeyValuePair<string, List<TP02CalibrationSample>> kv in byInstruction)
            {
                List<TP02CalibrationSample> group = kv.Value;
                if (group.Count < 2)
                {
                    sb.AppendLine(kv.Key.PadRight(10) + " insuficiente: precisa de pelo menos 2 operandos diferentes.");
                    continue;
                }

                int first = ParseWord(group[0].Word);
                int varyingMask = 0;
                int commonOnes = 0xFFFFFF;
                int commonZeros = 0xFFFFFF;
                for (i = 0; i < group.Count; i++)
                {
                    int w = ParseWord(group[i].Word);
                    varyingMask |= (first ^ w);
                    commonOnes &= w;
                    commonZeros &= (~w) & 0xFFFFFF;
                }
                int opcodeMask = (~varyingMask) & 0xFFFFFF;
                int opcodeValue = first & opcodeMask;

                sb.AppendLine(kv.Key);
                sb.AppendLine("  samples      : " + JoinWords(group));
                sb.AppendLine("  varying mask : 0x" + varyingMask.ToString("X6") + "  " + ToBinary24(varyingMask));
                sb.AppendLine("  opcode mask  : 0x" + opcodeMask.ToString("X6") + "  " + ToBinary24(opcodeMask));
                sb.AppendLine("  opcode value : 0x" + opcodeValue.ToString("X6") + "  " + ToBinary24(opcodeValue));
                sb.AppendLine("  candidate    : (WORD & 0x" + opcodeMask.ToString("X6") + ") == 0x" + opcodeValue.ToString("X6"));
                sb.AppendLine();
            }

            sb.AppendLine("2) COMPARAÇÕES COM MESMO OPERANDO");
            sb.AppendLine(new string('-', 92));
            int comparisons = 0;
            for (i = 0; i < samples.Count; i++)
            {
                int j;
                for (j = i + 1; j < samples.Count; j++)
                {
                    TP02CalibrationSample a = samples[i];
                    TP02CalibrationSample b = samples[j];
                    if (!string.Equals(a.Operand, b.Operand, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(a.Instruction, b.Instruction, StringComparison.OrdinalIgnoreCase)) continue;
                    int xor = ParseWord(a.Word) ^ ParseWord(b.Word);
                    sb.Append(a.Instruction).Append(' ').Append(a.Operand).Append("  ").Append(a.Word)
                      .Append("  vs  ").Append(b.Instruction).Append(' ').Append(b.Operand).Append("  ").Append(b.Word)
                      .Append("  XOR=0x").Append(xor.ToString("X6")).Append("  ").AppendLine(ToBinary24(xor));
                    comparisons++;
                }
            }
            if (comparisons == 0) sb.AppendLine("Nenhum par com mesmo operando e instruções diferentes foi carregado.");

            sb.AppendLine();
            sb.AppendLine("3) INTERPRETAÇÃO");
            sb.AppendLine(new string('-', 92));
            sb.AppendLine("• Bits que variam entre STR X0001 / STR X0002 são candidatos ao campo de operando.");
            sb.AppendLine("• Bits que mudam entre STR X0001 / AND X0001, mantendo o mesmo operando, são candidatos ao opcode.");
            sb.AppendLine("• Uma máscara só deve ser promovida a 'confirmada' após repetir o teste com vários endereços e mais de uma família X/Y/C/SC.");
            sb.AppendLine("• TMR/CNT usam 2 words; trate primeiro o word da instrução e depois o word de preset separadamente.");

            lastReport = sb.ToString();
            reportBox.Text = lastReport;
            statusLabel.Text = "Inferência concluída. Revise as máscaras antes de confirmar qualquer opcode.";
        }

        private void SavePatterns()
        {
            if (string.IsNullOrEmpty(lastReport)) InferPatterns();
            if (string.IsNullOrEmpty(lastReport)) return;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Relatório de calibração (*.cal.txt)|*.cal.txt|Texto (*.txt)|*.txt";
            dlg.FileName = "TP02_opcode_calibration.cal.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, lastReport, Encoding.UTF8);
            statusLabel.Text = "Relatório salvo: " + dlg.FileName;
        }

        private void ShowTestGuide()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ROTEIRO CONTROLADO PARA MAPEAR OPCODES TP02");
            sb.AppendLine(new string('=', 76));
            sb.AppendLine();
            sb.AppendLine("Mantenha cada projeto mínimo e altere somente UM item por vez.");
            sb.AppendLine();
            sb.AppendLine("Grupo A — descobrir bits do operando:");
            sb.AppendLine("  A1  STR X0001");
            sb.AppendLine("  A2  STR X0002");
            sb.AppendLine("  A3  STR X0004");
            sb.AppendLine("  A4  STR X0016");
            sb.AppendLine();
            sb.AppendLine("Grupo B — descobrir bits do opcode com mesmo operando:");
            sb.AppendLine("  B1  STR     X0001");
            sb.AppendLine("  B2  STR NOT X0001");
            sb.AppendLine("  B3  AND     X0001");
            sb.AppendLine("  B4  AND NOT X0001");
            sb.AppendLine("  B5  OR      X0001");
            sb.AppendLine("  B6  OR NOT  X0001");
            sb.AppendLine();
            sb.AppendLine("Grupo C — famílias de endereço:");
            sb.AppendLine("  C1  STR X0001");
            sb.AppendLine("  C2  STR Y0001");
            sb.AppendLine("  C3  STR C0001");
            sb.AppendLine("  C4  STR SC001");
            sb.AppendLine();
            sb.AppendLine("Grupo D — saídas:");
            sb.AppendLine("  D1  STR X0001 / OUT Y0001");
            sb.AppendLine("  D2  STR X0001 / OUT Y0002");
            sb.AppendLine("  D3  STR X0001 / OUT C0001");
            sb.AppendLine();
            sb.AppendLine("Grupo E — blocos de 2 words:");
            sb.AppendLine("  E1  TMR V0001 preset 10");
            sb.AppendLine("  E2  TMR V0002 preset 10");
            sb.AppendLine("  E3  TMR V0001 preset 20");
            sb.AppendLine("  E4  CNT V0001 preset 10");
            sb.AppendLine();
            sb.AppendLine("Para cada projeto: compile no PC12 original, leia exatamente a mesma faixa com RBP e salve o .rbpdump.");
            reportBox.Text = sb.ToString();
            lastReport = reportBox.Text;
            statusLabel.Text = "Roteiro de calibração exibido.";
        }

        private static string FindWordAtStep(string path, int step)
        {
            string[] lines = File.ReadAllLines(path);
            Regex rx = new Regex(@"^\s*(\d{4})\s+([0-9A-Fa-f]{6})(?:\s|$)");
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                Match m = rx.Match(lines[i]);
                if (!m.Success) continue;
                int parsed;
                if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) continue;
                if (parsed == step) return m.Groups[2].Value.ToUpperInvariant();
            }
            return string.Empty;
        }

        private static int ParseWord(string word)
        {
            int value;
            if (!int.TryParse(word, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return 0;
            return value & 0xFFFFFF;
        }

        private static string ToBinary24(int value)
        {
            string s = Convert.ToString(value & 0xFFFFFF, 2).PadLeft(24, '0');
            return s.Substring(0, 8) + " " + s.Substring(8, 8) + " " + s.Substring(16, 8);
        }

        private static string JoinWords(List<TP02CalibrationSample> group)
        {
            StringBuilder sb = new StringBuilder();
            int i;
            for (i = 0; i < group.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(group[i].Operand).Append('=').Append(group[i].Word);
            }
            return sb.ToString();
        }

        private void AddColumn(string header, string name, int width)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.Name = name;
            c.Width = width;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
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
