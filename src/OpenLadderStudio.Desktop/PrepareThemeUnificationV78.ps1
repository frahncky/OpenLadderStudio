$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$uiPath = Join-Path $root 'StudioUi.build.cs'

if (-not (Test-Path $shellPath)) { throw 'V78: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $uiPath)) { throw 'V78: StudioUi.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)
$ui = [System.IO.File]::ReadAllText($uiPath)

# ---------------------------------------------------------------------------
# 1. StudioTheme passa a ler a paleta central (OpenLadderPalette, em
#    AppBranding.cs). Ate a V68 o tema era fixado por substituicao de literal,
#    o que travava o produto em um unico tema e valia so para estes dois
#    arquivos: todas as outras janelas continuavam na paleta antiga. Trocando
#    os campos por propriedades, shell, editor, simulador e ferramentas passam
#    a responder ao mesmo tema, escolhido pelo usuario.
#
#    A cadeia V20 -> V21 -> V51 -> V68 continua rodando antes e intacta: os
#    literais que ela produz sao descartados aqui, no fim. Isso preserva as
#    ancoras textuais de que aqueles scripts dependem.
# ---------------------------------------------------------------------------
$themeFields = 0
$ui = [regex]::Replace($ui, '(?m)^(\s*)public static readonly Color ([A-Za-z]+) = Color\.FromArgb\([0-9]+, [0-9]+, [0-9]+\);(\r?)$', {
    param($m)
    $script:themeFields++
    # O grupo 3 devolve o \r: consumi-lo deixaria o arquivo com fim de linha misto.
    return $m.Groups[1].Value + 'public static Color ' + $m.Groups[2].Value + ' { get { return OpenLadderPalette.' + $m.Groups[2].Value + '; } }' + $m.Groups[3].Value
})
if ($themeFields -lt 15) { throw "V78: StudioTheme tinha $themeFields cores; esperado ao menos 15." }

# O shell e a tabela de menus tambem mantinham campos locais fixos. Resolver
# esses campos no fim da cadeia evita misturar paineis claros com editor escuro.
$shellColors = @{
    Shell = 'Shell'; Chrome = 'Chrome'; ChromeLight = 'ChromeLight'
    Border = 'Border'; Accent = 'Accent'; AccentDark = 'AccentDark'
    Workspace = 'Workspace'; Fore = 'Fore'; Muted = 'Muted'; Disabled = 'Disabled'
    hover = 'NavHover'
}
$shellFields = 0
$shell = [regex]::Replace($shell, '(?m)^(\s*)private readonly Color ([A-Za-z]+) = Color\.FromArgb\([0-9]+, [0-9]+, [0-9]+\);(\r?)$', {
    param($m)
    $name = $m.Groups[2].Value
    if (-not $shellColors.ContainsKey($name)) { return $m.Value }
    $script:shellFields++
    return $m.Groups[1].Value + 'private Color ' + $name + ' { get { return OpenLadderPalette.' + $shellColors[$name] + '; } }' + $m.Groups[3].Value
})
if ($shellFields -lt 10) { throw "V78: apenas $shellFields campos do shell receberam a paleta." }
$shell = $shell.Replace('return Color.FromArgb(43, 126, 84);', 'return OpenLadderPalette.NavActive;')
# Superficies e estados residuais introduzidos pelos scripts anteriores.
# Estas trocas ficam no fim para preservar as ancoras da cadeia historica.
$surfaceColors = @{
    'Color.FromArgb(255, 255, 255)' = 'OpenLadderPalette.Chrome'
    'Color.FromArgb(245, 248, 252)' = 'OpenLadderPalette.Shell'
    'Color.FromArgb(69, 190, 129)' = 'OpenLadderPalette.Ok'
    'Color.FromArgb(73, 92, 113)' = 'OpenLadderPalette.Muted'
    'Color.FromArgb(251, 191, 36)' = 'OpenLadderPalette.Warning'
    'Color.FromArgb(30, 45, 58)' = 'OpenLadderPalette.Fore'
    'Color.FromArgb(80, 92, 104)' = 'OpenLadderPalette.Muted'
    'Color.FromArgb(215, 166, 71)' = 'OpenLadderPalette.Warning'
}
foreach ($literal in $surfaceColors.Keys) {
    $shell = $shell.Replace($literal, $surfaceColors[$literal])
}
$shell = $shell.Replace('BackColor = Color.White;', 'BackColor = OpenLadderPalette.ChromeLight;')
$shell = $shell.Replace('ActiveLinkColor = Color.White;', 'ActiveLinkColor = OpenLadderPalette.Accent;')
$shell = $shell.Replace('new Rectangle(25, 21, 22, 22), Color.White)', 'new Rectangle(25, 21, 22, 22), OpenLadderPalette.OnAccent)')
$ui = $ui.Replace('BackColor = Color.FromArgb(17, 23, 30);', 'BackColor = OpenLadderPalette.Workspace;')

# ---------------------------------------------------------------------------
# 2. Paleta semantica dos icones. A V51 calibrou para fundo escuro e a V68
#    recalibrou para fundo claro; com os dois temas disponiveis as duas
#    calibragens precisam coexistir. A tabela guarda o par (escuro, claro) e a
#    escolha passa a ser feita em tempo de execucao.
# ---------------------------------------------------------------------------
$iconDuo = @{
    'Doc'       = @(91, 170, 245, 33, 118, 199)
    'Folder'    = @(238, 186, 76, 176, 106, 15)
    'Save'      = @(78, 201, 176, 13, 128, 128)
    'Undo'      = @(145, 166, 255, 108, 92, 196)
    'Redo'      = @(116, 184, 255, 82, 104, 198)
    'Plus'      = @(80, 200, 120, 47, 133, 71)
    'Minus'     = @(224, 102, 102, 196, 54, 54)
    'Check'     = @(72, 200, 136, 47, 133, 71)
    'Plug'      = @(244, 164, 96, 191, 92, 20)
    'Download'  = @(88, 166, 230, 24, 116, 190)
    'Refresh'   = @(100, 149, 237, 44, 110, 200)
    'Chip'      = @(74, 169, 229, 20, 122, 168)
    'Gear'      = @(172, 150, 220, 98, 90, 148)
    'Convert'   = @(80, 190, 205, 12, 124, 138)
    'Terminal'  = @(158, 186, 96, 92, 110, 40)
    'Bolt'      = @(245, 190, 72, 176, 120, 12)
    'Monitor'   = @(67, 192, 201, 13, 126, 132)
    'Grid'      = @(132, 164, 215, 78, 96, 158)
    'Select'    = @(226, 232, 240, 74, 90, 106)
    'ContactNO' = @(125, 211, 252, 20, 122, 178)
    'ContactNC' = @(125, 211, 252, 20, 122, 178)
    'BranchNO'  = @(125, 211, 252, 20, 122, 178)
    'BranchNC'  = @(125, 211, 252, 20, 122, 178)
    'EdgeUp'    = @(125, 211, 252, 20, 122, 178)
    'EdgeDown'  = @(125, 211, 252, 20, 122, 178)
    'Coil'      = @(251, 191, 36, 176, 120, 12)
    'CoilSet'   = @(251, 191, 36, 176, 120, 12)
    'CoilReset' = @(251, 191, 36, 176, 120, 12)
    'Timer'     = @(167, 139, 250, 108, 92, 196)
    'Counter'   = @(244, 114, 182, 190, 62, 128)
    'Trash'     = @(224, 102, 102, 196, 54, 54)
    'Ladder'    = @(125, 211, 252, 20, 122, 178)
    'Close'     = @(224, 102, 102, 196, 54, 54)
}

$iconCases = 0
$ui = [regex]::Replace($ui, 'case StudioIcon\.([A-Za-z]+):(\s+)return Color\.FromArgb\([0-9]+, [0-9]+, [0-9]+\);', {
    param($m)
    $name = $m.Groups[1].Value
    if (-not $iconDuo.ContainsKey($name)) { return $m.Value }
    $script:iconCases++
    $v = $iconDuo[$name]
    return 'case StudioIcon.' + $name + ':' + $m.Groups[2].Value + 'return OpenLadderPalette.Duo(' + ($v -join ', ') + ');'
})
if ($iconCases -lt 20) { throw "V78: apenas $iconCases icones receberam par de cores; esperado ao menos 20." }

# ---------------------------------------------------------------------------
# 3. Menu Exibir -> Tema. A troca vale na hora: o OpenLadderPalette.Use percorre
#    as janelas abertas trocando cada cor do tema que sai pela equivalente do que
#    entra. Antes daqui a paleta so mudava em memoria, a tela continuava igual e o
#    usuario tinha de reiniciar o programa para ver alguma coisa.
# ---------------------------------------------------------------------------
$themeMenuMethod = @'
        private void ApplyThemeChoice(OpenLadderThemeMode mode)
        {
            if (OpenLadderPalette.Mode == mode) return;
            OpenLadderPalette.Use(mode);
            if (statusText != null)
            {
                statusText.Text = mode == OpenLadderThemeMode.Dark
                    ? "Tema escuro aplicado."
                    : "Tema claro aplicado.";
            }
        }

        private void SetRailEnabled
'@
if (-not $shell.Contains('private void ApplyThemeChoice(')) {
    $anchor = '        private void SetRailEnabled'
    if (-not $shell.Contains($anchor)) { throw 'V78: ancora SetRailEnabled nao encontrada no shell.' }
    $index = $shell.IndexOf($anchor)
    $shell = $shell.Substring(0, $index) + $themeMenuMethod.Replace("`r`n", [Environment]::NewLine) + $shell.Substring($index + $anchor.Length)
}

$exibirAnchor = '            menu.Items.Add(exibir);'
if (-not $shell.Contains('MenuItem("Tema")')) {
    if (-not $shell.Contains($exibirAnchor)) { throw 'V78: menu Exibir nao encontrado no shell.' }
    $themeItems = @'
            ToolStripMenuItem tema = MenuItem("Tema");
            tema.DropDownItems.Add(DropItem("Escuro", delegate { ApplyThemeChoice(OpenLadderThemeMode.Dark); }));
            tema.DropDownItems.Add(DropItem("Claro", delegate { ApplyThemeChoice(OpenLadderThemeMode.Light); }));
            exibir.DropDownItems.Add(new ToolStripSeparator());
            exibir.DropDownItems.Add(tema);

'@
    $shell = $shell.Replace($exibirAnchor, $themeItems.Replace("`r`n", [Environment]::NewLine) + $exibirAnchor)
}

# ---------------------------------------------------------------------------
# 4. A ferramenta do editor virou de uso unico (volta ao ponteiro sozinha), entao
#    o rodape do shell nao pode mais anunciar uma ferramenta presa.
# ---------------------------------------------------------------------------
$oldStatus = 'if (statusText != null) statusText.Text = "Ferramenta: " + tool.ToString();'
$newStatus = 'if (statusText != null) statusText.Text = tool == LadderTool.Select' + [Environment]::NewLine +
             '                    ? "Mouse livre — clique para selecionar, duplo clique para editar."' + [Environment]::NewLine +
             '                    : "Clique no rung para inserir. Ctrl insere em sequência • botão direito ou Esc solta.";'
$shell = $shell.Replace($oldStatus, $newStatus)

# ---------------------------------------------------------------------------
# 5. Aresta da barra superior. Ela usava a mesma cor de borda de todo divisor
#    interno, entao a moldura de comando nao se distinguia do conteudo: o topo
#    lia como mais uma divisao entre paineis. Uma cor propria, um passo mais
#    forte, separa o que e comando do que e documento.
# ---------------------------------------------------------------------------
$shell = $shell.Replace('            bar.BottomLine = Border;', '            bar.BottomLine = OpenLadderPalette.HeaderLine;')
$shell = $shell.Replace('            brand.BottomLine = Border;', '            brand.BottomLine = OpenLadderPalette.HeaderLine;')

# O canvas e redesenhado pela V57/V74; suas cores finais precisam passar pela
# mesma paleta, inclusive na compilacao do editor como ferramenta separada.
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path $ladderPath)) { throw 'V78: LadderEditor.build.cs nao encontrado.' }
$ladder = [System.IO.File]::ReadAllText($ladderPath)
$ladderColors = @{
    'Color.White' = 'OpenLadderPalette.Canvas'
    'Color.FromArgb(72, 200, 136)' = 'OpenLadderPalette.Ok'
    'Color.FromArgb(224, 102, 102)' = 'OpenLadderPalette.Danger'
    'Color.FromArgb(232, 237, 242)' = 'OpenLadderPalette.GridLine'
    'Color.FromArgb(248, 250, 253)' = 'OpenLadderPalette.Chrome'
    'Color.FromArgb(132, 145, 158)' = 'OpenLadderPalette.Faint'
    'Color.FromArgb(32, 53, 70)' = 'OpenLadderPalette.Rail'
    'Color.FromArgb(248, 251, 254)' = 'OpenLadderPalette.Chrome'
    'Color.FromArgb(241, 244, 247)' = 'OpenLadderPalette.GridLine'
    'Color.FromArgb(48, 65, 78)' = 'OpenLadderPalette.Wire'
    'Color.FromArgb(225, 239, 252)' = 'OpenLadderPalette.SelectionFill'
    'Color.FromArgb(245, 248, 250)' = 'OpenLadderPalette.Chrome'
    'Color.FromArgb(47, 128, 237)' = 'OpenLadderPalette.SelectionEdge'
    'Color.FromArgb(220, 226, 232)' = 'OpenLadderPalette.Border'
    'Color.FromArgb(35, 96, 178)' = 'OpenLadderPalette.Accent'
    'Color.FromArgb(112, 126, 140)' = 'OpenLadderPalette.Muted'
    'Color.FromArgb(247, 250, 253)' = 'OpenLadderPalette.NavHover'
    'Color.FromArgb(203, 213, 223)' = 'OpenLadderPalette.Border'
    'Color.FromArgb(232, 243, 253)' = 'OpenLadderPalette.SelectionFill'
    'Color.FromArgb(31, 48, 62)' = 'OpenLadderPalette.Fore'
    'Color.FromArgb(25, 105, 145)' = 'OpenLadderPalette.Info'
    'Color.FromArgb(248, 250, 252)' = 'OpenLadderPalette.ChromeLight'
    'Color.FromArgb(86, 105, 120)' = 'OpenLadderPalette.Rail'
    'Color.FromArgb(82, 98, 112)' = 'OpenLadderPalette.Muted'
}
foreach ($literal in $ladderColors.Keys) {
    $ladder = $ladder.Replace($literal, $ladderColors[$literal])
}
[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($uiPath, $ui, [System.Text.Encoding]::UTF8)
Write-Host "OpenLadder Studio: tema unificado V78 ($themeFields cores, $iconCases icones)." -ForegroundColor Cyan
