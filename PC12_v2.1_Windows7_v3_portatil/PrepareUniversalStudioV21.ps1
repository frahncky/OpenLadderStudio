$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$versionPath = Join-Path (Get-Location) 'version.txt'
if (-not (Test-Path $path)) { throw 'UniversalStudioShell.build.cs nao encontrado. Execute PrepareUniversalStudioV20.ps1 antes.' }
if (-not (Test-Path $versionPath)) { throw 'version.txt nao encontrado.' }
$version = [System.IO.File]::ReadAllText($versionPath).Trim()
$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $haystack.Contains($needle)) { throw "Ancora nao encontrada em UniversalStudioShell.build.cs ($label)." }
    return $haystack.Replace($needle, $replacement)
}

function Replace-First([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    $i = $haystack.IndexOf($needle)
    if ($i -lt 0) { throw "Ancora nao encontrada em UniversalStudioShell.build.cs ($label)." }
    return $haystack.Substring(0, $i) + $replacement + $haystack.Substring($i + $needle.Length)
}

function Replace-Section([string]$haystack, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $haystack.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado em UniversalStudioShell.build.cs ($label)." }
    $end = $haystack.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado em UniversalStudioShell.build.cs ($label)." }
    return $haystack.Substring(0, $start) + $replacement + $haystack.Substring($end)
}

# O shell usa algumas cores locais alem do StudioTheme.
$text = Replace-Required $text '        private readonly Color Shell = Color.FromArgb(29, 31, 34);' '        private readonly Color Shell = Color.FromArgb(18, 24, 31);' 'Shell'
$text = Replace-Required $text '        private readonly Color Chrome = Color.FromArgb(37, 39, 43);' '        private readonly Color Chrome = Color.FromArgb(27, 36, 46);' 'Chrome'
$text = Replace-Required $text '        private readonly Color ChromeLight = Color.FromArgb(47, 50, 55);' '        private readonly Color ChromeLight = Color.FromArgb(38, 49, 62);' 'ChromeLight'
$text = Replace-Required $text '        private readonly Color Border = Color.FromArgb(61, 64, 69);' '        private readonly Color Border = Color.FromArgb(55, 68, 82);' 'Border'
$text = Replace-Required $text '        private readonly Color Accent = Color.FromArgb(45, 170, 107);' '        private readonly Color Accent = Color.FromArgb(38, 166, 154);' 'Accent'
$text = Replace-Required $text '        private readonly Color AccentDark = Color.FromArgb(34, 135, 83);' '        private readonly Color AccentDark = Color.FromArgb(28, 128, 119);' 'AccentDark'
$text = Replace-Required $text '        private readonly Color Workspace = Color.FromArgb(235, 238, 241);' '        private readonly Color Workspace = Color.FromArgb(244, 247, 250);' 'Workspace'

$text = Replace-Required $text '        private bool inspectorAllowed = false;' '        private bool inspectorAllowed = true;' 'inspetor inicial'
$text = Replace-Required $text '            miProps.Checked = false;' '            miProps.Checked = true;' 'menu propriedades'
$text = Replace-Required $text '            b.Height = 42;' '            b.Height = 40;' 'altura botao toolbar'
$text = Replace-Required $text '            b.Location = new Point(toolCursor, 1);' '            b.Location = new Point(toolCursor, 3);' 'posicao botao toolbar'
$text = Replace-Required $text '            sep.Bounds = new Rectangle(toolCursor + 7, 11, 1, 24);' '            sep.Bounds = new Rectangle(toolCursor + 7, 10, 1, 28);' 'separador toolbar'
$text = Replace-Required $text '            nav.Width = 190;' '            nav.Width = 218;' 'largura sidebar'
$text = Replace-Required $text '            wrap.Height = 110;' '            wrap.Height = 130;' 'altura console'

$toolbar = @'
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 48;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            Label brand = new Label();
            brand.Text = "OpenLadder Studio  v__VERSION__";
            brand.Dock = DockStyle.Right;
            brand.Width = 190;
            brand.TextAlign = ContentAlignment.MiddleRight;
            brand.ForeColor = Muted;
            brand.Font = StudioTheme.UiBold;
            brand.Padding = new Padding(0, 0, 14, 0);
            bar.Controls.Add(brand);

            toolCursor = 8;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Rung", StudioIcon.Plus, false, delegate { InvokeLadder("AddRung", null); });
            AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Controlador", StudioIcon.Chip, false, delegate { ShowDeviceManager(); });
            AddToolButton(bar, "Conectar", StudioIcon.Plug, true, delegate { ShowCommunication(); });
            AddToolButton(bar, "Ler PLC", StudioIcon.Download, false, delegate { ShowReader(); });
            AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Atualizar", StudioIcon.Refresh, false, delegate { ShowUpdater(); });
            return bar;
        }

'@
$toolbar = $toolbar.Replace('__VERSION__', $version)
$text = Replace-Section $text '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar V21'

$brand = @'
        private Control BuildBrand()
        {
            StudioPanel brand = new StudioPanel();
            brand.Dock = DockStyle.Top;
            brand.Height = 62;
            brand.Fill = StudioTheme.NavBg;
            brand.BottomLine = Border;
            brand.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(48, Accent)))
                    g.FillRectangle(b, new Rectangle(12, 12, 38, 38));
                using (Pen p = new Pen(Color.FromArgb(130, Accent), 1f))
                    g.DrawRectangle(p, 12, 12, 37, 37);
                StudioGlyph.Draw(g, StudioIcon.Ladder, new Rectangle(20, 20, 22, 22), Accent);
                TextRenderer.DrawText(g, "OpenLadder Studio", new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                    new Point(60, 13), Fore);
                string sub = currentProfile == null ? "Nenhum controlador selecionado" : currentProfile.Manufacturer + "  •  " + currentProfile.Model;
                TextRenderer.DrawText(g, sub, StudioTheme.Small, new Rectangle(60, 34, 148, 18), Muted,
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            };
            return brand;
        }

'@
$text = Replace-Section $text '        private Control BuildBrand()' '        private Panel BuildNav()' $brand 'marca V21'

$inspector = @'
        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Right;
            p.Width = 252;
            p.BackColor = Chrome;
            p.Padding = new Padding(16, 12, 16, 12);

            Label title = InspectorLabel("CONTEXTO DO PROJETO", 8.2f, true, Muted);
            title.Location = new Point(16, 14);
            p.Controls.Add(title);

            projectValue = InspectorLabel("Sem nome", 10.2f, true, Fore);
            projectValue.Location = new Point(16, 42);
            projectValue.MaximumSize = new Size(218, 34);
            p.Controls.Add(projectValue);

            AddDivider(p, 82, 218);

            Label device = InspectorLabel("CONTROLADOR", 8.0f, true, Muted);
            device.Location = new Point(16, 98);
            p.Controls.Add(device);

            deviceValue = InspectorLabel("Nenhum controlador", 9.4f, true, Fore);
            deviceValue.Location = new Point(16, 122);
            deviceValue.MaximumSize = new Size(218, 42);
            p.Controls.Add(deviceValue);

            familyValue = InspectorLabel("-", 8.2f, false, Muted);
            familyValue.Location = new Point(16, 166);
            familyValue.MaximumSize = new Size(218, 22);
            p.Controls.Add(familyValue);

            protocolValue = InspectorLabel("-", 8.2f, false, Muted);
            protocolValue.Location = new Point(16, 188);
            protocolValue.MaximumSize = new Size(218, 22);
            p.Controls.Add(protocolValue);

            supportValue = InspectorLabel("-", 8.3f, true, Muted);
            supportValue.Location = new Point(16, 212);
            p.Controls.Add(supportValue);

            AddDivider(p, 242, 218);

            Label resources = InspectorLabel("RECURSOS DISPONIVEIS", 8.0f, true, Muted);
            resources.Location = new Point(16, 258);
            p.Controls.Add(resources);

            capabilityValue = InspectorLabel("-", 8.3f, false, Fore);
            capabilityValue.Location = new Point(16, 282);
            capabilityValue.MaximumSize = new Size(218, 78);
            p.Controls.Add(capabilityValue);

            AddDivider(p, 368, 218);

            Label state = InspectorLabel("ESTADO", 8.0f, true, Muted);
            state.Location = new Point(16, 384);
            p.Controls.Add(state);

            connectionValue = InspectorLabel("●  OFFLINE", 9.2f, true, Color.FromArgb(168, 174, 181));
            connectionValue.Location = new Point(16, 408);
            p.Controls.Add(connectionValue);

            Button connect = InspectorButton("Conectar ao PLC", 16, 446, 218);
            connect.Click += delegate { ShowCommunication(); };
            p.Controls.Add(connect);

            Button change = InspectorButton("Selecionar controlador", 16, 488, 218);
            change.Click += delegate { ShowDeviceManager(); };
            p.Controls.Add(change);

            return p;
        }

'@
$text = Replace-Section $text '        private Panel BuildInspector()' '        private Panel BuildStatusBar()' $inspector 'inspetor V21'

$status = @'
        private Panel BuildStatusBar()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Bottom;
            p.Height = 28;
            p.BackColor = Color.FromArgb(16, 22, 29);
            p.Padding = new Padding(12, 0, 12, 0);

            statusText = new Label();
            statusText.Dock = DockStyle.Fill;
            statusText.TextAlign = ContentAlignment.MiddleLeft;
            statusText.Text = "Pronto para editar";
            statusText.ForeColor = Muted;
            statusText.Font = new Font("Segoe UI", 8.2f);
            p.Controls.Add(statusText);

            modeText = new Label();
            modeText.Dock = DockStyle.Right;
            modeText.Width = 360;
            modeText.TextAlign = ContentAlignment.MiddleRight;
            modeText.ForeColor = Color.FromArgb(171, 181, 191);
            modeText.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            p.Controls.Add(modeText);
            return p;
        }

'@
$text = Replace-Section $text '        private Panel BuildStatusBar()' '        private void RefreshProfileUi()' $status 'status V21'

$ladderNeedle = @'
            inspector.Visible = false;
            ShowDocument(ladderForm, "Programa Ladder", "LD");
'@
$ladderReplacement = @'
            inspector.Visible = inspectorAllowed;
            ShowDocument(ladderForm, "Programa Ladder", "LD");
'@
$text = Replace-First $text $ladderNeedle.TrimEnd() $ladderReplacement.TrimEnd() 'inspetor no Ladder'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'Universal Studio V21 aplicada.'
