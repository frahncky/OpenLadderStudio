$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'UniversalStudioShell.build.cs nao encontrado.' }

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
$version = if (Test-Path $versionPath) { [System.IO.File]::ReadAllText($versionPath).Trim() } else { '0.54' }
$shell = LF ([System.IO.File]::ReadAllText($shellPath))

# Salva o estado antes mesmo de abrir a tela de atualizacao. Assim, mesmo se o
# instalador precisar encerrar o processo, o projeto e a aba atual ja estao salvos.
$showUpdaterOld = @'
        private void ShowUpdater()
        {
            if (updaterForm == null || updaterForm.IsDisposed) updaterForm = new PC12UpdaterForm();
            inspector.Visible = false;
            ShowDocument(updaterForm, "Atualizações", "UPD");
            statusText.Text = "Atualizações do OpenLadder Studio";
        }
'@
$showUpdaterNew = @'
        private void ShowUpdater()
        {
            SaveUpdateResumeState();
            if (updaterForm == null || updaterForm.IsDisposed) updaterForm = new PC12UpdaterForm();
            inspector.Visible = false;
            ShowDocument(updaterForm, "Atualizações", "UPD");
            statusText.Text = "Atualizações";
        }
'@
$shell = Replace-Required $shell $showUpdaterOld.TrimEnd() $showUpdaterNew.TrimEnd() 'salvar sessao antes do updater'

# Lista lateral: somente componentes Ladder. Selecionar e operacoes de linha
# deixam de aparecer misturadas com os componentes.
$elementLibrary = @'
        private Panel BuildElementLibrary()
        {
            Panel host = new Panel();
            host.BackColor = StudioTheme.NavBg;

            Label title = InspectorLabel("COMPONENTES", 7.4f, true, StudioTheme.Faint);
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
$shell = Replace-Section $shell '        private Panel BuildElementLibrary()' '        private void AddElementTool' $elementLibrary 'biblioteca de componentes V54'

# Propriedades concentra as acoes que nao sao componentes.
$props = @'
        private Panel BuildSidebarPropertiesCard()
        {
            Panel card = new Panel();
            card.BackColor = StudioTheme.NavBg;

            Label a = InspectorLabel("Seleção", 7.4f, true, StudioTheme.Faint);
            a.Location = new Point(18, 8); card.Controls.Add(a);
            selectionValue = InspectorLabel("Nenhum elemento selecionado", 8.4f, true, Fore);
            selectionValue.Location = new Point(18, 28); selectionValue.MaximumSize = new Size(250, 38); card.Controls.Add(selectionValue);

            Button select = InspectorButton("Selecionar  (Esc)", 18, 72, 252);
            select.Height = 30;
            select.Click += delegate { SelectLadderTool(LadderTool.Select); };
            card.Controls.Add(select);

            Button edit = InspectorButton("Editar selecionado", 18, 106, 252);
            edit.Height = 30;
            edit.Click += delegate { EditSelectedLadderElement(); };
            card.Controls.Add(edit);

            Button add = InspectorButton("Adicionar linha", 18, 140, 122);
            add.Height = 30;
            add.Click += delegate { InvokeLadder("AddRung", null); };
            card.Controls.Add(add);

            Button remove = InspectorButton("Remover linha", 148, 140, 122);
            remove.Height = 30;
            remove.Click += delegate { InvokeLadder("DeleteSelectedRung", null); };
            card.Controls.Add(remove);

            Label hint = InspectorLabel("Delete  apaga o componente selecionado", 7.5f, false, StudioTheme.Faint);
            hint.Location = new Point(18, 179);
            hint.MaximumSize = new Size(252, 22);
            card.Controls.Add(hint);
            return card;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildSidebarPropertiesCard()' '        private void EditSelectedLadderElement()' $props 'acoes fora da lista de componentes'
$shell = Replace-Required $shell '            Panel props = BuildSidebarGroup("Propriedades", BuildSidebarPropertiesCard(), 174);' '            Panel props = BuildSidebarGroup("Propriedades", BuildSidebarPropertiesCard(), 228);' 'altura propriedades'

# A janela Sobre deve conter somente identificacao util.
$aboutOld = '"OpenLadder Studio v' + $version + '\r\n\r\nAmbiente Ladder com arquitetura multi-fabricante.\r\nWEG TP02: driver nativo em evolução.\r\nModbus RTU/TCP: monitoramento genérico em leitura."'
$aboutNew = '"OpenLadder Studio v' + $version + '"'
if ($shell.Contains($aboutOld)) { $shell = $shell.Replace($aboutOld, $aboutNew) }

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'UI V54 aplicada: componentes categorizados, acoes separadas, Delete explicito e updater robustecido.'
