$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
foreach ($p in @($shellPath, $ladderPath)) {
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
# Shell: a lista lateral volta a representar todos os elementos de edicao.
# Adicionar/Remover linha ficam em um grupo proprio, nunca no menu/toolbar.
# -----------------------------------------------------------------------------
$shell = LF ([System.IO.File]::ReadAllText($shellPath))

$elementLibrary = @'
        private Panel BuildElementLibrary()
        {
            Panel host = new Panel();
            host.BackColor = StudioTheme.NavBg;

            Label title = InspectorLabel("ELEMENTOS", 7.4f, true, StudioTheme.Faint);
            title.Dock = DockStyle.Top;
            title.Height = 30;
            title.Padding = new Padding(18, 8, 0, 0);

            FlowLayoutPanel list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = StudioTheme.NavBg;
            list.Padding = new Padding(10, 4, 8, 10);

            elementButtons.Clear();

            AddElementSection(list, "CONTATOS");
            AddElementTool(list, "Contato NA", StudioIcon.ContactNO, LadderTool.ContactNO);
            AddElementTool(list, "Contato NF", StudioIcon.ContactNC, LadderTool.ContactNC);
            AddElementTool(list, "Ramo paralelo NA", StudioIcon.ContactNO, LadderTool.ParallelNO);
            AddElementTool(list, "Ramo paralelo NF", StudioIcon.ContactNC, LadderTool.ParallelNC);

            AddElementSection(list, "SAÍDAS");
            AddElementTool(list, "Bobina", StudioIcon.Coil, LadderTool.Coil);
            AddElementTool(list, "SET", StudioIcon.Check, LadderTool.Set);
            AddElementTool(list, "RESET", StudioIcon.Refresh, LadderTool.Reset);

            AddElementSection(list, "TEMPORIZAÇÃO E CONTAGEM");
            AddElementTool(list, "Temporizador", StudioIcon.Timer, LadderTool.Timer);
            AddElementTool(list, "Contador", StudioIcon.Counter, LadderTool.Counter);

            AddElementSection(list, "FUNÇÕES");
            AddElementTool(list, "Borda de subida", StudioIcon.Bolt, LadderTool.EdgeUp);
            AddElementTool(list, "Borda de descida", StudioIcon.Bolt, LadderTool.EdgeDown);
            AddElementTool(list, "Função especial", StudioIcon.Chip, LadderTool.Function);
            AddElementTool(list, "END", StudioIcon.Terminal, LadderTool.End);

            AddElementSection(list, "LINHAS");
            AddElementAction(list, "Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); });
            AddElementAction(list, "Remover linha", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); });

            host.Controls.Add(list);
            host.Controls.Add(title);
            return host;
        }

        private static void AddElementSection(FlowLayoutPanel list, string text)
        {
            Label section = new Label();
            section.Width = 252;
            section.Height = 24;
            section.Margin = new Padding(8, 8, 0, 1);
            section.Text = text;
            section.TextAlign = ContentAlignment.MiddleLeft;
            section.ForeColor = StudioTheme.Faint;
            section.Font = StudioTheme.Section;
            list.Controls.Add(section);
        }

'@
$shell = Replace-Section $shell '        private Panel BuildElementLibrary()' '        private void AddElementTool' $elementLibrary 'elementos e linhas V55'

$props = @'
        private Panel BuildSidebarPropertiesCard()
        {
            Panel card = new Panel();
            card.BackColor = StudioTheme.NavBg;

            Label a = InspectorLabel("Seleção", 7.4f, true, StudioTheme.Faint);
            a.Location = new Point(18, 8); card.Controls.Add(a);
            selectionValue = InspectorLabel("Nenhum elemento selecionado", 8.4f, true, Fore);
            selectionValue.Location = new Point(18, 28); selectionValue.MaximumSize = new Size(250, 38); card.Controls.Add(selectionValue);

            Button edit = InspectorButton("Editar selecionado", 18, 72, 252);
            edit.Height = 30;
            edit.Click += delegate { EditSelectedLadderElement(); };
            card.Controls.Add(edit);

            Label hint = InspectorLabel("Esc: selecionar   •   Delete: apagar", 7.5f, false, StudioTheme.Faint);
            hint.Location = new Point(18, 110);
            hint.MaximumSize = new Size(252, 22);
            card.Controls.Add(hint);
            return card;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildSidebarPropertiesCard()' '        private void EditSelectedLadderElement()' $props 'propriedades enxutas V55'
$shell = $shell.Replace('            Panel props = BuildSidebarGroup("Propriedades", BuildSidebarPropertiesCard(), 228);', '            Panel props = BuildSidebarGroup("Propriedades", BuildSidebarPropertiesCard(), 158);')

# Garante que operacoes de linha nao reaparecam no menu Editar.
$shell = $shell.Replace('            editar.DropDownItems.Add(DropItem("Adicionar rung", delegate { InvokeLadder("AddRung", null); }));' + "`n", '')
$shell = $shell.Replace('            editar.DropDownItems.Add(DropItem("Excluir rung", delegate { InvokeLadder("DeleteSelectedRung", null); }));' + "`n", '')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Editor Ladder: Delete e a unica acao visual de exclusao de componente.
# Padroniza a linguagem exibida para "linha", mantendo "rung" so internamente.
# -----------------------------------------------------------------------------
$ladder = LF ([System.IO.File]::ReadAllText($ladderPath))
$ladder = $ladder.Replace('            AddToolButton(toolbox, "×  Apagar", t, LadderTool.Erase); t += 42;' + "`n", '')
$ladder = $ladder.Replace('Editor Ladder moderno • WEG TP02', 'Editor Ladder')
$ladder = $ladder.Replace('ELEMENTOS TP02', 'ELEMENTOS')
$ladder = $ladder.Replace('"Clique em uma posição do rung para aplicar: "', '"Clique em uma posição da linha para aplicar: "')
$ladder = $ladder.Replace('"Rung " + (canvas.SelectedRung + 1).ToString()', '"Linha " + (canvas.SelectedRung + 1).ToString()')
$ladder = $ladder.Replace('"Rung " + (r + 1).ToString()', '"Linha " + (r + 1).ToString()')
$ladder = $ladder.Replace('"Novo rung adicionado."', '"Nova linha adicionada."')
$ladder = $ladder.Replace('"Rung removido."', '"Linha removida."')
$ladder = $ladder.Replace('" rung(s)"', '" linha(s)"')
$ladder = $ladder.Replace('"Novo projeto TP02 criado."', '"Novo projeto criado."')
$ladder = $ladder.Replace('"O projeto precisa manter pelo menos um rung."', '"O projeto precisa manter pelo menos uma linha."')

# Ajuda compacta, coerente com os atalhos realmente implementados.
$helpOld = '            help.Text = "TP02: X/Y/C/SC para lógica\r\nTMR/CNT: V0001 a V0256\r\nDuplo clique: editar parâmetro\r\nCtrl+Z: desfazer • Del: apagar";'
$helpNew = '            help.Text = "X/Y/C/SC: lógica  •  TMR/CNT: V0001–V0256\r\nDuplo clique: editar  •  Esc: selecionar\r\nCtrl+Z: desfazer  •  Ctrl+Y: refazer  •  Delete: apagar";'
if ($ladder.Contains($helpOld)) { $ladder = $ladder.Replace($helpOld, $helpNew) }

[System.IO.File]::WriteAllText($ladderPath, $ladder, [System.Text.Encoding]::UTF8)
Write-Host 'UI V55 aplicada: linhas na lista de Elementos, Delete exclusivo e linguagem simplificada.'
