using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Pecas visuais da janela principal: cabecalho de painel, busca, itens da paleta de
    /// instrucoes, arvore do projeto, zoom e barra de status. Todas leem a paleta na hora
    /// de pintar (nada de cor guardada), entao acompanham a troca de tema sem reabrir, e
    /// todas medem em unidades de 96 dpi pelo mesmo fator da tela (<see cref="Graphics.DpiX"/>).
    /// </summary>
    internal static class StudioShape
    {
        public static int Scale(Graphics g, int value)
        {
            return (int)Math.Round(value * g.DpiX / 96.0f);
        }

        /// <summary>Preenche um retangulo de cantos arredondados (raio pequeno do sistema).</summary>
        public static void Fill(Graphics g, Rectangle r, Color color)
        {
            if (r.Width < 2 || r.Height < 2) return;
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = OpenLadderMetrics.RoundedRect(r, Scale(g, OpenLadderMetrics.RadiusSmall)))
            using (Brush brush = new SolidBrush(color))
                g.FillPath(brush, path);
            g.SmoothingMode = previous;
        }

        /// <summary>Contorno de cantos arredondados, desenhado para dentro do retangulo.</summary>
        public static void Outline(Graphics g, Rectangle r, Color color, float width)
        {
            if (r.Width < 3 || r.Height < 3) return;
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle inner = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath path = OpenLadderMetrics.RoundedRect(inner, Scale(g, OpenLadderMetrics.RadiusSmall)))
            using (Pen pen = new Pen(color, width))
                g.DrawPath(pen, path);
            g.SmoothingMode = previous;
        }

        /// <summary>Seta de expansao: aponta para a direita (recolhida) ou para baixo (expandida).</summary>
        public static void Chevron(Graphics g, Rectangle box, bool expanded, Color color)
        {
            float s = Math.Min(box.Width, box.Height) * 0.26f;
            float cx = box.Left + box.Width / 2f;
            float cy = box.Top + box.Height / 2f;
            PointF[] points = expanded
                ? new PointF[] { new PointF(cx - s, cy - s * 0.5f), new PointF(cx, cy + s * 0.5f), new PointF(cx + s, cy - s * 0.5f) }
                : new PointF[] { new PointF(cx - s * 0.5f, cy - s), new PointF(cx + s * 0.5f, cy), new PointF(cx - s * 0.5f, cy + s) };

            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, Math.Max(1.4f, g.DpiX / 96.0f * 1.5f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, points);
            }
            g.SmoothingMode = previous;
        }
    }

    /// <summary>Faixa de titulo de um painel (PROJETO, INSTRUCOES, PROPRIEDADES...).</summary>
    internal sealed class PanelHeader : StudioControl
    {
        private static readonly Font TitleFont = new Font("Segoe UI Semibold", 8.0f, FontStyle.Bold);

        public StudioIcon Icon = StudioIcon.None;
        public bool Raised;

        // Os filhos (por exemplo o botao Limpar) se pintam sobre o fundo deste cabecalho.
        public override Color BackColor
        {
            get { return Raised ? OpenLadderPalette.Chrome : OpenLadderPalette.NavBg; }
            set { }
        }

        public PanelHeader(string text)
        {
            Text = text;
            Height = 32;
            Dock = DockStyle.Top;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (Brush b = new SolidBrush(Raised ? OpenLadderPalette.Chrome : OpenLadderPalette.NavBg)) g.FillRectangle(b, ClientRectangle);

            int x = StudioShape.Scale(g, OpenLadderMetrics.Space3);
            if (Icon != StudioIcon.None)
            {
                int size = StudioShape.Scale(g, 14);
                StudioGlyph.Draw(g, Icon, new Rectangle(x, (Height - size) / 2, size, size), OpenLadderPalette.Faint);
                x += size + StudioShape.Scale(g, OpenLadderMetrics.Space2);
            }

            TextRenderer.DrawText(g, Text, TitleFont, new Rectangle(x, 0, Math.Max(1, Width - x), Height), OpenLadderPalette.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            using (Pen line = new Pen(OpenLadderPalette.Border)) g.DrawLine(line, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>Botao de texto sem moldura: aparece so no hover e no foco.</summary>
    internal sealed class GhostButton : StudioControl
    {
        private bool hover;
        private bool pressed;

        public GhostButton(string text)
        {
            Text = text;
            Height = 24;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = text;
            BackColor = OpenLadderPalette.Chrome;
        }

        public int MeasureWidth()
        {
            return TextRenderer.MeasureText(Text, StudioTheme.Small).Width + 24;
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
            using (Brush b = new SolidBrush(Parent == null ? OpenLadderPalette.Chrome : Parent.BackColor)) g.FillRectangle(b, ClientRectangle);

            Rectangle tile = new Rectangle(0, 0, Width, Height);
            if (pressed) StudioShape.Fill(g, tile, OpenLadderPalette.NavActive);
            else if (hover) StudioShape.Fill(g, tile, OpenLadderPalette.NavHover);
            if (Focused) StudioShape.Outline(g, tile, OpenLadderPalette.Focus, OpenLadderMetrics.FocusWidth);

            TextRenderer.DrawText(g, Text, StudioTheme.Small, tile, hover || Focused ? OpenLadderPalette.Fore : OpenLadderPalette.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Campo de busca com lupa, texto de orientacao e botao de limpar.</summary>
    internal sealed class SearchBox : StudioControl
    {
        public readonly TextBox Input = new TextBox();
        private readonly Label hint = new Label();
        private bool hotClear;

        /// <summary>Texto de orientacao: aparece enquanto o campo esta vazio.</summary>
        public string Placeholder
        {
            get { return hint.Text; }
            set { hint.Text = value; }
        }

        public SearchBox()
        {
            Height = 32;
            TabStop = false;
            Input.BorderStyle = BorderStyle.None;
            Input.BackColor = OpenLadderPalette.ChromeLight;
            Input.ForeColor = OpenLadderPalette.Fore;
            Input.Font = StudioTheme.Ui;
            Input.GotFocus += delegate { Invalidate(); };
            Input.LostFocus += delegate { Invalidate(); };
            Input.TextChanged += delegate { Invalidate(); OnTextChanged(EventArgs.Empty); };
            Input.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape && Input.Text.Length > 0)
                {
                    Input.Clear();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            Controls.Add(Input);

            // Rotulo transparente por cima do campo: o TextBox e opaco e esconderia um texto pintado por baixo.
            hint.AutoSize = false;
            hint.BackColor = Color.Transparent;
            hint.ForeColor = OpenLadderPalette.Faint;
            hint.Font = StudioTheme.Ui;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = "Buscar";
            hint.UseMnemonic = false;
            hint.Click += delegate { Input.Focus(); };
            Controls.Add(hint);
            hint.BringToFront();
        }

        public override string Text
        {
            get { return Input.Text; }
            set { Input.Text = value; }
        }

        private Rectangle ClearBounds()
        {
            int size = Math.Max(16, Height - 12);
            return new Rectangle(Width - size - 6, (Height - size) / 2, size, size);
        }

        private void PlaceInput()
        {
            int left = (int)Math.Round(Height * 0.95f);
            int right = Input.Text.Length > 0 ? ClearBounds().Width + 10 : 10;
            Input.Width = Math.Max(20, Width - left - right);
            Input.Location = new Point(left, Math.Max(0, (Height - Input.Height) / 2));
            hint.Bounds = new Rectangle(Input.Left + 2, 0, Input.Width, Height);
            hint.Visible = Input.Text.Length == 0;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); PlaceInput(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); PlaceInput(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = Input.Text.Length > 0 && ClearBounds().Contains(e.Location);
            if (hot != hotClear) { hotClear = hot; Cursor = hot ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hotClear = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Input.Text.Length > 0 && ClearBounds().Contains(e.Location)) Input.Clear();
            Input.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PlaceInput();
            Color around = Parent == null ? OpenLadderPalette.NavBg : Parent.BackColor;
            using (Brush b = new SolidBrush(around)) g.FillRectangle(b, ClientRectangle);

            Rectangle box = new Rectangle(0, 0, Width, Height);
            StudioShape.Fill(g, box, OpenLadderPalette.ChromeLight);
            bool focused = Input.Focused;
            StudioShape.Outline(g, box, focused ? OpenLadderPalette.Focus : OpenLadderPalette.Border, focused ? OpenLadderMetrics.FocusWidth : 1.0f);

            // Lupa: circulo e cabo, proporcionais a altura do campo.
            float s = Height;
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(focused ? OpenLadderPalette.Fore : OpenLadderPalette.Faint, Math.Max(1.5f, s / 18f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float d = s * 0.30f;
                float x = s * 0.30f;
                float y = (s - d) / 2f - s * 0.03f;
                g.DrawEllipse(pen, x, y, d, d);
                g.DrawLine(pen, x + d * 0.88f, y + d * 0.88f, x + d * 1.38f, y + d * 1.38f);
            }

            if (Input.Text.Length > 0)
            {
                Rectangle c = ClearBounds();
                if (hotClear) StudioShape.Fill(g, c, OpenLadderPalette.NavHover);
                StudioGlyph.Draw(g, StudioIcon.Close, Rectangle.Inflate(c, -4, -4), hotClear ? OpenLadderPalette.Fore : OpenLadderPalette.Faint);
            }
            g.SmoothingMode = previous;
        }
    }

    /// <summary>Titulo de grupo da paleta de instrucoes; clicar recolhe ou expande o grupo.</summary>
    internal sealed class PaletteSection : StudioControl
    {
        private static readonly Font TitleFont = new Font("Segoe UI Semibold", 7.75f, FontStyle.Bold);
        private bool hover;
        public bool Collapsed;
        public event EventHandler Toggled;

        public PaletteSection(string text)
        {
            Text = text;
            Height = 28;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = text;
        }

        public void Toggle()
        {
            Collapsed = !Collapsed;
            Invalidate();
            if (Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnClick(EventArgs e) { Toggle(); base.OnClick(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                Toggle();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (Brush b = new SolidBrush(OpenLadderPalette.NavBg)) g.FillRectangle(b, ClientRectangle);

            int pad = StudioShape.Scale(g, OpenLadderMetrics.Space1);
            Rectangle row = new Rectangle(pad, 0, Math.Max(1, Width - pad * 2), Height);
            if (Focused) StudioShape.Outline(g, row, OpenLadderPalette.Focus, OpenLadderMetrics.FocusWidth);

            int box = StudioShape.Scale(g, 16);
            Color ink = hover || Focused ? OpenLadderPalette.Fore : OpenLadderPalette.Muted;
            StudioShape.Chevron(g, new Rectangle(row.Left + StudioShape.Scale(g, OpenLadderMetrics.Space2), (Height - box) / 2, box, box), !Collapsed, ink);
            TextRenderer.DrawText(g, Text, TitleFont, new Rectangle(row.Left + StudioShape.Scale(g, 30), 0, Math.Max(1, row.Width - 34), Height), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Instrucao ou acao da paleta: icone, rotulo, hover e foco visiveis.</summary>
    internal sealed class PaletteItem : StudioControl
    {
        private bool hover;
        private bool pressed;
        public StudioIcon Icon = StudioIcon.None;

        public PaletteItem(string text, StudioIcon icon)
        {
            Text = text;
            Icon = icon;
            Height = 30;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = text;
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
            using (Brush b = new SolidBrush(OpenLadderPalette.NavBg)) g.FillRectangle(b, ClientRectangle);

            int pad = StudioShape.Scale(g, OpenLadderMetrics.Space1);
            Rectangle tile = new Rectangle(pad, 1, Math.Max(1, Width - pad * 2), Math.Max(1, Height - 2));
            if (pressed) StudioShape.Fill(g, tile, OpenLadderPalette.NavActive);
            else if (hover) StudioShape.Fill(g, tile, OpenLadderPalette.NavHover);
            if (Focused) StudioShape.Outline(g, tile, OpenLadderPalette.Focus, OpenLadderMetrics.FocusWidth);

            Color semantic = StudioIconPalette.For(Icon);
            int size = StudioShape.Scale(g, 16);
            StudioGlyph.Draw(g, Icon, new Rectangle(tile.Left + StudioShape.Scale(g, OpenLadderMetrics.Space3), (Height - size) / 2, size, size), semantic);
            int textLeft = tile.Left + StudioShape.Scale(g, 38);
            TextRenderer.DrawText(g, Text, StudioTheme.Ui, new Rectangle(textLeft, 0, Math.Max(1, tile.Right - textLeft - 4), Height),
                hover || pressed || Focused ? OpenLadderPalette.Fore : OpenLadderPalette.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>
    /// Arvore do projeto no estilo de um IDE: sem linhas pontilhadas, seta de
    /// expansao propria, icone por tipo, faixa de selecao e hover pela paleta.
    /// </summary>
    internal sealed class ProjectTree : TreeView
    {
        private TreeNode hotNode;
        private bool Scaled;

        // Indent e ItemHeight sao em pixels e o AutoScale do formulario nao os alcanca:
        // escala-se uma vez, na criacao da alca, pelo DPI real do controle.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Scaled) return;
            Scaled = true;
            using (Graphics g = CreateGraphics())
            {
                Indent = StudioShape.Scale(g, 16);
                ItemHeight = StudioShape.Scale(g, 28);
            }
        }

        public ProjectTree()
        {
            DrawMode = TreeViewDrawMode.OwnerDrawAll;
            ShowLines = false;
            ShowPlusMinus = false;
            ShowRootLines = false;
            FullRowSelect = true;
            HideSelection = false;
            BorderStyle = BorderStyle.None;
            ItemHeight = 28;
            Indent = 16;
            Scaled = false;
            DoubleBuffered = true;
            ShowNodeToolTips = true;
            BackColor = OpenLadderPalette.NavBg;
            ForeColor = OpenLadderPalette.Fore;
            Font = StudioTheme.Ui;
            ControlTheme.TrackScroll(this);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            TreeNode node = GetNodeAt(e.Location);
            if (node != hotNode) { hotNode = node; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hotNode != null) { hotNode = null; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            TreeNode node = GetNodeAt(e.Location);
            if (node != null && node.Nodes.Count > 0 && e.X < node.Level * Indent + ChevronZone())
            {
                node.Toggle();
                return;
            }
            base.OnMouseDown(e);
        }

        private int ChevronZone()
        {
            using (Graphics g = CreateGraphics()) return StudioShape.Scale(g, 28);
        }

        protected override void OnDrawNode(DrawTreeNodeEventArgs e)
        {
            TreeNode node = e.Node;
            if (node == null || e.Bounds.Height <= 0) return;

            Graphics g = e.Graphics;
            Rectangle row = new Rectangle(0, node.Bounds.Top, Width, ItemHeight);
            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            bool hot = node == hotNode;

            using (Brush back = new SolidBrush(selected ? OpenLadderPalette.NavActive : hot ? OpenLadderPalette.NavHover : OpenLadderPalette.NavBg))
                g.FillRectangle(back, row);
            if (selected)
                using (Brush bar = new SolidBrush(OpenLadderPalette.Accent))
                    g.FillRectangle(bar, 0, row.Top + 4, StudioShape.Scale(g, 3), row.Height - 8);

            int x = node.Level * Indent + StudioShape.Scale(g, OpenLadderMetrics.Space2);
            int box = StudioShape.Scale(g, 16);
            if (node.Nodes.Count > 0)
                StudioShape.Chevron(g, new Rectangle(x, row.Top + (row.Height - box) / 2, box, box), node.IsExpanded,
                    selected || hot ? OpenLadderPalette.Fore : OpenLadderPalette.Faint);
            x += box + StudioShape.Scale(g, OpenLadderMetrics.Space1);

            string tag = node.Tag == null ? string.Empty : node.Tag.ToString();
            bool reserved = tag.StartsWith("INFO_", StringComparison.Ordinal);
            StudioIcon icon = IconFor(tag);
            Color iconColor = reserved ? OpenLadderPalette.Faint : StudioIconPalette.For(icon);
            StudioGlyph.Draw(g, icon, new Rectangle(x, row.Top + (row.Height - box) / 2, box, box), iconColor);
            x += box + StudioShape.Scale(g, OpenLadderMetrics.Space2);

            Font font = node.NodeFont ?? Font;
            Color ink = reserved && !selected ? OpenLadderPalette.Faint : OpenLadderPalette.Fore;
            TextRenderer.DrawText(g, node.Text, font, new Rectangle(x, row.Top, Math.Max(1, Width - x - 4), row.Height), ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (selected && Focused) StudioShape.Outline(g, Rectangle.Inflate(row, -2, -1), OpenLadderPalette.Focus, 1.0f);
        }

        private static StudioIcon IconFor(string tag)
        {
            switch (tag)
            {
                case "ROOT": return StudioIcon.Folder;
                case "DEVICE": return StudioIcon.Chip;
                case "GROUP": return StudioIcon.Folder;
                case "MAIN": return StudioIcon.Ladder;
                case "INFO_VARIABLES": return StudioIcon.Grid;
                default: return StudioIcon.Doc;
            }
        }
    }

    /// <summary>Zoom em um unico controle segmentado: menos, percentual (volta a 100%) e mais.</summary>
    internal sealed class ZoomControl : StudioControl
    {
        private int hot = -1;
        private bool pressed;
        public event EventHandler MinusClick;
        public event EventHandler ResetClick;
        public event EventHandler PlusClick;

        public ZoomControl()
        {
            Height = 32;
            Width = 116;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = "Zoom do diagrama";
            Text = "100%";
            BackColor = OpenLadderPalette.Chrome;
        }

        public override string Text
        {
            get { return base.Text; }
            set { base.Text = value; Invalidate(); }
        }

        private int Hit(Point p)
        {
            int side = Math.Max(24, Height);
            if (p.X < side) return 0;
            if (p.X >= Width - side) return 2;
            return 1;
        }

        private void Fire(int segment)
        {
            EventHandler handler = segment == 0 ? MinusClick : segment == 2 ? PlusClick : ResetClick;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnClick(EventArgs e)
        {
            Point p = PointToClient(Cursor.Position);
            Fire(Hit(p));
            base.OnClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Down:
                    Fire(0); e.Handled = true; break;
                case Keys.Right:
                case Keys.Up:
                    Fire(2); e.Handled = true; break;
                case Keys.Enter:
                case Keys.Space:
                    Fire(1); e.Handled = true; break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color around = Parent == null ? OpenLadderPalette.Chrome : Parent.BackColor;
            using (Brush b = new SolidBrush(around)) g.FillRectangle(b, ClientRectangle);

            Rectangle box = new Rectangle(0, 0, Width, Height);
            StudioShape.Fill(g, box, OpenLadderPalette.ChromeLight);
            int side = Math.Max(24, Height);
            Rectangle minus = new Rectangle(0, 0, side, Height);
            Rectangle plus = new Rectangle(Width - side, 0, side, Height);
            Rectangle middle = new Rectangle(side, 0, Math.Max(1, Width - side * 2), Height);
            Rectangle[] parts = new Rectangle[] { minus, middle, plus };
            if (hot >= 0)
                StudioShape.Fill(g, Rectangle.Inflate(parts[hot], -2, -2), pressed ? OpenLadderPalette.NavActive : OpenLadderPalette.NavHover);
            StudioShape.Outline(g, box, Focused ? OpenLadderPalette.Focus : OpenLadderPalette.Border, Focused ? OpenLadderMetrics.FocusWidth : 1.0f);

            int arm = StudioShape.Scale(g, 5);
            using (Pen pen = new Pen(hot == 0 ? OpenLadderPalette.Fore : OpenLadderPalette.Muted, Math.Max(1.5f, g.DpiX / 96.0f * 1.6f)))
            {
                int cy = Height / 2;
                g.DrawLine(pen, minus.Left + side / 2 - arm, cy, minus.Left + side / 2 + arm, cy);
                g.DrawLine(pen, plus.Left + side / 2 - arm, cy, plus.Left + side / 2 + arm, cy);
            }
            using (Pen pen = new Pen(hot == 2 ? OpenLadderPalette.Fore : OpenLadderPalette.Muted, Math.Max(1.5f, g.DpiX / 96.0f * 1.6f)))
                g.DrawLine(pen, plus.Left + side / 2, Height / 2 - arm, plus.Left + side / 2, Height / 2 + arm);

            TextRenderer.DrawText(g, Text, StudioTheme.UiBold, middle, hot == 1 ? OpenLadderPalette.Fore : OpenLadderPalette.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Informacoes da barra de status, separadas por filetes finos em vez de barras de texto.</summary>
    internal sealed class StatusSegments : StudioControl
    {
        private string[] segments = new string[0];

        public StatusSegments()
        {
            Dock = DockStyle.Right;
            TabStop = false;
        }

        public void SetSegments(string[] values)
        {
            segments = values ?? new string[0];
            int width = 0;
            using (Graphics g = CreateGraphics())
            {
                int pad = StudioShape.Scale(g, OpenLadderMetrics.Space3);
                for (int i = 0; i < segments.Length; i++)
                    width += TextRenderer.MeasureText(segments[i], StudioTheme.Small).Width + pad * 2 + (i > 0 ? 1 : 0);
            }
            if (width != Width) Width = Math.Max(1, width);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color around = Parent == null ? OpenLadderPalette.Shell : Parent.BackColor;
            using (Brush b = new SolidBrush(around)) g.FillRectangle(b, ClientRectangle);

            int pad = StudioShape.Scale(g, OpenLadderMetrics.Space3);
            int x = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                {
                    int h = StudioShape.Scale(g, 12);
                    using (Pen line = new Pen(OpenLadderPalette.BorderStrong)) g.DrawLine(line, x, (Height - h) / 2, x, (Height + h) / 2);
                    x += 1;
                }
                int w = TextRenderer.MeasureText(segments[i], StudioTheme.Small).Width + pad * 2;
                TextRenderer.DrawText(g, segments[i], StudioTheme.Small, new Rectangle(x, 0, w, Height), OpenLadderPalette.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                x += w;
            }
        }
    }

    /// <summary>Fundo da barra de status, com o filete de cima da paleta.</summary>
    internal sealed class StatusBarPanel : Panel
    {
        public StatusBarPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = OpenLadderPalette.Shell;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen line = new Pen(OpenLadderPalette.Border)) e.Graphics.DrawLine(line, 0, 0, Width, 0);
        }
    }
}
