$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Get-Location) 'UniversalStudioShell.cs'
$outputPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$versionPath = Join-Path (Get-Location) 'version.txt'
$text = [System.IO.File]::ReadAllText($sourcePath)

if (-not (Test-Path $versionPath)) { throw 'version.txt não encontrado.' }
$version = [System.IO.File]::ReadAllText($versionPath).Trim()
if ($version -notmatch '^\d+\.\d+(\.\d+)?$') { throw "Versão inválida em version.txt: $version" }

function Invoke-Replace([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $haystack.Contains($needle)) {
        throw "Ancora nao encontrada em UniversalStudioShell.cs ($label). Ajuste PrepareUniversalStudioV20.ps1."
    }
    return $haystack.Replace($needle, $replacement)
}

function Invoke-SectionReplace([string]$haystack, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [bool]$keepEnd, [string]$label) {
    $start = $haystack.IndexOf($startAnchor)
    if ($start -lt 0) {
        throw "Inicio nao encontrado em UniversalStudioShell.cs ($label)."
    }
    $end = $haystack.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) {
        throw "Fim nao encontrado em UniversalStudioShell.cs ($label)."
    }
    $after = if ($keepEnd) { $end } else { $end + $endAnchor.Length }
    return $haystack.Substring(0, $start) + $replacement + $haystack.Substring($after)
}

$text = Invoke-Replace $text 'v0.12' ('v' + $version) 'versao'

$fieldNeedle = '        private bool inspectorAllowed = true;'
$fieldInsert = @'
        private bool inspectorAllowed = false;
        private bool focusMode;
        private bool focusNavVisible;
        private bool focusInspectorAllowed;
        private bool focusConsoleVisible;
'@
$text = Invoke-Replace $text $fieldNeedle $fieldInsert.TrimEnd() 'estado dos paineis'

$checksNeedle = @'
            miNav.Checked = true;
            miProps.Checked = true;
            miConsole.Checked = true;
'@
$checksInsert = @'
            miNav.Checked = true;
            miProps.Checked = false;
            miConsole.Checked = false;
'@
$text = Invoke-Replace $text $checksNeedle.Trim() $checksInsert.Trim() 'estado inicial dos menus'

$viewNeedle = '            exibir.DropDownItems.Add(miConsole);'
$viewInsert = @'
            exibir.DropDownItems.Add(miConsole);
            exibir.DropDownItems.Add(new ToolStripSeparator());
            exibir.DropDownItems.Add(DropItem("Modo foco do editor\tF11", delegate { ToggleFocusMode(); }));
'@
$text = Invoke-Replace $text $viewNeedle $viewInsert.TrimEnd() 'menu modo foco'

$plcNeedle = '            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));'
$plcInsert = @'
            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(DropItem("Mapa de mem\u00F3ria...", delegate { ShowMemoryMapManager(); }));
'@
$text = Invoke-Replace $text $plcNeedle $plcInsert.TrimEnd() 'menu PLC'

$consoleNeedle = @'
            consolePanel = BuildConsole();
            center.Controls.Add(consolePanel);
'@
$consoleInsert = @'
            consolePanel = BuildConsole();
            center.Controls.Add(consolePanel);
            consolePanel.Visible = false;
'@
$text = Invoke-Replace $text $consoleNeedle.Trim() $consoleInsert.Trim() 'console inicial'

$text = Invoke-Replace $text '            b.Height = 54;' '            b.Height = 42;' 'altura dos botoes'
$text = Invoke-Replace $text '            b.Location = new Point(toolCursor, 3);' '            b.Location = new Point(toolCursor, 1);' 'posicao dos botoes'
$text = Invoke-Replace $text '            sep.Bounds = new Rectangle(toolCursor + 7, 15, 1, 30);' '            sep.Bounds = new Rectangle(toolCursor + 7, 11, 1, 24);' 'separador da barra'
$text = Invoke-Replace $text '            bar.Height = 60;' '            bar.Height = 46;' 'altura da barra'

$text = Invoke-SectionReplace $text '            Label brand = new Label();' '            bar.Controls.Add(brand);' '' $false 'marca duplicada da barra'

$toolbarReplacement = @'
            toolCursor = 10;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Rung", StudioIcon.Plus, false, delegate { InvokeLadder("AddRung", null); });
            AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            return bar;
'@
$text = Invoke-SectionReplace $text '            toolCursor = 10;' '            return bar;' $toolbarReplacement.TrimEnd() $false 'barra de ferramentas compacta'

$brandMethodReplacement = @'
        private Control BuildBrand()
        {
            StudioPanel brand = new StudioPanel();
            brand.Dock = DockStyle.Top;
            brand.Height = 48;
            brand.Fill = StudioTheme.NavBg;
            brand.BottomLine = Border;
            brand.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                using (SolidBrush b = new SolidBrush(Accent))
                    g.FillRectangle(b, new Rectangle(14, 10, 28, 28));
                StudioGlyph.Draw(g, StudioIcon.Ladder, new Rectangle(18, 14, 20, 20), Color.White);
                TextRenderer.DrawText(g, "OpenLadder", new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                    new Point(50, 15), Fore);
            };
            return brand;
        }

'@
$text = Invoke-SectionReplace $text '        private Control BuildBrand()' '        private Panel BuildNav()' $brandMethodReplacement $true 'marca lateral compacta'

$text = Invoke-Replace $text '            nav.Width = 228;' '            nav.Width = 190;' 'largura da navegacao'
$text = Invoke-Replace $text '            wrap.Height = 150;' '            wrap.Height = 110;' 'altura do console'

$inspectorReplacement = @'
        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Right;
            p.Width = 230;
            p.BackColor = Chrome;
            p.Padding = new Padding(14, 12, 14, 12);

            Label title = InspectorLabel("CONTEXTO", 8.4f, true, Muted);
            title.Location = new Point(14, 14);
            p.Controls.Add(title);

            Label project = InspectorLabel("Projeto", 8.0f, false, Muted);
            project.Location = new Point(14, 48);
            p.Controls.Add(project);

            projectValue = InspectorLabel("Sem nome", 9.1f, true, Fore);
            projectValue.Location = new Point(14, 68);
            projectValue.MaximumSize = new Size(200, 28);
            p.Controls.Add(projectValue);

            AddDivider(p, 104, 202);

            Label device = InspectorLabel("Controlador ativo", 8.0f, false, Muted);
            device.Location = new Point(14, 120);
            p.Controls.Add(device);

            deviceValue = InspectorLabel("-", 9.2f, true, Fore);
            deviceValue.Location = new Point(14, 140);
            deviceValue.MaximumSize = new Size(200, 42);
            p.Controls.Add(deviceValue);

            supportValue = InspectorLabel("-", 8.2f, true, Muted);
            supportValue.Location = new Point(14, 182);
            p.Controls.Add(supportValue);

            AddDivider(p, 214, 202);

            Label status = InspectorLabel("CONEX\u00C3O", 8.0f, true, Muted);
            status.Location = new Point(14, 230);
            p.Controls.Add(status);

            connectionValue = InspectorLabel("\u25CF  OFFLINE", 9.0f, true, Color.FromArgb(168, 174, 181));
            connectionValue.Location = new Point(14, 254);
            p.Controls.Add(connectionValue);

            Button change = InspectorButton("Controlador...", 14, 294, 202);
            change.Click += delegate { ShowDeviceManager(); };
            p.Controls.Add(change);

            Button connect = InspectorButton("Comunica\u00E7\u00E3o...", 14, 336, 202);
            connect.Click += delegate { ShowCommunication(); };
            p.Controls.Add(connect);

            return p;
        }

'@
$text = Invoke-SectionReplace $text '        private Panel BuildInspector()' '        private Panel BuildStatusBar()' $inspectorReplacement $true 'inspetor compacto'

$text = Invoke-Replace $text '            modeText.Width = 470;' '            modeText.Width = 300;' 'largura do status direito'

$modeReplacement = @'
            if (modeText != null)
            {
                string model = currentProfile == null ? "SEM PLC" : currentProfile.Model;
                modeText.Text = model + "    |    OFFLINE";
            }

            UpdateRailCapabilities();
'@
$text = Invoke-SectionReplace $text '            if (modeText != null)' '            UpdateRailCapabilities();' $modeReplacement.TrimEnd() $false 'status sem repeticoes'

$text = Invoke-Replace $text '            inspector.Visible = true;' '            inspector.Visible = false;' 'ladder sem inspetor por padrao'

$toggleNeedle = '        private void TogglePanel(int which)'
$toggleInsert = @'
        private void ToggleFocusMode()
        {
            if (!focusMode)
            {
                focusNavVisible = navPanel != null && navPanel.Visible;
                focusInspectorAllowed = inspectorAllowed;
                focusConsoleVisible = consolePanel != null && consolePanel.Visible;
                focusMode = true;
                if (navPanel != null) navPanel.Visible = false;
                inspectorAllowed = false;
                if (consolePanel != null) consolePanel.Visible = false;
                if (miNav != null) miNav.Checked = false;
                if (miProps != null) miProps.Checked = false;
                if (miConsole != null) miConsole.Checked = false;
                ApplySelectedTab();
                if (statusText != null) statusText.Text = "Modo foco do editor - F11 para restaurar os pain\u00E9is";
            }
            else
            {
                focusMode = false;
                if (navPanel != null) navPanel.Visible = focusNavVisible;
                inspectorAllowed = focusInspectorAllowed;
                if (consolePanel != null) consolePanel.Visible = focusConsoleVisible;
                if (miNav != null) miNav.Checked = focusNavVisible;
                if (miProps != null) miProps.Checked = focusInspectorAllowed;
                if (miConsole != null) miConsole.Checked = focusConsoleVisible;
                ApplySelectedTab();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                ToggleFocusMode();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

'@
$text = Invoke-Replace $text $toggleNeedle ($toggleInsert + $toggleNeedle) 'modo foco'

$methodNeedle = '        private void ShowCommunication()'
$methodInsert = @'
        private void ShowMemoryMapManager()
        {
            using (PlcMemoryMapManagerForm dialog = new PlcMemoryMapManagerForm())
            {
                dialog.ShowDialog(this);
            }
            RefreshProfileUi();
            statusText.Text = currentProfile == null ? "Mapa de mem\u00F3ria atualizado" : "Mapa de mem\u00F3ria: " + currentProfile.Manufacturer + " " + currentProfile.Model;
        }

'@
$text = Invoke-Replace $text $methodNeedle ($methodInsert + $methodNeedle) 'ShowMemoryMapManager'

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
