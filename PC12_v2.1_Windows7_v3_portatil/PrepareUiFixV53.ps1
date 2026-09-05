$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$updaterPath = Join-Path $root 'PC12Updater.build.cs'

foreach ($p in @($shellPath, $updaterPath)) {
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

$shell = LF ([System.IO.File]::ReadAllText($shellPath))

$shell = Replace-Required $shell '            b.Width = b.MeasureWidth();' '            b.Width = Math.Max(b.MeasureWidth(), text == "Conectar" ? 112 : 88);' 'largura toolbar'

# Mantem apenas o verificador visivel; nao executa instalacao automatica oculta.
$shell = $shell.Replace('            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { StartAutomaticUpdater(); })); };', '')

$brand = @'
        private Control BuildBrand()
        {
            StudioPanel brand = new StudioPanel();
            brand.Dock = DockStyle.Top;
            brand.Height = 64;
            brand.Fill = StudioTheme.NavBg;
            brand.BottomLine = Border;

            System.Drawing.Icon brandIcon = null;
            try { brandIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            brand.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                if (brandIcon != null)
                    g.DrawIcon(brandIcon, new Rectangle(16, 12, 40, 40));
                else
                {
                    using (SolidBrush b = new SolidBrush(Accent)) g.FillRectangle(b, new Rectangle(18, 14, 36, 36));
                    StudioGlyph.Draw(g, StudioIcon.Ladder, new Rectangle(25, 21, 22, 22), Color.White);
                }
                TextRenderer.DrawText(g, "OpenLadder Studio", new Font("Segoe UI Semibold", 11.2f, FontStyle.Bold),
                    new Point(68, 22), Fore);
            };
            return brand;
        }

'@
$shell = Replace-Section $shell '        private Control BuildBrand()' '        private Panel BuildNav()' $brand 'brand enxuta'

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
            list.Padding = new Padding(10, 6, 8, 10);

            elementButtons.Clear();
            AddElementTool(list, "Selecionar", StudioIcon.Select, LadderTool.Select);
            AddElementTool(list, "Contato NA", StudioIcon.ContactNO, LadderTool.ContactNO);
            AddElementTool(list, "Contato NF", StudioIcon.ContactNC, LadderTool.ContactNC);
            AddElementTool(list, "Ramo paralelo NA", StudioIcon.ContactNO, LadderTool.ParallelNO);
            AddElementTool(list, "Ramo paralelo NF", StudioIcon.ContactNC, LadderTool.ParallelNC);
            AddElementTool(list, "Bobina", StudioIcon.Coil, LadderTool.Coil);
            AddElementTool(list, "Temporizador", StudioIcon.Timer, LadderTool.Timer);
            AddElementTool(list, "Contador", StudioIcon.Counter, LadderTool.Counter);
            AddElementTool(list, "SET", StudioIcon.Check, LadderTool.Set);
            AddElementTool(list, "RESET", StudioIcon.Refresh, LadderTool.Reset);
            AddElementTool(list, "Borda de subida", StudioIcon.Bolt, LadderTool.EdgeUp);
            AddElementTool(list, "Borda de descida", StudioIcon.Bolt, LadderTool.EdgeDown);
            AddElementTool(list, "Função especial", StudioIcon.Chip, LadderTool.Function);
            AddElementTool(list, "END", StudioIcon.Terminal, LadderTool.End);
            AddElementAction(list, "Adicionar linha", StudioIcon.Plus, delegate { InvokeLadder("AddRung", null); });
            AddElementAction(list, "Remover linha", StudioIcon.Minus, delegate { InvokeLadder("DeleteSelectedRung", null); });

            host.Controls.Add(list);
            host.Controls.Add(title);
            return host;
        }

'@
$shell = Replace-Section $shell '        private Panel BuildElementLibrary()' '        private void AddElementTool' $elementLibrary 'lista simples de elementos'

$shell = $shell.Replace('            statusText.Text = "Editor Ladder universal";', '            statusText.Text = "Pronto";')
$shell = $shell.Replace('            statusText.Text = "Modelo Ladder universal verificado";', '            statusText.Text = "Verificação concluída";')

# O ponto estavel e a ativacao do LinkLabel, independente da redacao anterior.
$noticeVisible = '                        updateNotice.Visible = true;'
$noticeExpanded = @'
                        updateNotice.Text = "● NOVA VERSÃO v" + latestVersion + " — ATUALIZAR";
                        updateNotice.Width = 300;
                        updateNotice.LinkColor = Color.FromArgb(251, 191, 36);
                        updateNotice.ActiveLinkColor = Color.White;
                        updateNotice.Visible = true;
                        if (statusText != null)
                        {
                            statusText.Text = "Nova versão v" + latestVersion + " disponível.";
                            statusText.ForeColor = Color.FromArgb(251, 191, 36);
                        }
                        MessageBox.Show(this,
                            "Uma nova versão do OpenLadder Studio está disponível: v" + latestVersion + ".\r\n\r\nClique em Atualizar para instalar.",
                            "Nova versão disponível", MessageBoxButtons.OK, MessageBoxIcon.Information);
'@
$shell = Replace-Required $shell $noticeVisible $noticeExpanded.TrimEnd() 'aviso visivel de update'

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

$updater = LF ([System.IO.File]::ReadAllText($updaterPath))
$launchOld = @'
                Process.Start(psi);
                Close();
'@
$launchNew = @'
                statusLabel.Text = "Instalador iniciado. Fechando o OpenLadder Studio...";
                Process.Start(psi);
                Application.Exit();
'@
$updater = Replace-Required $updater $launchOld.TrimEnd() $launchNew.TrimEnd() 'fechar Studio no update'
[System.IO.File]::WriteAllText($updaterPath, $updater, [System.Text.Encoding]::UTF8)

Write-Host 'V53 aplicada: update visivel, fechamento/reabertura, lista simples, textos enxutos e toolbar sem clipping.'
