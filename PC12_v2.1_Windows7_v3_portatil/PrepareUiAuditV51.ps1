$ErrorActionPreference = 'Stop'

$root = Get-Location
$uiPath = Join-Path $root 'StudioUi.build.cs'
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$ladderPath = Join-Path $root 'LadderEditor.build.cs'

foreach ($p in @($uiPath, $shellPath, $ladderPath)) {
    if (-not (Test-Path $p)) { throw "Arquivo de build nao encontrado: $p" }
}

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

# -----------------------------------------------------------------------------
# UI compartilhada: paleta petrol-blue, icones Ladder e bobina IEC correta.
# -----------------------------------------------------------------------------
$ui = LF ([System.IO.File]::ReadAllText($uiPath))
$ui = Replace-Required $ui '        public static readonly Color Shell = Color.FromArgb(18, 24, 31);' '        public static readonly Color Shell = Color.FromArgb(10, 31, 46);' 'UI Shell'
$ui = Replace-Required $ui '        public static readonly Color Chrome = Color.FromArgb(27, 36, 46);' '        public static readonly Color Chrome = Color.FromArgb(14, 42, 61);' 'UI Chrome'
$ui = Replace-Required $ui '        public static readonly Color ChromeLight = Color.FromArgb(38, 49, 62);' '        public static readonly Color ChromeLight = Color.FromArgb(24, 58, 79);' 'UI ChromeLight'
$ui = Replace-Required $ui '        public static readonly Color Border = Color.FromArgb(55, 68, 82);' '        public static readonly Color Border = Color.FromArgb(48, 76, 94);' 'UI Border'
$ui = Replace-Required $ui '        public static readonly Color Accent = Color.FromArgb(38, 166, 154);' '        public static readonly Color Accent = Color.FromArgb(47, 128, 237);' 'UI Accent'
$ui = Replace-Required $ui '        public static readonly Color AccentDark = Color.FromArgb(28, 128, 119);' '        public static readonly Color AccentDark = Color.FromArgb(35, 96, 178);' 'UI AccentDark'
$ui = Replace-Required $ui '        public static readonly Color Workspace = Color.FromArgb(244, 247, 250);' '        public static readonly Color Workspace = Color.FromArgb(248, 250, 252);' 'UI Workspace'
$ui = Replace-Required $ui '        public static readonly Color NavBg = Color.FromArgb(20, 27, 35);' '        public static readonly Color NavBg = Color.FromArgb(14, 42, 61);' 'UI NavBg'
$ui = Replace-Required $ui '        public static readonly Color NavHover = Color.FromArgb(31, 41, 52);' '        public static readonly Color NavHover = Color.FromArgb(27, 63, 85);' 'UI NavHover'
$ui = Replace-Required $ui '        public static readonly Color NavActive = Color.FromArgb(34, 46, 58);' '        public static readonly Color NavActive = Color.FromArgb(25, 72, 105);' 'UI NavActive'

$enumOld = @'
        None, Doc, Folder, Save, Undo, Plus, Minus, Check, Plug, Download,
        Refresh, Chip, Gear, Ladder, Convert, Terminal, Close, Bolt, Monitor, Grid
'@
$enumNew = @'
        None, Doc, Folder, Save, Undo, Redo, Plus, Minus, Check, Plug, Download,
        Refresh, Chip, Gear, Ladder, Convert, Terminal, Close, Bolt, Monitor, Grid,
        Select, ContactNO, ContactNC, Coil, Timer, Counter
'@
$ui = Replace-Required $ui $enumOld.TrimEnd() $enumNew.TrimEnd() 'enum StudioIcon'

$paletteUndoOld = '                case StudioIcon.Undo:     return Color.FromArgb(190, 132, 235);'
$paletteUndoNew = @'
                case StudioIcon.Undo:     return Color.FromArgb(145, 166, 255);
                case StudioIcon.Redo:     return Color.FromArgb(116, 184, 255);
'@
$ui = Replace-Required $ui $paletteUndoOld $paletteUndoNew.TrimEnd() 'paleta undo redo'

$paletteDefault = '                default:                  return StudioTheme.Fore;'
$paletteLadder = @'
                case StudioIcon.Select:    return Color.FromArgb(226, 232, 240);
                case StudioIcon.ContactNO: return Color.FromArgb(125, 211, 252);
                case StudioIcon.ContactNC: return Color.FromArgb(125, 211, 252);
                case StudioIcon.Coil:      return Color.FromArgb(251, 191, 36);
                case StudioIcon.Timer:     return Color.FromArgb(167, 139, 250);
                case StudioIcon.Counter:   return Color.FromArgb(244, 114, 182);
                default:                  return StudioTheme.Fore;
'@
$ui = Replace-Required $ui $paletteDefault $paletteLadder.TrimEnd() 'paleta Ladder'

$glyphAnchor = '                    case StudioIcon.Refresh:'
$glyphInsert = @'
                    case StudioIcon.Redo:
                        g.DrawArc(p, x + w * 0.16f, y + h * 0.20f, w * 0.68f, h * 0.62f, 50, -260);
                        g.FillPolygon(b, new PointF[] {
                            new PointF(x + w * 0.86f, y + h * 0.08f), new PointF(x + w * 0.88f, y + h * 0.50f),
                            new PointF(x + w * 0.52f, y + h * 0.32f) });
                        break;

                    case StudioIcon.Select:
                        g.FillPolygon(b, new PointF[] {
                            new PointF(x + w * 0.18f, y + h * 0.10f), new PointF(x + w * 0.78f, y + h * 0.58f),
                            new PointF(x + w * 0.52f, y + h * 0.61f), new PointF(x + w * 0.66f, y + h * 0.88f),
                            new PointF(x + w * 0.54f, y + h * 0.94f), new PointF(x + w * 0.39f, y + h * 0.67f),
                            new PointF(x + w * 0.18f, y + h * 0.86f) });
                        break;

                    case StudioIcon.ContactNO:
                    case StudioIcon.ContactNC:
                        g.DrawLine(p, x + w * 0.08f, cy, x + w * 0.32f, cy);
                        g.DrawLine(p, x + w * 0.38f, y + h * 0.18f, x + w * 0.38f, y + h * 0.82f);
                        g.DrawLine(p, x + w * 0.62f, y + h * 0.18f, x + w * 0.62f, y + h * 0.82f);
                        g.DrawLine(p, x + w * 0.68f, cy, x + w * 0.92f, cy);
                        if (icon == StudioIcon.ContactNC)
                            g.DrawLine(p, x + w * 0.31f, y + h * 0.82f, x + w * 0.69f, y + h * 0.18f);
                        break;

                    case StudioIcon.Coil:
                        g.DrawLine(p, x + w * 0.05f, cy, x + w * 0.27f, cy);
                        g.DrawArc(p, x + w * 0.24f, y + h * 0.16f, w * 0.31f, h * 0.68f, 90, 180);
                        g.DrawArc(p, x + w * 0.45f, y + h * 0.16f, w * 0.31f, h * 0.68f, -90, 180);
                        g.DrawLine(p, x + w * 0.73f, cy, x + w * 0.95f, cy);
                        break;

                    case StudioIcon.Timer:
                        g.DrawRectangle(p, x + w * 0.18f, y + h * 0.18f, w * 0.64f, h * 0.64f);
                        TextRenderer.DrawText(g, "T", StudioTheme.UiBold, Rectangle.Round(r), c,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        break;

                    case StudioIcon.Counter:
                        g.DrawRectangle(p, x + w * 0.18f, y + h * 0.18f, w * 0.64f, h * 0.64f);
                        TextRenderer.DrawText(g, "C", StudioTheme.UiBold, Rectangle.Round(r), c,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        break;

'@
$ui = Replace-Required $ui $glyphAnchor ($glyphInsert + $glyphAnchor) 'glifos Ladder'
[System.IO.File]::WriteAllText($uiPath, $ui, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Editor Ladder: bobina, Refazer e Adicionar/Remover linha apenas em Elementos.
# -----------------------------------------------------------------------------
$ladder = LF ([System.IO.File]::ReadAllText($ladderPath))
$redoFields = @'
        private readonly Stack<string> undoStack = new Stack<string>();
        private readonly Stack<string> redoStack = new Stack<string>();
'@
$ladder = Replace-Required $ladder '        private readonly Stack<string> undoStack = new Stack<string>();' $redoFields.TrimEnd() 'redoStack'

$commandOld = @'
            AddCommandButton(commandBar, "DESFAZER", x, 92, delegate { Undo(); }); x += 98;
            AddCommandButton(commandBar, "+ RUNG", x, 78, delegate { AddRung(); }); x += 84;
            AddCommandButton(commandBar, "- RUNG", x, 78, delegate { DeleteSelectedRung(); }); x += 88;
            AddCommandButton(commandBar, "VALIDAR", x, 92, delegate { ValidateProject(true); });
'@
$commandNew = @'
            AddCommandButton(commandBar, "DESFAZER", x, 92, delegate { Undo(); }); x += 98;
            AddCommandButton(commandBar, "REFAZER", x, 92, delegate { Redo(); }); x += 98;
            AddCommandButton(commandBar, "VALIDAR", x, 92, delegate { ValidateProject(true); });
'@
$ladder = Replace-Required $ladder $commandOld.TrimEnd() $commandNew.TrimEnd() 'toolbar Ladder'

$eraseOld = '            AddToolButton(toolbox, "×  Apagar", t, LadderTool.Erase); t += 48;'
$eraseNew = @'
            AddToolButton(toolbox, "×  Apagar", t, LadderTool.Erase); t += 42;
            AddToolActionButton(toolbox, "+  Adicionar linha", t, delegate { AddRung(); }, Color.FromArgb(72, 200, 136)); t += 38;
            AddToolActionButton(toolbox, "−  Remover linha", t, delegate { DeleteSelectedRung(); }, Color.FromArgb(224, 102, 102)); t += 48;
'@
$ladder = Replace-Required $ladder $eraseOld $eraseNew.TrimEnd() 'linhas em Elementos'

$helperAnchor = '        private void SetActiveTool(LadderTool tool)'
$helper = @'
        private void AddToolActionButton(Control parent, string text, int top, EventHandler action, Color accent)
        {
            FlatActionButton b = new FlatActionButton();
            b.Text = text;
            b.Location = new Point(10, top);
            b.Size = new Size(208, 34);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(10, 0, 0, 0);
            b.NormalColor = Navy;
            b.HoverColor = NavyLight;
            b.ForeColor = accent;
            b.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            b.Click += action;
            parent.Controls.Add(b);
        }

'@
$ladder = Replace-Required $ladder $helperAnchor ($helper + $helperAnchor) 'AddToolActionButton'

$coilOld = @'
                    g.DrawArc(p, new Rectangle(cx - 23, y - 15, 22, 30), -90, 180);
                    g.DrawArc(p, new Rectangle(cx + 1, y - 15, 22, 30), 90, 180);
'@
$coilNew = @'
                    g.DrawArc(p, new Rectangle(cx - 23, y - 15, 22, 30), 90, 180);
                    g.DrawArc(p, new Rectangle(cx + 1, y - 15, 22, 30), -90, 180);
'@
$ladder = Replace-Required $ladder $coilOld.TrimEnd() $coilNew.TrimEnd() 'bobina IEC'

$saveOld = @'
        private void SaveUndoState()
        {
            undoStack.Push(SerializeProject());
'@
$saveNew = @'
        private void SaveUndoState()
        {
            undoStack.Push(SerializeProject());
            redoStack.Clear();
'@
$ladder = Replace-Required $ladder $saveOld.TrimEnd() $saveNew.TrimEnd() 'redo reset'

$undoRedo = @'
        private void Undo()
        {
            if (undoStack.Count == 0) { statusLabel.Text = "Nada para desfazer."; return; }
            redoStack.Push(SerializeProject());
            DeserializeProject(undoStack.Pop());
            if (canvas.SelectedRung >= rungs.Count) canvas.SelectedRung = rungs.Count - 1;
            if (canvas.SelectedRung < 0) canvas.SelectedRung = 0;
            canvas.Invalidate();
            dirty = true;
            UpdateProjectLabel();
            statusLabel.Text = "Última alteração desfeita.";
        }

        private void Redo()
        {
            if (redoStack.Count == 0) { statusLabel.Text = "Nada para refazer."; return; }
            undoStack.Push(SerializeProject());
            DeserializeProject(redoStack.Pop());
            if (canvas.SelectedRung >= rungs.Count) canvas.SelectedRung = rungs.Count - 1;
            if (canvas.SelectedRung < 0) canvas.SelectedRung = 0;
            canvas.Invalidate();
            dirty = true;
            UpdateProjectLabel();
            statusLabel.Text = "Alteração refeita.";
        }

'@
$ladder = Replace-Section $ladder '        private void Undo()' '        private void MarkChanged(string message)' $undoRedo 'Undo Redo'
$ladder = $ladder.Replace('            undoStack.Clear();', "            undoStack.Clear();`n            redoStack.Clear();")
$keyOld = '            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.SuppressKeyPress = true; }'
$keyNew = @'
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.SuppressKeyPress = true; }
'@
$ladder = Replace-Required $ladder $keyOld $keyNew.TrimEnd() 'Ctrl+Y'
[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Shell: toolbar global enxuta e painel lateral unico Projeto/Elementos/Props.
# -----------------------------------------------------------------------------
$shell = LF ([System.IO.File]::ReadAllText($shellPath))
$shell = Replace-Required $shell '        private readonly Color Shell = Color.FromArgb(18, 24, 31);' '        private readonly Color Shell = Color.FromArgb(10, 31, 46);' 'Shell local'
$shell = Replace-Required $shell '        private readonly Color Chrome = Color.FromArgb(27, 36, 46);' '        private readonly Color Chrome = Color.FromArgb(14, 42, 61);' 'Chrome local'
$shell = Replace-Required $shell '        private readonly Color ChromeLight = Color.FromArgb(38, 49, 62);' '        private readonly Color ChromeLight = Color.FromArgb(24, 58, 79);' 'ChromeLight local'
$shell = Replace-Required $shell '        private readonly Color Border = Color.FromArgb(55, 68, 82);' '        private readonly Color Border = Color.FromArgb(48, 76, 94);' 'Border local'
$shell = Replace-Required $shell '        private readonly Color Accent = Color.FromArgb(38, 166, 154);' '        private readonly Color Accent = Color.FromArgb(47, 128, 237);' 'Accent local'
$shell = Replace-Required $shell '        private readonly Color AccentDark = Color.FromArgb(28, 128, 119);' '        private readonly Color AccentDark = Color.FromArgb(35, 96, 178);' 'AccentDark local'
$shell = Replace-Required $shell '        private readonly Color Workspace = Color.FromArgb(244, 247, 250);' '        private readonly Color Workspace = Color.FromArgb(248, 250, 252);' 'Workspace local'

$fieldOld = '        private readonly Dictionary<string, NavButton> navButtons = new Dictionary<string, NavButton>();'
$fieldNew = @'
        private readonly Dictionary<string, NavButton> navButtons = new Dictionary<string, NavButton>();
        private readonly Dictionary<string, NavButton> ladderToolButtons = new Dictionary<string, NavButton>();
        private Label selectionValue;
        private Label toolValue;
'@
$shell = Replace-Required $shell $fieldOld $fieldNew.TrimEnd() 'campos sidebar'

$versionPath = Join-Path $root 'version.txt'
$version = if (Test-Path $versionPath) { [System.IO.File]::ReadAllText($versionPath).Trim() } else { '0.50' }
$toolbar = @'
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 48;
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
            AddToolButton(bar, "Atualizar", StudioIcon.Refresh, false, delegate { ShowUpdater(); });
            AddToolButton(bar, "Configurações", StudioIcon.Gear, false, delegate { ShowDeviceManager(); });
            return bar;
        }

'@
$toolbar = $toolbar.Replace('__VERSION__', $version)
$shell = Replace-Section $shell '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar global'

$addRungMenu = '            editar.DropDownItems.Add(DropItem("Adicionar rung", delegate { InvokeLadder("AddRung", null); }));' + "`n"
$delRungMenu = '            editar.DropDownItems.Add(DropItem("Excluir rung", delegate { InvokeLadder("DeleteSelectedRung", null); }));' + "`n"
$shell = $shell.Replace($addRungMenu, '')
$shell = $shell.Replace($delRungMenu, '')

$menuUndoOld = '            editar.DropDownItems.Add(DropItem("Desfazer", delegate { InvokeLadder("Undo", null); }));'
$menuUndoNew = @'
            editar.DropDownItems.Add(DropItem("Desfazer", delegate { InvokeLadder("Undo", null); }));
            editar.DropDownItems.Add(DropItem("Refazer", delegate { InvokeLadder("Redo", null); }));
'@
$shell = Replace-Required $shell $menuUndoOld $menuUndoNew.TrimEnd() 'menu Refazer'

$propsOld = '            miProps = DropItem("Painel de propriedades", delegate { TogglePanel(1); });'
$propsNew = @'
            miProps = DropItem("Painel de propriedades", delegate { TogglePanel(1); });
            miProps.Visible = false;
'@
$shell = Replace-Required $shell $propsOld $propsNew.TrimEnd() 'ocultar painel direito no menu'

$nav = @'
        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 286;
            nav.BackColor = StudioTheme.NavBg;
            nav.AutoScroll = true;

            List<Control> items = new List<Control>();
            items.Add(BuildBrand());
            items.Add(new NavSection("Projeto"));
            items.Add(BuildSidebarProjectCard());
            items.Add(new NavSection("Elementos"));
            items.Add(ToolItem("Selecionar", StudioIcon.Select, LadderTool.Select));
            items.Add(ToolItem("Contato NA", StudioIcon.ContactNO, LadderTool.ContactNO));
            items.Add(ToolItem("Contato NF", StudioIcon.ContactNC, LadderTool.ContactNC));
            items.Add(ToolItem("Ramo paralelo NA", StudioIcon.ContactNO, LadderTool.ParallelNO));
            items.Add(ToolItem("Ramo paralelo NF", StudioIcon.ContactNC, LadderTool.ParallelNC));
            items.Add(ToolItem("Bobina", StudioIcon.Coil, LadderTool.Coil));
            items.Add(ToolItem("Temporizador", StudioIcon.Timer, LadderTool.Timer));
            items.Add(ToolItem("Contador", StudioIcon.Counter, LadderTool.Counter));
            items.Add(ToolItem("SET", StudioIcon.Check, LadderTool.Set));
            items.Add(ToolItem("RESET", StudioIcon.Refresh, LadderTool.Reset));
            items.Add(ToolItem("Borda de subida", StudioIcon.Bolt, LadderTool.EdgeUp));
            items.Add(ToolItem("Borda de descida", StudioIcon.Bolt, LadderTool.EdgeDown));
            items.Add(ToolItem("Função especial", StudioIcon.Chip, LadderTool.Function));
            items.Add(ToolItem("END", StudioIcon.Terminal, LadderTool.End));
            items.Add(ToolItem("Apagar elemento", StudioIcon.Minus, LadderTool.Erase));
            items.Add(SideAction("Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); }));
            items.Add(SideAction("Remover linha", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); }));
            items.Add(new NavSection("Propriedades"));
            items.Add(BuildSidebarPropertiesCard());

            for (int i = items.Count - 1; i >= 0; i--) nav.Controls.Add(items[i]);
            return nav;
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
            card.Dock = DockStyle.Top;
            card.Height = 132;
            card.BackColor = StudioTheme.NavBg;

            Label pc = InspectorLabel("Projeto atual", 7.6f, true, StudioTheme.Faint);
            pc.Location = new Point(18, 8); card.Controls.Add(pc);
            projectValue = InspectorLabel("Projeto sem nome", 9.4f, true, Fore);
            projectValue.Location = new Point(18, 28); projectValue.MaximumSize = new Size(246, 38); card.Controls.Add(projectValue);

            Label dc = InspectorLabel("Controlador", 7.6f, true, StudioTheme.Faint);
            dc.Location = new Point(18, 68); card.Controls.Add(dc);
            deviceValue = InspectorLabel("Nenhum controlador", 8.8f, true, Fore);
            deviceValue.Location = new Point(18, 87); deviceValue.MaximumSize = new Size(246, 24); card.Controls.Add(deviceValue);
            familyValue = InspectorLabel("-", 7.6f, false, Muted);
            familyValue.Location = new Point(18, 109); card.Controls.Add(familyValue);
            protocolValue = InspectorLabel("-", 7.6f, false, Muted);
            protocolValue.Location = new Point(132, 109); card.Controls.Add(protocolValue);

            supportValue = InspectorLabel("-", 7.6f, true, Muted); supportValue.Visible = false; card.Controls.Add(supportValue);
            capabilityValue = InspectorLabel("-", 7.6f, false, Muted); capabilityValue.Visible = false; card.Controls.Add(capabilityValue);
            connectionValue = InspectorLabel("● OFFLINE", 7.6f, true, Muted); connectionValue.Visible = false; card.Controls.Add(connectionValue);
            return card;
        }

        private Panel BuildSidebarPropertiesCard()
        {
            Panel card = new Panel();
            card.Dock = DockStyle.Top;
            card.Height = 112;
            card.BackColor = StudioTheme.NavBg;
            Label a = InspectorLabel("Seleção", 7.6f, true, StudioTheme.Faint);
            a.Location = new Point(18, 8); card.Controls.Add(a);
            selectionValue = InspectorLabel("Nenhum elemento selecionado", 8.5f, true, Fore);
            selectionValue.Location = new Point(18, 29); selectionValue.MaximumSize = new Size(246, 38); card.Controls.Add(selectionValue);
            Label b = InspectorLabel("Ferramenta ativa", 7.6f, true, StudioTheme.Faint);
            b.Location = new Point(18, 70); card.Controls.Add(b);
            toolValue = InspectorLabel("Selecionar", 8.4f, false, Muted);
            toolValue.Location = new Point(18, 89); card.Controls.Add(toolValue);
            return card;
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
$shell = Replace-Section $shell '        private Panel BuildNav()' '        private StudioPanel BuildConsole()' $nav 'painel lateral unico'

$inspector = @'
        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Width = 0;
            p.Height = 0;
            p.Visible = false;
            return p;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildInspector()' '        private Panel BuildStatusBar()' $inspector 'remover inspetor direito'

$prepare = @'
        private void PrepareLadderForStudio(LadderEditorForm form)
        {
            form.BackColor = Workspace;
            HideEmbeddedLadderChrome(form);
            CompactLadderControls(form);
            try
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo setTool = typeof(LadderEditorForm).GetMethod("SetActiveTool", flags);
                if (setTool != null) setTool.Invoke(form, new object[] { LadderTool.Select });
                FieldInfo cf = typeof(LadderEditorForm).GetField("canvas", flags);
                LadderCanvas lc = cf == null ? null : cf.GetValue(form) as LadderCanvas;
                if (lc != null) lc.SelectionChanged += delegate { RefreshLadderSelectionProperties(); };
            }
            catch { }
            RefreshLadderSelectionProperties();
        }

        private void HideEmbeddedLadderChrome(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Panel p = c as Panel;
                if (p != null)
                {
                    if (p.Dock == DockStyle.Top && (p.Height == 64 || p.Height == 58)) p.Visible = false;
                    else if (p.Dock == DockStyle.Bottom && p.Height <= 36) p.Visible = false;
                    else if (p.Dock == DockStyle.Left && p.Width >= 180 && p.Width <= 280) p.Visible = false;
                }
                if (c.HasChildren) HideEmbeddedLadderChrome(c);
            }
        }

        private void RefreshLadderSelectionProperties()
        {
            if (selectionValue == null || ladderForm == null || ladderForm.IsDisposed) return;
            try
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo cf = typeof(LadderEditorForm).GetField("canvas", flags);
                LadderCanvas lc = cf == null ? null : cf.GetValue(ladderForm) as LadderCanvas;
                MethodInfo get = typeof(LadderEditorForm).GetMethod("GetSelectedElement", flags);
                LadderElement el = get == null ? null : get.Invoke(ladderForm, null) as LadderElement;
                if (lc == null || lc.SelectedRung < 0) { selectionValue.Text = "Nenhum elemento selecionado"; return; }
                string pos = "Linha " + (lc.SelectedRung + 1).ToString() + " • Coluna " + (lc.SelectedColumn + 1).ToString();
                selectionValue.Text = el == null || el.Type == LadderElementType.Empty
                    ? pos + " • vazio"
                    : pos + " • " + el.Type.ToString() + (string.IsNullOrEmpty(el.Address) ? "" : "  " + el.Address);
            }
            catch { selectionValue.Text = "Seleção Ladder"; }
        }

'@
$shell = Replace-Section $shell '        private void PrepareLadderForStudio(LadderEditorForm form)' '        private void CompactLadderControls(Control root)' $prepare 'integracao Ladder'

$invokeOld = @'
                method.Invoke(ladderForm, args);
                UpdateProjectName();
'@
$invokeNew = @'
                method.Invoke(ladderForm, args);
                UpdateProjectName();
                RefreshLadderSelectionProperties();
'@
$shell = Replace-Required $shell $invokeOld.TrimEnd() $invokeNew.TrimEnd() 'refresh propriedades'
[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

Write-Host 'UI V51 aplicada: toolbar global, painel lateral unico, bobina correta, Refazer e linhas em Elementos.'
