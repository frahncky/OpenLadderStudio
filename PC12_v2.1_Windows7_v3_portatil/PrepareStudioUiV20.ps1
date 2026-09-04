$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path (Get-Location) 'StudioUi.cs'
$outputPath = Join-Path (Get-Location) 'StudioUi.build.cs'
$text = [System.IO.File]::ReadAllText($sourcePath)

$startAnchor = '    internal sealed class IconToolButton : StudioControl'
$endAnchor = '    internal sealed class StudioTab'

$start = $text.IndexOf($startAnchor)
if ($start -lt 0) {
    throw 'Classe IconToolButton não encontrada em StudioUi.cs.'
}

$end = $text.IndexOf($endAnchor, $start + $startAnchor.Length)
if ($end -lt 0) {
    throw 'Fim da classe IconToolButton não encontrado em StudioUi.cs.'
}

$replacement = @'
    /// <summary>Botao da barra de ferramentas: icone em cima, rotulo embaixo.</summary>
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

        private static Color IconColor(StudioIcon icon)
        {
            switch (icon)
            {
                case StudioIcon.Doc:      return Color.FromArgb(91, 170, 245);   // azul - novo
                case StudioIcon.Folder:   return Color.FromArgb(238, 186, 76);   // âmbar - abrir/arquivos
                case StudioIcon.Save:     return Color.FromArgb(78, 201, 176);   // turquesa - salvar
                case StudioIcon.Undo:     return Color.FromArgb(190, 132, 235);  // violeta - desfazer
                case StudioIcon.Plus:     return Color.FromArgb(80, 200, 120);   // verde - adicionar
                case StudioIcon.Minus:    return Color.FromArgb(224, 102, 102);  // vermelho suave - remover
                case StudioIcon.Check:    return Color.FromArgb(72, 200, 136);   // verde - validar
                case StudioIcon.Plug:     return Color.FromArgb(244, 164, 96);   // laranja - comunicação
                case StudioIcon.Download: return Color.FromArgb(88, 166, 230);   // azul - download
                case StudioIcon.Refresh:  return Color.FromArgb(100, 149, 237);  // azul royal - atualizar
                case StudioIcon.Chip:     return Color.FromArgb(74, 169, 229);   // azul claro - controlador
                case StudioIcon.Gear:     return Color.FromArgb(172, 150, 220);  // lilás - configurações
                case StudioIcon.Ladder:   return StudioTheme.Accent;             // verde OpenLadder
                case StudioIcon.Convert:  return Color.FromArgb(80, 190, 205);   // ciano - conversão
                case StudioIcon.Terminal: return Color.FromArgb(158, 186, 96);   // oliva - terminal
                case StudioIcon.Bolt:     return Color.FromArgb(245, 190, 72);   // amarelo - energia
                case StudioIcon.Monitor:  return Color.FromArgb(67, 192, 201);   // ciano - monitor
                case StudioIcon.Grid:     return Color.FromArgb(132, 164, 215);  // azul acinzentado - grade
                default:                  return StudioTheme.Fore;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = pressed ? StudioTheme.Shell : hover ? StudioTheme.ChromeLight : StudioTheme.Chrome;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Color iconColor = IconColor(Icon);
            Rectangle chip = new Rectangle((Width - 28) / 2, 3, 28, 28);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(hover ? 48 : 28, iconColor)))
                g.FillEllipse(b, chip);

            StudioGlyph.Draw(g, Icon, new Rectangle((Width - 20) / 2, 7, 20, 20), iconColor);

            Color labelColor = Emphasis ? iconColor : hover ? StudioTheme.Fore : StudioTheme.Muted;
            TextRenderer.DrawText(g, Text, StudioTheme.Small, new Rectangle(0, 31, Width, 16),
                labelColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

'@

$text = $text.Substring(0, $start) + $replacement + $text.Substring($end)
[System.IO.File]::WriteAllText($outputPath, $text, [System.Text.Encoding]::UTF8)
