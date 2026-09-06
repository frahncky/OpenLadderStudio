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
        public static readonly Color Dark = Color.FromArgb(22, 24, 27);

        public static Color For(SimTone tone)
        {
            switch (tone)
            {
                case SimTone.Structure: return Metal;
                case SimTone.Active: return Accent;
                case SimTone.Info: return Info;
                case SimTone.Warning: return Warning;
                case SimTone.Danger: return Error;
                case SimTone.Cargo: return Cargo;
                case SimTone.Muted: return Muted;
                case SimTone.Dark: return Dark;
            }
            return Fore;
        }

        /// <summary>Cor de texto legível sobre um preenchimento do tom indicado.</summary>
        public static Color OnFill(SimTone tone)
        {
            if (tone == SimTone.Active || tone == SimTone.Warning || tone == SimTone.Info || tone == SimTone.Cargo) return Shell;
            return Fore;
        }
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
                    using (SolidBrush brush = new SolidBrush(shape.On ? SimulatorTheme.For(shape.Fill) : Color.FromArgb(64, 68, 74)))
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
            using (Pen pen = new Pen(Color.FromArgb(120, 30, 33, 37), Math.Max(1.0f, scale * 2.0f)))
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
        private readonly IList<ISimulatedProcess> plants = SimulatedProcessCatalog.Create();
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Timer ticker = new Timer();
        private readonly List<SimBitRef> watched = new List<SimBitRef>();
        private readonly List<string> watchedNames = new List<string>();

        private ISimulatedProcess plant;
        private ProcessSynoptic synoptic;
        private RungStrip rungStrip;
        private BufferedListView ioList;
        private TextBox processBox;
        private TextBox scanBox;
        private TextBox programBox;
        private Label statusLabel;
        private Button runButton;
        private Button stopButton;
        private ComboBox speedCombo;
        private ComboBox plantCombo;
        private Panel fieldPanel;
        private Panel faultPanel;

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
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(980, 660);
            BackColor = SimulatorTheme.Shell;
            ForeColor = SimulatorTheme.Fore;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            plant = FindPlant(SimulatedProcessCatalog.DefaultId);

            BuildLayout();
            SelectPlant(plant);

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

        private ISimulatedProcess FindPlant(string id)
        {
            for (int i = 0; i < plants.Count; i++)
                if (plants[i].Id == id) return plants[i];
            return plants[0];
        }

        private void SelectPlant(ISimulatedProcess selected)
        {
            bool wasRunning = ticker.Enabled;
            if (wasRunning) Stop();

            plant = selected;
            synoptic.Bind(plant, engine.Image);
            BindFieldInputs();
            PopulateFieldPanel();
            PopulateFaultPanel();

            usingSample = true;
            Apply(plant.BuildSampleProgram());
            statusLabel.Text = "Planta selecionada: " + plant.DisplayName + ".";
        }

        private void Apply(UniversalLadderProgram program)
        {
            engine.Load(program);
            rungStrip.Bind(engine);
            ResetRun();
            programBox.Text = engine.DescribeProgram() + "\r\n\r\n" + DescribePlant();
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
                text.Append(plant.DescribeSampleProgram());
            }
            else
            {
                text.Append("\r\nEste programa veio do editor. Endereços fora da lista acima só mudam por forçamento.");
            }

            return text.ToString();
        }

        private void BuildLayout()
        {
            Label banner = new Label();
            banner.Dock = DockStyle.Top;
            banner.Height = 30;
            banner.TextAlign = ContentAlignment.MiddleLeft;
            banner.Padding = new Padding(12, 0, 0, 0);
            banner.BackColor = Color.FromArgb(58, 48, 28);
            banner.ForeColor = SimulatorTheme.Warning;
            banner.Text = "SIMULAÇÃO — PLC virtual do OpenLadder Studio. Nenhuma saída física é acionada e nenhum equipamento é comandado.";

            Panel toolbar = BuildToolbar();
            Panel left = BuildLeftPanel();
            Panel right = BuildRightPanel();

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = SimulatorTheme.Shell;
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

            // Ancoragem resolve do último filho para o primeiro: o painel Fill precisa
            // ficar à frente para ocupar apenas o espaço que sobra das barras.
            Controls.Add(body);
            body.BringToFront();
            Controls.Add(statusLabel);
            Controls.Add(toolbar);
            Controls.Add(banner);
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

            bar.Controls.Add(BarLabel("Planta:", 410));
            plantCombo = BarCombo(462, 250);
            for (int i = 0; i < plants.Count; i++) plantCombo.Items.Add(plants[i].DisplayName);
            plantCombo.SelectedIndex = IndexOf(plant);
            plantCombo.SelectedIndexChanged += delegate
            {
                if (plantCombo.SelectedIndex >= 0 && plantCombo.SelectedIndex < plants.Count)
                    SelectPlant(plants[plantCombo.SelectedIndex]);
            };
            bar.Controls.Add(plantCombo);

            bar.Controls.Add(BarLabel("Velocidade:", 736));
            speedCombo = BarCombo(818, 140);
            speedCombo.Items.AddRange(new object[] { "1x (tempo real)", "2x", "5x" });
            speedCombo.SelectedIndex = 0;
            speedCombo.SelectedIndexChanged += delegate
            {
                if (speedCombo.SelectedIndex == 1) speedFactor = 2.0;
                else if (speedCombo.SelectedIndex == 2) speedFactor = 5.0;
                else speedFactor = 1.0;
            };
            bar.Controls.Add(speedCombo);

            return bar;
        }

        private int IndexOf(ISimulatedProcess target)
        {
            for (int i = 0; i < plants.Count; i++)
                if (plants[i] == target) return i;
            return 0;
        }

        private Label BarLabel(string text, int x)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = SimulatorTheme.Muted;
            label.Location = new Point(x, 18);
            return label;
        }

        private ComboBox BarCombo(int x, int width)
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Location = new Point(x, 14);
            combo.Size = new Size(width, 24);
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = SimulatorTheme.Panel;
            combo.ForeColor = SimulatorTheme.Fore;
            return combo;
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
            panel.Width = 400;
            panel.BackColor = SimulatorTheme.Shell;
            panel.Padding = new Padding(12, 12, 6, 12);

            faultPanel = new Panel();
            faultPanel.Dock = DockStyle.Bottom;
            faultPanel.Height = 116;
            faultPanel.BackColor = SimulatorTheme.Shell;

            fieldPanel = new Panel();
            fieldPanel.Dock = DockStyle.Bottom;
            fieldPanel.Height = 112;
            fieldPanel.BackColor = SimulatorTheme.Shell;

            Panel buttons = BuildForcePanel();

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
            ioList.Columns.Add("Ponto", 182);
            ioList.Columns.Add("Valor", 52);
            ioList.Columns.Add("Forçado", 66);

            panel.Controls.Add(ioList);
            panel.Controls.Add(buttons);
            panel.Controls.Add(fieldPanel);
            panel.Controls.Add(faultPanel);
            panel.Controls.Add(SectionLabel("Tabela de I/O e forçamento"));
            return panel;
        }

        private void PopulateFieldPanel()
        {
            fieldPanel.Controls.Clear();

            Label caption = new Label();
            caption.Text = "BOTOEIRAS DE CAMPO (mantêm enquanto pressionadas)";
            caption.AutoSize = true;
            caption.ForeColor = SimulatorTheme.Muted;
            caption.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            caption.Location = new Point(0, 6);
            fieldPanel.Controls.Add(caption);

            int x = 0;
            int y = 28;
            IList<SimulatedIoPoint> points = plant.Points;
            for (int i = 0; i < points.Count; i++)
            {
                SimulatedIoPoint point = points[i];
                if (point.Direction != SimIoDirection.PlcInput || point.DrivenByProcess) continue;

                SimBitRef bit;
                if (!SimAddress.TryParseBit(point.Address, out bit)) continue;

                Button button = MomentaryButton(point.Name + "  " + point.Address, bit);
                if (x + button.Width > fieldPanel.Width - 12) { x = 0; y += button.Height + 6; }
                button.Location = new Point(x, y);
                fieldPanel.Controls.Add(button);
                x += button.Width + 8;
            }
        }

        private Button MomentaryButton(string text, SimBitRef bit)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(186, 30);
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

        private void PopulateFaultPanel()
        {
            faultPanel.Controls.Clear();

            Label caption = new Label();
            caption.Text = "FALHAS INJETÁVEIS NA PLANTA";
            caption.AutoSize = true;
            caption.ForeColor = SimulatorTheme.Muted;
            caption.Font = new Font("Segoe UI", 8.0f, FontStyle.Bold);
            caption.Location = new Point(0, 6);
            faultPanel.Controls.Add(caption);

            IList<SimulatedFault> faults = plant.Faults;
            for (int i = 0; i < faults.Count; i++)
            {
                SimulatedFault fault = faults[i];
                CheckBox check = new CheckBox();
                check.Text = fault.Name;
                check.AutoSize = true;
                check.Checked = fault.Active;
                check.ForeColor = SimulatorTheme.Fore;
                check.Location = new Point(2, 28 + (i * 24));
                check.CheckedChanged += delegate
                {
                    fault.Active = check.Checked;
                    statusLabel.Text = (check.Checked ? "Falha injetada: " : "Falha removida: ") + fault.Name + ". " + fault.Description;
                };
                faultPanel.Controls.Add(check);
            }
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

            scanBox = ReadOnlyBox();
            scanBox.Dock = DockStyle.Right;
            scanBox.Width = 300;

            processBox = ReadOnlyBox();
            processBox.Dock = DockStyle.Fill;

            Panel metricsBody = new Panel();
            metricsBody.Dock = DockStyle.Fill;
            metricsBody.Controls.Add(processBox);
            metricsBody.Controls.Add(scanBox);

            Panel metrics = new Panel();
            metrics.Dock = DockStyle.Top;
            metrics.Height = 178;
            metrics.Controls.Add(metricsBody);
            metrics.Controls.Add(SectionLabel("Estado da planta e da varredura"));

            rungStrip = new RungStrip();
            rungStrip.Dock = DockStyle.Top;
            rungStrip.Height = 26;

            Panel rungPanel = new Panel();
            rungPanel.Dock = DockStyle.Top;
            rungPanel.Height = 48;
            rungPanel.Controls.Add(rungStrip);
            rungPanel.Controls.Add(SectionLabel("Energização das linhas Ladder"));

            synoptic = new ProcessSynoptic();
            synoptic.Dock = DockStyle.Top;
            synoptic.Height = 260;

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

            AddWatch("C0001", "Auxiliar C0001");
            AddWatch("C0002", "Auxiliar C0002");
            AddWatch("V0001", "Temporizador V0001");
            AddWatch("V0002", "Contador V0002");
            AddWatch("SC004", "Pulso de 1 s");

            ioList.BeginUpdate();
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
            ioList.EndUpdate();
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
            if (ticker.Enabled) Stop();

            engine.Reset();
            engine.Forces.ReleaseAll();
            engine.Image.ClearAll();
            plant.Reset();

            accumulator = 0.0;
            lastScanCount = 0;
            lastRateMs = 0.0;
            scansPerSecond = 0;

            RefreshAll();
        }

        private void ExecuteStep()
        {
            plant.Step(StepMs / 1000.0, engine.Image);
            engine.Execute(StepMs);
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
            text += "Linhas no programa: " + engine.RungCount.ToString(CultureInfo.InvariantCulture) + "\r\n";
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
