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
# A V66 remove os helpers antigos do painel lateral; por isso a V72 e autocontida.
# -----------------------------------------------------------------------------
$workspace = @'
        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 248;
            nav.BackColor = StudioTheme.NavBg;

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = StudioTheme.NavBg;
            body.AutoScroll = true;

            List<Control> items = new List<Control>();
            items.Add(new NavSection("Projeto"));
            items.Add(V72ProjectAction("Configuração do PLC", StudioIcon.Chip, delegate { ShowDeviceManager(); }));
            items.Add(new NavSection("Programas"));
            items.Add(V72ProjectAction("Main (PRG)", StudioIcon.Ladder, delegate { ShowLadder(); }));
            items.Add(new NavSection("Execução"));
            items.Add(V72ProjectAction("Simulador", StudioIcon.Grid, delegate { ShowSimulator(); }));
            items.Add(V72ProjectAction("Monitor on-line", StudioIcon.Monitor, delegate { ShowMonitor(); }));
            items.Add(new NavSection("Projeto atual"));
            items.Add(V72ProjectCard());
            items.Add(new NavSection("Sistema"));
            items.Add(V72ProjectAction("Atualizações", StudioIcon.Refresh, delegate { ShowUpdater(); }));

            for (int i = items.Count - 1; i >= 0; i--) body.Controls.Add(items[i]);
            nav.Controls.Add(body);
            nav.Controls.Add(BuildBrand());
            return nav;
        }

        private NavButton V72ProjectAction(string text, StudioIcon icon, EventHandler action)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            if (action != null) b.Click += action;
            return b;
        }

        private Panel V72ProjectCard()
        {
            Panel card = new Panel();
            card.Dock = DockStyle.Top;
            card.Height = 158;
            card.BackColor = StudioTheme.NavBg;

            Label pc = InspectorLabel("Projeto", 7.4f, true, StudioTheme.Faint);
            pc.Location = new Point(18, 8); card.Controls.Add(pc);
            projectValue = InspectorLabel("Projeto1", 9.2f, true, Fore);
            projectValue.Location = new Point(18, 28); projectValue.MaximumSize = new Size(212, 34); card.Controls.Add(projectValue);

            Label dc = InspectorLabel("Controlador ativo", 7.4f, true, StudioTheme.Faint);
            dc.Location = new Point(18, 67); card.Controls.Add(dc);
            deviceValue = InspectorLabel("Nenhum controlador", 8.7f, true, Fore);
            deviceValue.Location = new Point(18, 87); deviceValue.MaximumSize = new Size(212, 24); card.Controls.Add(deviceValue);
            familyValue = InspectorLabel("-", 7.5f, false, Muted);
            familyValue.Location = new Point(18, 113); familyValue.MaximumSize = new Size(98, 20); card.Controls.Add(familyValue);
            protocolValue = InspectorLabel("-", 7.5f, false, Muted);
            protocolValue.Location = new Point(118, 113); protocolValue.MaximumSize = new Size(112, 20); card.Controls.Add(protocolValue);

            supportValue = InspectorLabel("-", 7.2f, true, Muted); supportValue.Visible = false; card.Controls.Add(supportValue);
            capabilityValue = InspectorLabel("-", 7.2f, false, Muted); capabilityValue.Visible = false; card.Controls.Add(capabilityValue);
            connectionValue = InspectorLabel("● OFFLINE", 7.2f, true, Muted); connectionValue.Visible = false; card.Controls.Add(connectionValue);
            return card;
        }

        private Panel V72InstructionPanel()
        {
            Panel host = new Panel();
            host.BackColor = StudioTheme.NavBg;

            Label title = InspectorLabel("INSTRUÇÕES", 7.8f, true, StudioTheme.Faint);
            title.Dock = DockStyle.Top;
            title.Height = 34;
            title.Padding = new Padding(16, 10, 0, 0);

            FlowLayoutPanel list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = StudioTheme.NavBg;
            list.Padding = new Padding(10, 4, 8, 10);

            V72AddSection(list, "CONTATOS");
            V72AddInstruction(list, "Contato NA", StudioIcon.ContactNO, LadderTool.ContactNO);
            V72AddInstruction(list, "Contato NF", StudioIcon.ContactNC, LadderTool.ContactNC);
            V72AddInstruction(list, "Ramo paralelo NA", StudioIcon.BranchNO, LadderTool.ParallelNO);
            V72AddInstruction(list, "Ramo paralelo NF", StudioIcon.BranchNC, LadderTool.ParallelNC);

            V72AddSection(list, "SAÍDAS");
            V72AddInstruction(list, "Bobina", StudioIcon.Coil, LadderTool.Coil);
            V72AddInstruction(list, "SET", StudioIcon.CoilSet, LadderTool.Set);
            V72AddInstruction(list, "RESET", StudioIcon.CoilReset, LadderTool.Reset);

            V72AddSection(list, "TEMPORIZAÇÃO E CONTAGEM");
            V72AddInstruction(list, "Temporizador", StudioIcon.Timer, LadderTool.Timer);
            V72AddInstruction(list, "Contador", StudioIcon.Counter, LadderTool.Counter);

            V72AddSection(list, "FUNÇÕES");
            V72AddInstruction(list, "Borda de subida", StudioIcon.EdgeUp, LadderTool.EdgeUp);
            V72AddInstruction(list, "Borda de descida", StudioIcon.EdgeDown, LadderTool.EdgeDown);
            V72AddInstruction(list, "Função especial", StudioIcon.Chip, LadderTool.Function);
            V72AddInstruction(list, "END", StudioIcon.Terminal, LadderTool.End);

            V72AddSection(list, "EDIÇÃO");
            V72AddInstruction(list, "Selecionar", StudioIcon.Select, LadderTool.Select);
            V72AddAction(list, "Apagar selecionado", StudioIcon.Trash, delegate { InvokeLadder("DeleteSelectedElement", null); });
            V72AddAction(list, "Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); });
            V72AddAction(list, "Remover linha", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); });

            host.Controls.Add(list);
            host.Controls.Add(title);
            return host;
        }

        private static void V72AddSection(FlowLayoutPanel list, string text)
        {
            Label section = new Label();
            section.Width = 256;
            section.Height = 24;
            section.Margin = new Padding(8, 8, 0, 1);
            section.Text = text;
            section.TextAlign = ContentAlignment.MiddleLeft;
            section.ForeColor = StudioTheme.Faint;
            section.Font = StudioTheme.Section;
            list.Controls.Add(section);
        }

        private void V72AddInstruction(FlowLayoutPanel list, string text, StudioIcon icon, LadderTool tool)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Dock = DockStyle.None;
            b.Width = 256;
            b.Height = 34;
            b.Margin = new Padding(0, 0, 0, 1);
            b.Click += delegate { V72SelectLadderTool(tool); };
            list.Controls.Add(b);
        }

        private void V72AddAction(FlowLayoutPanel list, string text, StudioIcon icon, EventHandler action)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Dock = DockStyle.None;
            b.Width = 256;
            b.Height = 34;
            b.Margin = new Padding(0, 0, 0, 1);
            if (action != null) b.Click += action;
            list.Controls.Add(b);
        }

        private void V72SelectLadderTool(LadderTool tool)
        {
            ShowLadder();
            try
            {
                MethodInfo method = typeof(LadderEditorForm).GetMethod("SetActiveTool", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null) method.Invoke(ladderForm, new object[] { tool });
                if (statusText != null) statusText.Text = "Ferramenta: " + tool.ToString();
            }
            catch (Exception ex)
            {
                if (statusText != null) statusText.Text = ex.Message;
            }
        }

'@
$shell = Replace-Section $shell '        private Panel BuildNav()' '        private StudioPanel BuildConsole()' $workspace 'workspace V72'

# -----------------------------------------------------------------------------
# Paleta independente a direita. Continua aparecendo apenas na aba Ladder por
# meio da logica existente de ApplySelectedTab/inspectorAllowed.
# -----------------------------------------------------------------------------
$inspector = @'
        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Right;
            p.Width = 286;
            p.BackColor = StudioTheme.NavBg;
            p.Padding = new Padding(0);

            Panel library = V72InstructionPanel();
            library.Dock = DockStyle.Fill;
            p.Controls.Add(library);
            return p;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildInspector()' '        private Panel BuildStatusBar()' $inspector 'paleta de instrucoes'

# Mensagens permanecem visiveis no workspace principal, como no mockup aprovado.
$shell = $shell.Replace('miConsole.Checked = false;', 'miConsole.Checked = true;')
$shell = $shell.Replace('wrap.Visible = false;', 'wrap.Visible = true;')
$shell = $shell.Replace('"SAÍDA"', '"MENSAGENS"')
$shell = $shell.Replace('"Saída"', '"Mensagens"')
$shell = $shell.Replace('"Sa\u00EDda"', '"Mensagens"')

# Barra de status mais informativa e alinhada ao uso de IDE de automacao.
$shell = [Regex]::Replace($shell, '(?m)^\s*modeText\.Width = \d+;$', '            modeText.Width = 650;')
$shell = [Regex]::Replace($shell,
    '(?m)^\s*modeText\.Text = model \+ .*?;$',
    '                modeText.Text = "PLC: " + model + "    |    " + (currentProfile == null ? "-" : currentProfile.Protocol) + "    |    OFFLINE    |    MODO: EDIÇÃO    |    ZOOM: 100%";')

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
$shell = Replace-Section $shell '        private void UpdateProjectName()' '        private void SetRailEnabled' $projectMethod 'titulo do projeto'

# Ajustes de densidade: mais area para o canvas sem perder os tres paineis.
$shell = $shell.Replace('wrap.Height = 150;', 'wrap.Height = 132;')

# Guardrails da estrutura final.
if ($shell -notmatch 'nav\.Width = 248;') { throw 'V72: painel Projeto nao aplicado.' }
if ($shell -notmatch 'p\.Width = 286;') { throw 'V72: paleta de instrucoes nao aplicada.' }
if ($shell -notmatch 'Panel library = V72InstructionPanel\(\);') { throw 'V72: biblioteca de instrucoes nao aplicada.' }
if ($shell -notmatch 'OpenLadder Studio - Projeto1') { throw 'V72: titulo de projeto nao aplicado.' }
if ($shell -notmatch 'Main \(PRG\)') { throw 'V72: programa Main nao aplicado.' }

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'V72 aplicada: Projeto a esquerda, Ladder ao centro, Instrucoes a direita e Mensagens abaixo.' -ForegroundColor Cyan
