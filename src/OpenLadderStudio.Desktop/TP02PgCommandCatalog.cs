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
            Text = "OpenLadder Studio - Comandos PG do PC12";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 520);
            Size = new Size(1080, 650);
            BackColor = Color.FromArgb(235, 238, 241);

            Label title = new Label();
            title.Text = "INVENTÁRIO DO PROTOCOLO PG - WEG TP02";
            title.Font = new Font("Segoe UI", 14.0f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(30, 54, 78);
            title.AutoSize = true;
            title.Location = new Point(18, 16);
            Controls.Add(title);

            Label note = new Label();
            note.Text = "Extraído do PC12 2.1. A coluna TX indica apenas comandos já autorizados pelos guardrails do OpenLadder.";
            note.Font = new Font("Segoe UI", 9.0f);
            note.ForeColor = Color.FromArgb(80, 88, 96);
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
            grid.BackgroundColor = Color.White;
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
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 54, 78);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grid.EnableHeadersVisualStyles = false;

            foreach (Tp02PgProtocol.CommandInfo info in Tp02PgProtocol.GetCommandCatalog())
            {
                int row = grid.Rows.Add(info.Code, info.Frame, info.Function, info.Evidence,
                    info.TransmitAllowed ? "LIBERADO" : "BLOQUEADO");
                grid.Rows[row].DefaultCellStyle.BackColor = info.TransmitAllowed
                    ? Color.FromArgb(230, 246, 237) : Color.FromArgb(249, 236, 236);
            }
            Controls.Add(grid);

            Button close = new Button();
            close.Text = "Fechar";
            close.Size = new Size(100, 30);
            close.Location = new Point(ClientSize.Width - 120, ClientSize.Height - 42);
            close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            close.DialogResult = DialogResult.OK;
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }
    }
}
