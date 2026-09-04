using System;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class ModbusMonitorProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ModbusMonitorForm());
        }
    }

    internal sealed class ModbusMonitorForm : Form
    {
        private readonly Color Shell = Color.FromArgb(29, 31, 34);
        private readonly Color Chrome = Color.FromArgb(37, 39, 43);
        private readonly Color PanelColor = Color.FromArgb(47, 50, 55);
        private readonly Color Border = Color.FromArgb(61, 64, 69);
        private readonly Color Accent = Color.FromArgb(45, 170, 107);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(150, 157, 164);

        private ComboBox transportCombo;
        private ComboBox portCombo;
        private ComboBox baudCombo;
        private ComboBox parityCombo;
        private ComboBox stopCombo;
        private NumericUpDown dataBitsBox;
        private TextBox hostBox;
        private NumericUpDown tcpPortBox;
        private NumericUpDown unitBox;
        private ComboBox functionCombo;
        private NumericUpDown addressBox;
        private NumericUpDown quantityBox;
        private NumericUpDown timeoutBox;
        private DataGridView resultGrid;
        private TextBox rawBox;
        private Label statusLabel;
        private Panel serialPanel;
        private Panel tcpPanel;

        public ModbusMonitorForm()
        {
            Text = "OpenLadder Studio - Modbus Monitor";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1180, 760);
            MinimumSize = new Size(980, 650);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            BuildUi();
            RefreshPorts();
            UpdateTransportUi();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 66;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = NewLabel("Monitor Modbus", 17.0f, true, Fore);
            title.Location = new Point(18, 10);
            header.Controls.Add(title);

            Label subtitle = NewLabel("Comunicação genérica RTU/TCP para dispositivos de vários fabricantes", 8.8f, false, Muted);
            subtitle.Location = new Point(20, 39);
            header.Controls.Add(subtitle);

            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = 330;
            left.BackColor = Chrome;
            left.AutoScroll = true;
            left.Padding = new Padding(16, 12, 16, 12);
            Controls.Add(left);

            int y = 12;
            AddCaption(left, "TRANSPORTE", y); y += 24;
            transportCombo = NewCombo(left, y, 286);
            transportCombo.Items.Add("Modbus RTU");
            transportCombo.Items.Add("Modbus TCP");
            transportCombo.SelectedIndex = 0;
            transportCombo.SelectedIndexChanged += delegate { UpdateTransportUi(); };
            y += 44;

            serialPanel = new Panel();
            serialPanel.Location = new Point(0, y);
            serialPanel.Size = new Size(314, 238);
            serialPanel.BackColor = Chrome;
            left.Controls.Add(serialPanel);
            BuildSerialPanel(serialPanel);

            tcpPanel = new Panel();
            tcpPanel.Location = new Point(0, y);
            tcpPanel.Size = new Size(314, 118);
            tcpPanel.BackColor = Chrome;
            left.Controls.Add(tcpPanel);
            BuildTcpPanel(tcpPanel);

            y += 246;
            AddDivider(left, y); y += 18;
            AddCaption(left, "LEITURA", y); y += 24;

            AddSmallLabel(left, "Unit ID", 16, y);
            unitBox = NewNumeric(left, 16, y + 18, 92, 1, 247, 1);
            AddSmallLabel(left, "Timeout (ms)", 126, y);
            timeoutBox = NewNumeric(left, 126, y + 18, 176, 100, 10000, 1000);
            y += 60;

            AddSmallLabel(left, "Função", 16, y);
            functionCombo = NewCombo(left, y + 18, 286);
            functionCombo.Location = new Point(16, y + 18);
            functionCombo.Items.Add("01 - Read Coils");
            functionCombo.Items.Add("02 - Read Discrete Inputs");
            functionCombo.Items.Add("03 - Read Holding Registers");
            functionCombo.Items.Add("04 - Read Input Registers");
            functionCombo.SelectedIndex = 2;
            functionCombo.SelectedIndexChanged += delegate { UpdateQuantityLimit(); };
            y += 64;

            AddSmallLabel(left, "Endereço inicial", 16, y);
            addressBox = NewNumeric(left, 16, y + 18, 136, 0, 65535, 0);
            AddSmallLabel(left, "Quantidade", 166, y);
            quantityBox = NewNumeric(left, 166, y + 18, 136, 1, 125, 10);
            y += 64;

            Button read = new Button();
            read.Text = "LER DISPOSITIVO";
            read.Location = new Point(16, y);
            read.Size = new Size(286, 38);
            read.FlatStyle = FlatStyle.Flat;
            read.FlatAppearance.BorderColor = Accent;
            read.BackColor = Accent;
            read.ForeColor = Color.White;
            read.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            read.Cursor = Cursors.Hand;
            read.Click += ReadDevice;
            left.Controls.Add(read);
            y += 50;

            statusLabel = NewLabel("Pronto", 8.7f, false, Muted);
            statusLabel.Location = new Point(16, y);
            statusLabel.MaximumSize = new Size(286, 60);
            left.Controls.Add(statusLabel);

            Panel center = new Panel();
            center.Dock = DockStyle.Fill;
            center.BackColor = Shell;
            center.Padding = new Padding(12);
            Controls.Add(center);

            Label resultTitle = NewLabel("Dados lidos", 11.0f, true, Fore);
            resultTitle.Dock = DockStyle.Top;
            resultTitle.Height = 30;
            center.Controls.Add(resultTitle);

            Panel rawPanel = new Panel();
            rawPanel.Dock = DockStyle.Bottom;
            rawPanel.Height = 122;
            rawPanel.BackColor = Chrome;
            rawPanel.Padding = new Padding(10);
            center.Controls.Add(rawPanel);

            Label rawTitle = NewLabel("Resposta bruta", 8.2f, true, Muted);
            rawTitle.Dock = DockStyle.Top;
            rawTitle.Height = 22;
            rawPanel.Controls.Add(rawTitle);

            rawBox = new TextBox();
            rawBox.Dock = DockStyle.Fill;
            rawBox.Multiline = true;
            rawBox.ReadOnly = true;
            rawBox.ScrollBars = ScrollBars.Vertical;
            rawBox.BackColor = Color.FromArgb(24, 26, 29);
            rawBox.ForeColor = Fore;
            rawBox.BorderStyle = BorderStyle.FixedSingle;
            rawBox.Font = new Font("Consolas", 9.0f);
            rawPanel.Controls.Add(rawBox);
            rawBox.BringToFront();

            resultGrid = new DataGridView();
            resultGrid.Dock = DockStyle.Fill;
            resultGrid.BackgroundColor = Shell;
            resultGrid.BorderStyle = BorderStyle.None;
            resultGrid.AllowUserToAddRows = false;
            resultGrid.AllowUserToDeleteRows = false;
            resultGrid.ReadOnly = true;
            resultGrid.RowHeadersVisible = false;
            resultGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            resultGrid.EnableHeadersVisualStyles = false;
            resultGrid.ColumnHeadersDefaultCellStyle.BackColor = Chrome;
            resultGrid.ColumnHeadersDefaultCellStyle.ForeColor = Fore;
            resultGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            resultGrid.DefaultCellStyle.BackColor = Shell;
            resultGrid.DefaultCellStyle.ForeColor = Fore;
            resultGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 82, 69);
            resultGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            resultGrid.GridColor = Border;
            resultGrid.Columns.Add("index", "Índice");
            resultGrid.Columns.Add("address", "Endereço");
            resultGrid.Columns.Add("value", "Valor");
            resultGrid.Columns.Add("hex", "Hex");
            center.Controls.Add(resultGrid);

            resultGrid.BringToFront();
            resultTitle.BringToFront();
            header.BringToFront();
            left.BringToFront();
        }

        private void BuildSerialPanel(Control parent)
        {
            AddSmallLabel(parent, "Porta COM", 16, 4);
            portCombo = NewCombo(parent, 22, 190);
            portCombo.Location = new Point(16, 22);

            Button refresh = new Button();
            refresh.Text = "↻";
            refresh.Location = new Point(216, 22);
            refresh.Size = new Size(72, 28);
            refresh.FlatStyle = FlatStyle.Flat;
            refresh.FlatAppearance.BorderColor = Border;
            refresh.BackColor = PanelColor;
            refresh.ForeColor = Fore;
            refresh.Click += delegate { RefreshPorts(); };
            parent.Controls.Add(refresh);

            AddSmallLabel(parent, "Baud rate", 16, 62);
            baudCombo = NewCombo(parent, 80, 132);
            baudCombo.Location = new Point(16, 80);
            baudCombo.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
            baudCombo.SelectedItem = "9600";

            AddSmallLabel(parent, "Data bits", 164, 62);
            dataBitsBox = NewNumeric(parent, 164, 80, 124, 5, 8, 8);

            AddSmallLabel(parent, "Paridade", 16, 122);
            parityCombo = NewCombo(parent, 140, 132);
            parityCombo.Location = new Point(16, 140);
            parityCombo.Items.AddRange(new object[] { "None", "Even", "Odd" });
            parityCombo.SelectedItem = "None";

            AddSmallLabel(parent, "Stop bits", 164, 122);
            stopCombo = NewCombo(parent, 140, 124);
            stopCombo.Location = new Point(164, 140);
            stopCombo.Items.AddRange(new object[] { "1", "2" });
            stopCombo.SelectedItem = "1";
        }

        private void BuildTcpPanel(Control parent)
        {
            AddSmallLabel(parent, "Endereço IP / host", 16, 4);
            hostBox = new TextBox();
            hostBox.Text = "192.168.0.10";
            hostBox.Location = new Point(16, 22);
            hostBox.Size = new Size(190, 28);
            hostBox.BackColor = PanelColor;
            hostBox.ForeColor = Fore;
            hostBox.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(hostBox);

            AddSmallLabel(parent, "Porta", 216, 4);
            tcpPortBox = NewNumeric(parent, 216, 22, 72, 1, 65535, 502);
        }

        private void UpdateTransportUi()
        {
            bool rtu = transportCombo != null && transportCombo.SelectedIndex == 0;
            if (serialPanel != null) serialPanel.Visible = rtu;
            if (tcpPanel != null) tcpPanel.Visible = !rtu;
        }

        private void UpdateQuantityLimit()
        {
            if (quantityBox == null || functionCombo == null) return;
            bool bits = functionCombo.SelectedIndex == 0 || functionCombo.SelectedIndex == 1;
            quantityBox.Maximum = bits ? 2000 : 125;
            if (quantityBox.Value > quantityBox.Maximum) quantityBox.Value = quantityBox.Maximum;
        }

        private void RefreshPorts()
        {
            if (portCombo == null) return;
            string selected = portCombo.SelectedItem as string;
            portCombo.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            for (int i = 0; i < ports.Length; i++) portCombo.Items.Add(ports[i]);
            if (!string.IsNullOrEmpty(selected) && portCombo.Items.Contains(selected)) portCombo.SelectedItem = selected;
            else if (portCombo.Items.Count > 0) portCombo.SelectedIndex = 0;
        }

        private void ReadDevice(object sender, EventArgs e)
        {
            resultGrid.Rows.Clear();
            rawBox.Clear();
            statusLabel.Text = "Lendo...";
            statusLabel.ForeColor = Muted;
            Cursor = Cursors.WaitCursor;

            try
            {
                byte unit = (byte)unitBox.Value;
                ushort address = (ushort)addressBox.Value;
                ushort quantity = (ushort)quantityBox.Value;
                ModbusFunction function = (ModbusFunction)(functionCombo.SelectedIndex + 1);
                ModbusReadResult result;

                if (transportCombo.SelectedIndex == 0)
                {
                    if (portCombo.SelectedItem == null) throw new InvalidOperationException("Nenhuma porta COM selecionada.");
                    ModbusRtuClient client = new ModbusRtuClient();
                    client.PortName = portCombo.SelectedItem.ToString();
                    client.BaudRate = int.Parse(baudCombo.SelectedItem.ToString());
                    client.DataBits = (int)dataBitsBox.Value;
                    client.Parity = ParseParity(parityCombo.SelectedItem.ToString());
                    client.StopBits = stopCombo.SelectedItem.ToString() == "2" ? StopBits.Two : StopBits.One;
                    client.TimeoutMs = (int)timeoutBox.Value;
                    result = client.Read(unit, function, address, quantity);
                }
                else
                {
                    ModbusTcpClient client = new ModbusTcpClient();
                    client.Host = hostBox.Text.Trim();
                    client.Port = (int)tcpPortBox.Value;
                    client.TimeoutMs = (int)timeoutBox.Value;
                    result = client.Read(unit, function, address, quantity);
                }

                rawBox.Text = ToHex(result.RawResponse);
                if (!result.Success)
                {
                    statusLabel.Text = "Erro: " + result.Error;
                    statusLabel.ForeColor = Color.FromArgb(220, 105, 105);
                    return;
                }

                if (result.Bits.Length > 0)
                {
                    for (int i = 0; i < result.Bits.Length; i++)
                        resultGrid.Rows.Add(i.ToString(), (address + i).ToString(), result.Bits[i] ? "ON" : "OFF", result.Bits[i] ? "1" : "0");
                }
                else
                {
                    for (int i = 0; i < result.Registers.Length; i++)
                    {
                        ushort value = result.Registers[i];
                        resultGrid.Rows.Add(i.ToString(), (address + i).ToString(), value.ToString(), "0x" + value.ToString("X4"));
                    }
                }

                statusLabel.Text = "Leitura concluída: " + quantity.ToString() + " ponto(s).";
                statusLabel.ForeColor = Accent;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erro: " + ex.Message;
                statusLabel.ForeColor = Color.FromArgb(220, 105, 105);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private Parity ParseParity(string text)
        {
            if (string.Equals(text, "Even", StringComparison.OrdinalIgnoreCase)) return Parity.Even;
            if (string.Equals(text, "Odd", StringComparison.OrdinalIgnoreCase)) return Parity.Odd;
            return Parity.None;
        }

        private string ToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private ComboBox NewCombo(Control parent, int top, int width)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(16, top);
            c.Size = new Size(width, 28);
            c.BackColor = PanelColor;
            c.ForeColor = Fore;
            parent.Controls.Add(c);
            return c;
        }

        private NumericUpDown NewNumeric(Control parent, int left, int top, int width, decimal min, decimal max, decimal value)
        {
            NumericUpDown n = new NumericUpDown();
            n.Location = new Point(left, top);
            n.Size = new Size(width, 28);
            n.Minimum = min;
            n.Maximum = max;
            n.Value = value;
            n.BackColor = PanelColor;
            n.ForeColor = Fore;
            parent.Controls.Add(n);
            return n;
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

        private void AddCaption(Control parent, string text, int top)
        {
            Label l = NewLabel(text, 8.2f, true, Muted);
            l.Location = new Point(16, top);
            parent.Controls.Add(l);
        }

        private void AddSmallLabel(Control parent, string text, int left, int top)
        {
            Label l = NewLabel(text, 8.0f, false, Muted);
            l.Location = new Point(left, top);
            parent.Controls.Add(l);
        }

        private void AddDivider(Control parent, int top)
        {
            Panel line = new Panel();
            line.Location = new Point(16, top);
            line.Size = new Size(286, 1);
            line.BackColor = Border;
            parent.Controls.Add(line);
        }
    }
}
