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
            StudioDiagnostics.Install();
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
        Coil,
        Timer,
        Counter,
        Set,
        Reset,
        EdgeUp,
        EdgeDown,
        Function,
        End
    }

    internal enum LadderTool
    {
        Select,
        ContactNO,
        ContactNC,
        ParallelNO,
        ParallelNC,
        Coil,
        Timer,
        Counter,
        Set,
        Reset,
        EdgeUp,
        EdgeDown,
        Function,
        End,
        Erase
    }

    internal sealed class LadderElement
    {
        public LadderElementType Type;
        public string Address;
        public string Parameter;
        public string Mode;

        public LadderElement()
        {
            Type = LadderElementType.Empty;
            Address = string.Empty;
            Parameter = string.Empty;
            Mode = string.Empty;
        }

        public void Clear()
        {
            Type = LadderElementType.Empty;
            Address = string.Empty;
            Parameter = string.Empty;
            Mode = string.Empty;
        }
    }

    internal sealed class LadderRung
    {
        public const int ColumnCount = 8;
        public LadderElement[] Elements;
        public LadderElement[] Parallel;

        public LadderRung()
        {
            Elements = new LadderElement[ColumnCount];
            Parallel = new LadderElement[ColumnCount];
            int i;
            for (i = 0; i < ColumnCount; i++)
            {
                Elements[i] = new LadderElement();
                Parallel[i] = new LadderElement();
            }
        }
    }

    internal sealed class LadderEditorForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color NavyLight = Color.FromArgb(27, 55, 86);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color CanvasColor = Color.FromArgb(246, 248, 251);
        private readonly Color TextPrimary = Color.FromArgb(35, 47, 60);
        private readonly Color TextSecondary = Color.FromArgb(95, 108, 122);

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
            MinimumSize = new Size(1040, 690);
            Size = new Size(1260, 800);
            BackColor = CanvasColor;
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleDimensions = new SizeF(96F, 96F);
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

            Label brand = new Label();
            brand.Text = "PC12 LADDER STUDIO";
            brand.AutoSize = true;
            brand.Font = new Font("Segoe UI Semibold", 13.0f, FontStyle.Bold);
            brand.ForeColor = Navy;
            brand.Location = new Point(22, 14);
            header.Controls.Add(brand);

            Label sub = new Label();
            sub.Text = "Editor Ladder moderno • WEG TP02";
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
            projectLabel.Width = 430;
            projectLabel.Padding = new Padding(0, 0, 24, 0);
            header.Controls.Add(projectLabel);

            Panel commandBar = new Panel();
            commandBar.Dock = DockStyle.Top;
            commandBar.Height = 58;
            commandBar.BackColor = Color.FromArgb(237, 242, 247);

            int x = 16;
            AddCommandButton(commandBar, "NOVO", x, 76, delegate { NewProject(true); }); x += 82;
            AddCommandButton(commandBar, "ABRIR", x, 76, delegate { OpenProject(); }); x += 82;
            AddCommandButton(commandBar, "SALVAR", x, 82, delegate { SaveProject(false); }); x += 88;
            AddCommandButton(commandBar, "SALVAR COMO", x, 108, delegate { SaveProject(true); }); x += 116;
            AddCommandButton(commandBar, "DESFAZER", x, 92, delegate { Undo(); }); x += 98;
            AddCommandButton(commandBar, "+ RUNG", x, 78, delegate { AddRung(); }); x += 84;
            AddCommandButton(commandBar, "- RUNG", x, 78, delegate { DeleteSelectedRung(); }); x += 88;
            AddCommandButton(commandBar, "VALIDAR", x, 92, delegate { ValidateProject(true); });

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 34;
            bottom.BackColor = Color.White;
            bottom.Padding = new Padding(18, 0, 18, 0);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.ForeColor = TextSecondary;
            statusLabel.Text = "Pronto";
            bottom.Controls.Add(statusLabel);

            toolLabel = new Label();
            toolLabel.Dock = DockStyle.Right;
            toolLabel.Width = 310;
            toolLabel.TextAlign = ContentAlignment.MiddleRight;
            toolLabel.ForeColor = Navy;
            toolLabel.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            bottom.Controls.Add(toolLabel);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = CanvasColor;
            // A ancoragem e resolvida do ultimo filho para o primeiro: quem entra por
            // ultimo escolhe seu espaco antes e fica na borda externa. O painel Fill
            // precisa entrar primeiro, senao ele ocupa toda a area e as barras passam
            // a se sobrepor ao conteudo.
            Controls.Add(body);
            body.BringToFront();
            Controls.Add(bottom);
            Controls.Add(commandBar);
            Controls.Add(header);

            Panel toolbox = new Panel();
            toolbox.Dock = DockStyle.Left;
            toolbox.Width = 238;
            toolbox.BackColor = Navy;
            toolbox.AutoScroll = true;

            Label toolsTitle = new Label();
            toolsTitle.Text = "ELEMENTOS TP02";
            toolsTitle.AutoSize = true;
            toolsTitle.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            toolsTitle.ForeColor = Color.FromArgb(166, 188, 210);
            toolsTitle.Location = new Point(18, 18);
            toolbox.Controls.Add(toolsTitle);

            int t = 50;
            AddToolButton(toolbox, "↖  Selecionar", t, LadderTool.Select); t += 38;
            AddToolButton(toolbox, "—| |—  Contato NA", t, LadderTool.ContactNO); t += 38;
            AddToolButton(toolbox, "—|/|—  Contato NF", t, LadderTool.ContactNC); t += 38;
            AddToolButton(toolbox, "↳ | |  Paralelo NA", t, LadderTool.ParallelNO); t += 38;
            AddToolButton(toolbox, "↳ |/|  Paralelo NF", t, LadderTool.ParallelNC); t += 38;
            AddToolButton(toolbox, "—( )—  OUT / Bobina", t, LadderTool.Coil); t += 38;
            AddToolButton(toolbox, "TMR  Temporizador", t, LadderTool.Timer); t += 38;
            AddToolButton(toolbox, "CNT  Contador", t, LadderTool.Counter); t += 38;
            AddToolButton(toolbox, "F-23  SET", t, LadderTool.Set); t += 38;
            AddToolButton(toolbox, "F-24  RESET", t, LadderTool.Reset); t += 38;
            AddToolButton(toolbox, "F-05  Borda ↑", t, LadderTool.EdgeUp); t += 38;
            AddToolButton(toolbox, "F-06  Borda ↓", t, LadderTool.EdgeDown); t += 38;
            AddToolButton(toolbox, "FUN  Função especial", t, LadderTool.Function); t += 38;
            AddToolButton(toolbox, "F-00  END", t, LadderTool.End); t += 38;
            AddToolButton(toolbox, "×  Apagar", t, LadderTool.Erase); t += 48;

            Label help = new Label();
            help.Text = "TP02: X/Y/C/SC para lógica\r\nTMR/CNT: V0001 a V0256\r\nDuplo clique: editar parâmetro\r\nCtrl+Z: desfazer • Del: apagar";
            help.AutoSize = true;
            help.Font = new Font("Segoe UI", 8.2f);
            help.ForeColor = Color.FromArgb(190, 210, 229);
            help.Location = new Point(18, t);
            toolbox.Controls.Add(help);

            Panel editorHost = new Panel();
            editorHost.Dock = DockStyle.Fill;
            editorHost.Padding = new Padding(18);
            editorHost.BackColor = CanvasColor;
            body.Controls.Add(editorHost);
            body.Controls.Add(toolbox);

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

        private void AddCommandButton(Control parent, string text, int left, int width, EventHandler action)
        {
            FlatActionButton b = new FlatActionButton();
            b.Text = text;
            b.Location = new Point(left, 11);
            b.Size = new Size(width, 36);
            b.NormalColor = Color.White;
            b.HoverColor = Color.FromArgb(222, 231, 240);
            b.ForeColor = Navy;
            b.Font = new Font("Segoe UI Semibold", 8.1f, FontStyle.Bold);
            b.Click += action;
            parent.Controls.Add(b);
        }

        private void AddToolButton(Control parent, string text, int top, LadderTool tool)
        {
            FlatActionButton b = new FlatActionButton();
            b.Text = text;
            b.Tag = tool;
            b.Location = new Point(10, top);
            b.Size = new Size(208, 34);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(10, 0, 0, 0);
            b.NormalColor = Navy;
            b.HoverColor = NavyLight;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            b.Click += delegate { SetActiveTool(tool); };
            parent.Controls.Add(b);
        }

        private void SetActiveTool(LadderTool tool)
        {
            activeTool = tool;
            string label = ToolName(tool);
            toolLabel.Text = "Ferramenta: " + label;
            statusLabel.Text = "Clique em uma posição do rung para aplicar: " + label + ".";
        }

        private static string ToolName(LadderTool tool)
        {
            if (tool == LadderTool.ContactNO) return "Contato NA";
            if (tool == LadderTool.ContactNC) return "Contato NF";
            if (tool == LadderTool.ParallelNO) return "Ramo paralelo NA";
            if (tool == LadderTool.ParallelNC) return "Ramo paralelo NF";
            if (tool == LadderTool.Coil) return "OUT / Bobina";
            if (tool == LadderTool.Timer) return "TMR";
            if (tool == LadderTool.Counter) return "CNT";
            if (tool == LadderTool.Set) return "SET F-23";
            if (tool == LadderTool.Reset) return "RESET F-24";
            if (tool == LadderTool.EdgeUp) return "Borda de subida F-05";
            if (tool == LadderTool.EdgeDown) return "Borda de descida F-06";
            if (tool == LadderTool.Function) return "Função especial";
            if (tool == LadderTool.End) return "END F-00";
            if (tool == LadderTool.Erase) return "Apagar";
            return "Selecionar";
        }

        private void CanvasSelectionChanged(object sender, EventArgs e)
        {
            if (canvas.SelectedRung < 0) return;
            LadderElement el = GetSelectedElement();
            string lane = canvas.SelectedLane == 1 ? " • ramo paralelo" : string.Empty;
            statusLabel.Text = "Rung " + (canvas.SelectedRung + 1).ToString() + " • Coluna " + (canvas.SelectedColumn + 1).ToString() + lane +
                (el.Type == LadderElementType.Empty ? " • vazio" : " • " + ElementDisplay(el));
        }

        private LadderElement GetSelectedElement()
        {
            if (canvas.SelectedRung < 0 || canvas.SelectedColumn < 0) return new LadderElement();
            LadderRung rung = rungs[canvas.SelectedRung];
            return canvas.SelectedLane == 1 ? rung.Parallel[canvas.SelectedColumn] : rung.Elements[canvas.SelectedColumn];
        }

        private void CanvasElementAction(object sender, EventArgs e)
        {
            int r = canvas.SelectedRung;
            int c = canvas.SelectedColumn;
            if (r < 0 || c < 0 || activeTool == LadderTool.Select) return;

            if (activeTool == LadderTool.ParallelNO || activeTool == LadderTool.ParallelNC)
            {
                AddParallelContact(r, c, activeTool == LadderTool.ParallelNC);
                return;
            }

            if (activeTool == LadderTool.Erase)
            {
                LadderElement selected = GetSelectedElement();
                if (selected.Type == LadderElementType.Empty) return;
                SaveUndoState();
                selected.Clear();
                MarkChanged("Elemento removido.");
                return;
            }

            if (activeTool == LadderTool.ContactNO || activeTool == LadderTool.ContactNC)
            {
                AddContact(r, c, activeTool == LadderTool.ContactNC);
                return;
            }

            AddOutputInstruction(r, c, activeTool);
        }

        private void AddContact(int r, int c, bool nc)
        {
            if (c == LadderRung.ColumnCount - 1)
            {
                MessageBox.Show("A última coluna é reservada para OUT, TMR, CNT e funções.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string address = AddressDialog.Ask("Contato TP02", "Endereço do contato (X0001, Y0001, C0001 ou SC001):", "X0001");
            if (address == null) return;
            address = NormalizeBitAddress(address);
            if (!IsBitAddress(address))
            {
                MessageBox.Show("Endereço inválido para contato TP02. Use X0001–X0384, Y0001–Y0384, C0001–C2048 ou SC001–SC128.", "Endereço inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveUndoState();
            LadderElement el = rungs[r].Elements[c];
            el.Clear();
            el.Type = nc ? LadderElementType.ContactNC : LadderElementType.ContactNO;
            el.Address = address;
            canvas.SelectedLane = 0;
            MarkChanged((nc ? "Contato NF " : "Contato NA ") + address + " inserido.");
        }

        private void AddParallelContact(int r, int c, bool nc)
        {
            if (c == LadderRung.ColumnCount - 1)
            {
                MessageBox.Show("Ramos paralelos são inseridos nas colunas de condição, antes da saída.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string address = AddressDialog.Ask("Ramo paralelo", "Endereço do contato paralelo:", "C0001");
            if (address == null) return;
            address = NormalizeBitAddress(address);
            if (!IsBitAddress(address))
            {
                MessageBox.Show("Endereço inválido para contato paralelo.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveUndoState();
            LadderElement el = rungs[r].Parallel[c];
            el.Clear();
            el.Type = nc ? LadderElementType.ContactNC : LadderElementType.ContactNO;
            el.Address = address;
            canvas.SelectedLane = 1;
            MarkChanged("Ramo paralelo adicionado em torno da coluna " + (c + 1).ToString() + ".");
        }

        private void AddOutputInstruction(int r, int c, LadderTool tool)
        {
            if (c != LadderRung.ColumnCount - 1)
            {
                MessageBox.Show("OUT, TMR, CNT e funções do TP02 devem ser inseridos na última coluna do rung.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LadderElement next = new LadderElement();

            if (tool == LadderTool.Coil)
            {
                string address = AddressDialog.Ask("OUT / Bobina", "Saída ou ponto auxiliar (Y0001 ou C0001):", "Y0001");
                if (address == null) return;
                address = NormalizeBitAddress(address);
                if (!IsCoilAddress(address)) { ShowOutputAddressError(); return; }
                next.Type = LadderElementType.Coil;
                next.Address = address;
            }
            else if (tool == LadderTool.Timer || tool == LadderTool.Counter)
            {
                string[] values = BlockDialog.Ask(tool == LadderTool.Timer ? "Temporizador TMR" : "Contador CNT",
                    "Identificador V0001–V0256:", "V0001",
                    tool == LadderTool.Timer ? "Preset (número ou D0001–D2048):" : "Valor máximo (número ou D0001–D2048):", "10",
                    tool == LadderTool.Timer);
                if (values == null) return;
                string v = NormalizeVAddress(values[0]);
                string preset = NormalizePreset(values[1]);
                if (!IsVAddress(v))
                {
                    MessageBox.Show("TMR/CNT no TP02 usam identificadores V0001 a V0256.", "Identificador inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!IsPreset(preset))
                {
                    MessageBox.Show("Preset inválido. Use um valor numérico ou um registrador D0001 a D2048.", "Preset inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                next.Type = tool == LadderTool.Timer ? LadderElementType.Timer : LadderElementType.Counter;
                next.Address = v;
                next.Parameter = preset;
                next.Mode = values.Length > 2 ? values[2] : string.Empty;
            }
            else if (tool == LadderTool.Set || tool == LadderTool.Reset)
            {
                string target = AddressDialog.Ask(tool == LadderTool.Set ? "SET F-23" : "RESET F-24", "Bobina alvo (Y0001 ou C0001):", "Y0001");
                if (target == null) return;
                target = NormalizeBitAddress(target);
                if (!IsCoilAddress(target)) { ShowOutputAddressError(); return; }
                next.Type = tool == LadderTool.Set ? LadderElementType.Set : LadderElementType.Reset;
                next.Address = target;
            }
            else if (tool == LadderTool.EdgeUp)
            {
                next.Type = LadderElementType.EdgeUp;
            }
            else if (tool == LadderTool.EdgeDown)
            {
                next.Type = LadderElementType.EdgeDown;
            }
            else if (tool == LadderTool.Function)
            {
                string[] values = FunctionDialog.Ask();
                if (values == null) return;
                string code = NormalizeFunction(values[0]);
                if (!IsFunctionCode(code))
                {
                    MessageBox.Show("Código de função inválido. Exemplos: F-16W, F-20, F-31.", "Função inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                next.Type = LadderElementType.Function;
                next.Address = code;
                next.Parameter = values[1].Trim();
            }
            else if (tool == LadderTool.End)
            {
                next.Type = LadderElementType.End;
                next.Address = "F-00";
            }
            else return;

            SaveUndoState();
            CopyElement(next, rungs[r].Elements[c]);
            rungs[r].Parallel[c].Clear();
            canvas.SelectedLane = 0;
            MarkChanged(ElementDisplay(next) + " inserido.");
        }

        private static void CopyElement(LadderElement source, LadderElement target)
        {
            target.Type = source.Type;
            target.Address = source.Address;
            target.Parameter = source.Parameter;
            target.Mode = source.Mode;
        }

        private void ShowOutputAddressError()
        {
            MessageBox.Show("Para OUT, SET e RESET use uma saída Y0001–Y0384 ou ponto auxiliar C0001–C2048.", "Endereço inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void CanvasElementDoubleClick(object sender, EventArgs e)
        {
            LadderElement el = GetSelectedElement();
            if (el.Type == LadderElementType.Empty) return;

            if (el.Type == LadderElementType.ContactNO || el.Type == LadderElementType.ContactNC)
            {
                string address = AddressDialog.Ask("Editar contato", "Novo endereço:", el.Address);
                if (address == null) return;
                address = NormalizeBitAddress(address);
                if (!IsBitAddress(address)) { MessageBox.Show("Endereço inválido.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                SaveUndoState(); el.Address = address; MarkChanged("Contato atualizado."); return;
            }

            if (el.Type == LadderElementType.Coil || el.Type == LadderElementType.Set || el.Type == LadderElementType.Reset)
            {
                string address = AddressDialog.Ask("Editar saída", "Novo endereço:", el.Address);
                if (address == null) return;
                address = NormalizeBitAddress(address);
                if (!IsCoilAddress(address)) { ShowOutputAddressError(); return; }
                SaveUndoState(); el.Address = address; MarkChanged("Saída atualizada."); return;
            }

            if (el.Type == LadderElementType.Timer || el.Type == LadderElementType.Counter)
            {
                string[] values = BlockDialog.Ask(el.Type == LadderElementType.Timer ? "Editar TMR" : "Editar CNT",
                    "Identificador V0001–V0256:", el.Address,
                    el.Type == LadderElementType.Timer ? "Preset:" : "Valor máximo:", el.Parameter,
                    el.Type == LadderElementType.Timer);
                if (values == null) return;
                string v = NormalizeVAddress(values[0]);
                string preset = NormalizePreset(values[1]);
                if (!IsVAddress(v) || !IsPreset(preset)) { MessageBox.Show("Parâmetros inválidos.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                SaveUndoState(); el.Address = v; el.Parameter = preset; el.Mode = values.Length > 2 ? values[2] : string.Empty; MarkChanged("Bloco atualizado."); return;
            }

            if (el.Type == LadderElementType.Function)
            {
                string[] values = FunctionDialog.Ask(el.Address, el.Parameter);
                if (values == null) return;
                string code = NormalizeFunction(values[0]);
                if (!IsFunctionCode(code)) { MessageBox.Show("Código de função inválido.", "PC12 Ladder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                SaveUndoState(); el.Address = code; el.Parameter = values[1].Trim(); MarkChanged("Função atualizada.");
            }
        }

        private static string NormalizeBitAddress(string value)
        {
            if (value == null) return string.Empty;
            string v = value.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            string prefix = string.Empty;
            string digits = string.Empty;
            if (v.StartsWith("SC")) { prefix = "SC"; digits = v.Substring(2); }
            else if (v.Length > 1) { prefix = v.Substring(0, 1); digits = v.Substring(1); }
            int n;
            if (!int.TryParse(digits, out n)) return v;
            if (prefix == "SC") return "SC" + n.ToString("000");
            if (prefix == "X" || prefix == "Y" || prefix == "C") return prefix + n.ToString("0000");
            return v;
        }

        private static bool IsBitAddress(string value)
        {
            int n;
            if (value.StartsWith("SC") && int.TryParse(value.Substring(2), out n)) return n >= 1 && n <= 128;
            if (value.StartsWith("X") && int.TryParse(value.Substring(1), out n)) return n >= 1 && n <= 384;
            if (value.StartsWith("Y") && int.TryParse(value.Substring(1), out n)) return n >= 1 && n <= 384;
            if (value.StartsWith("C") && int.TryParse(value.Substring(1), out n)) return n >= 1 && n <= 2048;
            return false;
        }

        private static bool IsCoilAddress(string value)
        {
            int n;
            if (value.StartsWith("Y") && int.TryParse(value.Substring(1), out n)) return n >= 1 && n <= 384;
            if (value.StartsWith("C") && int.TryParse(value.Substring(1), out n)) return n >= 1 && n <= 2048;
            return false;
        }

        private static string NormalizeVAddress(string value)
        {
            if (value == null) return string.Empty;
            string v = value.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (!v.StartsWith("V")) return v;
            int n;
            if (!int.TryParse(v.Substring(1), out n)) return v;
            return "V" + n.ToString("0000");
        }

        private static bool IsVAddress(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("V")) return false;
            int n;
            return int.TryParse(value.Substring(1), out n) && n >= 1 && n <= 256;
        }

        private static string NormalizePreset(string value)
        {
            if (value == null) return string.Empty;
            string v = value.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (v.StartsWith("D"))
            {
                int n;
                if (int.TryParse(v.Substring(1), out n)) return "D" + n.ToString("0000");
            }
            return v;
        }

        private static bool IsPreset(string value)
        {
            int n;
            if (int.TryParse(value, out n)) return n >= 0 && n <= 65535;
            if (value.StartsWith("D") && int.TryParse(value.Substring(1), out n)) return n >= 1 && n <= 2048;
            return false;
        }

        private static string NormalizeFunction(string value)
        {
            if (value == null) return string.Empty;
            string v = value.Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (v.StartsWith("F") && !v.StartsWith("F-")) v = "F-" + v.Substring(1);
            return v;
        }

        private static bool IsFunctionCode(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("F-") || value.Length < 4) return false;
            string tail = value.Substring(2);
            if (tail.EndsWith("W")) tail = tail.Substring(0, tail.Length - 1);
            int n;
            return int.TryParse(tail, out n) && n >= 0 && n <= 99;
        }

        private static bool IsOutputType(LadderElementType type)
        {
            return type == LadderElementType.Coil || type == LadderElementType.Timer || type == LadderElementType.Counter ||
                   type == LadderElementType.Set || type == LadderElementType.Reset || type == LadderElementType.EdgeUp ||
                   type == LadderElementType.EdgeDown || type == LadderElementType.Function || type == LadderElementType.End;
        }

        private static string ElementDisplay(LadderElement el)
        {
            if (el.Type == LadderElementType.ContactNO) return "Contato NA " + el.Address;
            if (el.Type == LadderElementType.ContactNC) return "Contato NF " + el.Address;
            if (el.Type == LadderElementType.Coil) return "OUT " + el.Address;
            if (el.Type == LadderElementType.Timer) return "TMR " + el.Address + "  " + el.Parameter + (el.Mode == "RESET" ? "  [com RESET]" : "");
            if (el.Type == LadderElementType.Counter) return "CNT " + el.Address + "  " + el.Parameter;
            if (el.Type == LadderElementType.Set) return "SET F-23  " + el.Address;
            if (el.Type == LadderElementType.Reset) return "RESET F-24  " + el.Address;
            if (el.Type == LadderElementType.EdgeUp) return "Borda ↑ F-05";
            if (el.Type == LadderElementType.EdgeDown) return "Borda ↓ F-06";
            if (el.Type == LadderElementType.Function) return el.Address + (string.IsNullOrEmpty(el.Parameter) ? string.Empty : "  " + el.Parameter);
            if (el.Type == LadderElementType.End) return "END F-00";
            return "Vazio";
        }

        private void AddRung()
        {
            SaveUndoState();
            rungs.Add(new LadderRung());
            canvas.SelectedRung = rungs.Count - 1;
            canvas.SelectedColumn = 0;
            canvas.SelectedLane = 0;
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
            canvas.SelectedLane = 0;
            MarkChanged("Rung removido.");
        }

        private void DeleteSelectedElement()
        {
            LadderElement el = GetSelectedElement();
            if (el.Type == LadderElementType.Empty) return;
            SaveUndoState();
            el.Clear();
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
            canvas.SelectedLane = 0;
            canvas.Invalidate();
            UpdateProjectLabel();
            statusLabel.Text = "Novo projeto TP02 criado.";
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
                DeserializeProject(File.ReadAllText(dlg.FileName, Encoding.UTF8));
                currentFile = dlg.FileName;
                dirty = false;
                undoStack.Clear();
                canvas.SelectedRung = 0;
                canvas.SelectedColumn = 0;
                canvas.SelectedLane = 0;
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
            sb.AppendLine("PC12-LADDER|2");
            int r;
            for (r = 0; r < rungs.Count; r++)
            {
                sb.Append("RUNG");
                int c;
                for (c = 0; c < LadderRung.ColumnCount; c++)
                {
                    sb.Append('|').Append(EncodeElement(rungs[r].Elements[c])).Append('~').Append(EncodeElement(rungs[r].Parallel[c]));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string EncodeElement(LadderElement e)
        {
            if (e.Type == LadderElementType.Empty) return "EMPTY";
            if (e.Type == LadderElementType.ContactNO) return "NO:" + Escape(e.Address);
            if (e.Type == LadderElementType.ContactNC) return "NC:" + Escape(e.Address);
            if (e.Type == LadderElementType.Coil) return "COIL:" + Escape(e.Address);
            if (e.Type == LadderElementType.Timer) return "TMR:" + Escape(e.Address) + ":" + Escape(e.Parameter) + ":" + Escape(e.Mode);
            if (e.Type == LadderElementType.Counter) return "CNT:" + Escape(e.Address) + ":" + Escape(e.Parameter);
            if (e.Type == LadderElementType.Set) return "SET:" + Escape(e.Address);
            if (e.Type == LadderElementType.Reset) return "RST:" + Escape(e.Address);
            if (e.Type == LadderElementType.EdgeUp) return "EUP";
            if (e.Type == LadderElementType.EdgeDown) return "EDN";
            if (e.Type == LadderElementType.Function) return "FUN:" + Escape(e.Address) + ":" + Escape(e.Parameter);
            if (e.Type == LadderElementType.End) return "END";
            return "EMPTY";
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value == null ? string.Empty : value);
        }

        private static string Unescape(string value)
        {
            return Uri.UnescapeDataString(value == null ? string.Empty : value);
        }

        private void DeserializeProject(string data)
        {
            string[] lines = data.Replace("\r", string.Empty).Split('\n');
            if (lines.Length == 0) throw new InvalidDataException("Arquivo vazio.");
            string header = lines[0].Trim();
            if (header != "PC12-LADDER|1" && header != "PC12-LADDER|2") throw new InvalidDataException("Formato de projeto não reconhecido.");
            bool legacy = header == "PC12-LADDER|1";
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
                    if (legacy)
                    {
                        DecodeLegacy(parts[c + 1], rung.Elements[c]);
                    }
                    else
                    {
                        string[] lanes = parts[c + 1].Split('~');
                        DecodeElement(lanes[0], rung.Elements[c]);
                        if (lanes.Length > 1) DecodeElement(lanes[1], rung.Parallel[c]);
                    }
                }
                loaded.Add(rung);
            }
            if (loaded.Count == 0) loaded.Add(new LadderRung());
            rungs.Clear();
            rungs.AddRange(loaded);
        }

        private static void DecodeLegacy(string token, LadderElement e)
        {
            if (token == "EMPTY") return;
            int colon = token.IndexOf(':');
            if (colon <= 0) return;
            string kind = token.Substring(0, colon);
            string address = token.Substring(colon + 1);
            if (kind == "NO") e.Type = LadderElementType.ContactNO;
            else if (kind == "NC") e.Type = LadderElementType.ContactNC;
            else if (kind == "COIL") e.Type = LadderElementType.Coil;
            e.Address = address;
        }

        private static void DecodeElement(string token, LadderElement e)
        {
            e.Clear();
            if (string.IsNullOrEmpty(token) || token == "EMPTY") return;
            string[] p = token.Split(':');
            string kind = p[0];
            if (kind == "NO") { e.Type = LadderElementType.ContactNO; if (p.Length > 1) e.Address = Unescape(p[1]); }
            else if (kind == "NC") { e.Type = LadderElementType.ContactNC; if (p.Length > 1) e.Address = Unescape(p[1]); }
            else if (kind == "COIL") { e.Type = LadderElementType.Coil; if (p.Length > 1) e.Address = Unescape(p[1]); }
            else if (kind == "TMR") { e.Type = LadderElementType.Timer; if (p.Length > 1) e.Address = Unescape(p[1]); if (p.Length > 2) e.Parameter = Unescape(p[2]); if (p.Length > 3) e.Mode = Unescape(p[3]); }
            else if (kind == "CNT") { e.Type = LadderElementType.Counter; if (p.Length > 1) e.Address = Unescape(p[1]); if (p.Length > 2) e.Parameter = Unescape(p[2]); }
            else if (kind == "SET") { e.Type = LadderElementType.Set; if (p.Length > 1) e.Address = Unescape(p[1]); }
            else if (kind == "RST") { e.Type = LadderElementType.Reset; if (p.Length > 1) e.Address = Unescape(p[1]); }
            else if (kind == "EUP") e.Type = LadderElementType.EdgeUp;
            else if (kind == "EDN") e.Type = LadderElementType.EdgeDown;
            else if (kind == "FUN") { e.Type = LadderElementType.Function; if (p.Length > 1) e.Address = Unescape(p[1]); if (p.Length > 2) e.Parameter = Unescape(p[2]); }
            else if (kind == "END") { e.Type = LadderElementType.End; e.Address = "F-00"; }
            else throw new InvalidDataException("Elemento desconhecido: " + kind);
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
            if (undoStack.Count == 0) { statusLabel.Text = "Nada para desfazer."; return; }
            DeserializeProject(undoStack.Pop());
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

        private bool ValidateProject(bool showDialog)
        {
            List<string> issues = new List<string>();
            Dictionary<string, string> usedV = new Dictionary<string, string>();
            int endCount = 0;
            int lastNonEmpty = -1;
            int r;
            for (r = 0; r < rungs.Count; r++)
            {
                LadderRung rung = rungs[r];
                bool hasAny = false;
                int c;
                for (c = 0; c < LadderRung.ColumnCount; c++)
                {
                    if (rung.Elements[c].Type != LadderElementType.Empty || rung.Parallel[c].Type != LadderElementType.Empty) hasAny = true;
                    if (rung.Parallel[c].Type != LadderElementType.Empty && c == LadderRung.ColumnCount - 1) issues.Add("Rung " + (r + 1).ToString() + ": ramo paralelo não pode ocupar a coluna de saída.");
                    LadderElement e = rung.Elements[c];
                    if ((e.Type == LadderElementType.Timer || e.Type == LadderElementType.Counter) && !string.IsNullOrEmpty(e.Address))
                    {
                        if (usedV.ContainsKey(e.Address)) issues.Add("Identificador " + e.Address + " repetido em TMR/CNT. O TP02 compartilha V0001–V0256 entre temporizadores e contadores.");
                        else usedV.Add(e.Address, "Rung " + (r + 1).ToString());
                    }
                    if (e.Type == LadderElementType.End) endCount++;
                }
                if (hasAny)
                {
                    lastNonEmpty = r;
                    if (!IsOutputType(rung.Elements[LadderRung.ColumnCount - 1].Type)) issues.Add("Rung " + (r + 1).ToString() + ": falta uma instrução de saída na última coluna.");
                }
            }
            if (endCount > 1) issues.Add("Existe mais de um END F-00 no projeto.");
            if (endCount == 1 && lastNonEmpty >= 0 && rungs[lastNonEmpty].Elements[LadderRung.ColumnCount - 1].Type != LadderElementType.End) issues.Add("END F-00 deve encerrar o último rung utilizado.");

            if (showDialog)
            {
                if (issues.Count == 0)
                    MessageBox.Show("Estrutura validada: nenhum problema básico encontrado.\r\n\r\nObservação: esta validação ainda não substitui a compilação oficial do PC12.", "Validação Ladder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show(string.Join("\r\n", issues.ToArray()), "Validação Ladder — revisar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            statusLabel.Text = issues.Count == 0 ? "Validação concluída sem problemas básicos." : "Validação encontrou " + issues.Count.ToString() + " ponto(s) para revisão.";
            return issues.Count == 0;
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

    internal sealed class LadderCanvas : ScrollableControl
    {
        public List<LadderRung> Rungs;
        public int SelectedRung = -1;
        public int SelectedColumn = -1;
        public int SelectedLane = 0;
        public event EventHandler SelectionChanged;
        public event EventHandler ElementAction;
        public event EventHandler ElementDoubleClick;

        private const int TopMargin = 24;
        private const int RungHeight = 116;
        private const int LeftRail = 46;
        private const int RightMargin = 34;

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
            AutoScrollMinSize = new Size(850, totalHeight);
            Point scroll = AutoScrollPosition;
            g.TranslateTransform(scroll.X, scroll.Y);

            int width = Math.Max(ClientSize.Width - RightMargin, 850);
            int rightRail = width - 28;
            int usable = rightRail - LeftRail;
            int cellWidth = usable / LadderRung.ColumnCount;

            using (Pen railPen = new Pen(Color.FromArgb(47, 64, 80), 3.0f))
            {
                int bottom = TopMargin + Math.Max(1, Rungs.Count) * RungHeight;
                g.DrawLine(railPen, LeftRail, TopMargin - 8, LeftRail, bottom - 12);
                g.DrawLine(railPen, rightRail, TopMargin - 8, rightRail, bottom - 12);
            }

            int r;
            for (r = 0; r < Rungs.Count; r++)
            {
                int rungTop = TopMargin + r * RungHeight;
                int y = rungTop + 40;
                int branchY = y + 42;
                using (Pen wirePen = new Pen(Color.FromArgb(52, 66, 80), 2.0f)) g.DrawLine(wirePen, LeftRail, y, rightRail, y);
                using (Font rungFont = new Font("Segoe UI Semibold", 8.0f, FontStyle.Bold))
                using (Brush rungBrush = new SolidBrush(Color.FromArgb(112, 126, 140))) g.DrawString((r + 1).ToString("000"), rungFont, rungBrush, 7, y - 9);

                int c;
                for (c = 0; c < LadderRung.ColumnCount; c++)
                {
                    int cellLeft = LeftRail + c * cellWidth;
                    Rectangle mainCell = new Rectangle(cellLeft + 2, y - 30, cellWidth - 4, 60);
                    if (r == SelectedRung && c == SelectedColumn && SelectedLane == 0) DrawSelection(g, mainCell);
                    DrawElement(g, Rungs[r].Elements[c], mainCell, y, false);

                    LadderElement branch = Rungs[r].Parallel[c];
                    if (branch.Type != LadderElementType.Empty && c < LadderRung.ColumnCount - 1)
                    {
                        int x1 = cellLeft + 9;
                        int x2 = cellLeft + cellWidth - 9;
                        using (Pen bp = new Pen(Color.FromArgb(52, 66, 80), 1.8f))
                        {
                            g.DrawLine(bp, x1, y, x1, branchY);
                            g.DrawLine(bp, x1, branchY, x2, branchY);
                            g.DrawLine(bp, x2, branchY, x2, y);
                        }
                        Rectangle branchCell = new Rectangle(cellLeft + 2, branchY - 22, cellWidth - 4, 44);
                        if (r == SelectedRung && c == SelectedColumn && SelectedLane == 1) DrawSelection(g, branchCell);
                        DrawElement(g, branch, branchCell, branchY, true);
                    }
                }
            }
            g.ResetTransform();
        }

        private static void DrawSelection(Graphics g, Rectangle cell)
        {
            using (Brush sel = new SolidBrush(Color.FromArgb(226, 240, 252))) g.FillRectangle(sel, cell);
            using (Pen selPen = new Pen(Color.FromArgb(0, 122, 204), 1.0f)) g.DrawRectangle(selPen, cell);
        }

        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch)
        {
            if (element.Type == LadderElementType.Empty) return;
            int cx = cell.Left + cell.Width / 2;
            using (Pen p = new Pen(Color.FromArgb(29, 43, 56), 2.2f))
            {
                if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC)
                {
                    g.DrawLine(p, cx - 16, y - 12, cx - 16, y + 12);
                    g.DrawLine(p, cx + 16, y - 12, cx + 16, y + 12);
                    if (element.Type == LadderElementType.ContactNC) g.DrawLine(p, cx - 19, y + 14, cx + 19, y - 14);
                }
                else if (element.Type == LadderElementType.Coil)
                {
                    g.DrawArc(p, new Rectangle(cx - 23, y - 15, 22, 30), -90, 180);
                    g.DrawArc(p, new Rectangle(cx + 1, y - 15, 22, 30), 90, 180);
                }
                else
                {
                    Rectangle block = new Rectangle(cx - 34, y - 20, 68, 40);
                    using (Brush fill = new SolidBrush(Color.FromArgb(247, 250, 253))) g.FillRectangle(fill, block);
                    g.DrawRectangle(p, block);
                }
            }

            string top = string.Empty;
            string bottom = string.Empty;
            if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC || element.Type == LadderElementType.Coil) top = element.Address;
            else if (element.Type == LadderElementType.Timer) { top = element.Mode == "RESET" ? "TMR-R" : "TMR"; bottom = element.Address + " " + element.Parameter; }
            else if (element.Type == LadderElementType.Counter) { top = "CNT"; bottom = element.Address + " " + element.Parameter; }
            else if (element.Type == LadderElementType.Set) { top = "SET F-23"; bottom = element.Address; }
            else if (element.Type == LadderElementType.Reset) { top = "RST F-24"; bottom = element.Address; }
            else if (element.Type == LadderElementType.EdgeUp) top = "↑  F-05";
            else if (element.Type == LadderElementType.EdgeDown) top = "↓  F-06";
            else if (element.Type == LadderElementType.Function) { top = element.Address; bottom = element.Parameter; }
            else if (element.Type == LadderElementType.End) top = "END F-00";

            using (Font f = new Font("Consolas", branch ? 7.6f : 8.4f, FontStyle.Bold))
            using (Brush b = new SolidBrush(Color.FromArgb(0, 102, 170)))
            {
                if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC || element.Type == LadderElementType.Coil)
                {
                    SizeF size = g.MeasureString(top, f);
                    float ty = branch ? y + 14 : y - 31;
                    g.DrawString(top, f, b, cx - size.Width / 2, ty);
                }
                else
                {
                    SizeF size = g.MeasureString(top, f);
                    g.DrawString(top, f, b, cx - size.Width / 2, y - 12);
                    if (!string.IsNullOrEmpty(bottom))
                    {
                        using (Font f2 = new Font("Consolas", 7.2f, FontStyle.Regular))
                        using (Brush b2 = new SolidBrush(Color.FromArgb(77, 91, 105)))
                        {
                            string text = bottom.Length > 14 ? bottom.Substring(0, 14) : bottom;
                            SizeF s2 = g.MeasureString(text, f2);
                            g.DrawString(text, f2, b2, cx - s2.Width / 2, y + 3);
                        }
                    }
                }
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
            int width = Math.Max(ClientSize.Width - RightMargin, 850);
            int rightRail = width - 28;
            int usable = rightRail - LeftRail;
            int cellWidth = usable / LadderRung.ColumnCount;
            int rung = (py - TopMargin) / RungHeight;
            int col = (px - LeftRail) / cellWidth;
            if (rung < 0 || rung >= Rungs.Count || col < 0 || col >= LadderRung.ColumnCount) return;
            int rungTop = TopMargin + rung * RungHeight;
            int y = rungTop + 40;
            int branchY = y + 42;
            SelectedRung = rung;
            SelectedColumn = col;
            SelectedLane = (Math.Abs(py - branchY) <= 22 && Rungs[rung].Parallel[col].Type != LadderElementType.Empty) ? 1 : 0;
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
            ClientSize = new Size(440, 160);
            Font = new Font("Segoe UI", 9.0f);
            BackColor = Color.White;

            Label label = new Label();
            label.Text = prompt;
            label.AutoSize = false;
            label.Size = new Size(400, 42);
            label.Location = new Point(20, 18);
            label.ForeColor = Color.FromArgb(45, 58, 72);
            Controls.Add(label);

            input = new TextBox();
            input.Text = defaultValue;
            input.Font = new Font("Consolas", 11.0f, FontStyle.Bold);
            input.Location = new Point(20, 64);
            input.Size = new Size(400, 26);
            input.CharacterCasing = CharacterCasing.Upper;
            Controls.Add(input);

            Button ok = new Button(); ok.Text = "OK"; ok.Location = new Point(254, 112); ok.Size = new Size(78, 30);
            ok.Click += delegate { result = input.Text; DialogResult = DialogResult.OK; Close(); }; Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Cancelar"; cancel.Location = new Point(342, 112); cancel.Size = new Size(78, 30); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            Shown += delegate { input.SelectAll(); input.Focus(); };
        }

        public static string Ask(string title, string prompt, string defaultValue)
        {
            using (AddressDialog dlg = new AddressDialog(title, prompt, defaultValue)) return dlg.ShowDialog() == DialogResult.OK ? dlg.result : null;
        }
    }

    internal sealed class BlockDialog : Form
    {
        private TextBox first;
        private TextBox second;
        private CheckBox reset;
        private string[] result;

        private BlockDialog(string title, string label1, string value1, string label2, string value2, bool showReset)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(470, showReset ? 242 : 210);
            Font = new Font("Segoe UI", 9.0f);
            BackColor = Color.White;

            AddLabel(label1, 18);
            first = AddText(value1, 48);
            AddLabel(label2, 86);
            second = AddText(value2, 116);

            if (showReset)
            {
                reset = new CheckBox();
                reset.Text = "Temporizador com entrada de RESET";
                reset.AutoSize = true;
                reset.Location = new Point(20, 154);
                Controls.Add(reset);
            }

            int y = showReset ? 194 : 162;
            Button ok = new Button(); ok.Text = "OK"; ok.Location = new Point(284, y); ok.Size = new Size(78, 30);
            ok.Click += delegate { result = showReset ? new string[] { first.Text, second.Text, reset.Checked ? "RESET" : "AUTO" } : new string[] { first.Text, second.Text }; DialogResult = DialogResult.OK; Close(); }; Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Cancelar"; cancel.Location = new Point(372, y); cancel.Size = new Size(78, 30); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        private void AddLabel(string text, int top)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(20, top); l.ForeColor = Color.FromArgb(45, 58, 72); Controls.Add(l);
        }

        private TextBox AddText(string text, int top)
        {
            TextBox b = new TextBox(); b.Text = text; b.Font = new Font("Consolas", 10.5f, FontStyle.Bold); b.Location = new Point(20, top); b.Size = new Size(430, 26); b.CharacterCasing = CharacterCasing.Upper; Controls.Add(b); return b;
        }

        public static string[] Ask(string title, string label1, string value1, string label2, string value2, bool showReset)
        {
            using (BlockDialog dlg = new BlockDialog(title, label1, value1, label2, value2, showReset)) return dlg.ShowDialog() == DialogResult.OK ? dlg.result : null;
        }
    }

    internal sealed class FunctionDialog : Form
    {
        private TextBox code;
        private TextBox parameters;
        private string[] result;

        private FunctionDialog(string initialCode, string initialParameters)
        {
            Text = "Função especial TP02";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(500, 226);
            Font = new Font("Segoe UI", 9.0f);
            BackColor = Color.White;

            Label l1 = new Label(); l1.Text = "Código da função (ex.: F-16W):"; l1.AutoSize = true; l1.Location = new Point(20, 20); Controls.Add(l1);
            code = new TextBox(); code.Text = initialCode; code.CharacterCasing = CharacterCasing.Upper; code.Font = new Font("Consolas", 10.5f, FontStyle.Bold); code.Location = new Point(20, 48); code.Size = new Size(180, 26); Controls.Add(code);
            Label l2 = new Label(); l2.Text = "Parâmetros (conforme manual da função):"; l2.AutoSize = true; l2.Location = new Point(20, 90); Controls.Add(l2);
            parameters = new TextBox(); parameters.Text = initialParameters; parameters.Font = new Font("Consolas", 10.0f); parameters.Location = new Point(20, 118); parameters.Size = new Size(460, 26); Controls.Add(parameters);
            Label note = new Label(); note.Text = "SET F-23, RESET F-24, F-05, F-06 e END F-00 já possuem botões próprios."; note.AutoSize = true; note.ForeColor = Color.FromArgb(95, 108, 122); note.Location = new Point(20, 151); Controls.Add(note);

            Button ok = new Button(); ok.Text = "OK"; ok.Location = new Point(314, 178); ok.Size = new Size(78, 30); ok.Click += delegate { result = new string[] { code.Text, parameters.Text }; DialogResult = DialogResult.OK; Close(); }; Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Cancelar"; cancel.Location = new Point(402, 178); cancel.Size = new Size(78, 30); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        public static string[] Ask()
        {
            return Ask("F-16W", string.Empty);
        }

        public static string[] Ask(string code, string parameters)
        {
            using (FunctionDialog dlg = new FunctionDialog(code, parameters)) return dlg.ShowDialog() == DialogResult.OK ? dlg.result : null;
        }
    }
}
