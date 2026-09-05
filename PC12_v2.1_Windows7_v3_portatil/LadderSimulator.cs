using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class SimulatorProgram
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LadderSimulatorForm());
        }
    }

    internal static class SimulatorTheme
    {
        public static readonly Color Shell = Color.FromArgb(29, 31, 34);
        public static readonly Color Chrome = Color.FromArgb(37, 39, 43);
        public static readonly Color Panel = Color.FromArgb(47, 50, 55);
        public static readonly Color Border = Color.FromArgb(61, 64, 69);
        public static readonly Color Accent = Color.FromArgb(45, 170, 107);
        public static readonly Color Fore = Color.FromArgb(226, 230, 234);
        public static readonly Color Muted = Color.FromArgb(150, 157, 164);
        public static readonly Color Error = Color.FromArgb(220, 105, 105);
        public static readonly Color Warning = Color.FromArgb(215, 166, 71);
        public static readonly Color Info = Color.FromArgb(91, 170, 245);
        public static readonly Color Metal = Color.FromArgb(86, 91, 98);
        public static readonly Color Cargo = Color.FromArgb(198, 148, 84);
    }

    internal sealed class BufferedListView : ListView
    {
        public BufferedListView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    /// <summary>
    /// Sinóptico da esteira. Desenha a planta a partir do estado físico e das saídas do PLC virtual.
    /// </summary>
    internal sealed class ConveyorSynoptic : Control
    {
        private ConveyorProcess plant;
        private PlcProcessImage image;
        private double beltPhase;

        private SimBitRef motorBit;
        private SimBitRef lampBit;
        private SimBitRef entryBit;
        private SimBitRef exitBit;
        private SimBitRef feedbackBit;
        private SimBitRef pusherBit;

        public ConveyorSynoptic()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = SimulatorTheme.Shell;
            Font = new Font("Segoe UI", 8.25f);

            SimAddress.TryParseBit(ConveyorProcess.MotorOutput, out motorBit);
            SimAddress.TryParseBit(ConveyorProcess.LampOutput, out lampBit);
            SimAddress.TryParseBit(ConveyorProcess.EntrySensorInput, out entryBit);
            SimAddress.TryParseBit(ConveyorProcess.ExitSensorInput, out exitBit);
            SimAddress.TryParseBit(ConveyorProcess.PusherFeedbackInput, out feedbackBit);
            SimAddress.TryParseBit(ConveyorProcess.PusherOutput, out pusherBit);
        }

        public void Bind(ConveyorProcess process, PlcProcessImage processImage)
        {
            plant = process;
            image = processImage;
            Invalidate();
        }

        /// <summary>Avança a animação da correia proporcionalmente à velocidade real simulada.</summary>
        public void Advance(double dtSeconds)
        {
            if (plant == null) return;
            beltPhase += plant.BeltSpeed * dtSeconds * 120.0;
            if (beltPhase > 10000.0) beltPhase = 0.0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (plant == null || image == null) return;

            int left = 56;
            int right = Math.Max(left + 80, Width - 56);
            int beltY = Height - 62;
            int beltHeight = 12;
            double scale = (right - left) / ConveyorProcess.BeltLength;

            DrawFrame(g, left, right, beltY, beltHeight);
            DrawBelt(g, left, right, beltY, beltHeight);
            DrawSensor(g, left, beltY, scale, ConveyorProcess.EntrySensorPosition, "S1  " + ConveyorProcess.EntrySensorInput, image.GetBit(entryBit));
            DrawSensor(g, left, beltY, scale, ConveyorProcess.ExitSensorPosition, "S2  " + ConveyorProcess.ExitSensorInput, image.GetBit(exitBit));
            DrawBoxes(g, left, beltY, scale);
            DrawPusher(g, left, beltY, scale);
            DrawMotor(g, left, beltY);
            DrawLamp(g, right);
            DrawLegend(g, left);
        }

        private void DrawFrame(Graphics g, int left, int right, int beltY, int beltHeight)
        {
            using (Pen pen = new Pen(SimulatorTheme.Border, 1.0f))
            {
                g.DrawLine(pen, left - 18, beltY + beltHeight + 22, right + 18, beltY + beltHeight + 22);
                g.DrawLine(pen, left - 6, beltY + beltHeight, left - 6, beltY + beltHeight + 22);
                g.DrawLine(pen, right + 6, beltY + beltHeight, right + 6, beltY + beltHeight + 22);
            }
        }

        private void DrawBelt(Graphics g, int left, int right, int beltY, int beltHeight)
        {
            Rectangle belt = new Rectangle(left, beltY, right - left, beltHeight);
            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Metal))
                g.FillRectangle(brush, belt);

            // Estrias que se deslocam com a velocidade real da correia.
            int offset = (int)(beltPhase % 18.0);
            using (Pen pen = new Pen(Color.FromArgb(120, 30, 33, 37), 2.0f))
                for (int x = left - 18 + offset; x < right; x += 18)
                {
                    if (x < left) continue;
                    g.DrawLine(pen, x, beltY + 1, x, beltY + beltHeight - 1);
                }

            using (Pen pen = new Pen(SimulatorTheme.Border, 1.0f))
                g.DrawRectangle(pen, belt);

            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Metal))
            {
                g.FillEllipse(brush, left - 12, beltY - 2, 16, 16);
                g.FillEllipse(brush, right - 4, beltY - 2, 16, 16);
            }
        }

        private void DrawBoxes(Graphics g, int left, int beltY, double scale)
        {
            int boxWidth = Math.Max(8, (int)(ConveyorProcess.BoxLength * scale));
            int boxHeight = 22;
            IList<ConveyorBox> boxes = plant.Boxes;

            for (int i = 0; i < boxes.Count; i++)
            {
                int centre = left + (int)(boxes[i].Position * scale);
                Rectangle box = new Rectangle(centre - (boxWidth / 2), beltY - boxHeight, boxWidth, boxHeight);
                using (SolidBrush brush = new SolidBrush(SimulatorTheme.Cargo))
                    g.FillRectangle(brush, box);
                using (Pen pen = new Pen(Color.FromArgb(230, 180, 110), 1.0f))
                    g.DrawRectangle(pen, box);
                using (Pen pen = new Pen(Color.FromArgb(150, 120, 70), 1.0f))
                    g.DrawLine(pen, box.Left + 2, box.Top + (boxHeight / 2), box.Right - 2, box.Top + (boxHeight / 2));
            }
        }

        private void DrawSensor(Graphics g, int left, int beltY, double scale, double position, string label, bool active)
        {
            int x = left + (int)(position * scale);
            Color colour = active ? SimulatorTheme.Accent : SimulatorTheme.Muted;

            using (Pen pen = new Pen(Color.FromArgb(active ? 150 : 60, colour), 1.0f))
            {
                pen.DashStyle = DashStyle.Dot;
                g.DrawLine(pen, x, beltY - 34, x, beltY);
            }

            using (SolidBrush brush = new SolidBrush(colour))
                g.FillRectangle(brush, x - 4, beltY - 42, 8, 8);

            using (SolidBrush brush = new SolidBrush(active ? SimulatorTheme.Fore : SimulatorTheme.Muted))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                g.DrawString(label, Font, brush, x, beltY + 18, format);
            }
        }

        private void DrawPusher(Graphics g, int left, int beltY, double scale)
        {
            int x = left + (int)(ConveyorProcess.PusherPosition * scale);
            int plateWidth = Math.Max(12, (int)(ConveyorProcess.PusherPlateWidth * scale));
            // O curso leva a placa do repouso ate a altura da caixa sobre a correia.
            int home = beltY - 88;
            int travel = 62;
            int y = home + (int)(plant.PusherStroke * travel);

            using (Pen pen = new Pen(SimulatorTheme.Border, 2.0f))
                g.DrawLine(pen, x, home - 10, x, y);

            Rectangle plate = new Rectangle(x - (plateWidth / 2), y, plateWidth, 10);
            bool commanded = image.GetBit(pusherBit);
            using (SolidBrush brush = new SolidBrush(commanded ? SimulatorTheme.Info : SimulatorTheme.Metal))
                g.FillRectangle(brush, plate);
            using (Pen pen = new Pen(SimulatorTheme.Border, 1.0f))
                g.DrawRectangle(pen, plate);

            bool feedback = image.GetBit(feedbackBit);
            int markerX = x + (plateWidth / 2) + 10;
            using (SolidBrush brush = new SolidBrush(feedback ? SimulatorTheme.Accent : SimulatorTheme.Muted))
                g.FillRectangle(brush, markerX, home + travel, 8, 8);
            using (SolidBrush brush = new SolidBrush(feedback ? SimulatorTheme.Fore : SimulatorTheme.Muted))
                g.DrawString(ConveyorProcess.PusherFeedbackInput, Font, brush, markerX + 12, home + travel - 4);

            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Muted))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                g.DrawString("Desviador  " + ConveyorProcess.PusherOutput, Font, brush, x, home - 28, format);
                g.DrawString("desviadas: " + plant.DivertedCount.ToString(CultureInfo.InvariantCulture), Font, brush, x, beltY + 34, format);
            }
        }

        private void DrawMotor(Graphics g, int left, int beltY)
        {
            bool running = image.GetBit(motorBit);
            Rectangle body = new Rectangle(left - 46, beltY - 6, 26, 26);

            using (SolidBrush brush = new SolidBrush(running ? SimulatorTheme.Accent : SimulatorTheme.Metal))
                g.FillRectangle(brush, body);
            using (Pen pen = new Pen(SimulatorTheme.Border, 1.0f))
                g.DrawRectangle(pen, body);

            using (SolidBrush brush = new SolidBrush(running ? SimulatorTheme.Shell : SimulatorTheme.Fore))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                g.DrawString("M", new Font("Segoe UI", 9.0f, FontStyle.Bold), brush, body, format);
            }

            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Muted))
                g.DrawString(ConveyorProcess.MotorOutput, Font, brush, left - 50, beltY + 24);
        }

        private void DrawLamp(Graphics g, int right)
        {
            bool on = image.GetBit(lampBit);
            Rectangle lamp = new Rectangle(right - 16, 14, 16, 16);

            using (SolidBrush brush = new SolidBrush(on ? SimulatorTheme.Warning : Color.FromArgb(70, 74, 80)))
                g.FillEllipse(brush, lamp);
            using (Pen pen = new Pen(SimulatorTheme.Border, 1.0f))
                g.DrawEllipse(pen, lamp);

            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Muted))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Far;
                g.DrawString("Sinaleiro  " + ConveyorProcess.LampOutput, Font, brush, right - 22, 16, format);
            }
        }

        private void DrawLegend(Graphics g, int left)
        {
            string text = "Correia " + plant.BeltSpeed.ToString("0.000", CultureInfo.InvariantCulture) + " m/s" +
                          "   ·   curso do desviador " + (plant.PusherStroke * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %" +
                          "   ·   perdidas " + plant.LostCount.ToString(CultureInfo.InvariantCulture);
            using (SolidBrush brush = new SolidBrush(SimulatorTheme.Muted))
                g.DrawString(text, Font, brush, left - 46, 16);

            if (plant.OverloadTripped)
                using (SolidBrush brush = new SolidBrush(SimulatorTheme.Error))
                    g.DrawString("RELÉ TÉRMICO ATUADO", new Font("Segoe UI", 8.25f, FontStyle.Bold), brush, left - 46, 34);
        }
    }

    /// <summary>
    /// Faixa compacta com o estado de energização de cada rung na última varredura.
    /// </summary>
    internal sealed class RungStrip : Control
    {
        private LadderScanEngine engine;

        public RungStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = SimulatorTheme.Panel;
            Font = new Font("Consolas", 7.5f);
        }

        public void Bind(LadderScanEngine scanEngine)
        {
            engine = scanEngine;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (engine == null) return;

            IList<CompiledRung> rungs = engine.Rungs;
            int cell = 26;
            int x = 4;

            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                for (int i = 0; i < rungs.Count && x + cell < Width; i++)
                {
                    Rectangle box = new Rectangle(x, 4, cell - 4, 16);
                    Color colour = !rungs[i].Reached ? Color.FromArgb(60, 63, 68)
                        : rungs[i].LastPower ? SimulatorTheme.Accent : Color.FromArgb(78, 82, 88);

                    using (SolidBrush brush = new SolidBrush(colour))
                        g.FillRectangle(brush, box);
                    using (SolidBrush brush = new SolidBrush(rungs[i].LastPower ? SimulatorTheme.Shell : SimulatorTheme.Muted))
                        g.DrawString(rungs[i].Number.ToString(CultureInfo.InvariantCulture), Font, brush, box.Left + (box.Width / 2f), box.Top + 3, format);
                    x += cell;
                }
            }
        }
    }

    internal sealed class LadderSimulatorForm : Form
    {
        private const double StepMs = 10.0;
        private const int MaxStepsPerTick = 60;

        private readonly LadderScanEngine engine = new LadderScanEngine();
        private readonly ConveyorProcess plant = new ConveyorProcess();
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Timer ticker = new Timer();
        private readonly List<SimBitRef> watched = new List<SimBitRef>();
        private readonly List<string> watchedNames = new List<string>();

        private ConveyorSynoptic synoptic;
        private RungStrip rungStrip;
        private BufferedListView ioList;
        private TextBox processBox;
        private TextBox scanBox;
        private TextBox programBox;
        private Label bannerLabel;
        private Label statusLabel;
        private Button runButton;
        private Button stopButton;
        private ComboBox speedCombo;

        private bool usingSample;
        private double accumulator;
        private double speedFactor = 1.0;
        private long lastScanCount;
        private double lastRateMs;
        private int scansPerSecond;
        private int refreshCounter;

        public LadderSimulatorForm()
        {
            Text = "Simulação de processo - OpenLadder Studio";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 720);
            MinimumSize = new Size(940, 640);
            BackColor = SimulatorTheme.Shell;
            ForeColor = SimulatorTheme.Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildLayout();
            BindFieldInputs();
            LoadSampleProgram();

            ticker.Interval = 30;
            ticker.Tick += OnTick;
        }

        /// <summary>
        /// Carrega um programa no PLC virtual. O shell usa este ponto para enviar o projeto aberto no editor.
        /// </summary>
        public void LoadProgram(UniversalLadderProgram program)
        {
            usingSample = false;
            Apply(program);
        }

        private void LoadSampleProgram()
        {
            usingSample = true;
            Apply(SimulationSamples.BuildConveyorProgram());
        }

        private void Apply(UniversalLadderProgram program)
        {
            engine.Load(program);
            rungStrip.Bind(engine);
            ResetRun();
            programBox.Text = engine.DescribeProgram() + "\r\n\r\n" + DescribePlant();
            statusLabel.Text = "Programa carregado: " + engine.ProgramName;
        }

        /// <summary>
        /// Descreve a planta conectada. Um programa vindo do editor pode usar endereços que
        /// esta planta não aciona, e nesse caso os pontos ficam disponíveis apenas por forçamento.
        /// </summary>
        private string DescribePlant()
        {
            StringBuilder text = new StringBuilder();
            text.Append("Planta conectada: ").Append(plant.DisplayName).Append("\r\n");
            text.Append(plant.Description).Append("\r\n\r\n");

            IList<SimulatedIoPoint> points = plant.Points;
            for (int i = 0; i < points.Count; i++)
            {
                string origin = points[i].Direction == SimIoDirection.PlcOutput ? "comandado pelo PLC"
                    : points[i].DrivenByProcess ? "escrito pela planta" : "botoeira de campo";
                text.Append("  ").Append(points[i].Address).Append("  ").Append(points[i].Name).Append("  (").Append(origin).Append(")\r\n");
            }

            if (usingSample)
            {
                text.Append("\r\nLógica do exemplo:\r\n");
                text.Append(SimulationSamples.DescribeConveyorProgram());
            }
            else
            {
                text.Append("\r\nEste programa veio do editor. Endereços fora da lista acima só mudam por forçamento.");
            }

            return text.ToString();
        }

        private void BuildLayout()
        {
            bannerLabel = new Label();
            bannerLabel.Dock = DockStyle.Top;
            bannerLabel.Height = 30;
            bannerLabel.TextAlign = ContentAlignment.MiddleLeft;
            bannerLabel.Padding = new Padding(12, 0, 0, 0);
            bannerLabel.BackColor = Color.FromArgb(58, 48, 28);
            bannerLabel.ForeColor = SimulatorTheme.Warning;
            bannerLabel.Text = "SIMULAÇÃO — PLC virtual do OpenLadder Studio. Nenhuma saída física é acionada e nenhum equipamento é comandado.";

            Panel toolbar = BuildToolbar();
            Panel left = BuildLeftPanel();
            Panel right = BuildRightPanel();

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = SimulatorTheme.Shell;
            body.Padding = new Padding(0);
            body.Controls.Add(right);
            body.Controls.Add(left);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Height = 26;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Padding = new Padding(12, 0, 0, 0);
            statusLabel.BackColor = SimulatorTheme.Chrome;
            statusLabel.ForeColor = SimulatorTheme.Muted;
            statusLabel.Text = "Pronto.";

            // Ancoragem resolve do ultimo filho para o primeiro: o painel Fill precisa
            // ficar a frente para ocupar apenas o espaco que sobra das barras.
            Controls.Add(body);
            body.BringToFront();
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
            Controls.Add(bannerLabel);
        }

        private Panel BuildToolbar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 52;
            bar.BackColor = SimulatorTheme.Chrome;

            runButton = ActionButton("Iniciar", 12, SimulatorTheme.Accent);
            runButton.Click += delegate { Start(); };
            bar.Controls.Add(runButton);

            stopButton = ActionButton("Parar", 108, SimulatorTheme.Error);
            stopButton.Enabled = false;
            stopButton.Click += delegate { Stop(); };
            bar.Controls.Add(stopButton);

            Button stepButton = ActionButton("Passo", 204, SimulatorTheme.Info);
            stepButton.Click += delegate { SingleStep(); };
            bar.Controls.Add(stepButton);

            Button resetButton = ActionButton("Reiniciar", 300, SimulatorTheme.Warning);
            resetButton.Click += delegate { ResetRun(); };
            bar.Controls.Add(resetButton);

            Label speedLabel = new Label();
            speedLabel.Text = "Velocidade:";
            speedLabel.AutoSize = true;
            speedLabel.ForeColor = SimulatorTheme.Muted;
            speedLabel.Location = new Point(410, 18);
            bar.Controls.Add(speedLabel);

            speedCombo = new ComboBox();
            speedCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            speedCombo.Items.AddRange(new object[] { "1x (tempo real)", "2x", "5x" });
            speedCombo.SelectedIndex = 0;
            speedCombo.Location = new Point(488, 14);
            speedCombo.Size = new Size(140, 24);
            speedCombo.FlatStyle = FlatStyle.Flat;
            speedCombo.BackColor = SimulatorTheme.Panel;
            speedCombo.ForeColor = SimulatorTheme.Fore;
            speedCombo.SelectedIndexChanged += delegate
            {
                if (speedCombo.SelectedIndex == 1) speedFactor = 2.0;
                else if (speedCombo.SelectedIndex == 2) speedFactor = 5.0;
                else speedFactor = 1.0;
            };
            bar.Controls.Add(speedCombo);

            return bar;
        }

        private Button ActionButton(string text, int x, Color accent)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, 12);
            button.Size = new Size(88, 28);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = accent;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = SimulatorTheme.Panel;
            button.ForeColor = SimulatorTheme.Fore;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private Panel BuildLeftPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Left;
            panel.Width = 396;
            panel.BackColor = SimulatorTheme.Shell;
            panel.Padding = new Padding(12, 12, 6, 12);

            Panel faults = BuildFaultPanel();
            Panel buttons = BuildForcePanel();
            Panel field = BuildFieldPanel();

            ioList = new BufferedListView();
            ioList.Dock = DockStyle.Fill;
            ioList.View = View.Details;
            ioList.FullRowSelect = true;
            ioList.HideSelection = false;
            ioList.GridLines = false;
            ioList.BorderStyle = BorderStyle.FixedSingle;
            ioList.BackColor = SimulatorTheme.Panel;
            ioList.ForeColor = SimulatorTheme.Fore;
            ioList.Font = new Font("Consolas", 9.0f);
            ioList.Columns.Add("Endereço", 78);
            ioList.Columns.Add("Ponto", 178);
            ioList.Columns.Add("Valor", 52);
            ioList.Columns.Add("Forçado", 66);

            panel.Controls.Add(ioList);
            panel.Controls.Add(buttons);
            panel.Controls.Add(field);
            panel.Controls.Add(faults);
            panel.Controls.Add(SectionLabel("Tabela de I/O e forçamento"));
            return panel;
        }

        private Panel BuildFieldPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Bottom;
            panel.Height = 84;
            panel.BackColor = SimulatorTheme.Shell;

            Label caption = new Label();
            caption.Text = "BOTOEIRAS DE CAMPO (mantêm enquanto pressionadas)";
            caption.AutoSize = true;
            caption.ForeColor = SimulatorTheme.Muted;
            caption.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            caption.Location = new Point(0, 6);
            panel.Controls.Add(caption);

            int x = 0;
            IList<SimulatedIoPoint> points = plant.Points;
            for (int i = 0; i < points.Count; i++)
            {
                SimulatedIoPoint point = points[i];
                if (point.Direction != SimIoDirection.PlcInput || point.DrivenByProcess) continue;

                SimBitRef bit;
                if (!SimAddress.TryParseBit(point.Address, out bit)) continue;

                Button button = MomentaryButton(point.Name + "  " + point.Address, bit);
                button.Location = new Point(x, 28);
                panel.Controls.Add(button);
                x += button.Width + 8;
            }

            return panel;
        }

        private Button MomentaryButton(string text, SimBitRef bit)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(178, 30);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = SimulatorTheme.Border;
            button.BackColor = SimulatorTheme.Panel;
            button.ForeColor = SimulatorTheme.Fore;
            button.UseVisualStyleBackColor = false;

            SimBitRef target = bit;
            button.MouseDown += delegate { engine.Field.Set(target, true); };
            button.MouseUp += delegate { engine.Field.Set(target, false); };
            button.MouseLeave += delegate { engine.Field.Set(target, false); };

            // Acionamento por teclado, para manter a botoeira acessível sem o mouse.
            button.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) engine.Field.Set(target, true);
            };
            button.KeyUp += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) engine.Field.Set(target, false);
            };

            return button;
        }

        private Panel BuildForcePanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Bottom;
            panel.Height = 40;
            panel.BackColor = SimulatorTheme.Shell;

            Button forceOn = SmallButton("Forçar 1", 0);
            forceOn.Click += delegate { ForceSelected(true); };
            panel.Controls.Add(forceOn);

            Button forceOff = SmallButton("Forçar 0", 92);
            forceOff.Click += delegate { ForceSelected(false); };
            panel.Controls.Add(forceOff);

            Button release = SmallButton("Liberar", 184);
            release.Click += delegate { ReleaseSelected(); };
            panel.Controls.Add(release);

            Button releaseAll = SmallButton("Liberar tudo", 276);
            releaseAll.Click += delegate
            {
                engine.Forces.ReleaseAll();
                statusLabel.Text = "Todos os forçamentos foram liberados.";
                RefreshIoList();
            };
            panel.Controls.Add(releaseAll);

            return panel;
        }

        private Button SmallButton(string text, int x)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, 6);
            button.Size = new Size(86, 26);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = SimulatorTheme.Border;
            button.BackColor = SimulatorTheme.Panel;
            button.ForeColor = SimulatorTheme.Fore;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private Panel BuildFaultPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Bottom;
            panel.Height = 108;
            panel.BackColor = SimulatorTheme.Shell;

            Label caption = new Label();
            caption.Text = "FALHAS INJETÁVEIS NA PLANTA";
            caption.AutoSize = true;
            caption.ForeColor = SimulatorTheme.Muted;
            caption.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            caption.Location = new Point(0, 6);
            panel.Controls.Add(caption);

            IList<SimulatedFault> faults = plant.Faults;
            for (int i = 0; i < faults.Count; i++)
            {
                SimulatedFault fault = faults[i];
                CheckBox check = new CheckBox();
                check.Text = fault.Name;
                check.AutoSize = true;
                check.ForeColor = SimulatorTheme.Fore;
                check.Location = new Point(2, 28 + (i * 24));
                check.CheckedChanged += delegate
                {
                    fault.Active = check.Checked;
                    statusLabel.Text = (check.Checked ? "Falha injetada: " : "Falha removida: ") + fault.Name + ". " + fault.Description;
                };
                panel.Controls.Add(check);
            }

            return panel;
        }

        private Panel BuildRightPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = SimulatorTheme.Shell;
            panel.Padding = new Padding(6, 12, 12, 12);

            programBox = ReadOnlyBox();
            programBox.Dock = DockStyle.Fill;

            Panel programPanel = new Panel();
            programPanel.Dock = DockStyle.Fill;
            programPanel.Controls.Add(programBox);
            programPanel.Controls.Add(SectionLabel("Programa carregado"));

            Panel metrics = new Panel();
            metrics.Dock = DockStyle.Top;
            metrics.Height = 164;

            scanBox = ReadOnlyBox();
            scanBox.Dock = DockStyle.Right;
            scanBox.Width = 300;

            processBox = ReadOnlyBox();
            processBox.Dock = DockStyle.Fill;

            Panel metricsBody = new Panel();
            metricsBody.Dock = DockStyle.Fill;
            metricsBody.Controls.Add(processBox);
            metricsBody.Controls.Add(scanBox);

            metrics.Controls.Add(metricsBody);
            metrics.Controls.Add(SectionLabel("Estado da planta e da varredura"));

            rungStrip = new RungStrip();
            rungStrip.Dock = DockStyle.Top;
            rungStrip.Height = 26;

            Panel rungPanel = new Panel();
            rungPanel.Dock = DockStyle.Top;
            rungPanel.Height = 48;
            rungPanel.Controls.Add(rungStrip);
            rungPanel.Controls.Add(SectionLabel("Energização dos rungs"));

            synoptic = new ConveyorSynoptic();
            synoptic.Dock = DockStyle.Top;
            synoptic.Height = 250;
            synoptic.Bind(plant, engine.Image);

            panel.Controls.Add(programPanel);
            panel.Controls.Add(metrics);
            panel.Controls.Add(rungPanel);
            panel.Controls.Add(synoptic);
            return panel;
        }

        private TextBox ReadOnlyBox()
        {
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = SimulatorTheme.Panel;
            box.ForeColor = SimulatorTheme.Fore;
            box.Font = new Font("Consolas", 9.0f);
            return box;
        }

        private Label SectionLabel(string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Top;
            label.Height = 22;
            label.Text = text.ToUpperInvariant();
            label.ForeColor = SimulatorTheme.Muted;
            label.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            return label;
        }

        /// <summary>
        /// Registra as entradas que nenhuma planta escreve. Sem esse registro, liberar um
        /// forçamento deixaria a entrada congelada no último valor.
        /// </summary>
        private void BindFieldInputs()
        {
            engine.Field.Clear();
            watched.Clear();
            watchedNames.Clear();

            IList<SimulatedIoPoint> points = plant.Points;
            for (int i = 0; i < points.Count; i++)
            {
                SimBitRef bit;
                if (!SimAddress.TryParseBit(points[i].Address, out bit)) continue;

                if (points[i].Direction == SimIoDirection.PlcInput && !points[i].DrivenByProcess)
                    engine.Field.Set(bit, false);

                watched.Add(bit);
                watchedNames.Add(points[i].Name);
            }

            AddWatch("C0001", "Marcha selada");
            AddWatch("V0001", "Horímetro de marcha");
            AddWatch("V0002", "Contador de caixas");
            AddWatch("SC004", "Pulso de 1 s");

            ioList.Items.Clear();
            for (int i = 0; i < watched.Count; i++)
            {
                ListViewItem item = new ListViewItem(SimAddress.Format(watched[i]));
                item.SubItems.Add(watchedNames[i]);
                item.SubItems.Add("0");
                item.SubItems.Add(string.Empty);
                item.Tag = watched[i];
                ioList.Items.Add(item);
            }
        }

        private void AddWatch(string address, string name)
        {
            SimBitRef bit;
            if (!SimAddress.TryParseBit(address, out bit)) return;
            watched.Add(bit);
            watchedNames.Add(name);
        }

        private void Start()
        {
            if (ticker.Enabled) return;
            accumulator = 0.0;
            clock.Reset();
            clock.Start();
            ticker.Start();
            runButton.Enabled = false;
            stopButton.Enabled = true;
            statusLabel.Text = "Simulação em execução.";
        }

        private void Stop()
        {
            ticker.Stop();
            clock.Stop();
            runButton.Enabled = true;
            stopButton.Enabled = false;
            statusLabel.Text = "Simulação parada. Use Passo para avançar uma varredura por vez.";
        }

        private void SingleStep()
        {
            if (ticker.Enabled) Stop();
            ExecuteStep();
            RefreshAll();
            statusLabel.Text = "Varredura " + engine.ScanCount.ToString(CultureInfo.InvariantCulture) + " executada.";
        }

        private void ResetRun()
        {
            bool wasRunning = ticker.Enabled;
            if (wasRunning) Stop();

            engine.Reset();
            engine.Forces.ReleaseAll();
            engine.Image.ClearAll();
            plant.Reset();

            accumulator = 0.0;
            lastScanCount = 0;
            lastRateMs = 0.0;
            scansPerSecond = 0;

            RefreshAll();
            statusLabel.Text = "Simulação reiniciada.";
        }

        private void ExecuteStep()
        {
            plant.Step(StepMs / 1000.0, engine.Image);
            engine.Execute(StepMs);
            synoptic.Advance(StepMs / 1000.0);
        }

        private void OnTick(object sender, EventArgs e)
        {
            double elapsed = clock.Elapsed.TotalMilliseconds;
            clock.Reset();
            clock.Start();

            accumulator += elapsed * speedFactor;

            int steps = 0;
            while (accumulator >= StepMs && steps < MaxStepsPerTick)
            {
                ExecuteStep();
                accumulator -= StepMs;
                steps++;
            }

            // Se a máquina não acompanha o passo pedido, descarta o atraso em vez de acumular.
            if (accumulator > StepMs * 4.0) accumulator = 0.0;

            lastRateMs += elapsed;
            if (lastRateMs >= 1000.0)
            {
                scansPerSecond = (int)(engine.ScanCount - lastScanCount);
                lastScanCount = engine.ScanCount;
                lastRateMs = 0.0;
            }

            synoptic.Invalidate();
            rungStrip.Invalidate();

            refreshCounter++;
            if (refreshCounter >= 3)
            {
                refreshCounter = 0;
                RefreshIoList();
                RefreshMetrics();
            }
        }

        private void RefreshAll()
        {
            RefreshIoList();
            RefreshMetrics();
            synoptic.Invalidate();
            rungStrip.Invalidate();
        }

        private void RefreshIoList()
        {
            for (int i = 0; i < ioList.Items.Count && i < watched.Count; i++)
            {
                SimBitRef bit = watched[i];
                ListViewItem item = ioList.Items[i];

                string value = bit.Area == SimBitArea.Variable
                    ? engine.Image.GetVariableValue(bit.Index).ToString(CultureInfo.InvariantCulture)
                    : (engine.Image.GetBit(bit) ? "1" : "0");

                bool forced = engine.Forces.IsForced(bit);
                string forcedText = forced ? "sim" : string.Empty;

                if (item.SubItems[2].Text != value) item.SubItems[2].Text = value;
                if (item.SubItems[3].Text != forcedText) item.SubItems[3].Text = forcedText;

                Color colour = forced ? SimulatorTheme.Warning
                    : engine.Image.GetBit(bit) ? SimulatorTheme.Accent : SimulatorTheme.Fore;
                if (item.ForeColor != colour) item.ForeColor = colour;
            }
        }

        private void RefreshMetrics()
        {
            processBox.Text = plant.StateSummary();

            string text = "Varreduras: " + engine.ScanCount.ToString(CultureInfo.InvariantCulture) + "\r\n";
            text += "Tempo simulado: " + (engine.TotalMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s\r\n";
            text += "Período de varredura: " + StepMs.ToString("0", CultureInfo.InvariantCulture) + " ms\r\n";
            text += "Varreduras por segundo: " + scansPerSecond.ToString(CultureInfo.InvariantCulture) + "\r\n";
            text += "Rungs no programa: " + engine.RungCount.ToString(CultureInfo.InvariantCulture) + "\r\n";
            text += "Forçamentos ativos: " + engine.Forces.Count.ToString(CultureInfo.InvariantCulture);
            scanBox.Text = text;
        }

        private void ForceSelected(bool value)
        {
            if (ioList.SelectedItems.Count == 0)
            {
                statusLabel.Text = "Selecione um ponto na tabela antes de forçar.";
                return;
            }

            for (int i = 0; i < ioList.SelectedItems.Count; i++)
            {
                SimBitRef bit = (SimBitRef)ioList.SelectedItems[i].Tag;
                engine.Forces.Force(bit, value);
            }

            statusLabel.Text = "Forçamento aplicado em " + ioList.SelectedItems.Count.ToString(CultureInfo.InvariantCulture) + " ponto(s).";
            RefreshIoList();
        }

        private void ReleaseSelected()
        {
            for (int i = 0; i < ioList.SelectedItems.Count; i++)
            {
                SimBitRef bit = (SimBitRef)ioList.SelectedItems[i].Tag;
                engine.Forces.Release(bit);
            }

            statusLabel.Text = "Forçamento liberado.";
            RefreshIoList();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ticker.Stop();
            base.OnFormClosed(e);
        }
    }
}
