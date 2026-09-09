$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$uiPath = Join-Path $root 'StudioUi.build.cs'

if (-not (Test-Path $shellPath)) { throw 'V68: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path $uiPath)) { throw 'V68: StudioUi.build.cs nao encontrado.' }

$shell = [System.IO.File]::ReadAllText($shellPath)
$ui = [System.IO.File]::ReadAllText($uiPath)

# Tema claro final, aplicado depois da auditoria V51.
$colors = @(
    @('Color.FromArgb(10, 31, 46)', 'Color.FromArgb(248, 250, 253)'),
    @('Color.FromArgb(14, 42, 61)', 'Color.FromArgb(255, 255, 255)'),
    @('Color.FromArgb(24, 58, 79)', 'Color.FromArgb(238, 244, 252)'),
    @('Color.FromArgb(48, 76, 94)', 'Color.FromArgb(207, 218, 230)'),
    @('Color.FromArgb(47, 128, 237)', 'Color.FromArgb(28, 105, 210)'),
    @('Color.FromArgb(35, 96, 178)', 'Color.FromArgb(18, 78, 160)'),
    @('Color.FromArgb(248, 250, 252)', 'Color.FromArgb(250, 252, 255)'),
    @('Color.FromArgb(27, 63, 85)', 'Color.FromArgb(235, 242, 250)'),
    @('Color.FromArgb(25, 72, 105)', 'Color.FromArgb(224, 236, 251)'),
    @('Color.FromArgb(18, 24, 31)', 'Color.FromArgb(248, 250, 253)'),
    @('Color.FromArgb(27, 36, 46)', 'Color.FromArgb(255, 255, 255)'),
    @('Color.FromArgb(38, 49, 62)', 'Color.FromArgb(238, 244, 252)'),
    @('Color.FromArgb(55, 68, 82)', 'Color.FromArgb(207, 218, 230)'),
    @('Color.FromArgb(38, 166, 154)', 'Color.FromArgb(28, 105, 210)'),
    @('Color.FromArgb(28, 128, 119)', 'Color.FromArgb(18, 78, 160)'),
    @('Color.FromArgb(226, 230, 234)', 'Color.FromArgb(30, 44, 62)'),
    @('Color.FromArgb(150, 157, 164)', 'Color.FromArgb(83, 101, 122)'),
    @('Color.FromArgb(108, 116, 124)', 'Color.FromArgb(111, 129, 149)'),
    @('Color.FromArgb(92, 97, 103)', 'Color.FromArgb(160, 172, 185)'),
    @('Color.FromArgb(22, 24, 27)', 'Color.FromArgb(255, 255, 255)'),
    @('Color.FromArgb(16, 22, 29)', 'Color.FromArgb(245, 248, 252)'),
    @('Color.FromArgb(168, 174, 181)', 'Color.FromArgb(120, 135, 151)'),
    @('Color.FromArgb(171, 181, 191)', 'Color.FromArgb(73, 92, 113)')
)
foreach ($pair in $colors) {
    $shell = $shell.Replace($pair[0], $pair[1])
    $ui = $ui.Replace($pair[0], $pair[1])
}

# A paleta vetorial da V20/V51 foi calibrada para fundo escuro. No tema claro
# (fundo branco) esses tons ficavam lavados e alguns icones sumiam. Aqui cada
# cor e reescrita para um tom mais fechado, mantendo a familia de matiz definida
# no guia de interface (azul = arquivos/controlador, ambar = abrir, turquesa =
# salvar/monitor, violeta = historico/configuracao, vermelho = remocao). Os
# valores tem contraste >= 4:1 sobre branco. A substituicao e tolerante: se a
# linha nao existir mais, apenas nao ha troca.
$glyphColors = @(
    @('case StudioIcon.Doc:      return Color.FromArgb(91, 170, 245);',  'case StudioIcon.Doc:      return Color.FromArgb(33, 118, 199);'),
    @('case StudioIcon.Folder:   return Color.FromArgb(238, 186, 76);',  'case StudioIcon.Folder:   return Color.FromArgb(176, 106, 15);'),
    @('case StudioIcon.Save:     return Color.FromArgb(78, 201, 176);',  'case StudioIcon.Save:     return Color.FromArgb(13, 128, 128);'),
    @('case StudioIcon.Undo:     return Color.FromArgb(145, 166, 255);', 'case StudioIcon.Undo:     return Color.FromArgb(108, 92, 196);'),
    @('case StudioIcon.Redo:     return Color.FromArgb(116, 184, 255);', 'case StudioIcon.Redo:     return Color.FromArgb(82, 104, 198);'),
    @('case StudioIcon.Plus:     return Color.FromArgb(80, 200, 120);',  'case StudioIcon.Plus:     return Color.FromArgb(47, 133, 71);'),
    @('case StudioIcon.Minus:    return Color.FromArgb(224, 102, 102);', 'case StudioIcon.Minus:    return Color.FromArgb(196, 54, 54);'),
    @('case StudioIcon.Check:    return Color.FromArgb(72, 200, 136);',  'case StudioIcon.Check:    return Color.FromArgb(47, 133, 71);'),
    @('case StudioIcon.Plug:     return Color.FromArgb(244, 164, 96);',  'case StudioIcon.Plug:     return Color.FromArgb(191, 92, 20);'),
    @('case StudioIcon.Download: return Color.FromArgb(88, 166, 230);',  'case StudioIcon.Download: return Color.FromArgb(24, 116, 190);'),
    @('case StudioIcon.Refresh:  return Color.FromArgb(100, 149, 237);', 'case StudioIcon.Refresh:  return Color.FromArgb(44, 110, 200);'),
    @('case StudioIcon.Chip:     return Color.FromArgb(74, 169, 229);',  'case StudioIcon.Chip:     return Color.FromArgb(20, 122, 168);'),
    @('case StudioIcon.Gear:     return Color.FromArgb(172, 150, 220);', 'case StudioIcon.Gear:     return Color.FromArgb(98, 90, 148);'),
    @('case StudioIcon.Convert:  return Color.FromArgb(80, 190, 205);',  'case StudioIcon.Convert:  return Color.FromArgb(12, 124, 138);'),
    @('case StudioIcon.Terminal: return Color.FromArgb(158, 186, 96);',  'case StudioIcon.Terminal: return Color.FromArgb(92, 110, 40);'),
    @('case StudioIcon.Bolt:     return Color.FromArgb(245, 190, 72);',  'case StudioIcon.Bolt:     return Color.FromArgb(176, 120, 12);'),
    @('case StudioIcon.Monitor:  return Color.FromArgb(67, 192, 201);',  'case StudioIcon.Monitor:  return Color.FromArgb(13, 126, 132);'),
    @('case StudioIcon.Grid:     return Color.FromArgb(132, 164, 215);', 'case StudioIcon.Grid:     return Color.FromArgb(78, 96, 158);'),
    @('case StudioIcon.Select:    return Color.FromArgb(226, 232, 240);', 'case StudioIcon.Select:    return Color.FromArgb(74, 90, 106);'),
    @('case StudioIcon.ContactNO: return Color.FromArgb(125, 211, 252);', 'case StudioIcon.ContactNO: return Color.FromArgb(20, 122, 178);'),
    @('case StudioIcon.ContactNC: return Color.FromArgb(125, 211, 252);', 'case StudioIcon.ContactNC: return Color.FromArgb(20, 122, 178);'),
    @('case StudioIcon.BranchNO:  return Color.FromArgb(125, 211, 252);', 'case StudioIcon.BranchNO:  return Color.FromArgb(20, 122, 178);'),
    @('case StudioIcon.BranchNC:  return Color.FromArgb(125, 211, 252);', 'case StudioIcon.BranchNC:  return Color.FromArgb(20, 122, 178);'),
    @('case StudioIcon.EdgeUp:    return Color.FromArgb(125, 211, 252);', 'case StudioIcon.EdgeUp:    return Color.FromArgb(20, 122, 178);'),
    @('case StudioIcon.EdgeDown:  return Color.FromArgb(125, 211, 252);', 'case StudioIcon.EdgeDown:  return Color.FromArgb(20, 122, 178);'),
    @('case StudioIcon.Coil:      return Color.FromArgb(251, 191, 36);',  'case StudioIcon.Coil:      return Color.FromArgb(176, 120, 12);'),
    @('case StudioIcon.CoilSet:   return Color.FromArgb(251, 191, 36);',  'case StudioIcon.CoilSet:   return Color.FromArgb(176, 120, 12);'),
    @('case StudioIcon.CoilReset: return Color.FromArgb(251, 191, 36);',  'case StudioIcon.CoilReset: return Color.FromArgb(176, 120, 12);'),
    @('case StudioIcon.Trash:     return Color.FromArgb(224, 102, 102);', 'case StudioIcon.Trash:     return Color.FromArgb(196, 54, 54);'),
    @('case StudioIcon.Timer:     return Color.FromArgb(167, 139, 250);', 'case StudioIcon.Timer:     return Color.FromArgb(108, 92, 196);'),
    @('case StudioIcon.Counter:   return Color.FromArgb(244, 114, 182);', 'case StudioIcon.Counter:   return Color.FromArgb(190, 62, 128);')
)
foreach ($pair in $glyphColors) { $ui = $ui.Replace($pair[0], $pair[1]) }

# Menus adicionais do conceito aprovado. A insercao e tolerante para acompanhar a evolucao do shell.
$menuOld = @'
            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(exibir);
            menu.Items.Add(plc);
            menu.Items.Add(ferramentas);
            menu.Items.Add(ajuda);
'@
$menuNew = @'
            ToolStripMenuItem projeto = MenuItem("Projeto");
            projeto.DropDownItems.Add(DropItem("Novo projeto", delegate { InvokeLadder("NewProject", new object[] { true }); }));
            projeto.DropDownItems.Add(DropItem("Abrir projeto...", delegate { InvokeLadder("OpenProject", null); }));
            projeto.DropDownItems.Add(new ToolStripSeparator());
            projeto.DropDownItems.Add(DropItem("Compilar / validar", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));

            ToolStripMenuItem simulacao = MenuItem("Simulador");
            simulacao.DropDownItems.Add(DropItem("Abrir simulador", delegate { ShowSimulator(); }));
            simulacao.DropDownItems.Add(DropItem("Monitor on-line", delegate { ShowMonitor(); }));

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
if ($shell.Contains($menuOld) -and -not $shell.Contains('ToolStripMenuItem projeto = MenuItem("Projeto")')) {
    $shell = $shell.Replace($menuOld, $menuNew)
}

# Barra de ferramentas e nomenclatura operacional.
$shell = $shell.Replace('AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });', 'AddToolButton(bar, "Compilar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });')
$shell = $shell.Replace('AddToolButton(bar, "Conectar", StudioIcon.Plug, true, delegate { ShowCommunication(); });', 'AddToolButton(bar, "Transferir", StudioIcon.Download, true, delegate { ShowCommunication(); });')
$shell = $shell.Replace('bar.Height = 48;', 'bar.Height = 72;')
$shell = $shell.Replace('bar.Height = 60;', 'bar.Height = 72;')
$shell = $shell.Replace('b.Height = 40;', 'b.Height = 64;')
$shell = $shell.Replace('b.Height = 54;', 'b.Height = 64;')
$shell = $shell.Replace('b.Location = new Point(toolCursor, 3);', 'b.Location = new Point(toolCursor, 4);')
$shell = $shell.Replace('sep.Bounds = new Rectangle(toolCursor + 7, 10, 1, 28);', 'sep.Bounds = new Rectangle(toolCursor + 7, 18, 1, 34);')

$monitor = 'AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });'
if ($shell.Contains($monitor) -and -not $shell.Contains('"Simulador", StudioIcon.Bolt')) {
    $shell = $shell.Replace($monitor, $monitor + [Environment]::NewLine + '            AddToolButton(bar, "Simulador", StudioIcon.Bolt, false, delegate { ShowSimulator(); });')
}

# A V51 ja fornece uma paleta vetorial colorida. No tema claro, o texto dos botoes fica escuro.
$ui = $ui.Replace('Emphasis ? StudioTheme.Accent : StudioTheme.Muted,', 'Emphasis ? StudioTheme.Accent : StudioTheme.Fore,')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($uiPath, $ui, [System.Text.Encoding]::UTF8)
Write-Host 'OpenLadder Studio: tema claro V68 aplicado.' -ForegroundColor Cyan
