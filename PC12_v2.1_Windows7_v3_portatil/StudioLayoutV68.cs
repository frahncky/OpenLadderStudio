using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class StudioProgramV68
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            UniversalStudioForm form = new UniversalStudioForm();
            StudioLayoutV68.Apply(form);
            Application.Run(form);
        }
    }

    internal static class StudioLayoutV68
    {
        private static readonly Color Navy = Color.FromArgb(18, 70, 126);
        private static readonly Color Blue = Color.FromArgb(28, 112, 220);
        private static readonly Color BlueSoft = Color.FromArgb(232, 242, 253);
        private static readonly Color Surface = Color.FromArgb(247, 249, 252);
        private static readonly Color Panel = Color.FromArgb(252, 253, 255);
        private static readonly Color Border = Color.FromArgb(207, 216, 226);
        private static readonly Color Text = Color.FromArgb(31, 43, 56);
        private static readonly Color Muted = Color.FromArgb(93, 108, 124);
        private static readonly Color Green = Color.FromArgb(31, 157, 85);
        private static readonly Color Amber = Color.FromArgb(222, 158, 43);
        private static readonly Color Red = Color.FromArgb(218, 68, 60);
        private static readonly Color Cyan = Color.FromArgb(25, 138, 184);

        private static Panel leftRail;
        private static Panel rightRail;
        private static TreeView projectTree;
        private static FlowLayoutPanel instructionFlow;
        private static TextBox instructionSearch;
        private static Label propertyProject;
        private static Label propertyController;

        public static void Apply(UniversalStudioForm form)
        {
            if (form == null) return;

            form.Text = "OpenLadder Studio";
            form.MinimumSize = new Size(1280, 720);
            form.Size = new Size(1600, 900);
            form.BackColor = Surface;
            form.ForeColor = Text;

            DisableLegacySidePanels(form);
            RebuildMenu(form);
            RebuildToolbar(form);
            RebuildWorkspace(form);
            RestyleConsole(form);
            RestyleStatus(form);
            HideEmbeddedLadderToolbox(form);
            RefreshProperties(form);

            form.Shown += delegate
            {
                HideEmbeddedLadderToolbox(form);
                RefreshProperties(form);
            };

            form.Activated += delegate
            {
                Label mode = Field<Label>(form, "modeText");
                if (mode != null && mode.Text != null)
                    mode.Text = mode.Text.Replace("v0.12", "v0.68");
            };
        }

        private static void DisableLegacySidePanels(UniversalStudioForm form)
        {
            Panel nav = Field<Panel>(form, "navPanel");
            Panel inspector = Field<Panel>(form, "inspector");

            if (nav != null)
            {
                nav.Visible = false;
                if (nav.Parent != null) nav.Parent.Controls.Remove(nav);
            }
            if (inspector != null)
            {
                inspector.Visible = false;
                if (inspector.Parent != null) inspector.Parent.Controls.Remove(inspector);
            }

            FieldInfo allowed = typeof(UniversalStudioForm).GetField("inspectorAllowed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (allowed != null) allowed.SetValue(form, false);

            ToolStripMenuItem miNav = Field<ToolStripMenuItem>(form, "miNav");
            ToolStripMenuItem miProps = Field<ToolStripMenuItem>(form, "miProps");
            if (miNav != null) miNav.Visible = false;
            if (miProps != null) miProps.Visible = false;
        }

        private static void RebuildMenu(final UniversalStudioForm form)
        {
        }

        private static void RebuildMenu(UniversalStudioForm form)
        {
            MenuStrip menu = null;
            foreach (Control c in form.Controls)
            {
                menu = c as MenuStrip;
                if (menu != null) break;
            }
            if (menu == null) return;

            menu.Items.Clear();
            menu.Height = 30;
            menu.Padding = new Padding(10, 2, 0, 2);
            menu.BackColor = Color.White;
            menu.ForeColor = Text;
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new ToolStripProfessionalRenderer(new V68ColorTable());

            ToolStripMenuItem arquivo = Menu("Arquivo");
            arquivo.DropDownItems.Add(Item("Novo projeto", delegate { InvokeLadder(form, "NewProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(Item("Abrir...", delegate { InvokeLadder(form, "OpenProject", null); }));
            arquivo.DropDownItems.Add(Item("Salvar", delegate { InvokeLadder(form, "SaveProject", new object[] { false }); }));
            arquivo.DropDownItems.Add(Item("Salvar como...", delegate { InvokeLadder(form, "SaveProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(new ToolStripSeparator());
            arquivo.DropDownItems.Add(Item("Sair", delegate { form.Close(); }));

            ToolStripMenuItem editar = Menu("Editar");
            editar.DropDownItems.Add(Item("Desfazer", delegate { InvokeLadder(form, "Undo", null); }));
            editar.DropDownItems.Add(Item("Adicionar rung", delegate { InvokeLadder(form, "AddRung", null); }));
            editar.DropDownItems.Add(Item("Excluir rung", delegate { InvokeLadder(form, "DeleteSelectedRung", null); }));

            ToolStripMenuItem exibir = Menu("Exibir");
            exibir.DropDownItems.Add(Item("Mensagens", delegate { SafeCall(form, "TogglePanel", new object[] { 2 }); }));
            exibir.DropDownItems.Add(Item("Editor Ladder", delegate { SafeCall(form, "ShowLadder", null); HideEmbeddedLadderToolbox(form); }));

            ToolStripMenuItem projeto = Menu("Projeto");
            projeto.DropDownItems.Add(Item("Validar / compilar", delegate { InvokeLadder(form, "ValidateProject", new object[] { true }); }));
            projeto.DropDownItems.Add(Item("Verificar portabilidade", delegate { SafeCall(form, "CheckPortability", null); }));

            ToolStripMenuItem plc = Menu("PLC");
            plc.DropDownItems.Add(Item("Selecionar controlador...", delegate { SafeCall(form, "ShowDeviceManager", null); RefreshProperties(form); }));
            plc.DropDownItems.Add(Item("Comunicação", delegate { SafeCall(form, "ShowCommunication", null); }));
            plc.DropDownItems.Add(Item("Monitor online", delegate { SafeCall(form, "ShowMonitor", null); }));
            plc.DropDownItems.Add(Item("Ler programa", delegate { SafeCall(form, "ShowReader", null); }));

            ToolStripMenuItem simulador = Menu("Simulador");
            simulador.DropDownItems.Add(Item("Simulação de processo", delegate { SafeCall(form, "ShowSimulator", null); }));

            ToolStripMenuItem ferramentas = Menu("Ferramentas");
            ferramentas.DropDownItems.Add(Item("Decodificador TP02", delegate { SafeCall(form, "ShowDecoder", null); }));
            ferramentas.DropDownItems.Add(Item("Calibração TP02", delegate { SafeCall(form, "ShowCalibration", null); }));
            ferramentas.DropDownItems.Add(Item("IL para Ladder", delegate { SafeCall(form, "ShowIl", null); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());
            ferramentas.DropDownItems.Add(Item("Atualizações", delegate { SafeCall(form, "ShowUpdater", null); }));

            ToolStripMenuItem janela = Menu("Janela");
            janela.DropDownItems.Add(Item("Mostrar editor principal", delegate { SafeCall(form, "ShowLadder", null); HideEmbeddedLadderToolbox(form); }));

            ToolStripMenuItem ajuda = Menu("Ajuda");
            ajuda.DropDownItems.Add(Item("Sobre o OpenLadder Studio", delegate
            {
                MessageBox.Show(form,
                    "OpenLadder Studio v0.68\r\n\r\nInterface industrial clara com editor Ladder, projeto, propriedades, mensagens e caixa de instruções integrados.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));

            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(exibir);
            menu.Items.Add(projeto);
            menu.Items.Add(plc);
            menu.Items.Add(simulador);
            menu.Items.Add(ferramentas);
            menu.Items.Add(janela);
            menu.Items.Add(ajuda);
        }

        private static void RebuildToolbar(UniversalStudioForm form)
        {
            StudioPanel toolbar = null;
            foreach (Control c in form.Controls)
            {
                StudioPanel p = c as StudioPanel;
                if (p != null && p.Dock == DockStyle.Top)
                {
                    toolbar = p;
                    break;
                }
            }
            if (toolbar == null) return;

            toolbar.Controls.Clear();
            toolbar.Height = 84;
            toolbar.Fill = Color.White;
            toolbar.BottomLine = Border;
            toolbar.BackColor = Color.White;

            V68BrandPanel brand = new V68BrandPanel();
            brand.Dock = DockStyle.Right;
            brand.Width = 315;
            toolbar.Controls.Add(brand);

            int x = 12;
            AddToolbar(toolbar, ref x, "Novo", StudioIcon.Doc, Blue, true, delegate { InvokeLadder(form, "NewProject", new object[] { true }); });
            AddToolbar(toolbar, ref x, "Abrir", StudioIcon.Folder, Amber, true, delegate { InvokeLadder(form, "OpenProject", null); });
            AddToolbar(toolbar, ref x, "Salvar", StudioIcon.Save, Navy, true, delegate { InvokeLadder(form, "SaveProject", new object[] { false }); });
            AddSeparator(toolbar, ref x);
            AddToolbar(toolbar, ref x, "Compilar", StudioIcon.Check, Green, true, delegate { InvokeLadder(form, "ValidateProject", new object[] { true }); });
            AddToolbar(toolbar, ref x, "Transferir", StudioIcon.Download, Blue, true, delegate { ExplainTransfer(form); });
            AddToolbar(toolbar, ref x, "Simulador", StudioIcon.Grid, Green, true, delegate { SafeCall(form, "ShowSimulator", null); });
            AddToolbar(toolbar, ref x, "Monitor", StudioIcon.Monitor, Cyan, true, delegate { SafeCall(form, "ShowMonitor", null); });
            AddToolbar(toolbar, ref x, "Diagnóstico", StudioIcon.Bolt, Red, true, delegate { SafeCall(form, "ShowCommunication", null); });
            AddSeparator(toolbar, ref x);
            AddToolbar(toolbar, ref x, "Desfazer", StudioIcon.Undo, Blue, true, delegate { InvokeLadder(form, "Undo", null); });
            AddToolbar(toolbar, ref x, "Refazer", StudioIcon.Refresh, Muted, false, null);
            AddSeparator(toolbar, ref x);
            AddToolbar(toolbar, ref x, "Zoom -", StudioIcon.Minus, Muted, false, null);
            AddToolbar(toolbar, ref x, "Zoom +", StudioIcon.Plus, Muted, false, null);
        }

        private static void RebuildWorkspace(UniversalStudioForm form)
        {
            Panel host = Field<Panel>(form, "host");
            DocTabStrip tabs = Field<DocTabStrip>(form, "tabStrip");
            StudioPanel consolePanel = Field<StudioPanel>(form, "consolePanel");
            if (host == null || host.Parent == null) return;

            Control center = host.Parent;
            Control workspace = center.Parent;
            if (workspace == null) return;

            workspace.BackColor = Surface;
            center.BackColor = Surface;
            host.BackColor = Color.White;
            host.Padding = new Padding(0);
            if (tabs != null)
            {
                tabs.BackColor = Color.White;
                tabs.Height = 38;
            }
            if (consolePanel != null) consolePanel.Height = 158;

            leftRail = BuildProjectRail(form);
            rightRail = BuildInstructionRail(form);

            workspace.Controls.Add(rightRail);
            workspace.Controls.Add(leftRail);
        }

        private static Panel BuildProjectRail(UniversalStudioForm form)
        {
            Panel rail = new Panel();
            rail.Dock = DockStyle.Left;
            rail.Width = 304;
            rail.BackColor = Panel;
            rail.Padding = new Padding(0);

            Panel header = HeaderPanel("Projeto", StudioIcon.Folder);
            header.Dock = DockStyle.Top;
            header.Height = 38;

            Panel properties = new Panel();
            properties.Dock = DockStyle.Bottom;
            properties.Height = 232;
            properties.BackColor = Color.White;
            properties.Padding = new Padding(12, 10, 12, 10);
            properties.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(Border)) e.Graphics.DrawLine(p, 0, 0, properties.Width, 0);
            };

            Label propTitle = new Label();
            propTitle.Text = "Propriedades";
            propTitle.Dock = DockStyle.Top;
            propTitle.Height = 26;
            propTitle.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
            propTitle.ForeColor = Text;
            propTitle.TextAlign = ContentAlignment.MiddleLeft;
            properties.Controls.Add(propTitle);

            Panel propBody = new Panel();
            propBody.Dock = DockStyle.Fill;
            propBody.BackColor = Color.White;
            properties.Controls.Add(propBody);
            propBody.BringToFront();

            propertyProject = AddPropertyRow(propBody, 4, "Nome", "Main");
            AddPropertyRow(propBody, 34, "Tipo", "Programa (PRG)");
            AddPropertyRow(propBody, 64, "Descrição", "Programa principal");
            AddPropertyRow(propBody, 94, "Linguagem", "Ladder (LD)");
            propertyController = AddPropertyRow(propBody, 124, "PLC", "-" );
            AddPropertyRow(propBody, 154, "Versão", "0.68");

            projectTree = new TreeView();
            projectTree.Dock = DockStyle.Fill;
            projectTree.BorderStyle = BorderStyle.None;
            projectTree.BackColor = Panel;
            projectTree.ForeColor = Text;
            projectTree.Font = new Font("Segoe UI", 9.1f);
            projectTree.FullRowSelect = true;
            projectTree.HideSelection = false;
            projectTree.ItemHeight = 26;
            projectTree.ShowLines = true;
            projectTree.ShowPlusMinus = true;
            projectTree.ShowRootLines = false;
            projectTree.ImageList = BuildTreeImages();

            TreeNode root = Node("Projeto1", "folder", "ROOT");
            root.Nodes.Add(Node("Configuração do PLC", "gear", "PLC_CONFIG"));

            TreeNode programs = Node("Programas", "folder", "PROGRAMS");
            programs.Nodes.Add(Node("Main (PRG)", "doc", "LD"));
            programs.Nodes.Add(Node("Funções (FUN)", "fx", "FUN"));
            programs.Nodes.Add(Node("Blocos de Função (FB)", "block", "FB"));
            root.Nodes.Add(programs);

            TreeNode vars = Node("Variáveis", "folder", "VARS");
            vars.Nodes.Add(Node("Globais", "globe", "GLOBAL"));
            vars.Nodes.Add(Node("Locais", "doc", "LOCAL"));
            vars.Nodes.Add(Node("Constantes", "pi", "CONST"));
            root.Nodes.Add(vars);
            root.Nodes.Add(Node("Bibliotecas", "library", "LIB"));

            projectTree.Nodes.Add(root);
            root.Expand();
            programs.Expand();
            vars.Expand();
            projectTree.SelectedNode = programs.Nodes[0];

            projectTree.NodeMouseDoubleClick += delegate(object sender, TreeNodeMouseClickEventArgs e)
            {
                string tag = e.Node == null ? string.Empty : Convert.ToString(e.Node.Tag);
                if (tag == "LD")
                {
                    SafeCall(form, "ShowLadder", null);
                    HideEmbeddedLadderToolbox(form);
                }
                else if (tag == "PLC_CONFIG")
                {
                    SafeCall(form, "ShowDeviceManager", null);
                    RefreshProperties(form);
                }
            };

            rail.Controls.Add(projectTree);
            rail.Controls.Add(properties);
            rail.Controls.Add(header);
            return rail;
        }

        private static Panel BuildInstructionRail(UniversalStudioForm form)
        {
            Panel rail = new Panel();
            rail.Dock = DockStyle.Right;
            rail.Width = 322;
            rail.BackColor = Panel;

            Panel header = HeaderPanel("Caixa de Instruções", StudioIcon.Ladder);
            header.Dock = DockStyle.Top;
            header.Height = 38;

            Panel searchWrap = new Panel();
            searchWrap.Dock = DockStyle.Top;
            searchWrap.Height = 48;
            searchWrap.BackColor = Panel;
            searchWrap.Padding = new Padding(10, 8, 10, 7);

            instructionSearch = new TextBox();
            instructionSearch.Dock = DockStyle.Fill;
            instructionSearch.BorderStyle = BorderStyle.FixedSingle;
            instructionSearch.Font = new Font("Segoe UI", 9.0f);
            instructionSearch.ForeColor = Muted;
            instructionSearch.Text = "Buscar instruções...";
            searchWrap.Controls.Add(instructionSearch);

            instructionFlow = new FlowLayoutPanel();
            instructionFlow.Dock = DockStyle.Fill;
            instructionFlow.FlowDirection = FlowDirection.TopDown;
            instructionFlow.WrapContents = false;
            instructionFlow.AutoScroll = true;
            instructionFlow.BackColor = Panel;
            instructionFlow.Padding = new Padding(8, 4, 6, 10);

            instructionSearch.GotFocus += delegate
            {
                if (instructionSearch.Text == "Buscar instruções...")
                {
                    instructionSearch.Text = string.Empty;
                    instructionSearch.ForeColor = Text;
                }
            };
            instructionSearch.LostFocus += delegate
            {
                if (string.IsNullOrWhiteSpace(instructionSearch.Text))
                {
                    instructionSearch.Text = "Buscar instruções...";
                    instructionSearch.ForeColor = Muted;
                }
            };
            instructionSearch.TextChanged += delegate
            {
                string filter = instructionSearch.Text == "Buscar instruções..." ? string.Empty : instructionSearch.Text;
                PopulateInstructions(form, filter);
            };

            rail.Controls.Add(instructionFlow);
            rail.Controls.Add(searchWrap);
            rail.Controls.Add(header);
            PopulateInstructions(form, string.Empty);
            return rail;
        }

        private static void PopulateInstructions(UniversalStudioForm form, string filter)
        {
            if (instructionFlow == null) return;
            instructionFlow.SuspendLayout();
            instructionFlow.Controls.Clear();

            List<V68InstructionSpec> specs = InstructionCatalog();
            string needle = (filter ?? string.Empty).Trim().ToLowerInvariant();
            string lastCategory = string.Empty;

            for (int i = 0; i < specs.Count; i++)
            {
                V68InstructionSpec spec = specs[i];
                if (needle.Length > 0 && spec.Text.ToLowerInvariant().IndexOf(needle) < 0 && spec.Category.ToLowerInvariant().IndexOf(needle) < 0)
                    continue;

                if (lastCategory != spec.Category)
                {
                    V68SectionLabel section = new V68SectionLabel();
                    section.Text = spec.Category;
                    section.Width = 292;
                    instructionFlow.Controls.Add(section);
                    lastCategory = spec.Category;
                }

                V68InstructionButton item = new V68InstructionButton();
                item.Text = spec.Text;
                item.Icon = spec.Icon;
                item.IconColor = spec.Enabled ? Blue : Color.FromArgb(150, 160, 170);
                item.Width = 292;
                item.Enabled = spec.Enabled;
                if (spec.Enabled && spec.Tool.HasValue)
                {
                    LadderTool tool = spec.Tool.Value;
                    item.Click += delegate { SetLadderTool(form, tool); };
                }
                instructionFlow.Controls.Add(item);
            }

            instructionFlow.ResumeLayout();
        }

        private static List<V68InstructionSpec> InstructionCatalog()
        {
            List<V68InstructionSpec> list = new List<V68InstructionSpec>();
            list.Add(new V68InstructionSpec("Contatos", "Contato NA", StudioIcon.Ladder, LadderTool.ContactNO, true));
            list.Add(new V68InstructionSpec("Contatos", "Contato NF", StudioIcon.Ladder, LadderTool.ContactNC, true));
            list.Add(new V68InstructionSpec("Contatos", "Contato de Borda (Subida)", StudioIcon.Bolt, LadderTool.EdgeUp, true));
            list.Add(new V68InstructionSpec("Contatos", "Contato de Borda (Descida)", StudioIcon.Bolt, LadderTool.EdgeDown, true));
            list.Add(new V68InstructionSpec("Bobinas", "Bobina", StudioIcon.Ladder, LadderTool.Coil, true));
            list.Add(new V68InstructionSpec("Bobinas", "Set (S)", StudioIcon.Plus, LadderTool.Set, true));
            list.Add(new V68InstructionSpec("Bobinas", "Reset (R)", StudioIcon.Minus, LadderTool.Reset, true));
            list.Add(new V68InstructionSpec("Temporizadores", "TON / TMR", StudioIcon.Grid, LadderTool.Timer, true));
            list.Add(new V68InstructionSpec("Temporizadores", "TOF (planejado)", StudioIcon.Grid, null, false));
            list.Add(new V68InstructionSpec("Temporizadores", "TP (planejado)", StudioIcon.Grid, null, false));
            list.Add(new V68InstructionSpec("Contadores", "CTU / CNT", StudioIcon.Grid, LadderTool.Counter, true));
            list.Add(new V68InstructionSpec("Contadores", "CTD (planejado)", StudioIcon.Grid, null, false));
            list.Add(new V68InstructionSpec("Contadores", "CTUD (planejado)", StudioIcon.Grid, null, false));
            list.Add(new V68InstructionSpec("Operações", "Função especial", StudioIcon.Gear, LadderTool.Function, true));
            list.Add(new V68InstructionSpec("Comparadores", "Comparadores (em evolução)", StudioIcon.Check, null, false));
            list.Add(new V68InstructionSpec("Conversão", "Conversão (em evolução)", StudioIcon.Convert, null, false));
            list.Add(new V68InstructionSpec("Movimentação", "Movimentação (em evolução)", StudioIcon.Download, null, false));
            list.Add(new V68InstructionSpec("Lógica", "Lógica (em evolução)", StudioIcon.Ladder, null, false));
            list.Add(new V68InstructionSpec("Matemática", "Matemática (em evolução)", StudioIcon.Plus, null, false));
            list.Add(new V68InstructionSpec("Outros", "END", StudioIcon.Terminal, LadderTool.End, true));
            list.Add(new V68InstructionSpec("Outros", "Apagar elemento", StudioIcon.Close, LadderTool.Erase, true));
            return list;
        }

        private static void RestyleConsole(UniversalStudioForm form)
        {
            StudioPanel wrap = Field<StudioPanel>(form, "consolePanel");
            StudioConsole console = Field<StudioConsole>(form, "console");
            if (wrap == null) return;

            wrap.Fill = Color.White;
            wrap.BottomLine = Border;
            wrap.BackColor = Color.White;
            wrap.Height = 158;

            foreach (Control c in wrap.Controls)
            {
                StudioPanel head = c as StudioPanel;
                if (head != null && head.Dock == DockStyle.Top)
                {
                    head.Controls.Clear();
                    head.Fill = Color.FromArgb(245, 248, 252);
                    head.BottomLine = Border;
                    head.Height = 34;

                    Label title = new Label();
                    title.Text = "Mensagens";
                    title.Dock = DockStyle.Left;
                    title.Width = 120;
                    title.Padding = new Padding(12, 0, 0, 0);
                    title.TextAlign = ContentAlignment.MiddleLeft;
                    title.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
                    title.ForeColor = Text;
                    head.Controls.Add(title);

                    Label filters = new Label();
                    filters.Text = "Todas   |   Erros   |   Avisos   |   Informações";
                    filters.Dock = DockStyle.Left;
                    filters.Width = 310;
                    filters.TextAlign = ContentAlignment.MiddleLeft;
                    filters.Font = new Font("Segoe UI", 8.4f);
                    filters.ForeColor = Muted;
                    head.Controls.Add(filters);
                    filters.BringToFront();
                    break;
                }
            }

            if (console != null)
            {
                console.BackColor = Color.White;
                console.ForeColor = Text;
                console.Font = new Font("Consolas", 8.5f);
            }
        }

        private static void RestyleStatus(UniversalStudioForm form)
        {
            Label status = Field<Label>(form, "statusText");
            Label mode = Field<Label>(form, "modeText");

            foreach (Control c in form.Controls)
            {
                Panel p = c as Panel;
                if (p == null || p.Dock != DockStyle.Bottom || p.Height > 32) continue;
                p.BackColor = Color.White;
                p.Padding = new Padding(12, 0, 12, 0);
                p.Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (Pen pen = new Pen(Border)) e.Graphics.DrawLine(pen, 0, 0, p.Width, 0);
                };
                break;
            }

            if (status != null)
            {
                status.Text = "●  Pronto";
                status.ForeColor = Green;
                status.Font = new Font("Segoe UI", 8.6f);
            }
            if (mode != null)
            {
                mode.Width = 610;
                mode.ForeColor = Muted;
                mode.Font = new Font("Segoe UI", 8.5f);
                mode.Text = (mode.Text ?? string.Empty).Replace("v0.12", "v0.68");
            }
        }

        private static void HideEmbeddedLadderToolbox(UniversalStudioForm form)
        {
            LadderEditorForm ladder = Field<LadderEditorForm>(form, "ladderForm");
            if (ladder == null || ladder.IsDisposed) return;

            ladder.BackColor = Surface;
            HideLadderSidebars(ladder);
        }

        private static void HideLadderSidebars(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Panel p = c as Panel;
                if (p != null)
                {
                    if (p.Dock == DockStyle.Left && p.Width >= 220 && p.Width <= 250)
                        p.Visible = false;
                    else if (p.Dock == DockStyle.Fill || p.Dock == DockStyle.None)
                        p.BackColor = p.BackColor == Color.FromArgb(18, 39, 63) ? Surface : p.BackColor;
                }
                if (c.HasChildren) HideLadderSidebars(c);
            }
        }

        private static void SetLadderTool(UniversalStudioForm form, LadderTool tool)
        {
            SafeCall(form, "ShowLadder", null);
            HideEmbeddedLadderToolbox(form);

            LadderEditorForm ladder = Field<LadderEditorForm>(form, "ladderForm");
            if (ladder == null) return;

            MethodInfo set = typeof(LadderEditorForm).GetMethod("SetActiveTool", BindingFlags.Instance | BindingFlags.NonPublic);
            if (set != null) set.Invoke(ladder, new object[] { tool });

            Label status = Field<Label>(form, "statusText");
            if (status != null)
            {
                status.Text = "Ferramenta Ladder: " + tool.ToString();
                status.ForeColor = Blue;
            }
        }

        private static void InvokeLadder(UniversalStudioForm form, string methodName, object[] args)
        {
            SafeCall(form, "InvokeLadder", new object[] { methodName, args });
            HideEmbeddedLadderToolbox(form);
            RefreshProperties(form);
        }

        private static void ExplainTransfer(UniversalStudioForm form)
        {
            MessageBox.Show(form,
                "A interface de transferência está preparada, mas o download de programa para PLC físico continua bloqueado enquanto o driver do controlador não declarar essa capacidade de forma validada.\r\n\r\nUse o Simulador para executar o Ladder localmente.",
                "Transferir para PLC", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void RefreshProperties(UniversalStudioForm form)
        {
            if (propertyProject != null)
            {
                Label project = Field<Label>(form, "projectValue");
                string text = project == null ? string.Empty : (project.Text ?? string.Empty).Trim();
                propertyProject.Text = string.IsNullOrEmpty(text) || text == "Sem nome" ? "Main" : text;
            }

            if (propertyController != null)
            {
                Label device = Field<Label>(form, "deviceValue");
                string text = device == null ? string.Empty : (device.Text ?? string.Empty).Trim();
                propertyController.Text = string.IsNullOrEmpty(text) ? "-" : text;
            }
        }

        private static void AddToolbar(Control parent, ref int x, string text, StudioIcon icon, Color color, bool enabled, EventHandler action)
        {
            V68ToolbarButton b = new V68ToolbarButton();
            b.Text = text;
            b.Icon = icon;
            b.IconColor = color;
            b.Location = new Point(x, 5);
            b.Size = new Size(74, 73);
            b.Enabled = enabled;
            if (action != null) b.Click += action;
            parent.Controls.Add(b);
            x += 76;
        }

        private static void AddSeparator(Control parent, ref int x)
        {
            Panel sep = new Panel();
            sep.BackColor = Border;
            sep.Bounds = new Rectangle(x + 5, 17, 1, 48);
            parent.Controls.Add(sep);
            x += 14;
        }

        private static Panel HeaderPanel(string text, StudioIcon icon)
        {
            V68Panel header = new V68Panel();
            header.Fill = Color.FromArgb(242, 246, 251);
            header.Line = Border;
            header.Icon = icon;
            header.Title = text;
            return header;
        }

        private static Label AddPropertyRow(Control parent, int top, string name, string value)
        {
            Label nameLabel = new Label();
            nameLabel.Text = name;
            nameLabel.Location = new Point(0, top);
            nameLabel.Size = new Size(88, 26);
            nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            nameLabel.ForeColor = Muted;
            nameLabel.Font = new Font("Segoe UI", 8.4f);
            parent.Controls.Add(nameLabel);

            Label valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.Location = new Point(92, top);
            valueLabel.Size = new Size(170, 26);
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.ForeColor = Text;
            valueLabel.Font = new Font("Segoe UI", 8.5f);
            parent.Controls.Add(valueLabel);
            return valueLabel;
        }

        private static TreeNode Node(string text, string imageKey, string tag)
        {
            TreeNode n = new TreeNode(text);
            n.ImageKey = imageKey;
            n.SelectedImageKey = imageKey;
            n.Tag = tag;
            return n;
        }

        private static ImageList BuildTreeImages()
        {
            ImageList list = new ImageList();
            list.ImageSize = new Size(18, 18);
            list.ColorDepth = ColorDepth.Depth32Bit;
            list.Images.Add("folder", Glyph(StudioIcon.Folder, Amber));
            list.Images.Add("gear", Glyph(StudioIcon.Gear, Muted));
            list.Images.Add("doc", Glyph(StudioIcon.Doc, Blue));
            list.Images.Add("fx", Glyph(StudioIcon.Convert, Blue));
            list.Images.Add("block", Glyph(StudioIcon.Grid, Blue));
            list.Images.Add("globe", Glyph(StudioIcon.Monitor, Blue));
            list.Images.Add("pi", Glyph(StudioIcon.Plus, Blue));
            list.Images.Add("library", Glyph(StudioIcon.Folder, Navy));
            return list;
        }

        private static Bitmap Glyph(StudioIcon icon, Color color)
        {
            Bitmap bmp = new Bitmap(18, 18);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                StudioGlyph.Draw(g, icon, new Rectangle(1, 1, 16, 16), color);
            }
            return bmp;
        }

        private static ToolStripMenuItem Menu(string text)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Text;
            item.BackColor = Color.White;
            return item;
        }

        private static ToolStripMenuItem Item(string text, EventHandler action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ForeColor = Text;
            item.BackColor = Color.White;
            if (action != null) item.Click += action;
            return item;
        }

        private static void SafeCall(UniversalStudioForm form, string method, object[] args)
        {
            try
            {
                MethodInfo mi = typeof(UniversalStudioForm).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null) throw new MissingMethodException(method);
                mi.Invoke(form, args);
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                MessageBox.Show(form, inner.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(form, ex.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static T Field<T>(object instance, string name) where T : class
        {
            if (instance == null) return null;
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance) as T;
        }
    }

    internal sealed class V68ColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Color.White; } }
        public override Color MenuStripGradientEnd { get { return Color.White; } }
        public override Color ToolStripDropDownBackground { get { return Color.White; } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(246, 249, 252); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(246, 249, 252); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(246, 249, 252); } }
        public override Color MenuBorder { get { return Color.FromArgb(207, 216, 226); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(207, 216, 226); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(232, 242, 253); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(232, 242, 253); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(232, 242, 253); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(232, 242, 253); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(232, 242, 253); } }
        public override Color SeparatorDark { get { return Color.FromArgb(207, 216, 226); } }
        public override Color SeparatorLight { get { return Color.White; } }
    }

    internal sealed class V68ToolbarButton : Control
    {
        private bool hover;
        private bool pressed;
        public StudioIcon Icon = StudioIcon.None;
        public Color IconColor = Color.FromArgb(28, 112, 220);

        public V68ToolbarButton()
        {
            Cursor = Cursors.Hand;
            TabStop = true;
            Font = new Font("Segoe UI", 8.2f);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color semantic = Enabled ? IconColor : Color.FromArgb(168, 176, 184);
            Color back = pressed ? Color.FromArgb(221, 235, 249) : hover && Enabled ? Color.FromArgb(237, 245, 253) : Color.White;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Rectangle chip = new Rectangle((Width - 36) / 2, 6, 36, 36);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(28, semantic))) g.FillEllipse(b, chip);
            StudioGlyph.Draw(g, Icon, new Rectangle((Width - 24) / 2, 12, 24, 24), semantic);

            TextRenderer.DrawText(g, Text, Font, new Rectangle(2, 47, Width - 4, 20), Enabled ? Color.FromArgb(31, 43, 56) : Color.FromArgb(150, 160, 170),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (hover && Enabled)
                using (SolidBrush b = new SolidBrush(semantic)) g.FillRectangle(b, 10, Height - 3, Width - 20, 2);

            if (Focused && Enabled)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3, Width - 6, Height - 6));
        }
    }

    internal sealed class V68BrandPanel : Control
    {
        public V68BrandPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(24, 82, 145), Color.FromArgb(15, 63, 116), 0f))
            {
                Point[] poly = new Point[] { new Point(28, 0), new Point(Width, 0), new Point(Width, Height), new Point(0, Height), new Point(20, 58) };
                g.FillPolygon(brush, poly);
            }

            StudioGlyph.Draw(g, StudioIcon.Ladder, new Rectangle(32, 18, 34, 34), Color.White);
            TextRenderer.DrawText(g, "OpenLadder Studio", new Font("Segoe UI Semibold", 14.0f, FontStyle.Bold), new Point(78, 15), Color.White);
            TextRenderer.DrawText(g, "PROGRAME IDEIAS. CONTROLE O AMANHÃ.", new Font("Segoe UI", 7.1f), new Point(80, 47), Color.FromArgb(220, 234, 248));
        }
    }

    internal sealed class V68Panel : Panel
    {
        public Color Fill = Color.White;
        public Color Line = Color.FromArgb(207, 216, 226);
        public StudioIcon Icon = StudioIcon.None;
        public string Title = string.Empty;

        public V68Panel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Fill)) e.Graphics.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(Line)) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
            StudioGlyph.Draw(e.Graphics, Icon, new Rectangle(12, 10, 17, 17), Color.FromArgb(28, 112, 220));
            TextRenderer.DrawText(e.Graphics, Title, new Font("Segoe UI Semibold", 9.3f, FontStyle.Bold), new Rectangle(38, 0, Width - 48, Height), Color.FromArgb(31, 43, 56), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            base.OnPaint(e);
        }
    }

    internal sealed class V68SectionLabel : Control
    {
        public V68SectionLabel()
        {
            Height = 31;
            Margin = new Padding(0, 2, 0, 0);
            Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(241, 246, 251))) e.Graphics.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(Color.FromArgb(220, 227, 235))) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
            TextRenderer.DrawText(e.Graphics, "›", new Font("Segoe UI Semibold", 11f, FontStyle.Bold), new Rectangle(8, 0, 20, Height), Color.FromArgb(28, 112, 220), TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(29, 0, Width - 36, Height), Color.FromArgb(31, 43, 56), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class V68InstructionButton : Control
    {
        private bool hover;
        public StudioIcon Icon = StudioIcon.None;
        public Color IconColor = Color.FromArgb(28, 112, 220);

        public V68InstructionButton()
        {
            Height = 34;
            Margin = new Padding(0);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 8.7f);
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color back = hover && Enabled ? Color.FromArgb(235, 244, 253) : Color.White;
            using (SolidBrush b = new SolidBrush(back)) e.Graphics.FillRectangle(b, ClientRectangle);
            Color icon = Enabled ? IconColor : Color.FromArgb(165, 173, 181);
            StudioGlyph.Draw(e.Graphics, Icon, new Rectangle(13, 9, 16, 16), icon);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(39, 0, Width - 45, Height), Enabled ? Color.FromArgb(31, 43, 56) : Color.FromArgb(145, 154, 164), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (Pen p = new Pen(Color.FromArgb(235, 239, 244))) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
            if (Focused && Enabled) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 3, Width - 8, Height - 6));
        }
    }

    internal sealed class V68InstructionSpec
    {
        public readonly string Category;
        public readonly string Text;
        public readonly StudioIcon Icon;
        public readonly LadderTool? Tool;
        public readonly bool Enabled;

        public V68InstructionSpec(string category, string text, StudioIcon icon, LadderTool? tool, bool enabled)
        {
            Category = category;
            Text = text;
            Icon = icon;
            Tool = tool;
            Enabled = enabled;
        }
    }
}
