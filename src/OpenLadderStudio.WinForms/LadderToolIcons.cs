using System.Drawing.Drawing2D;

namespace OpenLadderStudio.WinForms;

/// <summary>Ferramentas da caixa do diagrama que têm ícone próprio.</summary>
public enum LadderToolIcon
{
    Select,
    ContactNO,
    ContactNC,
    ParallelNO,
    ParallelNC,
    Coil,
    Timer,
    Counter,
    Set,
    Reset,
    RisingEdge,
    FallingEdge,
    Function,
    End,
    Erase
}

/// <summary>
/// Ícones da caixa de ferramentas desenhados como o próprio símbolo Ladder, com as cores da
/// paleta. São gerados no tamanho pedido (já em pixels do monitor) e refeitos ao trocar o tema.
/// </summary>
public static class LadderToolIcons
{
    public static Bitmap Create(LadderToolIcon icon, int size)
    {
        var bitmap = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(size / 24f, size / 24f);
        Color ink = ModernPalette.Fore;
        Color accent = ModernPalette.Info;
        using var pen = new Pen(ink, 1.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var wire = new Pen(Color.FromArgb(150, ModernPalette.Muted), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        switch (icon)
        {
            case LadderToolIcon.Select:
                using (var fill = new SolidBrush(ink))
                {
                    var arrow = new[] { new PointF(6, 3), new PointF(6, 19), new PointF(10, 15), new PointF(13, 21), new PointF(15.5f, 20), new PointF(12.5f, 14), new PointF(18, 14) };
                    g.FillPolygon(fill, arrow);
                }
                break;
            case LadderToolIcon.ContactNO:
            case LadderToolIcon.ContactNC:
                Contact(g, pen, wire, 12, icon == LadderToolIcon.ContactNC);
                break;
            case LadderToolIcon.ParallelNO:
            case LadderToolIcon.ParallelNC:
                using (var branch = new Pen(accent, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(branch, new[] { new PointF(2, 6), new PointF(2, 16), new PointF(7.5f, 16) });
                using (var branch = new Pen(accent, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(branch, new[] { new PointF(16.5f, 16), new PointF(22, 16), new PointF(22, 6) });
                g.DrawLine(wire, 2, 6, 22, 6);
                Contact(g, pen, null, 16, icon == LadderToolIcon.ParallelNC, 4.5f, 5.5f);
                break;
            case LadderToolIcon.Coil:
            case LadderToolIcon.Set:
            case LadderToolIcon.Reset:
                g.DrawLine(wire, 1, 12, 5, 12);
                g.DrawLine(wire, 19, 12, 23, 12);
                g.DrawArc(pen, 5, 5, 7, 14, 100, 160);
                g.DrawArc(pen, 12, 5, 7, 14, -80, 160);
                if (icon != LadderToolIcon.Coil) Letter(g, icon == LadderToolIcon.Set ? "S" : "R", 12, 12, 9f, ink);
                break;
            case LadderToolIcon.RisingEdge:
            case LadderToolIcon.FallingEdge:
                Contact(g, pen, wire, 12, false);
                using (var arrow = new Pen(accent, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    bool up = icon == LadderToolIcon.RisingEdge;
                    g.DrawLine(arrow, 12, 7.5f, 12, 16.5f);
                    g.DrawLines(arrow, up
                        ? new[] { new PointF(9.8f, 10), new PointF(12, 7.5f), new PointF(14.2f, 10) }
                        : new[] { new PointF(9.8f, 14), new PointF(12, 16.5f), new PointF(14.2f, 14) });
                }
                break;
            case LadderToolIcon.End:
                g.DrawLine(wire, 1, 12, 13, 12);
                using (var stop = new SolidBrush(ink))
                {
                    g.FillRectangle(stop, 14, 4, 2.6f, 16);
                    g.FillRectangle(stop, 18.4f, 4, 2.6f, 16);
                }
                break;
            case LadderToolIcon.Timer:
            case LadderToolIcon.Counter:
            case LadderToolIcon.Function:
                Color kind = icon switch
                {
                    LadderToolIcon.Timer => ModernPalette.Accent,
                    LadderToolIcon.Counter => ModernPalette.Info,
                    _ => ModernPalette.Focus
                };
                Block(g, kind, icon switch
                {
                    LadderToolIcon.Timer => "T",
                    LadderToolIcon.Counter => "C",
                    _ => "F"
                }, ink);
                break;
            case LadderToolIcon.Erase:
                using (var danger = new Pen(ModernPalette.Danger, 1.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLines(danger, new[] { new PointF(5, 7), new PointF(19, 7) });
                    g.DrawLines(danger, new[] { new PointF(9.5f, 7), new PointF(10, 4), new PointF(14, 4), new PointF(14.5f, 7) });
                    g.DrawLines(danger, new[] { new PointF(7, 7), new PointF(8, 20), new PointF(16, 20), new PointF(17, 7) });
                    g.DrawLine(danger, 10.5f, 10.5f, 10.5f, 16.5f);
                    g.DrawLine(danger, 13.5f, 10.5f, 13.5f, 16.5f);
                }
                break;
        }
        return bitmap;
    }

    private static void Contact(Graphics g, Pen pen, Pen wire, float y, bool closedSlash, float half = 4.5f, float bar = 6.5f)
    {
        if (wire != null)
        {
            g.DrawLine(wire, 1, y, 12 - half, y);
            g.DrawLine(wire, 12 + half, y, 23, y);
        }
        g.DrawLine(pen, 12 - half, y - bar, 12 - half, y + bar);
        g.DrawLine(pen, 12 + half, y - bar, 12 + half, y + bar);
        if (closedSlash) g.DrawLine(pen, 9.5f, y + bar - 1.5f, 14.5f, y - bar + 1.5f);
    }

    private static void Block(Graphics g, Color kind, string text, Color ink)
    {
        var box = new RectangleF(3, 4, 18, 16);
        using var path = Rounded(box, 3.5f);
        using (var fill = new SolidBrush(Color.FromArgb(70, kind))) g.FillPath(fill, path);
        using (var edge = new Pen(kind, 1.5f)) g.DrawPath(edge, path);
        Letter(g, text, 12, 12.3f, 10f, ink);
    }

    private static void Letter(Graphics g, string text, float x, float y, float pixels, Color ink)
    {
        using var font = new Font("Segoe UI Semibold", pixels, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ink);
        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, x, y, format);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
