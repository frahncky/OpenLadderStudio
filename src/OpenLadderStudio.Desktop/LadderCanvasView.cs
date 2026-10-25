using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    /// <summary>Pedido de gravar o comentário de uma linha (índice 0-based).</summary>
    internal sealed class LadderCommentEventArgs : EventArgs
    {
        public readonly int Rung;
        public readonly string Text;

        public LadderCommentEventArgs(int rung, string text)
        {
            Rung = rung;
            Text = text;
        }
    }

    /// <summary>
    /// Editor do comentário da linha, no próprio diagrama. Enter quebra a linha;
    /// Ctrl+Enter confirma e Esc cancela (perder o foco também confirma, tratado pelo canvas).
    /// </summary>
    internal sealed class RungCommentBox : TextBox
    {
        public event EventHandler CommitRequested;
        public event EventHandler CancelRequested;

        public RungCommentBox()
        {
            Multiline = true;
            AcceptsReturn = true;
            WordWrap = true;
            BorderStyle = BorderStyle.None;
            ScrollBars = ScrollBars.None;
            MaxLength = LadderProjectCodec.MaxCommentLength;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter))
            {
                if (CommitRequested != null) CommitRequested(this, EventArgs.Empty);
                return true;
            }
            if (keyData == Keys.Escape)
            {
                if (CancelRequested != null) CancelRequested(this, EventArgs.Empty);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    /// <summary>
    /// Diagrama Ladder. A geometria vem de <see cref="LadderCanvasLayout"/>: cada linha é um
    /// bloco com faixa de comentário em cima e o diagrama embaixo; a calha de números e o
    /// cabeçalho de colunas ficam congelados cada um no seu eixo e são desenhados por último.
    /// O desenho mede em unidades lógicas e aplica DPI e zoom numa matriz só, então o texto
    /// cresce com a escala do Windows em vez de ficar do tamanho de 100%.
    /// </summary>
    internal sealed class LadderCanvas : ScrollableControl
    {
        public List<LadderRung> Rungs;
        public int SelectedRung = -1;
        public int SelectedColumn = -1;
        public int SelectedLane = 0;

        private int HoverRung = -1;
        private int HoverColumn = -1;
        private int hoverComment = -1;
        private bool hoverAdd;
        private float zoomFactor = 1.0f;
        private float dpiScale = 1.0f;
        private bool updatingZoomExtent;
        private bool[] rungPower;

        public event EventHandler SelectionChanged;
        public event EventHandler ElementAction;
        public event EventHandler ElementDoubleClick;
        public event EventHandler ToolReleased;
        public event MouseEventHandler ElementContextMenu;
        public event EventHandler<LadderMoveEventArgs> ElementMoved;
        public event EventHandler AddRungRequested;
        public event EventHandler<LadderCommentEventArgs> CommentEdited;

        /// <summary>Arrastar e abrir menu só valem em modo ponteiro. Com uma
        /// ferramenta armada o clique existe para inserir.</summary>
        public bool DragEnabled = true;

        private bool dragArmed;
        private bool dragActive;
        private Point dragOrigin;
        private int dragRung = -1;
        private int dragColumn = -1;
        private int dragLane;
        private int dropRung = -1;
        private int dropColumn = -1;

        // Edição do comentário no próprio lugar.
        private RungCommentBox editBox;
        private int editRung = -1;
        private Font editFont;

        public LadderCanvas()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            TabStop = true;
            AccessibleName = "Diagrama Ladder";
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            MouseDown += CanvasMouseDown;
            MouseUp += CanvasMouseUp;
            MouseMove += CanvasMouseMove;
            MouseLeave += delegate { ClearHover(); };
            MouseDoubleClick += CanvasMouseDoubleClick;
        }

        // ------------------------------------------------------------------
        // Escala: DPI do monitor x zoom do usuário x ajuste à largura.
        // ------------------------------------------------------------------

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                using (Graphics g = CreateGraphics()) dpiScale = Math.Max(0.5f, g.DpiX / 96.0f);
            }
            catch { dpiScale = 1.0f; }
            UpdateZoomExtent();
        }

        // Reserva a largura da barra de rolagem para que ela aparecer não mude a escala
        // de ajuste e fique ligando e desligando a barra.
        private int ViewportWidth
        {
            get
            {
                return Math.Max(1, ClientSize.Width
                    + (VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0)
                    - SystemInformation.VerticalScrollBarWidth);
            }
        }

        private int LogicalWidth
        {
            get { return Math.Max(LadderCanvasLayout.DesignWidth, (int)(ViewportWidth / dpiScale)); }
        }

        private float EffectiveZoom
        {
            get { return zoomFactor * dpiScale * Math.Max(LadderCanvasLayout.MinFit, Math.Min(1.0f, ViewportWidth / (dpiScale * LadderCanvasLayout.DesignWidth))); }
        }

        public int ZoomPercent
        {
            get { return (int)Math.Round(zoomFactor * 100.0f); }
        }

        public void SetZoomPercent(int percent)
        {
            int target = Math.Max(50, Math.Min(200, percent));
            float next = target / 100.0f;
            if (Math.Abs(next - zoomFactor) < 0.001f) return;

            Point oldScroll = AutoScrollPosition;
            float oldZoom = EffectiveZoom;
            float centerX = (-oldScroll.X + ClientSize.Width / 2.0f) / Math.Max(0.01f, oldZoom);
            float centerY = (-oldScroll.Y + ClientSize.Height / 2.0f) / Math.Max(0.01f, oldZoom);
            zoomFactor = next;
            UpdateZoomExtent();

            int scrollX = Math.Max(0, (int)Math.Round(centerX * EffectiveZoom - ClientSize.Width / 2.0f));
            int scrollY = Math.Max(0, (int)Math.Round(centerY * EffectiveZoom - ClientSize.Height / 2.0f));
            AutoScrollPosition = new Point(scrollX, scrollY);
            PlaceEditBox();
            Invalidate();
        }

        private void UpdateZoomExtent()
        {
            if (updatingZoomExtent) return;
            updatingZoomExtent = true;
            try
            {
                LadderCanvasLayout layout = CurrentLayout();
                float zoom = EffectiveZoom;
                AutoScrollMinSize = new Size(
                    Math.Max(1, (int)Math.Round(layout.Width * zoom)),
                    Math.Max(1, (int)Math.Ceiling(layout.ContentHeight * zoom)));
            }
            finally { updatingZoomExtent = false; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateZoomExtent();
            PlaceEditBox();
            Invalidate();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            PlaceEditBox();
        }

        // ------------------------------------------------------------------
        // Energização (V86/V88): o resultado da varredura chega por linha.
        // ------------------------------------------------------------------

        /// <summary>
        /// Resultado da última varredura por linha (true = energizada). Null limpa o
        /// destaque. Sem dados por elemento, só se afirma condução onde a coluna não
        /// tem ramo paralelo; nas demais o símbolo fica neutro.
        /// </summary>
        public void SetRungPower(bool[] power)
        {
            rungPower = power;
            Invalidate();
        }

        private bool RungOn(int rung)
        {
            return rungPower != null && rung >= 0 && rung < rungPower.Length && rungPower[rung];
        }

        private LadderPower PowerFor(int rung, int column, bool branch)
        {
            if (rungPower == null || rung < 0 || rung >= rungPower.Length) return LadderPower.Unknown;
            if (Rungs[rung].Parallel[column].Type != LadderElementType.Empty && column < LadderRung.ColumnCount - 1) return LadderPower.Unknown;
            return rungPower[rung] ? LadderPower.On : LadderPower.Off;
        }

        // ------------------------------------------------------------------
        // Geometria compartilhada entre desenho, ponteiro e edição.
        // ------------------------------------------------------------------

        private LadderCanvasLayout CurrentLayout()
        {
            int count = Rungs == null ? 0 : Rungs.Count;
            string[] comments = new string[count];
            bool[] branch = new bool[count];
            for (int r = 0; r < count; r++)
            {
                comments[r] = r == editRung && editBox != null ? editBox.Text : Rungs[r].Comment;
                for (int c = 0; c < LadderRung.ColumnCount - 1; c++)
                {
                    if (Rungs[r].Parallel[c].Type != LadderElementType.Empty) { branch[r] = true; break; }
                }
            }
            return LadderCanvasLayout.Compute(comments, branch, LogicalWidth);
        }

        private bool HasParallel(int rung, int column)
        {
            return column < LadderRung.ColumnCount - 1 && Rungs[rung].Parallel[column].Type != LadderElementType.Empty;
        }

        private PointF ToLogical(Point client)
        {
            Point scroll = AutoScrollPosition;
            float zoom = Math.Max(0.01f, EffectiveZoom);
            return new PointF((client.X - scroll.X) / zoom, (client.Y - scroll.Y) / zoom);
        }

        private LadderRegion Locate(Point client, out int rung, out int column, out int lane)
        {
            rung = -1;
            column = -1;
            lane = 0;
            if (Rungs == null) return LadderRegion.None;
            PointF p = ToLogical(client);
            // O cabeçalho congelado cobre o topo do que está rolado por baixo.
            if (client.Y < LadderCanvasLayout.HeaderHeight * EffectiveZoom) return LadderRegion.None;
            return CurrentLayout().Locate((int)Math.Floor(p.X), (int)Math.Floor(p.Y), HasParallel, out rung, out column, out lane);
        }

        private void ClearHover()
        {
            if (HoverRung != -1 || HoverColumn != -1 || hoverComment != -1 || hoverAdd)
            {
                HoverRung = -1;
                HoverColumn = -1;
                hoverComment = -1;
                hoverAdd = false;
                Invalidate();
            }
        }

        /// <summary>Rola até a linha ficar toda visível (usado ao adicionar e ao comentar).</summary>
        public void ScrollToRung(int rung)
        {
            if (Rungs == null || rung < 0 || rung >= Rungs.Count) return;
            LadderCanvasLayout layout = CurrentLayout();
            float zoom = EffectiveZoom;
            int top = (int)Math.Floor((layout.Top[rung] - LadderCanvasLayout.TopPad) * zoom);
            int bottom = (int)Math.Ceiling((layout.Top[rung] + layout.Height[rung]) * zoom);
            int view = ClientSize.Height;
            int header = (int)(LadderCanvasLayout.HeaderHeight * zoom);
            int current = -AutoScrollPosition.Y;
            int target = current;
            if (top - header < current) target = Math.Max(0, top - header);
            else if (bottom > current + view) target = Math.Max(0, bottom - view);
            if (target != current) AutoScrollPosition = new Point(-AutoScrollPosition.X, target);
            PlaceEditBox();
            Invalidate();
        }

        // ------------------------------------------------------------------
        // Pintura
        // ------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Rungs == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            LadderCanvasLayout layout = CurrentLayout();
            UpdateZoomExtent();
            float zoom = EffectiveZoom;
            Point scroll = AutoScrollPosition;
            using (Matrix view = new Matrix(zoom, 0.0f, 0.0f, zoom, scroll.X, scroll.Y))
                g.Transform = view;

            float visibleTop = -scroll.Y / zoom;
            float visibleBottom = (-scroll.Y + ClientSize.Height) / zoom;

            using (Brush page = new SolidBrush(OpenLadderPalette.Canvas))
                g.FillRectangle(page, 0, 0, layout.Width + 40, layout.ContentHeight);

            for (int r = 0; r < Rungs.Count; r++)
            {
                if (layout.Top[r] + layout.Height[r] < visibleTop || layout.Top[r] > visibleBottom) continue;
                DrawRung(g, layout, r);
            }

            DrawAddRow(g, layout);

            if (dragActive && dropRung >= 0 && dropColumn >= 0)
            {
                Rectangle alvo = layout.CellRect(dropRung, dropColumn);
                using (Pen marca = new Pen(OpenLadderPalette.Accent, 2.0f))
                {
                    marca.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(marca, alvo);
                }
            }

            DrawRowGutter(g, layout, scroll, zoom);
            DrawColumnHeader(g, layout, scroll, zoom);

            g.ResetTransform();
        }

        private void DrawRung(Graphics g, LadderCanvasLayout L, int r)
        {
            bool selected = r == SelectedRung;
            bool on = RungOn(r);
            int top = L.Top[r];
            int height = L.Height[r];
            int wire = L.WireAt(r);
            LadderRung rung = Rungs[r];

            if (selected)
            {
                using (Brush row = new SolidBrush(OpenLadderPalette.CanvasRow))
                    g.FillRectangle(row, 0, top, L.Width + 40, height);
            }

            DrawComment(g, L, r);

            // Trilhos do diagrama (não atravessam a faixa de comentário).
            using (Pen rail = new Pen(OpenLadderPalette.Rail, 3.0f))
            {
                g.DrawLine(rail, LadderCanvasLayout.LeftRail, wire - 40, LadderCanvasLayout.LeftRail, top + height - 10);
                g.DrawLine(rail, L.RightRail, wire - 40, L.RightRail, top + height - 10);
            }

            // Células: o fundo só existe sob o ponteiro e na seleção.
            for (int c = 0; c < LadderRung.ColumnCount; c++)
            {
                bool cellSelected = selected && c == SelectedColumn && SelectedLane == 0;
                if (r == HoverRung && c == HoverColumn && !cellSelected)
                    DrawHover(g, L.CellRect(r, c));
                if (cellSelected) DrawSelection(g, L.CellRect(r, c));
                if (selected && c == SelectedColumn && SelectedLane == 1 && HasParallel(r, c))
                    DrawSelection(g, L.BranchRect(r, c));
            }

            // Guias de coluna só na linha selecionada.
            if (selected)
            {
                using (Pen guide = new Pen(OpenLadderPalette.GridLine, 1.0f))
                {
                    guide.DashStyle = DashStyle.Dash;
                    for (int c = 1; c < LadderRung.ColumnCount; c++)
                    {
                        int gx = LadderCanvasLayout.LeftRail + c * L.CellWidth;
                        g.DrawLine(guide, gx, wire - 34, gx, wire + 34);
                    }
                }
            }

            // Fio principal: energizado tem halo e traço grosso, não só outra cor.
            if (on)
            {
                using (Pen halo = new Pen(OpenLadderPalette.Tint(OpenLadderPalette.Energized, 70), 10.0f))
                {
                    halo.StartCap = LineCap.Round;
                    halo.EndCap = LineCap.Round;
                    g.DrawLine(halo, LadderCanvasLayout.LeftRail, wire, L.RightRail, wire);
                }
            }
            using (Pen wirePen = new Pen(on ? OpenLadderPalette.Energized : OpenLadderPalette.Wire, on ? 3.4f : 2.2f))
                g.DrawLine(wirePen, LadderCanvasLayout.LeftRail, wire, L.RightRail, wire);

            for (int c = 0; c < LadderRung.ColumnCount; c++)
            {
                Rectangle main = L.CellRect(r, c);
                DrawElementScaled(g, rung.Elements[c], main, wire, false, ElementBackground(r, c, false), PowerFor(r, c, false));

                if (!HasParallel(r, c)) continue;
                int branchY = L.BranchAt(r);
                int cellLeft = LadderCanvasLayout.LeftRail + c * L.CellWidth;
                int x1 = cellLeft + 12;
                int x2 = cellLeft + L.CellWidth - 12;
                using (Pen bp = new Pen(OpenLadderPalette.Wire, 2.0f))
                {
                    g.DrawLine(bp, x1, wire, x1, branchY);
                    g.DrawLine(bp, x1, branchY, x2, branchY);
                    g.DrawLine(bp, x2, branchY, x2, wire);
                }
                DrawElementScaled(g, rung.Parallel[c], L.BranchRect(r, c), branchY, true, ElementBackground(r, c, true), PowerFor(r, c, true));
            }

            using (Pen separator = new Pen(OpenLadderPalette.GridLine, 1.0f))
                g.DrawLine(separator, LadderCanvasLayout.GutterWidth, top + height - 1, L.Width + 40, top + height - 1);
        }

        private void DrawElementScaled(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background, LadderPower power)
        {
            if (element == null || element.Type == LadderElementType.Empty) return;

            float k = LadderCanvasLayout.SymbolScale;
            int cx = cell.Left + cell.Width / 2;
            int w = (int)Math.Round(cell.Width / k);
            int h = (int)Math.Round(cell.Height / k);
            GraphicsState state = g.Save();
            g.TranslateTransform(cx, y);
            g.ScaleTransform(k, k);
            LadderSymbols.Draw(g, element, new Rectangle(-w / 2, -h / 2, w, h), 0, branch, background, power);
            g.Restore(state);
        }

        private void DrawComment(Graphics g, LadderCanvasLayout L, int r)
        {
            Rectangle strip = L.CommentRect(r);
            bool selected = r == SelectedRung;
            bool hover = hoverComment == r;
            bool editing = r == editRung && editBox != null;
            string text = editing ? editBox.Text : Rungs[r].Comment;
            bool empty = string.IsNullOrEmpty(text);

            Rectangle box = new Rectangle(strip.Left, strip.Top + 3, strip.Width, strip.Height - 6);
            if (editing)
            {
                FillRounded(g, box, OpenLadderPalette.ChromeLight);
                OutlineRounded(g, box, OpenLadderPalette.Focus, 2.0f);
                return;
            }
            if (hover) FillRounded(g, box, OpenLadderPalette.CanvasHover);

            int textLeft = strip.Left + LadderCanvasLayout.CommentPadX + 6;
            int textWidth = strip.Width - 2 * LadderCanvasLayout.CommentPadX - 6;

            if (empty)
            {
                using (Font font = new Font("Segoe UI", LadderCanvasLayout.CommentFontPixels, FontStyle.Italic, GraphicsUnit.Pixel))
                using (Brush ink = new SolidBrush(hover || selected ? OpenLadderPalette.Muted : OpenLadderPalette.Faint))
                using (StringFormat format = LadderCanvasLayout.CommentFormat())
                    g.DrawString("Clique duas vezes para comentar", font, ink,
                        new RectangleF(textLeft, strip.Top + LadderCanvasLayout.CommentPadY, textWidth, LadderCanvasLayout.LineHeight()), format);
                return;
            }

            // Barra à esquerda: marca o comentário como parte da linha, sem caixa pesada.
            using (Brush bar = new SolidBrush(selected ? OpenLadderPalette.Accent : OpenLadderPalette.BorderStrong))
                g.FillRectangle(bar, strip.Left, strip.Top + 6, 3, strip.Height - 12);

            using (Font font = LadderCanvasLayout.CommentFont())
            using (Brush ink = new SolidBrush(OpenLadderPalette.Muted))
            using (StringFormat format = LadderCanvasLayout.CommentFormat())
            {
                float line = font.GetHeight(g);
                int linesShown = Math.Max(1, (strip.Height - 2 * LadderCanvasLayout.CommentPadY) / LadderCanvasLayout.LineHeight());
                g.DrawString(text, font, ink,
                    new RectangleF(textLeft, strip.Top + LadderCanvasLayout.CommentPadY, textWidth, linesShown * line + 1), format);
            }
        }

        private void DrawAddRow(Graphics g, LadderCanvasLayout L)
        {
            Rectangle ar = L.AddRect();
            bool active = hoverAdd;
            using (Pen dash = new Pen(active ? OpenLadderPalette.Accent : OpenLadderPalette.BorderStrong, active ? 1.8f : 1.2f))
            {
                dash.DashStyle = DashStyle.Dash;
                OutlineRounded(g, ar, dash);
            }
            if (active) FillRounded(g, ar, OpenLadderPalette.Tint(OpenLadderPalette.Accent, 26));

            using (Font font = new Font("Segoe UI Semibold", 15.0f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush ink = new SolidBrush(active ? OpenLadderPalette.Accent : OpenLadderPalette.Muted))
            using (StringFormat centered = new StringFormat(StringFormat.GenericTypographic))
            {
                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Center;
                g.DrawString("+  Nova linha", font, ink, new RectangleF(ar.Left, ar.Top, ar.Width, ar.Height), centered);
            }
        }

        /// <summary>Faixa de colunas fixa no topo: acompanha só a rolagem horizontal.</summary>
        private void DrawColumnHeader(Graphics g, LadderCanvasLayout L, Point scroll, float zoom)
        {
            using (Matrix header = new Matrix(zoom, 0.0f, 0.0f, zoom, scroll.X, 0.0f))
                g.Transform = header;

            // Faixa e divisórias são retângulos alinhados ao eixo: com antialias a
            // borda superior fica meio pixel para dentro e deixa passar uma linha do
            // conteúdo que rolou por baixo.
            g.SmoothingMode = SmoothingMode.None;

            int height = LadderCanvasLayout.HeaderHeight;
            int width = L.RightRail + ClientSize.Width + 80;
            using (Brush band = new SolidBrush(OpenLadderPalette.Chrome))
                g.FillRectangle(band, -width, -height, width * 2, height * 2);
            using (Pen edge = new Pen(OpenLadderPalette.Border, 1.0f))
                g.DrawLine(edge, -width, height, width * 2, height);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Font font = new Font("Segoe UI Semibold", 12.0f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font small = new Font("Segoe UI", 10.0f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (StringFormat centered = new StringFormat(StringFormat.GenericTypographic))
            {
                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Center;
                for (int c = 0; c < LadderRung.ColumnCount; c++)
                {
                    int left = LadderCanvasLayout.LeftRail + c * L.CellWidth;
                    bool active = c == SelectedColumn;
                    if (active)
                    {
                        using (Brush fill = new SolidBrush(OpenLadderPalette.SelectionFill))
                            g.FillRectangle(fill, left + 2, 3, L.CellWidth - 4, height - 6);
                    }

                    bool output = c == LadderRung.ColumnCount - 1;
                    string label = "C" + (c + 1).ToString();
                    Color ink = active ? OpenLadderPalette.Accent : OpenLadderPalette.Muted;
                    if (output)
                    {
                        using (Brush b = new SolidBrush(ink))
                            g.DrawString(label, font, b, new RectangleF(left, 0, L.CellWidth * 0.45f, height), centered);
                        using (Brush b = new SolidBrush(active ? OpenLadderPalette.Accent : OpenLadderPalette.Faint))
                            g.DrawString("saída", small, b, new RectangleF(left + L.CellWidth * 0.40f, 0, L.CellWidth * 0.58f, height), centered);
                    }
                    else
                    {
                        using (Brush b = new SolidBrush(ink))
                            g.DrawString(label, font, b, new RectangleF(left, 0, L.CellWidth, height), centered);
                    }
                }
            }
        }

        /// <summary>Calha de linhas fixa à esquerda: acompanha só a rolagem vertical e acaba junto com o conteúdo.</summary>
        private void DrawRowGutter(Graphics g, LadderCanvasLayout L, Point scroll, float zoom)
        {
            using (Matrix gutter = new Matrix(zoom, 0.0f, 0.0f, zoom, 0.0f, scroll.Y))
                g.Transform = gutter;

            g.SmoothingMode = SmoothingMode.None;
            int bottom = L.ContentBottom;
            int topEdge = (int)Math.Floor(-scroll.Y / zoom) - 4;
            int w = LadderCanvasLayout.GutterWidth;
            using (Brush band = new SolidBrush(OpenLadderPalette.Chrome))
                g.FillRectangle(band, 0, topEdge, w - 1, bottom - topEdge);
            using (Pen edge = new Pen(OpenLadderPalette.Border, 1.0f))
                g.DrawLine(edge, w - 1, topEdge, w - 1, bottom);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Font number = new Font("Segoe UI Semibold", 15.0f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font tag = new Font("Segoe UI Semibold", 10.0f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat centered = new StringFormat(StringFormat.GenericTypographic))
            {
                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Center;
                for (int r = 0; r < Rungs.Count; r++)
                {
                    bool active = r == SelectedRung;
                    bool on = RungOn(r);
                    int wire = L.WireAt(r);
                    Rectangle badge = new Rectangle(8, wire - 17, w - 17, 34);

                    if (active)
                    {
                        using (Brush bar = new SolidBrush(OpenLadderPalette.Accent))
                            g.FillRectangle(bar, 0, L.Top[r] + 2, 4, L.Height[r] - 4);
                    }

                    Color fill = on ? OpenLadderPalette.EnergizedBg : active ? OpenLadderPalette.SelectionFill : OpenLadderPalette.ChromeLight;
                    Color line = on ? OpenLadderPalette.Energized : active ? OpenLadderPalette.SelectionEdge : OpenLadderPalette.Border;
                    Color ink = on ? OpenLadderPalette.Energized : active ? OpenLadderPalette.Accent : OpenLadderPalette.Muted;
                    FillRounded(g, badge, fill);
                    OutlineRounded(g, badge, line, on ? 2.0f : 1.2f);
                    using (Brush b = new SolidBrush(ink))
                        g.DrawString("L" + (r + 1).ToString(), number, b, new RectangleF(badge.Left, badge.Top, badge.Width, badge.Height), centered);

                    if (on)
                    {
                        using (Brush b = new SolidBrush(OpenLadderPalette.Energized))
                            g.DrawString("ATIVA", tag, b, new RectangleF(0, badge.Bottom + 1, w - 1, 14), centered);
                    }
                }
            }
        }

        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            return OpenLadderMetrics.RoundedRect(r, radius);
        }

        private static void FillRounded(Graphics g, Rectangle r, Color color)
        {
            using (GraphicsPath path = RoundedPath(r, 6))
            using (Brush b = new SolidBrush(color))
                g.FillPath(b, path);
        }

        private static void OutlineRounded(Graphics g, Rectangle r, Color color, float width)
        {
            using (Pen pen = new Pen(color, width)) OutlineRounded(g, r, pen);
        }

        private static void OutlineRounded(Graphics g, Rectangle r, Pen pen)
        {
            using (GraphicsPath path = RoundedPath(r, 6)) g.DrawPath(pen, path);
        }

        private static void DrawHover(Graphics g, Rectangle cell)
        {
            FillRounded(g, cell, OpenLadderPalette.CanvasHover);
            OutlineRounded(g, cell, OpenLadderPalette.Border, 1.0f);
        }

        private static void DrawSelection(Graphics g, Rectangle cell)
        {
            FillRounded(g, cell, OpenLadderPalette.SelectionFill);
            OutlineRounded(g, cell, OpenLadderPalette.SelectionEdge, 1.8f);
        }

        /// <summary>Cor exata do que está atrás do símbolo, para ele apagar o fio sem deixar um retângulo.</summary>
        private Color ElementBackground(int rung, int column, bool branch)
        {
            if (rung == SelectedRung && column == SelectedColumn && SelectedLane == (branch ? 1 : 0))
                return OpenLadderPalette.SelectionFill;
            if (!branch && rung == HoverRung && column == HoverColumn)
                return OpenLadderPalette.CanvasHover;
            return rung == SelectedRung ? OpenLadderPalette.CanvasRow : OpenLadderPalette.Canvas;
        }

        // ------------------------------------------------------------------
        // Ponteiro
        // ------------------------------------------------------------------

        private LadderElement CellElement(int rung, int column, int lane)
        {
            if (Rungs == null || rung < 0 || rung >= Rungs.Count) return null;
            if (column < 0 || column >= LadderRung.ColumnCount) return null;
            return lane == 1 ? Rungs[rung].Parallel[column] : Rungs[rung].Elements[column];
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            int rung, col, lane;
            LadderRegion region = Locate(e.Location, out rung, out col, out lane);
            bool inside = region == LadderRegion.Cell;

            // O arrasto só começa depois de o ponteiro sair do lugar: um clique
            // com tremor de mão continua sendo clique, não movimento de elemento.
            if (dragArmed && !dragActive && (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                if (Math.Abs(e.X - dragOrigin.X) > 4 || Math.Abs(e.Y - dragOrigin.Y) > 4)
                {
                    dragActive = true;
                    Cursor = Cursors.SizeAll;
                }
            }

            if (dragActive)
            {
                int alvoRung = inside ? rung : -1;
                int alvoCol = inside ? col : -1;
                if (dropRung != alvoRung || dropColumn != alvoCol)
                {
                    dropRung = alvoRung;
                    dropColumn = alvoCol;
                    Invalidate();
                }
                return;
            }

            int newHoverRung = inside ? rung : -1;
            int newHoverCol = inside ? col : -1;
            int newComment = region == LadderRegion.Comment ? rung : -1;
            bool newAdd = region == LadderRegion.AddRow;
            if (newHoverRung != HoverRung || newHoverCol != HoverColumn || newComment != hoverComment || newAdd != hoverAdd)
            {
                HoverRung = newHoverRung;
                HoverColumn = newHoverCol;
                hoverComment = newComment;
                hoverAdd = newAdd;
                Cursor = newAdd ? Cursors.Hand : newComment >= 0 ? Cursors.IBeam : (DragEnabled ? Cursors.Default : Cursors.Cross);
                Invalidate();
            }
        }

        private void CanvasMouseDown(object sender, MouseEventArgs e)
        {
            Focus();

            int rung, col, lane;
            LadderRegion region = Locate(e.Location, out rung, out col, out lane);

            if (e.Button == MouseButtons.Right)
            {
                // Com ferramenta armada o botão direito devolve o mouse. Em modo
                // ponteiro ele abre o menu sob o cursor: clicar com o direito e não
                // acontecer nada não é comportamento de editor.
                if (DragEnabled && (region == LadderRegion.Cell || region == LadderRegion.Comment || region == LadderRegion.Gutter))
                {
                    SelectRegion(region, rung, col, lane);
                    if (ElementContextMenu != null) ElementContextMenu(this, new MouseEventArgs(e.Button, e.Clicks, e.X, e.Y, e.Delta));
                    return;
                }

                if (ToolReleased != null) ToolReleased(this, EventArgs.Empty);
                return;
            }

            if (region == LadderRegion.AddRow)
            {
                if (AddRungRequested != null) AddRungRequested(this, EventArgs.Empty);
                return;
            }

            SelectRegion(region, rung, col, lane);
            if (region != LadderRegion.Cell) return;

            // Segurar o botão sobre um elemento arma o arrasto. Ele só vira
            // movimento de verdade quando o ponteiro anda (ver CanvasMouseMove).
            dragArmed = false;
            dragActive = false;
            if (DragEnabled)
            {
                LadderElement origem = CellElement(rung, col, lane);
                if (origem != null && origem.Type != LadderElementType.Empty)
                {
                    dragArmed = true;
                    dragOrigin = e.Location;
                    dragRung = rung;
                    dragColumn = col;
                    dragLane = lane;
                    dropRung = -1;
                    dropColumn = -1;
                }
            }

            if (ElementAction != null) ElementAction(this, EventArgs.Empty);
        }

        private void SelectRegion(LadderRegion region, int rung, int col, int lane)
        {
            if (rung < 0 || rung >= Rungs.Count) return;
            SelectedRung = rung;
            if (region == LadderRegion.Cell)
            {
                SelectedColumn = col;
                SelectedLane = lane;
            }
            else
            {
                if (SelectedColumn < 0) SelectedColumn = 0;
                SelectedLane = 0;
            }
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        private void CanvasMouseUp(object sender, MouseEventArgs e)
        {
            bool moveu = dragActive;
            int destinoRung = dropRung;
            int destinoColuna = dropColumn;

            dragArmed = false;
            dragActive = false;
            dropRung = -1;
            dropColumn = -1;
            Cursor = DragEnabled ? Cursors.Default : Cursors.Cross;

            if (!moveu) return;
            Invalidate();

            if (destinoRung < 0 || destinoColuna < 0) return;
            if (destinoRung == dragRung && destinoColuna == dragColumn) return;

            LadderMoveEventArgs args = new LadderMoveEventArgs(dragRung, dragColumn, dragLane, destinoRung, destinoColuna);
            if (ElementMoved != null) ElementMoved(this, args);
        }

        private void CanvasMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int rung, col, lane;
            LadderRegion region = Locate(e.Location, out rung, out col, out lane);
            if (region == LadderRegion.Comment)
            {
                SelectRegion(region, rung, col, lane);
                BeginEditComment(rung);
                return;
            }
            if (region != LadderRegion.Cell) return;
            SelectRegion(region, rung, col, lane);
            if (ElementDoubleClick != null) ElementDoubleClick(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------------
        // Teclado: Enter ou F2 comentam a linha selecionada.
        // ------------------------------------------------------------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (editRung < 0 && Focused && Rungs != null && SelectedRung >= 0
                && (keyData == Keys.F2 || keyData == Keys.Enter))
            {
                BeginEditComment(SelectedRung);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------
        // Edição do comentário no próprio lugar.
        // ------------------------------------------------------------------

        public bool IsEditingComment
        {
            get { return editRung >= 0; }
        }

        public void BeginEditComment(int rung)
        {
            if (Rungs == null || rung < 0 || rung >= Rungs.Count) return;
            if (editRung == rung) return;
            if (editRung >= 0) EndEditComment(true, false);

            SelectedRung = rung;
            if (SelectedColumn < 0) SelectedColumn = 0;
            editRung = rung;

            RungCommentBox box = new RungCommentBox();
            box.BackColor = OpenLadderPalette.ChromeLight;
            box.ForeColor = OpenLadderPalette.Fore;
            box.AccessibleName = "Comentário da linha " + (rung + 1).ToString();
            box.Text = (Rungs[rung].Comment ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n");
            box.TextChanged += delegate { OnEditTextChanged(); };
            box.LostFocus += delegate { EndEditComment(true, false); };
            box.CommitRequested += delegate { EndEditComment(true, true); };
            box.CancelRequested += delegate { EndEditComment(false, true); };
            editBox = box;
            Controls.Add(box);

            UpdateZoomExtent();
            ScrollToRung(rung);
            PlaceEditBox();
            box.Focus();
            box.SelectionStart = box.TextLength;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        private void OnEditTextChanged()
        {
            UpdateZoomExtent();
            PlaceEditBox();
            Invalidate();
        }

        private void PlaceEditBox()
        {
            if (editBox == null || editRung < 0 || Rungs == null || editRung >= Rungs.Count) return;

            LadderCanvasLayout layout = CurrentLayout();
            Rectangle rc = layout.CommentRect(editRung);
            float zoom = EffectiveZoom;
            Point scroll = AutoScrollPosition;

            float pixels = LadderCanvasLayout.CommentFontPixels * zoom;
            if (editFont == null || Math.Abs(editFont.Size - pixels) > 0.2f)
            {
                Font next = new Font("Segoe UI", pixels, FontStyle.Regular, GraphicsUnit.Pixel);
                editBox.Font = next;
                if (editFont != null) editFont.Dispose();
                editFont = next;
            }

            int left = (int)Math.Round((rc.Left + LadderCanvasLayout.CommentPadX + 6) * zoom) + scroll.X;
            int top = (int)Math.Round((rc.Top + LadderCanvasLayout.CommentPadY) * zoom) + scroll.Y;
            int width = (int)Math.Round((rc.Width - 2 * LadderCanvasLayout.CommentPadX - 6) * zoom);
            int height = (int)Math.Round((rc.Height - 2 * LadderCanvasLayout.CommentPadY) * zoom);
            editBox.SetBounds(left, top, Math.Max(40, width), Math.Max(10, height));
        }

        /// <summary>Confirma o comentário em edição, se houver (usado antes de salvar).</summary>
        public void CommitCommentEdit()
        {
            if (editRung >= 0) EndEditComment(true, false);
        }

        public void EndEditComment(bool commit, bool restoreFocus)
        {
            if (editRung < 0 || editBox == null) return;

            int rung = editRung;
            RungCommentBox box = editBox;
            string text = box.Text.Replace("\r\n", "\n");
            editRung = -1;
            editBox = null;

            // Em descarte (LostFocus durante o fechamento do formulario) nao ha handle para
            // o BeginInvoke nem sentido em gravar: o controle libera tudo e sai.
            bool alive = !IsDisposed && !Disposing;
            bool deferred = alive && IsHandleCreated;
            box.Visible = false;
            Controls.Remove(box);
            if (deferred) BeginInvoke((MethodInvoker)delegate { box.Dispose(); });
            else box.Dispose();
            if (editFont != null)
            {
                Font old = editFont;
                editFont = null;
                if (deferred) BeginInvoke((MethodInvoker)delegate { old.Dispose(); });
                else old.Dispose();
            }
            if (!alive) return;

            UpdateZoomExtent();
            Invalidate();
            if (commit && CommentEdited != null) CommentEdited(this, new LadderCommentEventArgs(rung, text));
            UpdateZoomExtent();
            Invalidate();
            if (restoreFocus && !Focused) Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && editFont != null) editFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
