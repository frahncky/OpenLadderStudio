using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ModernPC12
{
    internal enum FlowState
    {
        Pending,
        Active,
        Done,
        Failed
    }

    /// <summary>
    /// Faixa de passos de uma operacao com o PLC: Conectar, Verificar, Transferir e
    /// Confirmar (ou os equivalentes da leitura). Cada passo mostra o estado por
    /// simbolo e texto, nao so por cor: ○ pendente, ● em andamento, ✓ concluido,
    /// ✕ falhou. So apresenta; nao decide nada nem altera o que e transmitido.
    /// </summary>
    internal sealed class ProgramFlowSteps : Control
    {
        private readonly string[] titles;
        private readonly FlowState[] states;

        public ProgramFlowSteps(params string[] stepTitles)
        {
            titles = stepTitles;
            states = new FlowState[stepTitles.Length];
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = 54;
            BackColor = OpenLadderPalette.Shell;
            Font = new Font("Segoe UI", 9.0f);
            TabStop = false;
            AccessibleRole = AccessibleRole.ProgressBar;
        }

        public void Reset()
        {
            for (int i = 0; i < states.Length; i++) states[i] = FlowState.Pending;
            Invalidate();
        }

        public void Set(int index, FlowState state)
        {
            if (index < 0 || index >= states.Length || states[index] == state) return;
            states[index] = state;
            Invalidate();
        }

        /// <summary>Marca como concluidos os passos anteriores e o indicado como em andamento.</summary>
        public void Advance(int index)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (i < index && states[i] != FlowState.Failed) states[i] = FlowState.Done;
                else if (i == index) states[i] = FlowState.Active;
                else states[i] = FlowState.Pending;
            }
            Invalidate();
        }

        /// <summary>Marca como falho o passo que estava em andamento (ou o primeiro pendente).</summary>
        public void FailActive()
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == FlowState.Active) { states[i] = FlowState.Failed; Invalidate(); return; }
            }
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == FlowState.Pending) { states[i] = FlowState.Failed; Invalidate(); return; }
            }
        }

        public void CompleteAll()
        {
            for (int i = 0; i < states.Length; i++) states[i] = FlowState.Done;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int count = states.Length;
            if (count == 0) return;
            int margin = 20;
            int slot = Math.Max(120, (Width - margin * 2) / count);
            using (Font bold = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold))
            {
                for (int i = 0; i < count; i++)
                {
                    int left = margin + i * slot;
                    int mid = Height / 2;
                    FlowState state = states[i];
                    Color colour = Colour(state);

                    Rectangle badge = new Rectangle(left, mid - 12, 24, 24);
                    using (Brush fill = new SolidBrush(state == FlowState.Pending ? BackColor : OpenLadderPalette.Tint(colour, 55)))
                        g.FillEllipse(fill, badge);
                    using (Pen ring = new Pen(colour, state == FlowState.Active ? 2.4f : 1.6f))
                        g.DrawEllipse(ring, badge);

                    using (StringFormat centered = new StringFormat())
                    using (Brush ink = new SolidBrush(colour))
                    {
                        centered.Alignment = StringAlignment.Center;
                        centered.LineAlignment = StringAlignment.Center;
                        g.DrawString(Symbol(state, i), bold, ink, new RectangleF(badge.Left, badge.Top, badge.Width, badge.Height + 1), centered);
                    }

                    using (Brush text = new SolidBrush(state == FlowState.Pending ? OpenLadderPalette.Muted : OpenLadderPalette.Fore))
                        g.DrawString(titles[i], state == FlowState.Active ? bold : Font, text, left + 32, mid - 9);

                    if (i < count - 1)
                    {
                        using (Pen arrow = new Pen(OpenLadderPalette.Border, 1.6f))
                        {
                            int x1 = left + slot - 34;
                            int x2 = left + slot - 12;
                            g.DrawLine(arrow, x1, mid, x2, mid);
                            g.DrawLine(arrow, x2 - 4, mid - 4, x2, mid);
                            g.DrawLine(arrow, x2 - 4, mid + 4, x2, mid);
                        }
                    }
                }
            }

            using (Pen line = new Pen(OpenLadderPalette.Border))
                g.DrawLine(line, 0, Height - 1, Width, Height - 1);
        }

        private static Color Colour(FlowState state)
        {
            switch (state)
            {
                case FlowState.Active: return OpenLadderPalette.Accent;
                case FlowState.Done: return OpenLadderPalette.Ok;
                case FlowState.Failed: return OpenLadderPalette.Danger;
            }
            return OpenLadderPalette.Muted;
        }

        private static string Symbol(FlowState state, int index)
        {
            switch (state)
            {
                case FlowState.Done: return "✓";
                case FlowState.Failed: return "✕";
            }
            return (index + 1).ToString();
        }
    }

    /// <summary>
    /// Painel comum de progresso e registro das telas de leitura e transferencia:
    /// titulo, barra de progresso indeterminada enquanto ha operacao em curso e o
    /// registro em fonte monoespacada (o TextBox e do formulario, que continua dono
    /// do texto e da gravacao em arquivo).
    /// </summary>
    internal sealed class ProgramLogPanel : Panel
    {
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Label caption = new Label();

        public ProgramLogPanel(TextBox log)
        {
            Dock = DockStyle.Fill;
            BackColor = OpenLadderPalette.Chrome;
            Padding = new Padding(12, 8, 12, 12);

            log.Dock = DockStyle.Fill;
            log.BackColor = OpenLadderPalette.Canvas;
            log.ForeColor = OpenLadderPalette.Fore;
            Controls.Add(log);

            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 30;
            head.BackColor = OpenLadderPalette.Chrome;
            Controls.Add(head);

            caption.Text = "REGISTRO DA OPERAÇÃO";
            caption.AutoSize = true;
            caption.Font = new Font("Segoe UI Semibold", 8.0f, FontStyle.Bold);
            caption.ForeColor = OpenLadderPalette.Muted;
            caption.Location = new Point(2, 7);
            head.Controls.Add(caption);

            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 0;
            progress.Size = new Size(180, 10);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            progress.Location = new Point(head.Width - progress.Width - 4, 10);
            progress.Visible = false;
            head.Controls.Add(progress);
            head.SizeChanged += delegate { progress.Left = head.Width - progress.Width - 4; };

            log.BringToFront();
        }

        public void SetBusy(string label, bool busy)
        {
            caption.Text = busy ? "REGISTRO DA OPERAÇÃO - " + label : "REGISTRO DA OPERAÇÃO";
            progress.Visible = busy;
            progress.MarqueeAnimationSpeed = busy ? 30 : 0;
        }
    }
}
