$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'V72: UniversalStudioShell.build.cs nao encontrado.' }

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "V72: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "V72: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$shell = LF ([System.IO.File]::ReadAllText($shellPath))

# -----------------------------------------------------------------------------
# Workspace aprovado: Projeto a esquerda, editor no centro e Instrucoes a direita.
# O painel esquerdo deixa de misturar a biblioteca Ladder com a arvore do projeto.
# -----------------------------------------------------------------------------
$nav = @'
        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 248;
            nav.BackColor = StudioTheme.NavBg;
            nav.Padding = new Padding(0);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = StudioTheme.NavBg;
            body.AutoScroll = true;

            List<Control> items = new List<Control>();
            items.Add(new NavSection("Projeto"));
            items.Add(SideAction("Configuração do PLC", StudioIcon.Chip, delegate { ShowDeviceManager(); }));
            items.Add(new NavSection("Programas"));
            items.Add(SideAction("Main (PRG)", StudioIcon.Ladder, delegate { ShowLadder(); }));
            items.Add(new NavSection("Execução"));
            items.Add(SideAction("Simulador", StudioIcon.Grid, delegate { ShowSimulator(); }));
            items.Add(SideAction("Monitor on-line", StudioIcon.Monitor, delegate { ShowMonitor(); }));
            items.Add(new NavSection("Projeto atual"));
            items.Add(BuildSidebarProjectCard());
            items.Add(new NavSection("Seleção"));
            items.Add(BuildSidebarPropertiesCard());
            items.Add(new NavSection("Sistema"));
            items.Add(SideAction("Atualizações", StudioIcon.Refresh, delegate { ShowUpdater(); }));

            for (int i = items.Count - 1; i >= 0; i--) body.Controls.Add(items[i]);

            nav.Controls.Add(body);
            nav.Controls.Add(BuildBrand());
            return nav;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildNav()' '        private Panel BuildSidebarGroup' $nav 'painel Projeto')

# -----------------------------------------------------------------------------
# Biblioteca Ladder vira uma paleta independente, a direita, com busca e todos
# os comandos reais ja suportados pelo editor.
# -----------------------------------------------------------------------------
$inspector = @'
        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Right;
            p.Width = 286;
            p.BackColor = StudioTheme.NavBg;
            p.Padding = new Padding(0);

            Panel library = BuildElementLibrary();
            library.Dock = DockStyle.Fill;
            p.Controls.Add(library);
            return p;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildInspector()' '        private Panel BuildStatusBar()' $inspector 'paleta de instrucoes')

# Nomenclatura visual conforme o conceito aprovado.
$shell = $shell.Replace('ELEMENTOS LADDER', 'INSTRUÇÕES')
$shell = $shell.Replace('Buscar elemento...', 'Buscar instrução...')
$shell = $shell.Replace('elementSearch.Text == "Buscar elemento..."', 'elementSearch.Text == "Buscar instrução..."')
$shell = $shell.Replace('elementSearch.Text = "Buscar elemento..."', 'elementSearch.Text = "Buscar instrução..."')

# Mensagens permanecem visiveis no workspace principal, como no mockup aprovado.
$shell = $shell.Replace('miConsole.Checked = false;', 'miConsole.Checked = true;')
$shell = $shell.Replace('wrap.Visible = false;', 'wrap.Visible = true;')
$shell = $shell.Replace('"SAÍDA"', '"MENSAGENS"')
$shell = $shell.Replace('"Saída"', '"Mensagens"')

# Barra de status mais informativa e alinhada ao uso de IDE de automacao.
$shell = [Regex]::Replace($shell, '(?m)^\s*modeText\.Width = \d+;$', '            modeText.Width = 650;')
$shell = [Regex]::Replace($shell,
    '(?m)^\s*modeText\.Text = model \+ .*?;$',
    '                modeText.Text = "PLC: " + model + "    |    " + protocol + "    |    OFFLINE    |    MODO: EDIÇÃO    |    ZOOM: 100%";')

# O titulo acompanha o projeto ativo. A guarda de nulidade da V70 continua valida.
$projectMethod = @'
        private void UpdateProjectName()
        {
            if (ladderForm == null || ladderForm.IsDisposed || projectValue == null) return;
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("projectLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Label label = field == null ? null : field.GetValue(ladderForm) as Label;
                string value = label == null ? string.Empty : (label.Text ?? string.Empty).Trim();
                projectValue.Text = string.IsNullOrEmpty(value) ? "Projeto1" : value;
                Text = "OpenLadder Studio - " + projectValue.Text;
            }
            catch
            {
                if (projectValue != null) projectValue.Text = "Projeto1";
                Text = "OpenLadder Studio - Projeto1";
            }
        }

'@
$shell = Replace-Section $shell '        private void UpdateProjectName()' '        private void SetRailEnabled' $projectMethod 'titulo do projeto')

# Ajustes de densidade: mais area para o canvas sem perder os tres paineis.
$shell = $shell.Replace('consolePanel.Height = 150;', 'consolePanel.Height = 132;')
$shell = $shell.Replace('wrap.Height = 150;', 'wrap.Height = 132;')

# Guardrails: a nova estrutura deve existir antes de o build seguir.
if ($shell -notmatch 'nav\.Width = 248;') { throw 'V72: painel Projeto nao aplicado.' }
if ($shell -notmatch 'p\.Width = 286;') { throw 'V72: paleta de instrucoes nao aplicada.' }
if ($shell -notmatch 'BuildElementLibrary\(\)') { throw 'V72: biblioteca Ladder nao encontrada.' }
if ($shell -notmatch 'OpenLadder Studio - Projeto1') { throw 'V72: titulo de projeto nao aplicado.' }

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'V72 aplicada: Projeto a esquerda, Ladder ao centro, Instrucoes a direita e Mensagens abaixo.' -ForegroundColor Cyan
