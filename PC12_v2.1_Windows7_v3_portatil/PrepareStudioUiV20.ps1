$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path (Get-Location) 'StudioUi.cs'
$outputPath = Join-Path (Get-Location) 'StudioUi.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

function Replace-Section([string]$haystack, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $haystack.IndexOf($startAnchor)
    if ($start -lt 0) { throw "Inicio nao encontrado em StudioUi.cs ($label)." }
    $end = $haystack.IndexOf($endAnchor, $start + $startAnchor.Length)
    if ($end -lt 0) { throw "Fim nao encontrado em StudioUi.cs ($label)." }
    return $haystack.Substring(0, $start) + $replacement + $haystack.Substring($end)
}

function Replace-Required([string]$haystack, [string]$needle, [string]$replacement, [string]$label) {
    if (-not $haystack.Contains($needle)) { throw "Ancora nao encontrada em StudioUi.cs ($label)." }
    return $haystack.Replace($needle, $replacement)
}

# Ajuste leve do tema para um contraste mais limpo e consistente.
$text = Replace-Required $text '        public static readonly Color Shell = Color.FromArgb(29, 31, 34);' '        public static readonly Color Shell = Color.FromArgb(27, 29, 32);' 'cor Shell'
$text = Replace-Required $text '        public static readonly Color Chrome = Color.FromArgb(37, 39, 43);' '        public static readonly Color Chrome = Color.FromArgb(35, 38, 42);' 'cor Chrome'
$text = Replace-Required $text '        public static readonly Color ChromeLight = Color.FromArgb(47, 50, 55);' '        public static readonly Color ChromeLight = Color.FromArgb(46, 50, 55);' 'cor ChromeLight'
$text = Replace-Required $text '        public static readonly Color Border = Color.FromArgb(61, 64, 69);' '        public static readonly Color Border = Color.FromArgb(57, 62, 68);' 'cor Border'
$text = Replace-Required $text '        public static readonly Color NavBg = Color.FromArgb(24, 26, 29);' '        public static readonly Color NavBg = Color.FromArgb(22, 24, 27);' 'cor NavBg'

$paletteAnchor = '    /// <summary>Item da navegacao lateral: icone, rotulo e marca de selecao.</summary>'
$paletteIndex = $text.IndexOf($paletteAnchor)
if ($paletteIndex -lt 0) { throw 'Ponto de insercao da paleta de icones nao encontrado.' }

$palette = @'
    /// <summary>Paleta semantica compartilhada pelos icones do estudio.</summary>
    internal static class StudioIconPalette
    {
        public static Color For(StudioIcon icon)
        {
            switch (icon)
            {
                case StudioIcon.Doc:      return Color.FromArgb(91, 170, 245);
                case StudioIcon.Folder:   return Color.FromArgb(238, 186, 76);
                case StudioIcon.Save:     return Color.FromArgb(78, 201, 176);
                case StudioIcon.Undo:     return Color.FromArgb(190, 132, 235);
                case StudioIcon.Plus:     return Color.FromArgb(80, 200, 120);
                case StudioIcon.Minus:    return Color.FromArgb(224, 102, 102);
                case StudioIcon.Check:    return Color.FromArgb(72, 200, 136);
                case StudioIcon.Plug:     return Color.FromArgb(244, 164, 96);
                case StudioIcon.Download: return Color.FromArgb(88, 166, 230);
                case StudioIcon.Refresh:  return Color.FromArgb(100, 149, 237);
                case StudioIcon.Chip:     return Color.FromArgb(74, 169, 229);
                case StudioIcon.Gear:     return Color.FromArgb(172, 150, 220);
                case StudioIcon.Ladder:   return StudioTheme.Accent;
                case StudioIcon.Convert:  return Color.FromArgb(80, 190, 205);
                case StudioIcon.Terminal: return Color.FromArgb(158, 186, 96);
                case StudioIcon.Bolt:     return Color.FromArgb(245, 190, 72);
                case StudioIcon.Monitor:  return Color.FromArgb(67, 192, 201);
                case StudioIcon.Grid:     return Color.FromArgb(132, 164, 215);
                default:                  return StudioTheme.Fore;
            }
        }
    }

'@
$text = $text.Substring(0, $paletteIndex) + $palette + $text.Substring($paletteIndex)

$navReplacement = @'
    internal sealed class NavButton : StudioControl
    {
        private bool hover;
        public StudioIcon Icon = StudioIcon.None;
        public string Key = "";
        public bool Active;

        public NavButton()
        {
            Height = 34;
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
            Color back = !on ? StudioTheme.NavBg
                : Active ? StudioTheme.NavActive : hover ? StudioTheme.NavHover : StudioTheme.NavBg;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Color semantic = StudioIconPalette.For(Icon);
            if (Active && on)
                using (SolidBrush b = new SolidBrush(semantic))
                    g.FillRectangle(b, new Rectangle(0, 0, 3, Height));

            Color fore = !on ? StudioTheme.Disabled
                : Active || hover ? StudioTheme.Fore : StudioTheme.Muted;
            Color iconColor = !on ? StudioTheme.Disabled : Active || hover ? semantic : StudioTheme.Faint;
            StudioGlyph.Draw(g, Icon, new Rectangle(18, (Height - 16) / 2, 16, 16), iconColor);
            TextRenderer.DrawText(g, Text, Active && on ? StudioTheme.UiBold : StudioTheme.Ui,
                new Rectangle(44, 0, Width - 52, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            if (Focused && on)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(6, 3, Math.Max(1, Width - 10), Math.Max(1, Height - 6)), StudioTheme.Fore, back);
        }
    }

'@
$text = Replace-Section $text '    internal sealed class NavButton : StudioControl' '    /// <summary>Botao da barra de ferramentas: icone em cima, rotulo embaixo.</summary>' $navReplacement 'NavButton'

$toolReplacement = @'
    internal sealed class IconToolButton : StudioControl
    {
        private bool hover;
        private bool pressed;
        public StudioIcon Icon = StudioIcon.None;
        public bool Emphasis;

        public IconToolButton()
        {
            Height = 54;
            Cursor = Cursors.Hand;
            BackColor = StudioTheme.Chrome;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        public int MeasureWidth()
        {
            int w = TextRenderer.MeasureText(Text, StudioTheme.Small).Width + 18;
            return Math.Max(56, w);
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
            Color back = pressed ? StudioTheme.Shell : hover ? StudioTheme.ChromeLight : StudioTheme.Chrome;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Color iconColor = StudioIconPalette.For(Icon);
            bool compact = Height <= 44;
            int iconSize = compact ? 18 : 20;
            int iconY = compact ? 4 : 8;
            int chipSize = iconSize + 8;
            int chipY = Math.Max(1, iconY - 3);
            Rectangle chip = new Rectangle((Width - chipSize) / 2, chipY, chipSize, chipSize);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(hover || Focused ? 54 : 30, iconColor)))
                g.FillEllipse(b, chip);

            StudioGlyph.Draw(g, Icon, new Rectangle((Width - iconSize) / 2, iconY, iconSize, iconSize), iconColor);

            int labelY = compact ? 25 : 32;
            int labelHeight = Math.Max(13, Height - labelY - 1);
            Color labelColor = Emphasis ? iconColor : hover || Focused ? StudioTheme.Fore : StudioTheme.Muted;
            TextRenderer.DrawText(g, Text, StudioTheme.Small, new Rectangle(0, labelY, Width, labelHeight),
                labelColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

            if (hover)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(145, iconColor)))
                    g.FillRectangle(b, new Rectangle(8, Height - 2, Math.Max(1, Width - 16), 2));

            if (Focused)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 2, Math.Max(1, Width - 6), Math.Max(1, Height - 4)), StudioTheme.Fore, back);
        }
    }

'@
$text = Replace-Section $text '    internal sealed class IconToolButton : StudioControl' '    internal sealed class StudioTab' $toolReplacement 'IconToolButton'

$tabNeedle = @'
                Color fore = active ? StudioTheme.Fore : StudioTheme.Muted;
                StudioGlyph.Draw(g, t.Icon, new Rectangle(r.X + 12, (Height - 14) / 2, 14, 14),
                    active ? StudioTheme.Accent : StudioTheme.Faint);
'@
$tabReplacement = @'
                Color fore = active ? StudioTheme.Fore : StudioTheme.Muted;
                Color tabIcon = StudioIconPalette.For(t.Icon);
                StudioGlyph.Draw(g, t.Icon, new Rectangle(r.X + 12, (Height - 14) / 2, 14, 14),
                    active ? tabIcon : StudioTheme.Faint);
'@
$text = Replace-Required $text $tabNeedle.TrimEnd() $tabReplacement.TrimEnd() 'cores das abas'

[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
