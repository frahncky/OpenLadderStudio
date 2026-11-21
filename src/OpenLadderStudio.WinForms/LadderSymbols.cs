using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ModernPC12
{
    /// <summary>
    /// Estado de energizacao de um elemento na simulacao. Unknown e o padrao do
    /// editor (sem simulacao) e tambem o que se mostra quando o resultado da
    /// linha nao permite afirmar se aquele elemento especifico conduz.
    /// </summary>
    internal enum LadderPower
    {
        Unknown,
        Off,
        On
    }

    /// <summary>
    /// Simbolos do diagrama Ladder no padrao IEC 61131-3: contato NA <c>--| |--</c>,
    /// NF <c>--|/|--</c>, bordas <c>|P|</c> e <c>|N|</c>, bobinas <c>( )</c>, <c>(S)</c>
    /// e <c>(R)</c>, e blocos funcionais com cabecalho, pinos de entrada/saida e
    /// operandos. Toda a geometria esta em unidades logicas do canvas e os textos
    /// em pixels (GraphicsUnit.Pixel), para que desenho e rotulo cresçam juntos em
    /// qualquer zoom e em qualquer escala de DPI. As cores vem de OpenLadderPalette.
    /// </summary>
    internal static class LadderSymbols
    {
        private const float Stroke = 2.4f;
        private const float StrokeOn = 3.0f;

        public static void Draw(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background, LadderPower power)
        {
            if (element == null || element.Type == LadderElementType.Empty) return;

            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = cell.Left + cell.Width / 2;
            bool on = power == LadderPower.On;
            Color ink = on ? OpenLadderPalette.Energized : OpenLadderPalette.Fore;
            Color label = on ? OpenLadderPalette.Energized : OpenLadderPalette.Info;

            switch (element.Type)
            {
                case LadderElementType.ContactNO:
                case LadderElementType.ContactNC:
                case LadderElementType.EdgeUp:
                case LadderElementType.EdgeDown:
                    DrawContact(g, element.Type, cx, y, background, ink, on);
                    DrawAddress(g, element.Address, cell, cx, branch ? y + 17 : y - 31, label, on);
                    break;

                case LadderElementType.Coil:
                case LadderElementType.Set:
                case LadderElementType.Reset:
                    DrawCoil(g, element.Type, cx, y, background, ink, on);
                    DrawAddress(g, element.Address, cell, cx, branch ? y + 17 : y - 31, label, on);
                    break;

                case LadderElementType.Timer:
                    DrawBlock(g, element.Mode == "RESET" ? "TMR-R" : "TMR", "IN", "Q", element.Address, element.Parameter, cell, cx, y, branch, power, OpenLadderPalette.Accent);
                    break;

                case LadderElementType.Counter:
                    DrawBlock(g, "CNT", "CU", "Q", element.Address, element.Parameter, cell, cx, y, branch, power, OpenLadderPalette.Info);
                    break;

                case LadderElementType.Function:
                    DrawBlock(g, string.IsNullOrEmpty(element.Address) ? "FUN" : element.Address, "EN", "ENO", string.Empty, element.Parameter, cell, cx, y, branch, power, OpenLadderPalette.Focus);
                    break;

                default:
                    DrawBlock(g, "END", string.Empty, string.Empty, "F-00", string.Empty, cell, cx, y, branch, power, OpenLadderPalette.Muted);
                    break;
            }

            g.SmoothingMode = previous;
        }

        private static Pen NewPen(Color colour, bool on)
        {
            Pen pen = new Pen(colour, on ? StrokeOn : Stroke);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }

        private static Font PixelFont(string family, float pixels, FontStyle style)
        {
            return new Font(family, pixels, style, GraphicsUnit.Pixel);
        }

        /// <summary>Fundo arredondado discreto do simbolo; aceso, ganha a cor de energia.</summary>
        private static void DrawChip(Graphics g, Rectangle box, int radius, Color ink, bool on)
        {
            using (GraphicsPath path = OpenLadderMetrics.RoundedRect(box, radius))
            {
                using (Brush fill = new SolidBrush(OpenLadderPalette.Tint(ink, on ? 46 : 16)))
                    g.FillPath(fill, path);
                using (Pen edge = new Pen(OpenLadderPalette.Tint(ink, on ? 120 : 46), 1.0f))
                    g.DrawPath(edge, path);
            }
        }

        private static void DrawContact(Graphics g, LadderElementType type, int cx, int y, Color background, Color ink, bool on)
        {
            const int half = 12;
            const int bar = 11;

            // Apaga o fio dentro do simbolo: o contato interrompe a linha.
            using (Pen clear = new Pen(background, 6.0f))
                g.DrawLine(clear, cx - half - 5, y, cx + half + 5, y);

            DrawChip(g, new Rectangle(cx - half - 6, y - bar - 3, (half + 6) * 2, (bar + 3) * 2), 7, ink, on);

            using (Pen p = NewPen(ink, on))
            {
                g.DrawLine(p, cx - half - 6, y, cx - half, y);
                g.DrawLine(p, cx + half, y, cx + half + 6, y);
                g.DrawLine(p, cx - half, y - bar, cx - half, y + bar);
                g.DrawLine(p, cx + half, y - bar, cx + half, y + bar);

                // NF: barra inclinada dentro das barras do contato (IEC 61131-3).
                if (type == LadderElementType.ContactNC)
                    g.DrawLine(p, cx - 7, y + 9, cx + 7, y - 9);
            }

            string mark = type == LadderElementType.EdgeUp ? "P" : type == LadderElementType.EdgeDown ? "N" : string.Empty;
            DrawMark(g, mark, cx, y, ink);
        }

        private static void DrawCoil(Graphics g, LadderElementType type, int cx, int y, Color background, Color ink, bool on)
        {
            using (Pen clear = new Pen(background, 6.0f))
                g.DrawLine(clear, cx - 21, y, cx + 21, y);

            using (Brush fill = new SolidBrush(OpenLadderPalette.Tint(ink, on ? 52 : 16)))
                g.FillEllipse(fill, cx - 16, y - 14, 32, 28);

            using (Pen p = NewPen(ink, on))
            {
                // Dois parenteses: ( ) como no padrao IEC.
                g.DrawLine(p, cx - 23, y, cx - 18, y);
                g.DrawLine(p, cx + 18, y, cx + 23, y);
                g.DrawArc(p, new Rectangle(cx - 20, y - 14, 13, 28), 100, 160);
                g.DrawArc(p, new Rectangle(cx + 7, y - 14, 13, 28), -80, 160);
            }

            string mark = type == LadderElementType.Set ? "S" : type == LadderElementType.Reset ? "R" : string.Empty;
            DrawMark(g, mark, cx, y, ink);
        }

        private static void DrawMark(Graphics g, string mark, int cx, int y, Color ink)
        {
            if (string.IsNullOrEmpty(mark)) return;
            using (Font f = PixelFont("Segoe UI Semibold", 13.0f, FontStyle.Bold))
            using (Brush b = new SolidBrush(ink))
            using (StringFormat format = Centered())
                g.DrawString(mark, f, b, cx, y + 0.5f, format);
        }

        private static StringFormat Centered()
        {
            StringFormat format = new StringFormat(StringFormat.GenericTypographic);
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags |= StringFormatFlags.NoWrap;
            return format;
        }

        /// <summary>Endereco do operando numa etiqueta arredondada, reduzido se nao couber na celula.</summary>
        private static void DrawAddress(Graphics g, string text, Rectangle cell, int cx, int top, Color colour, bool on)
        {
            if (string.IsNullOrEmpty(text)) return;
            float size = 11.5f;
            float available = cell.Width - 14;
            using (StringFormat format = Centered())
            {
                Font f = PixelFont("Consolas", size, FontStyle.Bold);
                try
                {
                    SizeF measured = g.MeasureString(text, f, 1000, format);
                    while (size > 8.0f && measured.Width > available)
                    {
                        f.Dispose();
                        size -= 0.5f;
                        f = PixelFont("Consolas", size, FontStyle.Bold);
                        measured = g.MeasureString(text, f, 1000, format);
                    }
                    int width = (int)Math.Ceiling(measured.Width) + 12;
                    Rectangle pill = new Rectangle(cx - width / 2, top, width, 15);
                    using (GraphicsPath path = OpenLadderMetrics.RoundedRect(pill, 7))
                    using (Brush back = new SolidBrush(OpenLadderPalette.Tint(colour, on ? 52 : 30)))
                        g.FillPath(back, path);
                    using (Brush b = new SolidBrush(colour))
                        g.DrawString(text, f, b, cx, top + 8.0f, format);
                }
                finally { f.Dispose(); }
            }
        }

        private static void DrawBlock(Graphics g, string title, string pinIn, string pinOut, string address, string operand,
            Rectangle cell, int cx, int y, bool branch, LadderPower power, Color kind)
        {
            bool on = power == LadderPower.On;
            Color accent = on ? OpenLadderPalette.Energized : kind;

            int width = Math.Max(64, Math.Min(cell.Width - 8, 92));
            int height = branch ? 40 : 52;
            Rectangle box = new Rectangle(cx - width / 2, y - height / 2, width, height);
            int header = branch ? 14 : 16;

            using (GraphicsPath path = OpenLadderMetrics.RoundedRect(box, 7))
            {
                using (Brush fill = new SolidBrush(OpenLadderPalette.ChromeLight)) g.FillPath(fill, path);
                Region clip = g.Clip;
                g.SetClip(path, CombineMode.Intersect);
                using (Brush band = new SolidBrush(OpenLadderPalette.Tint(accent, on ? 90 : 64)))
                    g.FillRectangle(band, box.Left, box.Top, box.Width + 1, header);
                g.Clip = clip;
                clip.Dispose();
                using (Pen border = new Pen(on ? accent : OpenLadderPalette.Tint(accent, 150), on ? 2.2f : 1.4f)) g.DrawPath(border, path);
            }

            using (StringFormat centered = Centered())
            using (Font titleFont = PixelFont("Segoe UI Semibold", 10.5f, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(OpenLadderPalette.Fore))
                g.DrawString(title, titleFont, titleBrush, box.Left + box.Width / 2f, box.Top + header / 2f + 0.5f, centered);

            using (Font small = PixelFont("Segoe UI", 9.5f, FontStyle.Regular))
            using (Brush pinBrush = new SolidBrush(OpenLadderPalette.Muted))
            using (Brush outBrush = new SolidBrush(on ? OpenLadderPalette.Energized : OpenLadderPalette.Muted))
            {
                if (!branch)
                {
                    float pinY = box.Top + header + 8.5f;
                    using (StringFormat left = new StringFormat(StringFormat.GenericTypographic))
                    using (StringFormat right = new StringFormat(StringFormat.GenericTypographic))
                    {
                        left.LineAlignment = StringAlignment.Center;
                        right.LineAlignment = StringAlignment.Center;
                        right.Alignment = StringAlignment.Far;
                        if (!string.IsNullOrEmpty(pinIn)) g.DrawString(pinIn, small, pinBrush, box.Left + 5, pinY, left);
                        if (!string.IsNullOrEmpty(pinOut)) g.DrawString(pinOut, small, outBrush, box.Right - 5, pinY, right);
                    }
                }
            }

            string body = (address + " " + operand).Trim();
            if (body.Length > 0)
            {
                using (Font f = PixelFont("Consolas", branch ? 9.5f : 10.0f, FontStyle.Bold))
                using (Brush b = new SolidBrush(on ? OpenLadderPalette.Energized : OpenLadderPalette.Info))
                using (StringFormat centered = Centered())
                {
                    string text = body.Length > 12 ? body.Substring(0, 12) : body;
                    float textY = branch ? box.Top + header + (height - header) / 2f : box.Bottom - 10.0f;
                    g.DrawString(text, f, b, box.Left + box.Width / 2f, textY, centered);
                }
            }
        }
    }
}
