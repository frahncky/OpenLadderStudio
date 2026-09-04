using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class DirectStudioProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DirectStudioForm());
        }
    }

    /// <summary>
    /// Ordena a ancoragem dos filhos de um container.
    ///
    /// O layout do Windows Forms percorre os filhos do ultimo indice para o primeiro:
    /// quem esta no indice mais alto escolhe seu espaco primeiro e fica na borda
    /// externa. Um controle Fill inserido por ultimo ocupa toda a area util e as
    /// barras ancoradas passam a se sobrepor ao conteudo.
    ///
    /// Passe o controle Fill primeiro e depois as barras, da mais interna para a mais
    /// externa.
    /// </summary>
    internal static class DockOrder
    {
        public static void Apply(Control parent, params Control[] fillThenInnerToOuter)
        {
            if (parent == null || fillThenInnerToOuter == null) return;
            int i;
            for (i = 0; i < fillThenInnerToOuter.Length; i++)
            {
                Control c = fillThenInnerToOuter[i];
                if (c != null && c.Parent == parent) parent.Controls.SetChildIndex(c, i);
            }
        }
    }

    internal sealed class OpenLadderColorTable : ProfessionalColorTable
    {
        private readonly Color chrome = Color.FromArgb(37, 39, 43);
        private readonly Color hover = Color.FromArgb(52, 55, 60);
        private readonly Color border = Color.FromArgb(61, 64, 69);

        public override Color MenuStripGradientBegin { get { return chrome; } }
        public override Color MenuStripGradientEnd { get { return chrome; } }
        public override Color SeparatorDark { get { return border; } }
        public override Color SeparatorLight { get { return chrome; } }
        public override Color ToolStripDropDownBackground { get { return chrome; } }
        public override Color MenuBorder { get { return border; } }
        public override Color MenuItemBorder { get { return hover; } }
        public override Color MenuItemSelected { get { return hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return hover; } }
        public override Color MenuItemPressedGradientBegin { get { return hover; } }
        public override Color MenuItemPressedGradientEnd { get { return hover; } }
        public override Color ImageMarginGradientBegin { get { return chrome; } }
        public override Color ImageMarginGradientMiddle { get { return chrome; } }
        public override Color ImageMarginGradientEnd { get { return chrome; } }
        public override Color ToolStripBorder { get { return border; } }
        public override Color ToolStripGradientBegin { get { return chrome; } }
        public override Color ToolStripGradientMiddle { get { return chrome; } }
        public override Color ToolStripGradientEnd { get { return chrome; } }
        public override Color ButtonSelectedGradientBegin { get { return hover; } }
        public override Color ButtonSelectedGradientMiddle { get { return hover; } }
        public override Color ButtonSelectedGradientEnd { get { return hover; } }
        public override Color ButtonPressedGradientBegin { get { return Color.FromArgb(43, 126, 84); } }
        public override Color ButtonPressedGradientMiddle { get { return Color.FromArgb(43, 126, 84); } }
        public override Color ButtonPressedGradientEnd { get { return Color.FromArgb(43, 126, 84); } }
    }

    internal sealed class DirectStudioForm : Form
    {
        private readonly Color Shell = Color.FromArgb(29, 31, 34);
        private readonly Color Chrome = Color.FromArgb(37, 39, 43);
        private readonly Color ChromeLight = Color.FromArgb(47, 50, 55);
        private readonly Color Border = Color.FromArgb(61, 64, 69);
        private readonly Color Accent = Color.FromArgb(45, 170, 107);
        private readonly Color AccentDark = Color.FromArgb(34, 135, 83);
        private readonly Color Workspace = Color.FromArgb(235, 238, 241);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(150, 157, 164);

        private Panel host;
        private Panel inspector;
        private Label documentTitle;
        private Label statusText;
        private Label modeText;
        private Label projectValue;
        private Label rungsValue;
        private Label connectionValue;
        private Button activeRailButton;

        private LadderEditorForm ladderForm;
        private TP02BridgeForm bridgeForm;
        private TP02ProgramReaderForm readerForm;
        private TP02AutoDecoderForm decoderForm;
        private TP02CalibrationCampaignForm calibrationForm;
        private TP02IlToLadderForm ilForm;
        private PC12UpdaterForm updaterForm;

        public DirectStudioForm()
        {
            Text = "OpenLadder Studio";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            Size = new Size(1500, 900);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            BuildUi();
            ShowLadder();
        }

        // O layout ancorado do Windows Forms percorre os filhos do fim para o inicio da
        // colecao: o ultimo controle adicionado escolhe seu espaco primeiro e fica na
        // borda externa. Por isso cada container recebe o controle Fill primeiro e os
        // controles de borda depois, do mais interno para o mais externo.
        //
        // BringToFront() move o controle para o indice 0, ou seja, para o FIM da fila de
        // ancoragem. Aplicado a barras ancoradas, ele fazia o painel Fill ocupar toda a
        // area antes e as barras se sobrepunham ao conteudo: era o que deixava o menu
        // flutuando sobre a area de trabalho e a trilha lateral cobrindo a paleta do
        // editor ladder.
        private void BuildUi()
        {
            Panel workspace = new Panel();
            workspace.Dock = DockStyle.Fill;
            workspace.BackColor = Shell;
            Controls.Add(workspace);

            Panel status = BuildStatusBar();
            Controls.Add(status);

            ToolStrip toolbar = BuildToolbar();
            Controls.Add(toolbar);

            MenuStrip menu = BuildMenu();
            Controls.Add(menu);
            MainMenuStrip = menu;

            Panel center = new Panel();
            center.Dock = DockStyle.Fill;
            center.BackColor = Workspace;
            center.Padding = new Padding(0);
            workspace.Controls.Add(center);

            inspector = BuildInspector();
            workspace.Controls.Add(inspector);

            Panel rail = BuildRail();
            workspace.Controls.Add(rail);

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Workspace;
            center.Controls.Add(host);

            Panel tab = new Panel();
            tab.Dock = DockStyle.Top;
            tab.Height = 34;
            tab.BackColor = ChromeLight;
            tab.Padding = new Padding(14, 0, 10, 0);
            center.Controls.Add(tab);

            documentTitle = new Label();
            documentTitle.Dock = DockStyle.Fill;
            documentTitle.TextAlign = ContentAlignment.MiddleLeft;
            documentTitle.ForeColor = Fore;
            documentTitle.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            tab.Controls.Add(documentTitle);

            Panel accentLine = new Panel();
            accentLine.Dock = DockStyle.Bottom;
            accentLine.Height = 2;
            accentLine.BackColor = Accent;
            tab.Controls.Add(accentLine);
        }

        private MenuStrip BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.Dock = DockStyle.Top;
            menu.Height = 27;
            menu.BackColor = Chrome;
            menu.ForeColor = Fore;
            menu.Padding = new Padding(8, 2, 0, 2);
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new ToolStripProfessionalRenderer(new OpenLadderColorTable());

            ToolStripMenuItem arquivo = MenuItem("Arquivo");
            arquivo.DropDownItems.Add(DropItem("Novo projeto", delegate { InvokeLadder("NewProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(DropItem("Abrir...", delegate { InvokeLadder("OpenProject", null); }));
            arquivo.DropDownItems.Add(DropItem("Salvar", delegate { InvokeLadder("SaveProject", new object[] { false }); }));
            arquivo.DropDownItems.Add(DropItem("Salvar como...", delegate { InvokeLadder("SaveProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(new ToolStripSeparator());
            arquivo.DropDownItems.Add(DropItem("Sair", delegate { Close(); }));

            ToolStripMenuItem editar = MenuItem("Editar");
            editar.DropDownItems.Add(DropItem("Desfazer", delegate { InvokeLadder("Undo", null); }));
            editar.DropDownItems.Add(new ToolStripSeparator());
            editar.DropDownItems.Add(DropItem("Adicionar rung", delegate { InvokeLadder("AddRung", null); }));
            editar.DropDownItems.Add(DropItem("Excluir rung", delegate { InvokeLadder("DeleteSelectedRung", null); }));
            editar.DropDownItems.Add(DropItem("Validar programa", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));

            ToolStripMenuItem plc = MenuItem("PLC");
            plc.DropDownItems.Add(DropItem("Comunicação", delegate { ShowBridge(); }));
            plc.DropDownItems.Add(DropItem("Ler programa", delegate { ShowReader(); }));
            plc.DropDownItems.Add(DropItem("Decodificar programa", delegate { ShowDecoder(); }));

            ToolStripMenuItem ferramentas = MenuItem("Ferramentas");
            ferramentas.DropDownItems.Add(DropItem("Calibração", delegate { ShowCalibration(); }));
            ferramentas.DropDownItems.Add(DropItem("IL para Ladder", delegate { ShowIl(); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());
            ferramentas.DropDownItems.Add(DropItem("Atualizações", delegate { ShowUpdater(); }));

            ToolStripMenuItem ajuda = MenuItem("Ajuda");
            ajuda.DropDownItems.Add(DropItem("Sobre o OpenLadder Studio", delegate
            {
                MessageBox.Show(this, "OpenLadder Studio v0.11\r\nProgramação Ladder e ferramentas para WEG TP02.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));

            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(plc);
            menu.Items.Add(ferramentas);
            menu.Items.Add(ajuda);
            return menu;
        }

        private ToolStrip BuildToolbar()
        {
            ToolStrip bar = new ToolStrip();
            bar.Dock = DockStyle.Top;
            bar.Height = 40;
            bar.BackColor = Chrome;
            bar.ForeColor = Fore;
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Padding = new Padding(8, 4, 8, 4);
            bar.RenderMode = ToolStripRenderMode.Professional;
            bar.Renderer = new ToolStripProfessionalRenderer(new OpenLadderColorTable());

            bar.Items.Add(ToolButton("Novo", delegate { InvokeLadder("NewProject", new object[] { true }); }));
            bar.Items.Add(ToolButton("Abrir", delegate { InvokeLadder("OpenProject", null); }));
            bar.Items.Add(ToolButton("Salvar", delegate { InvokeLadder("SaveProject", new object[] { false }); }));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(ToolButton("Desfazer", delegate { InvokeLadder("Undo", null); }));
            bar.Items.Add(ToolButton("+ Rung", delegate { InvokeLadder("AddRung", null); }));
            bar.Items.Add(ToolButton("Validar", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));
            bar.Items.Add(new ToolStripSeparator());

            ToolStripButton plc = ToolButton("PLC", delegate { ShowBridge(); });
            plc.ForeColor = Accent;
            plc.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            bar.Items.Add(plc);
            bar.Items.Add(ToolButton("Ler PLC", delegate { ShowReader(); }));
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(ToolButton("Atualizar", delegate { ShowUpdater(); }));

            ToolStripLabel brand = new ToolStripLabel("OpenLadder Studio  v0.11");
            brand.Alignment = ToolStripItemAlignment.Right;
            brand.ForeColor = Muted;
            brand.Margin = new Padding(10, 0, 8, 0);
            bar.Items.Add(brand);
            return bar;
        }

        private Panel BuildRail()
        {
            Panel rail = new Panel();
            rail.Dock = DockStyle.Left;
            rail.Width = 68;
            rail.BackColor = Color.FromArgb(31, 33, 37);
            rail.Padding = new Padding(0, 10, 0, 0);

            Label mark = new Label();
            mark.Text = "OL";
            mark.Dock = DockStyle.Top;
            mark.Height = 42;
            mark.TextAlign = ContentAlignment.MiddleCenter;
            mark.ForeColor = Accent;
            mark.Font = new Font("Segoe UI Semibold", 13.0f, FontStyle.Bold);
            rail.Controls.Add(mark);

            int top = 56;
            AddRailButton(rail, "LD", "Ladder", top, delegate { ShowLadder(); }); top += 50;
            AddRailButton(rail, "PLC", "Comunicação", top, delegate { ShowBridge(); }); top += 50;
            AddRailButton(rail, "RBP", "Ler PLC", top, delegate { ShowReader(); }); top += 50;
            AddRailButton(rail, "DEC", "Decodificar", top, delegate { ShowDecoder(); }); top += 50;
            AddRailButton(rail, "CAL", "Calibração", top, delegate { ShowCalibration(); }); top += 50;
            AddRailButton(rail, "IL", "IL para Ladder", top, delegate { ShowIl(); }); top += 50;
            AddRailButton(rail, "UPD", "Atualizar", top, delegate { ShowUpdater(); });
            return rail;
        }

        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Right;
            p.Width = 246;
            p.BackColor = Chrome;

            Label title = InspectorLabel("PROPRIEDADES", 9.0f, true, Muted);
            title.Location = new Point(16, 14);
            p.Controls.Add(title);

            int y = 52;
            Label ignored;
            y = AddInspectorField(p, y, "Projeto", "Sem nome", out projectValue);
            y = AddInspectorField(p, y, "Redes", "0 rung(s)", out rungsValue);

            AddDivider(p, y);
            y += 16;

            y = AddInspectorField(p, y, "Controlador", "WEG TP02-60MR", out ignored);

            Label station = InspectorLabel("Estação  01", 8.4f, false, Muted);
            station.Location = new Point(16, y);
            p.Controls.Add(station);
            y += 30;

            AddDivider(p, y);
            y += 16;

            Label status = InspectorLabel("CONEXÃO", 8.2f, true, Muted);
            status.Location = new Point(16, y);
            p.Controls.Add(status);
            y += 26;

            connectionValue = InspectorLabel("●  OFFLINE", 9.2f, true, Color.FromArgb(168, 174, 181));
            connectionValue.Location = new Point(16, y);
            p.Controls.Add(connectionValue);
            y += 36;

            Button open = new Button();
            open.Text = "Configurar comunicação";
            open.Location = new Point(16, y);
            open.Size = new Size(214, 34);
            open.FlatStyle = FlatStyle.Flat;
            open.FlatAppearance.BorderColor = Border;
            open.BackColor = ChromeLight;
            open.ForeColor = Fore;
            open.Cursor = Cursors.Hand;
            open.Click += delegate { ShowBridge(); };
            p.Controls.Add(open);

            return p;
        }

        // Legenda em cima, valor embaixo. O valor tem largura fixa com reticencias,
        // para que um nome de projeto longo nunca vaze do painel.
        private int AddInspectorField(Control parent, int top, string caption, string initial, out Label value)
        {
            Label c = InspectorLabel(caption, 8.2f, false, Muted);
            c.Location = new Point(16, top);
            parent.Controls.Add(c);

            value = InspectorLabel(initial, 9.2f, true, Fore);
            value.AutoSize = false;
            value.AutoEllipsis = true;
            value.Bounds = new Rectangle(16, top + 20, 214, 19);
            parent.Controls.Add(value);
            return top + 48;
        }

        private Panel BuildStatusBar()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Bottom;
            p.Height = 27;
            p.BackColor = Color.FromArgb(25, 27, 30);
            p.Padding = new Padding(10, 0, 10, 0);

            statusText = new Label();
            statusText.Dock = DockStyle.Fill;
            statusText.TextAlign = ContentAlignment.MiddleLeft;
            statusText.Text = "Pronto";
            statusText.ForeColor = Muted;
            statusText.Font = new Font("Segoe UI", 8.2f);
            p.Controls.Add(statusText);

            modeText = new Label();
            modeText.Dock = DockStyle.Right;
            modeText.Width = 330;
            modeText.TextAlign = ContentAlignment.MiddleRight;
            modeText.Text = "TP02-60MR    |    OFFLINE    |    v0.11";
            modeText.ForeColor = Muted;
            modeText.Font = new Font("Segoe UI", 8.2f);
            p.Controls.Add(modeText);
            return p;
        }

        private void ShowLadder()
        {
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                ladderForm = new LadderEditorForm();
                PrepareLadderForStudio(ladderForm);
            }
            inspector.Visible = true;
            ShowDocument(ladderForm, "Programa Ladder", "LD");
            statusText.Text = "Editor Ladder";
            UpdateProjectName();
        }

        private void ShowBridge()
        {
            if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
            inspector.Visible = false;
            ShowDocument(bridgeForm, "Comunicação com PLC", "PLC");
            statusText.Text = "Comunicação TP02";
        }

        private void ShowReader()
        {
            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            inspector.Visible = false;
            ShowDocument(readerForm, "Leitura do programa", "RBP");
            statusText.Text = "Leitura RBP";
        }

        private void ShowDecoder()
        {
            if (decoderForm == null || decoderForm.IsDisposed) decoderForm = new TP02AutoDecoderForm();
            inspector.Visible = false;
            ShowDocument(decoderForm, "Decodificador", "DEC");
            statusText.Text = "Decodificação offline";
        }

        private void ShowCalibration()
        {
            if (calibrationForm == null || calibrationForm.IsDisposed) calibrationForm = new TP02CalibrationCampaignForm();
            inspector.Visible = false;
            ShowDocument(calibrationForm, "Calibração", "CAL");
            statusText.Text = "Calibração de opcodes";
        }

        private void ShowIl()
        {
            if (ilForm == null || ilForm.IsDisposed) ilForm = new TP02IlToLadderForm();
            inspector.Visible = false;
            ShowDocument(ilForm, "IL → Ladder", "IL");
            statusText.Text = "Reconstrução Ladder";
        }

        private void ShowUpdater()
        {
            if (updaterForm == null || updaterForm.IsDisposed) updaterForm = new PC12UpdaterForm();
            inspector.Visible = false;
            ShowDocument(updaterForm, "Atualizações", "UPD");
            statusText.Text = "Atualizações do OpenLadder Studio";
        }

        private void ShowDocument(Form child, string title, string railCode)
        {
            HideChildren();
            host.Controls.Clear();
            documentTitle.Text = title;
            child.TopLevel = false;
            child.FormBorderStyle = FormBorderStyle.None;
            child.Dock = DockStyle.Fill;
            host.Controls.Add(child);
            child.Show();
            child.BringToFront();
            SelectRail(railCode);
        }

        private void PrepareLadderForStudio(LadderEditorForm form)
        {
            form.BackColor = Workspace;
            foreach (Control c in form.Controls)
            {
                Panel p = c as Panel;
                if (p == null) continue;
                if (p.Dock == DockStyle.Top && (p.Height == 64 || p.Height == 58)) p.Visible = false;
                if (p.Dock == DockStyle.Bottom && p.Height <= 36) p.Visible = false;
            }
            CompactLadderControls(form);
        }

        private void CompactLadderControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Label label = c as Label;
                if (label != null && label.Text != null && label.Text.Length > 90)
                {
                    label.Visible = false;
                }

                if (c.HasChildren) CompactLadderControls(c);
            }
        }

        private void InvokeLadder(string methodName, object[] args)
        {
            ShowLadder();
            try
            {
                MethodInfo method = typeof(LadderEditorForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new MissingMethodException(methodName);
                method.Invoke(ladderForm, args);
                UpdateProjectName();
                statusText.Text = "Pronto";
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                MessageBox.Show(this, inner.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateProjectName()
        {
            if (ladderForm == null || ladderForm.IsDisposed) return;
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("projectLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Label l = field == null ? null : field.GetValue(ladderForm) as Label;
                string value = l == null ? string.Empty : (l.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(value))
                {
                    projectValue.Text = "Sem nome";
                }
                else
                {
                    // O editor publica "<nome>   |   <n> rung(s)" em um rotulo unico.
                    string[] parts = value.Split('|');
                    projectValue.Text = parts[0].Trim();
                    if (parts.Length > 1) rungsValue.Text = parts[1].Trim();
                }
            }
            catch
            {
                projectValue.Text = "Projeto Ladder";
            }
        }

        private void HideChildren()
        {
            Form[] forms = new Form[] { ladderForm, bridgeForm, readerForm, decoderForm, calibrationForm, ilForm, updaterForm };
            for (int i = 0; i < forms.Length; i++)
                if (forms[i] != null && !forms[i].IsDisposed) forms[i].Hide();
        }

        private void AddRailButton(Control parent, string code, string tip, int top, EventHandler action)
        {
            Button b = new Button();
            b.Name = "rail_" + code;
            b.Text = code;
            b.Location = new Point(7, top);
            b.Size = new Size(54, 42);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(31, 33, 37);
            b.ForeColor = Muted;
            b.Font = new Font("Segoe UI Semibold", code.Length > 2 ? 7.5f : 9.0f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            ToolTip tt = new ToolTip();
            tt.SetToolTip(b, tip);
            b.Click += delegate(object sender, EventArgs e)
            {
                SetActiveRail(b);
                action(sender, e);
            };
            parent.Controls.Add(b);
        }

        private void SelectRail(string code)
        {
            Control[] found = Controls.Find("rail_" + code, true);
            if (found.Length > 0)
            {
                Button b = found[0] as Button;
                if (b != null) SetActiveRail(b);
            }
        }

        private void SetActiveRail(Button b)
        {
            if (activeRailButton != null && !activeRailButton.IsDisposed)
            {
                activeRailButton.BackColor = Color.FromArgb(31, 33, 37);
                activeRailButton.ForeColor = Muted;
            }
            b.BackColor = AccentDark;
            b.ForeColor = Color.White;
            activeRailButton = b;
        }

        private ToolStripMenuItem MenuItem(string text)
        {
            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            m.BackColor = Chrome;
            return m;
        }

        private ToolStripMenuItem DropItem(string text, EventHandler click)
        {
            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            m.BackColor = Chrome;
            m.Click += click;
            return m;
        }

        private ToolStripButton ToolButton(string text, EventHandler click)
        {
            ToolStripButton b = new ToolStripButton(text);
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.ForeColor = Fore;
            b.AutoSize = false;
            b.Width = Math.Max(58, TextRenderer.MeasureText(text, Font).Width + 22);
            b.Margin = new Padding(1, 0, 1, 0);
            b.Click += click;
            return b;
        }

        private Label InspectorLabel(string text, float size, bool bold, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = color;
            l.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            return l;
        }

        private void AddDivider(Control parent, int top)
        {
            Panel line = new Panel();
            line.Location = new Point(16, top);
            line.Size = new Size(210, 1);
            line.BackColor = Border;
            parent.Controls.Add(line);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Form[] forms = new Form[] { ladderForm, bridgeForm, readerForm, decoderForm, calibrationForm, ilForm, updaterForm };
            for (int i = 0; i < forms.Length; i++)
                if (forms[i] != null && !forms[i].IsDisposed) forms[i].Dispose();
            base.OnFormClosing(e);
        }
    }
}
