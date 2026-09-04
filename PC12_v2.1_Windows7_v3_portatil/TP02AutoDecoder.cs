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
    internal static class TP02AutoDecoderProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02AutoDecoderForm());
        }
    }

    internal sealed class TP02Rule
    {
        public string Instruction = string.Empty;
        public int OpcodeMask;
        public int OpcodeValue;
        public int OperandMask;
        public int Samples;
        public string Status = "CANDIDATE";

        public bool Matches(int word)
        {
            return (word & OpcodeMask) == OpcodeValue;
        }
    }

    internal sealed class TP02CampaignOperand
    {
        public string Instruction = string.Empty;
        public string Operand = string.Empty;
        public int Word;
    }

    internal sealed class TP02AutoDecodeRow
    {
        public int Step;
        public string Word = string.Empty;
        public string Verified = "UNKNOWN";
        public string Suggestion = string.Empty;
        public string Operand = string.Empty;
        public string Status = string.Empty;
    }

    internal sealed class TP02AutoDecoderForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);

        private readonly List<TP02Rule> rules = new List<TP02Rule>();
        private readonly List<TP02CampaignOperand> operands = new List<TP02CampaignOperand>();
        private readonly List<TP02AutoDecodeRow> rows = new List<TP02AutoDecodeRow>();

        private DataGridView grid;
        private TextBox logBox;
        private Label statusLabel;
        private CheckBox candidateCheck;
        private string dumpPath = string.Empty;
        private string rulesPath = string.Empty;
        private string campaignPath = string.Empty;

        public TP02AutoDecoderForm()
        {
            Text = "TP02 Auto Decoder";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 700);
            Size = new Size(1400, 850);
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

            header.Controls.Add(NewLabel("DECODIFICADOR AUTOMÁTICO TP02", 15.0f, FontStyle.Bold, Navy, 22, 12));
            header.Controls.Add(NewLabel("Aplica regras de opcode sem promover hipóteses a fatos. CONFIRMED é aceito; CANDIDATE é apenas sugestão.", 8.8f, FontStyle.Regular, TextSecondary, 24, 43));

            Label safe = new Label();
            safe.Text = "OFFLINE • NÃO ESCREVE NO PLC";
            safe.Dock = DockStyle.Right;
            safe.Width = 290;
            safe.TextAlign = ContentAlignment.MiddleCenter;
            safe.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            safe.ForeColor = Success;
            header.Controls.Add(safe);

            Panel commands = new Panel();
            commands.Dock = DockStyle.Top;
            commands.Height = 92;
            commands.BackColor = Canvas;
            Controls.Add(commands);

            Button dump = ButtonAt("ABRIR RBP DUMP", 16, 12, 140, true);
            dump.Click += delegate { OpenDump(); };
            commands.Controls.Add(dump);

            Button rule = ButtonAt("ABRIR REGRAS", 168, 12, 125, false);
            rule.Click += delegate { OpenRules(); };
            commands.Controls.Add(rule);

            Button campaign = ButtonAt("ABRIR CAMPANHA", 305, 12, 135, false);
            campaign.Click += delegate { OpenCampaign(); };
            commands.Controls.Add(campaign);

            Button decode = ButtonAt("DECODIFICAR", 452, 12, 120, false);
            decode.Click += delegate { Decode(); };
            commands.Controls.Add(decode);

            Button export = ButtonAt("EXPORTAR IL", 584, 12, 120, false);
            export.Click += delegate { ExportIl(); };
            commands.Controls.Add(export);

            candidateCheck = new CheckBox();
            candidateCheck.Text = "Mostrar sugestões CANDIDATE";
            candidateCheck.Checked = true;
            candidateCheck.AutoSize = true;
            candidateCheck.Location = new Point(724, 21);
            candidateCheck.ForeColor = Warning;
            candidateCheck.CheckedChanged += delegate { RefreshGrid(); };
            commands.Controls.Add(candidateCheck);

            statusLabel = NewLabel("Carregue um dump e um arquivo .rules.tsv.", 8.5f, FontStyle.Regular, TextSecondary, 16, 61);
            commands.Controls.Add(statusLabel);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 480;
            split.BackColor = Canvas;
            Controls.Add(split);
            split.BringToFront();
            DockOrder.Apply(this, split, commands, header);

            grid = new DataGridView();
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
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
            grid.Font = new Font("Consolas", 9.0f);
            AddColumn("Passo", "Step", 70);
            AddColumn("WORD", "Word", 90);
            AddColumn("Verificado", "Verified", 170);
            AddColumn("Sugestão", "Suggestion", 170);
            AddColumn("Operando", "Operand", 170);
            AddColumn("Regra", "Status", 110);
            split.Panel1.Controls.Add(grid);

            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.WordWrap = false;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.Font = new Font("Consolas", 9.1f);
            logBox.BackColor = Color.FromArgb(20, 28, 36);
            logBox.ForeColor = Color.FromArgb(220, 233, 245);
            split.Panel2.Controls.Add(logBox);

        }

        private void OpenDump()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Dump RBP (*.rbpdump)|*.rbpdump|Texto (*.txt)|*.txt|Todos (*.*)|*.*";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            dumpPath = dlg.FileName;
            statusLabel.Text = "Dump: " + Path.GetFileName(dumpPath);
            if (rules.Count > 0) Decode();
        }

        private void OpenRules()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Regras TP02 (*.rules.tsv)|*.rules.tsv|TSV (*.tsv)|*.tsv|Texto (*.txt)|*.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            rulesPath = dlg.FileName;
            LoadRules(rulesPath);
            statusLabel.Text = rules.Count.ToString() + " regra(s) carregadas de " + Path.GetFileName(rulesPath) + ".";
            if (!string.IsNullOrEmpty(dumpPath)) Decode();
        }

        private void OpenCampaign()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Campanha TP02 (*.tpcampaign.tsv)|*.tpcampaign.tsv|TSV (*.tsv)|*.tsv|Texto (*.txt)|*.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            campaignPath = dlg.FileName;
            LoadCampaignOperands(campaignPath);
            statusLabel.Text = operands.Count.ToString() + " operando(s) conhecidos carregados da campanha.";
            if (!string.IsNullOrEmpty(dumpPath) && rules.Count > 0) Decode();
        }

        private void LoadRules(string path)
        {
            rules.Clear();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].StartsWith("#")) continue;
                string[] p = lines[i].Split('\t');
                if (p.Length < 6) continue;
                int mask, value, operandMask, samples;
                if (!TryHex24(p[1], out mask) || !TryHex24(p[2], out value) || !TryHex24(p[3], out operandMask)) continue;
                if (!int.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out samples)) samples = 0;
                TP02Rule r = new TP02Rule();
                r.Instruction = p[0].Trim().ToUpperInvariant();
                r.OpcodeMask = mask;
                r.OpcodeValue = value;
                r.OperandMask = operandMask;
                r.Samples = samples;
                r.Status = p[5].Trim().ToUpperInvariant();
                rules.Add(r);
            }
        }

        private void LoadCampaignOperands(string path)
        {
            operands.Clear();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].StartsWith("#")) continue;
                string[] p = lines[i].Split('\t');
                if (p.Length < 8) continue;
                int word;
                if (!TryHex24(p[7], out word)) continue;
                TP02CampaignOperand x = new TP02CampaignOperand();
                x.Instruction = p[2].Trim().ToUpperInvariant();
                x.Operand = p[3].Trim().ToUpperInvariant();
                x.Word = word;
                operands.Add(x);
            }
        }

        private void Decode()
        {
            if (string.IsNullOrEmpty(dumpPath) || !File.Exists(dumpPath))
            {
                MessageBox.Show("Carregue um dump RBP.", "TP02 Auto Decoder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (rules.Count == 0)
            {
                MessageBox.Show("Carregue primeiro um arquivo de regras .rules.tsv.", "TP02 Auto Decoder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<TP02MachineStep> steps = ParseDump(dumpPath);
            rows.Clear();
            int confirmed = 0, suggested = 0, unknown = 0, ambiguous = 0;
            int i;
            for (i = 0; i < steps.Count; i++)
            {
                TP02MachineStep s = steps[i];
                int word = ParseWord(s.Word);
                List<TP02Rule> confirmedMatches = new List<TP02Rule>();
                List<TP02Rule> candidateMatches = new List<TP02Rule>();
                int j;
                for (j = 0; j < rules.Count; j++)
                {
                    if (!rules[j].Matches(word)) continue;
                    if (string.Equals(rules[j].Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase)) confirmedMatches.Add(rules[j]);
                    else candidateMatches.Add(rules[j]);
                }

                TP02AutoDecodeRow row = new TP02AutoDecodeRow();
                row.Step = s.Step;
                row.Word = s.Word;

                if (confirmedMatches.Count == 1)
                {
                    TP02Rule r = confirmedMatches[0];
                    row.Verified = r.Instruction;
                    row.Operand = ResolveOperand(r, word);
                    row.Status = "CONFIRMED";
                    confirmed++;
                }
                else if (confirmedMatches.Count > 1)
                {
                    row.Verified = "AMBIGUOUS";
                    row.Status = "CONFIRMED x" + confirmedMatches.Count.ToString();
                    row.Suggestion = JoinInstructions(confirmedMatches);
                    ambiguous++;
                }
                else if (candidateMatches.Count == 1)
                {
                    TP02Rule r = candidateMatches[0];
                    row.Suggestion = r.Instruction;
                    row.Operand = ResolveOperand(r, word);
                    row.Status = "CANDIDATE";
                    suggested++;
                }
                else if (candidateMatches.Count > 1)
                {
                    row.Suggestion = "AMBIGUOUS: " + JoinInstructions(candidateMatches);
                    row.Status = "CANDIDATE x" + candidateMatches.Count.ToString();
                    ambiguous++;
                }
                else
                {
                    row.Verified = "UNKNOWN";
                    unknown++;
                }
                rows.Add(row);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("TP02 AUTO DECODER — RESULTADO");
            sb.AppendLine(new string('=', 80));
            sb.AppendLine("Dump       : " + dumpPath);
            sb.AppendLine("Regras     : " + rulesPath);
            sb.AppendLine("Campanha   : " + (string.IsNullOrEmpty(campaignPath) ? "não carregada" : campaignPath));
            sb.AppendLine("Passos     : " + rows.Count.ToString());
            sb.AppendLine("CONFIRMED  : " + confirmed.ToString());
            sb.AppendLine("CANDIDATE  : " + suggested.ToString());
            sb.AppendLine("AMBIGUOUS  : " + ambiguous.ToString());
            sb.AppendLine("UNKNOWN    : " + unknown.ToString());
            sb.AppendLine();
            sb.AppendLine("Regra de segurança: CANDIDATE nunca é gravado como instrução verificada na exportação IL.");
            sb.AppendLine("Operandos fora das amostras conhecidas aparecem como RAW=0x...... até a codificação de endereço ser comprovada.");
            logBox.Text = sb.ToString();
            RefreshGrid();
            statusLabel.Text = "Decodificação concluída: " + confirmed.ToString() + " verificada(s), " + suggested.ToString() + " sugestão(ões).";
        }

        private string ResolveOperand(TP02Rule rule, int word)
        {
            int i;
            for (i = 0; i < operands.Count; i++)
            {
                TP02CampaignOperand x = operands[i];
                if (!string.Equals(x.Instruction, rule.Instruction, StringComparison.OrdinalIgnoreCase)) continue;
                if ((x.Word & rule.OperandMask) == (word & rule.OperandMask)) return x.Operand;
            }
            return rule.OperandMask == 0 ? string.Empty : "RAW=0x" + (word & rule.OperandMask).ToString("X6", CultureInfo.InvariantCulture);
        }

        private void RefreshGrid()
        {
            if (grid == null) return;
            grid.Rows.Clear();
            int i;
            for (i = 0; i < rows.Count; i++)
            {
                TP02AutoDecodeRow r = rows[i];
                string suggestion = candidateCheck != null && candidateCheck.Checked ? r.Suggestion : string.Empty;
                int row = grid.Rows.Add(r.Step.ToString("0000"), r.Word, r.Verified, suggestion, r.Operand, r.Status);
                if (r.Status.StartsWith("CONFIRMED")) grid.Rows[row].DefaultCellStyle.ForeColor = Success;
                else if (r.Status.StartsWith("CANDIDATE") || r.Status.StartsWith("AMBIGUOUS")) grid.Rows[row].DefaultCellStyle.ForeColor = Warning;
            }
        }

        private void ExportIl()
        {
            if (rows.Count == 0)
            {
                MessageBox.Show("Execute a decodificação primeiro.", "TP02 Auto Decoder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "IL verificada (*.verified.il.txt)|*.verified.il.txt|Texto (*.txt)|*.txt";
            dlg.FileName = string.IsNullOrEmpty(dumpPath) ? "TP02_verified.il.txt" : Path.GetFileNameWithoutExtension(dumpPath) + ".verified.il.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; PC12 Studio TP02 — IL automática segura");
            sb.AppendLine("; Apenas regras CONFIRMED viram instrução. CANDIDATE permanece comentário.");
            int i;
            for (i = 0; i < rows.Count; i++)
            {
                TP02AutoDecodeRow r = rows[i];
                if (r.Status.StartsWith("CONFIRMED") && r.Verified != "AMBIGUOUS")
                {
                    sb.Append(r.Step.ToString("0000")).Append(": ").Append(r.Verified);
                    if (!string.IsNullOrEmpty(r.Operand)) sb.Append(' ').Append(r.Operand);
                    sb.Append("    ; ").Append(r.Word).AppendLine(" [CONFIRMED]");
                }
                else
                {
                    sb.Append(r.Step.ToString("0000")).Append(": UNKNOWN    ; ").Append(r.Word);
                    if (!string.IsNullOrEmpty(r.Suggestion)) sb.Append(" | sugestão ").Append(r.Suggestion).Append(' ').Append(r.Operand);
                    sb.AppendLine();
                }
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            statusLabel.Text = "IL verificada salva: " + dlg.FileName;
        }

        private static List<TP02MachineStep> ParseDump(string path)
        {
            List<TP02MachineStep> result = new List<TP02MachineStep>();
            string[] lines = File.ReadAllLines(path);
            Regex rx = new Regex(@"^\s*(\d{4})\s+([0-9A-Fa-f]{6})(?:\s|$)");
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                Match m = rx.Match(lines[i]);
                if (!m.Success) continue;
                int step;
                if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out step)) continue;
                TP02MachineStep s = new TP02MachineStep();
                s.Step = step;
                s.Word = m.Groups[2].Value.ToUpperInvariant();
                result.Add(s);
            }
            return result;
        }

        private static int ParseWord(string word)
        {
            int value;
            if (!int.TryParse(word, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return 0;
            return value & 0xFFFFFF;
        }

        private static bool TryHex24(string text, out int value)
        {
            text = (text ?? string.Empty).Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            if (!int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return false;
            value &= 0xFFFFFF;
            return true;
        }

        private static string JoinInstructions(List<TP02Rule> list)
        {
            StringBuilder sb = new StringBuilder();
            int i;
            for (i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(list[i].Instruction);
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
