using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class DirectStudioProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DirectStudioForm());
        }
    }

    /// <summary>Paleta unica do OpenLadder Studio.</summary>
    internal static class StudioTheme
    {
        public static readonly Color Shell = Color.FromArgb(29, 31, 34);
        public static readonly Color Chrome = Color.FromArgb(37, 39, 43);
        public static readonly Color ChromeLight = Color.FromArgb(47, 50, 55);
        public static readonly Color Border = Color.FromArgb(61, 64, 69);
        public static readonly Color Accent = Color.FromArgb(45, 170, 107);
        public static readonly Color AccentDark = Color.FromArgb(34, 135, 83);
        public static readonly Color Workspace = Color.FromArgb(235, 238, 241);
        public static readonly Color Fore = Color.FromArgb(226, 230, 234);
        public static readonly Color Muted = Color.FromArgb(150, 157, 164);

        public static readonly Color NavBg = Color.FromArgb(24, 26, 29);
        public static readonly Color NavHover = Color.FromArgb(40, 43, 47);
        public static readonly Color NavActive = Color.FromArgb(45, 49, 54);
        public static readonly Color Faint = Color.FromArgb(108, 116, 124);

        public static readonly Color Danger = Color.FromArgb(203, 78, 66);
        public static readonly Color Warning = Color.FromArgb(206, 145, 55);
        public static readonly Color Info = Color.FromArgb(88, 158, 220);

        public static readonly Font Ui = new Font("Segoe UI", 8.75f);
        public static readonly Font UiBold = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 7.75f);
        public static readonly Font Section = new Font("Segoe UI Semibold", 7.25f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 8.25f);
        public static readonly Font MonoBold = new Font("Consolas", 8.25f, FontStyle.Bold);
    }

    internal enum StudioIcon
    {
        None, Doc, Folder, Save, Undo, Plus, Minus, Check, Plug, Download,
        Refresh, Chip, Gear, Ladder, Convert, Terminal, Close, Bolt, Monitor
    }

    /// <summary>Icones vetoriais em GDI+, sem depender de arquivos externos.</summary>
    internal static class StudioGlyph
    {
        private static Pen NewPen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        public static void Draw(Graphics g, StudioIcon icon, Rectangle r, Color c)
        {
            if (icon == StudioIcon.None) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x = r.X, y = r.Y, w = r.Width, h = r.Height;
            float cx = x + w / 2f, cy = y + h / 2f;

            using (Pen p = NewPen(c, 1.5f))
            using (SolidBrush b = new SolidBrush(c))
            {
                switch (icon)
                {
                    case StudioIcon.Doc:
                        g.DrawPolygon(p, new PointF[] {
                            new PointF(x + w * 0.22f, y + h * 0.08f), new PointF(x + w * 0.62f, y + h * 0.08f),
                            new PointF(x + w * 0.80f, y + h * 0.28f), new PointF(x + w * 0.80f, y + h * 0.92f),
                            new PointF(x + w * 0.22f, y + h * 0.92f) });
                        g.DrawLine(p, x + w * 0.36f, y + h * 0.54f, x + w * 0.66f, y + h * 0.54f);
                        g.DrawLine(p, x + w * 0.36f, y + h * 0.72f, x + w * 0.66f, y + h * 0.72f);
                        break;

                    case StudioIcon.Folder:
                        g.DrawPolygon(p, new PointF[] {
                            new PointF(x + w * 0.10f, y + h * 0.82f), new PointF(x + w * 0.10f, y + h * 0.20f),
                            new PointF(x + w * 0.42f, y + h * 0.20f), new PointF(x + w * 0.52f, y + h * 0.34f),
                            new PointF(x + w * 0.90f, y + h * 0.34f), new PointF(x + w * 0.90f, y + h * 0.82f) });
                        break;

                    case StudioIcon.Save:
                        g.DrawRectangle(p, x + w * 0.14f, y + h * 0.14f, w * 0.72f, h * 0.72f);
                        g.DrawRectangle(p, x + w * 0.32f, y + h * 0.14f, w * 0.36f, h * 0.26f);
                        g.DrawRectangle(p, x + w * 0.28f, y + h * 0.56f, w * 0.44f, h * 0.30f);
                        break;

                    case StudioIcon.Undo:
                        g.DrawArc(p, x + w * 0.16f, y + h * 0.20f, w * 0.68f, h * 0.62f, 150, 260);
                        g.FillPolygon(b, new PointF[] {
                            new PointF(x + w * 0.14f, y + h * 0.08f), new PointF(x + w * 0.12f, y + h * 0.50f),
                            new PointF(x + w * 0.48f, y + h * 0.32f) });
                        break;

                    case StudioIcon.Refresh:
                        g.DrawArc(p, x + w * 0.16f, y + h * 0.20f, w * 0.68f, h * 0.62f, 40, 260);
                        g.FillPolygon(b, new PointF[] {
                            new PointF(x + w * 0.86f, y + h * 0.08f), new PointF(x + w * 0.88f, y + h * 0.50f),
                            new PointF(x + w * 0.52f, y + h * 0.32f) });
                        break;

                    case StudioIcon.Plus:
                        g.DrawLine(p, cx, y + h * 0.18f, cx, y + h * 0.82f);
                        g.DrawLine(p, x + w * 0.18f, cy, x + w * 0.82f, cy);
                        break;

                    case StudioIcon.Minus:
                        g.DrawLine(p, x + w * 0.18f, cy, x + w * 0.82f, cy);
                        break;

                    case StudioIcon.Check:
                        g.DrawEllipse(p, x + w * 0.10f, y + h * 0.10f, w * 0.80f, h * 0.80f);
                        g.DrawLines(p, new PointF[] {
                            new PointF(x + w * 0.30f, cy), new PointF(x + w * 0.44f, y + h * 0.68f),
                            new PointF(x + w * 0.72f, y + h * 0.34f) });
                        break;

                    case StudioIcon.Plug:
                        g.DrawLine(p, x + w * 0.34f, y + h * 0.06f, x + w * 0.34f, y + h * 0.30f);
                        g.DrawLine(p, x + w * 0.66f, y + h * 0.06f, x + w * 0.66f, y + h * 0.30f);
                        g.DrawRectangle(p, x + w * 0.20f, y + h * 0.30f, w * 0.60f, h * 0.32f);
                        g.DrawLine(p, cx, y + h * 0.62f, cx, y + h * 0.94f);
                        break;

                    case StudioIcon.Download:
                        g.DrawLine(p, cx, y + h * 0.10f, cx, y + h * 0.60f);
                        g.DrawLines(p, new PointF[] {
                            new PointF(x + w * 0.30f, y + h * 0.42f), new PointF(cx, y + h * 0.64f),
                            new PointF(x + w * 0.70f, y + h * 0.42f) });
                        g.DrawLine(p, x + w * 0.18f, y + h * 0.86f, x + w * 0.82f, y + h * 0.86f);
                        break;

                    case StudioIcon.Chip:
                        g.DrawRectangle(p, x + w * 0.26f, y + h * 0.26f, w * 0.48f, h * 0.48f);
                        for (int i = 0; i < 3; i++)
                        {
                            float t = y + h * (0.34f + i * 0.16f);
                            g.DrawLine(p, x + w * 0.10f, t, x + w * 0.26f, t);
                            g.DrawLine(p, x + w * 0.74f, t, x + w * 0.90f, t);
                            float sx = x + w * (0.34f + i * 0.16f);
                            g.DrawLine(p, sx, y + h * 0.10f, sx, y + h * 0.26f);
                            g.DrawLine(p, sx, y + h * 0.74f, sx, y + h * 0.90f);
                        }
                        break;

                    case StudioIcon.Gear:
                        g.DrawEllipse(p, x + w * 0.30f, y + h * 0.30f, w * 0.40f, h * 0.40f);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = Math.PI * i / 4.0;
                            g.DrawLine(p,
                                (float)(cx + Math.Cos(a) * w * 0.30f), (float)(cy + Math.Sin(a) * h * 0.30f),
                                (float)(cx + Math.Cos(a) * w * 0.46f), (float)(cy + Math.Sin(a) * h * 0.46f));
                        }
                        break;

                    case StudioIcon.Ladder:
                        g.DrawLine(p, x + w * 0.14f, y + h * 0.10f, x + w * 0.14f, y + h * 0.90f);
                        g.DrawLine(p, x + w * 0.86f, y + h * 0.10f, x + w * 0.86f, y + h * 0.90f);
                        g.DrawLine(p, x + w * 0.14f, y + h * 0.34f, x + w * 0.38f, y + h * 0.34f);
                        g.DrawLine(p, x + w * 0.38f, y + h * 0.22f, x + w * 0.38f, y + h * 0.46f);
                        g.DrawLine(p, x + w * 0.54f, y + h * 0.22f, x + w * 0.54f, y + h * 0.46f);
                        g.DrawLine(p, x + w * 0.54f, y + h * 0.34f, x + w * 0.86f, y + h * 0.34f);
                        g.DrawLine(p, x + w * 0.14f, y + h * 0.70f, x + w * 0.86f, y + h * 0.70f);
                        break;

                    case StudioIcon.Convert:
                        g.DrawLine(p, x + w * 0.14f, y + h * 0.34f, x + w * 0.74f, y + h * 0.34f);
                        g.DrawLines(p, new PointF[] {
                            new PointF(x + w * 0.58f, y + h * 0.18f), new PointF(x + w * 0.80f, y + h * 0.34f),
                            new PointF(x + w * 0.58f, y + h * 0.50f) });
                        g.DrawLine(p, x + w * 0.86f, y + h * 0.70f, x + w * 0.26f, y + h * 0.70f);
                        g.DrawLines(p, new PointF[] {
                            new PointF(x + w * 0.42f, y + h * 0.54f), new PointF(x + w * 0.20f, y + h * 0.70f),
                            new PointF(x + w * 0.42f, y + h * 0.86f) });
                        break;

                    case StudioIcon.Terminal:
                        g.DrawRectangle(p, x + w * 0.10f, y + h * 0.16f, w * 0.80f, h * 0.68f);
                        g.DrawLines(p, new PointF[] {
                            new PointF(x + w * 0.26f, y + h * 0.38f), new PointF(x + w * 0.42f, cy),
                            new PointF(x + w * 0.26f, y + h * 0.62f) });
                        g.DrawLine(p, x + w * 0.52f, y + h * 0.64f, x + w * 0.74f, y + h * 0.64f);
                        break;

                    case StudioIcon.Monitor:
                        g.DrawRectangle(p, x + w * 0.10f, y + h * 0.16f, w * 0.80f, h * 0.54f);
                        g.DrawLine(p, x + w * 0.36f, y + h * 0.86f, x + w * 0.64f, y + h * 0.86f);
                        g.DrawLine(p, cx, y + h * 0.70f, cx, y + h * 0.86f);
                        g.DrawLines(p, new PointF[] {
                            new PointF(x + w * 0.22f, y + h * 0.52f), new PointF(x + w * 0.38f, y + h * 0.34f),
                            new PointF(x + w * 0.52f, y + h * 0.56f), new PointF(x + w * 0.78f, y + h * 0.30f) });
                        break;

                    case StudioIcon.Bolt:
                        g.FillPolygon(b, new PointF[] {
                            new PointF(x + w * 0.56f, y + h * 0.06f), new PointF(x + w * 0.22f, y + h * 0.56f),
                            new PointF(x + w * 0.46f, y + h * 0.56f), new PointF(x + w * 0.40f, y + h * 0.94f),
                            new PointF(x + w * 0.78f, y + h * 0.42f), new PointF(x + w * 0.54f, y + h * 0.42f) });
                        break;

                    case StudioIcon.Close:
                        g.DrawLine(p, x + w * 0.26f, y + h * 0.26f, x + w * 0.74f, y + h * 0.74f);
                        g.DrawLine(p, x + w * 0.74f, y + h * 0.26f, x + w * 0.26f, y + h * 0.74f);
                        break;
                }
            }
            g.SmoothingMode = old;
        }
    }

    /// <summary>Painel pintado, usado nas barras e blocos do estudio.</summary>
    internal sealed class StudioPanel : StudioControl
    {
        public Color Fill = StudioTheme.Chrome;
        public Color BottomLine = Color.Empty;

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Fill)) e.Graphics.FillRectangle(b, ClientRectangle);
            if (BottomLine != Color.Empty)
                using (Pen pen = new Pen(BottomLine))
                    e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            base.OnPaint(e);
        }
    }

    internal abstract class StudioControl : Control
    {
        protected StudioControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
        }
    }

    /// <summary>Cabecalho de grupo da navegacao lateral.</summary>
    internal sealed class NavSection : StudioControl
    {
        public NavSection(string text)
        {
            Text = text;
            Height = 30;
            Dock = DockStyle.Top;
            BackColor = StudioTheme.NavBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(StudioTheme.NavBg)) e.Graphics.FillRectangle(b, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, Text.ToUpper(CultureInfo.InvariantCulture), StudioTheme.Section,
                new Point(18, 12), StudioTheme.Faint);
        }
    }

    /// <summary>Item da navegacao lateral: icone, rotulo e marca de selecao.</summary>
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
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = Active ? StudioTheme.NavActive : hover ? StudioTheme.NavHover : StudioTheme.NavBg;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            if (Active)
                using (SolidBrush b = new SolidBrush(StudioTheme.Accent))
                    g.FillRectangle(b, new Rectangle(0, 0, 3, Height));

            Color fore = Active ? StudioTheme.Fore : hover ? StudioTheme.Fore : StudioTheme.Muted;
            StudioGlyph.Draw(g, Icon, new Rectangle(18, (Height - 16) / 2, 16, 16),
                Active ? StudioTheme.Accent : fore);
            TextRenderer.DrawText(g, Text, Active ? StudioTheme.UiBold : StudioTheme.Ui,
                new Rectangle(44, 0, Width - 52, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = pressed ? StudioTheme.Shell : hover ? StudioTheme.ChromeLight : StudioTheme.Chrome;
            using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Color fore = Emphasis ? StudioTheme.Accent : StudioTheme.Fore;
            StudioGlyph.Draw(g, Icon, new Rectangle((Width - 20) / 2, 8, 20, 20), fore);
            TextRenderer.DrawText(g, Text, StudioTheme.Small, new Rectangle(0, 32, Width, 16),
                Emphasis ? StudioTheme.Accent : StudioTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class StudioTab
    {
        public string Key;
        public string Title;
        public StudioIcon Icon;
        public bool Closable;
        public string Status;
        public Form Document;
        internal Rectangle Bounds;
        internal Rectangle CloseBounds;
    }

    /// <summary>Abas de documento do estudio.</summary>
    internal sealed class DocTabStrip : StudioControl
    {
        public delegate void TabEvent(StudioTab tab);

        private readonly List<StudioTab> tabs = new List<StudioTab>();
        private int selected = -1;
        private int hot = -1;
        private bool hotClose;

        public event EventHandler SelectedChanged;
        public event TabEvent TabClosed;

        public List<StudioTab> Tabs { get { return tabs; } }
        public StudioTab Selected { get { return selected >= 0 && selected < tabs.Count ? tabs[selected] : null; } }

        public DocTabStrip()
        {
            Height = 32;
            Dock = DockStyle.Top;
            BackColor = StudioTheme.Shell;
        }

        public StudioTab Find(string key)
        {
            int i;
            for (i = 0; i < tabs.Count; i++) if (tabs[i].Key == key) return tabs[i];
            return null;
        }

        public void Open(StudioTab tab)
        {
            int i;
            for (i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].Key != tab.Key) continue;
                Select(i);
                return;
            }
            tabs.Add(tab);
            Select(tabs.Count - 1);
        }

        public void SelectKey(string key)
        {
            int i;
            for (i = 0; i < tabs.Count; i++) if (tabs[i].Key == key) { Select(i); return; }
        }

        private void Select(int index)
        {
            selected = index;
            Invalidate();
            if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
        }

        private void LayoutTabs()
        {
            int x = 0;
            int i;
            for (i = 0; i < tabs.Count; i++)
            {
                StudioTab t = tabs[i];
                int w = TextRenderer.MeasureText(t.Title, StudioTheme.UiBold).Width + 48;
                if (t.Closable) w += 20;
                t.Bounds = new Rectangle(x, 0, w, Height);
                t.CloseBounds = t.Closable
                    ? new Rectangle(x + w - 24, (Height - 16) / 2, 16, 16)
                    : Rectangle.Empty;
                x += w;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            LayoutTabs();
            int found = -1;
            bool close = false;
            int i;
            for (i = 0; i < tabs.Count; i++)
            {
                if (!tabs[i].Bounds.Contains(e.Location)) continue;
                found = i;
                close = tabs[i].Closable && tabs[i].CloseBounds.Contains(e.Location);
                break;
            }
            if (found != hot || close != hotClose) { hot = found; hotClose = close; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hot = -1; hotClose = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            LayoutTabs();
            int i;
            for (i = 0; i < tabs.Count; i++)
            {
                if (!tabs[i].Bounds.Contains(e.Location)) continue;

                if (tabs[i].Closable && tabs[i].CloseBounds.Contains(e.Location))
                {
                    StudioTab removed = tabs[i];
                    tabs.RemoveAt(i);
                    // Fechar uma aba anterior a selecionada desloca a selecao em um.
                    if (i < selected) selected--;
                    if (selected >= tabs.Count) selected = tabs.Count - 1;
                    hot = -1;
                    Invalidate();
                    if (TabClosed != null) TabClosed(removed);
                    if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
                    return;
                }

                if (selected != i) Select(i);
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            LayoutTabs();
            using (SolidBrush b = new SolidBrush(StudioTheme.Shell)) g.FillRectangle(b, ClientRectangle);

            int i;
            for (i = 0; i < tabs.Count; i++)
            {
                StudioTab t = tabs[i];
                bool active = i == selected;
                Rectangle r = t.Bounds;

                if (active)
                {
                    using (SolidBrush b = new SolidBrush(StudioTheme.ChromeLight)) g.FillRectangle(b, r);
                    using (SolidBrush b = new SolidBrush(StudioTheme.Accent))
                        g.FillRectangle(b, new Rectangle(r.X, 0, r.Width, 2));
                }
                else if (i == hot)
                {
                    using (SolidBrush b = new SolidBrush(StudioTheme.Chrome)) g.FillRectangle(b, r);
                }

                Color fore = active ? StudioTheme.Fore : StudioTheme.Muted;
                StudioGlyph.Draw(g, t.Icon, new Rectangle(r.X + 12, (Height - 14) / 2, 14, 14),
                    active ? StudioTheme.Accent : StudioTheme.Faint);
                TextRenderer.DrawText(g, t.Title, active ? StudioTheme.UiBold : StudioTheme.Ui,
                    new Rectangle(r.X + 32, 0, r.Width - (t.Closable ? 54 : 38), Height), fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                if (t.Closable)
                    StudioGlyph.Draw(g, StudioIcon.Close, Rectangle.Inflate(t.CloseBounds, -4, -4),
                        i == hot && hotClose ? StudioTheme.Fore : StudioTheme.Faint);
            }
        }
    }

    internal sealed class StudioLogEntry
    {
        public DateTime Time;
        public int Level;
        public string Text;
    }

    /// <summary>Console de saida do estudio, com carimbo de hora e severidade.</summary>
    internal sealed class StudioConsole : ListBox
    {
        public StudioConsole()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Color.FromArgb(22, 24, 27);
            ForeColor = StudioTheme.Fore;
            ItemHeight = 18;
            IntegralHeight = false;
            Font = StudioTheme.Mono;
        }

        public void Write(int level, string text)
        {
            StudioLogEntry entry = new StudioLogEntry();
            entry.Time = DateTime.Now;
            entry.Level = level;
            entry.Text = text;
            Items.Add(entry);
            while (Items.Count > 400) Items.RemoveAt(0);
            TopIndex = Math.Max(0, Items.Count - 1);
        }

        private static Color LevelColor(int level)
        {
            if (level == 1) return StudioTheme.Accent;
            if (level == 2) return StudioTheme.Warning;
            if (level == 3) return StudioTheme.Danger;
            return StudioTheme.Info;
        }

        private static string LevelTag(int level)
        {
            if (level == 1) return "  ok  ";
            if (level == 2) return " aviso";
            if (level == 3) return " erro ";
            return " info ";
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            StudioLogEntry entry = Items[e.Index] as StudioLogEntry;
            if (entry == null) return;

            Graphics g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush b = new SolidBrush(sel ? StudioTheme.ChromeLight : BackColor))
                g.FillRectangle(b, e.Bounds);

            int y = e.Bounds.Y + 2;
            int x = e.Bounds.X + 10;
            TextRenderer.DrawText(g, entry.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                StudioTheme.Mono, new Point(x, y), StudioTheme.Faint);
            x += 66;

            Color lc = LevelColor(entry.Level);
            TextRenderer.DrawText(g, LevelTag(entry.Level), StudioTheme.MonoBold, new Point(x, y), lc);
            x += 58;

            TextRenderer.DrawText(g, entry.Text, StudioTheme.Mono, new Point(x, y), StudioTheme.Fore);
        }
    }

    internal sealed class OpenLadderColorTable : ProfessionalColorTable
    {
        private readonly Color chrome = Color.FromArgb(37, 39, 43);
        private readonly Color hover = Color.FromArgb(52, 55, 60);
        private readonly Color border = Color.FromArgb(61, 64, 69);

        public override Color MenuStripGradientBegin { get { return chrome; } }
        public override Color MenuStripGradientEnd { get { return chrome; } }
        public override Color SeparatorDark { get { return border; } }
        public override Color SeparatorLight { get { return chrome; } }
        public override Color ToolStripDropDownBackground { get { return chrome; } }
        public override Color MenuBorder { get { return border; } }
        public override Color MenuItemBorder { get { return hover; } }
        public override Color MenuItemSelected { get { return hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return hover; } }
        public override Color MenuItemPressedGradientBegin { get { return hover; } }
        public override Color MenuItemPressedGradientEnd { get { return hover; } }
        public override Color ImageMarginGradientBegin { get { return chrome; } }
        public override Color ImageMarginGradientMiddle { get { return chrome; } }
        public override Color ImageMarginGradientEnd { get { return chrome; } }
        public override Color ToolStripBorder { get { return border; } }
        public override Color ToolStripGradientBegin { get { return chrome; } }
        public override Color ToolStripGradientMiddle { get { return chrome; } }
        public override Color ToolStripGradientEnd { get { return chrome; } }
        public override Color ButtonSelectedGradientBegin { get { return hover; } }
        public override Color ButtonSelectedGradientMiddle { get { return hover; } }
        public override Color ButtonSelectedGradientEnd { get { return hover; } }
        public override Color ButtonPressedGradientBegin { get { return Color.FromArgb(43, 126, 84); } }
        public override Color ButtonPressedGradientMiddle { get { return Color.FromArgb(43, 126, 84); } }
        public override Color ButtonPressedGradientEnd { get { return Color.FromArgb(43, 126, 84); } }
    }

    internal sealed class DirectStudioForm : Form
    {
        private readonly Color Shell = StudioTheme.Shell;
        private readonly Color Chrome = StudioTheme.Chrome;
        private readonly Color ChromeLight = StudioTheme.ChromeLight;
        private readonly Color Border = StudioTheme.Border;
        private readonly Color Accent = StudioTheme.Accent;
        private readonly Color AccentDark = StudioTheme.AccentDark;
        private readonly Color Workspace = StudioTheme.Workspace;
        private readonly Color Fore = StudioTheme.Fore;
        private readonly Color Muted = StudioTheme.Muted;

        private Panel host;
        private Panel inspector;
        private DocTabStrip tabStrip;
        private StudioConsole console;
        private StudioPanel consolePanel;
        private readonly List<NavButton> navButtons = new List<NavButton>();
        private Panel navPanel;
        private bool inspectorAllowed = true;
        private ToolStripMenuItem miNav;
        private ToolStripMenuItem miProps;
        private ToolStripMenuItem miConsole;
        private Label statusText;
        private Label modeText;
        private Label projectValue;
        private Label rungsValue;
        private Label connectionValue;

        private LadderEditorForm ladderForm;
        private TP02BridgeForm bridgeForm;
        private TP02ProgramReaderForm readerForm;
        private TP02AutoDecoderForm decoderForm;
        private TP02CalibrationCampaignForm calibrationForm;
        private TP02IlToLadderForm ilForm;
        private PC12UpdaterForm updaterForm;

        public DirectStudioForm()
        {
            Text = "OpenLadder Studio";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            Size = new Size(1500, 900);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            BuildUi();
            ShowLadder();
        }

        // O layout ancorado do Windows Forms percorre os filhos do fim para o inicio da
        // colecao: o ultimo controle adicionado escolhe seu espaco primeiro e fica na
        // borda externa. Por isso cada container recebe o controle Fill primeiro e os
        // controles de borda depois, do mais interno para o mais externo.
        //
        // BringToFront() move o controle para o indice 0, ou seja, para o FIM da fila de
        // ancoragem. Aplicado a barras ancoradas, ele fazia o painel Fill ocupar toda a
        // area antes e as barras se sobrepunham ao conteudo: era o que deixava o menu
        // flutuando sobre a area de trabalho e a trilha lateral cobrindo a paleta do
        // editor ladder.
        private void BuildUi()
        {
            Panel workspace = new Panel();
            workspace.Dock = DockStyle.Fill;
            workspace.BackColor = Shell;
            Controls.Add(workspace);

            Control status = BuildStatusBar();
            Controls.Add(status);

            Control toolbar = BuildToolbar();
            Controls.Add(toolbar);

            MenuStrip menu = BuildMenu();
            Controls.Add(menu);
            MainMenuStrip = menu;

            Panel center = new Panel();
            center.Dock = DockStyle.Fill;
            center.BackColor = Workspace;
            workspace.Controls.Add(center);

            inspector = BuildInspector();
            workspace.Controls.Add(inspector);

            navPanel = BuildNav();
            workspace.Controls.Add(navPanel);

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Workspace;
            center.Controls.Add(host);

            consolePanel = BuildConsole();
            center.Controls.Add(consolePanel);

            tabStrip = new DocTabStrip();
            tabStrip.SelectedChanged += delegate { ApplySelectedTab(); };
            tabStrip.TabClosed += delegate(StudioTab t)
            {
                if (t.Document != null && !t.Document.IsDisposed) t.Document.Visible = false;
                console.Write(0, "Documento fechado: " + t.Title);
            };
            center.Controls.Add(tabStrip);
        }

        private MenuStrip BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.Dock = DockStyle.Top;
            menu.Height = 27;
            menu.BackColor = Chrome;
            menu.ForeColor = Fore;
            menu.Padding = new Padding(8, 2, 0, 2);
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new ToolStripProfessionalRenderer(new OpenLadderColorTable());

            ToolStripMenuItem arquivo = MenuItem("Arquivo");
            arquivo.DropDownItems.Add(DropItem("Novo projeto", delegate { InvokeLadder("NewProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(DropItem("Abrir...", delegate { InvokeLadder("OpenProject", null); }));
            arquivo.DropDownItems.Add(DropItem("Salvar", delegate { InvokeLadder("SaveProject", new object[] { false }); }));
            arquivo.DropDownItems.Add(DropItem("Salvar como...", delegate { InvokeLadder("SaveProject", new object[] { true }); }));
            arquivo.DropDownItems.Add(new ToolStripSeparator());
            arquivo.DropDownItems.Add(DropItem("Sair", delegate { Close(); }));

            ToolStripMenuItem editar = MenuItem("Editar");
            editar.DropDownItems.Add(DropItem("Desfazer", delegate { InvokeLadder("Undo", null); }));
            editar.DropDownItems.Add(new ToolStripSeparator());
            editar.DropDownItems.Add(DropItem("Adicionar rung", delegate { InvokeLadder("AddRung", null); }));
            editar.DropDownItems.Add(DropItem("Excluir rung", delegate { InvokeLadder("DeleteSelectedRung", null); }));
            editar.DropDownItems.Add(DropItem("Validar programa", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));

            miNav = DropItem("Painel de navegação", delegate { TogglePanel(0); });
            miProps = DropItem("Painel de propriedades", delegate { TogglePanel(1); });
            miConsole = DropItem("Painel de saída", delegate { TogglePanel(2); });
            miNav.Checked = true;
            miProps.Checked = true;
            miConsole.Checked = true;

            ToolStripMenuItem exibir = MenuItem("Exibir");
            exibir.DropDownItems.Add(miNav);
            exibir.DropDownItems.Add(miProps);
            exibir.DropDownItems.Add(miConsole);

            ToolStripMenuItem plc = MenuItem("PLC");
            plc.DropDownItems.Add(DropItem("Comunicação", delegate { ShowBridge(); }));
            plc.DropDownItems.Add(DropItem("Ler programa", delegate { ShowReader(); }));
            plc.DropDownItems.Add(DropItem("Decodificar programa", delegate { ShowDecoder(); }));

            ToolStripMenuItem ferramentas = MenuItem("Ferramentas");
            ferramentas.DropDownItems.Add(DropItem("Calibração", delegate { ShowCalibration(); }));
            ferramentas.DropDownItems.Add(DropItem("IL para Ladder", delegate { ShowIl(); }));
            ferramentas.DropDownItems.Add(new ToolStripSeparator());
            ferramentas.DropDownItems.Add(DropItem("Atualizações", delegate { ShowUpdater(); }));

            ToolStripMenuItem ajuda = MenuItem("Ajuda");
            ajuda.DropDownItems.Add(DropItem("Sobre o OpenLadder Studio", delegate
            {
                MessageBox.Show(this, "OpenLadder Studio v0.11\r\nProgramação Ladder e ferramentas para WEG TP02.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));

            menu.Items.Add(arquivo);
            menu.Items.Add(editar);
            menu.Items.Add(exibir);
            menu.Items.Add(plc);
            menu.Items.Add(ferramentas);
            menu.Items.Add(ajuda);
            return menu;
        }

        private int toolCursor;

        private void AddToolButton(Control bar, string text, StudioIcon icon, bool emphasis, EventHandler action)
        {
            IconToolButton b = new IconToolButton();
            b.Text = text;
            b.Icon = icon;
            b.Emphasis = emphasis;
            b.Height = 54;
            b.Width = b.MeasureWidth();
            b.Location = new Point(toolCursor, 3);
            if (action != null) b.Click += action;
            bar.Controls.Add(b);
            toolCursor += b.Width;
        }

        private void AddToolSeparator(Control bar)
        {
            Panel sep = new Panel();
            sep.BackColor = Border;
            sep.Bounds = new Rectangle(toolCursor + 7, 15, 1, 30);
            bar.Controls.Add(sep);
            toolCursor += 15;
        }

        private Control BuildToolbar()
        {
            StudioPanel bar = new StudioPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 60;
            bar.Fill = Chrome;
            bar.BottomLine = Border;

            Label brand = new Label();
            brand.Text = "OpenLadder Studio   v0.11";
            brand.Dock = DockStyle.Right;
            brand.Width = 210;
            brand.TextAlign = ContentAlignment.MiddleRight;
            brand.ForeColor = Muted;
            brand.Font = StudioTheme.Ui;
            brand.Padding = new Padding(0, 0, 16, 0);
            bar.Controls.Add(brand);

            toolCursor = 10;
            AddToolButton(bar, "Novo", StudioIcon.Doc, false, delegate { InvokeLadder("NewProject", new object[] { true }); });
            AddToolButton(bar, "Abrir", StudioIcon.Folder, false, delegate { InvokeLadder("OpenProject", null); });
            AddToolButton(bar, "Salvar", StudioIcon.Save, false, delegate { InvokeLadder("SaveProject", new object[] { false }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Desfazer", StudioIcon.Undo, false, delegate { InvokeLadder("Undo", null); });
            AddToolButton(bar, "Rung", StudioIcon.Plus, false, delegate { InvokeLadder("AddRung", null); });
            AddToolButton(bar, "Validar", StudioIcon.Check, false, delegate { InvokeLadder("ValidateProject", new object[] { true }); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Comunicação", StudioIcon.Plug, true, delegate { ShowBridge(); });
            AddToolButton(bar, "Ler PLC", StudioIcon.Download, false, delegate { ShowReader(); });
            AddToolSeparator(bar);
            AddToolButton(bar, "Atualizar", StudioIcon.Refresh, false, delegate { ShowUpdater(); });
            return bar;
        }

        private NavButton NavItem(string text, StudioIcon icon, string key, EventHandler action)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Icon = icon;
            b.Key = key;
            if (action != null) b.Click += action;
            navButtons.Add(b);
            return b;
        }

        private Control BuildBrand()
        {
            StudioPanel brand = new StudioPanel();
            brand.Dock = DockStyle.Top;
            brand.Height = 64;
            brand.Fill = StudioTheme.NavBg;
            brand.BottomLine = Border;
            brand.Paint += delegate(object sender, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                using (SolidBrush b = new SolidBrush(Accent))
                    g.FillRectangle(b, new Rectangle(18, 18, 28, 28));
                StudioGlyph.Draw(g, StudioIcon.Ladder, new Rectangle(22, 22, 20, 20), Color.White);
                TextRenderer.DrawText(g, "OpenLadder", new Font("Segoe UI Semibold", 11.0f, FontStyle.Bold),
                    new Point(56, 16), Fore);
                TextRenderer.DrawText(g, "Studio  •  WEG TP02", StudioTheme.Small, new Point(58, 37), Muted);
            };
            return brand;
        }

        private Panel BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 228;
            nav.BackColor = StudioTheme.NavBg;

            List<Control> items = new List<Control>();
            items.Add(BuildBrand());
            items.Add(new NavSection("Editor ladder"));
            items.Add(NavItem("Editor Ladder", StudioIcon.Ladder, "ladder", delegate { ShowLadder(); }));
            items.Add(NavItem("Validar projeto", StudioIcon.Check, "", delegate { InvokeLadder("ValidateProject", new object[] { true }); }));
            items.Add(new NavSection("TP02 bridge"));
            items.Add(NavItem("Comunicação TP02", StudioIcon.Plug, "bridge", delegate { ShowBridge(); }));
            items.Add(NavItem("Ler programa (RBP)", StudioIcon.Download, "reader", delegate { ShowReader(); }));
            items.Add(new NavSection("Análise PC12"));
            items.Add(NavItem("Decodificador", StudioIcon.Chip, "decoder", delegate { ShowDecoder(); }));
            items.Add(NavItem("Calibração", StudioIcon.Gear, "calibration", delegate { ShowCalibration(); }));
            items.Add(NavItem("IL para Ladder", StudioIcon.Convert, "il", delegate { ShowIl(); }));
            items.Add(new NavSection("Sistema"));
            items.Add(NavItem("Atualizações", StudioIcon.Refresh, "updater", delegate { ShowUpdater(); }));

            // Filhos ancorados ao topo empilham do ultimo para o primeiro:
            // insere na ordem inversa para que a lista acima seja a ordem visual.
            int i;
            for (i = items.Count - 1; i >= 0; i--) nav.Controls.Add(items[i]);
            return nav;
        }

        private StudioPanel BuildConsole()
        {
            StudioPanel wrap = new StudioPanel();
            wrap.Dock = DockStyle.Bottom;
            wrap.Height = 156;
            wrap.Fill = Color.FromArgb(22, 24, 27);

            console = new StudioConsole();
            console.Dock = DockStyle.Fill;
            wrap.Controls.Add(console);

            StudioPanel head = new StudioPanel();
            head.Dock = DockStyle.Top;
            head.Height = 27;
            head.Fill = Chrome;
            head.BottomLine = Border;
            head.Paint += delegate(object sender, PaintEventArgs e)
            {
                StudioGlyph.Draw(e.Graphics, StudioIcon.Terminal, new Rectangle(12, 6, 14, 14), Muted);
                TextRenderer.DrawText(e.Graphics, "SAÍDA", StudioTheme.Section, new Point(34, 8), Muted);
            };
            wrap.Controls.Add(head);

            Button clear = new Button();
            clear.Text = "Limpar";
            clear.Dock = DockStyle.Right;
            clear.Width = 74;
            clear.FlatStyle = FlatStyle.Flat;
            clear.FlatAppearance.BorderSize = 0;
            clear.BackColor = Chrome;
            clear.ForeColor = Muted;
            clear.Font = StudioTheme.Small;
            clear.Cursor = Cursors.Hand;
            clear.TabStop = false;
            clear.Click += delegate { console.Items.Clear(); };
            head.Controls.Add(clear);

            return wrap;
        }

        private Panel BuildInspector()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Right;
            p.Width = 246;
            p.BackColor = Chrome;

            Label title = InspectorLabel("PROPRIEDADES", 9.0f, true, Muted);
            title.Location = new Point(16, 14);
            p.Controls.Add(title);

            int y = 52;
            Label ignored;
            y = AddInspectorField(p, y, "Projeto", "Sem nome", out projectValue);
            y = AddInspectorField(p, y, "Redes", "0 rung(s)", out rungsValue);

            AddDivider(p, y);
            y += 16;

            y = AddInspectorField(p, y, "Controlador", "WEG TP02-60MR", out ignored);

            Label station = InspectorLabel("Estação  01", 8.4f, false, Muted);
            station.Location = new Point(16, y);
            p.Controls.Add(station);
            y += 30;

            AddDivider(p, y);
            y += 16;

            Label status = InspectorLabel("CONEXÃO", 8.2f, true, Muted);
            status.Location = new Point(16, y);
            p.Controls.Add(status);
            y += 26;

            connectionValue = InspectorLabel("●  OFFLINE", 9.2f, true, Color.FromArgb(168, 174, 181));
            connectionValue.Location = new Point(16, y);
            p.Controls.Add(connectionValue);
            y += 36;

            Button open = new Button();
            open.Text = "Configurar comunicação";
            open.Location = new Point(16, y);
            open.Size = new Size(214, 34);
            open.FlatStyle = FlatStyle.Flat;
            open.FlatAppearance.BorderColor = Border;
            open.BackColor = ChromeLight;
            open.ForeColor = Fore;
            open.Cursor = Cursors.Hand;
            open.Click += delegate { ShowBridge(); };
            p.Controls.Add(open);

            return p;
        }

        // Legenda em cima, valor embaixo. O valor tem largura fixa com reticencias,
        // para que um nome de projeto longo nunca vaze do painel.
        private int AddInspectorField(Control parent, int top, string caption, string initial, out Label value)
        {
            Label c = InspectorLabel(caption, 8.2f, false, Muted);
            c.Location = new Point(16, top);
            parent.Controls.Add(c);

            value = InspectorLabel(initial, 9.2f, true, Fore);
            value.AutoSize = false;
            value.AutoEllipsis = true;
            value.Bounds = new Rectangle(16, top + 20, 214, 19);
            parent.Controls.Add(value);
            return top + 48;
        }

        private Control BuildStatusBar()
        {
            StudioPanel p = new StudioPanel();
            p.Dock = DockStyle.Bottom;
            p.Height = 26;
            p.Fill = Color.FromArgb(25, 27, 30);

            statusText = new Label();
            statusText.Dock = DockStyle.Fill;
            statusText.TextAlign = ContentAlignment.MiddleLeft;
            statusText.Text = "Pronto";
            statusText.ForeColor = Muted;
            statusText.Font = StudioTheme.Small;
            p.Controls.Add(statusText);

            modeText = new Label();
            modeText.Dock = DockStyle.Right;
            modeText.Width = 340;
            modeText.TextAlign = ContentAlignment.MiddleRight;
            modeText.Text = "TP02-60MR    |    OFFLINE    |    v0.11";
            modeText.ForeColor = Muted;
            modeText.Font = StudioTheme.Small;
            modeText.Padding = new Padding(0, 0, 12, 0);
            p.Controls.Add(modeText);

            StudioPanel dot = new StudioPanel();
            dot.Dock = DockStyle.Left;
            dot.Width = 26;
            dot.Fill = p.Fill;
            dot.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(Accent))
                    e.Graphics.FillEllipse(b, 11, 10, 7, 7);
            };
            p.Controls.Add(dot);
            return p;
        }

        private void ShowLadder()
        {
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                ladderForm = new LadderEditorForm();
                PrepareLadderForStudio(ladderForm);
            }
            ShowDocument(ladderForm, "Programa Ladder", "ladder", StudioIcon.Ladder, "Editor Ladder");
            UpdateProjectName();
        }

        private void ShowBridge()
        {
            if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
            ShowDocument(bridgeForm, "Comunicação TP02", "bridge", StudioIcon.Plug, "Comunicação TP02");
        }

        private void ShowReader()
        {
            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            ShowDocument(readerForm, "Leitura RBP", "reader", StudioIcon.Download, "Leitura do programa");
        }

        private void ShowDecoder()
        {
            if (decoderForm == null || decoderForm.IsDisposed) decoderForm = new TP02AutoDecoderForm();
            ShowDocument(decoderForm, "Decodificador", "decoder", StudioIcon.Chip, "Decodificação offline");
        }

        private void ShowCalibration()
        {
            if (calibrationForm == null || calibrationForm.IsDisposed) calibrationForm = new TP02CalibrationCampaignForm();
            ShowDocument(calibrationForm, "Calibração", "calibration", StudioIcon.Gear, "Calibração de opcodes");
        }

        private void ShowIl()
        {
            if (ilForm == null || ilForm.IsDisposed) ilForm = new TP02IlToLadderForm();
            ShowDocument(ilForm, "IL → Ladder", "il", StudioIcon.Convert, "Reconstrução Ladder");
        }

        private void ShowUpdater()
        {
            if (updaterForm == null || updaterForm.IsDisposed) updaterForm = new PC12UpdaterForm();
            ShowDocument(updaterForm, "Atualizações", "updater", StudioIcon.Refresh, "Atualizações do OpenLadder Studio");
        }

        /// <summary>
        /// Abre (ou reativa) um documento. Os formularios permanecem no host e apenas
        /// alternam a visibilidade, para que as abas mantenham o estado de cada um.
        /// </summary>
        private void ShowDocument(Form child, string title, string key, StudioIcon icon, string status)
        {
            if (child.Parent != host)
            {
                child.TopLevel = false;
                child.FormBorderStyle = FormBorderStyle.None;
                child.Dock = DockStyle.Fill;
                host.Controls.Add(child);
                console.Write(0, "Documento aberto: " + title);
            }

            StudioTab tab = tabStrip.Find(key);
            if (tab == null)
            {
                tab = new StudioTab();
                tab.Key = key;
                tab.Title = title;
                tab.Icon = icon;
                tab.Status = status;
                tab.Closable = key != "ladder";
                tab.Document = child;
                tabStrip.Open(tab);
            }
            else
            {
                tabStrip.SelectKey(key);
            }
            ApplySelectedTab();
        }

        private void ApplySelectedTab()
        {
            if (tabStrip == null || host == null) return;
            StudioTab tab = tabStrip.Selected;

            int i;
            for (i = 0; i < host.Controls.Count; i++)
                host.Controls[i].Visible = tab != null && host.Controls[i] == tab.Document;

            SelectNav(tab == null ? "" : tab.Key);
            if (inspector != null) inspector.Visible = inspectorAllowed && tab != null && tab.Key == "ladder";
            if (tab == null) return;

            if (!tab.Document.IsDisposed)
            {
                tab.Document.Show();
                tab.Document.BringToFront();
            }
            statusText.Text = tab.Status;
        }

        private void TogglePanel(int which)
        {
            if (which == 0)
            {
                navPanel.Visible = !navPanel.Visible;
                miNav.Checked = navPanel.Visible;
            }
            else if (which == 1)
            {
                // O inspetor pertence ao editor ladder; o menu guarda a preferência.
                inspectorAllowed = !inspectorAllowed;
                miProps.Checked = inspectorAllowed;
                ApplySelectedTab();
            }
            else
            {
                consolePanel.Visible = !consolePanel.Visible;
                miConsole.Checked = consolePanel.Visible;
            }
        }

        private void SelectNav(string key)
        {
            int i;
            for (i = 0; i < navButtons.Count; i++)
            {
                bool active = navButtons[i].Key.Length > 0 && navButtons[i].Key == key;
                if (navButtons[i].Active == active) continue;
                navButtons[i].Active = active;
                navButtons[i].Invalidate();
            }
        }

        private void PrepareLadderForStudio(LadderEditorForm form)
        {
            form.BackColor = Workspace;
            foreach (Control c in form.Controls)
            {
                Panel p = c as Panel;
                if (p == null) continue;
                // O cabecalho e a barra de comandos do editor sao redundantes dentro do
                // estudio. A faixa inferior fica visivel: ela mostra rung, coluna,
                // contagem de redes e a ferramenta ativa do proprio editor.
                if (p.Dock == DockStyle.Top && (p.Height == 64 || p.Height == 58)) p.Visible = false;
            }
            CompactLadderControls(form);
        }

        private void CompactLadderControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Label label = c as Label;
                if (label != null && label.Text != null && label.Text.Length > 90)
                {
                    label.Visible = false;
                }

                if (c.HasChildren) CompactLadderControls(c);
            }
        }

        private void InvokeLadder(string methodName, object[] args)
        {
            ShowLadder();
            try
            {
                if (console != null) console.Write(0, "Comando do editor: " + methodName);
                MethodInfo method = typeof(LadderEditorForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new MissingMethodException(methodName);
                method.Invoke(ladderForm, args);
                UpdateProjectName();
                statusText.Text = "Pronto";
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

        private void UpdateProjectName()
        {
            if (ladderForm == null || ladderForm.IsDisposed) return;
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("projectLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Label l = field == null ? null : field.GetValue(ladderForm) as Label;
                string value = l == null ? string.Empty : (l.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(value))
                {
                    projectValue.Text = "Sem nome";
                }
                else
                {
                    // O editor publica "<nome>   |   <n> rung(s)" em um rotulo unico.
                    string[] parts = value.Split('|');
                    projectValue.Text = parts[0].Trim();
                    if (parts.Length > 1) rungsValue.Text = parts[1].Trim();
                }
            }
            catch
            {
                projectValue.Text = "Projeto Ladder";
            }
        }

        private ToolStripMenuItem MenuItem(string text)
        {
            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            m.BackColor = Chrome;
            return m;
        }

        private ToolStripMenuItem DropItem(string text, EventHandler click)
        {
            ToolStripMenuItem m = new ToolStripMenuItem(text);
            m.ForeColor = Fore;
            m.BackColor = Chrome;
            m.Click += click;
            return m;
        }

        private Label InspectorLabel(string text, float size, bool bold, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = color;
            l.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            return l;
        }

        private void AddDivider(Control parent, int top)
        {
            Panel line = new Panel();
            line.Location = new Point(16, top);
            line.Size = new Size(210, 1);
            line.BackColor = Border;
            parent.Controls.Add(line);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Form[] forms = new Form[] { ladderForm, bridgeForm, readerForm, decoderForm, calibrationForm, ilForm, updaterForm };
            for (int i = 0; i < forms.Length; i++)
                if (forms[i] != null && !forms[i].IsDisposed) forms[i].Dispose();
            base.OnFormClosing(e);
        }
    }
}
