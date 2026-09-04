using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class DeviceManagerProgram
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
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
        private readonly Color Warning = Color.FromArgb(215, 166, 71);

        private DataGridView grid;
        private Label modelValue;
        private Label driverValue;
        private Label protocolValue;
        private Label transportValue;
        private Label supportValue;
        private Label capabilitiesValue;
        private Label notesValue;
        private Label originValue;
        private Button useButton;
        private Button editButton;
        private Button deleteButton;
        private PlcDeviceProfile selectedProfile;

        public PlcDeviceProfile SelectedProfile { get { return selectedProfile; } }

        public PlcDeviceManagerForm()
        {
            Text = "OpenLadder Studio - Controladores";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1080, 660);
            MinimumSize = new Size(960, 580);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            BuildUi();
            LoadProfiles(null);
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 76;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = NewLabel("Controladores e drivers", 18.0f, true, Fore);
            title.Location = new Point(22, 10);
            header.Controls.Add(title);

            Label sub = NewLabel("Perfis nativos e controladores personalizados do OpenLadder Studio", 8.7f, false, Muted);
            sub.Location = new Point(24, 45);
            header.Controls.Add(sub);

            Button create = ButtonStyle("NOVO PERFIL", 790, 18, 116, PanelColor);
            create.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            create.Click += CreateCustomProfile;
            header.Controls.Add(create);

            editButton = ButtonStyle("EDITAR", 914, 18, 72, PanelColor);
            editButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            editButton.Click += EditCustomProfile;
            header.Controls.Add(editButton);

            deleteButton = ButtonStyle("EXCLUIR", 994, 18, 72, PanelColor);
            deleteButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            deleteButton.Click += DeleteCustomProfile;
            header.Controls.Add(deleteButton);

            header.Resize += delegate
            {
                create.Left = header.ClientSize.Width - 290;
                editButton.Left = header.ClientSize.Width - 166;
                deleteButton.Left = header.ClientSize.Width - 86;
            };

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 60;
            footer.BackColor = Chrome;
            Controls.Add(footer);

            useButton = ButtonStyle("USAR ESTE CONTROLADOR", 14, 13, 214, Accent);
            useButton.Click += UseSelectedProfile;
            footer.Controls.Add(useButton);

            Label hint = NewLabel("Perfis personalizados Modbus reutilizam os drivers genéricos RTU/TCP.", 8.2f, false, Muted);
            hint.Location = new Point(246, 23);
            footer.Controls.Add(hint);

            Button close = ButtonStyle("FECHAR", 944, 13, 110, PanelColor);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Click += delegate { Close(); };
            footer.Controls.Add(close);
            footer.Resize += delegate { close.Left = footer.ClientSize.Width - close.Width - 14; };

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 620;
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
            grid.Columns.Add("origin", "Origem");
            grid.Columns.Add("support", "Suporte");
            grid.SelectionChanged += GridSelectionChanged;
            split.Panel1.Padding = new Padding(12);
            split.Panel1.Controls.Add(grid);

            Panel details = split.Panel2;
            details.Padding = new Padding(20);

            Label detailTitle = NewLabel("PERFIL DO DISPOSITIVO", 9.0f, true, Muted);
            detailTitle.Location = new Point(20, 20);
            details.Controls.Add(detailTitle);

            AddPair(details, "Controlador", out modelValue, 60);
            AddPair(details, "Origem", out originValue, 108);
            AddPair(details, "Driver", out driverValue, 156);
            AddPair(details, "Protocolo", out protocolValue, 204);
            AddPair(details, "Transporte", out transportValue, 252);
            AddPair(details, "Nível de suporte", out supportValue, 300);

            Label capTitle = NewLabel("Recursos disponíveis", 8.2f, false, Muted);
            capTitle.Location = new Point(20, 360);
            details.Controls.Add(capTitle);

            capabilitiesValue = NewLabel("-", 9.0f, false, Fore);
            capabilitiesValue.Location = new Point(20, 382);
            capabilitiesValue.MaximumSize = new Size(390, 60);
            details.Controls.Add(capabilitiesValue);

            Label noteTitle = NewLabel("Observação", 8.2f, false, Muted);
            noteTitle.Location = new Point(20, 452);
            details.Controls.Add(noteTitle);

            notesValue = NewLabel("-", 8.8f, false, Fore);
            notesValue.Location = new Point(20, 474);
            notesValue.MaximumSize = new Size(390, 92);
            details.Controls.Add(notesValue);

            header.BringToFront();
            footer.BringToFront();
        }

        private void LoadProfiles(string preferredId)
        {
            grid.Rows.Clear();
            PlcDeviceProfile current = PlcProfileStore.Load();
            string targetId = string.IsNullOrEmpty(preferredId) ? (current == null ? string.Empty : current.Id) : preferredId;
            int selectedRow = -1;

            for (int i = 0; i < PlcDriverRegistry.Profiles.Count; i++)
            {
                PlcDeviceProfile p = PlcDriverRegistry.Profiles[i];
                int row = AddProfileRow(p, false);
                if (string.Equals(targetId, p.Id, StringComparison.OrdinalIgnoreCase)) selectedRow = row;
            }

            List<PlcDeviceProfile> custom = CustomPlcProfileStore.LoadAll();
            for (int i = 0; i < custom.Count; i++)
            {
                PlcDeviceProfile p = custom[i];
                int row = AddProfileRow(p, true);
                if (string.Equals(targetId, p.Id, StringComparison.OrdinalIgnoreCase)) selectedRow = row;
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

        private int AddProfileRow(PlcDeviceProfile profile, bool custom)
        {
            int row = grid.Rows.Add(profile.Manufacturer, profile.Family, profile.Model, custom ? "Personalizado" : "Nativo", SupportText(profile.SupportLevel));
            grid.Rows[row].Tag = profile;
            if (custom) grid.Rows[row].DefaultCellStyle.ForeColor = Color.FromArgb(190, 230, 210);
            return row;
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
            bool custom = CustomPlcProfileStore.IsCustom(profile);
            modelValue.Text = profile.Manufacturer + " " + profile.Model;
            originValue.Text = custom ? "Perfil personalizado" : "Catálogo nativo";
            originValue.ForeColor = custom ? Accent : Fore;
            driverValue.Text = driver == null ? profile.DriverId : driver.DisplayName;
            protocolValue.Text = profile.Protocol;
            transportValue.Text = TransportText(profile.Transport);
            supportValue.Text = SupportText(profile.SupportLevel);
            supportValue.ForeColor = SupportColor(profile.SupportLevel);
            capabilitiesValue.Text = driver == null ? "Driver ainda não registrado." : driver.Capabilities.Summary();
            notesValue.Text = profile.Notes + (driver == null ? string.Empty : " " + driver.DescribeConnection(profile));

            editButton.Enabled = custom;
            deleteButton.Enabled = custom;
            useButton.Enabled = profile.SupportLevel != PlcSupportLevel.Planned && driver != null;
            useButton.Text = profile.SupportLevel == PlcSupportLevel.Planned ? "DRIVER AINDA PLANEJADO" : "USAR ESTE CONTROLADOR";
        }

        private void CreateCustomProfile(object sender, EventArgs e)
        {
            using (PlcProfileEditorForm dialog = new PlcProfileEditorForm(null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ResultProfile == null) return;
                CustomPlcProfileStore.Upsert(dialog.ResultProfile);
                LoadProfiles(dialog.ResultProfile.Id);
            }
        }

        private void EditCustomProfile(object sender, EventArgs e)
        {
            if (!CustomPlcProfileStore.IsCustom(selectedProfile)) return;
            using (PlcProfileEditorForm dialog = new PlcProfileEditorForm(selectedProfile))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ResultProfile == null) return;
                CustomPlcProfileStore.Upsert(dialog.ResultProfile);
                PlcDeviceProfile active = PlcProfileStore.Load();
                if (active != null && string.Equals(active.Id, dialog.ResultProfile.Id, StringComparison.OrdinalIgnoreCase))
                    PlcProfileStore.Save(dialog.ResultProfile);
                LoadProfiles(dialog.ResultProfile.Id);
            }
        }

        private void DeleteCustomProfile(object sender, EventArgs e)
        {
            if (!CustomPlcProfileStore.IsCustom(selectedProfile)) return;
            if (MessageBox.Show(this,
                "Excluir o perfil personalizado " + selectedProfile.Manufacturer + " " + selectedProfile.Model + "?\r\n\r\nAs configurações de conexão e o mapa de memória associados não serão apagados automaticamente.",
                "Excluir perfil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            PlcDeviceProfile active = PlcProfileStore.Load();
            string removedId = selectedProfile.Id;
            CustomPlcProfileStore.Delete(removedId);
            if (active != null && string.Equals(active.Id, removedId, StringComparison.OrdinalIgnoreCase))
                PlcProfileStore.Save(PlcDriverRegistry.DefaultProfile);
            LoadProfiles(null);
        }

        private void UseSelectedProfile(object sender, EventArgs e)
        {
            if (selectedProfile == null) return;
            IPlcDriver driver = PlcDriverRegistry.FindDriver(selectedProfile.DriverId);
            if (selectedProfile.SupportLevel == PlcSupportLevel.Planned || driver == null)
            {
                MessageBox.Show(this, "Este perfil está cadastrado, mas ainda não possui um driver funcional.", "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                PlcProfileStore.Save(selectedProfile);
                MessageBox.Show(this,
                    "Controlador selecionado: " + selectedProfile.Manufacturer + " " + selectedProfile.Model + ".\r\n\r\nO perfil foi salvo como padrão do OpenLadder Studio. As configurações de conexão e o mapa de memória permanecem separados para este modelo.",
                    "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível salvar o perfil.\r\n\r\n" + ex.Message, "OpenLadder Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddPair(Control parent, string caption, out Label value, int top)
        {
            Label c = NewLabel(caption, 8.2f, false, Muted);
            c.Location = new Point(20, top);
            parent.Controls.Add(c);

            value = NewLabel("-", 9.2f, true, Fore);
            value.Location = new Point(20, top + 18);
            value.MaximumSize = new Size(390, 24);
            parent.Controls.Add(value);
        }

        private Button ButtonStyle(string text, int left, int top, int width, Color back)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = back == Accent ? Accent : Border;
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
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

        private string SupportText(PlcSupportLevel level)
        {
            if (level == PlcSupportLevel.Implemented) return "Implementado";
            if (level == PlcSupportLevel.Experimental) return "Experimental";
            return "Planejado";
        }

        private Color SupportColor(PlcSupportLevel level)
        {
            if (level == PlcSupportLevel.Implemented) return Accent;
            if (level == PlcSupportLevel.Experimental) return Warning;
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

    internal sealed class PlcProfileEditorForm : Form
    {
        private readonly Color Shell = Color.FromArgb(29, 31, 34);
        private readonly Color Chrome = Color.FromArgb(37, 39, 43);
        private readonly Color PanelColor = Color.FromArgb(47, 50, 55);
        private readonly Color Border = Color.FromArgb(61, 64, 69);
        private readonly Color Accent = Color.FromArgb(45, 170, 107);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(150, 157, 164);

        private readonly PlcDeviceProfile original;
        private TextBox manufacturerBox;
        private TextBox familyBox;
        private TextBox modelBox;
        private ComboBox driverBox;
        private TextBox notesBox;
        private Label protocolPreview;

        public PlcDeviceProfile ResultProfile { get; private set; }

        public PlcProfileEditorForm(PlcDeviceProfile profile)
        {
            original = profile;
            Text = profile == null ? "Novo controlador personalizado" : "Editar controlador personalizado";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(570, 560);
            MinimumSize = new Size(570, 560);
            MaximumSize = new Size(570, 560);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            BuildUi();
            LoadProfile();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 72;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = LabelStyle(original == null ? "Novo perfil de PLC" : "Editar perfil de PLC", 16.0f, true, Fore);
            title.Location = new Point(20, 10);
            header.Controls.Add(title);

            Label sub = LabelStyle("Crie um modelo próprio usando a comunicação Modbus já implementada.", 8.4f, false, Muted);
            sub.Location = new Point(22, 42);
            header.Controls.Add(sub);

            int y = 94;
            AddCaption("Fabricante", 22, y);
            manufacturerBox = TextBoxStyle(22, y + 20, 510); y += 62;

            AddCaption("Família", 22, y);
            familyBox = TextBoxStyle(22, y + 20, 245);
            AddCaption("Modelo", 287, y);
            modelBox = TextBoxStyle(287, y + 20, 245); y += 62;

            AddCaption("Driver / protocolo", 22, y);
            driverBox = new ComboBox();
            driverBox.DropDownStyle = ComboBoxStyle.DropDownList;
            driverBox.Location = new Point(22, y + 20);
            driverBox.Size = new Size(510, 28);
            driverBox.BackColor = PanelColor;
            driverBox.ForeColor = Fore;
            driverBox.Items.Add("Modbus RTU - Serial");
            driverBox.Items.Add("Modbus TCP - Ethernet");
            driverBox.SelectedIndexChanged += delegate { UpdateProtocolPreview(); };
            Controls.Add(driverBox); y += 62;

            protocolPreview = LabelStyle("-", 8.5f, true, Accent);
            protocolPreview.Location = new Point(22, y);
            Controls.Add(protocolPreview); y += 34;

            AddCaption("Observação", 22, y);
            notesBox = TextBoxStyle(22, y + 20, 510);
            notesBox.Multiline = true;
            notesBox.ScrollBars = ScrollBars.Vertical;
            notesBox.Height = 92;

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 64;
            footer.BackColor = Chrome;
            Controls.Add(footer);

            Button save = ButtonStyle("SALVAR PERFIL", 22, 14, 150, Accent);
            save.Click += SaveProfile;
            footer.Controls.Add(save);

            Button cancel = ButtonStyle("CANCELAR", 438, 14, 110, PanelColor);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancel);

            Label safe = LabelStyle("Leitura/monitoramento; escrita permanece desabilitada.", 8.0f, false, Muted);
            safe.Location = new Point(190, 24);
            footer.Controls.Add(safe);
        }

        private void LoadProfile()
        {
            if (original == null)
            {
                driverBox.SelectedIndex = 0;
                notesBox.Text = "Perfil personalizado criado no OpenLadder Studio.";
                return;
            }

            manufacturerBox.Text = original.Manufacturer;
            familyBox.Text = original.Family;
            modelBox.Text = original.Model;
            notesBox.Text = original.Notes;
            driverBox.SelectedIndex = string.Equals(original.DriverId, "generic.modbus.tcp", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            UpdateProtocolPreview();
        }

        private void UpdateProtocolPreview()
        {
            if (protocolPreview == null || driverBox == null) return;
            protocolPreview.Text = driverBox.SelectedIndex == 1
                ? "Modbus TCP • TCP/IP • driver generic.modbus.tcp"
                : "Modbus RTU • Serial • driver generic.modbus.rtu";
        }

        private void SaveProfile(object sender, EventArgs e)
        {
            string manufacturer = manufacturerBox.Text.Trim();
            string family = familyBox.Text.Trim();
            string model = modelBox.Text.Trim();
            if (manufacturer.Length == 0)
            {
                MessageBox.Show(this, "Informe o fabricante.", "Perfil de PLC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                manufacturerBox.Focus();
                return;
            }
            if (model.Length == 0)
            {
                MessageBox.Show(this, "Informe o modelo.", "Perfil de PLC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                modelBox.Focus();
                return;
            }

            bool tcp = driverBox.SelectedIndex == 1;
            PlcDeviceProfile p = new PlcDeviceProfile();
            p.Id = original == null ? CustomPlcProfileStore.CreateId(manufacturer, family, model, tcp ? "generic.modbus.tcp" : "generic.modbus.rtu") : original.Id;
            p.Manufacturer = manufacturer;
            p.Family = family.Length == 0 ? "Personalizado" : family;
            p.Model = model;
            p.Protocol = tcp ? "Modbus TCP" : "Modbus RTU";
            p.Transport = tcp ? PlcTransportKind.Tcp : PlcTransportKind.Serial;
            p.DriverId = tcp ? "generic.modbus.tcp" : "generic.modbus.rtu";
            p.SupportLevel = PlcSupportLevel.Experimental;
            p.Notes = notesBox.Text.Trim();
            if (p.Notes.Length == 0) p.Notes = "Perfil personalizado criado no OpenLadder Studio.";

            ResultProfile = p;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void AddCaption(string text, int left, int top)
        {
            Label l = LabelStyle(text, 8.2f, false, Muted);
            l.Location = new Point(left, top);
            Controls.Add(l);
        }

        private TextBox TextBoxStyle(int left, int top, int width)
        {
            TextBox t = new TextBox();
            t.Location = new Point(left, top);
            t.Size = new Size(width, 28);
            t.BackColor = PanelColor;
            t.ForeColor = Fore;
            t.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(t);
            return t;
        }

        private Button ButtonStyle(string text, int left, int top, int width, Color back)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 36);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = back == Accent ? Accent : Border;
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private Label LabelStyle(string text, float size, bool bold, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = color;
            l.Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            return l;
        }
    }
}
