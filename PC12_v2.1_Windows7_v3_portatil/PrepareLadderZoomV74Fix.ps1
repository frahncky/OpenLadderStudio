$ErrorActionPreference = 'Stop'

$root = Get-Location
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
foreach ($p in @($ladderPath, $shellPath)) {
    if (-not (Test-Path $p)) { throw "V74: arquivo de build nao encontrado: $p" }
}

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "V74: ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}
function Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "V74: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "V74: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# -----------------------------------------------------------------------------
# LadderCanvas: escala unica para renderizacao, scroll e coordenadas do mouse.
# -----------------------------------------------------------------------------
$ladder = LF ([System.IO.File]::ReadAllText($ladderPath))

$zoomState = @'
        private int HoverColumn = -1;
        private float zoomFactor = 1.0f;

        public int ZoomPercent
        {
            get { return (int)Math.Round(zoomFactor * 100.0f); }
        }

        public void SetZoomPercent(int percent)
        {
            int target = Math.Max(50, Math.Min(200, percent));
            float next = target / 100.0f;
            if (Math.Abs(next - zoomFactor) < 0.001f) return;

            Point oldScroll = AutoScrollPosition;
            float oldZoom = zoomFactor;
            float centerX = (-oldScroll.X + ClientSize.Width / 2.0f) / Math.Max(0.01f, oldZoom);
            float centerY = (-oldScroll.Y + ClientSize.Height / 2.0f) / Math.Max(0.01f, oldZoom);

            zoomFactor = next;
            UpdateZoomExtent();

            int scrollX = Math.Max(0, (int)Math.Round(centerX * zoomFactor - ClientSize.Width / 2.0f));
            int scrollY = Math.Max(0, (int)Math.Round(centerY * zoomFactor - ClientSize.Height / 2.0f));
            AutoScrollPosition = new Point(scrollX, scrollY);
            Invalidate();
        }

        private void UpdateZoomExtent()
        {
            int count = Rungs == null ? 1 : Math.Max(1, Rungs.Count);
            int totalHeight = TopMargin + count * RungHeight + 44;
            int logicalWidth = Math.Max((int)Math.Ceiling(ClientSize.Width / Math.Max(0.01f, zoomFactor)) - RightMargin, 920);
            AutoScrollMinSize = new Size(
                Math.Max(1, (int)Math.Ceiling((logicalWidth + 40) * zoomFactor)),
                Math.Max(1, (int)Math.Ceiling(totalHeight * zoomFactor)));
        }
'@
$ladder = Required $ladder '        private int HoverColumn = -1;' $zoomState.TrimEnd() 'estado do zoom'

$ladder = Required $ladder '            AutoScrollMinSize = new Size(920, totalHeight);' '            UpdateZoomExtent();' 'extensao escalada'
$matrix = @'
            using (Matrix view = new Matrix(zoomFactor, 0.0f, 0.0f, zoomFactor, scroll.X, scroll.Y))
                g.Transform = view;
'@
$ladder = Required $ladder '            g.TranslateTransform(scroll.X, scroll.Y);' $matrix.TrimEnd() 'matriz do canvas'
$ladder = $ladder.Replace('            int width = Math.Max(ClientSize.Width - RightMargin, 920);',
                          '            int width = Math.Max((int)Math.Ceiling(ClientSize.Width / Math.Max(0.01f, zoomFactor)) - RightMargin, 920);')

$ladder = Required $ladder '            int px = e.X - scroll.X;' '            int px = (int)Math.Floor((e.X - scroll.X) / Math.Max(0.01f, zoomFactor));' 'hover X'
$ladder = Required $ladder '            int py = e.Y - scroll.Y;' '            int py = (int)Math.Floor((e.Y - scroll.Y) / Math.Max(0.01f, zoomFactor));' 'hover Y'
$ladder = Required $ladder '            int px = point.X - scroll.X;' '            int px = (int)Math.Floor((point.X - scroll.X) / Math.Max(0.01f, zoomFactor));' 'selecao X'
$ladder = Required $ladder '            int py = point.Y - scroll.Y;' '            int py = (int)Math.Floor((point.Y - scroll.Y) / Math.Max(0.01f, zoomFactor));' 'selecao Y'

$resize = @'
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateZoomExtent();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
'@
$ladder = Required $ladder '        protected override void OnPaint(PaintEventArgs e)' $resize.TrimEnd() 'resize do canvas'

$zoomMethods = @'
        private void ZoomIn()
        {
            SetEditorZoom(Math.Min(200, GetZoomPercent() + 10));
        }

        private void ZoomOut()
        {
            SetEditorZoom(Math.Max(50, GetZoomPercent() - 10));
        }

        private void ZoomReset()
        {
            SetEditorZoom(100);
        }

        private void SetEditorZoom(int percent)
        {
            if (canvas == null) return;
            canvas.SetZoomPercent(percent);
            if (statusLabel != null) statusLabel.Text = "Zoom: " + canvas.ZoomPercent.ToString() + "%";
        }

        private int GetZoomPercent()
        {
            return canvas == null ? 100 : canvas.ZoomPercent;
        }

        private void FormKeyDown(object sender, KeyEventArgs e)
'@
$ladder = Required $ladder '        private void FormKeyDown(object sender, KeyEventArgs e)' $zoomMethods.TrimEnd() 'comandos do formulario'

# V51 ja adicionou Ctrl+Y; usa essa linha como ancora estavel.
$keyAnchor = '            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.SuppressKeyPress = true; }'
$keyZoom = @'
            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.SuppressKeyPress = true; }
            else if (e.Control && (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus)) { ZoomIn(); e.SuppressKeyPress = true; }
            else if (e.Control && (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)) { ZoomOut(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D0) { ZoomReset(); e.SuppressKeyPress = true; }
'@
$ladder = Required $ladder $keyAnchor $keyZoom.TrimEnd() 'atalhos do editor'

if ($ladder -notmatch 'public int ZoomPercent') { throw 'V74: ZoomPercent ausente.' }
if ($ladder -notmatch 'new Matrix\(zoomFactor') { throw 'V74: matriz de escala ausente.' }
if ($ladder -notmatch 'point\.X - scroll\.X\) / Math\.Max') { throw 'V74: hit-testing sem escala.' }
[System.IO.File]::WriteAllText($ladderPath, $ladder, (New-Object System.Text.UTF8Encoding($false)))

# -----------------------------------------------------------------------------
# Shell: grupo compacto, menu, atalhos globais e status operacional.
# -----------------------------------------------------------------------------
$shell = LF ([System.IO.File]::ReadAllText($shellPath))
if (-not $shell.Contains('private Label v74ZoomLabel;')) {
    $shell = Required $shell '        private TreeNode v73ProjectRoot;' "        private TreeNode v73ProjectRoot;`n        private Label v74ZoomLabel;" 'campo do indicador'
}

# Faz RefreshProfileUi usar a leitura real do canvas. A substituicao ocorre antes
# da insercao do helper, para nao atingir o proprio V74UpdateModeText.
$shell = [Regex]::Replace($shell,
    '(?m)^\s*modeText\.Text = .*MODO: EDIÇÃO.*;$',
    '                V74UpdateModeText();')

$helpers = @'
        private int V74ReadZoomPercent()
        {
            if (ladderForm == null || ladderForm.IsDisposed) return 100;
            try
            {
                MethodInfo method = typeof(LadderEditorForm).GetMethod("GetZoomPercent", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) return 100;
                object value = method.Invoke(ladderForm, null);
                return value is int ? (int)value : 100;
            }
            catch { return 100; }
        }

        private void V74UpdateModeText()
        {
            int zoom = V74ReadZoomPercent();
            if (v74ZoomLabel != null) v74ZoomLabel.Text = zoom.ToString() + "%";
            if (modeText == null) return;
            string model = currentProfile == null ? "SEM PLC" : currentProfile.Model;
            string protocol = currentProfile == null ? "-" : currentProfile.Protocol;
            modeText.Text = "PLC: " + model + "    |    " + protocol + "    |    OFF-LINE    |    MODO: EDIÇÃO    |    ZOOM: " + zoom.ToString() + "%";
        }

        private void V74Zoom(string methodName)
        {
            ShowLadder();
            try
            {
                MethodInfo method = typeof(LadderEditorForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new MissingMethodException(methodName);
                method.Invoke(ladderForm, null);
                int zoom = V74ReadZoomPercent();
                if (statusText != null) statusText.Text = "Zoom do editor: " + zoom.ToString() + "%";
                V74UpdateModeText();
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                MessageBox.Show(this, inner.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void V74AddZoomControls(Control bar)
        {
            Panel group = new Panel();
            group.BackColor = Chrome;
            group.Bounds = new Rectangle(toolCursor + 4, 8, 126, 40);

            Button minus = new Button();
            minus.Text = "−";
            minus.Bounds = new Rectangle(0, 3, 34, 32);
            minus.FlatStyle = FlatStyle.Flat;
            minus.FlatAppearance.BorderColor = Border;
            minus.BackColor = ChromeLight;
            minus.ForeColor = Fore;
            minus.Font = new Font("Segoe UI Semibold", 11.0f, FontStyle.Bold);
            minus.Cursor = Cursors.Hand;
            minus.TabStop = false;
            minus.Click += delegate { V74Zoom("ZoomOut"); };
            group.Controls.Add(minus);

            v74ZoomLabel = new Label();
            v74ZoomLabel.Text = V74ReadZoomPercent().ToString() + "%";
            v74ZoomLabel.Bounds = new Rectangle(36, 3, 52, 32);
            v74ZoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            v74ZoomLabel.ForeColor = Fore;
            v74ZoomLabel.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            v74ZoomLabel.Cursor = Cursors.Hand;
            v74ZoomLabel.Click += delegate { V74Zoom("ZoomReset"); };
            group.Controls.Add(v74ZoomLabel);

            Button plus = new Button();
            plus.Text = "+";
            plus.Bounds = new Rectangle(90, 3, 34, 32);
            plus.FlatStyle = FlatStyle.Flat;
            plus.FlatAppearance.BorderColor = Border;
            plus.BackColor = ChromeLight;
            plus.ForeColor = Fore;
            plus.Font = new Font("Segoe UI Semibold", 10.0f, FontStyle.Bold);
            plus.Cursor = Cursors.Hand;
            plus.TabStop = false;
            plus.Click += delegate { V74Zoom("ZoomIn"); };
            group.Controls.Add(plus);

            bar.Controls.Add(group);
            toolCursor += 134;
        }

'@
$shell = Required $shell '        private Control BuildToolbar()' ($helpers + '        private Control BuildToolbar()') 'helpers do shell'

$toolbar = @'
        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 56;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            toolCursor = 8;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Compilar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolButton(bar, "Transferir", StudioIcon.Download, false, delegate { V73ShowTransferCenter(); });
            AddToolButton(bar, "Simulador", StudioIcon.Grid, false, delegate { ShowSimulator(); });
            AddToolButton(bar, "Monitorar", StudioIcon.Monitor, false, delegate { ShowMonitor(); });
            AddToolButton(bar, "Diagnóstico", StudioIcon.Gear, false, delegate { V73ShowDiagnostics(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Refazer", StudioIcon.Redo, false, delegate { InvokeLadder("Redo", null); });
            AddToolSeparator(bar);
            V74AddZoomControls(bar);
            return bar;
        }

'@
$shell = Section $shell '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar V74'

$exibirOld = @'
            exibir.DropDownItems.Add(miNav);
            exibir.DropDownItems.Add(miProps);
            exibir.DropDownItems.Add(miConsole);
'@
$exibirNew = @'
            exibir.DropDownItems.Add(miNav);
            exibir.DropDownItems.Add(miProps);
            exibir.DropDownItems.Add(miConsole);
            exibir.DropDownItems.Add(new ToolStripSeparator());
            exibir.DropDownItems.Add(DropItem("Diminuir zoom", delegate { V74Zoom("ZoomOut"); }));
            exibir.DropDownItems.Add(DropItem("Zoom 100%", delegate { V74Zoom("ZoomReset"); }));
            exibir.DropDownItems.Add(DropItem("Aumentar zoom", delegate { V74Zoom("ZoomIn"); }));
'@
$shell = Required $shell $exibirOld.TrimEnd() $exibirNew.TrimEnd() 'menu Exibir'

$keyOld = @'
            if (IsLadderActive())
            {
                if (keyData == (Keys.Control | Keys.Z))
'@
$keyNew = @'
            if (IsLadderActive())
            {
                if (keyData == (Keys.Control | Keys.Add) || keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Shift | Keys.Oemplus))
                {
                    V74Zoom("ZoomIn");
                    return true;
                }
                if (keyData == (Keys.Control | Keys.Subtract) || keyData == (Keys.Control | Keys.OemMinus))
                {
                    V74Zoom("ZoomOut");
                    return true;
                }
                if (keyData == (Keys.Control | Keys.D0))
                {
                    V74Zoom("ZoomReset");
                    return true;
                }
                if (keyData == (Keys.Control | Keys.Z))
'@
$shell = Required $shell $keyOld.TrimEnd() $keyNew.TrimEnd() 'atalhos globais'

if ($shell -notmatch 'V74AddZoomControls\(bar\)') { throw 'V74: grupo de zoom ausente.' }
if ($shell -notmatch 'ZOOM: " \+ zoom\.ToString\(\)') { throw 'V74: status dinamico ausente.' }
if ($shell -notmatch 'Diminuir zoom') { throw 'V74: menu de zoom ausente.' }
[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))

Write-Host 'V74 aplicada: zoom real 50%-200%, hit-testing escalado, atalhos e status dinamico.' -ForegroundColor Cyan
