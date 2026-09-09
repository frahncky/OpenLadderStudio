$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$tp02Path = Join-Path $root 'TP02ControlV31.build.cs'
foreach ($p in @($shellPath, $tp02Path)) {
    if (-not (Test-Path $p)) { throw "Arquivo de build nao encontrado: $p" }
}

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

$shell = LF ([System.IO.File]::ReadAllText($shellPath))
if (-not $shell.Contains('using System.IO;')) {
    $shell = $shell.Replace('using System.Drawing;', "using System.Drawing;`nusing System.IO;")
}

# -----------------------------------------------------------------------------
# Estado: o monitor MMI de leitura e separado do antigo Bridge Lab.
# -----------------------------------------------------------------------------
$fieldAnchor = '        private TP02BridgeForm bridgeForm;'
if (-not $shell.Contains('private TP02ControlV31Form tp02MonitorForm;')) {
    if (-not $shell.Contains($fieldAnchor)) { throw 'Campo bridgeForm nao encontrado.' }
    $shell = $shell.Replace($fieldAnchor, $fieldAnchor + "`n        private TP02ControlV31Form tp02MonitorForm;")
}

# -----------------------------------------------------------------------------
# Menu: ferramentas comuns ficam no nivel principal; engenharia reversa TP02
# passa para um submenu explicitamente avancado. Atualizacao vai para Ajuda.
# -----------------------------------------------------------------------------
$menu = @'
        private MenuStrip BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.Dock = DockStyle.Top;
            menu.Height = 27;
            menu.BackColor = Chrome;
            menu.ForeColor = Fore;
            menu.Padding = new Padding(8, 2, 0, 2);
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new ToolStripProfessionalRenderer(new UniversalStudioColorTable());

            ToolStripMenuItem arquivo = MenuItem("Arquivo");
            arquivo.DropDownItems.Add(DropItem("Novo projeto", delegate { InvokeLadder("NewProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(DropItem("Abrir...", delegate { InvokeLadder("OpenProject", null); }));
            arquivo.DropDownItems.Add(DropItem("Salvar", delegate { InvokeLadder("SaveProject", new object[] { false }); }));
            arquivo.DropDownItems.Add(DropItem("Salvar como...", delegate { InvokeLadder("SaveProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(new ToolStripSeparator());
            arquivo.DropDownItems.Add(DropItem("Sair", delegate { Close(); }));

            ToolStripMenuItem editar = MenuItem("Editar");
            editar.DropDownItems.Add(DropItem("Desfazer", delegate { InvokeLadder("Undo", null); }));
            editar.DropDownItems.Add(DropItem("Refazer", delegate { InvokeLadder("Redo", null); }));
            editar.DropDownItems.Add(new ToolStripSeparator());
            editar.DropDownItems.Add(DropItem("Validar programa", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));

            miNav = DropItem("Painel lateral", delegate { TogglePanel(0); });
            miProps = DropItem("Propriedades", delegate { TogglePanel(1); });
            miConsole = DropItem("Sa\u00EDda", delegate { TogglePanel(2); });
            miNav.Checked = true;
            miProps.Checked = true;
            miConsole.Checked = true;

            ToolStripMenuItem exibir = MenuItem("Exibir");
            exibir.DropDownItems.Add(miNav);
            exibir.DropDownItems.Add(miProps);
            exibir.DropDownItems.Add(miConsole);

            ToolStripMenuItem plc = MenuItem("PLC");
            plc.DropDownItems.Add(DropItem("Selecionar controlador...", delegate { ShowDeviceManager(); }));
            plc.DropDownItems.Add(new ToolStripSeparator());
            plc.DropDownItems.Add(DropItem("Conectar...", delegate { ShowCommunication(); }));
            plc.DropDownItems.Add(DropItem("Monitor", delegate { ShowMonitor(); }));
            plc.DropDownItems.Add(DropItem("Ler programa do PLC", delegate { ShowReader(); }));

            ToolStripMenuItem ferramentas = MenuItem("Ferramentas");
            ferramentas.DropDownItems.Add(DropItem("Simular projeto atual", delegate { ShowSimulator(); }));
            ferramentas.DropDownItems.Add(DropItem("Verificar compatibilidade com o controlador", delegate { CheckPortability(); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem tp02 = MenuItem("Diagn\u00F3stico avan\u00E7ado TP02");
            tp02.DropDownItems.Add(DropItem("Testar link de programa\u00E7\u00E3o (PG)", delegate { LaunchInstalledTool("OpenLadderTP02PgLab.exe", "Laborat\u00F3rio PG TP02"); }));
            tp02.DropDownItems.Add(DropItem("Monitor MMI (somente leitura)", delegate { ShowTp02ReadOnlyMonitor(); }));
            tp02.DropDownItems.Add(new ToolStripSeparator());
            tp02.DropDownItems.Add(DropItem("Analisar projeto/serial PC12", delegate { ShowTp02BridgeLab(); }));
            tp02.DropDownItems.Add(DropItem("Capturar tr\u00E1fego serial PC12/TP02", delegate { LaunchInstalledTool("OpenLadderTP02Capture.exe", "Captura serial PC12/TP02"); }));
            tp02.DropDownItems.Add(DropItem("Decodificar RBP", delegate { ShowDecoder(); }));
            tp02.DropDownItems.Add(DropItem("Calibrar opcodes", delegate { ShowCalibration(); }));
            tp02.DropDownItems.Add(DropItem("Converter IL para Ladder", delegate { ShowIl(); }));
            ferramentas.DropDownItems.Add(tp02);

            ToolStripMenuItem ajuda = MenuItem("Ajuda");
            ajuda.DropDownItems.Add(DropItem("Verificar atualiza\u00E7\u00F5es", delegate { ShowUpdater(); }));
            ajuda.DropDownItems.Add(new ToolStripSeparator());
            ajuda.DropDownItems.Add(DropItem("Sobre o OpenLadder Studio", delegate
            {
                MessageBox.Show(this, "OpenLadder Studio\r\n\r\nEditor Ladder, simula\u00E7\u00E3o e comunica\u00E7\u00E3o com controladores compat\u00EDveis.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));

            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(exibir);
            menu.Items.Add(plc);
            menu.Items.Add(ferramentas);
            menu.Items.Add(ajuda);
            return menu;
        }

'@
$shell = Replace-Section $shell '        private MenuStrip BuildMenu()' '        private int toolCursor;' $menu 'menu funcional V66'

# Toolbar: somente acoes de uso frequente e com nomes que descrevem o resultado.
$toolbar = @'
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 52;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            toolCursor = 8;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Refazer", StudioIcon.Redo, false, delegate { InvokeLadder("Redo", null); });
            AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Conectar", StudioIcon.Plug, false, delegate { ShowCommunication(); });
            AddToolButton(bar, "Monitor", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            AddToolButton(bar, "Ler programa", StudioIcon.Download, false, delegate { ShowReader(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Controlador", StudioIcon.Chip, false, delegate { ShowDeviceManager(); });
            AddToolButton(bar, "Atualizar", StudioIcon.Refresh, false, delegate { ShowUpdater(); });
            return bar;
        }

'@
$shell = Replace-Section $shell '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar funcional V66'

# Navegacao lateral: remove ferramentas de engenharia reversa do fluxo cotidiano.
$nav = @'
        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 228;
            nav.BackColor = StudioTheme.NavBg;

            List<Control> items = new List<Control>();
            items.Add(BuildBrand());
            items.Add(new NavSection("Projeto"));
            items.Add(NavItem("Editor Ladder", StudioIcon.Ladder, "LD", delegate { ShowLadder(); }));
            items.Add(NavItem("Simular projeto", StudioIcon.Grid, "SIM", delegate { ShowSimulator(); }));
            items.Add(new NavSection("Controlador"));
            items.Add(NavItem("Selecionar controlador", StudioIcon.Chip, "DEV", delegate { ShowDeviceManager(); }));
            items.Add(NavItem("Conectar", StudioIcon.Plug, "PLC", delegate { ShowCommunication(); }));
            items.Add(NavItem("Monitor", StudioIcon.Monitor, "MON", delegate { ShowMonitor(); }));
            items.Add(NavItem("Ler programa", StudioIcon.Download, "RBP", delegate { ShowReader(); }));
            items.Add(new NavSection("Sistema"));
            items.Add(NavItem("Atualiza\u00E7\u00F5es", StudioIcon.Refresh, "UPD", delegate { ShowUpdater(); }));

            int i;
            for (i = items.Count - 1; i >= 0; i--) nav.Controls.Add(items[i]);
            return nav;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildNav()' '        private StudioPanel BuildConsole()' $nav 'navegacao V66'

$rail = @'
        private void UpdateRailCapabilities()
        {
            SetRailEnabled("LD", true);
            SetRailEnabled("SIM", true);
            SetRailEnabled("DEV", true);
            SetRailEnabled("UPD", true);

            bool connect = currentDriver != null && currentDriver.Capabilities.Connect;
            bool monitor = currentDriver != null && (currentDriver.Capabilities.MonitorBits || currentDriver.Capabilities.ReadRegisters);
            bool readProgram = currentDriver != null && currentDriver.Capabilities.ReadProgram;

            SetRailEnabled("PLC", connect);
            SetRailEnabled("MON", monitor);
            SetRailEnabled("RBP", readProgram);
        }

'@
$shell = Replace-Section $shell '        private void UpdateRailCapabilities()' '        private void ShowLadder()' $rail 'capacidades da navegacao V66'

$communication = @'
        private void ShowCommunication()
        {
            RefreshProfileUi();
            if (currentDriver == null || !currentDriver.Capabilities.Connect)
            {
                MessageBox.Show(this, "Selecione um controlador com comunica\u00E7\u00E3o dispon\u00EDvel antes de conectar.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowDeviceManager();
                return;
            }

            if (IsTp02())
            {
                ShowTp02ConnectionCenter();
                return;
            }

            if (IsGenericModbus())
            {
                ShowModbus("PLC");
                return;
            }

            MessageBox.Show(this, "A comunica\u00E7\u00E3o deste controlador ainda n\u00E3o possui uma tela operacional integrada.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowTp02ConnectionCenter()
        {
            Form dialog = new Form();
            dialog.Text = "Conex\u00E3o - WEG TP02";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.ClientSize = new Size(620, 330);
            dialog.BackColor = Workspace;
            dialog.Font = Font;

            Label title = new Label();
            title.Text = "Escolha o tipo de comunica\u00E7\u00E3o com o TP02";
            title.Font = new Font("Segoe UI Semibold", 13.0f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(30, 45, 58);
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            dialog.Controls.Add(title);

            Label info = new Label();
            info.Text = "O TP02 possui caminhos diferentes. Programa\u00E7\u00E3o PG e MMI/Computer Link n\u00E3o s\u00E3o a mesma interface.";
            info.ForeColor = Color.FromArgb(80, 92, 104);
            info.MaximumSize = new Size(565, 44);
            info.AutoSize = true;
            info.Location = new Point(26, 58);
            dialog.Controls.Add(info);

            Button pg = new Button();
            pg.Text = "Testar link de programa\u00E7\u00E3o (PG)";
            pg.Location = new Point(26, 112);
            pg.Size = new Size(270, 42);
            pg.FlatStyle = FlatStyle.Flat;
            pg.BackColor = Color.White;
            pg.Click += delegate { dialog.Close(); LaunchInstalledTool("OpenLadderTP02PgLab.exe", "Laborat\u00F3rio PG TP02"); };
            dialog.Controls.Add(pg);

            Label pgInfo = new Label();
            pgInfo.Text = "Para o cabo de programa\u00E7\u00E3o usado pelo PC12. Executa somente os testes PG permitidos pelo pacote seguro.";
            pgInfo.ForeColor = Color.FromArgb(80, 92, 104);
            pgInfo.MaximumSize = new Size(270, 56);
            pgInfo.AutoSize = true;
            pgInfo.Location = new Point(28, 160);
            dialog.Controls.Add(pgInfo);

            Button mmi = new Button();
            mmi.Text = "Abrir monitor MMI (leitura)";
            mmi.Location = new Point(322, 112);
            mmi.Size = new Size(270, 42);
            mmi.FlatStyle = FlatStyle.Flat;
            mmi.BackColor = Color.White;
            mmi.Click += delegate { dialog.Close(); ShowTp02ReadOnlyMonitor(); };
            dialog.Controls.Add(mmi);

            Label mmiInfo = new Label();
            mmiInfo.Text = "Para Computer Link na porta MMI. N\u00E3o deve ser confundido com o modo PG de programa\u00E7\u00E3o.";
            mmiInfo.ForeColor = Color.FromArgb(80, 92, 104);
            mmiInfo.MaximumSize = new Size(270, 56);
            mmiInfo.AutoSize = true;
            mmiInfo.Location = new Point(324, 160);
            dialog.Controls.Add(mmiInfo);

            Button close = new Button();
            close.Text = "Fechar";
            close.Location = new Point(492, 272);
            close.Size = new Size(100, 32);
            close.Click += delegate { dialog.Close(); };
            dialog.Controls.Add(close);

            dialog.ShowDialog(this);
            dialog.Dispose();
        }

        private void LaunchInstalledTool(string exeName, string friendlyName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
            if (!File.Exists(path))
            {
                MessageBox.Show(this, friendlyName + " n\u00E3o foi encontrado na instala\u00E7\u00E3o atual.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(path);
                statusText.Text = friendlyName + " aberto";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "N\u00E3o foi poss\u00EDvel abrir " + friendlyName + ".\r\n\r\n" + ex.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowTp02ReadOnlyMonitor()
        {
            if (!RequireTp02("O monitor MMI atual \u00E9 espec\u00EDfico do WEG TP02.")) return;
            if (tp02MonitorForm == null || tp02MonitorForm.IsDisposed)
            {
                tp02MonitorForm = new TP02ControlV31Form();
                tp02MonitorForm.SetReadOnlyMode(true);
            }
            inspector.Visible = false;
            ShowDocument(tp02MonitorForm, "Monitor MMI - WEG TP02", "MON");
            statusText.Text = "Monitor TP02 em modo somente leitura";
        }

        private void ShowTp02BridgeLab()
        {
            if (!RequireTp02("A an\u00E1lise PC12/TP02 \u00E9 uma ferramenta avan\u00E7ada espec\u00EDfica do WEG TP02.")) return;
            if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
            inspector.Visible = false;
            ShowDocument(bridgeForm, "Laborat\u00F3rio de an\u00E1lise TP02", "LABTP02");
            statusText.Text = "Diagn\u00F3stico avan\u00E7ado TP02";
        }

'@
$shell = Replace-Section $shell '        private void ShowCommunication()' '        private void ShowMonitor()' $communication 'comunicacao TP02 V66'

$monitor = @'
        private void ShowMonitor()
        {
            RefreshProfileUi();
            if (currentDriver == null || (!currentDriver.Capabilities.MonitorBits && !currentDriver.Capabilities.ReadRegisters))
            {
                MessageBox.Show(this, "O controlador selecionado ainda n\u00E3o oferece monitoramento integrado.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (IsGenericModbus())
            {
                ShowModbus("MON");
                return;
            }

            if (IsTp02())
            {
                ShowTp02ReadOnlyMonitor();
                return;
            }

            MessageBox.Show(this, "Monitoramento ainda n\u00E3o implementado para este controlador.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

'@
$shell = Replace-Section $shell '        private void ShowMonitor()' '        private void ShowModbus(string railCode)' $monitor 'monitor TP02 V66'

$sim = @'
        private void ShowSimulator()
        {
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                ladderForm = new LadderEditorForm();
                PrepareLadderForStudio(ladderForm);
            }

            UniversalLadderConversionReport report = UniversalLadderAdapter.FromEditor(ladderForm);
            if (report.ElementCount <= 0)
            {
                DialogResult choice = MessageBox.Show(this,
                    "O projeto atual ainda n\u00E3o possui elementos Ladder.\r\n\r\nDeseja abrir a simula\u00E7\u00E3o de demonstra\u00E7\u00E3o?",
                    "Simula\u00E7\u00E3o", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (choice != DialogResult.Yes) return;
            }

            if (simulatorForm == null || simulatorForm.IsDisposed) simulatorForm = new LadderSimulatorForm();
            if (report.ElementCount > 0)
            {
                simulatorForm.LoadProgram(report.Program);
                statusText.Text = "Simulando o projeto Ladder atual";
            }
            else
            {
                statusText.Text = "Simula\u00E7\u00E3o de demonstra\u00E7\u00E3o";
            }

            inspector.Visible = false;
            ShowDocument(simulatorForm, report.ElementCount > 0 ? "Simula\u00E7\u00E3o do projeto" : "Simula\u00E7\u00E3o de demonstra\u00E7\u00E3o", "SIM");
        }

'@
$shell = Replace-Section $shell '        private void ShowSimulator()' '        private void CheckPortability()' $sim 'simulacao explicita V66'

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# TP02 Computer Link: quando chamado pelo monitor do Studio, oculta comandos de
# escrita e RUN/STOP. As rotinas continuam no laboratorio interno, mas o fluxo
# operacional comum fica coerente com as capacidades de leitura do driver.
# -----------------------------------------------------------------------------
$tp02 = LF ([System.IO.File]::ReadAllText($tp02Path))
$anchor = '        private void LoadDefaults()'
if (-not $tp02.Contains('internal void SetReadOnlyMode(bool readOnly)')) {
    if (-not $tp02.Contains($anchor)) { throw 'LoadDefaults nao encontrado em TP02ControlV31.build.cs.' }
    $method = @'
        internal void SetReadOnlyMode(bool readOnly)
        {
            if (!readOnly) return;
            Text = "OpenLadder Studio - Monitor TP02";
            if (runButton != null) runButton.Visible = false;
            if (stopButton != null) stopButton.Visible = false;
            if (writeBitButton != null) writeBitButton.Visible = false;
            if (writeWordButton != null) writeWordButton.Visible = false;
            if (bitStateCombo != null) bitStateCombo.Visible = false;
            if (wordValueBox != null) wordValueBox.Visible = false;
            if (modeLabel != null) modeLabel.Text = "MMI COMPUTER LINK - SOMENTE LEITURA";
            ApplyReadOnlyLabels(this);
        }

        private static void ApplyReadOnlyLabels(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                Label label = control as Label;
                if (label != null)
                {
                    if (label.Text == "CONTROLE ONLINE - WEG TP02") label.Text = "MONITOR - WEG TP02";
                    if (label.Text.Contains("leitura, escrita e RUN/STOP")) label.Text = "Computer Link na porta MMI: leitura e diagn\u00F3stico";
                    if (label.Text.Contains("RUN, STOP, SCS e WRV")) label.Text = "Modo integrado do OpenLadder Studio: somente leitura. Comandos de escrita e RUN/STOP ficam ocultos.";
                }
                if (control.HasChildren) ApplyReadOnlyLabels(control);
            }
        }

'@
    $tp02 = $tp02.Replace($anchor, $method + $anchor)
}
[System.IO.File]::WriteAllText($tp02Path, $tp02, [System.Text.Encoding]::UTF8)

Write-Host 'V66 aplicada: ferramentas reorganizadas, TP02 separado por PG/MMI e monitor em modo de leitura.'
