$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'StudioUi.build.cs'
if (-not (Test-Path $path)) { throw 'StudioUi.build.cs nao encontrado. Execute PrepareStudioUiV20.ps1 antes.' }
$text = [System.IO.File]::ReadAllText($path)

function Replace-Required([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $haystack.Contains($needle)) { throw "Ancora nao encontrada em StudioUi.build.cs ($label)." }
    return $haystack.Replace($needle, $replacement)
}

function Replace-Section([string]$haystack, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $haystack.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado em StudioUi.build.cs ($label)." }
    $end = $haystack.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado em StudioUi.build.cs ($label)." }
    return $haystack.Substring(0, $start) + $replacement + $haystack.Substring($end)
}

# Paleta inspirada em IDEs industriais modernas: casco azul-grafite, area de trabalho clara e teal como estado ativo.
$text = Replace-Required $text '        public static readonly Color Shell = Color.FromArgb(27, 29, 32);' '        public static readonly Color Shell = Color.FromArgb(18, 24, 31);' 'Shell'
$text = Replace-Required $text '        public static readonly Color Chrome = Color.FromArgb(35, 38, 42);' '        public static readonly Color Chrome = Color.FromArgb(27, 36, 46);' 'Chrome'
$text = Replace-Required $text '        public static readonly Color ChromeLight = Color.FromArgb(46, 50, 55);' '        public static readonly Color ChromeLight = Color.FromArgb(38, 49, 62);' 'ChromeLight'
$text = Replace-Required $text '        public static readonly Color Border = Color.FromArgb(57, 62, 68);' '        public static readonly Color Border = Color.FromArgb(55, 68, 82);' 'Border'
$text = Replace-Required $text '        public static readonly Color Accent = Color.FromArgb(45, 170, 107);' '        public static readonly Color Accent = Color.FromArgb(38, 166, 154);' 'Accent'
$text = Replace-Required $text '        public static readonly Color AccentDark = Color.FromArgb(34, 135, 83);' '        public static readonly Color AccentDark = Color.FromArgb(28, 128, 119);' 'AccentDark'
$text = Replace-Required $text '        public static readonly Color Workspace = Color.FromArgb(235, 238, 241);' '        public static readonly Color Workspace = Color.FromArgb(244, 247, 250);' 'Workspace'
$text = Replace-Required $text '        public static readonly Color NavBg = Color.FromArgb(22, 24, 27);' '        public static readonly Color NavBg = Color.FromArgb(20, 27, 35);' 'NavBg'
$text = Replace-Required $text '        public static readonly Color NavHover = Color.FromArgb(40, 43, 47);' '        public static readonly Color NavHover = Color.FromArgb(31, 41, 52);' 'NavHover'
$text = Replace-Required $text '        public static readonly Color NavActive = Color.FromArgb(45, 49, 54);' '        public static readonly Color NavActive = Color.FromArgb(34, 46, 58);' 'NavActive'

$nav = @'
    internal sealed class NavButton : StudioControl
    {
        private bool hover;
        public StudioIcon Icon = StudioIcon.None;
        public string Key = "";
        public bool Active;

        public NavButton()
        {
            Height = 40;
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            BackColor = StudioTheme.NavBg;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
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

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            bool on = Enabled;
            Color back = !on ? StudioTheme.NavBg : Active ? StudioTheme.NavActive : hover ? StudioTheme.NavHover : StudioTheme.NavBg;
            Rectangle tile = new Rectangle(6, 2, Math.Max(1, Width - 12), Math.Max(1, Height - 4));
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, tile);

            Color semantic = StudioIconPalette.For(Icon);
            if (Active && on)
                using (SolidBrush b = new SolidBrush(semantic))
                    g.FillRectangle(b, new Rectangle(6, 7, 3, Math.Max(1, Height - 14)));

            Color fore = !on ? StudioTheme.Disabled : Active || hover ? StudioTheme.Fore : StudioTheme.Muted;
            Color iconColor = !on ? StudioTheme.Disabled : Active || hover ? semantic : Color.FromArgb(185, semantic);
            StudioGlyph.Draw(g, Icon, new Rectangle(18, (Height - 18) / 2, 18, 18), iconColor);
            TextRenderer.DrawText(g, Text, Active && on ? StudioTheme.UiBold : StudioTheme.Ui,
                new Rectangle(47, 0, Math.Max(1, Width - 58), Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (Focused && on)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(10, 5, Math.Max(1, Width - 20), Math.Max(1, Height - 10)), StudioTheme.Fore, back);
        }
    }

'@
$text = Replace-Section $text '    internal sealed class NavButton : StudioControl' '    /// <summary>Botao da barra de ferramentas: icone em cima, rotulo embaixo.</summary>' $nav 'NavButton V21'

$tool = @'
    internal sealed class IconToolButton : StudioControl
    {
        private bool hover;
        private bool pressed;
        public StudioIcon Icon = StudioIcon.None;
        public bool Emphasis;

        public IconToolButton()
        {
            Height = 40;
            Cursor = Cursors.Hand;
            BackColor = StudioTheme.Chrome;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        public int MeasureWidth()
        {
            int label = TextRenderer.MeasureText(Text, StudioTheme.Ui).Width;
            return Math.Max(76, label + 46);
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
            Rectangle tile = new Rectangle(3, 4, Math.Max(1, Width - 6), Math.Max(1, Height - 8));
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

            StudioGlyph.Draw(g, Icon, new Rectangle(13, (Height - 18) / 2, 18, 18), semantic);
            Color label = Emphasis || hover || Focused ? StudioTheme.Fore : StudioTheme.Muted;
            TextRenderer.DrawText(g, Text, Emphasis ? StudioTheme.UiBold : StudioTheme.Ui,
                new Rectangle(39, 0, Math.Max(1, Width - 45), Height), label,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (Focused)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(6, 7, Math.Max(1, Width - 12), Math.Max(1, Height - 14)), StudioTheme.Fore, back);
        }
    }

'@
$text = Replace-Section $text '    internal sealed class IconToolButton : StudioControl' '    internal sealed class StudioTab' $tool 'IconToolButton V21'

$text = Replace-Required $text '            Height = 32;\n            Dock = DockStyle.Top;\n            BackColor = StudioTheme.Shell;' '            Height = 36;\n            Dock = DockStyle.Top;\n            BackColor = StudioTheme.Chrome;' 'DocTabStrip'
$text = Replace-Required $text '            BackColor = Color.FromArgb(22, 24, 27);' '            BackColor = Color.FromArgb(17, 23, 30);' 'console'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'Studio UI V21 aplicada.'
