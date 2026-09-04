using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class UniversalStudioProgram
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UniversalStudioForm());
        }
    }

    internal sealed class UniversalStudioColorTable : ProfessionalColorTable
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

    internal sealed class UniversalStudioForm : Form
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
        private readonly Color Disabled = Color.FromArgb(92, 97, 103);

        private Panel host;
        private Panel inspector;
        private Label statusText;
        private Label modeText;
        private Label projectValue;
        private Label deviceValue;
        private Label familyValue;
        private Label protocolValue;
        private Label supportValue;
        private Label capabilityValue;
        private Label connectionValue;
        private readonly Dictionary<string, NavButton> navButtons = new Dictionary<string, NavButton>();
        private Panel navPanel;
        private DocTabStrip tabStrip;
        private StudioConsole console;
        private StudioPanel consolePanel;
        private bool inspectorAllowed = true;
        private ToolStripMenuItem miNav;
        private ToolStripMenuItem miProps;
        private ToolStripMenuItem miConsole;

        private PlcDeviceProfile currentProfile;
        private IPlcDriver currentDriver;

        private LadderEditorForm ladderForm;
        private TP02BridgeForm bridgeForm;
        private TP02ProgramReaderForm readerForm;
        private TP02AutoDecoderForm decoderForm;
        private TP02CalibrationCampaignForm calibrationForm;
        private TP02IlToLadderForm ilForm;
        private PC12UpdaterForm updaterForm;
        private ModbusMonitorForm modbusForm;

        public UniversalStudioForm()
        {
            Text = "OpenLadder Studio";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            Size = new Size(1500, 900);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            currentProfile = PlcProfileStore.Load();
            currentDriver = currentProfile == null ? null : PlcDriverRegistry.FindDriver(currentProfile.DriverId);

            BuildUi();
            RefreshProfileUi();
            ShowLadder();
        }

        // A ancoragem e resolvida do ultimo filho para o primeiro: quem entra por
        // ultimo escolhe seu espaco antes e fica na borda externa. BringToFront()
        // move o controle para o indice 0, ou seja, para o FIM dessa fila; aplicado
        // as barras, ele fazia o painel Fill ocupar toda a area antes e as barras
        // passavam a ser desenhadas por cima do conteudo.
        // A ancoragem e resolvida do ultimo filho para o primeiro: quem entra por
        // ultimo escolhe seu espaco antes e fica na borda externa. O painel Fill
        // precisa entrar primeiro.
        private void BuildUi()
        {
            Panel workspace = new Panel();
            workspace.Dock = DockStyle.Fill;
            workspace.BackColor = Shell;
            Controls.Add(workspace);
            workspace.BringToFront();

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
            menu.Renderer = new ToolStripProfessionalRenderer(new UniversalStudioColorTable());

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
            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(new ToolStripSeparator());
            plc.DropDownItems.Add(DropItem("Comunicação", delegate { ShowCommunication(); }));
            plc.DropDownItems.Add(DropItem("Monitor online", delegate { ShowMonitor(); }));
            plc.DropDownItems.Add(DropItem("Ler programa", delegate { ShowReader(); }));

            ToolStripMenuItem ferramentas = MenuItem("Ferramentas");
            ferramentas.DropDownItems.Add(DropItem("Verificar portabilidade do Ladder", delegate { CheckPortability(); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());
            ferramentas.DropDownItems.Add(DropItem("Decodificador TP02", delegate { ShowDecoder(); }));
            ferramentas.DropDownItems.Add(DropItem("Calibração TP02", delegate { ShowCalibration(); }));
            ferramentas.DropDownItems.Add(DropItem("IL para Ladder", delegate { ShowIl(); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());
            ferramentas.DropDownItems.Add(DropItem("Atualizações", delegate { ShowUpdater(); }));

            ToolStripMenuItem ajuda = MenuItem("Ajuda");
            ajuda.DropDownItems.Add(DropItem("Sobre o OpenLadder Studio", delegate
            {
                MessageBox.Show(this,
                    "OpenLadder Studio v0.12\r\n\r\nAmbiente Ladder com arquitetura multi-fabricante.\r\nWEG TP02: driver nativo em evolução.\r\nModbus RTU/TCP: monitoramento genérico em leitura.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            brand.Text = "OpenLadder Studio  v0.12";
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
            AddToolButton(bar, "Controlador", StudioIcon.Chip, true, delegate { ShowDeviceManager(); });
            AddToolButton(bar, "Comunicação", StudioIcon.Plug, false, delegate { ShowCommunication(); });
            AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            AddToolButton(bar, "Portabilidade", StudioIcon.Folder, false, delegate { CheckPortability(); });
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
            if (key.Length > 0) navButtons[key] = b;
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
                string sub = currentProfile == null
                    ? "Studio"
                    : "Studio  •  " + currentProfile.Model;
                TextRenderer.DrawText(g, sub, StudioTheme.Small, new Point(58, 37), Muted);
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
            items.Add(NavItem("Editor Ladder", StudioIcon.Ladder, "LD", delegate { ShowLadder(); }));
            items.Add(NavItem("Validar projeto", StudioIcon.Check, "", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));
            items.Add(new NavSection("Controlador"));
            items.Add(NavItem("Selecionar controlador", StudioIcon.Chip, "DEV", delegate { ShowDeviceManager(); }));
            items.Add(new NavSection("Comunicação"));
            items.Add(NavItem("Comunicação", StudioIcon.Plug, "PLC", delegate { ShowCommunication(); }));
            items.Add(NavItem("Monitor online", StudioIcon.Monitor, "MON", delegate { ShowMonitor(); }));
            items.Add(NavItem("Ler programa (RBP)", StudioIcon.Download, "RBP", delegate { ShowReader(); }));
            items.Add(new NavSection("Análise TP02"));
            items.Add(NavItem("Decodificador", StudioIcon.Bolt, "DEC", delegate { ShowDecoder(); }));
            items.Add(NavItem("Calibração", StudioIcon.Gear, "CAL", delegate { ShowCalibration(); }));
            items.Add(NavItem("IL para Ladder", StudioIcon.Convert, "IL", delegate { ShowIl(); }));
            items.Add(new NavSection("Sistema"));
            items.Add(NavItem("Atualizações", StudioIcon.Refresh, "UPD", delegate { ShowUpdater(); }));

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
            wrap.Height = 150;
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
            p.Width = 270;
            p.BackColor = Chrome;
            p.Padding = new Padding(16, 12, 16, 12);

            Label title = InspectorLabel("PROJETO E CONTROLADOR", 9.0f, true, Muted);
            title.Location = new Point(16, 14);
            p.Controls.Add(title);

            Label project = InspectorLabel("Projeto", 8.2f, false, Muted);
            project.Location = new Point(16, 50);
            p.Controls.Add(project);

            projectValue = InspectorLabel("Sem nome", 9.2f, true, Fore);
            projectValue.Location = new Point(16, 70);
            projectValue.MaximumSize = new Size(235, 24);
            p.Controls.Add(projectValue);

            AddDivider(p, 104, 238);

            Label device = InspectorLabel("Controlador ativo", 8.2f, false, Muted);
            device.Location = new Point(16, 120);
            p.Controls.Add(device);

            deviceValue = InspectorLabel("-", 9.5f, true, Fore);
            deviceValue.Location = new Point(16, 140);
            deviceValue.MaximumSize = new Size(235, 40);
            p.Controls.Add(deviceValue);

            familyValue = InspectorLabel("-", 8.3f, false, Muted);
            familyValue.Location = new Point(16, 174);
            familyValue.MaximumSize = new Size(235, 34);
            p.Controls.Add(familyValue);

            protocolValue = InspectorLabel("-", 8.3f, false, Muted);
            protocolValue.Location = new Point(16, 198);
            protocolValue.MaximumSize = new Size(235, 34);
            p.Controls.Add(protocolValue);

            supportValue = InspectorLabel("-", 8.4f, true, Muted);
            supportValue.Location = new Point(16, 224);
            p.Controls.Add(supportValue);

            AddDivider(p, 254, 238);

            Label capabilities = InspectorLabel("RECURSOS DO DRIVER", 8.2f, true, Muted);
            capabilities.Location = new Point(16, 270);
            p.Controls.Add(capabilities);

            capabilityValue = InspectorLabel("-", 8.4f, false, Fore);
            capabilityValue.Location = new Point(16, 294);
            capabilityValue.MaximumSize = new Size(235, 82);
            p.Controls.Add(capabilityValue);

            AddDivider(p, 382, 238);

            Label status = InspectorLabel("CONEXÃO", 8.2f, true, Muted);
            status.Location = new Point(16, 398);
            p.Controls.Add(status);

            connectionValue = InspectorLabel("●  OFFLINE", 9.2f, true, Color.FromArgb(168, 174, 181));
            connectionValue.Location = new Point(16, 422);
            p.Controls.Add(connectionValue);

            Button connect = InspectorButton("Abrir comunicação", 16, 458, 238);
            connect.Click += delegate { ShowCommunication(); };
            p.Controls.Add(connect);

            Button change = InspectorButton("Trocar controlador", 16, 500, 238);
            change.Click += delegate { ShowDeviceManager(); };
            p.Controls.Add(change);

            return p;
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
            modeText.Width = 470;
            modeText.TextAlign = ContentAlignment.MiddleRight;
            modeText.ForeColor = Muted;
            modeText.Font = new Font("Segoe UI", 8.2f);
            p.Controls.Add(modeText);
            return p;
        }

        private void RefreshProfileUi()
        {
            currentProfile = PlcProfileStore.Load();
            currentDriver = currentProfile == null ? null : PlcDriverRegistry.FindDriver(currentProfile.DriverId);

            if (deviceValue != null)
                deviceValue.Text = currentProfile == null ? "Nenhum controlador" : currentProfile.Manufacturer + " " + currentProfile.Model;
            if (familyValue != null)
                familyValue.Text = currentProfile == null ? "-" : currentProfile.Family;
            if (protocolValue != null)
                protocolValue.Text = currentProfile == null ? "-" : currentProfile.Protocol;
            if (supportValue != null)
            {
                supportValue.Text = currentProfile == null ? "Sem perfil" : SupportText(currentProfile.SupportLevel);
                supportValue.ForeColor = currentProfile == null ? Muted : SupportColor(currentProfile.SupportLevel);
            }
            if (capabilityValue != null)
                capabilityValue.Text = currentDriver == null ? "Driver não disponível." : currentDriver.Capabilities.Summary();
            if (modeText != null)
            {
                string model = currentProfile == null ? "SEM PLC" : currentProfile.Model;
                string protocol = currentProfile == null ? "-" : currentProfile.Protocol;
                modeText.Text = model + "    |    " + protocol + "    |    OFFLINE    |    v0.12";
            }

            UpdateRailCapabilities();
        }

        private void UpdateRailCapabilities()
        {
            SetRailEnabled("LD", true);
            SetRailEnabled("DEV", true);
            SetRailEnabled("UPD", true);

            bool connect = currentDriver != null && currentDriver.Capabilities.Connect;
            bool monitor = currentDriver != null && (currentDriver.Capabilities.MonitorBits || currentDriver.Capabilities.ReadRegisters);
            bool readProgram = currentDriver != null && currentDriver.Capabilities.ReadProgram;
            bool tp02 = IsTp02();

            SetRailEnabled("PLC", connect);
            SetRailEnabled("MON", monitor);
            SetRailEnabled("RBP", readProgram);
            SetRailEnabled("DEC", tp02);
            SetRailEnabled("CAL", tp02);
            SetRailEnabled("IL", tp02);
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
            statusText.Text = "Editor Ladder universal";
            UpdateProjectName();
        }

        private void ShowDeviceManager()
        {
            using (PlcDeviceManagerForm dialog = new PlcDeviceManagerForm())
            {
                dialog.ShowDialog(this);
            }
            RefreshProfileUi();
            statusText.Text = currentProfile == null ? "Nenhum controlador selecionado" : "Controlador ativo: " + currentProfile.Manufacturer + " " + currentProfile.Model;
        }

        private void ShowCommunication()
        {
            RefreshProfileUi();
            if (currentDriver == null || !currentDriver.Capabilities.Connect)
            {
                MessageBox.Show(this, "O controlador selecionado ainda não possui driver de comunicação ativo.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowDeviceManager();
                return;
            }

            if (IsTp02())
            {
                if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
                inspector.Visible = false;
                ShowDocument(bridgeForm, "Comunicação - WEG TP02", "PLC");
                statusText.Text = "Driver WEG TP02";
                return;
            }

            if (IsGenericModbus())
            {
                ShowModbus("PLC");
                return;
            }

            MessageBox.Show(this, "O perfil está cadastrado, mas a tela de comunicação deste fabricante ainda não foi implementada.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowMonitor()
        {
            RefreshProfileUi();
            if (currentDriver == null || (!currentDriver.Capabilities.MonitorBits && !currentDriver.Capabilities.ReadRegisters))
            {
                MessageBox.Show(this, "O driver selecionado ainda não oferece monitoramento.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (IsGenericModbus())
            {
                ShowModbus("MON");
                return;
            }

            if (IsTp02())
            {
                if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
                inspector.Visible = false;
                ShowDocument(bridgeForm, "Monitor e diagnóstico - WEG TP02", "MON");
                statusText.Text = "Monitoramento TP02 em modo de leitura";
                return;
            }

            MessageBox.Show(this, "Monitoramento ainda não implementado para este driver.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowModbus(string railCode)
        {
            if (modbusForm == null || modbusForm.IsDisposed) modbusForm = new ModbusMonitorForm();
            PrepareModbusForProfile(modbusForm);
            inspector.Visible = false;
            ShowDocument(modbusForm, "Monitor Modbus RTU/TCP", railCode);
            statusText.Text = currentProfile == null ? "Monitor Modbus" : "Monitor " + currentProfile.Protocol;
        }

        private void PrepareModbusForProfile(ModbusMonitorForm form)
        {
            if (form == null || currentProfile == null) return;
            try
            {
                FieldInfo field = typeof(ModbusMonitorForm).GetField("transportCombo", BindingFlags.Instance | BindingFlags.NonPublic);
                ComboBox combo = field == null ? null : field.GetValue(form) as ComboBox;
                if (combo != null)
                {
                    if (string.Equals(currentProfile.DriverId, "generic.modbus.tcp", StringComparison.OrdinalIgnoreCase)) combo.SelectedIndex = 1;
                    else if (string.Equals(currentProfile.DriverId, "generic.modbus.rtu", StringComparison.OrdinalIgnoreCase)) combo.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void ShowReader()
        {
            RefreshProfileUi();
            if (currentDriver == null || !currentDriver.Capabilities.ReadProgram)
            {
                MessageBox.Show(this, "Leitura do programa não está disponível para o controlador selecionado.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!IsTp02())
            {
                MessageBox.Show(this, "O driver informa capacidade de leitura, mas ainda não existe uma tela de leitura integrada para esta família.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            inspector.Visible = false;
            ShowDocument(readerForm, "Leitura do programa - WEG TP02", "RBP");
            statusText.Text = "Leitura RBP do TP02";
        }

        private void ShowDecoder()
        {
            if (!RequireTp02("O decodificador atual trabalha com a linguagem de máquina RBP do WEG TP02.")) return;
            if (decoderForm == null || decoderForm.IsDisposed) decoderForm = new TP02AutoDecoderForm();
            inspector.Visible = false;
            ShowDocument(decoderForm, "Decodificador TP02", "DEC");
            statusText.Text = "Decodificação TP02";
        }

        private void ShowCalibration()
        {
            if (!RequireTp02("A calibração de opcodes atual é específica do WEG TP02.")) return;
            if (calibrationForm == null || calibrationForm.IsDisposed) calibrationForm = new TP02CalibrationCampaignForm();
            inspector.Visible = false;
            ShowDocument(calibrationForm, "Calibração de opcodes TP02", "CAL");
            statusText.Text = "Calibração TP02";
        }

        private void ShowIl()
        {
            if (!RequireTp02("O conversor IL atual usa a representação pesquisada para o WEG TP02.")) return;
            if (ilForm == null || ilForm.IsDisposed) ilForm = new TP02IlToLadderForm();
            inspector.Visible = false;
            ShowDocument(ilForm, "IL para Ladder - TP02", "IL");
            statusText.Text = "Reconstrução Ladder TP02";
        }

        private void ShowUpdater()
        {
            if (updaterForm == null || updaterForm.IsDisposed) updaterForm = new PC12UpdaterForm();
            inspector.Visible = false;
            ShowDocument(updaterForm, "Atualizações", "UPD");
            statusText.Text = "Atualizações do OpenLadder Studio";
        }

        private void CheckPortability()
        {
            ShowLadder();
            UniversalLadderConversionReport report = UniversalLadderAdapter.FromEditor(ladderForm);
            string text = UniversalLadderAdapter.CheckTarget(report, currentProfile);
            MessageBox.Show(this, text, "Portabilidade do projeto Ladder", MessageBoxButtons.OK,
                report.UnsupportedCount == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            statusText.Text = "Modelo Ladder universal verificado";
        }

        private bool RequireTp02(string explanation)
        {
            RefreshProfileUi();
            if (IsTp02()) return true;
            MessageBox.Show(this, explanation + "\r\n\r\nControlador ativo: " + (currentProfile == null ? "nenhum" : currentProfile.Manufacturer + " " + currentProfile.Model) + ".",
                "Recurso específico de fabricante", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private bool IsTp02()
        {
            return currentProfile != null && string.Equals(currentProfile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsGenericModbus()
        {
            if (currentProfile == null) return false;
            return string.Equals(currentProfile.DriverId, "generic.modbus.rtu", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentProfile.DriverId, "generic.modbus.tcp", StringComparison.OrdinalIgnoreCase);
        }

        private static StudioIcon IconFor(string railCode)
        {
            switch (railCode)
            {
                case "LD": return StudioIcon.Ladder;
                case "DEV": return StudioIcon.Chip;
                case "PLC": return StudioIcon.Plug;
                case "MON": return StudioIcon.Monitor;
                case "RBP": return StudioIcon.Download;
                case "DEC": return StudioIcon.Bolt;
                case "CAL": return StudioIcon.Gear;
                case "IL": return StudioIcon.Convert;
                case "UPD": return StudioIcon.Refresh;
            }
            return StudioIcon.Doc;
        }

        /// <summary>
        /// Abre (ou reativa) um documento. Os formularios permanecem no host e apenas
        /// alternam a visibilidade, para que cada aba preserve o proprio estado.
        /// </summary>
        private void ShowDocument(Form child, string title, string railCode)
        {
            if (child.Parent != host)
            {
                child.TopLevel = false;
                child.FormBorderStyle = FormBorderStyle.None;
                child.Dock = DockStyle.Fill;
                host.Controls.Add(child);
                console.Write(0, "Documento aberto: " + title);
            }

            StudioTab tab = tabStrip.Find(railCode);
            if (tab == null)
            {
                tab = new StudioTab();
                tab.Key = railCode;
                tab.Title = title;
                tab.Icon = IconFor(railCode);
                tab.Status = title;
                tab.Closable = railCode != "LD";
                tab.Document = child;
                tabStrip.Open(tab);
            }
            else
            {
                tabStrip.SelectKey(railCode);
            }
            ApplySelectedTab();
        }

        private StudioTab lastTab;

        private void ApplySelectedTab()
        {
            if (tabStrip == null || host == null) return;
            StudioTab tab = tabStrip.Selected;

            // Guarda o texto de status na aba que sai, para restaura-lo ao voltar.
            if (lastTab != null && statusText != null) lastTab.Status = statusText.Text;

            int i;
            for (i = 0; i < host.Controls.Count; i++)
                host.Controls[i].Visible = tab != null && host.Controls[i] == tab.Document;

            SelectNav(tab == null ? "" : tab.Key);
            if (inspector != null) inspector.Visible = inspectorAllowed && tab != null && tab.Key == "LD";
            lastTab = tab;
            if (tab == null) return;

            if (!tab.Document.IsDisposed)
            {
                tab.Document.Show();
                tab.Document.BringToFront();
            }
            if (!string.IsNullOrEmpty(tab.Status)) statusText.Text = tab.Status;
        }

        private void SelectNav(string key)
        {
            foreach (KeyValuePair<string, NavButton> pair in navButtons)
            {
                bool active = pair.Key == key;
                if (pair.Value.Active == active) continue;
                pair.Value.Active = active;
                pair.Value.Invalidate();
            }
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
                if (label != null && label.Text != null && label.Text.Length > 90) label.Visible = false;

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
                Label label = field == null ? null : field.GetValue(ladderForm) as Label;
                string value = label == null ? string.Empty : (label.Text ?? string.Empty).Trim();
                projectValue.Text = string.IsNullOrEmpty(value) ? "Sem nome" : value;
            }
            catch
            {
                projectValue.Text = "Projeto Ladder";
            }
        }

        private void SetRailEnabled(string code, bool enabled)
        {
            NavButton b;
            if (!navButtons.TryGetValue(code, out b)) return;
            b.Enabled = enabled;
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

        private Button InspectorButton(string text, int left, int top, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Border;
            b.BackColor = ChromeLight;
            b.ForeColor = Fore;
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void AddDivider(Control parent, int top, int width)
        {
            Panel line = new Panel();
            line.Location = new Point(16, top);
            line.Size = new Size(width, 1);
            line.BackColor = Border;
            parent.Controls.Add(line);
        }

        private string SupportText(PlcSupportLevel level)
        {
            if (level == PlcSupportLevel.Implemented) return "Implementado";
            if (level == PlcSupportLevel.Experimental) return "Experimental";
            return "Planejado";
        }

        private Color SupportColor(PlcSupportLevel level)
        {
            if (level == PlcSupportLevel.Implemented) return Accent;
            if (level == PlcSupportLevel.Experimental) return Color.FromArgb(215, 166, 71);
            return Muted;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Form[] forms = new Form[] { ladderForm, bridgeForm, readerForm, decoderForm, calibrationForm, ilForm, updaterForm, modbusForm };
            for (int i = 0; i < forms.Length; i++)
                if (forms[i] != null && !forms[i].IsDisposed) forms[i].Dispose();
            base.OnFormClosing(e);
        }
    }
}
