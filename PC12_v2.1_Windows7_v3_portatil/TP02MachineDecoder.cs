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
    internal static class TP02DecoderProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TP02MachineDecoderForm());
        }
    }

    internal sealed class TP02MachineStep
    {
        public int Step;
        public string Word = string.Empty;
        public string High { get { return Word.Length >= 2 ? Word.Substring(0, 2) : string.Empty; } }
        public string Low { get { return Word.Length >= 4 ? Word.Substring(2, 2) : string.Empty; } }
        public string External { get { return Word.Length >= 6 ? Word.Substring(4, 2) : string.Empty; } }
    }

    internal sealed class TP02OpcodeEntry
    {
        public string Word = string.Empty;
        public string Instruction = "UNKNOWN";
        public string Operand = string.Empty;
        public string Evidence = "Não confirmado";
        public string Notes = string.Empty;

        public string ToIl()
        {
            string op = string.IsNullOrEmpty(Operand) ? string.Empty : " " + Operand;
            return Instruction + op;
        }
    }

    internal sealed class TP02MachineDecoderForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);

        private readonly List<TP02MachineStep> steps = new List<TP02MachineStep>();
        private readonly Dictionary<string, TP02OpcodeEntry> map = new Dictionary<string, TP02OpcodeEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly string mapPath;
        private DataGridView grid;
        private TextBox selectedWordBox;
        private ComboBox instructionCombo;
        private TextBox operandBox;
        private ComboBox evidenceCombo;
        private TextBox notesBox;
        private TextBox compareBox;
        private Label statusLabel;
        private string currentDumpPath = string.Empty;

        public TP02MachineDecoderForm()
        {
            mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tp02_opcode_map.tsv");
            Text = "TP02 Machine Decoder - RBP para IL";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 700);
            Size = new Size(1380, 850);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildUi();
            LoadBuiltInObservations();
            LoadMap(false);
            RefreshGrid();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 72;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label title = NewLabel("DECODIFICADOR TP02 — RBP → BOOLEAN / IL", 15.0f, FontStyle.Bold, Navy, 22, 12);
            header.Controls.Add(title);
            Label sub = NewLabel("Laboratório de calibração de palavras de máquina. O sistema só decodifica opcodes comprovados.", 8.8f, FontStyle.Regular, TextSecondary, 24, 43);
            header.Controls.Add(sub);

            Label safe = new Label();
            safe.Text = "OFFLINE • NÃO ENVIA COMANDOS AO PLC";
            safe.Dock = DockStyle.Right;
            safe.Width = 330;
            safe.TextAlign = ContentAlignment.MiddleCenter;
            safe.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            safe.ForeColor = Success;
            header.Controls.Add(safe);

            Panel commands = new Panel();
            commands.Dock = DockStyle.Top;
            commands.Height = 62;
            commands.BackColor = Canvas;
            Controls.Add(commands);

            Button open = ButtonAt("ABRIR .RBPDUMP", 16, 13, 140, true);
            open.Click += delegate { OpenDump(); };
            commands.Controls.Add(open);

            Button compare = ButtonAt("COMPARAR DUMPS", 168, 13, 145, false);
            compare.Click += delegate { CompareDumps(); };
            commands.Controls.Add(compare);

            Button loadMap = ButtonAt("CARREGAR MAPA", 325, 13, 130, false);
            loadMap.Click += delegate { LoadMap(true); };
            commands.Controls.Add(loadMap);

            Button saveMap = ButtonAt("SALVAR MAPA", 467, 13, 120, false);
            saveMap.Click += delegate { SaveMap(); };
            commands.Controls.Add(saveMap);

            Button export = ButtonAt("EXPORTAR IL", 599, 13, 120, false);
            export.Click += delegate { ExportIl(); };
            commands.Controls.Add(export);

            Button sample = ButtonAt("AMOSTRA DO MANUAL", 731, 13, 155, false);
            sample.Click += delegate { LoadManualSample(); };
            commands.Controls.Add(sample);

            statusLabel = NewLabel("Pronto. Carregue um dump RBP ou use a amostra documentada no manual.", 8.6f, FontStyle.Regular, TextSecondary, 905, 23);
            commands.Controls.Add(statusLabel);

            SplitContainer horizontal = new SplitContainer();
            horizontal.Dock = DockStyle.Fill;
            horizontal.Orientation = Orientation.Horizontal;
            horizontal.SplitterDistance = 500;
            horizontal.BackColor = Canvas;
            Controls.Add(horizontal);
            DockOrder.Apply(this, horizontal, commands, header);

            SplitContainer main = new SplitContainer();
            main.Dock = DockStyle.Fill;
            main.SplitterDistance = 850;
            main.BackColor = Canvas;
            horizontal.Panel1.Controls.Add(main);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.AutoGenerateColumns = false;
            grid.Font = new Font("Consolas", 9.0f);
            grid.SelectionChanged += delegate { LoadSelectedStep(); };
            AddGridColumn("Passo", "Step", 70);
            AddGridColumn("WORD", "Word", 90);
            AddGridColumn("HIGH", "High", 65);
            AddGridColumn("LOW", "Low", 65);
            AddGridColumn("EXT", "Ext", 60);
            AddGridColumn("Decodificação", "Decoded", 250);
            AddGridColumn("Evidência", "Evidence", 170);
            main.Panel1.Controls.Add(grid);

            Panel editor = new Panel();
            editor.Dock = DockStyle.Fill;
            editor.BackColor = Color.White;
            editor.Padding = new Padding(18);
            main.Panel2.Controls.Add(editor);

            editor.Controls.Add(NewLabel("Calibração do word selecionado", 12.5f, FontStyle.Bold, TextPrimary, 18, 16));
            editor.Controls.Add(NewLabel("WORD", 8.0f, FontStyle.Bold, TextSecondary, 18, 55));
            selectedWordBox = new TextBox();
            selectedWordBox.ReadOnly = true;
            selectedWordBox.Font = new Font("Consolas", 12.0f, FontStyle.Bold);
            selectedWordBox.Location = new Point(18, 75);
            selectedWordBox.Size = new Size(160, 27);
            editor.Controls.Add(selectedWordBox);

            editor.Controls.Add(NewLabel("Instrução Boolean / IL", 8.0f, FontStyle.Bold, TextSecondary, 18, 116));
            instructionCombo = new ComboBox();
            instructionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            instructionCombo.Location = new Point(18, 137);
            instructionCombo.Size = new Size(230, 25);
            instructionCombo.Items.AddRange(new object[] { "UNKNOWN", "STR", "STR NOT", "AND", "AND NOT", "OR", "OR NOT", "AND STR", "OR STR", "OUT", "TMR", "CNT", "FUN", "END" });
            instructionCombo.SelectedItem = "UNKNOWN";
            editor.Controls.Add(instructionCombo);

            editor.Controls.Add(NewLabel("Operando", 8.0f, FontStyle.Bold, TextSecondary, 18, 176));
            operandBox = new TextBox();
            operandBox.CharacterCasing = CharacterCasing.Upper;
            operandBox.Font = new Font("Consolas", 10.0f, FontStyle.Bold);
            operandBox.Location = new Point(18, 197);
            operandBox.Size = new Size(230, 25);
            editor.Controls.Add(operandBox);

            editor.Controls.Add(NewLabel("Evidência", 8.0f, FontStyle.Bold, TextSecondary, 18, 236));
            evidenceCombo = new ComboBox();
            evidenceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            evidenceCombo.Location = new Point(18, 257);
            evidenceCombo.Size = new Size(230, 25);
            evidenceCombo.Items.AddRange(new object[] { "Não confirmado", "Manual", "Teste controlado", "Inferido por comparação" });
            evidenceCombo.SelectedItem = "Não confirmado";
            editor.Controls.Add(evidenceCombo);

            editor.Controls.Add(NewLabel("Notas", 8.0f, FontStyle.Bold, TextSecondary, 18, 296));
            notesBox = new TextBox();
            notesBox.Multiline = true;
            notesBox.ScrollBars = ScrollBars.Vertical;
            notesBox.Location = new Point(18, 317);
            notesBox.Size = new Size(285, 80);
            editor.Controls.Add(notesBox);

            Button learn = ButtonAt("APRENDER WORD", 18, 415, 140, true);
            learn.Click += delegate { LearnSelectedWord(); };
            editor.Controls.Add(learn);

            Button remove = ButtonAt("REMOVER MAPA", 170, 415, 133, false);
            remove.Click += delegate { RemoveSelectedMapping(); };
            editor.Controls.Add(remove);

            Label warning = NewLabel("Importante: um mapeamento exato vale apenas para o WORD completo. Para descobrir campos de endereço, use COMPARAR DUMPS com programas que diferem em um único operando.", 8.2f, FontStyle.Regular, Warning, 18, 461);
            warning.MaximumSize = new Size(290, 0);
            editor.Controls.Add(warning);

            Panel comparePanel = new Panel();
            comparePanel.Dock = DockStyle.Fill;
            comparePanel.Padding = new Padding(12);
            comparePanel.BackColor = Color.FromArgb(20, 28, 36);
            horizontal.Panel2.Controls.Add(comparePanel);

            compareBox = new TextBox();
            compareBox.Dock = DockStyle.Fill;
            compareBox.Multiline = true;
            compareBox.ReadOnly = true;
            compareBox.WordWrap = false;
            compareBox.ScrollBars = ScrollBars.Both;
            compareBox.Font = new Font("Consolas", 9.0f);
            compareBox.BackColor = Color.FromArgb(20, 28, 36);
            compareBox.ForeColor = Color.FromArgb(220, 233, 245);
            comparePanel.Controls.Add(compareBox);

        }

        private void AddGridColumn(string header, string name, int width)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.Name = name;
            c.Width = width;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(c);
        }

        private void OpenDump()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Dump RBP (*.rbpdump)|*.rbpdump|Texto (*.txt)|*.txt|Todos (*.*)|*.*";
            dlg.Title = "Abrir leitura RBP";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            List<TP02MachineStep> loaded = ParseDump(dlg.FileName);
            if (loaded.Count == 0)
            {
                MessageBox.Show("Nenhuma linha de passo/WORD foi reconhecida no arquivo.", "TP02 Decoder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            steps.Clear();
            steps.AddRange(loaded);
            currentDumpPath = dlg.FileName;
            compareBox.Text = BuildStatistics(steps, Path.GetFileName(dlg.FileName));
            RefreshGrid();
            statusLabel.Text = steps.Count.ToString() + " passo(s) carregados.";
        }

        private void LoadManualSample()
        {
            steps.Clear();
            steps.Add(NewStep(0, "5E1509"));
            steps.Add(NewStep(1, "204006"));
            steps.Add(NewStep(2, "20C10F"));
            currentDumpPath = string.Empty;
            compareBox.Text = "AMOSTRA DOCUMENTADA NO MANUAL TP02\r\n" + new string('=', 72) + "\r\n" +
                "RBP, leitura dos passos 0000–0002:\r\n" +
                "0000  5E1509\r\n0001  204006\r\n0002  20C10F\r\n\r\n" +
                "O manual comprova os três WORDs e sua ordem, mas não publica a associação individual de cada WORD com STR/OUT/END.\r\n" +
                "Por isso eles permanecem semanticamente UNKNOWN até calibração controlada.";
            RefreshGrid();
            statusLabel.Text = "Amostra oficial de três passos carregada.";
        }

        private static TP02MachineStep NewStep(int step, string word)
        {
            TP02MachineStep s = new TP02MachineStep();
            s.Step = step;
            s.Word = word.ToUpperInvariant();
            return s;
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
                result.Add(NewStep(step, m.Groups[2].Value));
            }
            return result;
        }

        private void RefreshGrid()
        {
            grid.Rows.Clear();
            int i;
            for (i = 0; i < steps.Count; i++)
            {
                TP02MachineStep s = steps[i];
                TP02OpcodeEntry entry;
                bool known = map.TryGetValue(s.Word, out entry) && entry.Instruction != "UNKNOWN";
                string decoded = known ? entry.ToIl() : (IsManualObservation(s.Word) ? "UNKNOWN (amostra manual)" : "UNKNOWN");
                string evidence = map.ContainsKey(s.Word) ? map[s.Word].Evidence : (IsManualObservation(s.Word) ? "Manual: WORD observado" : string.Empty);
                int row = grid.Rows.Add(s.Step.ToString("0000"), s.Word, s.High, s.Low, s.External, decoded, evidence);
                if (known) grid.Rows[row].DefaultCellStyle.ForeColor = Success;
                else if (IsManualObservation(s.Word)) grid.Rows[row].DefaultCellStyle.ForeColor = Warning;
            }
            if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
        }

        private void LoadSelectedStep()
        {
            if (grid.SelectedRows.Count == 0) return;
            string word = Convert.ToString(grid.SelectedRows[0].Cells["Word"].Value);
            selectedWordBox.Text = word;
            TP02OpcodeEntry entry;
            if (map.TryGetValue(word, out entry))
            {
                instructionCombo.SelectedItem = entry.Instruction;
                if (instructionCombo.SelectedIndex < 0) instructionCombo.SelectedItem = "UNKNOWN";
                operandBox.Text = entry.Operand;
                evidenceCombo.SelectedItem = entry.Evidence;
                if (evidenceCombo.SelectedIndex < 0) evidenceCombo.SelectedItem = "Não confirmado";
                notesBox.Text = entry.Notes;
            }
            else
            {
                instructionCombo.SelectedItem = "UNKNOWN";
                operandBox.Text = string.Empty;
                evidenceCombo.SelectedItem = IsManualObservation(word) ? "Manual" : "Não confirmado";
                notesBox.Text = IsManualObservation(word) ? "WORD presente no exemplo RBP do manual. Sem associação semântica individual publicada." : string.Empty;
            }
        }

        private void LearnSelectedWord()
        {
            string word = (selectedWordBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (!Regex.IsMatch(word, "^[0-9A-F]{6}$")) return;
            TP02OpcodeEntry e = new TP02OpcodeEntry();
            e.Word = word;
            e.Instruction = instructionCombo.SelectedItem == null ? "UNKNOWN" : instructionCombo.SelectedItem.ToString();
            e.Operand = (operandBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            e.Evidence = evidenceCombo.SelectedItem == null ? "Não confirmado" : evidenceCombo.SelectedItem.ToString();
            e.Notes = (notesBox.Text ?? string.Empty).Replace('\t', ' ').Replace("\r", " ").Replace("\n", " ");
            map[word] = e;
            SaveMap();
            RefreshGrid();
            statusLabel.Text = "Mapeamento salvo para " + word + ".";
        }

        private void RemoveSelectedMapping()
        {
            string word = (selectedWordBox.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (map.ContainsKey(word))
            {
                map.Remove(word);
                SaveMap();
                RefreshGrid();
                statusLabel.Text = "Mapeamento removido para " + word + ".";
            }
        }

        private void LoadBuiltInObservations()
        {
            // Os WORDs abaixo aparecem no exemplo RBP do manual TP02. Eles são observações,
            // não mapeamentos de instruções. Não atribuir STR/OUT/END sem experimento controlado.
        }

        private static bool IsManualObservation(string word)
        {
            return string.Equals(word, "5E1509", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(word, "204006", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(word, "20C10F", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadMap(bool showMessage)
        {
            if (!File.Exists(mapPath))
            {
                if (showMessage) MessageBox.Show("Ainda não existe mapa local. Use APRENDER WORD para criar o primeiro mapeamento.", "TP02 Decoder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                string[] lines = File.ReadAllLines(mapPath, Encoding.UTF8);
                int i;
                for (i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].StartsWith("#")) continue;
                    string[] p = lines[i].Split('\t');
                    if (p.Length < 4 || !Regex.IsMatch(p[0], "^[0-9A-Fa-f]{6}$")) continue;
                    TP02OpcodeEntry e = new TP02OpcodeEntry();
                    e.Word = p[0].ToUpperInvariant();
                    e.Instruction = p[1];
                    e.Operand = p[2];
                    e.Evidence = p[3];
                    e.Notes = p.Length > 4 ? p[4] : string.Empty;
                    map[e.Word] = e;
                }
                RefreshGrid();
                if (showMessage) statusLabel.Text = map.Count.ToString() + " mapeamento(s) carregados.";
            }
            catch (Exception ex)
            {
                if (showMessage) MessageBox.Show(ex.Message, "Erro ao carregar mapa", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveMap()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# TP02 opcode map - WORD\tINSTRUCTION\tOPERAND\tEVIDENCE\tNOTES");
                foreach (KeyValuePair<string, TP02OpcodeEntry> kv in map)
                {
                    TP02OpcodeEntry e = kv.Value;
                    sb.Append(e.Word).Append('\t').Append(e.Instruction).Append('\t').Append(e.Operand.Replace("\t", " ")).Append('\t')
                      .Append(e.Evidence.Replace("\t", " ")).Append('\t').Append(e.Notes.Replace("\t", " ")).AppendLine();
                }
                File.WriteAllText(mapPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erro ao salvar mapa", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CompareDumps()
        {
            OpenFileDialog a = new OpenFileDialog();
            a.Filter = "Dump RBP (*.rbpdump)|*.rbpdump|Texto (*.txt)|*.txt|Todos (*.*)|*.*";
            a.Title = "Dump A — referência";
            if (a.ShowDialog(this) != DialogResult.OK) return;
            OpenFileDialog b = new OpenFileDialog();
            b.Filter = a.Filter;
            b.Title = "Dump B — alteração controlada";
            if (b.ShowDialog(this) != DialogResult.OK) return;

            List<TP02MachineStep> left = ParseDump(a.FileName);
            List<TP02MachineStep> right = ParseDump(b.FileName);
            Dictionary<int, TP02MachineStep> lm = IndexByStep(left);
            Dictionary<int, TP02MachineStep> rm = IndexByStep(right);
            SortedSet<int> all = new SortedSet<int>();
            foreach (int k in lm.Keys) all.Add(k);
            foreach (int k in rm.Keys) all.Add(k);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("COMPARAÇÃO CONTROLADA DE DUMPS RBP");
            sb.AppendLine(new string('=', 84));
            sb.AppendLine("A: " + a.FileName);
            sb.AppendLine("B: " + b.FileName);
            sb.AppendLine();
            sb.AppendLine("PASSO  WORD-A  WORD-B  XOR     ALTERAÇÃO POR BYTE");
            sb.AppendLine("-----  ------  ------  ------  ---------------------------------");
            int changes = 0;
            foreach (int step in all)
            {
                TP02MachineStep la;
                TP02MachineStep rb;
                string wa = lm.TryGetValue(step, out la) ? la.Word : "------";
                string wb = rm.TryGetValue(step, out rb) ? rb.Word : "------";
                if (wa == wb) continue;
                changes++;
                string xor = wa != "------" && wb != "------" ? XorWords(wa, wb) : "------";
                string details = wa != "------" && wb != "------" ? ByteDiff(wa, wb) : "passo adicionado/removido";
                sb.Append(step.ToString("0000")).Append("   ").Append(wa).Append("  ").Append(wb).Append("  ").Append(xor).Append("  ").AppendLine(details);
            }
            sb.AppendLine();
            sb.AppendLine("Passos alterados: " + changes.ToString());
            sb.AppendLine();
            sb.AppendLine("COMO USAR: mantenha todo o programa idêntico e altere apenas UMA coisa por teste, por exemplo STR X0001 → STR X0002. O XOR mostra quais bits carregam o operando. Em outro par, troque STR → AND mantendo X0001 para isolar os bits do opcode.");
            compareBox.Text = sb.ToString();
            statusLabel.Text = changes.ToString() + " passo(s) diferentes entre os dumps.";
        }

        private static Dictionary<int, TP02MachineStep> IndexByStep(List<TP02MachineStep> list)
        {
            Dictionary<int, TP02MachineStep> d = new Dictionary<int, TP02MachineStep>();
            int i;
            for (i = 0; i < list.Count; i++) d[list[i].Step] = list[i];
            return d;
        }

        private static string XorWords(string a, string b)
        {
            int av;
            int bv;
            if (!int.TryParse(a, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out av)) return "??????";
            if (!int.TryParse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bv)) return "??????";
            return (av ^ bv).ToString("X6", CultureInfo.InvariantCulture);
        }

        private static string ByteDiff(string a, string b)
        {
            StringBuilder sb = new StringBuilder();
            string[] names = new string[] { "HIGH", "LOW", "EXT" };
            int i;
            for (i = 0; i < 3; i++)
            {
                string aa = a.Substring(i * 2, 2);
                string bb = b.Substring(i * 2, 2);
                if (aa == bb) continue;
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(names[i]).Append(' ').Append(aa).Append("→").Append(bb);
            }
            return sb.Length == 0 ? "sem alteração de byte" : sb.ToString();
        }

        private string BuildStatistics(List<TP02MachineStep> list, string name)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int i;
            for (i = 0; i < list.Count; i++)
            {
                if (!counts.ContainsKey(list[i].Word)) counts[list[i].Word] = 0;
                counts[list[i].Word]++;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DUMP RBP CARREGADO: " + name);
            sb.AppendLine(new string('=', 72));
            sb.AppendLine("Passos: " + list.Count.ToString());
            sb.AppendLine("WORDs distintos: " + counts.Count.ToString());
            sb.AppendLine();
            sb.AppendLine("FREQUÊNCIA DOS WORDs");
            foreach (KeyValuePair<string, int> kv in counts)
            {
                TP02OpcodeEntry e;
                string decoded = map.TryGetValue(kv.Key, out e) && e.Instruction != "UNKNOWN" ? e.ToIl() : "UNKNOWN";
                sb.Append(kv.Key).Append("  x").Append(kv.Value.ToString().PadLeft(3)).Append("  ").AppendLine(decoded);
            }
            sb.AppendLine();
            sb.AppendLine("Use COMPARAR DUMPS para descobrir quais bits mudam quando apenas uma instrução ou endereço é alterado no programa de referência.");
            return sb.ToString();
        }

        private void ExportIl()
        {
            if (steps.Count == 0)
            {
                MessageBox.Show("Carregue primeiro um dump RBP.", "TP02 Decoder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Lista de instruções (*.il.txt)|*.il.txt|Texto (*.txt)|*.txt";
            dlg.FileName = string.IsNullOrEmpty(currentDumpPath) ? "TP02_decodificado.il.txt" : Path.GetFileNameWithoutExtension(currentDumpPath) + ".il.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; PC12 Studio TP02 - decodificação RBP para IL");
            sb.AppendLine("; UNKNOWN significa que o WORD ainda não foi comprovado por calibração.");
            int i;
            for (i = 0; i < steps.Count; i++)
            {
                TP02MachineStep s = steps[i];
                TP02OpcodeEntry e;
                if (map.TryGetValue(s.Word, out e) && e.Instruction != "UNKNOWN")
                    sb.Append(s.Step.ToString("0000")).Append(": ").Append(e.ToIl()).Append("    ; ").Append(s.Word).Append(" [").Append(e.Evidence).AppendLine("]");
                else
                    sb.Append(s.Step.ToString("0000")).Append(": UNKNOWN ").AppendLine(s.Word);
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            statusLabel.Text = "IL exportada: " + dlg.FileName;
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
