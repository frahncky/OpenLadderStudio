$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'V73: UniversalStudioShell.build.cs nao encontrado.' }

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "V73: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "V73: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$shell = LF ([System.IO.File]::ReadAllText($shellPath))

# Estado da arvore de projeto.
if (-not $shell.Contains('private TreeView v73ProjectTree;')) {
    $shell = $shell.Replace('        private Panel navPanel;', "        private Panel navPanel;`n        private TreeView v73ProjectTree;`n        private TreeNode v73ProjectRoot;")
}

# -----------------------------------------------------------------------------
# Menu superior final: a mesma ordem do conceito aprovado, preservando apenas
# recursos realmente existentes no projeto.
# -----------------------------------------------------------------------------
$menu = @'
        private void V73ShowTransferCenter()
        {
            RefreshProfileUi();
            Form dialog = new Form();
            dialog.Text = "Transferência com o PLC";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.ClientSize = new Size(590, 330);
            dialog.BackColor = Workspace;
            dialog.Font = Font;

            Label title = InspectorLabel("TRANSFERÊNCIA", 12.0f, true, Fore);
            title.Location = new Point(24, 22); dialog.Controls.Add(title);

            string model = currentProfile == null ? "Nenhum controlador selecionado" : currentProfile.Manufacturer + "  •  " + currentProfile.Model;
            Label device = InspectorLabel(model, 9.0f, true, Fore);
            device.Location = new Point(24, 58); device.MaximumSize = new Size(540, 24); dialog.Controls.Add(device);

            string protocol = currentProfile == null ? "-" : currentProfile.Protocol;
            Label proto = InspectorLabel("Protocolo: " + protocol, 8.2f, false, Muted);
            proto.Location = new Point(24, 84); dialog.Controls.Add(proto);

            bool canRead = currentDriver != null && currentDriver.Capabilities.ReadProgram;
            bool canUpload = currentDriver != null && currentDriver.Capabilities.UploadProgram;
            Label support = InspectorLabel(canUpload
                ? "O driver declara suporte a envio de programa; use a tela específica do controlador quando disponível."
                : "Envio de programa ao PLC não está liberado para este driver. A interface não executará uma gravação não suportada.",
                8.2f, false, canUpload ? Fore : Muted);
            support.Location = new Point(24, 112); support.MaximumSize = new Size(540, 44); dialog.Controls.Add(support);

            Button connect = InspectorButton("Conectar ao PLC", 24, 176, 164);
            connect.Click += delegate { dialog.Close(); ShowCommunication(); };
            dialog.Controls.Add(connect);

            Button read = InspectorButton("Ler programa do PLC", 200, 176, 174);
            read.Enabled = canRead;
            read.Click += delegate { dialog.Close(); ShowReader(); };
            dialog.Controls.Add(read);

            Button controller = InspectorButton("Selecionar controlador", 386, 176, 178);
            controller.Click += delegate { dialog.Close(); ShowDeviceManager(); };
            dialog.Controls.Add(controller);

            Label hint = InspectorLabel(canRead
                ? "A leitura do programa está disponível para o controlador selecionado."
                : "A leitura de programa não está disponível para o controlador selecionado.",
                8.0f, false, Muted);
            hint.Location = new Point(24, 226); hint.MaximumSize = new Size(540, 38); dialog.Controls.Add(hint);

            Button close = InspectorButton("Fechar", 464, 282, 100);
            close.Click += delegate { dialog.Close(); };
            dialog.Controls.Add(close);

            dialog.ShowDialog(this);
            dialog.Dispose();
        }

        private void V73ShowDiagnostics()
        {
            RefreshProfileUi();
            Form dialog = new Form();
            dialog.Text = "Diagnóstico - OpenLadder Studio";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.ClientSize = new Size(590, 350);
            dialog.BackColor = Workspace;
            dialog.Font = Font;

            Label title = InspectorLabel("DIAGNÓSTICO", 12.0f, true, Fore);
            title.Location = new Point(24, 22); dialog.Controls.Add(title);

            string model = currentProfile == null ? "Nenhum controlador selecionado" : currentProfile.Manufacturer + "  •  " + currentProfile.Model;
            Label device = InspectorLabel(model, 9.0f, true, Fore);
            device.Location = new Point(24, 58); device.MaximumSize = new Size(540, 24); dialog.Controls.Add(device);

            string protocol = currentProfile == null ? "-" : currentProfile.Protocol;
            Label summary = InspectorLabel("Protocolo: " + protocol + "    •    Estado: OFF-LINE", 8.2f, false, Muted);
            summary.Location = new Point(24, 86); dialog.Controls.Add(summary);

            Label info = InspectorLabel("Use as verificações abaixo sem enviar comandos desconhecidos ao equipamento.", 8.2f, false, Muted);
            info.Location = new Point(24, 116); info.MaximumSize = new Size(540, 36); dialog.Controls.Add(info);

            Button portability = InspectorButton("Verificar compatibilidade", 24, 166, 174);
            portability.Click += delegate { dialog.Close(); CheckPortability(); };
            dialog.Controls.Add(portability);

            Button communication = InspectorButton("Abrir comunicação", 210, 166, 164);
            communication.Click += delegate { dialog.Close(); ShowCommunication(); };
            dialog.Controls.Add(communication);

            Button monitor = InspectorButton("Monitorar", 386, 166, 178);
            monitor.Click += delegate { dialog.Close(); ShowMonitor(); };
            dialog.Controls.Add(monitor);

            Label tp02 = InspectorLabel(IsTp02()
                ? "TP02 ativo: os recursos avançados permanecem separados no menu Ferramentas e o monitor comum opera em leitura."
                : "Os recursos avançados do TP02 só são usados quando um perfil TP02 está ativo.",
                8.0f, false, Muted);
            tp02.Location = new Point(24, 222); tp02.MaximumSize = new Size(540, 48); dialog.Controls.Add(tp02);

            Button close = InspectorButton("Fechar", 464, 300, 100);
            close.Click += delegate { dialog.Close(); };
            dialog.Controls.Add(close);

            dialog.ShowDialog(this);
            dialog.Dispose();
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
            editar.DropDownItems.Add(DropItem("Refazer", delegate { InvokeLadder("Redo", null); }));
            editar.DropDownItems.Add(new ToolStripSeparator());
            editar.DropDownItems.Add(DropItem("Apagar elemento selecionado", delegate { InvokeLadder("DeleteSelectedElement", null); }));

            miNav = DropItem("Projeto", delegate { TogglePanel(0); });
            miProps = DropItem("Instruções", delegate { TogglePanel(1); });
            miConsole = DropItem("Mensagens", delegate { TogglePanel(2); });
            miNav.Checked = true;
            miProps.Checked = true;
            miConsole.Checked = true;

            ToolStripMenuItem exibir = MenuItem("Exibir");
            exibir.DropDownItems.Add(miNav);
            exibir.DropDownItems.Add(miProps);
            exibir.DropDownItems.Add(miConsole);

            ToolStripMenuItem projeto = MenuItem("Projeto");
            projeto.DropDownItems.Add(DropItem("Configuração do PLC", delegate { ShowDeviceManager(); }));
            projeto.DropDownItems.Add(DropItem("Main (PRG)", delegate { ShowLadder(); }));
            projeto.DropDownItems.Add(new ToolStripSeparator());
            projeto.DropDownItems.Add(DropItem("Compilar / validar", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));

            ToolStripMenuItem plc = MenuItem("PLC");
            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(DropItem("Conectar...", delegate { ShowCommunication(); }));
            plc.DropDownItems.Add(DropItem("Transferência...", delegate { V73ShowTransferCenter(); }));
            plc.DropDownItems.Add(DropItem("Monitorar", delegate { ShowMonitor(); }));
            plc.DropDownItems.Add(DropItem("Ler programa do PLC", delegate { ShowReader(); }));

            ToolStripMenuItem simulador = MenuItem("Simulador");
            simulador.DropDownItems.Add(DropItem("Abrir simulador", delegate { ShowSimulator(); }));

            ToolStripMenuItem ferramentas = MenuItem("Ferramentas");
            ferramentas.DropDownItems.Add(DropItem("Diagnóstico", delegate { V73ShowDiagnostics(); }));
            ferramentas.DropDownItems.Add(DropItem("Verificar compatibilidade", delegate { CheckPortability(); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem tp02 = MenuItem("Diagnóstico avançado TP02");
            tp02.DropDownItems.Add(DropItem("Testar link de programação (PG)", delegate { LaunchInstalledTool("OpenLadderTP02PgLab.exe", "Laboratório PG TP02"); }));
            tp02.DropDownItems.Add(DropItem("Monitor MMI (somente leitura)", delegate { ShowTp02ReadOnlyMonitor(); }));
            tp02.DropDownItems.Add(DropItem("Analisar projeto/serial PC12", delegate { ShowTp02BridgeLab(); }));
            tp02.DropDownItems.Add(DropItem("Capturar tráfego serial PC12/TP02", delegate { LaunchInstalledTool("OpenLadderTP02Capture.exe", "Captura serial PC12/TP02"); }));
            tp02.DropDownItems.Add(DropItem("Decodificar RBP", delegate { ShowDecoder(); }));
            ferramentas.DropDownItems.Add(tp02);

            ToolStripMenuItem janela = MenuItem("Janela");
            janela.DropDownItems.Add(DropItem("Projeto", delegate { TogglePanel(0); }));
            janela.DropDownItems.Add(DropItem("Instruções", delegate { TogglePanel(1); }));
            janela.DropDownItems.Add(DropItem("Mensagens", delegate { TogglePanel(2); }));

            ToolStripMenuItem ajuda = MenuItem("Ajuda");
            ajuda.DropDownItems.Add(DropItem("Verificar atualizações", delegate { ShowUpdater(); }));
            ajuda.DropDownItems.Add(new ToolStripSeparator());
            ajuda.DropDownItems.Add(DropItem("Sobre o OpenLadder Studio", delegate
            {
                MessageBox.Show(this, "OpenLadder Studio\r\n\r\nEditor Ladder, simulação e comunicação com controladores compatíveis.",
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
            return menu;
        }

'@
$shell = Replace-Section $shell '        private MenuStrip BuildMenu()' '        private int toolCursor;' $menu 'menu V73'

# -----------------------------------------------------------------------------
# Toolbar: ordem aproximada ao mockup aprovado, mas com semantica segura. O
# comando Transferir abre uma central e nunca envia programa sem suporte do driver.
# -----------------------------------------------------------------------------
$toolbar = @'
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 56;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            toolCursor = 8;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Compilar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolButton(bar, "Transferir", StudioIcon.Download, false, delegate { V73ShowTransferCenter(); });
            AddToolButton(bar, "Simulador", StudioIcon.Grid, false, delegate { ShowSimulator(); });
            AddToolButton(bar, "Monitorar", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            AddToolButton(bar, "Diagnóstico", StudioIcon.Gear, false, delegate { V73ShowDiagnostics(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Refazer", StudioIcon.Redo, false, delegate { InvokeLadder("Redo", null); });
            return bar;
        }

'@
$shell = Replace-Section $shell '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar V73'

# -----------------------------------------------------------------------------
# Projeto: arvore real a esquerda. FUN/FB/Variaveis/Bibliotecas aparecem como
# estrutura de projeto, sem fingir que ja possuem editores funcionais.
# -----------------------------------------------------------------------------
$nav = @'
        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 252;
            nav.BackColor = StudioTheme.NavBg;

            Panel info = V73ProjectCard();
            info.Dock = DockStyle.Bottom;
            info.Height = 138;

            v73ProjectTree = new TreeView();
            v73ProjectTree.Dock = DockStyle.Fill;
            v73ProjectTree.BorderStyle = BorderStyle.None;
            v73ProjectTree.BackColor = StudioTheme.NavBg;
            v73ProjectTree.ForeColor = Fore;
            v73ProjectTree.Font = new Font("Segoe UI", 8.8f);
            v73ProjectTree.HideSelection = false;
            v73ProjectTree.FullRowSelect = true;
            v73ProjectTree.ShowLines = true;
            v73ProjectTree.ShowPlusMinus = true;
            v73ProjectTree.ShowRootLines = false;
            v73ProjectTree.ItemHeight = 25;
            v73ProjectTree.Indent = 18;

            v73ProjectRoot = new TreeNode("Projeto1");
            v73ProjectRoot.NodeFont = new Font("Segoe UI Semibold", 9.1f, FontStyle.Bold);
            v73ProjectRoot.Tag = "ROOT";

            TreeNode config = new TreeNode("Configuração do PLC"); config.Tag = "PLC_CONFIG";
            TreeNode programs = new TreeNode("Programas"); programs.Tag = "GROUP";
            TreeNode main = new TreeNode("Main (PRG)"); main.Tag = "MAIN";
            TreeNode fun = new TreeNode("FUN"); fun.Tag = "INFO_FUN"; fun.ForeColor = StudioTheme.Faint;
            TreeNode fb = new TreeNode("FB"); fb.Tag = "INFO_FB"; fb.ForeColor = StudioTheme.Faint;
            programs.Nodes.Add(main); programs.Nodes.Add(fun); programs.Nodes.Add(fb);

            TreeNode variables = new TreeNode("Variáveis"); variables.Tag = "INFO_VARIABLES"; variables.ForeColor = StudioTheme.Faint;
            TreeNode libraries = new TreeNode("Bibliotecas"); libraries.Tag = "INFO_LIBRARIES"; libraries.ForeColor = StudioTheme.Faint;

            TreeNode execution = new TreeNode("Execução"); execution.Tag = "GROUP";
            TreeNode simulation = new TreeNode("Simulador"); simulation.Tag = "SIM";
            TreeNode monitor = new TreeNode("Monitor on-line"); monitor.Tag = "MON";
            execution.Nodes.Add(simulation); execution.Nodes.Add(monitor);

            TreeNode system = new TreeNode("Sistema"); system.Tag = "GROUP";
            TreeNode updates = new TreeNode("Atualizações"); updates.Tag = "UPD";
            system.Nodes.Add(updates);

            v73ProjectRoot.Nodes.Add(config);
            v73ProjectRoot.Nodes.Add(programs);
            v73ProjectRoot.Nodes.Add(variables);
            v73ProjectRoot.Nodes.Add(libraries);
            v73ProjectRoot.Nodes.Add(execution);
            v73ProjectRoot.Nodes.Add(system);
            v73ProjectTree.Nodes.Add(v73ProjectRoot);
            v73ProjectRoot.Expand();
            programs.Expand();
            execution.Expand();

            v73ProjectTree.AfterSelect += delegate(object sender, TreeViewEventArgs e)
            {
                string key = e.Node == null || e.Node.Tag == null ? string.Empty : e.Node.Tag.ToString();
                if (key == "PLC_CONFIG") ShowDeviceManager();
                else if (key == "MAIN") ShowLadder();
                else if (key == "SIM") ShowSimulator();
                else if (key == "MON") ShowMonitor();
                else if (key == "UPD") ShowUpdater();
                else if (key.StartsWith("INFO_", StringComparison.Ordinal))
                {
                    if (statusText != null) statusText.Text = e.Node.Text + ": estrutura reservada para expansão do projeto.";
                }
            };

            nav.Controls.Add(v73ProjectTree);
            nav.Controls.Add(info);
            nav.Controls.Add(BuildBrand());
            return nav;
        }

        private Panel V73ProjectCard()
        {
            Panel card = new Panel();
            card.BackColor = StudioTheme.NavBg;
            card.Padding = new Padding(14, 8, 12, 8);

            Label pc = InspectorLabel("PROJETO ATUAL", 7.2f, true, StudioTheme.Faint);
            pc.Location = new Point(14, 8); card.Controls.Add(pc);
            projectValue = InspectorLabel("Projeto1", 8.8f, true, Fore);
            projectValue.Location = new Point(14, 28); projectValue.MaximumSize = new Size(220, 30); card.Controls.Add(projectValue);

            deviceValue = InspectorLabel("Nenhum controlador", 8.2f, true, Fore);
            deviceValue.Location = new Point(14, 65); deviceValue.MaximumSize = new Size(220, 22); card.Controls.Add(deviceValue);
            familyValue = InspectorLabel("-", 7.4f, false, Muted);
            familyValue.Location = new Point(14, 89); familyValue.MaximumSize = new Size(106, 18); card.Controls.Add(familyValue);
            protocolValue = InspectorLabel("-", 7.4f, false, Muted);
            protocolValue.Location = new Point(122, 89); protocolValue.MaximumSize = new Size(112, 18); card.Controls.Add(protocolValue);

            supportValue = InspectorLabel("-", 7.1f, true, Muted); supportValue.Visible = false; card.Controls.Add(supportValue);
            capabilityValue = InspectorLabel("-", 7.1f, false, Muted); capabilityValue.Visible = false; card.Controls.Add(capabilityValue);
            connectionValue = InspectorLabel("● OFF-LINE", 7.4f, true, Muted);
            connectionValue.Location = new Point(14, 111); card.Controls.Add(connectionValue);
            return card;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildNav()' '        private StudioPanel BuildConsole()' $nav 'arvore de projeto V73'

# -----------------------------------------------------------------------------
# Paleta de instrucoes: painel independente a direita, agora com busca local.
# -----------------------------------------------------------------------------
$inspector = @'
        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Right;
            p.Width = 292;
            p.BackColor = StudioTheme.NavBg;
            p.Padding = new Padding(0);
            p.Controls.Add(V73InstructionPanel());
            return p;
        }

        private Panel V73InstructionPanel()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = StudioTheme.NavBg;

            Panel searchHost = new Panel();
            searchHost.Dock = DockStyle.Top;
            searchHost.Height = 72;
            searchHost.BackColor = StudioTheme.NavBg;

            Label title = InspectorLabel("INSTRUÇÕES", 7.8f, true, StudioTheme.Faint);
            title.Location = new Point(14, 9); searchHost.Controls.Add(title);

            TextBox search = new TextBox();
            search.Location = new Point(14, 34);
            search.Size = new Size(262, 24);
            search.BorderStyle = BorderStyle.FixedSingle;
            search.BackColor = ChromeLight;
            search.ForeColor = Fore;
            search.Font = StudioTheme.Ui;
            searchHost.Controls.Add(search);

            FlowLayoutPanel list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = StudioTheme.NavBg;
            list.Padding = new Padding(10, 3, 8, 10);

            V73AddSection(list, "CONTATOS");
            V73AddInstruction(list, "Contato NA", StudioIcon.ContactNO, LadderTool.ContactNO);
            V73AddInstruction(list, "Contato NF", StudioIcon.ContactNC, LadderTool.ContactNC);
            V73AddInstruction(list, "Ramo paralelo NA", StudioIcon.ContactNO, LadderTool.ParallelNO);
            V73AddInstruction(list, "Ramo paralelo NF", StudioIcon.ContactNC, LadderTool.ParallelNC);

            V73AddSection(list, "SAÍDAS");
            V73AddInstruction(list, "Bobina", StudioIcon.Coil, LadderTool.Coil);
            V73AddInstruction(list, "SET", StudioIcon.Check, LadderTool.Set);
            V73AddInstruction(list, "RESET", StudioIcon.Refresh, LadderTool.Reset);

            V73AddSection(list, "TEMPORIZAÇÃO E CONTAGEM");
            V73AddInstruction(list, "Temporizador", StudioIcon.Timer, LadderTool.Timer);
            V73AddInstruction(list, "Contador", StudioIcon.Counter, LadderTool.Counter);

            V73AddSection(list, "FUNÇÕES");
            V73AddInstruction(list, "Borda de subida", StudioIcon.Bolt, LadderTool.EdgeUp);
            V73AddInstruction(list, "Borda de descida", StudioIcon.Bolt, LadderTool.EdgeDown);
            V73AddInstruction(list, "Função especial", StudioIcon.Chip, LadderTool.Function);
            V73AddInstruction(list, "END", StudioIcon.Terminal, LadderTool.End);

            V73AddSection(list, "EDIÇÃO");
            V73AddInstruction(list, "Selecionar", StudioIcon.Select, LadderTool.Select);
            V73AddAction(list, "Apagar selecionado", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedElement", null); });
            V73AddAction(list, "Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); });
            V73AddAction(list, "Remover linha", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); });

            search.TextChanged += delegate
            {
                string q = (search.Text ?? string.Empty).Trim();
                foreach (Control c in list.Controls)
                {
                    NavButton b = c as NavButton;
                    if (b != null) b.Visible = q.Length == 0 || b.Text.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
                }
            };

            host.Controls.Add(list);
            host.Controls.Add(searchHost);
            return host;
        }

        private static void V73AddSection(FlowLayoutPanel list, string text)
        {
            Label section = new Label();
            section.Width = 260;
            section.Height = 24;
            section.Margin = new Padding(8, 8, 0, 1);
            section.Text = text;
            section.TextAlign = ContentAlignment.MiddleLeft;
            section.ForeColor = StudioTheme.Faint;
            section.Font = StudioTheme.Section;
            list.Controls.Add(section);
        }

        private void V73AddInstruction(FlowLayoutPanel list, string text, StudioIcon icon, LadderTool tool)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Dock = DockStyle.None;
            b.Width = 260;
            b.Height = 34;
            b.Margin = new Padding(0, 0, 0, 1);
            b.Click += delegate { V73SelectLadderTool(tool); };
            list.Controls.Add(b);
        }

        private void V73AddAction(FlowLayoutPanel list, string text, StudioIcon icon, EventHandler action)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Dock = DockStyle.None;
            b.Width = 260;
            b.Height = 34;
            b.Margin = new Padding(0, 0, 0, 1);
            if (action != null) b.Click += action;
            list.Controls.Add(b);
        }

        private void V73SelectLadderTool(LadderTool tool)
        {
            ShowLadder();
            try
            {
                MethodInfo method = typeof(LadderEditorForm).GetMethod("SetActiveTool", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null) method.Invoke(ladderForm, new object[] { tool });
                if (statusText != null) statusText.Text = "Ferramenta: " + tool.ToString();
            }
            catch (Exception ex)
            {
                if (statusText != null) statusText.Text = ex.Message;
            }
        }

'@
$shell = Replace-Section $shell '        private Panel BuildInspector()' '        private Panel BuildStatusBar()' $inspector 'paleta V73'

# Titulo e arvore acompanham o nome real do projeto sem carregar a indicacao de
# alteracao/rungs para a barra de titulo.
$projectMethod = @'
        private void UpdateProjectName()
        {
            if (ladderForm == null || ladderForm.IsDisposed || projectValue == null) return;
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("projectLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Label label = field == null ? null : field.GetValue(ladderForm) as Label;
                string value = label == null ? string.Empty : (label.Text ?? string.Empty).Trim();
                int meta = value.IndexOf("     |", StringComparison.Ordinal);
                if (meta >= 0) value = value.Substring(0, meta).Trim();
                int dirtyMark = value.IndexOf("  •", StringComparison.Ordinal);
                if (dirtyMark >= 0) value = value.Substring(0, dirtyMark).Trim();
                if (string.IsNullOrEmpty(value) || value == "Projeto sem nome") value = "Projeto1";
                projectValue.Text = value;
                if (v73ProjectRoot != null) v73ProjectRoot.Text = value;
                Text = "OpenLadder Studio - " + value;
            }
            catch
            {
                projectValue.Text = "Projeto1";
                if (v73ProjectRoot != null) v73ProjectRoot.Text = "Projeto1";
                Text = "OpenLadder Studio - Projeto1";
            }
        }

'@
$shell = Replace-Section $shell '        private void UpdateProjectName()' '        private void SetRailEnabled' $projectMethod 'titulo e arvore V73'

# O projeto ainda nao possui zoom do canvas; nao exibir um valor fixo como se
# fosse um estado operacional real.
$shell = $shell.Replace('    |    ZOOM: 100%', '')

# Guardrails da V73.
if ($shell -notmatch 'MenuItem\("Janela"\)') { throw 'V73: menu Janela nao aplicado.' }
if ($shell -notmatch 'AddToolButton\(bar, "Diagnóstico"') { throw 'V73: toolbar final nao aplicada.' }
if ($shell -notmatch 'v73ProjectTree = new TreeView\(\)') { throw 'V73: arvore de projeto nao aplicada.' }
if ($shell -notmatch 'V73InstructionPanel\(\)') { throw 'V73: paleta de instrucoes nao aplicada.' }
if ($shell -notmatch 'V73ShowTransferCenter\(\)') { throw 'V73: central de transferencia nao aplicada.' }

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'V73 aplicada: arvore de projeto, toolbar funcional, transferencia segura, diagnostico e busca de instrucoes.' -ForegroundColor Cyan
