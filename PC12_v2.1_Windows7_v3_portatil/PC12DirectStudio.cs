using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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

    /// <summary>Paleta unica do OpenLadder Studio.</summary>
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
        private readonly Color Shell = StudioTheme.Shell;
        private readonly Color Chrome = StudioTheme.Chrome;
        private readonly Color ChromeLight = StudioTheme.ChromeLight;
        private readonly Color Border = StudioTheme.Border;
        private readonly Color Accent = StudioTheme.Accent;
        private readonly Color AccentDark = StudioTheme.AccentDark;
        private readonly Color Workspace = StudioTheme.Workspace;
        private readonly Color Fore = StudioTheme.Fore;
        private readonly Color Muted = StudioTheme.Muted;

        private Panel host;
        private Panel inspector;
        private DocTabStrip tabStrip;
        private StudioConsole console;
        private StudioPanel consolePanel;
        private readonly List<NavButton> navButtons = new List<NavButton>();
        private Panel navPanel;
        private bool inspectorAllowed = true;
        private ToolStripMenuItem miNav;
        private ToolStripMenuItem miProps;
        private ToolStripMenuItem miConsole;
        private Label statusText;
        private Label modeText;
        private Label projectValue;
        private Label rungsValue;
        private Label connectionValue;

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

            Control status = BuildStatusBar();
            Controls.Add(status);

            Control toolbar = BuildToolbar();
            Controls.Add(toolbar);

            MenuStrip menu = BuildMenu();
            Controls.Add(menu);
            MainMenuStrip = menu;

            Panel center = new Panel();
            center.Dock = DockStyle.Fill;
            center.BackColor = Workspace;
            workspace.Controls.Add(center);

            inspector = BuildInspector();
            workspace.Controls.Add(inspector);

            navPanel = BuildNav();
            workspace.Controls.Add(navPanel);

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Workspace;
            center.Controls.Add(host);

            consolePanel = BuildConsole();
            center.Controls.Add(consolePanel);

            tabStrip = new DocTabStrip();
            tabStrip.SelectedChanged += delegate { ApplySelectedTab(); };
            tabStrip.TabClosed += delegate(StudioTab t)
            {
                if (t.Document != null && !t.Document.IsDisposed) t.Document.Visible = false;
                console.Write(0, "Documento fechado: " + t.Title);
            };
            center.Controls.Add(tabStrip);
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

            miNav = DropItem("Painel de navegação", delegate { TogglePanel(0); });
            miProps = DropItem("Painel de propriedades", delegate { TogglePanel(1); });
            miConsole = DropItem("Painel de saída", delegate { TogglePanel(2); });
            miNav.Checked = true;
            miProps.Checked = true;
            miConsole.Checked = true;

            ToolStripMenuItem exibir = MenuItem("Exibir");
            exibir.DropDownItems.Add(miNav);
            exibir.DropDownItems.Add(miProps);
            exibir.DropDownItems.Add(miConsole);

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
            menu.Items.Add(exibir);
            menu.Items.Add(plc);
            menu.Items.Add(ferramentas);
            menu.Items.Add(ajuda);
            return menu;
        }

        private int toolCursor;

        private void AddToolButton(Control bar, string text, StudioIcon icon, bool emphasis, EventHandler action)
        {
            IconToolButton b = new IconToolButton();
            b.Text = text;
            b.Icon = icon;
            b.Emphasis = emphasis;
            b.Height = 54;
            b.Width = b.MeasureWidth();
            b.Location = new Point(toolCursor, 3);
            if (action != null) b.Click += action;
            bar.Controls.Add(b);
            toolCursor += b.Width;
        }

        private void AddToolSeparator(Control bar)
        {
            Panel sep = new Panel();
            sep.BackColor = Border;
            sep.Bounds = new Rectangle(toolCursor + 7, 15, 1, 30);
            bar.Controls.Add(sep);
            toolCursor += 15;
        }

        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 60;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            Label brand = new Label();
            brand.Text = "OpenLadder Studio   v0.11";
            brand.Dock = DockStyle.Right;
            brand.Width = 210;
            brand.TextAlign = ContentAlignment.MiddleRight;
            brand.ForeColor = Muted;
            brand.Font = StudioTheme.Ui;
            brand.Padding = new Padding(0, 0, 16, 0);
            bar.Controls.Add(brand);

            toolCursor = 10;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Rung", StudioIcon.Plus, false, delegate { InvokeLadder("AddRung", null); });
            AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Comunicação", StudioIcon.Plug, true, delegate { ShowBridge(); });
            AddToolButton(bar, "Ler PLC", StudioIcon.Download, false, delegate { ShowReader(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Atualizar", StudioIcon.Refresh, false, delegate { ShowUpdater(); });
            return bar;
        }

        private NavButton NavItem(string text, StudioIcon icon, string key, EventHandler action)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Key = key;
            if (action != null) b.Click += action;
            navButtons.Add(b);
            return b;
        }

        private Control BuildBrand()
        {
            StudioPanel brand = new StudioPanel();
            brand.Dock = DockStyle.Top;
            brand.Height = 64;
            brand.Fill = StudioTheme.NavBg;
            brand.BottomLine = Border;
            brand.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                using (SolidBrush b = new SolidBrush(Accent))
                    g.FillRectangle(b, new Rectangle(18, 18, 28, 28));
                StudioGlyph.Draw(g, StudioIcon.Ladder, new Rectangle(22, 22, 20, 20), Color.White);
                TextRenderer.DrawText(g, "OpenLadder", new Font("Segoe UI Semibold", 11.0f, FontStyle.Bold),
                    new Point(56, 16), Fore);
                TextRenderer.DrawText(g, "Studio  •  WEG TP02", StudioTheme.Small, new Point(58, 37), Muted);
            };
            return brand;
        }

        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 228;
            nav.BackColor = StudioTheme.NavBg;

            List<Control> items = new List<Control>();
            items.Add(BuildBrand());
            items.Add(new NavSection("Editor ladder"));
            items.Add(NavItem("Editor Ladder", StudioIcon.Ladder, "ladder", delegate { ShowLadder(); }));
            items.Add(NavItem("Validar projeto", StudioIcon.Check, "", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));
            items.Add(new NavSection("TP02 bridge"));
            items.Add(NavItem("Comunicação TP02", StudioIcon.Plug, "bridge", delegate { ShowBridge(); }));
            items.Add(NavItem("Ler programa (RBP)", StudioIcon.Download, "reader", delegate { ShowReader(); }));
            items.Add(new NavSection("Análise PC12"));
            items.Add(NavItem("Decodificador", StudioIcon.Chip, "decoder", delegate { ShowDecoder(); }));
            items.Add(NavItem("Calibração", StudioIcon.Gear, "calibration", delegate { ShowCalibration(); }));
            items.Add(NavItem("IL para Ladder", StudioIcon.Convert, "il", delegate { ShowIl(); }));
            items.Add(new NavSection("Sistema"));
            items.Add(NavItem("Atualizações", StudioIcon.Refresh, "updater", delegate { ShowUpdater(); }));

            // Filhos ancorados ao topo empilham do ultimo para o primeiro:
            // insere na ordem inversa para que a lista acima seja a ordem visual.
            int i;
            for (i = items.Count - 1; i >= 0; i--) nav.Controls.Add(items[i]);
            return nav;
        }

        private StudioPanel BuildConsole()
        {
            StudioPanel wrap = new StudioPanel();
            wrap.Dock = DockStyle.Bottom;
            wrap.Height = 156;
            wrap.Fill = Color.FromArgb(22, 24, 27);

            console = new StudioConsole();
            console.Dock = DockStyle.Fill;
            wrap.Controls.Add(console);

            StudioPanel head = new StudioPanel();
            head.Dock = DockStyle.Top;
            head.Height = 27;
            head.Fill = Chrome;
            head.BottomLine = Border;
            head.Paint += delegate(object sender, PaintEventArgs e)
            {
                StudioGlyph.Draw(e.Graphics, StudioIcon.Terminal, new Rectangle(12, 6, 14, 14), Muted);
                TextRenderer.DrawText(e.Graphics, "SAÍDA", StudioTheme.Section, new Point(34, 8), Muted);
            };
            wrap.Controls.Add(head);

            Button clear = new Button();
            clear.Text = "Limpar";
            clear.Dock = DockStyle.Right;
            clear.Width = 74;
            clear.FlatStyle = FlatStyle.Flat;
            clear.FlatAppearance.BorderSize = 0;
            clear.BackColor = Chrome;
            clear.ForeColor = Muted;
            clear.Font = StudioTheme.Small;
            clear.Cursor = Cursors.Hand;
            clear.TabStop = false;
            clear.Click += delegate { console.Items.Clear(); };
            head.Controls.Add(clear);

            return wrap;
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

        private Control BuildStatusBar()
        {
            StudioPanel p = new StudioPanel();
            p.Dock = DockStyle.Bottom;
            p.Height = 26;
            p.Fill = Color.FromArgb(25, 27, 30);

            statusText = new Label();
            statusText.Dock = DockStyle.Fill;
            statusText.TextAlign = ContentAlignment.MiddleLeft;
            statusText.Text = "Pronto";
            statusText.ForeColor = Muted;
            statusText.Font = StudioTheme.Small;
            p.Controls.Add(statusText);

            modeText = new Label();
            modeText.Dock = DockStyle.Right;
            modeText.Width = 340;
            modeText.TextAlign = ContentAlignment.MiddleRight;
            modeText.Text = "TP02-60MR    |    OFFLINE    |    v0.11";
            modeText.ForeColor = Muted;
            modeText.Font = StudioTheme.Small;
            modeText.Padding = new Padding(0, 0, 12, 0);
            p.Controls.Add(modeText);

            StudioPanel dot = new StudioPanel();
            dot.Dock = DockStyle.Left;
            dot.Width = 26;
            dot.Fill = p.Fill;
            dot.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(Accent))
                    e.Graphics.FillEllipse(b, 11, 10, 7, 7);
            };
            p.Controls.Add(dot);
            return p;
        }

        private void ShowLadder()
        {
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                ladderForm = new LadderEditorForm();
                PrepareLadderForStudio(ladderForm);
            }
            ShowDocument(ladderForm, "Programa Ladder", "ladder", StudioIcon.Ladder, "Editor Ladder");
            UpdateProjectName();
        }

        private void ShowBridge()
        {
            if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
            ShowDocument(bridgeForm, "Comunicação TP02", "bridge", StudioIcon.Plug, "Comunicação TP02");
        }

        private void ShowReader()
        {
            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            ShowDocument(readerForm, "Leitura RBP", "reader", StudioIcon.Download, "Leitura do programa");
        }

        private void ShowDecoder()
        {
            if (decoderForm == null || decoderForm.IsDisposed) decoderForm = new TP02AutoDecoderForm();
            ShowDocument(decoderForm, "Decodificador", "decoder", StudioIcon.Chip, "Decodificação offline");
        }

        private void ShowCalibration()
        {
            if (calibrationForm == null || calibrationForm.IsDisposed) calibrationForm = new TP02CalibrationCampaignForm();
            ShowDocument(calibrationForm, "Calibração", "calibration", StudioIcon.Gear, "Calibração de opcodes");
        }

        private void ShowIl()
        {
            if (ilForm == null || ilForm.IsDisposed) ilForm = new TP02IlToLadderForm();
            ShowDocument(ilForm, "IL → Ladder", "il", StudioIcon.Convert, "Reconstrução Ladder");
        }

        private void ShowUpdater()
        {
            if (updaterForm == null || updaterForm.IsDisposed) updaterForm = new PC12UpdaterForm();
            ShowDocument(updaterForm, "Atualizações", "updater", StudioIcon.Refresh, "Atualizações do OpenLadder Studio");
        }

        /// <summary>
        /// Abre (ou reativa) um documento. Os formularios permanecem no host e apenas
        /// alternam a visibilidade, para que as abas mantenham o estado de cada um.
        /// </summary>
        private void ShowDocument(Form child, string title, string key, StudioIcon icon, string status)
        {
            if (child.Parent != host)
            {
                child.TopLevel = false;
                child.FormBorderStyle = FormBorderStyle.None;
                child.Dock = DockStyle.Fill;
                host.Controls.Add(child);
                console.Write(0, "Documento aberto: " + title);
            }

            StudioTab tab = tabStrip.Find(key);
            if (tab == null)
            {
                tab = new StudioTab();
                tab.Key = key;
                tab.Title = title;
                tab.Icon = icon;
                tab.Status = status;
                tab.Closable = key != "ladder";
                tab.Document = child;
                tabStrip.Open(tab);
            }
            else
            {
                tabStrip.SelectKey(key);
            }
            ApplySelectedTab();
        }

        private void ApplySelectedTab()
        {
            if (tabStrip == null || host == null) return;
            StudioTab tab = tabStrip.Selected;

            int i;
            for (i = 0; i < host.Controls.Count; i++)
                host.Controls[i].Visible = tab != null && host.Controls[i] == tab.Document;

            SelectNav(tab == null ? "" : tab.Key);
            if (inspector != null) inspector.Visible = inspectorAllowed && tab != null && tab.Key == "ladder";
            if (tab == null) return;

            if (!tab.Document.IsDisposed)
            {
                tab.Document.Show();
                tab.Document.BringToFront();
            }
            statusText.Text = tab.Status;
        }

        private void TogglePanel(int which)
        {
            if (which == 0)
            {
                navPanel.Visible = !navPanel.Visible;
                miNav.Checked = navPanel.Visible;
            }
            else if (which == 1)
            {
                // O inspetor pertence ao editor ladder; o menu guarda a preferência.
                inspectorAllowed = !inspectorAllowed;
                miProps.Checked = inspectorAllowed;
                ApplySelectedTab();
            }
            else
            {
                consolePanel.Visible = !consolePanel.Visible;
                miConsole.Checked = consolePanel.Visible;
            }
        }

        private void SelectNav(string key)
        {
            int i;
            for (i = 0; i < navButtons.Count; i++)
            {
                bool active = navButtons[i].Key.Length > 0 && navButtons[i].Key == key;
                if (navButtons[i].Active == active) continue;
                navButtons[i].Active = active;
                navButtons[i].Invalidate();
            }
        }

        private void PrepareLadderForStudio(LadderEditorForm form)
        {
            form.BackColor = Workspace;
            foreach (Control c in form.Controls)
            {
                Panel p = c as Panel;
                if (p == null) continue;
                // O cabecalho e a barra de comandos do editor sao redundantes dentro do
                // estudio. A faixa inferior fica visivel: ela mostra rung, coluna,
                // contagem de redes e a ferramenta ativa do proprio editor.
                if (p.Dock == DockStyle.Top && (p.Height == 64 || p.Height == 58)) p.Visible = false;
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
                if (console != null) console.Write(0, "Comando do editor: " + methodName);
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
