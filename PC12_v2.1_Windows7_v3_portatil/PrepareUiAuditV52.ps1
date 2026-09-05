$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $ladderPath)) { throw 'LadderEditor.build.cs nao encontrado.' }

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "Ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$versionPath = Join-Path $root 'version.txt'
$version = if (Test-Path $versionPath) { [System.IO.File]::ReadAllText($versionPath).Trim() } else { '0.52' }

# -----------------------------------------------------------------------------
# Shell: corrige inconsistencias de versao, semantica e densidade visual.
# -----------------------------------------------------------------------------
$shell = LF ([System.IO.File]::ReadAllText($shellPath))
$shell = $shell.Replace('v0.12', 'v' + $version)
$shell = $shell.Replace('Painel de navegação', 'Painel lateral')

$fieldNeedle = '        private Label toolValue;'
$fieldReplacement = @'
        private Label toolValue;
        private TextBox elementSearch;
        private readonly List<NavButton> elementButtons = new List<NavButton>();
        private readonly ToolTip toolbarTips = new ToolTip();
'@
$shell = Replace-Required $shell $fieldNeedle $fieldReplacement.TrimEnd() 'campos UX V52'

$toolButton = @'
        private void AddToolButton(Control bar, string text, StudioIcon icon, bool emphasis, EventHandler action)
        {
            IconToolButton b = new IconToolButton();
            b.Text = text;
            b.Icon = icon;
            b.Emphasis = emphasis;
            b.Height = 48;
            b.Width = b.MeasureWidth();
            b.Location = new Point(toolCursor, 2);
            b.AccessibleName = text;
            b.AccessibleDescription = text + " - OpenLadder Studio";
            if (action != null) b.Click += action;
            toolbarTips.SetToolTip(b, text);
            bar.Controls.Add(b);
            toolCursor += b.Width;
        }

'@
$shell = Replace-Section $shell '        private void AddToolButton(Control bar, string text, StudioIcon icon, bool emphasis, EventHandler action)' '        private void AddToolSeparator(Control bar)' $toolButton 'toolbar buttons compactos'

$toolbar = @'
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 52;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            Label brand = new Label();
            brand.Text = "OpenLadder Studio  v__VERSION__";
            brand.Dock = DockStyle.Right;
            brand.Width = 190;
            brand.TextAlign = ContentAlignment.MiddleRight;
            brand.ForeColor = Muted;
            brand.Font = StudioTheme.UiBold;
            brand.Padding = new Padding(0, 0, 14, 0);
            bar.Controls.Add(brand);

            toolCursor = 8;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Refazer", StudioIcon.Redo, false, delegate { InvokeLadder("Redo", null); });
            AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Conectar", StudioIcon.Plug, true, delegate { ShowCommunication(); });
            AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            AddToolButton(bar, "Ler PLC", StudioIcon.Download, false, delegate { ShowReader(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Controlador", StudioIcon.Chip, false, delegate { ShowDeviceManager(); });
            AddToolButton(bar, "Atualizar", StudioIcon.Refresh, false, delegate { ShowUpdater(); });
            return bar;
        }

'@
$toolbar = $toolbar.Replace('__VERSION__', $version)
$shell = Replace-Section $shell '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar V52'

$brand = @'
        private Control BuildBrand()
        {
            StudioPanel brand = new StudioPanel();
            brand.Dock = DockStyle.Top;
            brand.Height = 72;
            brand.Fill = StudioTheme.NavBg;
            brand.BottomLine = Border;

            System.Drawing.Icon brandIcon = null;
            try { brandIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            brand.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                if (brandIcon != null)
                    g.DrawIcon(brandIcon, new Rectangle(16, 16, 40, 40));
                else
                {
                    using (SolidBrush b = new SolidBrush(Accent)) g.FillRectangle(b, new Rectangle(18, 18, 36, 36));
                    StudioGlyph.Draw(g, StudioIcon.Ladder, new Rectangle(25, 25, 22, 22), Color.White);
                }

                TextRenderer.DrawText(g, "OpenLadder Studio", new Font("Segoe UI Semibold", 11.2f, FontStyle.Bold),
                    new Point(68, 17), Fore);
                string sub = currentProfile == null ? "Automação e programação Ladder" : currentProfile.Manufacturer + "  •  " + currentProfile.Model;
                TextRenderer.DrawText(g, sub, StudioTheme.Small,
                    new Rectangle(68, 40, 202, 18), Muted,
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            };
            return brand;
        }

'@
$shell = Replace-Section $shell '        private Control BuildBrand()' '        private Panel BuildNav()' $brand 'identidade lateral'

$nav = @'
        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 292;
            nav.BackColor = StudioTheme.NavBg;

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = StudioTheme.NavBg;

            Panel elements = BuildElementLibrary();
            elements.Dock = DockStyle.Fill;

            Panel props = BuildSidebarGroup("Propriedades", BuildSidebarPropertiesCard(), 174);
            props.Dock = DockStyle.Bottom;

            Panel project = BuildSidebarGroup("Projeto", BuildSidebarProjectCard(), 176);
            project.Dock = DockStyle.Top;

            body.Controls.Add(elements);
            body.Controls.Add(props);
            body.Controls.Add(project);

            nav.Controls.Add(body);
            nav.Controls.Add(BuildBrand());
            return nav;
        }

        private Panel BuildSidebarGroup(string title, Control content, int height)
        {
            Panel wrap = new Panel();
            wrap.Height = height;
            wrap.BackColor = StudioTheme.NavBg;

            content.Dock = DockStyle.Fill;
            NavSection head = new NavSection(title);
            wrap.Controls.Add(content);
            wrap.Controls.Add(head);
            return wrap;
        }

        private Panel BuildElementLibrary()
        {
            Panel host = new Panel();
            host.BackColor = StudioTheme.NavBg;

            Panel searchBar = new Panel();
            searchBar.Dock = DockStyle.Top;
            searchBar.Height = 66;
            searchBar.BackColor = StudioTheme.NavBg;

            Label title = InspectorLabel("ELEMENTOS LADDER", 7.4f, true, StudioTheme.Faint);
            title.Location = new Point(18, 10);
            searchBar.Controls.Add(title);

            elementSearch = new TextBox();
            elementSearch.Location = new Point(16, 31);
            elementSearch.Size = new Size(258, 24);
            elementSearch.BorderStyle = BorderStyle.FixedSingle;
            elementSearch.BackColor = ChromeLight;
            elementSearch.ForeColor = StudioTheme.Faint;
            elementSearch.Font = StudioTheme.Ui;
            elementSearch.Text = "Buscar elemento...";
            elementSearch.Enter += delegate
            {
                if (elementSearch.Text == "Buscar elemento...")
                {
                    elementSearch.Text = string.Empty;
                    elementSearch.ForeColor = Fore;
                }
            };
            elementSearch.Leave += delegate
            {
                if (string.IsNullOrWhiteSpace(elementSearch.Text))
                {
                    elementSearch.Text = "Buscar elemento...";
                    elementSearch.ForeColor = StudioTheme.Faint;
                }
            };
            elementSearch.TextChanged += delegate
            {
                string query = elementSearch.Text == "Buscar elemento..." ? string.Empty : elementSearch.Text;
                ApplyElementFilter(query);
            };
            searchBar.Controls.Add(elementSearch);

            FlowLayoutPanel list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = StudioTheme.NavBg;
            list.Padding = new Padding(10, 6, 8, 10);

            elementButtons.Clear();
            AddElementTool(list, "Selecionar", StudioIcon.Select, LadderTool.Select);
            AddElementTool(list, "Contato NA", StudioIcon.ContactNO, LadderTool.ContactNO);
            AddElementTool(list, "Contato NF", StudioIcon.ContactNC, LadderTool.ContactNC);
            AddElementTool(list, "Ramo paralelo NA", StudioIcon.ContactNO, LadderTool.ParallelNO);
            AddElementTool(list, "Ramo paralelo NF", StudioIcon.ContactNC, LadderTool.ParallelNC);
            AddElementTool(list, "Bobina", StudioIcon.Coil, LadderTool.Coil);
            AddElementTool(list, "Temporizador", StudioIcon.Timer, LadderTool.Timer);
            AddElementTool(list, "Contador", StudioIcon.Counter, LadderTool.Counter);
            AddElementTool(list, "SET", StudioIcon.Check, LadderTool.Set);
            AddElementTool(list, "RESET", StudioIcon.Refresh, LadderTool.Reset);
            AddElementTool(list, "Borda de subida", StudioIcon.Bolt, LadderTool.EdgeUp);
            AddElementTool(list, "Borda de descida", StudioIcon.Bolt, LadderTool.EdgeDown);
            AddElementTool(list, "Função especial", StudioIcon.Chip, LadderTool.Function);
            AddElementTool(list, "END", StudioIcon.Terminal, LadderTool.End);
            AddElementTool(list, "Apagar elemento", StudioIcon.Minus, LadderTool.Erase);
            AddElementAction(list, "Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); });
            AddElementAction(list, "Remover linha", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); });

            host.Controls.Add(list);
            host.Controls.Add(searchBar);
            return host;
        }

        private void AddElementTool(FlowLayoutPanel list, string text, StudioIcon icon, LadderTool tool)
        {
            NavButton b = ToolItem(text, icon, tool);
            ConfigureElementButton(b);
            elementButtons.Add(b);
            list.Controls.Add(b);
        }

        private void AddElementAction(FlowLayoutPanel list, string text, StudioIcon icon, EventHandler action)
        {
            NavButton b = SideAction(text, icon, action);
            ConfigureElementButton(b);
            elementButtons.Add(b);
            list.Controls.Add(b);
        }

        private static void ConfigureElementButton(NavButton b)
        {
            b.Dock = DockStyle.None;
            b.Width = 252;
            b.Height = 34;
            b.Margin = new Padding(0, 0, 0, 1);
        }

        private void ApplyElementFilter(string query)
        {
            string q = (query ?? string.Empty).Trim();
            for (int i = 0; i < elementButtons.Count; i++)
            {
                NavButton b = elementButtons[i];
                b.Visible = q.Length == 0 || b.Text.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }
        }

        private NavButton ToolItem(string text, StudioIcon icon, LadderTool tool)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Click += delegate { SelectLadderTool(tool); };
            ladderToolButtons[tool.ToString()] = b;
            return b;
        }

        private NavButton SideAction(string text, StudioIcon icon, EventHandler action)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            if (action != null) b.Click += action;
            return b;
        }

        private Panel BuildSidebarProjectCard()
        {
            Panel card = new Panel();
            card.BackColor = StudioTheme.NavBg;

            Label pc = InspectorLabel("Projeto atual", 7.4f, true, StudioTheme.Faint);
            pc.Location = new Point(18, 8); card.Controls.Add(pc);
            projectValue = InspectorLabel("Projeto sem nome", 9.2f, true, Fore);
            projectValue.Location = new Point(18, 28); projectValue.MaximumSize = new Size(250, 34); card.Controls.Add(projectValue);

            Label dc = InspectorLabel("Controlador", 7.4f, true, StudioTheme.Faint);
            dc.Location = new Point(18, 65); card.Controls.Add(dc);
            deviceValue = InspectorLabel("Nenhum controlador", 8.7f, true, Fore);
            deviceValue.Location = new Point(18, 84); deviceValue.MaximumSize = new Size(250, 22); card.Controls.Add(deviceValue);
            familyValue = InspectorLabel("-", 7.5f, false, Muted);
            familyValue.Location = new Point(18, 108); familyValue.MaximumSize = new Size(118, 20); card.Controls.Add(familyValue);
            protocolValue = InspectorLabel("-", 7.5f, false, Muted);
            protocolValue.Location = new Point(138, 108); protocolValue.MaximumSize = new Size(132, 20); card.Controls.Add(protocolValue);

            supportValue = InspectorLabel("-", 7.6f, true, Muted);
            supportValue.Location = new Point(18, 131); card.Controls.Add(supportValue);
            capabilityValue = InspectorLabel("-", 7.6f, false, Muted); capabilityValue.Visible = false; card.Controls.Add(capabilityValue);
            connectionValue = InspectorLabel("● OFFLINE", 7.6f, true, Muted); connectionValue.Visible = false; card.Controls.Add(connectionValue);
            return card;
        }

        private Panel BuildSidebarPropertiesCard()
        {
            Panel card = new Panel();
            card.BackColor = StudioTheme.NavBg;

            Label a = InspectorLabel("Seleção", 7.4f, true, StudioTheme.Faint);
            a.Location = new Point(18, 8); card.Controls.Add(a);
            selectionValue = InspectorLabel("Nenhum elemento selecionado", 8.4f, true, Fore);
            selectionValue.Location = new Point(18, 28); selectionValue.MaximumSize = new Size(250, 38); card.Controls.Add(selectionValue);

            Label b = InspectorLabel("Ferramenta ativa", 7.4f, true, StudioTheme.Faint);
            b.Location = new Point(18, 68); card.Controls.Add(b);
            toolValue = InspectorLabel("Selecionar", 8.3f, false, Muted);
            toolValue.Location = new Point(18, 87); card.Controls.Add(toolValue);

            Button edit = InspectorButton("Editar elemento selecionado", 18, 112, 252);
            edit.Height = 30;
            edit.Click += delegate { EditSelectedLadderElement(); };
            card.Controls.Add(edit);
            return card;
        }

        private void EditSelectedLadderElement()
        {
            ShowLadder();
            try
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo get = typeof(LadderEditorForm).GetMethod("GetSelectedElement", flags);
                LadderElement el = get == null ? null : get.Invoke(ladderForm, null) as LadderElement;
                if (el == null || el.Type == LadderElementType.Empty)
                {
                    statusText.Text = "Selecione um elemento Ladder para editar.";
                    return;
                }

                MethodInfo edit = typeof(LadderEditorForm).GetMethod("CanvasElementDoubleClick", flags);
                if (edit == null) return;
                edit.Invoke(ladderForm, new object[] { ladderForm, EventArgs.Empty });
                UpdateProjectName();
                RefreshLadderSelectionProperties();
            }
            catch (Exception ex)
            {
                statusText.Text = "Não foi possível editar o elemento: " + ex.Message;
            }
        }

        private void SelectLadderTool(LadderTool tool)
        {
            ShowLadder();
            try
            {
                MethodInfo m = typeof(LadderEditorForm).GetMethod("SetActiveTool", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null) m.Invoke(ladderForm, new object[] { tool });
                foreach (KeyValuePair<string, NavButton> pair in ladderToolButtons)
                {
                    pair.Value.Active = string.Equals(pair.Key, tool.ToString(), StringComparison.OrdinalIgnoreCase);
                    pair.Value.Invalidate();
                }
                if (toolValue != null) toolValue.Text = LadderToolLabel(tool);
                statusText.Text = "Ferramenta: " + LadderToolLabel(tool);
            }
            catch (Exception ex) { statusText.Text = ex.Message; }
        }

        private static string LadderToolLabel(LadderTool tool)
        {
            if (tool == LadderTool.ContactNO) return "Contato NA";
            if (tool == LadderTool.ContactNC) return "Contato NF";
            if (tool == LadderTool.ParallelNO) return "Ramo paralelo NA";
            if (tool == LadderTool.ParallelNC) return "Ramo paralelo NF";
            if (tool == LadderTool.Coil) return "Bobina";
            if (tool == LadderTool.Timer) return "Temporizador";
            if (tool == LadderTool.Counter) return "Contador";
            if (tool == LadderTool.Set) return "SET";
            if (tool == LadderTool.Reset) return "RESET";
            if (tool == LadderTool.EdgeUp) return "Borda de subida";
            if (tool == LadderTool.EdgeDown) return "Borda de descida";
            if (tool == LadderTool.Function) return "Função especial";
            if (tool == LadderTool.End) return "END";
            if (tool == LadderTool.Erase) return "Apagar";
            return "Selecionar";
        }

'@
$shell = Replace-Section $shell '        private Panel BuildNav()' '        private StudioPanel BuildConsole()' $nav 'painel lateral V52'

# Saida passa a iniciar recolhida para priorizar o editor. Continua acessivel em Exibir.
$shell = $shell.Replace('            miConsole.Checked = true;', '            miConsole.Checked = false;')
$consoleNeedle = '            wrap.Fill = Color.FromArgb(22, 24, 27);'
$consoleReplacement = @'
            wrap.Fill = Color.FromArgb(22, 24, 27);
            wrap.Visible = false;
'@
$shell = Replace-Required $shell $consoleNeedle $consoleReplacement.TrimEnd() 'console recolhido'

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Editor Ladder standalone: ajuda coerente com Refazer e identidade atual.
# -----------------------------------------------------------------------------
$ladder = LF ([System.IO.File]::ReadAllText($ladderPath))
$ladder = $ladder.Replace('Ctrl+Z: desfazer • Del: apagar', 'Ctrl+Z: desfazer • Ctrl+Y: refazer • Del: apagar')
$ladder = $ladder.Replace('ELEMENTOS TP02', 'ELEMENTOS LADDER')
[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)

Write-Host 'UI V52 aplicada: versao coerente, toolbar sem clipping, elementos pesquisaveis, propriedades persistentes e identidade unificada.'
