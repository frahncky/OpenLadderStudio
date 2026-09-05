$ErrorActionPreference = 'Stop'

$root = Get-Location
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
$uiPath = Join-Path $root 'StudioUi.build.cs'
$updaterPath = Join-Path $root 'PC12Updater.build.cs'

foreach ($p in @($shellPath, $uiPath, $updaterPath)) {
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
# Toolbar: largura calculada com a fonte usada no estado de destaque.
# -----------------------------------------------------------------------------
$ui = LF ([System.IO.File]::ReadAllText($uiPath))
$measureOld = @'
        public int MeasureWidth()
        {
            int label = TextRenderer.MeasureText(Text, StudioTheme.Ui).Width;
            return Math.Max(76, label + 46);
        }
'@
$measureNew = @'
        public int MeasureWidth()
        {
            int label = TextRenderer.MeasureText(Text, StudioTheme.UiBold).Width;
            return Math.Max(92, label + 54);
        }
'@
$ui = Replace-Required $ui $measureOld.TrimEnd() $measureNew.TrimEnd() 'largura toolbar'
[System.IO.File]::WriteAllText($uiPath, $ui, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Shell: lista simples de elementos, sem busca, sem apagar; identidade enxuta.
# -----------------------------------------------------------------------------
$shell = LF ([System.IO.File]::ReadAllText($shellPath))

# A verificacao de update deve ocorrer uma unica vez ao abrir, sem updater oculto
# instalando silenciosamente por tras da interface.
$shell = $shell.Replace('            Shown += delegate { CheckForUpdatesInBackground(); };', '')
$shell = $shell.Replace('            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { StartAutomaticUpdater(); })); };', '')
$ctorAnchor = @'
            RefreshProfileUi();
            ShowLadder();
'@
$ctorReplacement = @'
            RefreshProfileUi();
            ShowLadder();
            Shown += delegate { BeginInvoke(new MethodInvoker(delegate { CheckForUpdatesInBackground(); })); };
'@
$shell = Replace-Required $shell $ctorAnchor.TrimEnd() $ctorReplacement.TrimEnd() 'check update no startup'

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

# Remove textos de status redundantes.
$shell = $shell.Replace('            statusText.Text = "Editor Ladder universal";', '            statusText.Text = "Pronto";')
$shell = $shell.Replace('            statusText.Text = "Modelo Ladder universal verificado";', '            statusText.Text = "Verificação concluída";')

# Aviso de versao deve ser impossivel de passar despercebido: destaque persistente
# na barra inferior e uma notificacao unica por abertura.
$noticeOld = @'
                        updateNotice.Text = "Atualização v" + latestVersion + " disponível";
                        updateNotice.Visible = true;
'@
$noticeNew = @'
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
$shell = Replace-Required $shell $noticeOld.TrimEnd() $noticeNew.TrimEnd() 'aviso visivel de update'

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)

# -----------------------------------------------------------------------------
# Updater: ao iniciar o instalador encerra o aplicativo inteiro, nao apenas a
# aba/formulario do atualizador. O FormClosing do Studio salva a sessao.
# -----------------------------------------------------------------------------
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
