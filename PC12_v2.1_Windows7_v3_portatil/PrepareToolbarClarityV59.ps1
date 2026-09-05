$ErrorActionPreference = 'Stop'

$root = Get-Location
$uiPath = Join-Path $root 'StudioUi.build.cs'
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
foreach ($p in @($uiPath, $shellPath)) {
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
# Toolbar: largura baseada no texto real e sem elipse. Isso evita "Conect..."
# em DPI/escala maiores e protege os demais comandos da mesma regressao.
# -----------------------------------------------------------------------------
$ui = LF ([System.IO.File]::ReadAllText($uiPath))
$tool = @'
    internal sealed class IconToolButton : StudioControl
    {
        private bool hover;
        private bool pressed;
        public StudioIcon Icon = StudioIcon.None;
        public bool Emphasis;

        public IconToolButton()
        {
            Height = 46;
            Cursor = Cursors.Hand;
            BackColor = StudioTheme.Chrome;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        public int MeasureWidth()
        {
            Font font = Emphasis ? StudioTheme.UiBold : StudioTheme.Ui;
            int label = TextRenderer.MeasureText(Text ?? string.Empty, font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            return Math.Max(84, label + 50);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color semantic = StudioIconPalette.For(Icon);
            Color back = pressed ? StudioTheme.Shell : hover ? StudioTheme.ChromeLight : StudioTheme.Chrome;
            Rectangle tile = new Rectangle(3, 3, Math.Max(1, Width - 6), Math.Max(1, Height - 6));
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, tile);

            if (Emphasis)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(42, semantic))) g.FillRectangle(b, tile);
                using (Pen p = new Pen(Color.FromArgb(150, semantic), 1f)) g.DrawRectangle(p, tile.X, tile.Y, tile.Width - 1, tile.Height - 1);
            }
            else if (hover)
            {
                using (Pen p = new Pen(StudioTheme.Border, 1f)) g.DrawRectangle(p, tile.X, tile.Y, tile.Width - 1, tile.Height - 1);
            }

            StudioGlyph.Draw(g, Icon, new Rectangle(12, (Height - 18) / 2, 18, 18), semantic);
            Color labelColor = Emphasis || hover || Focused ? StudioTheme.Fore : StudioTheme.Muted;
            TextRenderer.DrawText(g, Text, Emphasis ? StudioTheme.UiBold : StudioTheme.Ui,
                new Rectangle(36, 0, Math.Max(1, Width - 40), Height), labelColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            if (Focused)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(6, 6, Math.Max(1, Width - 12), Math.Max(1, Height - 12)), StudioTheme.Fore, back);
        }
    }

'@
$ui = Replace-Section $ui '    internal sealed class IconToolButton : StudioControl' '    internal sealed class StudioTab' $tool 'IconToolButton V59'
[System.IO.File]::WriteAllText($uiPath, $ui, [System.Text.Encoding]::UTF8)

# Shell: respeita a medicao real, garante Conectar com folga e alinha altura ao bar.
$shell = LF ([System.IO.File]::ReadAllText($shellPath))
$shell = $shell.Replace('            b.Height = 54;', '            b.Height = 46;')
$shell = [regex]::Replace($shell,
    '            b\.Width = Math\.Max\(b\.MeasureWidth\(\), text == "Conectar" \? 112 : 88\);',
    '            b.Width = Math.Max(b.MeasureWidth(), text == "Conectar" ? 132 : 92);')

# Se uma etapa anterior nao tiver o minimo especial, ainda aplica a regra final.
$shell = $shell.Replace('            b.Width = b.MeasureWidth();', '            b.Width = Math.Max(b.MeasureWidth(), text == "Conectar" ? 132 : 92);')

# Nomenclatura consistente no menu PLC.
$shell = $shell.Replace('plc.DropDownItems.Add(DropItem("Comunicação", delegate { ShowCommunication(); }));',
                       'plc.DropDownItems.Add(DropItem("Conectar", delegate { ShowCommunication(); }));')

[System.IO.File]::WriteAllText($shellPath, $shell, [System.Text.Encoding]::UTF8)
Write-Host 'UI V59 aplicada: toolbar sem elipse, Conectar completo e dimensoes resistentes a DPI.'
