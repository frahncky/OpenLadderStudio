$ErrorActionPreference = 'Stop'
# V94 - Redesenho da janela principal (etapa 2 do redesenho de interface).
# Roda depois do V89, no fim da cadeia. As pecas novas (cabecalho de painel, busca,
# paleta de instrucoes, arvore, zoom, barra de status) estao em StudioShellParts.cs;
# aqui o shell gerado passa a usa-las. Composicao:
#   - a marca sai do painel lateral (ja esta na barra de titulo);
#   - o painel lateral vira "PROJETO", com arvore no estilo de IDE e sem o cartao
#     "PROJETO ATUAL" (o resumo do controlador ja vive na barra de status);
#   - "INSTRUCOES" ganha busca com lupa e orientacao, grupos recolhiveis e itens compactos;
#   - "MENSAGENS" ganha cabecalho alinhado e botao Limpar proprio;
#   - a barra de status separa os dados por filetes finos;
#   - o zoom vira um controle segmentado no estilo da barra de ferramentas;
#   - menu e barra de titulo formam uma faixa so, e os menus suspensos usam Raised.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$uiPath = Join-Path $root 'StudioUi.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V94: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path -LiteralPath $uiPath)) { throw 'V94: StudioUi.build.cs nao encontrado.' }

function Read-Lf([string]$path) {
    $raw = [IO.File]::ReadAllText($path)
    return @{ Text = $raw.Replace("`r`n", "`n"); Crlf = $raw.Contains("`r`n") }
}

function Write-Back([string]$path, [string]$text, [bool]$crlf) {
    if ($crlf) { $text = $text.Replace("`n", "`r`n") }
    [IO.File]::WriteAllText($path, $text, [Text.Encoding]::UTF8)
}

function Rep([string]$text, [string]$old, [string]$new, [string]$label) {
    $old = $old.Replace("`r`n", "`n")
    $new = $new.Replace("`r`n", "`n")
    $first = $text.IndexOf($old, [StringComparison]::Ordinal)
    if ($first -lt 0) { throw "V94: ancora nao encontrada ($label). Ajuste PrepareShellRedesignV94.ps1." }
    if ($text.IndexOf($old, $first + 1, [StringComparison]::Ordinal) -ge 0) { throw "V94: ancora ambigua ($label)." }
    return $text.Replace($old, $new)
}

# Troca tudo entre duas ancoras de inicio de membro (a segunda fica).
function Replace-Region([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$new, [string]$label) {
    $a = $text.IndexOf($startAnchor, [StringComparison]::Ordinal)
    if ($a -lt 0) { throw "V94: inicio nao encontrado ($label). Ajuste PrepareShellRedesignV94.ps1." }
    if ($text.IndexOf($startAnchor, $a + 1, [StringComparison]::Ordinal) -ge 0) { throw "V94: inicio ambiguo ($label)." }
    $b = $text.IndexOf($endAnchor, $a + $startAnchor.Length, [StringComparison]::Ordinal)
    if ($b -lt 0) { throw "V94: fim nao encontrado ($label). Ajuste PrepareShellRedesignV94.ps1." }
    return $text.Substring(0, $a) + $new.Replace("`r`n", "`n") + $text.Substring($b)
}

$shellFile = Read-Lf $shellPath
$shell = $shellFile.Text

if ($shell.Contains('new ProjectTree()')) {
    Write-Host 'OpenLadder Studio: redesenho do shell V94 ja aplicado.' -ForegroundColor DarkGray
    return
}

# ---------------------------------------------------------------------------
# 1. Campos cujo tipo muda.
# ---------------------------------------------------------------------------
$shell = Rep $shell '        private Label modeText;' '        private StatusSegments modeText;' 'campo modeText'
$shell = Rep $shell '        private Label v74ZoomLabel;' '        private ZoomControl v74ZoomLabel;' 'campo zoom'
$shell = Rep $shell '        private TreeView v73ProjectTree;' '        private ProjectTree v73ProjectTree;' 'campo arvore'

# ---------------------------------------------------------------------------
# 2. Divisorias de painel: 4 px na cor da janela (os paineis ficam como cartoes),
#    no lugar das barras de 6 px na cor de borda.
# ---------------------------------------------------------------------------
$shell = Rep $shell '            navSplit.SplitterWidth = 6;' '            navSplit.SplitterWidth = 4;' 'divisoria do painel lateral'
$shell = Rep $shell '            navSplit.BackColor = Border;' '            navSplit.BackColor = Shell;' 'cor da divisoria lateral'
$shell = Rep $shell '            inspectorSplit.SplitterWidth = 6;' '            inspectorSplit.SplitterWidth = 4;' 'divisoria do inspetor'
$shell = Rep $shell '            inspectorSplit.BackColor = Border;' '            inspectorSplit.BackColor = Shell;' 'cor da divisoria do inspetor'

# ---------------------------------------------------------------------------
# 3. Menu: mesma faixa da barra de titulo; suspensos na superficie elevada.
# ---------------------------------------------------------------------------
$shell = Rep $shell '            menu.BackColor = Chrome;' '            menu.BackColor = Shell;' 'fundo do menu'
$shell = Rep $shell '            menu.Renderer = new ToolStripProfessionalRenderer(new UniversalStudioColorTable());' '            menu.Renderer = new OpenLadderToolStripRenderer();' 'renderizador do menu'
$shell = Rep $shell '            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            m.BackColor = Chrome;
            return m;' '            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            return m;' 'item de menu'
$shell = Rep $shell '            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            m.BackColor = Chrome;
            m.Click += click;' '            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            m.Click += click;' 'item suspenso'

# ---------------------------------------------------------------------------
# 4. Barra de ferramentas: separador centralizado e zoom segmentado.
# ---------------------------------------------------------------------------
$shell = Rep $shell '            sep.Bounds = new Rectangle(toolCursor + 7, 18, 1, 34);' '            sep.Bounds = new Rectangle(toolCursor + 7, 12, 1, 32);' 'separador da barra'

$zoom = @'
        private void V74AddZoomControls(Control bar)
        {
            ZoomControl zoom = new ZoomControl();
            zoom.Bounds = new Rectangle(toolCursor + 4, 12, 116, 32);
            zoom.MinusClick += delegate { V74Zoom("ZoomOut"); };
            zoom.PlusClick += delegate { V74Zoom("ZoomIn"); };
            zoom.ResetClick += delegate { V74Zoom("ZoomReset"); };
            toolbarTips.SetToolTip(zoom, "Zoom do diagrama (o percentual ajusta à largura disponível)");
            v74ZoomLabel = zoom;
            v74ZoomLabel.Text = V74ReadZoomPercent().ToString() + "%";
            bar.Controls.Add(zoom);
            toolCursor += 124;
        }
'@
$shell = Replace-Region $shell '        private void V74AddZoomControls(Control bar)' '        private void AddToolButtonTip(' $zoom 'zoom'

# ---------------------------------------------------------------------------
# 5. Painel lateral: sem marca e sem cartao; arvore de IDE.
# ---------------------------------------------------------------------------
$nav = @'
        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 252;
            nav.BackColor = StudioTheme.NavBg;

            // Os rotulos de projeto e controlador continuam sendo alimentados pelo resto
            // do shell; o resumo visivel mora na barra de status e na arvore.
            V73ProjectCard();

            v73ProjectTree = new ProjectTree();
            v73ProjectTree.Dock = DockStyle.Fill;

            v73ProjectRoot = new TreeNode("Projeto1");
            v73ProjectRoot.NodeFont = new Font("Segoe UI Semibold", 9.25f, FontStyle.Bold);
            v73ProjectRoot.Tag = "ROOT";

            TreeNode programs = new TreeNode("Programas"); programs.Tag = "GROUP";
            TreeNode main = new TreeNode("Main (PRG)"); main.Tag = "MAIN";
            TreeNode fun = new TreeNode("FUN"); fun.Tag = "INFO_FUN";
            TreeNode fb = new TreeNode("FB"); fb.Tag = "INFO_FB";
            programs.Nodes.Add(main); programs.Nodes.Add(fun); programs.Nodes.Add(fb);

            TreeNode variables = new TreeNode("Variáveis"); variables.Tag = "INFO_VARIABLES";
            TreeNode libraries = new TreeNode("Bibliotecas"); libraries.Tag = "INFO_LIBRARIES";

            v73ProjectRoot.Nodes.Add(programs);
            v73ProjectRoot.Nodes.Add(variables);
            v73ProjectRoot.Nodes.Add(libraries);
            v73ProjectTree.Nodes.Add(v73ProjectRoot);
            v73ProjectRoot.Expand();
            programs.Expand();

            v73ProjectTree.AfterSelect += delegate(object sender, TreeViewEventArgs e)
            {
                string key = e.Node == null || e.Node.Tag == null ? string.Empty : e.Node.Tag.ToString();
                if (key == "MAIN") ShowLadder();
                else if (key == "DEVICE") ShowDeviceManager();
                else if (key.StartsWith("INFO_", StringComparison.Ordinal))
                {
                    if (statusText != null) statusText.Text = e.Node.Text + ": estrutura reservada para expansão do projeto.";
                }
            };

            PanelHeader header = new PanelHeader("PROJETO");
            header.Icon = StudioIcon.Folder;

            nav.Controls.Add(v73ProjectTree);
            nav.Controls.Add(header);
            return nav;
        }

'@
$shell = Replace-Region $shell '        private Control BuildBrand()' '        private Panel V73ProjectCard()' $nav 'marca e painel lateral'

# ---------------------------------------------------------------------------
# 6. Mensagens: cabecalho alinhado e botao Limpar proprio.
# ---------------------------------------------------------------------------
$console = @'
        private StudioPanel BuildConsole()
        {
            StudioPanel wrap = new StudioPanel();
            wrap.Dock = DockStyle.Bottom;
            wrap.Height = 130;
            wrap.Fill = OpenLadderPalette.Chrome;
            wrap.Visible = true;

            console = new StudioConsole();
            console.Dock = DockStyle.Fill;
            ControlTheme.TrackScroll(console);
            wrap.Controls.Add(console);

            PanelHeader head = new PanelHeader("MENSAGENS");
            head.Raised = true;
            head.Icon = StudioIcon.Terminal;
            head.Height = 30;
            wrap.Controls.Add(head);

            GhostButton clear = new GhostButton("Limpar");
            clear.Width = clear.MeasureWidth();
            clear.AccessibleDescription = "Limpa as mensagens do console";
            clear.Click += delegate { console.Items.Clear(); };
            toolbarTips.SetToolTip(clear, "Limpar as mensagens");
            head.Controls.Add(clear);
            Action placeClear = delegate
            {
                clear.Location = new Point(Math.Max(0, head.ClientSize.Width - clear.Width - 8), Math.Max(0, (head.ClientSize.Height - clear.Height) / 2));
            };
            head.Resize += delegate { placeClear(); };
            placeClear();

            return wrap;
        }

'@
$shell = Replace-Region $shell '        private StudioPanel BuildConsole()' '        private Panel BuildInspector()' $console 'console'

# ---------------------------------------------------------------------------
# 7. Instrucoes: busca com lupa, grupos recolhiveis, itens compactos.
# ---------------------------------------------------------------------------
$palette = @'
        private Panel V73InstructionPanel()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = StudioTheme.NavBg;

            PanelHeader header = new PanelHeader("INSTRUÇÕES");
            header.Icon = StudioIcon.Ladder;

            Panel searchHost = new Panel();
            searchHost.Dock = DockStyle.Top;
            searchHost.Height = 48;
            searchHost.BackColor = StudioTheme.NavBg;
            searchHost.Padding = new Padding(12, 8, 12, 8);

            SearchBox search = new SearchBox();
            search.Placeholder = "Buscar instrução…";
            search.Dock = DockStyle.Fill;
            searchHost.Controls.Add(search);
            toolbarTips.SetToolTip(search.Input, "Filtrar as instruções pelo nome (Esc limpa)");
            search.AccessibleName = "Buscar instrução";
            search.Input.AccessibleName = "Buscar instrução";

            FlowLayoutPanel list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = StudioTheme.NavBg;
            list.Padding = new Padding(4, 2, 4, 8);
            ControlTheme.TrackScroll(list);

            List<PaletteSection> sections = new List<PaletteSection>();
            Dictionary<PaletteSection, List<PaletteItem>> members = new Dictionary<PaletteSection, List<PaletteItem>>();
            PaletteSection[] current = new PaletteSection[1];

            Action refresh = delegate
            {
                string q = (search.Text ?? string.Empty).Trim();
                foreach (PaletteSection s in sections)
                {
                    bool any = false;
                    foreach (PaletteItem it in members[s])
                    {
                        bool match = q.Length == 0 || it.Text.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
                        if (match) any = true;
                        it.Visible = match && (q.Length > 0 || !s.Collapsed);
                    }
                    s.Visible = q.Length == 0 || any;
                }
            };

            Action<string> addSection = delegate(string text)
            {
                PaletteSection s = new PaletteSection(text);
                s.Margin = new Padding(0, 6, 0, 0);
                s.Toggled += delegate { refresh(); };
                sections.Add(s);
                members[s] = new List<PaletteItem>();
                current[0] = s;
                list.Controls.Add(s);
            };

            Action<string, StudioIcon, EventHandler, string> addItem = delegate(string text, StudioIcon icon, EventHandler action, string tip)
            {
                PaletteItem b = new PaletteItem(text, icon);
                b.Margin = new Padding(0);
                if (action != null) b.Click += action;
                toolbarTips.SetToolTip(b, tip);
                members[current[0]].Add(b);
                list.Controls.Add(b);
            };

            string insertTip = " — escolha e clique no diagrama para inserir";
            addSection("EDIÇÃO");
            addItem("Selecionar / mover", StudioIcon.Select, delegate { V73SelectLadderTool(LadderTool.Select); }, "Selecionar / mover" + insertTip);
            addItem("Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); }, "Adicionar linha");
            addItem("Excluir linha selecionada", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); }, "Excluir linha selecionada");
            addItem("Apagar elemento selecionado", StudioIcon.Trash, delegate { InvokeLadder("DeleteSelectedElement", null); }, "Apagar elemento selecionado");

            addSection("CONTATOS");
            addItem("Contato NA", StudioIcon.ContactNO, delegate { V73SelectLadderTool(LadderTool.ContactNO); }, "Contato NA" + insertTip);
            addItem("Contato NF", StudioIcon.ContactNC, delegate { V73SelectLadderTool(LadderTool.ContactNC); }, "Contato NF" + insertTip);
            addItem("Ramo paralelo NA", StudioIcon.BranchNO, delegate { V73SelectLadderTool(LadderTool.ParallelNO); }, "Ramo paralelo NA" + insertTip);
            addItem("Ramo paralelo NF", StudioIcon.BranchNC, delegate { V73SelectLadderTool(LadderTool.ParallelNC); }, "Ramo paralelo NF" + insertTip);

            addSection("SAÍDAS");
            addItem("Bobina", StudioIcon.Coil, delegate { V73SelectLadderTool(LadderTool.Coil); }, "Bobina" + insertTip);
            addItem("SET", StudioIcon.CoilSet, delegate { V73SelectLadderTool(LadderTool.Set); }, "SET" + insertTip);
            addItem("RESET", StudioIcon.CoilReset, delegate { V73SelectLadderTool(LadderTool.Reset); }, "RESET" + insertTip);

            addSection("TEMPORIZAÇÃO E CONTAGEM");
            addItem("Temporizador", StudioIcon.Timer, delegate { V73SelectLadderTool(LadderTool.Timer); }, "Temporizador" + insertTip);
            addItem("Contador", StudioIcon.Counter, delegate { V73SelectLadderTool(LadderTool.Counter); }, "Contador" + insertTip);

            addSection("FUNÇÕES");
            addItem("Borda de subida", StudioIcon.EdgeUp, delegate { V73SelectLadderTool(LadderTool.EdgeUp); }, "Borda de subida" + insertTip);
            addItem("Borda de descida", StudioIcon.EdgeDown, delegate { V73SelectLadderTool(LadderTool.EdgeDown); }, "Borda de descida" + insertTip);
            addItem("Função especial", StudioIcon.Chip, delegate { V73SelectLadderTool(LadderTool.Function); }, "Função especial" + insertTip);
            addItem("END", StudioIcon.Terminal, delegate { V73SelectLadderTool(LadderTool.End); }, "END" + insertTip);

            search.TextChanged += delegate { refresh(); };

            Action fitPalette = delegate
            {
                int inner = Math.Max(120, list.ClientSize.Width - list.Padding.Horizontal);
                foreach (Control c in list.Controls) c.Width = inner;
            };
            list.SizeChanged += delegate { fitPalette(); };
            fitPalette();

            properties = new SelectionPropertiesCard();
            host.Controls.Add(list);
            host.Controls.Add(properties);
            host.Controls.Add(searchHost);
            host.Controls.Add(header);
            return host;
        }

'@
$shell = Replace-Region $shell '        private Panel V73InstructionPanel()' '        private void V73SelectLadderTool(LadderTool tool)' $palette 'paleta de instrucoes'

# ---------------------------------------------------------------------------
# 8. Barra de status: segmentos com filete fino.
# ---------------------------------------------------------------------------
$status = @'
        private Panel BuildStatusBar()
        {
            StatusBarPanel p = new StatusBarPanel();
            p.Dock = DockStyle.Bottom;
            p.Height = 28;
            p.Padding = new Padding(12, 1, 4, 0);

            statusText = new Label();
            statusText.Dock = DockStyle.Fill;
            statusText.TextAlign = ContentAlignment.MiddleLeft;
            statusText.Text = "Pronto para editar";
            statusText.ForeColor = Muted;
            statusText.Font = new Font("Segoe UI", 8.25f);
            p.Controls.Add(statusText);

            updateNotice = new LinkLabel();
            updateNotice.Dock = DockStyle.Right;
            updateNotice.Width = 230;
            updateNotice.TextAlign = ContentAlignment.MiddleRight;
            updateNotice.LinkColor = OpenLadderPalette.Ok;
            updateNotice.ActiveLinkColor = OpenLadderPalette.Accent;
            updateNotice.VisitedLinkColor = OpenLadderPalette.Ok;
            updateNotice.Font = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold);
            updateNotice.Visible = false;
            updateNotice.Cursor = Cursors.Hand;
            updateNotice.Click += delegate { StartUpdateNow(); };
            p.Controls.Add(updateNotice);

            modeText = new StatusSegments();
            modeText.Dock = DockStyle.Right;
            p.Controls.Add(modeText);
            return p;
        }
'@
$shell = Replace-Region $shell '        private Panel BuildStatusBar()' '        private void CheckForUpdatesInBackground()' $status 'barra de status'

$shell = Rep $shell '                modeText.Text = "PLC: " + model + protocol + "   │   ○ OFF-LINE   │   MODO: " + CurrentModeName() + "   │   ZOOM: " + zoom.ToString() + "%";' '                modeText.SetSegments(new string[] { "PLC: " + model + protocol, "○ OFF-LINE", "MODO: " + CurrentModeName(), "ZOOM: " + zoom.ToString() + "%" });' 'texto do modo'

# O no do controlador mostra so "fabricante modelo" (o icone de chip ja diz o que e);
# a familia e o protocolo ficam na dica do no.
$shell = Rep $shell '            deviceNode.Text = "Controlador: " + (currentProfile == null ? "nenhum" : currentProfile.Manufacturer + " " + currentProfile.Model);' '            deviceNode.Text = currentProfile == null ? "Nenhum controlador" : currentProfile.Manufacturer + " " + currentProfile.Model;
            deviceNode.ToolTipText = currentProfile == null
                ? "Nenhum controlador selecionado  —  clique para escolher"
                : "Controlador: " + currentProfile.Manufacturer + " " + currentProfile.Model
                    + "  |  Família: " + (string.IsNullOrEmpty(currentProfile.Family) ? "-" : currentProfile.Family)
                    + "  |  Protocolo: " + (string.IsNullOrEmpty(currentProfile.Protocol) ? "-" : currentProfile.Protocol)
                    + "  —  clique para escolher outro modelo";' 'no do controlador'
Write-Back $shellPath $shell $shellFile.Crlf

# ---------------------------------------------------------------------------
# 9. StudioUi: botao da barra com cantos suaves e foco da paleta; painel que
#    acompanha a troca de tema (Fill e BottomLine nao sao BackColor).
# ---------------------------------------------------------------------------
$uiFile = Read-Lf $uiPath
$ui = $uiFile.Text

$ui = Rep $ui '            Rectangle tile = new Rectangle(3, 3, Math.Max(1, Width - 6), Math.Max(1, Height - 6));
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, tile);

            if (Emphasis)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(42, semantic))) g.FillRectangle(b, tile);
                using (Pen p = new Pen(Color.FromArgb(150, semantic), 1f)) g.DrawRectangle(p, tile.X, tile.Y, tile.Width - 1, tile.Height - 1);
            }
            else if (hover)
            {
                using (Pen p = new Pen(StudioTheme.Border, 1f)) g.DrawRectangle(p, tile.X, tile.Y, tile.Width - 1, tile.Height - 1);
            }' '            Rectangle tile = new Rectangle(3, 3, Math.Max(1, Width - 6), Math.Max(1, Height - 6));
            using (SolidBrush b = new SolidBrush(StudioTheme.Chrome)) g.FillRectangle(b, ClientRectangle);
            StudioShape.Fill(g, tile, back);

            if (Emphasis)
            {
                StudioShape.Fill(g, tile, OpenLadderPalette.Tint(semantic, 42));
                StudioShape.Outline(g, tile, OpenLadderPalette.Tint(semantic, 150), 1.0f);
            }
            else if (hover)
            {
                StudioShape.Outline(g, tile, StudioTheme.Border, 1.0f);
            }' 'botao da barra'

$ui = Rep $ui '            if (Focused)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(6, 6, Math.Max(1, Width - 12), Math.Max(1, Height - 12)), StudioTheme.Fore, back);
        }
    }
    internal sealed class StudioTab' '            if (Focused)
                StudioShape.Outline(g, tile, OpenLadderPalette.Focus, OpenLadderMetrics.FocusWidth);
        }
    }
    internal sealed class StudioTab' 'foco do botao da barra'

$ui = Rep $ui '    internal sealed class StudioPanel : StudioControl
    {
        public Color Fill = StudioTheme.Chrome;
        public Color BottomLine = Color.Empty;
' '    internal sealed class StudioPanel : StudioControl, IPaletteRestylable
    {
        public Color Fill = StudioTheme.Chrome;
        public Color BottomLine = Color.Empty;

        public void RestylePalette(IDictionary<int, Color> map)
        {
            Fill = OpenLadderPalette.Swap(map, Fill);
            if (BottomLine != Color.Empty) BottomLine = OpenLadderPalette.Swap(map, BottomLine);
            Invalidate();
        }
' 'painel que acompanha o tema'

Write-Back $uiPath $ui $uiFile.Crlf
Write-Host 'OpenLadder Studio: redesenho do shell V94 aplicado (painel Projeto, paleta de instrucoes, status, zoom).' -ForegroundColor Cyan
