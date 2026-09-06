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
    'Coil'      = @(251, 191, 36, 176, 120, 12)
    'Timer'     = @(167, 139, 250, 108, 92, 196)
    'Counter'   = @(244, 114, 182, 190, 62, 128)
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
# 3. Menu Exibir -> Tema. A troca vale integralmente na proxima abertura:
#    muita tela fixa a cor no construtor, entao repintar a quente deixaria a
#    janela pela metade. O usuario escolhe reiniciar na hora.
# ---------------------------------------------------------------------------
$themeMenuMethod = @'
        private void ApplyThemeChoice(OpenLadderThemeMode mode)
        {
            if (OpenLadderPalette.Mode == mode) return;
            OpenLadderPalette.Use(mode);

            string label = mode == OpenLadderThemeMode.Dark ? "escuro" : "claro";
            DialogResult answer = MessageBox.Show(
                "Tema " + label + " selecionado.\r\n\r\nAs janelas já abertas mantêm as cores atuais. Reiniciar o OpenLadder Studio agora para aplicar em tudo?",
                "Tema do OpenLadder Studio",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;
            if (!ConfirmDiscardBeforeRestart()) return;
            Application.Restart();
        }

        private bool ConfirmDiscardBeforeRestart()
        {
            // Reiniciar nunca pode descartar projeto em edicao sem aviso.
            try
            {
                if (ladderForm == null || ladderForm.IsDisposed) return true;
                FieldInfo field = typeof(LadderEditorForm).GetField("dirty", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) return true;
                if (!(bool)field.GetValue(ladderForm)) return true;
            }
            catch
            {
                return true;
            }

            return MessageBox.Show(
                "O projeto tem alterações não salvas. Reiniciar mesmo assim?",
                "Alterações não salvas",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
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

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText($uiPath, $ui, [System.Text.Encoding]::UTF8)
Write-Host "OpenLadder Studio: tema unificado V78 ($themeFields cores, $iconCases icones)." -ForegroundColor Cyan
