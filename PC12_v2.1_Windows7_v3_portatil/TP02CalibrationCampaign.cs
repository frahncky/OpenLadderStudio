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
    internal static class TP02CampaignProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02CalibrationCampaignForm());
        }
    }

    internal sealed class TP02CampaignItem
    {
        public string Id = string.Empty;
        public string Group = string.Empty;
        public string Instruction = string.Empty;
        public string Operand = string.Empty;
        public string Detail = string.Empty;
        public int Step;
        public string FilePath = string.Empty;
        public string Word1 = string.Empty;
        public string Word2 = string.Empty;

        public bool NeedsSecondWord
        {
            get { return string.Equals(Instruction, "TMR", StringComparison.OrdinalIgnoreCase) || string.Equals(Instruction, "CNT", StringComparison.OrdinalIgnoreCase); }
        }
    }

    internal sealed class TP02CalibrationCampaignForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);

        private readonly List<TP02CampaignItem> items = new List<TP02CampaignItem>();
        private DataGridView grid;
        private TextBox reportBox;
        private Label statusLabel;
        private string lastReport = string.Empty;
        private string lastRules = string.Empty;

        public TP02CalibrationCampaignForm()
        {
            Text = "TP02 Calibration Campaign";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1120, 700);
            Size = new Size(1420, 860);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            LoadDefaultCampaign();
            RefreshGrid();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 74;
            header.BackColor = Color.White;
            Controls.Add(header);

            header.Controls.Add(NewLabel("CAMPANHA GUIADA DE CALIBRAÇÃO TP02", 15.0f, FontStyle.Bold, Navy, 22, 12));
            header.Controls.Add(NewLabel("Organiza os testes controlados, associa os dumps RBP e gera regras candidatas de opcode.", 8.8f, FontStyle.Regular, TextSecondary, 24, 43));

            Label safe = new Label();
            safe.Text = "OFFLINE • SOMENTE ANÁLISE";
            safe.Dock = DockStyle.Right;
            safe.Width = 260;
            safe.TextAlign = ContentAlignment.MiddleCenter;
            safe.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            safe.ForeColor = Success;
            header.Controls.Add(safe);

            Panel commands = new Panel();
            commands.Dock = DockStyle.Top;
            commands.Height = 98;
            commands.BackColor = Canvas;
            Controls.Add(commands);

            Button choose = ButtonAt("ASSOCIAR DUMP", 16, 12, 132, true);
            choose.Click += delegate { AssignDumpToSelected(); };
            commands.Controls.Add(choose);

            Button folder = ButtonAt("IMPORTAR PASTA", 160, 12, 125, false);
            folder.Click += delegate { ImportFolder(); };
            commands.Controls.Add(folder);

            Button analyze = ButtonAt("ANALISAR CAMPANHA", 297, 12, 150, false);
            analyze.Click += delegate { AnalyzeCampaign(); };
            commands.Controls.Add(analyze);

            Button save = ButtonAt("SALVAR CAMPANHA", 459, 12, 140, false);
            save.Click += delegate { SaveCampaign(); };
            commands.Controls.Add(save);

            Button load = ButtonAt("ABRIR CAMPANHA", 611, 12, 135, false);
            load.Click += delegate { OpenCampaign(); };
            commands.Controls.Add(load);

            Button rules = ButtonAt("EXPORTAR REGRAS", 758, 12, 140, false);
            rules.Click += delegate { ExportRules(); };
            commands.Controls.Add(rules);

            Button reset = ButtonAt("REINICIAR", 910, 12, 105, false);
            reset.Click += delegate { LoadDefaultCampaign(); RefreshGrid(); reportBox.Clear(); lastReport = string.Empty; lastRules = string.Empty; };
            commands.Controls.Add(reset);

            statusLabel = NewLabel("Selecione um teste e associe o .rbpdump correspondente.", 8.5f, FontStyle.Regular, TextSecondary, 1030, 23);
            commands.Controls.Add(statusLabel);

            Label guide = NewLabel("Nomeie os dumps começando pelo ID do teste (A1_, A2_, B1_...) para usar IMPORTAR PASTA automaticamente.", 8.5f, FontStyle.Regular, Warning, 18, 62);
            commands.Controls.Add(guide);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 430;
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
            grid.Font = new Font("Segoe UI", 8.7f);
            AddColumn("ID", "Id", 48);
            AddColumn("Grupo", "Group", 120);
            AddColumn("Instrução", "Instruction", 100);
            AddColumn("Operando", "Operand", 105);
            AddColumn("Detalhe", "Detail", 210);
            AddColumn("Passo", "Step", 65);
            AddColumn("WORD 1", "Word1", 88);
            AddColumn("WORD 2", "Word2", 88);
            AddColumn("Dump", "Dump", 300);
            AddColumn("Estado", "State", 100);
            split.Panel1.Controls.Add(grid);

            reportBox = new TextBox();
            reportBox.Dock = DockStyle.Fill;
            reportBox.Multiline = true;
            reportBox.ReadOnly = true;
            reportBox.WordWrap = false;
            reportBox.ScrollBars = ScrollBars.Both;
            reportBox.Font = new Font("Consolas", 9.1f);
            reportBox.BackColor = Color.FromArgb(20, 28, 36);
            reportBox.ForeColor = Color.FromArgb(220, 233, 245);
            split.Panel2.Controls.Add(reportBox);

        }

        private void LoadDefaultCampaign()
        {
            items.Clear();
            Add("A1", "Operando", "STR", "X0001", "base", 0);
            Add("A2", "Operando", "STR", "X0002", "muda 1 bit de endereço", 0);
            Add("A3", "Operando", "STR", "X0004", "muda outro bit", 0);
            Add("A4", "Operando", "STR", "X0016", "expande campo de endereço", 0);

            Add("B1", "Opcode", "STR", "X0001", "referência", 0);
            Add("B2", "Opcode", "STR NOT", "X0001", "mesmo operando", 0);
            Add("B3", "Opcode", "AND", "X0001", "mesmo operando", 0);
            Add("B4", "Opcode", "AND NOT", "X0001", "mesmo operando", 0);
            Add("B5", "Opcode", "OR", "X0001", "mesmo operando", 0);
            Add("B6", "Opcode", "OR NOT", "X0001", "mesmo operando", 0);

            Add("C1", "Família", "STR", "X0001", "entrada", 0);
            Add("C2", "Família", "STR", "Y0001", "saída como contato", 0);
            Add("C3", "Família", "STR", "C0001", "relé interno", 0);
            Add("C4", "Família", "STR", "SC001", "relé especial", 0);

            Add("D1", "Saída", "OUT", "Y0001", "STR X0001 / OUT Y0001", 1);
            Add("D2", "Saída", "OUT", "Y0002", "STR X0001 / OUT Y0002", 1);
            Add("D3", "Saída", "OUT", "C0001", "STR X0001 / OUT C0001", 1);

            Add("E1", "2 words", "TMR", "V0001", "preset 10", 0);
            Add("E2", "2 words", "TMR", "V0002", "preset 10", 0);
            Add("E3", "2 words", "TMR", "V0001", "preset 20", 0);
            Add("E4", "2 words", "CNT", "V0001", "preset 10", 0);
        }

        private void Add(string id, string group, string instruction, string operand, string detail, int step)
        {
            TP02CampaignItem x = new TP02CampaignItem();
            x.Id = id;
            x.Group = group;
            x.Instruction = instruction;
            x.Operand = operand;
            x.Detail = detail;
            x.Step = step;
            items.Add(x);
        }

        private void AssignDumpToSelected()
        {
            if (grid.SelectedRows.Count == 0) return;
            int index = grid.SelectedRows[0].Index;
            if (index < 0 || index >= items.Count) return;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Dump RBP (*.rbpdump)|*.rbpdump|Texto (*.txt)|*.txt|Todos (*.*)|*.*";
            dlg.Title = "Associar dump ao teste " + items[index].Id;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            LoadWords(items[index], dlg.FileName);
            RefreshGrid();
        }

        private void ImportFolder()
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Selecione a pasta que contém dumps nomeados A1_..., A2_..., B1_...";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string[] files = Directory.GetFiles(dlg.SelectedPath, "*.*", SearchOption.TopDirectoryOnly);
            int matched = 0;
            int i;
            for (i = 0; i < items.Count; i++)
            {
                string prefix = items[i].Id + "_";
                string exact = items[i].Id + ".";
                int j;
                for (j = 0; j < files.Length; j++)
                {
                    string name = Path.GetFileName(files[j]);
                    if (!(name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || name.StartsWith(exact, StringComparison.OrdinalIgnoreCase))) continue;
                    if (!name.EndsWith(".rbpdump", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;
                    if (LoadWords(items[i], files[j])) matched++;
                    break;
                }
            }
            RefreshGrid();
            statusLabel.Text = matched.ToString() + " teste(s) associados automaticamente.";
        }

        private static bool LoadWords(TP02CampaignItem item, string path)
        {
            string w1 = FindWordAtStep(path, item.Step);
            if (string.IsNullOrEmpty(w1)) return false;
            item.FilePath = path;
            item.Word1 = w1;
            item.Word2 = item.NeedsSecondWord ? FindWordAtStep(path, item.Step + 1) : string.Empty;
            return true;
        }

        private void RefreshGrid()
        {
            grid.Rows.Clear();
            int complete = 0;
            int i;
            for (i = 0; i < items.Count; i++)
            {
                TP02CampaignItem x = items[i];
                bool ok = !string.IsNullOrEmpty(x.Word1) && (!x.NeedsSecondWord || !string.IsNullOrEmpty(x.Word2));
                if (ok) complete++;
                int row = grid.Rows.Add(x.Id, x.Group, x.Instruction, x.Operand, x.Detail, x.Step.ToString("0000"), x.Word1, x.Word2, string.IsNullOrEmpty(x.FilePath) ? string.Empty : Path.GetFileName(x.FilePath), ok ? "OK" : "PENDENTE");
                grid.Rows[row].DefaultCellStyle.ForeColor = ok ? Success : TextSecondary;
            }
            statusLabel.Text = complete.ToString() + "/" + items.Count.ToString() + " testes completos.";
            if (grid.Rows.Count > 0 && grid.SelectedRows.Count == 0) grid.Rows[0].Selected = true;
        }

        private void AnalyzeCampaign()
        {
            List<TP02CampaignItem> complete = new List<TP02CampaignItem>();
            int i;
            for (i = 0; i < items.Count; i++) if (!string.IsNullOrEmpty(items[i].Word1)) complete.Add(items[i]);
            if (complete.Count < 2)
            {
                MessageBox.Show("Associe pelo menos dois dumps antes da análise.", "Campanha TP02", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StringBuilder report = new StringBuilder();
            StringBuilder rules = new StringBuilder();
            report.AppendLine("TP02 — CAMPANHA GUIADA DE CALIBRAÇÃO");
            report.AppendLine(new string('=', 96));
            report.AppendLine("Testes preenchidos: " + complete.Count.ToString() + "/" + items.Count.ToString());
            report.AppendLine();

            rules.AppendLine("# TP02 candidate opcode rules");
            rules.AppendLine("# INSTRUCTION\tOPCODE_MASK\tOPCODE_VALUE\tOPERAND_MASK\tSAMPLES\tSTATUS");

            Dictionary<string, List<TP02CampaignItem>> groups = new Dictionary<string, List<TP02CampaignItem>>(StringComparer.OrdinalIgnoreCase);
            for (i = 0; i < complete.Count; i++)
            {
                string key = complete[i].Instruction;
                if (!groups.ContainsKey(key)) groups[key] = new List<TP02CampaignItem>();
                groups[key].Add(complete[i]);
            }

            report.AppendLine("1) REGRAS CANDIDATAS POR INSTRUÇÃO");
            report.AppendLine(new string('-', 96));
            foreach (KeyValuePair<string, List<TP02CampaignItem>> kv in groups)
            {
                List<TP02CampaignItem> group = kv.Value;
                if (group.Count < 2)
                {
                    report.AppendLine(kv.Key.PadRight(10) + " — ainda sem amostras suficientes para inferir máscara.");
                    continue;
                }

                int first = ParseWord(group[0].Word1);
                int varying = 0;
                for (i = 1; i < group.Count; i++) varying |= first ^ ParseWord(group[i].Word1);
                int opcodeMask = (~varying) & 0xFFFFFF;
                int opcodeValue = first & opcodeMask;
                int operandMask = varying;

                report.AppendLine(kv.Key);
                report.AppendLine("  amostras      : " + Describe(group));
                report.AppendLine("  operand mask  : 0x" + operandMask.ToString("X6") + "  " + ToBinary24(operandMask));
                report.AppendLine("  opcode mask   : 0x" + opcodeMask.ToString("X6") + "  " + ToBinary24(opcodeMask));
                report.AppendLine("  opcode value  : 0x" + opcodeValue.ToString("X6") + "  " + ToBinary24(opcodeValue));
                report.AppendLine("  regra candidata: (WORD & 0x" + opcodeMask.ToString("X6") + ") == 0x" + opcodeValue.ToString("X6"));
                report.AppendLine();

                rules.Append(kv.Key).Append('\t').Append(opcodeMask.ToString("X6")).Append('\t').Append(opcodeValue.ToString("X6"))
                     .Append('\t').Append(operandMask.ToString("X6")).Append('\t').Append(group.Count.ToString()).Append('\t').AppendLine("CANDIDATE");
            }

            report.AppendLine("2) ISOLAMENTO DO OPCODE COM MESMO OPERANDO");
            report.AppendLine(new string('-', 96));
            int comparisons = 0;
            for (i = 0; i < complete.Count; i++)
            {
                int j;
                for (j = i + 1; j < complete.Count; j++)
                {
                    TP02CampaignItem a = complete[i];
                    TP02CampaignItem b = complete[j];
                    if (a.Step != b.Step) continue;
                    if (!string.Equals(a.Operand, b.Operand, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(a.Instruction, b.Instruction, StringComparison.OrdinalIgnoreCase)) continue;
                    int xor = ParseWord(a.Word1) ^ ParseWord(b.Word1);
                    report.Append(a.Id).Append(' ').Append(a.Instruction).Append(" vs ").Append(b.Id).Append(' ').Append(b.Instruction)
                          .Append(" | ").Append(a.Operand).Append(" | XOR=0x").Append(xor.ToString("X6")).Append("  ").AppendLine(ToBinary24(xor));
                    comparisons++;
                }
            }
            if (comparisons == 0) report.AppendLine("Nenhum par comparável ainda disponível.");

            report.AppendLine();
            report.AppendLine("3) BLOCOS DE DOIS WORDS (TMR/CNT)");
            report.AppendLine(new string('-', 96));
            for (i = 0; i < complete.Count; i++)
            {
                TP02CampaignItem x = complete[i];
                if (!x.NeedsSecondWord) continue;
                report.Append(x.Id).Append("  ").Append(x.Instruction).Append(' ').Append(x.Operand).Append("  ")
                      .Append(x.Detail).Append("  WORD1=").Append(x.Word1).Append("  WORD2=").Append(string.IsNullOrEmpty(x.Word2) ? "PENDENTE" : x.Word2).AppendLine();
            }

            report.AppendLine();
            report.AppendLine("4) CRITÉRIO DE PROMOÇÃO");
            report.AppendLine(new string('-', 96));
            report.AppendLine("As regras exportadas permanecem CANDIDATE. Só devem virar CONFIRMED após repetição com vários endereços,");
            report.AppendLine("famílias X/Y/C/SC e comparação cruzada entre instruções. O decodificador não deve tratar CANDIDATE como prova final.");

            lastReport = report.ToString();
            lastRules = rules.ToString();
            reportBox.Text = lastReport;
            statusLabel.Text = "Campanha analisada; regras candidatas prontas para exportação.";
        }

        private void SaveCampaign()
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Campanha TP02 (*.tpcampaign.tsv)|*.tpcampaign.tsv|Texto (*.txt)|*.txt";
            dlg.FileName = "TP02_calibration_campaign.tpcampaign.tsv";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# ID\tGROUP\tINSTRUCTION\tOPERAND\tDETAIL\tSTEP\tFILE\tWORD1\tWORD2");
            int i;
            for (i = 0; i < items.Count; i++)
            {
                TP02CampaignItem x = items[i];
                sb.Append(x.Id).Append('\t').Append(Clean(x.Group)).Append('\t').Append(Clean(x.Instruction)).Append('\t').Append(Clean(x.Operand)).Append('\t')
                  .Append(Clean(x.Detail)).Append('\t').Append(x.Step.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Clean(x.FilePath)).Append('\t')
                  .Append(x.Word1).Append('\t').Append(x.Word2).AppendLine();
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            statusLabel.Text = "Campanha salva: " + dlg.FileName;
        }

        private void OpenCampaign()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Campanha TP02 (*.tpcampaign.tsv)|*.tpcampaign.tsv|Texto (*.txt)|*.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string[] lines = File.ReadAllLines(dlg.FileName, Encoding.UTF8);
            List<TP02CampaignItem> loaded = new List<TP02CampaignItem>();
            int i;
            for (i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].StartsWith("#")) continue;
                string[] p = lines[i].Split('\t');
                if (p.Length < 9) continue;
                int step;
                if (!int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out step)) step = 0;
                TP02CampaignItem x = new TP02CampaignItem();
                x.Id = p[0]; x.Group = p[1]; x.Instruction = p[2]; x.Operand = p[3]; x.Detail = p[4]; x.Step = step;
                x.FilePath = p[6]; x.Word1 = p[7]; x.Word2 = p[8];
                loaded.Add(x);
            }
            if (loaded.Count == 0) return;
            items.Clear();
            items.AddRange(loaded);
            RefreshGrid();
            statusLabel.Text = "Campanha carregada: " + dlg.FileName;
        }

        private void ExportRules()
        {
            if (string.IsNullOrEmpty(lastRules)) AnalyzeCampaign();
            if (string.IsNullOrEmpty(lastRules)) return;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Regras candidatas (*.rules.tsv)|*.rules.tsv|Texto (*.txt)|*.txt";
            dlg.FileName = "tp02_opcode_rules.rules.tsv";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, lastRules, Encoding.UTF8);

            string reportPath = Path.ChangeExtension(dlg.FileName, ".cal.txt");
            if (!string.IsNullOrEmpty(lastReport)) File.WriteAllText(reportPath, lastReport, Encoding.UTF8);
            statusLabel.Text = "Regras e relatório exportados.";
        }

        private static string FindWordAtStep(string path, int step)
        {
            if (!File.Exists(path)) return string.Empty;
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

        private static string Describe(List<TP02CampaignItem> group)
        {
            StringBuilder sb = new StringBuilder();
            int i;
            for (i = 0; i < group.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(group[i].Id).Append(':').Append(group[i].Operand).Append('=').Append(group[i].Word1);
            }
            return sb.ToString();
        }

        private static string Clean(string text)
        {
            return (text ?? string.Empty).Replace('\t', ' ').Replace("\r", " ").Replace("\n", " ");
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
            b.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
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
