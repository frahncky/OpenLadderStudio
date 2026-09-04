using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class MemoryMapManagerProgram
    {
        [STAThread]
        private static void Main()
        {
            StudioDiagnostics.Install();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PlcMemoryMapManagerForm());
        }
    }

    internal sealed class PlcMemoryMapManagerForm : Form
    {
        private readonly Color Shell = Color.FromArgb(29, 31, 34);
        private readonly Color Chrome = Color.FromArgb(37, 39, 43);
        private readonly Color PanelColor = Color.FromArgb(47, 50, 55);
        private readonly Color Border = Color.FromArgb(61, 64, 69);
        private readonly Color Accent = Color.FromArgb(45, 170, 107);
        private readonly Color Fore = Color.FromArgb(226, 230, 234);
        private readonly Color Muted = Color.FromArgb(150, 157, 164);

        private PlcDeviceProfile profile;
        private DataGridView grid;
        private Label profileLabel;
        private Label statusLabel;

        public PlcMemoryMapManagerForm()
        {
            profile = PlcProfileStore.Load();
            Text = "OpenLadder Studio - Mapa de memória";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 620);
            MinimumSize = new Size(880, 520);
            BackColor = Shell;
            ForeColor = Fore;
            Font = new Font("Segoe UI", 9.0f);
            BuildUi();
            LoadMap();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 76;
            header.BackColor = Chrome;
            Controls.Add(header);

            Label title = NewLabel("Mapa de memória do controlador", 17.0f, true, Fore);
            title.Location = new Point(20, 10);
            header.Controls.Add(title);

            profileLabel = NewLabel("-", 8.8f, true, Accent);
            profileLabel.Location = new Point(22, 42);
            header.Controls.Add(profileLabel);

            Label hint = NewLabel("Defina áreas, endereços e prefixos usados pelo monitor e por futuros drivers.", 8.2f, false, Muted);
            hint.Location = new Point(22, 59);
            header.Controls.Add(hint);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 62;
            footer.BackColor = Chrome;
            Controls.Add(footer);

            Button add = ButtonStyle("ADICIONAR ÁREA", 14, 13, 140, PanelColor);
            add.Click += delegate { AddRow(null); };
            footer.Controls.Add(add);

            Button remove = ButtonStyle("REMOVER", 164, 13, 110, PanelColor);
            remove.Click += RemoveSelected;
            footer.Controls.Add(remove);

            Button defaults = ButtonStyle("RESTAURAR PADRÃO", 284, 13, 150, PanelColor);
            defaults.Click += RestoreDefaults;
            footer.Controls.Add(defaults);

            Button save = ButtonStyle("SALVAR MAPA", 444, 13, 140, Accent);
            save.Click += SaveMap;
            footer.Controls.Add(save);

            statusLabel = NewLabel("Pronto", 8.3f, false, Muted);
            statusLabel.Location = new Point(600, 23);
            statusLabel.MaximumSize = new Size(240, 34);
            footer.Controls.Add(statusLabel);

            Button close = ButtonStyle("FECHAR", 850, 13, 110, PanelColor);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Click += delegate { Close(); };
            footer.Controls.Add(close);
            footer.Resize += delegate { close.Left = footer.ClientSize.Width - close.Width - 14; };

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Shell;
            grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
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

            grid.Columns.Add("name", "Nome da área");
            DataGridViewComboBoxColumn kind = new DataGridViewComboBoxColumn();
            kind.Name = "kind";
            kind.HeaderText = "Tipo";
            kind.Items.AddRange(new object[] { "Coil", "DiscreteInput", "HoldingRegister", "InputRegister", "VendorSpecific" });
            grid.Columns.Add(kind);
            grid.Columns.Add("start", "Endereço inicial");
            grid.Columns.Add("length", "Tamanho");
            grid.Columns.Add("prefix", "Prefixo");
            grid.Columns.Add("notes", "Observação");
            Controls.Add(grid);

            grid.BringToFront();
            header.BringToFront();
            footer.BringToFront();
        }

        private void LoadMap()
        {
            grid.Rows.Clear();
            profile = PlcProfileStore.Load();
            profileLabel.Text = profile == null ? "Perfil atual: genérico" : "Perfil atual: " + profile.Manufacturer + " " + profile.Model + " • " + profile.Protocol;
            List<PlcMemoryArea> areas = PlcMemoryMapStore.Load(profile);
            for (int i = 0; i < areas.Count; i++) AddRow(areas[i]);
            statusLabel.Text = areas.Count.ToString() + " área(s) carregada(s).";
        }

        private void AddRow(PlcMemoryArea area)
        {
            PlcMemoryArea a = area ?? new PlcMemoryArea();
            int row = grid.Rows.Add(a.Name, a.Kind.ToString(), a.StartAddress.ToString(), a.Length.ToString(), a.Prefix, a.Notes);
            grid.Rows[row].Tag = a;
        }

        private void RemoveSelected(object sender, EventArgs e)
        {
            if (grid.CurrentRow == null) return;
            grid.Rows.Remove(grid.CurrentRow);
            statusLabel.Text = "Área removida da edição.";
        }

        private void RestoreDefaults(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Restaurar o mapa padrão do perfil atual?", "Mapa de memória", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            grid.Rows.Clear();
            List<PlcMemoryArea> defaults = PlcMemoryMapStore.CreateDefaults(profile);
            for (int i = 0; i < defaults.Count; i++) AddRow(defaults[i]);
            statusLabel.Text = "Mapa padrão carregado. Clique em SALVAR MAPA.";
        }

        private void SaveMap(object sender, EventArgs e)
        {
            try
            {
                List<PlcMemoryArea> areas = new List<PlcMemoryArea>();
                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    DataGridViewRow row = grid.Rows[i];
                    PlcMemoryArea a = new PlcMemoryArea();
                    a.Name = Cell(row, "name", "Área " + (i + 1).ToString());
                    PlcMemoryAreaKind kind;
                    if (!Enum.TryParse<PlcMemoryAreaKind>(Cell(row, "kind", "HoldingRegister"), true, out kind)) kind = PlcMemoryAreaKind.HoldingRegister;
                    a.Kind = kind;
                    a.StartAddress = ParseNumber(Cell(row, "start", "0"), 0, 65535, "Endereço inicial");
                    a.Length = ParseNumber(Cell(row, "length", "1"), 1, 2000, "Tamanho");
                    a.Prefix = Cell(row, "prefix", string.Empty);
                    a.Notes = Cell(row, "notes", string.Empty);
                    areas.Add(a);
                }
                PlcMemoryMapStore.Save(profile, areas);
                statusLabel.Text = "Mapa salvo para o controlador atual.";
                statusLabel.ForeColor = Accent;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erro: " + ex.Message;
                statusLabel.ForeColor = Color.FromArgb(220, 105, 105);
            }
        }

        private int ParseNumber(string text, int min, int max, string name)
        {
            int value;
            if (!int.TryParse(text, out value) || value < min || value > max)
                throw new InvalidOperationException(name + " inválido: " + text);
            return value;
        }

        private string Cell(DataGridViewRow row, string column, string fallback)
        {
            object value = row.Cells[column].Value;
            string text = value == null ? string.Empty : value.ToString().Trim();
            return text.Length == 0 ? fallback : text;
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
    }
}
