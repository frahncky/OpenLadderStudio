using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Capturador de tráfego serial entre o PC12 original e o TP02.
    ///
    /// Funciona como ponte: abre a porta virtual em que o PC12 acredita estar o PLC e
    /// a porta física em que o PLC realmente está, e repassa os bytes de um lado para
    /// o outro registrando tudo. Não injeta nada, não responde nada e não altera
    /// nenhum byte: é estritamente um relé, e por isso não há risco de enviar comando
    /// desconhecido ao equipamento.
    ///
    /// Serve para obter o protocolo com semântica: como quem gera os quadros é o
    /// próprio PC12, sabe-se qual botão produziu cada troca.
    /// </summary>
    internal sealed class TP02SerialCaptureForm : Form
    {
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color Navy = Color.FromArgb(24, 42, 66);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(96, 110, 124);
        private readonly Color Accent = Color.FromArgb(45, 170, 107);

        private ComboBox pcPortCombo;
        private ComboBox plcPortCombo;
        private ComboBox baudCombo;
        private ComboBox parityCombo;
        private ComboBox dataBitsCombo;
        private ComboBox stopBitsCombo;
        private CheckBox dtrCheck;
        private CheckBox rtsCheck;
        private NumericUpDown gapBox;
        private Button startButton;
        private TextBox logBox;

        private Thread worker;
        private volatile bool stopRequested;

        private delegate void LineHandler(string line);

        public TP02SerialCaptureForm()
        {
            Text = "OpenLadder Studio - Captura serial PC12 / TP02";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1180, 780);
            MinimumSize = new Size(980, 620);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            BuildUi();
            RefreshPorts();
            FormClosing += delegate { stopRequested = true; };
        }

        private void BuildUi()
        {
            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.ReadOnly = true;
            logBox.WordWrap = false;
            logBox.Font = new Font("Consolas", 9.2f);
            logBox.BackColor = Color.FromArgb(20, 28, 36);
            logBox.ForeColor = Color.FromArgb(218, 232, 245);
            Controls.Add(logBox);
            logBox.BringToFront();

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 78;
            header.BackColor = Navy;
            Controls.Add(header);

            Label title = Text14("Captura serial PC12 / TP02", 15.0f, FontStyle.Bold, Color.White, 22, 14);
            header.Controls.Add(title);
            header.Controls.Add(Text14("Ponte passiva: repassa os bytes entre o PC12 e o PLC e registra os dois sentidos.",
                9.0f, FontStyle.Regular, Color.FromArgb(178, 198, 218), 24, 44));

            Panel setup = new Panel();
            setup.Dock = DockStyle.Top;
            setup.Height = 176;
            setup.BackColor = Color.White;
            Controls.Add(setup);

            setup.Controls.Add(Text14("Porta usada pelo PC12 (par virtual)", 8.4f, FontStyle.Regular, TextSecondary, 18, 14));
            pcPortCombo = Combo(18, 34, 150);
            setup.Controls.Add(pcPortCombo);

            setup.Controls.Add(Text14("Porta física do PLC", 8.4f, FontStyle.Regular, TextSecondary, 186, 14));
            plcPortCombo = Combo(186, 34, 150);
            setup.Controls.Add(plcPortCombo);

            Button refresh = Btn("ATUALIZAR", 354, 33, 110, false);
            refresh.Click += delegate { RefreshPorts(); };
            setup.Controls.Add(refresh);

            setup.Controls.Add(Text14("Baud", 8.4f, FontStyle.Regular, TextSecondary, 480, 14));
            baudCombo = Combo(480, 34, 100);
            baudCombo.Items.AddRange(new object[] { "38400", "19200", "9600", "4800", "2400", "1200" });
            baudCombo.SelectedItem = "19200";
            setup.Controls.Add(baudCombo);

            setup.Controls.Add(Text14("Paridade", 8.4f, FontStyle.Regular, TextSecondary, 592, 14));
            parityCombo = Combo(592, 34, 96);
            parityCombo.Items.AddRange(new object[] { "Odd", "Even", "None" });
            parityCombo.SelectedItem = "Odd";
            setup.Controls.Add(parityCombo);

            setup.Controls.Add(Text14("Bits", 8.4f, FontStyle.Regular, TextSecondary, 700, 14));
            dataBitsCombo = Combo(700, 34, 70);
            dataBitsCombo.Items.AddRange(new object[] { "8", "7" });
            dataBitsCombo.SelectedItem = "8";
            setup.Controls.Add(dataBitsCombo);

            setup.Controls.Add(Text14("Stop", 8.4f, FontStyle.Regular, TextSecondary, 782, 14));
            stopBitsCombo = Combo(782, 34, 70);
            stopBitsCombo.Items.AddRange(new object[] { "1", "2" });
            stopBitsCombo.SelectedItem = "1";
            setup.Controls.Add(stopBitsCombo);

            setup.Controls.Add(Text14("Silêncio que separa quadros (ms)", 8.4f, FontStyle.Regular, TextSecondary, 864, 14));
            gapBox = new NumericUpDown();
            gapBox.Location = new Point(864, 34);
            gapBox.Size = new Size(80, 25);
            gapBox.Minimum = 5;
            gapBox.Maximum = 500;
            gapBox.Value = 25;
            setup.Controls.Add(gapBox);

            dtrCheck = Check("DTR na porta do PLC", 480, 70);
            setup.Controls.Add(dtrCheck);
            rtsCheck = Check("RTS na porta do PLC", 640, 70);
            setup.Controls.Add(rtsCheck);

            startButton = Btn("INICIAR CAPTURA", 18, 66, 190, true);
            startButton.Click += delegate { Toggle(); };
            setup.Controls.Add(startButton);

            Button save = Btn("SALVAR CAPTURA", 218, 66, 160, false);
            save.Click += delegate { SaveCapture(); };
            setup.Controls.Add(save);

            Button clear = Btn("LIMPAR", 386, 66, 90, false);
            clear.Click += delegate { logBox.Clear(); };
            setup.Controls.Add(clear);

            setup.Controls.Add(Text14(
                "Como usar: instale um par de portas virtuais (com0com). Aponte o PC12 para uma ponta e este capturador para a outra.",
                8.4f, FontStyle.Regular, TextSecondary, 18, 112));
            setup.Controls.Add(Text14(
                "O PC12 original só aceita COM1 a COM4, então a ponta que ele enxerga precisa estar nessa faixa.",
                8.4f, FontStyle.Regular, TextSecondary, 18, 132));
            setup.Controls.Add(Text14(
                "Esta tela nunca transmite por conta própria: só repassa o que recebe. Abra o PC12 depois de iniciar a captura.",
                8.4f, FontStyle.Regular, Accent, 18, 152));

            DockOrder.Apply(this, logBox, setup, header);
        }

        private void RefreshPorts()
        {
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            foreach (ComboBox c in new ComboBox[] { pcPortCombo, plcPortCombo })
            {
                object keep = c.SelectedItem;
                c.Items.Clear();
                c.Items.AddRange(ports);
                if (keep != null && c.Items.Contains(keep)) c.SelectedItem = keep;
                else if (c.Items.Count > 0) c.SelectedIndex = 0;
            }
        }

        private void Toggle()
        {
            if (worker != null && worker.IsAlive)
            {
                stopRequested = true;
                return;
            }
            if (pcPortCombo.SelectedItem == null || plcPortCombo.SelectedItem == null)
            {
                MessageBox.Show("Selecione as duas portas.", "Captura serial", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string pcPort = pcPortCombo.SelectedItem.ToString();
            string plcPort = plcPortCombo.SelectedItem.ToString();
            if (string.Equals(pcPort, plcPort, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("As duas portas precisam ser diferentes: uma é o par virtual do PC12, a outra é o PLC.",
                    "Captura serial", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int baud = int.Parse(baudCombo.SelectedItem.ToString(), CultureInfo.InvariantCulture);
            int bits = int.Parse(dataBitsCombo.SelectedItem.ToString(), CultureInfo.InvariantCulture);
            Parity parity = (Parity)Enum.Parse(typeof(Parity), parityCombo.SelectedItem.ToString());
            StopBits stop = stopBitsCombo.SelectedItem.ToString() == "2" ? StopBits.Two : StopBits.One;
            int gap = (int)gapBox.Value;
            bool dtr = dtrCheck.Checked;
            bool rts = rtsCheck.Checked;

            stopRequested = false;
            startButton.Text = "PARAR CAPTURA";
            Line("== captura iniciada em " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " ==");
            Line("   PC12 -> " + pcPort + "   PLC -> " + plcPort + "   " + baud + " " + bits
                + (parity == Parity.Even ? "E" : parity == Parity.Odd ? "O" : "N")
                + (stop == StopBits.Two ? "2" : "1"));

            worker = new Thread(delegate() { Relay(pcPort, plcPort, baud, bits, parity, stop, gap, dtr, rts); });
            worker.IsBackground = true;
            worker.Start();
        }

        private void Relay(string pcPort, string plcPort, int baud, int bits, Parity parity,
            StopBits stop, int gap, bool dtr, bool rts)
        {
            SerialPort a = null, b = null;
            try
            {
                a = Open(pcPort, baud, bits, parity, stop, false, false);
                b = Open(plcPort, baud, bits, parity, stop, dtr, rts);

                List<byte> fromPc = new List<byte>();
                List<byte> fromPlc = new List<byte>();
                DateTime lastPc = DateTime.UtcNow, lastPlc = DateTime.UtcNow;

                while (!stopRequested)
                {
                    bool idle = true;

                    int n = a.BytesToRead;
                    if (n > 0)
                    {
                        byte[] buf = new byte[n];
                        int got = a.Read(buf, 0, n);
                        b.Write(buf, 0, got);
                        for (int i = 0; i < got; i++) fromPc.Add(buf[i]);
                        lastPc = DateTime.UtcNow;
                        idle = false;
                    }

                    n = b.BytesToRead;
                    if (n > 0)
                    {
                        byte[] buf = new byte[n];
                        int got = b.Read(buf, 0, n);
                        a.Write(buf, 0, got);
                        for (int i = 0; i < got; i++) fromPlc.Add(buf[i]);
                        lastPlc = DateTime.UtcNow;
                        idle = false;
                    }

                    if (fromPc.Count > 0 && (DateTime.UtcNow - lastPc).TotalMilliseconds >= gap)
                    {
                        Emit("PC12 -> PLC", fromPc);
                        fromPc.Clear();
                    }
                    if (fromPlc.Count > 0 && (DateTime.UtcNow - lastPlc).TotalMilliseconds >= gap)
                    {
                        Emit("PLC -> PC12", fromPlc);
                        fromPlc.Clear();
                    }

                    if (idle) Thread.Sleep(1);
                }

                if (fromPc.Count > 0) Emit("PC12 -> PLC", fromPc);
                if (fromPlc.Count > 0) Emit("PLC -> PC12", fromPlc);
            }
            catch (Exception ex)
            {
                Line("ERRO  " + ex.Message);
            }
            finally
            {
                Close(a);
                Close(b);
                Line("== captura encerrada ==");
                Finish();
            }
        }

        private static SerialPort Open(string name, int baud, int bits, Parity parity, StopBits stop, bool dtr, bool rts)
        {
            SerialPort p = new SerialPort(name);
            p.BaudRate = baud;
            p.DataBits = bits;
            p.Parity = parity;
            p.StopBits = stop;
            p.Handshake = Handshake.None;
            p.DtrEnable = dtr;
            p.RtsEnable = rts;
            p.ReadTimeout = 50;
            p.WriteTimeout = 1000;
            p.Open();
            p.DiscardInBuffer();
            p.DiscardOutBuffer();
            return p;
        }

        private static void Close(SerialPort p)
        {
            if (p == null) return;
            try { if (p.IsOpen) p.Close(); }
            catch { }
            p.Dispose();
        }

        private void Emit(string direction, List<byte> bytes)
        {
            byte[] frame = bytes.ToArray();
            StringBuilder hex = new StringBuilder();
            StringBuilder ascii = new StringBuilder();
            int sum = 0;
            for (int i = 0; i < frame.Length; i++)
            {
                if (i > 0) hex.Append(' ');
                hex.Append(frame[i].ToString("X2", CultureInfo.InvariantCulture));
                ascii.Append(frame[i] >= 32 && frame[i] < 127 ? (char)frame[i] : '.');
                sum += frame[i];
            }

            StringBuilder note = new StringBuilder();
            note.Append(frame.Length.ToString(CultureInfo.InvariantCulture)).Append("B");
            if ((sum % 256) == 0xFF) note.Append("  soma=FF");
            // Formato derivado do codigo do PC12: CMD LEN payload[LEN] CHECKSUM.
            if (frame.Length >= 3 && frame[1] == frame.Length - 3)
            {
                note.Append("  CMD=").Append(frame[0].ToString("X2", CultureInfo.InvariantCulture));
                note.Append(" LEN=").Append(frame[1].ToString(CultureInfo.InvariantCulture));
            }

            Line(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "  " + direction.PadRight(12)
                + note.ToString().PadRight(26)
                + hex.ToString()
                + "   |" + ascii.ToString() + "|");
        }

        private void Line(string text)
        {
            if (logBox == null || logBox.IsDisposed) return;
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new LineHandler(Line), new object[] { text });
                return;
            }
            logBox.AppendText(text + Environment.NewLine);
        }

        private void Finish()
        {
            if (logBox == null || logBox.IsDisposed) return;
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new MethodInvoker(Finish));
                return;
            }
            startButton.Text = "INICIAR CAPTURA";
        }

        private void SaveCapture()
        {
            if (logBox.TextLength == 0) return;
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "Captura (*.txt)|*.txt";
            dlg.FileName = "TP02_captura_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, logBox.Text, Encoding.UTF8);
        }

        private Label Text14(string text, float size, FontStyle style, Color color, int left, int top)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = color;
            l.Font = new Font("Segoe UI", size, style);
            l.Location = new Point(left, top);
            return l;
        }

        private ComboBox Combo(int left, int top, int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(left, top);
            c.Size = new Size(width, 25);
            return c;
        }

        private CheckBox Check(string text, int left, int top)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Checked = true;
            c.ForeColor = TextPrimary;
            c.Location = new Point(left, top);
            return c;
        }

        private Button Btn(string text, int left, int top, int width, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 32);
            b.FlatStyle = FlatStyle.Flat;
            b.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            b.BackColor = primary ? Accent : Color.FromArgb(232, 237, 242);
            b.ForeColor = primary ? Color.White : TextPrimary;
            b.FlatAppearance.BorderColor = Color.FromArgb(198, 208, 218);
            return b;
        }
    }

    internal static class TP02SerialCaptureProgram
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppBranding.Install();
            Application.Run(new TP02SerialCaptureForm());
        }
    }
}
