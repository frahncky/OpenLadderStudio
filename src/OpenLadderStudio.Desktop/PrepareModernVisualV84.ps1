$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$path = Join-Path $root 'StudioUi.build.cs'
if (-not (Test-Path -LiteralPath $path)) { throw 'Interface preparada nao encontrada.' }
$ui = [IO.File]::ReadAllText($path)
# Keep semantic icon colors. Muted monochrome glyphs made the action bar hard
# to scan, especially for users learning the editor.
$ui = $ui.Replace('new Font("Segoe UI", 8.75f)', 'new Font("Segoe UI", 9.25f)')
$ui = $ui.Replace('new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold)', 'new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold)')
$ui = $ui.Replace('new Font("Segoe UI", 7.75f)', 'new Font("Segoe UI", 8.25f)')
$ui = $ui.Replace('using (Pen p = NewPen(c, 1.5f))', 'using (Pen p = NewPen(c, Math.Max(1.5f, r.Width / 13f)))')
$ui = [regex]::Replace($ui, 'Height = 34;(\r?\n\s+Dock = DockStyle\.Top;)', 'Height = 40;$1')
$ui = [regex]::Replace($ui, 'Height = 54;(\r?\n\s+Cursor = Cursors\.Hand;)', 'Height = 60;$1')
$ui = $ui.Replace('new Rectangle(18, (Height - 16) / 2, 16, 16)', 'new Rectangle(18, (Height - 18) / 2, 18, 18)')
# Outline arrowheads match the rounded stroke used by the other glyphs.
$ui = $ui.Replace('g.FillPolygon(b, new PointF[] {', 'g.DrawPolygon(p, new PointF[] {')
# Bind every Studio control to the same persisted theme palette as the other
# windows. The previous fixed colors left this main screen visually unchanged.
foreach ($name in @('Shell','Chrome','ChromeLight','Border','Accent','AccentDark','Workspace','Fore','Muted','NavBg','NavHover','NavActive','Faint','Disabled','Danger','Warning','Info')) {
    $ui = [regex]::Replace($ui,
        'public static readonly Color ' + $name + ' = Color\.FromArgb\([^;]+;',
        'public static readonly Color ' + $name + ' = OpenLadderPalette.' + $name + ';')
}
# Make the primary edit action unmistakable and keep destructive actions in red.
$ui = $ui.Replace('        public bool Active;', '        public bool Active;' + [Environment]::NewLine + '        public bool Primary;')
$ui = $ui.Replace('Active ? StudioTheme.NavActive : hover ? StudioTheme.NavHover : StudioTheme.NavBg;', 'Primary ? OpenLadderPalette.Accent : Active ? StudioTheme.NavActive : hover ? StudioTheme.NavHover : StudioTheme.NavBg;')
$ui = $ui.Replace('if (Active && on)', 'if ((Active || Primary) && on)')
$ui = $ui.Replace('Active || hover ? StudioTheme.Fore : StudioTheme.Muted;', 'Active || hover || Primary ? StudioTheme.Fore : StudioTheme.Muted;')
$ui = $ui.Replace('Active && on ? StudioTheme.Accent : fore);', '(Active || Primary) && on ? StudioTheme.Accent : ((Icon == StudioIcon.Minus || Icon == StudioIcon.Trash) ? StudioTheme.Danger : fore));')
$ui = $ui.Replace('(Active || Primary) && on ? StudioTheme.Accent : ((Icon == StudioIcon.Minus || Icon == StudioIcon.Trash) ? StudioTheme.Danger : fore)', 'Primary && on ? OpenLadderPalette.OnAccent : ((Active && on) ? StudioTheme.Accent : ((Icon == StudioIcon.Minus || Icon == StudioIcon.Trash) ? StudioTheme.Danger : fore))')
$ui = $ui.Replace('Active || hover || Primary ? StudioTheme.Fore : StudioTheme.Muted;', 'Primary && on ? OpenLadderPalette.OnAccent : (Active || hover ? StudioTheme.Fore : StudioTheme.Muted);')
$ui = $ui.Replace('Active && on ? StudioTheme.UiBold : StudioTheme.Ui,', '(Active || Primary) && on ? StudioTheme.UiBold : StudioTheme.Ui,')
[IO.File]::WriteAllText($path, $ui, [Text.Encoding]::UTF8)

# Organize the editor around three user-resizable areas: project navigation,
# the active document, and contextual properties. Nested SplitContainers keep
# each region usable as the window changes size and allow panels to collapse.
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'Shell preparado nao encontrado.' }
$shell = [IO.File]::ReadAllText($shellPath)
# The shell and its menu renderer use the same active palette as the editor.
foreach ($name in @('Shell','Chrome','ChromeLight','Border','Accent','AccentDark','Workspace','Fore','Muted','Disabled')) {
    $shell = [regex]::Replace($shell,
        '(private readonly Color ' + $name + ' = )Color\.FromArgb\([^;]+;',
        ('$1OpenLadderPalette.' + $name + ';'))
}
$shell = [regex]::Replace($shell, '(private readonly Color chrome = )Color\.FromArgb\([^;]+;', '$1OpenLadderPalette.Chrome;')
$shell = [regex]::Replace($shell, '(private readonly Color hover = )Color\.FromArgb\([^;]+;', '$1OpenLadderPalette.NavHover;')
$shell = [regex]::Replace($shell, '(private readonly Color border = )Color\.FromArgb\([^;]+;', '$1OpenLadderPalette.Border;')
$buildStart = $shell.IndexOf('        private void BuildUi()')
$openBrace = if ($buildStart -ge 0) { $shell.IndexOf('{', $buildStart) } else { -1 }
if ($buildStart -lt 0 -or $openBrace -lt 0) { throw 'Nao foi possivel localizar BuildUi no shell.' }
$depth = 0
$buildEnd = -1
for ($i = $openBrace; $i -lt $shell.Length; $i++) {
    if ($shell[$i] -eq '{') { $depth++ }
    elseif ($shell[$i] -eq '}') {
        $depth--
        if ($depth -eq 0) { $buildEnd = $i + 1; break }
    }
}
if ($buildEnd -lt 0) { throw 'Nao foi possivel delimitar o metodo BuildUi.' }
$buildUi = @'
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

            navSplit = new SplitContainer();
            navSplit.Dock = DockStyle.Fill;
            navSplit.Orientation = Orientation.Vertical;
            navSplit.SplitterWidth = 6;
            navSplit.Size = new Size(1400, 760);
            navSplit.SplitterDistance = 252;
            navSplit.Panel1MinSize = 190;
            navSplit.Panel2MinSize = 600;
            navSplit.BackColor = Border;
            workspace.Controls.Add(navSplit);

            navPanel = BuildNav();
            navPanel.Dock = DockStyle.Fill;
            navSplit.Panel1.Controls.Add(navPanel);

            inspectorSplit = new SplitContainer();
            inspectorSplit.Dock = DockStyle.Fill;
            inspectorSplit.Orientation = Orientation.Vertical;
            inspectorSplit.SplitterWidth = 6;
            inspectorSplit.Size = new Size(1100, 700);
            inspectorSplit.SplitterDistance = 820;
            inspectorSplit.Panel1MinSize = 420;
            inspectorSplit.Panel2MinSize = 220;
            inspectorSplit.BackColor = Border;
            navSplit.Panel2.Controls.Add(inspectorSplit);

            Panel center = new Panel();
            center.Dock = DockStyle.Fill;
            center.BackColor = Workspace;
            inspectorSplit.Panel1.Controls.Add(center);

            inspector = BuildInspector();
            inspector.Dock = DockStyle.Fill;
            inspectorSplit.Panel2.Controls.Add(inspector);

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

            Shown += delegate
            {
                int navMax = navSplit.ClientSize.Width - navSplit.Panel2MinSize - navSplit.SplitterWidth;
                if (navMax >= navSplit.Panel1MinSize)
                    navSplit.SplitterDistance = Math.Min(252, navMax);
                int workMax = inspectorSplit.ClientSize.Width - inspectorSplit.Panel2MinSize - inspectorSplit.SplitterWidth;
                if (workMax >= inspectorSplit.Panel1MinSize)
                    inspectorSplit.SplitterDistance = Math.Min(920, workMax);
            };
        }

'@
$shell = $shell.Substring(0, $buildStart) + $buildUi.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + $shell.Substring($buildEnd)
if (-not $shell.Contains('private SplitContainer navSplit;')) {
    $shell = $shell.Replace('        private Panel navPanel;', '        private Panel navPanel;' + [Environment]::NewLine + '        private SplitContainer navSplit;' + [Environment]::NewLine + '        private SplitContainer inspectorSplit;')
}
$actionMenu = @'
            ToolStripMenuItem acoes = MenuItem("Ações");
            ToolStripMenuItem programacao = MenuItem("Programação");
            programacao.DropDownItems.Add(DropItem("Abrir editor Ladder", delegate { ShowLadder(); }));
            programacao.DropDownItems.Add(DropItem("Validar projeto", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));
            programacao.DropDownItems.Add(DropItem("Simular processo", delegate { ShowSimulator(); }));

            ToolStripMenuItem comunicacao = MenuItem("PLC e comunicação");
            comunicacao.DropDownItems.Add(DropItem("Selecionar controlador", delegate { ShowDeviceManager(); }));
            comunicacao.DropDownItems.Add(DropItem("Comunicação", delegate { ShowCommunication(); }));
            comunicacao.DropDownItems.Add(DropItem("Monitor on-line", delegate { ShowMonitor(); }));
            comunicacao.DropDownItems.Add(DropItem("Ler programa do PLC", delegate { ShowReader(); }));

            ToolStripMenuItem engenharia = MenuItem("Engenharia reversa");
            engenharia.DropDownItems.Add(DropItem("Decodificador", delegate { ShowDecoder(); }));
            engenharia.DropDownItems.Add(DropItem("Calibrar instruções", delegate { ShowCalibration(); }));
            engenharia.DropDownItems.Add(DropItem("Converter IL para Ladder", delegate { ShowIl(); }));
            acoes.DropDownItems.Add(programacao);
            acoes.DropDownItems.Add(comunicacao);
            acoes.DropDownItems.Add(engenharia);

'@
$shell = $shell.Replace('            ToolStripMenuItem ajuda = MenuItem("Ajuda");', $actionMenu + '            ToolStripMenuItem ajuda = MenuItem("Ajuda");')
$shell = $shell.Replace('            menu.Items.Add(exibir);', '            menu.Items.Add(exibir);' + [Environment]::NewLine + '            menu.Items.Add(acoes);')
# Put line editing where it is immediately visible above the instruction
# library. These controls used to be at the very bottom of a long scroll list.
$shell = [regex]::Replace($shell,
    '(?m)^[ \t]*(?:V73AddSection\(list, "EDIÇÃO"\);[ \t]*|V73AddInstruction\(list, "Selecionar"[^\r\n]*|V73AddAction\(list, "(?:Apagar selecionado|Adicionar linha|Remover linha)"[^\r\n]*)\r?\n', '')
$editActions = @'
            V73AddSection(list, "EDIÇÃO");
            V73AddInstruction(list, "Selecionar / mover", StudioIcon.Select, LadderTool.Select);
            V73AddAction(list, "Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); });
            V73AddAction(list, "Excluir linha selecionada", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); });
            V73AddAction(list, "Apagar elemento selecionado", StudioIcon.Trash, delegate { InvokeLadder("DeleteSelectedElement", null); });

'@
$shell = $shell.Replace('            V73AddSection(list, "CONTATOS");', $editActions + '            V73AddSection(list, "CONTATOS");')
if ([regex]::Matches($shell, 'V73AddSection\(list, "EDIÇÃO"\);').Count -ne 1) {
    throw 'A paleta deve conter uma unica secao EDICAO.'
}
if ($shell.IndexOf('V73AddInstruction(list, "Selecionar / mover"') -gt $shell.IndexOf('V73AddSection(list, "CONTATOS")')) {
    throw 'A ferramenta de selecao deve aparecer antes da biblioteca de contatos.'
}
$shell = $shell.Replace('v73ProjectTree.Font = new Font("Segoe UI", 8.8f);', 'v73ProjectTree.Font = new Font("Segoe UI", 9.25f);')
$shell = $shell.Replace('v73ProjectTree.ItemHeight = 25;', 'v73ProjectTree.ItemHeight = 28;')
$actionMethod = [regex]::Match($shell, '(?s)private void V73AddAction\(FlowLayoutPanel list, string text, StudioIcon icon, EventHandler action\)\s*\{.*?\n        \}')
if (-not $actionMethod.Success) { throw 'Nao foi possivel localizar as acoes de edicao da paleta.' }
$actionBody = $actionMethod.Value.Replace('            b.Icon = icon;', '            b.Icon = icon;' + [Environment]::NewLine + '            b.Primary = icon == StudioIcon.Plus;' + [Environment]::NewLine + '            b.Active = icon == StudioIcon.Plus;')
if (-not $actionBody.Contains('b.Primary = icon == StudioIcon.Plus;')) { throw 'Nao foi possivel destacar o botao Adicionar linha.' }
$actionBody = $actionBody.Replace('            b.Height = 34;', '            b.Height = 40;')
$shell = $shell.Substring(0, $actionMethod.Index) + $actionBody + $shell.Substring($actionMethod.Index + $actionMethod.Length)
$shell = $shell.Replace('navPanel.Visible = !navPanel.Visible;' + [Environment]::NewLine + '                miNav.Checked = navPanel.Visible;', 'navSplit.Panel1Collapsed = !navSplit.Panel1Collapsed;' + [Environment]::NewLine + '                miNav.Checked = !navSplit.Panel1Collapsed;')
$shell = $shell.Replace('if (inspector != null) inspector.Visible = inspectorAllowed && tab != null && tab.Key == "LD";', 'if (inspectorSplit != null) inspectorSplit.Panel2Collapsed = !(inspectorAllowed && tab != null && tab.Key == "LD");')
[IO.File]::WriteAllText($shellPath, $shell, [Text.Encoding]::UTF8)

Write-Host 'Visual V84 aplicado: paleta unificada, icones semanticos, acoes de linha no topo e layout redimensionavel.'
