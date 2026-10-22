using System;
using System.Drawing;
using System.Windows.Forms;
using OpenLadderStudio.Core;

namespace ModernPC12
{
    internal sealed class TP02PgCommandCatalogForm : Form
    {
        internal TP02PgCommandCatalogForm()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6.0f, 13.0f);
            Text = "OpenLadder Studio - Comandos PG do PC12";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 520);
            Size = new Size(1080, 650);
            BackColor = OpenLadderPalette.Shell;
            ForeColor = OpenLadderPalette.Fore;

            Label title = new Label();
            title.Text = "INVENTÁRIO DO PROTOCOLO PG - WEG TP02";
            title.Font = new Font("Segoe UI", 14.0f, FontStyle.Bold);
            title.ForeColor = OpenLadderPalette.Fore;
            title.AutoSize = true;
            title.Location = new Point(18, 16);
            Controls.Add(title);

            Label note = new Label();
            note.Text = "Extraído do PC12 2.1. A coluna TX mostra a disponibilidade de transmissão no OpenLadder.";
            note.Font = new Font("Segoe UI", 9.0f);
            note.ForeColor = OpenLadderPalette.Muted;
            note.AutoSize = true;
            note.Location = new Point(20, 48);
            Controls.Add(note);

            DataGridView grid = new DataGridView();
            grid.Location = new Point(20, 78);
            grid.Size = new Size(ClientSize.Width - 40, ClientSize.Height - 130);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = OpenLadderPalette.ChromeLight;
            grid.DefaultCellStyle.BackColor = OpenLadderPalette.ChromeLight;
            grid.DefaultCellStyle.ForeColor = OpenLadderPalette.Fore;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.Columns.Add("Code", "CMD");
            grid.Columns.Add("Frame", "QUADRO / FORMATO");
            grid.Columns.Add("Function", "FUNÇÃO");
            grid.Columns.Add("Evidence", "EVIDÊNCIA");
            grid.Columns.Add("Tx", "TX");
            grid.Columns[0].FillWeight = 35;
            grid.Columns[1].FillWeight = 95;
            grid.Columns[2].FillWeight = 120;
            grid.Columns[3].FillWeight = 155;
            grid.Columns[4].FillWeight = 35;
            grid.ColumnHeadersDefaultCellStyle.BackColor = OpenLadderPalette.Chrome;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = OpenLadderPalette.Fore;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.EnableHeadersVisualStyles = false;

            foreach (Tp02PgProtocol.CommandInfo info in Tp02PgProtocol.GetCommandCatalog())
            {
                int row = grid.Rows.Add(info.Code, info.Frame, info.Function, info.Evidence,
                    info.TransmitAllowed ? "LIBERADO" : "BLOQUEADO");
                grid.Rows[row].Cells[4].Style.ForeColor = info.TransmitAllowed
                    ? OpenLadderPalette.Ok : OpenLadderPalette.Muted;
            }
            Controls.Add(grid);

            Button memory = new Button();
            memory.Text = "Memória e relógio…";
            memory.Size = new Size(180, 30);
            memory.Location = new Point(20, ClientSize.Height - 42);
            memory.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            memory.FlatStyle = FlatStyle.Flat;
            memory.FlatAppearance.BorderSize = 0;
            memory.BackColor = OpenLadderPalette.Accent;
            memory.ForeColor = OpenLadderPalette.OnAccent;
            memory.Click += delegate { using (TP02PgMemoryForm form = new TP02PgMemoryForm()) form.ShowDialog(this); };
            Controls.Add(memory);

            Button close = new Button();
            close.Text = "Fechar";
            close.Size = new Size(100, 30);
            close.Location = new Point(ClientSize.Width - 120, ClientSize.Height - 42);
            close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderColor = OpenLadderPalette.Border;
            close.BackColor = OpenLadderPalette.Chrome;
            close.ForeColor = OpenLadderPalette.Fore;
            close.DialogResult = DialogResult.OK;
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }
    }
}
