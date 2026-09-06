$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$uiPath = Join-Path $root 'StudioUi.build.cs'

function Replace-Required([string]$text, [string]$old, [string]$new, [string]$label) {
    if (-not $text.Contains($old)) { throw "V68: trecho nao encontrado: $label" }
    return $text.Replace($old, $new)
}

if (-not (Test-Path $shellPath)) { throw 'V68: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $uiPath)) { throw 'V68: StudioUi.build.cs nao encontrado.' }

$shell = Get-Content $shellPath -Raw
$ui = Get-Content $uiPath -Raw

# Paleta clara inspirada na interface aprovada: branco, azul institucional e estados semaforicos.
$replacements = @(
    @('Color.FromArgb(29, 31, 34)', 'Color.FromArgb(248, 250, 253)'),
    @('Color.FromArgb(37, 39, 43)', 'Color.FromArgb(255, 255, 255)'),
    @('Color.FromArgb(47, 50, 55)', 'Color.FromArgb(238, 244, 252)'),
    @('Color.FromArgb(61, 64, 69)', 'Color.FromArgb(207, 218, 230)'),
    @('Color.FromArgb(45, 170, 107)', 'Color.FromArgb(28, 105, 210)'),
    @('Color.FromArgb(34, 135, 83)', 'Color.FromArgb(18, 78, 160)'),
    @('Color.FromArgb(235, 238, 241)', 'Color.FromArgb(250, 252, 255)'),
    @('Color.FromArgb(226, 230, 234)', 'Color.FromArgb(30, 44, 62)'),
    @('Color.FromArgb(150, 157, 164)', 'Color.FromArgb(83, 101, 122)'),
    @('Color.FromArgb(24, 26, 29)', 'Color.FromArgb(245, 248, 252)'),
    @('Color.FromArgb(40, 43, 47)', 'Color.FromArgb(235, 242, 250)'),
    @('Color.FromArgb(45, 49, 54)', 'Color.FromArgb(224, 236, 251)'),
    @('Color.FromArgb(108, 116, 124)', 'Color.FromArgb(111, 129, 149)'),
    @('Color.FromArgb(92, 97, 103)', 'Color.FromArgb(160, 172, 185)'),
    @('Color.FromArgb(22, 24, 27)', 'Color.FromArgb(255, 255, 255)'),
    @('Color.FromArgb(52, 55, 60)', 'Color.FromArgb(235, 242, 250)'),
    @('Color.FromArgb(43, 126, 84)', 'Color.FromArgb(215, 231, 250)')
)
foreach ($pair in $replacements) {
    $shell = $shell.Replace($pair[0], $pair[1])
    $ui = $ui.Replace($pair[0], $pair[1])
}

# Identidade e versao.
$shell = $shell.Replace('OpenLadder Studio  v0.12', 'OpenLadder Studio  v0.68')
$shell = $shell.Replace('OpenLadder Studio v0.12', 'OpenLadder Studio v0.68')

# Menu completo como no conceito visual aprovado.
$oldMenu = @'
            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(exibir);
            menu.Items.Add(plc);
            menu.Items.Add(ferramentas);
            menu.Items.Add(ajuda);
'@
$newMenu = @'
            ToolStripMenuItem projeto = MenuItem("Projeto");
            projeto.DropDownItems.Add(DropItem("Novo projeto", delegate { InvokeLadder("NewProject", new object[] { true }); }));
            projeto.DropDownItems.Add(DropItem("Abrir projeto...", delegate { InvokeLadder("OpenProject", null); }));
            projeto.DropDownItems.Add(new ToolStripSeparator());
            projeto.DropDownItems.Add(DropItem("Compilar / validar", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));

            ToolStripMenuItem simulacao = MenuItem("Simulador");
            simulacao.DropDownItems.Add(DropItem("Abrir simulador", delegate { ShowSimulator(); }));
            simulacao.DropDownItems.Add(DropItem("Monitor online", delegate { ShowMonitor(); }));

            ToolStripMenuItem janela = MenuItem("Janela");
            janela.DropDownItems.Add(DropItem("Navegacao", delegate { TogglePanel(0); }));
            janela.DropDownItems.Add(DropItem("Propriedades", delegate { TogglePanel(1); }));
            janela.DropDownItems.Add(DropItem("Mensagens", delegate { TogglePanel(2); }));

            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(exibir);
            menu.Items.Add(projeto);
            menu.Items.Add(plc);
            menu.Items.Add(simulacao);
            menu.Items.Add(ferramentas);
            menu.Items.Add(janela);
            menu.Items.Add(ajuda);
'@
$shell = Replace-Required $shell $oldMenu $newMenu 'menu principal'

# Toolbar: nomes e agrupamento mais proximos do mockup aprovado, sem trocar a logica existente.
$shell = $shell.Replace('AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });', 'AddToolButton(bar, "Compilar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });')
$shell = $shell.Replace('AddToolButton(bar, "Comunicação", StudioIcon.Plug, false, delegate { ShowCommunication(); });', 'AddToolButton(bar, "Transferir", StudioIcon.Download, false, delegate { ShowCommunication(); });')
$oldMonitor = 'AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });'
$newMonitor = $oldMonitor + "`r`n            AddToolButton(bar, \"Simulador\", StudioIcon.Bolt, false, delegate { ShowSimulator(); });"
if ($shell.Contains($oldMonitor) -and -not $shell.Contains('"Simulador", StudioIcon.Bolt')) { $shell = $shell.Replace($oldMonitor, $newMonitor) }

# Barra mais alta e respirada.
$shell = $shell.Replace('bar.Height = 60;', 'bar.Height = 72;')
$shell = $shell.Replace('b.Height = 54;', 'b.Height = 64;')
$shell = $shell.Replace('b.Location = new Point(toolCursor, 3);', 'b.Location = new Point(toolCursor, 4);')
$shell = $shell.Replace('sep.Bounds = new Rectangle(toolCursor + 7, 15, 1, 30);', 'sep.Bounds = new Rectangle(toolCursor + 7, 18, 1, 34);')

# Icones coloridos por funcao, mantendo desenho vetorial GDI+ e compatibilidade com Windows 7.
$needle = @'
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = pressed ? StudioTheme.Shell : hover ? StudioTheme.ChromeLight : StudioTheme.Chrome;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Color fore = Emphasis ? StudioTheme.Accent : StudioTheme.Fore;
'@
$replacement = @'
        private static Color ToolColor(StudioIcon icon)
        {
            switch (icon)
            {
                case StudioIcon.Doc: return Color.FromArgb(36, 127, 206);
                case StudioIcon.Folder: return Color.FromArgb(237, 164, 24);
                case StudioIcon.Save: return Color.FromArgb(31, 98, 184);
                case StudioIcon.Check: return Color.FromArgb(35, 155, 86);
                case StudioIcon.Download: return Color.FromArgb(39, 105, 218);
                case StudioIcon.Bolt: return Color.FromArgb(22, 153, 103);
                case StudioIcon.Monitor: return Color.FromArgb(35, 108, 210);
                case StudioIcon.Gear: return Color.FromArgb(222, 70, 63);
                case StudioIcon.Undo: return Color.FromArgb(49, 112, 213);
                case StudioIcon.Refresh: return Color.FromArgb(93, 110, 130);
                case StudioIcon.Plus: return Color.FromArgb(35, 155, 86);
                case StudioIcon.Minus: return Color.FromArgb(48, 112, 205);
                default: return StudioTheme.Accent;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = pressed ? StudioTheme.Shell : hover ? StudioTheme.ChromeLight : StudioTheme.Chrome;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Color fore = Emphasis ? StudioTheme.Accent : ToolColor(Icon);
'@
$ui = Replace-Required $ui $needle $replacement 'cores dos icones da toolbar'

# Toolbar com texto escuro e legivel no tema claro.
$ui = $ui.Replace('Emphasis ? StudioTheme.Accent : StudioTheme.Muted,', 'Emphasis ? StudioTheme.Accent : StudioTheme.Fore,')

Set-Content -Path $shellPath -Value $shell -Encoding UTF8
Set-Content -Path $uiPath -Value $ui -Encoding UTF8
Write-Host 'OpenLadder Studio v0.68: interface visual aplicada.' -ForegroundColor Cyan
