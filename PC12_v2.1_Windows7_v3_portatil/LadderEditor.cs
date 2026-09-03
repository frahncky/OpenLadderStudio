using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class LadderProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LadderEditorForm());
        }
    }

    internal enum LadderElementType
    {
        Empty,
        ContactNO,
        ContactNC,
        Coil
    }

    internal enum LadderTool
    {
        Select,
        ContactNO,
        ContactNC,
        Coil,
        Erase
    }

    internal sealed class LadderElement
    {
        public LadderElementType Type;
        public string Address;

        public LadderElement()
        {
            Type = LadderElementType.Empty;
            Address = string.Empty;
        }

        public LadderElement Clone()
        {
            LadderElement e = new LadderElement();
            e.Type = Type;
            e.Address = Address;
            return e;
        }
    }

    internal sealed class LadderRung
    {
        public const int ColumnCount = 8;
        public LadderElement[] Elements;

        public LadderRung()
        {
            Elements = new LadderElement[ColumnCount];
            int i;
            for (i = 0; i < ColumnCount; i++) Elements[i] = new LadderElement();
        }
    }

    internal sealed class LadderEditorForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color NavyLight = Color.FromArgb(27, 55, 86);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color AccentHover = Color.FromArgb(0, 102, 170);
        private readonly Color CanvasColor = Color.FromArgb(246, 248, 251);
        private readonly Color Border = Color.FromArgb(218, 226, 234);
        private readonly Color TextPrimary = Color.FromArgb(35, 47, 60);
        private readonly Color TextSecondary = Color.FromArgb(95, 108, 122);
        private readonly Color Success = Color.FromArgb(27, 132, 86);

        private readonly List<LadderRung> rungs = new List<LadderRung>();
        private readonly Stack<string> undoStack = new Stack<string>();
        private LadderCanvas canvas;
        private Label projectLabel;
        private Label statusLabel;
        private Label toolLabel;
        private LadderTool activeTool = LadderTool.Select;
        private string currentFile = string.Empty;
        private bool dirty;

        public LadderEditorForm()
        {
            Text = "PC12 Ladder Studio";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 650);
            Size = new Size(1180, 760);
            BackColor = CanvasColor;
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            KeyDown += FormKeyDown;
            FormClosing += FormClosingCheck;

            BuildUi();
            NewProject(false);
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 64;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label brand = new Label();
            brand.Text = "PC12 LADDER STUDIO";
            brand.AutoSize = true;
            brand.Font = new Font("Segoe UI Semibold", 13.0f, FontStyle.Bold);
            brand.ForeColor = Navy;
            brand.Location = new Point(22, 14);
            header.Controls.Add(brand);

            Label sub = new Label();
            sub.Text = "Editor Ladder moderno • TP02";
            sub.AutoSize = true;
            sub.ForeColor = TextSecondary;
            sub.Location = new Point(24, 39);
            header.Controls.Add(sub);

            projectLabel = new Label();
            projectLabel.AutoSize = false;
            projectLabel.TextAlign = ContentAlignment.MiddleRight;
            projectLabel.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            projectLabel.ForeColor = TextPrimary;
            projectLabel.Dock = DockStyle.Right;
            projectLabel.Width = 360;
            projectLabel.Padding = new Padding(0, 0, 24, 0);
            header.Controls.Add(projectLabel);

            Panel commandBar = new Panel();
            commandBar.Dock = DockStyle.Top;
            commandBar.Height = 58;
            commandBar.BackColor = Color.FromArgb(237, 242, 247);
            Controls.Add(commandBar);

            int x = 18;
            AddCommandButton(commandBar, "NOVO", x, delegate { NewProject(true); }); x += 82;
            AddCommandButton(commandBar, "ABRIR", x, delegate { OpenProject(); }); x += 82;
            AddCommandButton(commandBar, "SALVAR", x, delegate { SaveProject(false); }); x += 90;
            AddCommandButton(commandBar, "SALVAR COMO", x, delegate { SaveProject(true); }); x += 116;

            Panel divider = new Panel();
            divider.BackColor = Color.FromArgb(204, 214, 224);
            divider.Location = new Point(x + 4, 13);
            divider.Size = new Size(1, 32);
            commandBar.Controls.Add(divider);
            x += 20;

            AddCommandButton(commandBar, "DESFAZER", x, delegate { Undo(); }); x += 100;
            AddCommandButton(commandBar, "+ RUNG", x, delegate { AddRung(); }); x += 88;
            AddCommandButton(commandBar, "- RUNG", x, delegate { DeleteSelectedRung(); });

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 34;
            bottom.BackColor = Color.White;
            bottom.Padding = new Padding(18, 0, 18, 0);
            Controls.Add(bottom);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.ForeColor = TextSecondary;
            statusLabel.Text = "Pronto";
            bottom.Controls.Add(statusLabel);

            toolLabel = new Label();
            toolLabel.Dock = DockStyle.Right;
            toolLabel.Width = 260;
            toolLabel.TextAlign = ContentAlignment.MiddleRight;
            toolLabel.ForeColor = Navy;
            toolLabel.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            bottom.Controls.Add(toolLabel);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = CanvasColor;
            Controls.Add(body);

            Panel toolbox = new Panel();
            toolbox.Dock = DockStyle.Left;
            toolbox.Width = 210;
            toolbox.BackColor = Navy;
            body.Controls.Add(toolbox);

            Label toolsTitle = new Label();
            toolsTitle.Text = "ELEMENTOS LADDER";
            toolsTitle.AutoSize = true;
            toolsTitle.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            toolsTitle.ForeColor = Color.FromArgb(166, 188, 210);
            toolsTitle.Location = new Point(20, 24);
            toolbox.Controls.Add(toolsTitle);

            AddToolButton(toolbox, "↖  Selecionar", 64, LadderTool.Select);
            AddToolButton(toolbox, "—| |—  Contato NA", 112, LadderTool.ContactNO);
            AddToolButton(toolbox, "—|/|—  Contato NF", 160, LadderTool.ContactNC);
            AddToolButton(toolbox, "—( )—  Bobina", 208, LadderTool.Coil);
            AddToolButton(toolbox, "×  Apagar elemento", 256, LadderTool.Erase);

            Label hintTitle = new Label();
            hintTitle.Text = "ATALHOS";
            hintTitle.AutoSize = true;
            hintTitle.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            hintTitle.ForeColor = Color.FromArgb(166, 188, 210);
            hintTitle.Location = new Point(20, 330);
            toolbox.Controls.Add(hintTitle);

            Label hints = new Label();
            hints.Text = "Ctrl+N   Novo\r\nCtrl+O   Abrir\r\nCtrl+S   Salvar\r\nCtrl+Z   Desfazer\r\nDel        Apagar\r\nDuplo clique  Endereço";
            hints.AutoSize = true;
            hints.Font = new Font("Consolas", 8.7f);
            hints.ForeColor = Color.FromArgb(210, 225, 239);
            hints.Location = new Point(20, 356);
            toolbox.Controls.Add(hints);

            Label phase = new Label();
            phase.Text = "ETAPA 1\r\nEditor local. A gravação no PLC\r\nserá habilitada após validar\r\no protocolo do TP02.";
            phase.AutoSize = true;
            phase.Font = new Font("Segoe UI", 8.2f);
            phase.ForeColor = Color.FromArgb(169, 191, 212);
            phase.Location = new Point(20, 470);
            toolbox.Controls.Add(phase);

            Panel editorHost = new Panel();
            editorHost.Dock = DockStyle.Fill;
            editorHost.Padding = new Padding(18);
            editorHost.BackColor = CanvasColor;
            body.Controls.Add(editorHost);

            Panel editorCard = new Panel();
            editorCard.Dock = DockStyle.Fill;
            editorCard.BackColor = Color.White;
            editorCard.Padding = new Padding(1);
            editorHost.Controls.Add(editorCard);

            canvas = new LadderCanvas();
            canvas.Dock = DockStyle.Fill;
            canvas.BackColor = Color.White;
            canvas.Rungs = rungs;
            canvas.SelectionChanged += CanvasSelectionChanged;
            canvas.ElementAction += CanvasElementAction;
            canvas.ElementDoubleClick += CanvasElementDoubleClick;
            editorCard.Controls.Add(canvas);

            SetActiveTool(LadderTool.Select);
        }

        private void AddCommandButton(Control parent, string text, int left, EventHandler action)
        {
            FlatActionButton b = new FlatActionButton();
            b.Text = text;
            b.Location = new Point(left, 11);
            b.Size = new Size(text == "SALVAR COMO" ? 108 : 76, 36);
            b.NormalColor = Color.White;
            b.HoverColor = Color.FromArgb(222, 231, 240);
            b.ForeColor = Navy;
            b.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            b.Click += action;
            parent.Controls.Add(b);
        }

        private void AddToolButton(Control parent, string text, int top, LadderTool tool)
        {
            FlatActionButton b = new FlatActionButton();
            b.Text = text;
            b.Tag = tool;
            b.Location = new Point(12, top);
            b.Size = new Size(186, 40);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(12, 0, 0, 0);
            b.NormalColor = Navy;
            b.HoverColor = NavyLight;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            b.Click += delegate { SetActiveTool(tool); };
            parent.Controls.Add(b);
        }

        private void SetActiveTool(LadderTool tool)
        {
            activeTool = tool;
            string label = "Selecionar";
            if (tool == LadderTool.ContactNO) label = "Contato normalmente aberto";
            if (tool == LadderTool.ContactNC) label = "Contato normalmente fechado";
            if (tool == LadderTool.Coil) label = "Bobina";
            if (tool == LadderTool.Erase) label = "Apagar";
            toolLabel.Text = "Ferramenta: " + label;
            statusLabel.Text = "Clique em uma posição do rung para aplicar a ferramenta.";
        }

        private void CanvasSelectionChanged(object sender, EventArgs e)
        {
            if (canvas.SelectedRung < 0)
            {
                statusLabel.Text = "Nenhum elemento selecionado.";
                return;
            }

            LadderElement el = rungs[canvas.SelectedRung].Elements[canvas.SelectedColumn];
            statusLabel.Text = "Rung " + (canvas.SelectedRung + 1).ToString() + " • Coluna " + (canvas.SelectedColumn + 1).ToString() +
                (el.Type == LadderElementType.Empty ? " • vazio" : " • " + ElementName(el.Type) + " " + el.Address);
        }

        private void CanvasElementAction(object sender, EventArgs e)
        {
            int r = canvas.SelectedRung;
            int c = canvas.SelectedColumn;
            if (r < 0 || c < 0) return;

            if (activeTool == LadderTool.Select) return;

            SaveUndoState();
            LadderElement el = rungs[r].Elements[c];

            if (activeTool == LadderTool.Erase)
            {
                el.Type = LadderElementType.Empty;
                el.Address = string.Empty;
                MarkChanged("Elemento removido.");
                return;
            }

            LadderElementType type = LadderElementType.ContactNO;
            if (activeTool == LadderTool.ContactNC) type = LadderElementType.ContactNC;
            if (activeTool == LadderTool.Coil) type = LadderElementType.Coil;

            if (type == LadderElementType.Coil && c != LadderRung.ColumnCount - 1)
            {
                MessageBox.Show("A bobina deve ser inserida na última coluna do rung.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                undoStack.Pop();
                return;
            }

            if (type != LadderElementType.Coil && c == LadderRung.ColumnCount - 1)
            {
                MessageBox.Show("A última coluna é reservada para a bobina de saída.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                undoStack.Pop();
                return;
            }

            string defaultAddress = type == LadderElementType.Coil ? "Y0" : "X0";
            string address = AddressDialog.Ask("Endereço do elemento", "Informe o endereço (ex.: X0, X1, Y0, M0, T0, C0):", defaultAddress);
            if (address == null)
            {
                undoStack.Pop();
                return;
            }

            address = NormalizeAddress(address);
            if (!IsValidAddress(address))
            {
                MessageBox.Show("Endereço inválido. Use endereços iniciados por X, Y, M, T ou C, seguidos de números.\r\nExemplos: X0, X12, Y3, M10, T0, C2.", "Endereço inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                undoStack.Pop();
                return;
            }

            el.Type = type;
            el.Address = address;
            MarkChanged(ElementName(type) + " " + address + " inserido.");
        }

        private void CanvasElementDoubleClick(object sender, EventArgs e)
        {
            int r = canvas.SelectedRung;
            int c = canvas.SelectedColumn;
            if (r < 0 || c < 0) return;
            LadderElement el = rungs[r].Elements[c];
            if (el.Type == LadderElementType.Empty) return;

            string address = AddressDialog.Ask("Editar endereço", "Informe o novo endereço:", el.Address);
            if (address == null) return;
            address = NormalizeAddress(address);
            if (!IsValidAddress(address))
            {
                MessageBox.Show("Endereço inválido.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveUndoState();
            el.Address = address;
            MarkChanged("Endereço atualizado para " + address + ".");
        }

        private static string NormalizeAddress(string value)
        {
            if (value == null) return string.Empty;
            return value.Trim().ToUpperInvariant().Replace(" ", string.Empty);
        }

        private static bool IsValidAddress(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2) return false;
            char prefix = value[0];
            if (prefix != 'X' && prefix != 'Y' && prefix != 'M' && prefix != 'T' && prefix != 'C') return false;
            int i;
            for (i = 1; i < value.Length; i++) if (!char.IsDigit(value[i])) return false;
            return true;
        }

        private static string ElementName(LadderElementType type)
        {
            if (type == LadderElementType.ContactNO) return "Contato NA";
            if (type == LadderElementType.ContactNC) return "Contato NF";
            if (type == LadderElementType.Coil) return "Bobina";
            return "Vazio";
        }

        private void AddRung()
        {
            SaveUndoState();
            rungs.Add(new LadderRung());
            canvas.SelectedRung = rungs.Count - 1;
            canvas.SelectedColumn = 0;
            MarkChanged("Novo rung adicionado.");
        }

        private void DeleteSelectedRung()
        {
            if (rungs.Count <= 1)
            {
                MessageBox.Show("O projeto precisa manter pelo menos um rung.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int r = canvas.SelectedRung;
            if (r < 0) r = rungs.Count - 1;
            SaveUndoState();
            rungs.RemoveAt(r);
            if (r >= rungs.Count) r = rungs.Count - 1;
            canvas.SelectedRung = r;
            canvas.SelectedColumn = 0;
            MarkChanged("Rung removido.");
        }

        private void DeleteSelectedElement()
        {
            int r = canvas.SelectedRung;
            int c = canvas.SelectedColumn;
            if (r < 0 || c < 0) return;
            LadderElement el = rungs[r].Elements[c];
            if (el.Type == LadderElementType.Empty) return;
            SaveUndoState();
            el.Type = LadderElementType.Empty;
            el.Address = string.Empty;
            MarkChanged("Elemento removido.");
        }

        private void NewProject(bool askSave)
        {
            if (askSave && !ConfirmDiscard()) return;
            rungs.Clear();
            rungs.Add(new LadderRung());
            rungs.Add(new LadderRung());
            currentFile = string.Empty;
            dirty = false;
            undoStack.Clear();
            canvas.SelectedRung = 0;
            canvas.SelectedColumn = 0;
            canvas.Invalidate();
            UpdateProjectLabel();
            statusLabel.Text = "Novo projeto criado.";
        }

        private bool ConfirmDiscard()
        {
            if (!dirty) return true;
            DialogResult result = MessageBox.Show("Existem alterações não salvas. Deseja salvá-las agora?", "PC12 Ladder Studio", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes) return SaveProject(false);
            return true;
        }

        private void OpenProject()
        {
            if (!ConfirmDiscard()) return;

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Projeto Ladder moderno (*.pladder)|*.pladder|Todos os arquivos (*.*)|*.*";
            dlg.Title = "Abrir projeto Ladder";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string data = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                DeserializeProject(data);
                currentFile = dlg.FileName;
                dirty = false;
                undoStack.Clear();
                canvas.SelectedRung = 0;
                canvas.SelectedColumn = 0;
                canvas.Invalidate();
                UpdateProjectLabel();
                statusLabel.Text = "Projeto aberto com sucesso.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir o projeto.\r\n\r\n" + ex.Message, "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool SaveProject(bool saveAs)
        {
            string path = currentFile;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.Filter = "Projeto Ladder moderno (*.pladder)|*.pladder";
                dlg.DefaultExt = "pladder";
                dlg.AddExtension = true;
                dlg.Title = "Salvar projeto Ladder";
                dlg.FileName = string.IsNullOrEmpty(path) ? "Projeto_TP02.pladder" : Path.GetFileName(path);
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                path = dlg.FileName;
            }

            try
            {
                File.WriteAllText(path, SerializeProject(), Encoding.UTF8);
                currentFile = path;
                dirty = false;
                UpdateProjectLabel();
                statusLabel.Text = "Projeto salvo em " + path;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar o projeto.\r\n\r\n" + ex.Message, "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private string SerializeProject()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PC12-LADDER|1");
            int r;
            for (r = 0; r < rungs.Count; r++)
            {
                sb.Append("RUNG");
                int c;
                for (c = 0; c < LadderRung.ColumnCount; c++)
                {
                    LadderElement e = rungs[r].Elements[c];
                    sb.Append('|');
                    if (e.Type == LadderElementType.Empty) sb.Append("EMPTY");
                    else if (e.Type == LadderElementType.ContactNO) sb.Append("NO:").Append(e.Address);
                    else if (e.Type == LadderElementType.ContactNC) sb.Append("NC:").Append(e.Address);
                    else if (e.Type == LadderElementType.Coil) sb.Append("COIL:").Append(e.Address);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private void DeserializeProject(string data)
        {
            string[] lines = data.Replace("\r", string.Empty).Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != "PC12-LADDER|1") throw new InvalidDataException("Formato de projeto não reconhecido.");

            List<LadderRung> loaded = new List<LadderRung>();
            int i;
            for (i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split('|');
                if (parts.Length != LadderRung.ColumnCount + 1 || parts[0] != "RUNG") throw new InvalidDataException("Rung inválido na linha " + (i + 1).ToString() + ".");

                LadderRung rung = new LadderRung();
                int c;
                for (c = 0; c < LadderRung.ColumnCount; c++)
                {
                    string token = parts[c + 1];
                    if (token == "EMPTY") continue;
                    int colon = token.IndexOf(':');
                    if (colon <= 0) throw new InvalidDataException("Elemento inválido na linha " + (i + 1).ToString() + ".");
                    string kind = token.Substring(0, colon);
                    string address = token.Substring(colon + 1);
                    if (!IsValidAddress(address)) throw new InvalidDataException("Endereço inválido: " + address);
                    if (kind == "NO") rung.Elements[c].Type = LadderElementType.ContactNO;
                    else if (kind == "NC") rung.Elements[c].Type = LadderElementType.ContactNC;
                    else if (kind == "COIL") rung.Elements[c].Type = LadderElementType.Coil;
                    else throw new InvalidDataException("Tipo de elemento desconhecido: " + kind);
                    rung.Elements[c].Address = address;
                }
                loaded.Add(rung);
            }

            if (loaded.Count == 0) loaded.Add(new LadderRung());
            rungs.Clear();
            rungs.AddRange(loaded);
        }

        private void SaveUndoState()
        {
            undoStack.Push(SerializeProject());
            while (undoStack.Count > 30)
            {
                string[] states = undoStack.ToArray();
                undoStack.Clear();
                int i;
                for (i = states.Length - 2; i >= 0; i--) undoStack.Push(states[i]);
            }
        }

        private void Undo()
        {
            if (undoStack.Count == 0)
            {
                statusLabel.Text = "Nada para desfazer.";
                return;
            }
            string state = undoStack.Pop();
            DeserializeProject(state);
            if (canvas.SelectedRung >= rungs.Count) canvas.SelectedRung = rungs.Count - 1;
            if (canvas.SelectedRung < 0) canvas.SelectedRung = 0;
            canvas.Invalidate();
            dirty = true;
            UpdateProjectLabel();
            statusLabel.Text = "Última alteração desfeita.";
        }

        private void MarkChanged(string message)
        {
            dirty = true;
            canvas.Invalidate();
            UpdateProjectLabel();
            statusLabel.Text = message;
        }

        private void UpdateProjectLabel()
        {
            string name = string.IsNullOrEmpty(currentFile) ? "Projeto sem nome" : Path.GetFileName(currentFile);
            projectLabel.Text = name + (dirty ? "  •  não salvo" : "") + "     |     " + rungs.Count.ToString() + " rung(s)";
        }

        private void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N) { NewProject(true); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.O) { OpenProject(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.S) { SaveProject(false); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelectedElement(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { SetActiveTool(LadderTool.Select); e.SuppressKeyPress = true; }
        }

        private void FormClosingCheck(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscard()) e.Cancel = true;
        }
    }

    internal sealed class LadderCanvas : Control
    {
        public List<LadderRung> Rungs;
        public int SelectedRung = -1;
        public int SelectedColumn = -1;
        public event EventHandler SelectionChanged;
        public event EventHandler ElementAction;
        public event EventHandler ElementDoubleClick;

        private const int TopMargin = 28;
        private const int RungHeight = 82;
        private const int LeftRail = 44;
        private const int RightMargin = 38;

        public LadderCanvas()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            MouseDown += CanvasMouseDown;
            MouseDoubleClick += CanvasMouseDoubleClick;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Rungs == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int totalHeight = TopMargin + Math.Max(1, Rungs.Count) * RungHeight + 40;
            AutoScrollMinSize = new Size(820, totalHeight);
            Point scroll = AutoScrollPosition;
            g.TranslateTransform(scroll.X, scroll.Y);

            int width = Math.Max(ClientSize.Width - RightMargin, 820);
            int rightRail = width - 28;
            int usable = rightRail - LeftRail;
            int cellWidth = usable / LadderRung.ColumnCount;

            using (Pen railPen = new Pen(Color.FromArgb(47, 64, 80), 3.0f))
            {
                int bottom = TopMargin + Math.Max(1, Rungs.Count) * RungHeight;
                g.DrawLine(railPen, LeftRail, TopMargin - 12, LeftRail, bottom - 10);
                g.DrawLine(railPen, rightRail, TopMargin - 12, rightRail, bottom - 10);
            }

            int r;
            for (r = 0; r < Rungs.Count; r++)
            {
                int y = TopMargin + r * RungHeight + RungHeight / 2;

                using (Pen wirePen = new Pen(Color.FromArgb(52, 66, 80), 2.0f))
                {
                    g.DrawLine(wirePen, LeftRail, y, rightRail, y);
                }

                using (Font rungFont = new Font("Segoe UI Semibold", 8.0f, FontStyle.Bold))
                using (Brush rungBrush = new SolidBrush(Color.FromArgb(112, 126, 140)))
                {
                    g.DrawString((r + 1).ToString("000"), rungFont, rungBrush, 7, y - 9);
                }

                int c;
                for (c = 0; c < LadderRung.ColumnCount; c++)
                {
                    int cellLeft = LeftRail + c * cellWidth;
                    Rectangle cell = new Rectangle(cellLeft + 2, y - 30, cellWidth - 4, 60);

                    if (r == SelectedRung && c == SelectedColumn)
                    {
                        using (Brush sel = new SolidBrush(Color.FromArgb(226, 240, 252))) g.FillRectangle(sel, cell);
                        using (Pen selPen = new Pen(Color.FromArgb(0, 122, 204), 1.0f)) g.DrawRectangle(selPen, cell);
                    }

                    DrawElement(g, Rungs[r].Elements[c], cell, y);
                }
            }

            g.ResetTransform();
        }

        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y)
        {
            if (element.Type == LadderElementType.Empty) return;

            int cx = cell.Left + cell.Width / 2;
            int symbolHalf = 16;
            using (Pen p = new Pen(Color.FromArgb(29, 43, 56), 2.2f))
            {
                if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC)
                {
                    g.DrawLine(p, cx - symbolHalf, y - 13, cx - symbolHalf, y + 13);
                    g.DrawLine(p, cx + symbolHalf, y - 13, cx + symbolHalf, y + 13);
                    if (element.Type == LadderElementType.ContactNC) g.DrawLine(p, cx - 19, y + 15, cx + 19, y - 15);
                }
                else if (element.Type == LadderElementType.Coil)
                {
                    Rectangle leftArc = new Rectangle(cx - 22, y - 16, 22, 32);
                    Rectangle rightArc = new Rectangle(cx, y - 16, 22, 32);
                    g.DrawArc(p, leftArc, -90, 180);
                    g.DrawArc(p, rightArc, 90, 180);
                }
            }

            using (Font f = new Font("Consolas", 9.0f, FontStyle.Bold))
            using (Brush b = new SolidBrush(Color.FromArgb(0, 102, 170)))
            {
                SizeF size = g.MeasureString(element.Address, f);
                g.DrawString(element.Address, f, b, cx - size.Width / 2, y - 31);
            }
        }

        private void CanvasMouseDown(object sender, MouseEventArgs e)
        {
            SelectFromPoint(e.Location);
            if (ElementAction != null) ElementAction(this, EventArgs.Empty);
        }

        private void CanvasMouseDoubleClick(object sender, MouseEventArgs e)
        {
            SelectFromPoint(e.Location);
            if (ElementDoubleClick != null) ElementDoubleClick(this, EventArgs.Empty);
        }

        private void SelectFromPoint(Point point)
        {
            if (Rungs == null || Rungs.Count == 0) return;
            Point scroll = AutoScrollPosition;
            int px = point.X - scroll.X;
            int py = point.Y - scroll.Y;

            int width = Math.Max(ClientSize.Width - RightMargin, 820);
            int rightRail = width - 28;
            int usable = rightRail - LeftRail;
            int cellWidth = usable / LadderRung.ColumnCount;

            int rung = (py - TopMargin) / RungHeight;
            int col = (px - LeftRail) / cellWidth;
            if (rung < 0 || rung >= Rungs.Count || col < 0 || col >= LadderRung.ColumnCount) return;

            SelectedRung = rung;
            SelectedColumn = col;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }
    }

    internal sealed class FlatActionButton : Button
    {
        public Color NormalColor = Color.White;
        public Color HoverColor = Color.Gainsboro;

        public FlatActionButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            MouseEnter += delegate { BackColor = HoverColor; };
            MouseLeave += delegate { BackColor = NormalColor; };
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            BackColor = NormalColor;
        }
    }

    internal sealed class AddressDialog : Form
    {
        private TextBox input;
        private string result;

        private AddressDialog(string title, string prompt, string defaultValue)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 158);
            Font = new Font("Segoe UI", 9.0f);
            BackColor = Color.White;

            Label label = new Label();
            label.Text = prompt;
            label.AutoSize = false;
            label.Size = new Size(390, 42);
            label.Location = new Point(20, 18);
            label.ForeColor = Color.FromArgb(45, 58, 72);
            Controls.Add(label);

            input = new TextBox();
            input.Text = defaultValue;
            input.Font = new Font("Consolas", 11.0f, FontStyle.Bold);
            input.Location = new Point(20, 64);
            input.Size = new Size(390, 26);
            input.CharacterCasing = CharacterCasing.Upper;
            Controls.Add(input);

            Button ok = new Button();
            ok.Text = "OK";
            ok.Location = new Point(244, 110);
            ok.Size = new Size(78, 30);
            ok.Click += delegate { result = input.Text; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancelar";
            cancel.Location = new Point(332, 110);
            cancel.Size = new Size(78, 30);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate { input.SelectAll(); input.Focus(); };
        }

        public static string Ask(string title, string prompt, string defaultValue)
        {
            using (AddressDialog dlg = new AddressDialog(title, prompt, defaultValue))
            {
                return dlg.ShowDialog() == DialogResult.OK ? dlg.result : null;
            }
        }
    }
}
