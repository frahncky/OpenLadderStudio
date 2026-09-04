using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ModernPC12
{
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
        public static readonly Color Disabled = Color.FromArgb(92, 97, 103);

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
        Refresh, Chip, Gear, Ladder, Convert, Terminal, Close, Bolt, Monitor, Grid
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

                    case StudioIcon.Grid:
                        g.DrawRectangle(p, x + w * 0.12f, y + h * 0.12f, w * 0.76f, h * 0.76f);
                        g.DrawLine(p, x + w * 0.12f, y + h * 0.38f, x + w * 0.88f, y + h * 0.38f);
                        g.DrawLine(p, x + w * 0.12f, y + h * 0.64f, x + w * 0.88f, y + h * 0.64f);
                        g.DrawLine(p, x + w * 0.38f, y + h * 0.12f, x + w * 0.38f, y + h * 0.88f);
                        g.DrawLine(p, x + w * 0.64f, y + h * 0.12f, x + w * 0.64f, y + h * 0.88f);
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

            if (Active && on)
                using (SolidBrush b = new SolidBrush(StudioTheme.Accent))
                    g.FillRectangle(b, new Rectangle(0, 0, 3, Height));

            Color fore = !on ? StudioTheme.Disabled
                : Active || hover ? StudioTheme.Fore : StudioTheme.Muted;
            StudioGlyph.Draw(g, Icon, new Rectangle(18, (Height - 16) / 2, 16, 16),
                Active && on ? StudioTheme.Accent : fore);
            TextRenderer.DrawText(g, Text, Active && on ? StudioTheme.UiBold : StudioTheme.Ui,
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
}
