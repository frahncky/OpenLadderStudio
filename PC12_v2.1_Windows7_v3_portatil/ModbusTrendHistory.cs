using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ModernPC12
{
    internal sealed class ModbusTrendSample
    {
        public DateTime Timestamp;
        public double Value;
    }

    internal sealed class ModbusTrendSignal
    {
        public ModbusFunction Function;
        public int Address;
        public string DisplayName = string.Empty;
        public bool IsBit;
        public readonly List<ModbusTrendSample> Samples = new List<ModbusTrendSample>();

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal sealed class ModbusTrendHistory
    {
        public const int MaxSignals = 8;
        public const int MaxSamplesPerSignal = 600;

        private readonly List<ModbusTrendSignal> signals = new List<ModbusTrendSignal>();

        public IList<ModbusTrendSignal> Signals
        {
            get { return signals.AsReadOnly(); }
        }

        public bool Track(ModbusFunction function, int address, string displayName, bool isBit, out string message)
        {
            for (int i = 0; i < signals.Count; i++)
            {
                ModbusTrendSignal existing = signals[i];
                if (existing.Function == function && existing.Address == address)
                {
                    message = "O sinal já está sendo rastreado.";
                    return true;
                }
            }

            if (signals.Count >= MaxSignals)
            {
                message = "O limite atual é de " + MaxSignals.ToString(CultureInfo.InvariantCulture) + " sinais simultâneos.";
                return false;
            }

            ModbusTrendSignal signal = new ModbusTrendSignal();
            signal.Function = function;
            signal.Address = address;
            signal.DisplayName = string.IsNullOrEmpty(displayName) ? "Endereço " + address.ToString(CultureInfo.InvariantCulture) : displayName;
            signal.IsBit = isBit;
            signals.Add(signal);
            message = "Sinal adicionado ao histórico: " + signal.DisplayName + ".";
            return true;
        }

        public void Capture(ModbusFunction function, int startAddress, bool[] bits, ushort[] registers, DateTime timestamp)
        {
            int bitCount = bits == null ? 0 : bits.Length;
            int registerCount = registers == null ? 0 : registers.Length;

            for (int i = 0; i < signals.Count; i++)
            {
                ModbusTrendSignal signal = signals[i];
                if (signal.Function != function) continue;

                int offset = signal.Address - startAddress;
                double value;
                if (signal.IsBit)
                {
                    if (offset < 0 || offset >= bitCount) continue;
                    value = bits[offset] ? 1.0 : 0.0;
                }
                else
                {
                    if (offset < 0 || offset >= registerCount) continue;
                    value = registers[offset];
                }

                ModbusTrendSample sample = new ModbusTrendSample();
                sample.Timestamp = timestamp;
                sample.Value = value;
                signal.Samples.Add(sample);
                while (signal.Samples.Count > MaxSamplesPerSignal)
                    signal.Samples.RemoveAt(0);
            }
        }

        public void Remove(ModbusTrendSignal signal)
        {
            if (signal != null) signals.Remove(signal);
        }

        public void ClearSamples(ModbusTrendSignal signal)
        {
            if (signal != null) signal.Samples.Clear();
        }

        public void ClearAll()
        {
            signals.Clear();
        }

        public void ExportCsv(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("Arquivo de destino inválido.", "fileName");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("timestamp;signal;function;address;value");
            for (int i = 0; i < signals.Count; i++)
            {
                ModbusTrendSignal signal = signals[i];
                for (int j = 0; j < signal.Samples.Count; j++)
                {
                    ModbusTrendSample sample = signal.Samples[j];
                    sb.Append(sample.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                    sb.Append(';');
                    sb.Append(EscapeCsv(signal.DisplayName));
                    sb.Append(';');
                    sb.Append(((int)signal.Function).ToString(CultureInfo.InvariantCulture));
                    sb.Append(';');
                    sb.Append(signal.Address.ToString(CultureInfo.InvariantCulture));
                    sb.Append(';');
                    sb.Append(sample.Value.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine();
                }
            }
            File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
        }

        private string EscapeCsv(string text)
        {
            string value = text ?? string.Empty;
            if (value.IndexOfAny(new char[] { ';', '"', '\r', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    internal sealed class ModbusTrendForm : Form
    {
        private readonly Color Shell = Color.FromArgb(29, 31, 34);
        private readonly Color Chrome = Color.FromArgb(37, 39, 43);
        private readonly Color PanelColor = Color.FromArgb(47, 50, 55);
        private readonly Color Border = Color.FromArgb(61, 64, 69);
        private readonly Color Accent = Color.FromArgb(45, 170, 107);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(150, 157, 164);

        private readonly ModbusTrendHistory history;
        private ComboBox signalCombo;
        private Label statsLabel;
        private ModbusTrendCanvas canvas;
        private Timer refreshTimer;

        public ModbusTrendForm(ModbusTrendHistory history)
        {
            if (history == null) throw new ArgumentNullException("history");
            this.history = history;

            Text = "OpenLadder Studio - Tendências Modbus";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(980, 620);
            MinimumSize = new Size(760, 500);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);

            BuildUi();
            RefreshSignalList();

            refreshTimer = new Timer();
            refreshTimer.Interval = 500;
            refreshTimer.Tick += delegate { RefreshView(); };
            refreshTimer.Start();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = NewLabel("Tendências em tempo real", 17.0f, true, Fore);
            title.Location = new Point(18, 10);
            header.Controls.Add(title);

            Label sub = NewLabel("Histórico em memória dos sinais rastreados pelo monitor Modbus. Máximo: 8 sinais, 600 amostras por sinal.", 8.2f, false, Muted);
            sub.Location = new Point(20, 42);
            header.Controls.Add(sub);

            signalCombo = new ComboBox();
            signalCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            signalCombo.Location = new Point(20, 62);
            signalCombo.Size = new Size(430, 26);
            signalCombo.BackColor = PanelColor;
            signalCombo.ForeColor = Fore;
            signalCombo.SelectedIndexChanged += delegate { RefreshView(); };
            header.Controls.Add(signalCombo);

            statsLabel = NewLabel("Nenhum sinal rastreado.", 8.3f, false, Muted);
            statsLabel.Location = new Point(470, 66);
            statsLabel.MaximumSize = new Size(470, 24);
            header.Controls.Add(statsLabel);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 58;
            footer.BackColor = Chrome;
            Controls.Add(footer);

            Button export = ActionButton("EXPORTAR CSV", 14, 11, 130, Accent);
            export.Click += ExportCsv;
            footer.Controls.Add(export);

            Button clear = ActionButton("LIMPAR AMOSTRAS", 152, 11, 150, PanelColor);
            clear.Click += ClearSelectedSamples;
            footer.Controls.Add(clear);

            Button remove = ActionButton("REMOVER SINAL", 310, 11, 140, PanelColor);
            remove.Click += RemoveSelectedSignal;
            footer.Controls.Add(remove);

            Button close = ActionButton("FECHAR", 840, 11, 110, PanelColor);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Click += delegate { Close(); };
            footer.Controls.Add(close);
            footer.Resize += delegate { close.Left = footer.ClientSize.Width - close.Width - 14; };

            canvas = new ModbusTrendCanvas();
            canvas.Dock = DockStyle.Fill;
            canvas.BackColor = Shell;
            Controls.Add(canvas);

            canvas.BringToFront();
            header.BringToFront();
            footer.BringToFront();
        }

        private void RefreshSignalList()
        {
            ModbusTrendSignal selected = signalCombo == null ? null : signalCombo.SelectedItem as ModbusTrendSignal;
            signalCombo.Items.Clear();
            for (int i = 0; i < history.Signals.Count; i++)
                signalCombo.Items.Add(history.Signals[i]);

            if (selected != null && signalCombo.Items.Contains(selected))
                signalCombo.SelectedItem = selected;
            else if (signalCombo.Items.Count > 0)
                signalCombo.SelectedIndex = 0;

            RefreshView();
        }

        public void NotifySignalsChanged()
        {
            RefreshSignalList();
        }

        private void RefreshView()
        {
            if (signalCombo == null || canvas == null) return;
            ModbusTrendSignal signal = signalCombo.SelectedItem as ModbusTrendSignal;
            canvas.Signal = signal;

            if (signal == null || signal.Samples.Count == 0)
            {
                statsLabel.Text = signal == null ? "Nenhum sinal rastreado." : "Aguardando amostras para " + signal.DisplayName + ".";
                statsLabel.ForeColor = Muted;
                canvas.Invalidate();
                return;
            }

            double min = signal.Samples[0].Value;
            double max = min;
            double last = min;
            for (int i = 0; i < signal.Samples.Count; i++)
            {
                double v = signal.Samples[i].Value;
                if (v < min) min = v;
                if (v > max) max = v;
                last = v;
            }

            statsLabel.Text = "Atual: " + last.ToString("0.###", CultureInfo.InvariantCulture) +
                              "   Mín: " + min.ToString("0.###", CultureInfo.InvariantCulture) +
                              "   Máx: " + max.ToString("0.###", CultureInfo.InvariantCulture) +
                              "   Amostras: " + signal.Samples.Count.ToString(CultureInfo.InvariantCulture);
            statsLabel.ForeColor = Accent;
            canvas.Invalidate();
        }

        private void ExportCsv(object sender, EventArgs e)
        {
            if (history.Signals.Count == 0)
            {
                MessageBox.Show(this, "Nenhum sinal está sendo rastreado.", "Tendências", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Exportar histórico de tendências";
                dialog.Filter = "Arquivo CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*";
                dialog.FileName = "OpenLadder-Trends-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    history.ExportCsv(dialog.FileName);
                    MessageBox.Show(this, "Histórico exportado com sucesso.", "Tendências", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Não foi possível exportar o histórico.\r\n\r\n" + ex.Message, "Tendências", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ClearSelectedSamples(object sender, EventArgs e)
        {
            ModbusTrendSignal signal = signalCombo.SelectedItem as ModbusTrendSignal;
            if (signal == null) return;
            history.ClearSamples(signal);
            RefreshView();
        }

        private void RemoveSelectedSignal(object sender, EventArgs e)
        {
            ModbusTrendSignal signal = signalCombo.SelectedItem as ModbusTrendSignal;
            if (signal == null) return;
            history.Remove(signal);
            RefreshSignalList();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (refreshTimer != null) refreshTimer.Stop();
            base.OnFormClosed(e);
        }

        private Label NewLabel(string text, float size, bool bold, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = color;
            l.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            return l;
        }

        private Button ActionButton(string text, int left, int top, int width, Color back)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 36);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = back == Accent ? Accent : Border;
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }
    }

    internal sealed class ModbusTrendCanvas : Control
    {
        private readonly Color GridColor = Color.FromArgb(56, 59, 64);
        private readonly Color AxisColor = Color.FromArgb(120, 126, 132);
        private readonly Color TextColor = Color.FromArgb(188, 194, 200);
        private readonly Color TrendColor = Color.FromArgb(45, 170, 107);
        private readonly Color EmptyColor = Color.FromArgb(120, 126, 132);

        public ModbusTrendSignal Signal;

        public ModbusTrendCanvas()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Font = new Font("Segoe UI", 8.0f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle plot = new Rectangle(66, 24, Math.Max(10, ClientSize.Width - 90), Math.Max(10, ClientSize.Height - 70));
            if (plot.Width <= 10 || plot.Height <= 10) return;

            using (Pen gridPen = new Pen(GridColor))
            using (Pen axisPen = new Pen(AxisColor))
            using (Brush textBrush = new SolidBrush(TextColor))
            {
                for (int i = 0; i <= 5; i++)
                {
                    int y = plot.Top + (plot.Height * i / 5);
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                }
                for (int i = 0; i <= 6; i++)
                {
                    int x = plot.Left + (plot.Width * i / 6);
                    g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                }
                g.DrawRectangle(axisPen, plot);

                if (Signal == null || Signal.Samples.Count == 0)
                {
                    using (Brush emptyBrush = new SolidBrush(EmptyColor))
                        g.DrawString("Aguardando amostras...", Font, emptyBrush, plot.Left + 12, plot.Top + 12);
                    return;
                }

                double min = Signal.Samples[0].Value;
                double max = min;
                for (int i = 1; i < Signal.Samples.Count; i++)
                {
                    double v = Signal.Samples[i].Value;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                if (Signal.IsBit)
                {
                    min = 0.0;
                    max = 1.0;
                }
                else if (Math.Abs(max - min) < 0.000001)
                {
                    double pad = Math.Abs(max) * 0.05;
                    if (pad < 1.0) pad = 1.0;
                    min -= pad;
                    max += pad;
                }
                else
                {
                    double pad = (max - min) * 0.08;
                    min -= pad;
                    max += pad;
                }

                g.DrawString(max.ToString("0.###", CultureInfo.InvariantCulture), Font, textBrush, 8, plot.Top - 5);
                g.DrawString(min.ToString("0.###", CultureInfo.InvariantCulture), Font, textBrush, 8, plot.Bottom - 12);

                int count = Signal.Samples.Count;
                PointF[] points = new PointF[count];
                double range = max - min;
                if (range <= 0.0) range = 1.0;

                for (int i = 0; i < count; i++)
                {
                    float x = count == 1 ? plot.Left : plot.Left + (float)i * plot.Width / (float)(count - 1);
                    double normalized = (Signal.Samples[i].Value - min) / range;
                    float y = plot.Bottom - (float)(normalized * plot.Height);
                    points[i] = new PointF(x, y);
                }

                using (Pen trendPen = new Pen(TrendColor, 2.0f))
                using (Brush lastBrush = new SolidBrush(TrendColor))
                {
                    if (points.Length > 1) g.DrawLines(trendPen, points);
                    PointF last = points[points.Length - 1];
                    g.FillEllipse(lastBrush, last.X - 3, last.Y - 3, 6, 6);
                }

                DateTime firstTime = Signal.Samples[0].Timestamp;
                DateTime lastTime = Signal.Samples[Signal.Samples.Count - 1].Timestamp;
                g.DrawString(firstTime.ToString("HH:mm:ss"), Font, textBrush, plot.Left, plot.Bottom + 8);
                string lastText = lastTime.ToString("HH:mm:ss");
                SizeF lastSize = g.MeasureString(lastText, Font);
                g.DrawString(lastText, Font, textBrush, plot.Right - lastSize.Width, plot.Bottom + 8);
                g.DrawString(Signal.DisplayName, Font, textBrush, plot.Left + 8, plot.Top + 8);
            }
        }
    }
}
