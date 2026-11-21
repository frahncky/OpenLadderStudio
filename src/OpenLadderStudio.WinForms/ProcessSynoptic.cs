using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ModernPC12
{
    // Renderizador do sinóptico compartilhado pelas duas trilhas. Extraído sem mudança de
    // desenho de LadderSimulator.cs. Cada trilha fornece a sua classe SimulatorTheme (as cores
    // por papel); as cores de cena vêm de OpenLadderPaletteTokens, no núcleo.
    // C# 5 e sem anotações de nulabilidade: também é compilado pelo csc do .NET Framework.

    /// <summary>
    /// Sinóptico genérico: desenha a cena descrita pela planta, sem conhecer processo algum.
    /// A cena vem em coordenadas próprias e é escalada com proporção preservada.
    /// </summary>
    internal sealed class ProcessSynoptic : Control
    {
        private const double BaseFontSize = 13.0;

        private readonly SimScene scene = new SimScene();
        private ISimulatedProcess plant;
        private PlcProcessImage image;

        public ProcessSynoptic()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = SimulatorTheme.Shell;
            Font = new Font("Segoe UI", 8.25f);
        }

        public void Bind(ISimulatedProcess process, PlcProcessImage processImage)
        {
            plant = process;
            image = processImage;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (plant == null || image == null) return;

            scene.Clear();
            plant.BuildScene(scene, image);

            float scale = (float)Math.Min(Width / SimScene.Width, Height / SimScene.Height);
            if (scale <= 0.0f) return;
            float offsetX = (float)((Width - (SimScene.Width * scale)) / 2.0);
            float offsetY = (float)((Height - (SimScene.Height * scale)) / 2.0);

            float fontSize = Math.Max(6.0f, (float)(BaseFontSize * scale * 0.75));
            using (Font text = new Font("Segoe UI", fontSize))
            using (Font strong = new Font("Segoe UI", fontSize, FontStyle.Bold))
            {
                IList<SimShape> shapes = scene.Shapes;
                for (int i = 0; i < shapes.Count; i++) Draw(g, shapes[i], scale, offsetX, offsetY, text, strong);
            }
        }

        private void Draw(Graphics g, SimShape shape, float scale, float ox, float oy, Font text, Font strong)
        {
            float x = ox + (float)(shape.X * scale);
            float y = oy + (float)(shape.Y * scale);
            float w = (float)(shape.W * scale);
            float h = (float)(shape.H * scale);

            switch (shape.Kind)
            {
                case SimShapeKind.Rectangle:
                    FillAndOutline(g, new RectangleF(x, y, w, h), shape.Fill, shape.Stroke);
                    if (shape.Text.Length > 0) DrawCentred(g, shape.Text, strong, SimulatorTheme.OnFill(shape.Fill), new RectangleF(x, y, w, h));
                    break;

                case SimShapeKind.Ellipse:
                    using (SolidBrush brush = new SolidBrush(SimulatorTheme.For(shape.Fill)))
                        g.FillEllipse(brush, x, y, w, h);
                    using (Pen pen = new Pen(SimulatorTheme.For(shape.Stroke), 1.0f))
                        g.DrawEllipse(pen, x, y, w, h);
                    break;

                case SimShapeKind.Line:
                    using (Pen pen = new Pen(SimulatorTheme.For(shape.Stroke), Math.Max(1.0f, scale * 1.6f)))
                    {
                        if (shape.Dashed) pen.DashStyle = DashStyle.Dot;
                        g.DrawLine(pen, x, y, ox + (float)(shape.W * scale), oy + (float)(shape.H * scale));
                    }
                    break;

                case SimShapeKind.Text:
                    DrawAligned(g, shape, shape.Bold ? strong : text, x, y);
                    break;

                case SimShapeKind.Belt:
                    DrawBelt(g, shape, x, y, w, h, scale);
                    break;

                case SimShapeKind.Level:
                    DrawLevel(g, shape, x, y, w, h);
                    break;

                case SimShapeKind.Lamp:
                    using (SolidBrush brush = new SolidBrush(shape.On ? SimulatorTheme.For(shape.Fill) : Color.FromArgb(OpenLadderPaletteTokens.SceneLampOff.Argb(true))))
                        g.FillEllipse(brush, x, y, w, h);
                    using (Pen pen = new Pen(SimulatorTheme.Border, 1.0f))
                        g.DrawEllipse(pen, x, y, w, h);
                    break;
            }
        }

        private static void FillAndOutline(Graphics g, RectangleF bounds, SimTone fill, SimTone stroke)
        {
            using (SolidBrush brush = new SolidBrush(SimulatorTheme.For(fill)))
                g.FillRectangle(brush, bounds);
            using (Pen pen = new Pen(SimulatorTheme.For(stroke), 1.0f))
                g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        private static void DrawCentred(Graphics g, string value, Font font, Color colour, RectangleF bounds)
        {
            using (SolidBrush brush = new SolidBrush(colour))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                g.DrawString(value, font, brush, bounds, format);
            }
        }

        private static void DrawAligned(Graphics g, SimShape shape, Font font, float x, float y)
        {
            using (SolidBrush brush = new SolidBrush(SimulatorTheme.For(shape.Fill)))
            using (StringFormat format = new StringFormat())
            {
                if (shape.Align == SimTextAlign.Center) format.Alignment = StringAlignment.Center;
                else if (shape.Align == SimTextAlign.Right) format.Alignment = StringAlignment.Far;
                g.DrawString(shape.Text, font, brush, x, y, format);
            }
        }

        private static void DrawBelt(Graphics g, SimShape shape, float x, float y, float w, float h, float scale)
        {
            RectangleF belt = new RectangleF(x, y, w, h);
            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Metal))
                g.FillRectangle(brush, belt);

            float step = Math.Max(6.0f, 18.0f * scale);
            float offset = (float)(shape.Value % 18.0) * scale;
            using (Pen pen = new Pen(Color.FromArgb(OpenLadderPaletteTokens.SceneBeltStripeAlpha, Color.FromArgb(OpenLadderPaletteTokens.SceneBeltStripe.Argb(true))), Math.Max(1.0f, scale * 2.0f)))
                for (float sx = x - step + offset; sx < x + w; sx += step)
                {
                    if (sx < x) continue;
                    g.DrawLine(pen, sx, y + 1.0f, sx, y + h - 1.0f);
                }

            using (Pen pen = new Pen(SimulatorTheme.Border, 1.0f))
                g.DrawRectangle(pen, belt.X, belt.Y, belt.Width, belt.Height);
        }

        private static void DrawLevel(Graphics g, SimShape shape, float x, float y, float w, float h)
        {
            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Dark))
                g.FillRectangle(brush, x, y, w, h);

            float filled = (float)(h * shape.Value);
            if (filled > 0.5f)
                using (SolidBrush brush = new SolidBrush(SimulatorTheme.For(shape.Fill)))
                    g.FillRectangle(brush, x, y + h - filled, w, filled);

            using (Pen pen = new Pen(SimulatorTheme.For(shape.Stroke), 1.5f))
                g.DrawRectangle(pen, x, y, w, h);
        }
    }
}
