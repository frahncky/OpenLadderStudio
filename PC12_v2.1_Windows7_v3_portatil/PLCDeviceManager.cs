using System;
using System.Drawing;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class DeviceManagerProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PlcDeviceManagerForm());
        }
    }

    internal sealed class PlcDeviceManagerForm : Form
    {
        private readonly Color Shell = Color.FromArgb(29, 31, 34);
        private readonly Color Chrome = Color.FromArgb(37, 39, 43);
        private readonly Color PanelColor = Color.FromArgb(47, 50, 55);
        private readonly Color Border = Color.FromArgb(61, 64, 69);
        private readonly Color Accent = Color.FromArgb(45, 170, 107);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(150, 157, 164);

        private DataGridView grid;
        private Label modelValue;
        private Label driverValue;
        private Label protocolValue;
        private Label transportValue;
        private Label supportValue;
        private Label capabilitiesValue;
        private Label notesValue;
        private Button useButton;
        private PlcDeviceProfile selectedProfile;

        public PlcDeviceProfile SelectedProfile { get { return selectedProfile; } }

        public PlcDeviceManagerForm()
        {
            Text = "OpenLadder Studio - Controladores";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 620);
            MinimumSize = new Size(900, 560);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            BuildUi();
            LoadProfiles();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 72;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "Controladores e drivers";
            title.AutoSize = true;
            title.Font = new Font("Segoe UI Semibold", 18.0f, FontStyle.Bold);
            title.ForeColor = Fore;
            title.Location = new Point(22, 12);
            header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Arquitetura multi-fabricante do OpenLadder Studio";
            sub.AutoSize = true;
            sub.ForeColor = Muted;
            sub.Location = new Point(24, 45);
            header.Controls.Add(sub);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 58;
            footer.BackColor = Chrome;
            Controls.Add(footer);

            useButton = new Button();
            useButton.Text = "USAR ESTE CONTROLADOR";
            useButton.Size = new Size(210, 34);
            useButton.Location = new Point(14, 12);
            useButton.FlatStyle = FlatStyle.Flat;
            useButton.FlatAppearance.BorderColor = Accent;
            useButton.BackColor = Accent;
            useButton.ForeColor = Color.White;
            useButton.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            useButton.Click += UseSelectedProfile;
            footer.Controls.Add(useButton);

            Button close = new Button();
            close.Text = "FECHAR";
            close.Size = new Size(110, 34);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(844, 12);
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderColor = Border;
            close.BackColor = PanelColor;
            close.ForeColor = Fore;
            close.Click += delegate { Close(); };
            footer.Controls.Add(close);
            footer.Resize += delegate { close.Left = footer.ClientSize.Width - close.Width - 14; };

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 560;
            split.BackColor = Shell;
            split.Panel1.BackColor = Shell;
            split.Panel2.BackColor = Chrome;
            Controls.Add(split);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Shell;
            grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.MultiSelect = false;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Chrome;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Fore;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Chrome;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Shell;
            grid.DefaultCellStyle.ForeColor = Fore;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 82, 69);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.GridColor = Border;
            grid.Columns.Add("manufacturer", "Fabricante");
            grid.Columns.Add("family", "Família");
            grid.Columns.Add("model", "Modelo");
            grid.Columns.Add("support", "Suporte");
            grid.SelectionChanged += GridSelectionChanged;
            split.Panel1.Padding = new Padding(12);
            split.Panel1.Controls.Add(grid);

            Panel details = split.Panel2;
            details.Padding = new Padding(20);

            Label detailTitle = NewLabel("PERFIL DO DISPOSITIVO", 9.0f, true, Muted);
            detailTitle.Location = new Point(20, 20);
            details.Controls.Add(detailTitle);

            AddPair(details, "Controlador", out modelValue, 62);
            AddPair(details, "Driver", out driverValue, 116);
            AddPair(details, "Protocolo", out protocolValue, 170);
            AddPair(details, "Transporte", out transportValue, 224);
            AddPair(details, "Nível de suporte", out supportValue, 278);

            Label capTitle = NewLabel("Recursos disponíveis", 8.2f, false, Muted);
            capTitle.Location = new Point(20, 342);
            details.Controls.Add(capTitle);

            capabilitiesValue = NewLabel("-", 9.0f, false, Fore);
            capabilitiesValue.Location = new Point(20, 364);
            capabilitiesValue.MaximumSize = new Size(330, 60);
            details.Controls.Add(capabilitiesValue);

            Label noteTitle = NewLabel("Observação", 8.2f, false, Muted);
            noteTitle.Location = new Point(20, 430);
            details.Controls.Add(noteTitle);

            notesValue = NewLabel("-", 8.8f, false, Fore);
            notesValue.Location = new Point(20, 452);
            notesValue.MaximumSize = new Size(330, 70);
            details.Controls.Add(notesValue);

            header.BringToFront();
            footer.BringToFront();
        }

        private void AddPair(Control parent, string caption, out Label value, int top)
        {
            Label c = NewLabel(caption, 8.2f, false, Muted);
            c.Location = new Point(20, top);
            parent.Controls.Add(c);

            value = NewLabel("-", 9.2f, true, Fore);
            value.Location = new Point(20, top + 20);
            value.MaximumSize = new Size(330, 24);
            parent.Controls.Add(value);
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

        private void LoadProfiles()
        {
            grid.Rows.Clear();
            PlcDeviceProfile current = PlcProfileStore.Load();
            int selectedRow = -1;

            for (int i = 0; i < PlcDriverRegistry.Profiles.Count; i++)
            {
                PlcDeviceProfile p = PlcDriverRegistry.Profiles[i];
                int row = grid.Rows.Add(p.Manufacturer, p.Family, p.Model, SupportText(p.SupportLevel));
                grid.Rows[row].Tag = p;
                if (current != null && string.Equals(current.Id, p.Id, StringComparison.OrdinalIgnoreCase)) selectedRow = row;
            }

            if (grid.Rows.Count > 0)
            {
                if (selectedRow < 0) selectedRow = 0;
                grid.ClearSelection();
                grid.Rows[selectedRow].Selected = true;
                grid.CurrentCell = grid.Rows[selectedRow].Cells[0];
                ShowProfile(grid.Rows[selectedRow].Tag as PlcDeviceProfile);
            }
        }

        private void GridSelectionChanged(object sender, EventArgs e)
        {
            if (grid.CurrentRow == null) return;
            ShowProfile(grid.CurrentRow.Tag as PlcDeviceProfile);
        }

        private void ShowProfile(PlcDeviceProfile profile)
        {
            selectedProfile = profile;
            if (profile == null) return;

            IPlcDriver driver = PlcDriverRegistry.FindDriver(profile.DriverId);
            modelValue.Text = profile.Manufacturer + " " + profile.Model;
            driverValue.Text = driver == null ? profile.DriverId : driver.DisplayName;
            protocolValue.Text = profile.Protocol;
            transportValue.Text = TransportText(profile.Transport);
            supportValue.Text = SupportText(profile.SupportLevel);
            supportValue.ForeColor = SupportColor(profile.SupportLevel);
            capabilitiesValue.Text = driver == null ? "Driver ainda não registrado." : driver.Capabilities.Summary();
            notesValue.Text = profile.Notes + (driver == null ? string.Empty : " " + driver.DescribeConnection(profile));

            useButton.Enabled = profile.SupportLevel != PlcSupportLevel.Planned;
            useButton.Text = profile.SupportLevel == PlcSupportLevel.Planned ? "DRIVER AINDA PLANEJADO" : "USAR ESTE CONTROLADOR";
        }

        private void UseSelectedProfile(object sender, EventArgs e)
        {
            if (selectedProfile == null) return;
            if (selectedProfile.SupportLevel == PlcSupportLevel.Planned)
            {
                MessageBox.Show(this, "Este perfil já está cadastrado na arquitetura, mas o driver ainda não foi implementado.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                PlcProfileStore.Save(selectedProfile);
                MessageBox.Show(this, "Controlador selecionado: " + selectedProfile.Manufacturer + " " + selectedProfile.Model + ".\r\n\r\nO perfil foi salvo como padrão do OpenLadder Studio.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível salvar o perfil.\r\n\r\n" + ex.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string SupportText(PlcSupportLevel level)
        {
            if (level == PlcSupportLevel.Implemented) return "Implementado";
            if (level == PlcSupportLevel.Experimental) return "Experimental";
            return "Planejado";
        }

        private Color SupportColor(PlcSupportLevel level)
        {
            if (level == PlcSupportLevel.Implemented) return Accent;
            if (level == PlcSupportLevel.Experimental) return Color.FromArgb(215, 166, 71);
            return Muted;
        }

        private string TransportText(PlcTransportKind kind)
        {
            if (kind == PlcTransportKind.Serial) return "Serial";
            if (kind == PlcTransportKind.Tcp) return "TCP/IP";
            if (kind == PlcTransportKind.EthernetIndustrial) return "Ethernet industrial";
            return "Específico do fabricante";
        }
    }
}
