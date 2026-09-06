$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path $shellPath)) { throw 'V75: UniversalStudioShell.build.cs nao encontrado.' }

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "V75: ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}

$shell = LF ([System.IO.File]::ReadAllText($shellPath))

# Habilita os tres divisores depois que o workspace completo ja foi montado.
$buildAnchor = '            center.Controls.Add(tabStrip);'
$buildReplacement = @'
            center.Controls.Add(tabStrip);
            V75EnableWorkspaceResize(workspace, center);
'@
$shell = Required $shell $buildAnchor $buildReplacement.TrimEnd() 'ativacao dos divisores'

$helpers = @'
        private void V75EnableWorkspaceResize(Panel workspace, Panel center)
        {
            if (workspace == null || center == null || navPanel == null || inspector == null || consolePanel == null) return;

            Panel navGrip = V75Grip(Cursors.VSplit);
            navPanel.Controls.Add(navGrip);
            navGrip.BringToFront();
            Action placeNavGrip = delegate
            {
                navGrip.SetBounds(Math.Max(0, navPanel.ClientSize.Width - 5), 0, 5, navPanel.ClientSize.Height);
            };
            placeNavGrip();
            navPanel.Resize += delegate { placeNavGrip(); };
            navGrip.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || !navPanel.Visible) return;
                Point p = workspace.PointToClient(Control.MousePosition);
                int other = inspector.Visible ? inspector.Width : 0;
                int max = Math.Max(190, workspace.ClientSize.Width - other - 520);
                navPanel.Width = Math.Max(190, Math.Min(max, p.X));
                if (statusText != null) statusText.Text = "Largura do painel Projeto: " + navPanel.Width.ToString() + " px";
            };
            navGrip.DoubleClick += delegate { navPanel.Width = 252; };

            Panel inspectorGrip = V75Grip(Cursors.VSplit);
            inspector.Controls.Add(inspectorGrip);
            inspectorGrip.BringToFront();
            Action placeInspectorGrip = delegate
            {
                inspectorGrip.SetBounds(0, 0, 5, inspector.ClientSize.Height);
            };
            placeInspectorGrip();
            inspector.Resize += delegate { placeInspectorGrip(); };
            inspectorGrip.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || !inspector.Visible) return;
                Point p = workspace.PointToClient(Control.MousePosition);
                int other = navPanel.Visible ? navPanel.Width : 0;
                int max = Math.Max(230, workspace.ClientSize.Width - other - 520);
                int target = workspace.ClientSize.Width - p.X;
                inspector.Width = Math.Max(230, Math.Min(max, target));
                if (statusText != null) statusText.Text = "Largura do painel Instruções: " + inspector.Width.ToString() + " px";
            };
            inspectorGrip.DoubleClick += delegate { inspector.Width = 292; };

            Panel consoleGrip = V75Grip(Cursors.HSplit);
            consolePanel.Controls.Add(consoleGrip);
            consoleGrip.BringToFront();
            Action placeConsoleGrip = delegate
            {
                consoleGrip.SetBounds(0, 0, consolePanel.ClientSize.Width, 5);
            };
            placeConsoleGrip();
            consolePanel.Resize += delegate { placeConsoleGrip(); };
            consoleGrip.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || !consolePanel.Visible) return;
                Point p = center.PointToClient(Control.MousePosition);
                int max = Math.Max(96, center.ClientSize.Height - 240);
                int target = center.ClientSize.Height - p.Y;
                consolePanel.Height = Math.Max(96, Math.Min(max, target));
                if (statusText != null) statusText.Text = "Altura do painel Mensagens: " + consolePanel.Height.ToString() + " px";
            };
            consoleGrip.DoubleClick += delegate { consolePanel.Height = 132; };
        }

        private Panel V75Grip(Cursor cursor)
        {
            Panel grip = new Panel();
            grip.BackColor = Border;
            grip.Cursor = cursor;
            grip.TabStop = false;
            grip.MouseEnter += delegate { grip.BackColor = Accent; };
            grip.MouseLeave += delegate { grip.BackColor = Border; };
            return grip;
        }

        private void V75ResetWorkspaceLayout()
        {
            if (navPanel != null)
            {
                navPanel.Visible = true;
                navPanel.Width = 252;
            }
            inspectorAllowed = true;
            if (inspector != null) inspector.Width = 292;
            if (consolePanel != null)
            {
                consolePanel.Visible = true;
                consolePanel.Height = 132;
            }
            if (miNav != null) miNav.Checked = true;
            if (miProps != null) miProps.Checked = true;
            if (miConsole != null) miConsole.Checked = true;
            ApplySelectedTab();
            if (statusText != null) statusText.Text = "Layout padrão restaurado";
        }

'@
$shell = Required $shell '        private MenuStrip BuildMenu()' ($helpers + '        private MenuStrip BuildMenu()') 'helpers do workspace'

# O menu Janela passa a oferecer restauracao imediata das proporcoes aprovadas.
$janelaAnchor = '            janela.DropDownItems.Add(DropItem("Mensagens", delegate { TogglePanel(2); }));'
$janelaReplacement = @'
            janela.DropDownItems.Add(DropItem("Mensagens", delegate { TogglePanel(2); }));
            janela.DropDownItems.Add(new ToolStripSeparator());
            janela.DropDownItems.Add(DropItem("Restaurar layout padrão", delegate { V75ResetWorkspaceLayout(); }));
'@
$shell = Required $shell $janelaAnchor $janelaReplacement.TrimEnd() 'comando de restaurar layout'

# Guardrails: V75 deve permanecer uma camada puramente de interface.
if ($shell -notmatch 'V75EnableWorkspaceResize\(workspace, center\)') { throw 'V75: resize do workspace nao aplicado.' }
if ($shell -notmatch 'Largura do painel Projeto') { throw 'V75: divisor do painel Projeto ausente.' }
if ($shell -notmatch 'Largura do painel Instruções') { throw 'V75: divisor do painel Instrucoes ausente.' }
if ($shell -notmatch 'Altura do painel Mensagens') { throw 'V75: divisor do painel Mensagens ausente.' }
if ($shell -notmatch 'Restaurar layout padrão') { throw 'V75: restauracao de layout ausente.' }

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'V75 aplicada: painéis Projeto/Instruções/Mensagens redimensionáveis e layout restaurável.' -ForegroundColor Cyan
