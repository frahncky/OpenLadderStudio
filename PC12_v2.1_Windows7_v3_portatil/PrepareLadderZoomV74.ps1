$ErrorActionPreference = 'Stop'

$root = Get-Location
$ladderPath = Join-Path $root 'LadderEditor.build.cs'
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
foreach ($p in @($ladderPath, $shellPath)) {
    if (-not (Test-Path $p)) { throw "V74: arquivo de build nao encontrado: $p" }
}

function LF([string]$text) { return $text.Replace("`r`n", "`n") }
function Replace-Required([string]$text, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $text.Contains($needle)) { throw "V74: ancora nao encontrada ($label)." }
    return $text.Replace($needle, $replacement)
}
function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor)
    if ($start -lt 0) { throw "V74: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "V74: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

# =============================================================================
# Editor Ladder: zoom verdadeiro no canvas. Pintura, scroll e hit-testing usam
# a mesma transformacao, evitando o falso "100%" que existia antes da V73.
# =============================================================================
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
            float centerX = (-oldScroll.X + ClientSize.Width / 2.0f) / oldZoom;
            float centerY = (-oldScroll.Y + ClientSize.Height / 2.0f) / oldZoom;

            zoomFactor = next;
            UpdateZoomExtent();

            int scrollX = Math.Max(0, (int)Math.Round(centerX * zoomFactor - ClientSize.Width / 2.0f));
            int scrollY = Math.Max(0, (int)Math.Round(centerY * zoomFactor - ClientSize.Height / 2.0f));
            AutoScrollPosition = new Point(scrollX, scrollY);
            Invalidate();
        }

        private void UpdateZoomExtent()
        {
            int totalHeight = TopMargin + Math.Max(1, Rungs == null ? 1 : Rungs.Count) * RungHeight + 44;
            int logicalWidth = Math.Max((int)Math.Ceiling(ClientSize.Width / Math.Max(0.01f, zoomFactor)) - RightMargin, 920);
            AutoScrollMinSize = new Size(
                Math.Max(1, (int)Math.Ceiling((logicalWidth + 40) * zoomFactor)),
                Math.Max(1, (int)Math.Ceiling(totalHeight * zoomFactor)));
        }
'@
$ladder = Replace-Required $ladder '        private int HoverColumn = -1;' $zoomState.TrimEnd() 'estado de zoom do canvas'

$paintOld = @'
            int totalHeight = TopMargin + Math.Max(1, Rungs.Count) * RungHeight + 44;
            AutoScrollMinSize = new Size(920, totalHeight);
            Point scroll = AutoScrollPosition;
            g.TranslateTransform(scroll.X, scroll.Y);

            int width = Math.Max(ClientSize.Width - RightMargin, 920);
'@
$paintNew = @'
            int totalHeight = TopMargin + Math.Max(1, Rungs.Count) * RungHeight + 44;
            int width = Math.Max((int)Math.Ceiling(ClientSize.Width / Math.Max(0.01f, zoomFactor)) - RightMargin, 920);
            UpdateZoomExtent();
            Point scroll = AutoScrollPosition;
            using (Matrix view = new Matrix(zoomFactor, 0.0f, 0.0f, zoomFactor, scroll.X, scroll.Y))
                g.Transform = view;
'@
$ladder = Replace-Required $ladder $paintOld.TrimEnd() $paintNew.TrimEnd() 'transformacao de pintura'

# Hover e selecao convertem coordenadas fisicas para a mesma geometria logica.
$ladder = Replace-Required $ladder '            int px = e.X - scroll.X;' '            int px = (int)Math.Floor((e.X - scroll.X) / Math.Max(0.01f, zoomFactor));' 'hover X'
$ladder = Replace-Required $ladder '            int py = e.Y - scroll.Y;' '            int py = (int)Math.Floor((e.Y - scroll.Y) / Math.Max(0.01f, zoomFactor));' 'hover Y'
$ladder = Replace-Required $ladder '            int px = point.X - scroll.X;' '            int px = (int)Math.Floor((point.X - scroll.X) / Math.Max(0.01f, zoomFactor));' 'hit-test X'
$ladder = Replace-Required $ladder '            int py = point.Y - scroll.Y;' '            int py = (int)Math.Floor((point.Y - scroll.Y) / Math.Max(0.01f, zoomFactor));' 'hit-test Y'
$ladder = $ladder.Replace('            int width = Math.Max(ClientSize.Width - RightMargin, 920);',
                          '            int width = Math.Max((int)Math.Ceiling(ClientSize.Width / Math.Max(0.01f, zoomFactor)) - RightMargin, 920);')

# Atualiza extensao quando o usuario redimensiona o canvas.
$resizeAnchor = '        protected override void OnPaint(PaintEventArgs e)'
$resizeMethod = @'
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateZoomExtent();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
'@
$ladder = Replace-Required $ladder $resizeAnchor $resizeMethod.TrimEnd() 'resize com zoom'

# Comandos do formulario, usados tanto pelo editor isolado quanto pelo shell.
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
            statusLabel.Text = "Zoom: " + canvas.ZoomPercent.ToString() + "%";
        }

        private int GetZoomPercent()
        {
            return canvas == null ? 100 : canvas.ZoomPercent;
        }

        private void FormKeyDown(object sender, KeyEventArgs e)
'@
$ladder = Replace-Required $ladder '        private void FormKeyDown(object sender, KeyEventArgs e)' $zoomMethods.TrimEnd() 'comandos de zoom'

# Atalhos tambem funcionam no editor standalone.
$keyOld = @'
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelectedElement(); e.SuppressKeyPress = true; }
'@
$keyNew = @'
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.SuppressKeyPress = true; }
            else if (e.Control && (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus)) { ZoomIn(); e.SuppressKeyPress = true; }
            else if (e.Control && (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)) { ZoomOut(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.D0) { ZoomReset(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelectedElement(); e.SuppressKeyPress = true; }
'@
$ladder = Replace-Required $ladder $keyOld.TrimEnd() $keyNew.TrimEnd() 'atalhos standalone'

# Guardrails do canvas.
if ($ladder -notmatch 'public int ZoomPercent') { throw 'V74: propriedade ZoomPercent ausente.' }
if ($ladder -notmatch 'new Matrix\(zoomFactor') { throw 'V74: escala grafica nao aplicada.' }
if ($ladder -notmatch 'point\.X - scroll\.X\) / Math\.Max') { throw 'V74: hit-testing nao escalado.' }

[System.IO.File]::WriteAllText($ladderPath, $ladder, (New-Object System.Text.UTF8Encoding($false)))

# =============================================================================
# Shell: controles compactos de zoom, atalhos globais e status sincronizado.
# =============================================================================
$shell = LF ([System.IO.File]::ReadAllText($shellPath))

if (-not $shell.Contains('private Label v74ZoomLabel;')) {
    $shell = Replace-Required $shell '        private TreeNode v73ProjectRoot;' "        private TreeNode v73ProjectRoot;`n        private Label v74ZoomLabel;" 'campo do zoom no shell'
}

# RefreshProfileUi deixa de escrever um percentual estatico; usa o valor real.
$shell = [Regex]::Replace($shell,
    '(?m)^\s*modeText\.Text = .*MODO: EDIÇÃO.*;$',
    '                V74UpdateModeText();')

$zoomHelpers = @'
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
$shell = $shell.Replace('        private Control BuildToolbar()', $zoomHelpers + '        private Control BuildToolbar()')

# Toolbar V73 + grupo compacto de zoom.
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
$shell = Replace-Section $shell '        private Control BuildToolbar()' '        private NavButton NavItem' $toolbar 'toolbar com zoom V74'

# Zoom tambem fica no menu Exibir.
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
$shell = Replace-Required $shell $exibirOld.TrimEnd() $exibirNew.TrimEnd() 'menu Exibir zoom'

# Atalhos globais: funcionam mesmo quando o foco esta no painel de projeto.
$keyAnchor = @'
            if (IsLadderActive())
            {
                if (keyData == (Keys.Control | Keys.Z))
'@
$keyReplacement = @'
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
$shell = Replace-Required $shell $keyAnchor.TrimEnd() $keyReplacement.TrimEnd() 'atalhos globais de zoom'

# Ao voltar para a aba Ladder, o indicador e sincronizado com o canvas.
$shell = $shell.Replace('            UpdateProjectName();`n        }', '            UpdateProjectName();`n            V74UpdateModeText();`n        }')

if ($shell -notmatch 'V74AddZoomControls\(bar\)') { throw 'V74: controles de zoom nao aplicados.' }
if ($shell -notmatch 'ZOOM: " \+ zoom\.ToString\(\)') { throw 'V74: status de zoom nao aplicado.' }
if ($shell -notmatch 'ZoomReset') { throw 'V74: reset de zoom nao aplicado.' }

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'V74 aplicada: zoom real 50%-200%, controles compactos, atalhos e status sincronizado.' -ForegroundColor Cyan
