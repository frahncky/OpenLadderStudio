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
        private const float Stroke = 2.2f;
        private const float StrokeOn = 2.9f;

        public static void Draw(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background, LadderPower power)
        {
            if (element == null || element.Type == LadderElementType.Empty) return;

            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = cell.Left + cell.Width / 2;
            bool on = power == LadderPower.On;
            Color ink = on ? OpenLadderPalette.Energized : OpenLadderPalette.Fore;

            switch (element.Type)
            {
                case LadderElementType.ContactNO:
                case LadderElementType.ContactNC:
                case LadderElementType.EdgeUp:
                case LadderElementType.EdgeDown:
                    DrawContact(g, element.Type, cx, y, background, ink, on);
                    DrawAddress(g, element.Address, cell, cx, branch ? y + 17 : y - 31, ink == OpenLadderPalette.Fore ? OpenLadderPalette.Info : ink);
                    break;

                case LadderElementType.Coil:
                case LadderElementType.Set:
                case LadderElementType.Reset:
                    DrawCoil(g, element.Type, cx, y, background, ink, on);
                    DrawAddress(g, element.Address, cell, cx, branch ? y + 17 : y - 31, ink == OpenLadderPalette.Fore ? OpenLadderPalette.Info : ink);
                    break;

                case LadderElementType.Timer:
                    DrawBlock(g, element.Mode == "RESET" ? "TMR-R" : "TMR", "IN", "Q", element.Address, element.Parameter, cell, cx, y, branch, power);
                    break;

                case LadderElementType.Counter:
                    DrawBlock(g, "CNT", "CU", "Q", element.Address, element.Parameter, cell, cx, y, branch, power);
                    break;

                case LadderElementType.Function:
                    DrawBlock(g, string.IsNullOrEmpty(element.Address) ? "FUN" : element.Address, "EN", "ENO", string.Empty, element.Parameter, cell, cx, y, branch, power);
                    break;

                default:
                    DrawBlock(g, "END", string.Empty, string.Empty, "F-00", string.Empty, cell, cx, y, branch, power);
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

        private static void DrawContact(Graphics g, LadderElementType type, int cx, int y, Color background, Color ink, bool on)
        {
            const int half = 14;
            const int bar = 14;

            // Apaga o fio entre as barras: o contato interrompe a linha.
            using (Pen clear = new Pen(background, 5.0f))
                g.DrawLine(clear, cx - half, y, cx + half, y);

            if (on)
            {
                using (Brush glow = new SolidBrush(OpenLadderPalette.Tint(ink, 48)))
                    g.FillRectangle(glow, cx - half, y - bar, half * 2, bar * 2);
            }

            using (Pen p = NewPen(ink, on))
            {
                g.DrawLine(p, cx - half, y - bar, cx - half, y + bar);
                g.DrawLine(p, cx + half, y - bar, cx + half, y + bar);

                // NF: barra inclinada dentro das barras do contato (IEC 61131-3).
                if (type == LadderElementType.ContactNC)
                    g.DrawLine(p, cx - 8, y + 11, cx + 8, y - 11);
            }

            string mark = type == LadderElementType.EdgeUp ? "P" : type == LadderElementType.EdgeDown ? "N" : string.Empty;
            DrawMark(g, mark, cx, y, ink);
        }

        private static void DrawCoil(Graphics g, LadderElementType type, int cx, int y, Color background, Color ink, bool on)
        {
            using (Pen clear = new Pen(background, 5.0f))
                g.DrawLine(clear, cx - 19, y, cx + 19, y);

            if (on)
            {
                using (Brush glow = new SolidBrush(OpenLadderPalette.Tint(ink, 48)))
                    g.FillEllipse(glow, cx - 20, y - 15, 40, 30);
            }

            using (Pen p = NewPen(ink, on))
            {
                // Dois parenteses afastados: ( ) como no padrao IEC.
                g.DrawArc(p, new Rectangle(cx - 21, y - 15, 13, 30), 90, 180);
                g.DrawArc(p, new Rectangle(cx + 8, y - 15, 13, 30), -90, 180);
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

        /// <summary>Endereco do operando, centrado e reduzido se nao couber na celula.</summary>
        private static void DrawAddress(Graphics g, string text, Rectangle cell, int cx, int top, Color colour)
        {
            if (string.IsNullOrEmpty(text)) return;
            float size = 11.5f;
            float available = cell.Width - 4;
            using (StringFormat format = Centered())
            {
                Font f = PixelFont("Consolas", size, FontStyle.Bold);
                try
                {
                    while (size > 8.0f && g.MeasureString(text, f, 1000, format).Width > available)
                    {
                        f.Dispose();
                        size -= 0.5f;
                        f = PixelFont("Consolas", size, FontStyle.Bold);
                    }
                    using (Brush b = new SolidBrush(colour))
                        g.DrawString(text, f, b, cx, top + 7.0f, format);
                }
                finally { f.Dispose(); }
            }
        }

        private static void DrawBlock(Graphics g, string title, string pinIn, string pinOut, string address, string operand,
            Rectangle cell, int cx, int y, bool branch, LadderPower power)
        {
            bool on = power == LadderPower.On;
            Color frame = on ? OpenLadderPalette.Energized : OpenLadderPalette.Rail;
            Color ink = on ? OpenLadderPalette.Energized : OpenLadderPalette.Fore;

            int width = Math.Max(64, Math.Min(cell.Width - 8, 92));
            int height = branch ? 40 : 50;
            Rectangle box = new Rectangle(cx - width / 2, y - height / 2, width, height);
            int header = branch ? 13 : 15;

            using (Brush fill = new SolidBrush(OpenLadderPalette.ChromeLight)) g.FillRectangle(fill, box);
            using (Brush band = new SolidBrush(on ? OpenLadderPalette.Tint(OpenLadderPalette.Energized, 70) : OpenLadderPalette.Border))
                g.FillRectangle(band, box.Left + 1, box.Top + 1, box.Width - 1, header);
            using (Pen border = new Pen(frame, on ? 2.2f : 1.6f)) g.DrawRectangle(border, box);

            using (StringFormat centered = Centered())
            using (Font titleFont = PixelFont("Segoe UI Semibold", 10.5f, FontStyle.Bold))
            using (Brush titleBrush = new SolidBrush(OpenLadderPalette.Fore))
                g.DrawString(title, titleFont, titleBrush, box.Left + box.Width / 2f, box.Top + 1 + header / 2f, centered);

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
                        if (!string.IsNullOrEmpty(pinIn)) g.DrawString(pinIn, small, pinBrush, box.Left + 4, pinY, left);
                        if (!string.IsNullOrEmpty(pinOut)) g.DrawString(pinOut, small, outBrush, box.Right - 4, pinY, right);
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
                    float textY = branch ? box.Top + header + (height - header) / 2f : box.Bottom - 9.0f;
                    g.DrawString(text, f, b, box.Left + box.Width / 2f, textY, centered);
                }
            }
        }
    }
}
