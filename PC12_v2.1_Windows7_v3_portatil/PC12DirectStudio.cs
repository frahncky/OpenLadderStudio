using System;
using System.Drawing;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class DirectStudioProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DirectStudioForm());
        }
    }

    internal sealed class DirectStudioForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color NavyLight = Color.FromArgb(27, 55, 86);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);

        private Panel host;
        private LadderEditorForm ladderForm;
        private TP02BridgeForm bridgeForm;
        private TP02ProgramReaderForm readerForm;
        private TP02AutoDecoderForm decoderForm;
        private TP02CalibrationCampaignForm calibrationForm;
        private TP02IlToLadderForm ilForm;
        private PC12UpdaterForm updaterForm;

        public DirectStudioForm()
        {
            Text = "PC12 Studio TP02";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            Size = new Size(1450, 900);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildUi();
            ShowLadder();
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 46;
            header.BackColor = Color.White;
            Controls.Add(header);

            Label brand = new Label();
            brand.Text = "PC12 STUDIO TP02";
            brand.AutoSize = true;
            brand.Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold);
            brand.ForeColor = Navy;
            brand.Location = new Point(18, 12);
            header.Controls.Add(brand);

            Label version = new Label();
            version.Text = "v0.8";
            version.AutoSize = true;
            version.Font = new Font("Segoe UI", 8.0f);
            version.ForeColor = TextSecondary;
            version.Location = new Point(184, 16);
            header.Controls.Add(version);

            Panel nav = new Panel();
            nav.Dock = DockStyle.Top;
            nav.Height = 40;
            nav.BackColor = Navy;
            Controls.Add(nav);

            int x = 8;
            AddTab(nav, "LADDER", x, 88, delegate { ShowLadder(); }); x += 92;
            AddTab(nav, "COMUNICAÇÃO", x, 112, delegate { ShowBridge(); }); x += 116;
            AddTab(nav, "LER PLC", x, 88, delegate { ShowReader(); }); x += 92;
            AddTab(nav, "DECODIFICAR", x, 110, delegate { ShowDecoder(); }); x += 114;
            AddTab(nav, "CALIBRAÇÃO", x, 108, delegate { ShowCalibration(); }); x += 112;
            AddTab(nav, "IL → LADDER", x, 112, delegate { ShowIl(); }); x += 116;
            AddTab(nav, "ATUALIZAR", x, 104, delegate { ShowUpdater(); });

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Canvas;
            Controls.Add(host);

            nav.BringToFront();
            header.BringToFront();
        }

        private void AddTab(Control parent, string text, int left, int width, EventHandler action)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, 3);
            b.Size = new Size(width, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Navy;
            b.ForeColor = Color.FromArgb(228, 236, 245);
            b.Font = new Font("Segoe UI Semibold", 8.3f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.Click += delegate(object sender, EventArgs e)
            {
                SetActive(b);
                action(sender, e);
            };
            parent.Controls.Add(b);
        }

        private void SetActive(Button button)
        {
            if (button.Parent != null)
            {
                foreach (Control c in button.Parent.Controls)
                {
                    Button b = c as Button;
                    if (b != null) b.BackColor = Navy;
                }
            }
            button.BackColor = NavyLight;
        }

        private void Prepare(Form child)
        {
            HideChildren();
            host.Controls.Clear();
            child.TopLevel = false;
            child.FormBorderStyle = FormBorderStyle.None;
            child.Dock = DockStyle.Fill;
            host.Controls.Add(child);
            child.Show();
            child.BringToFront();
        }

        private void HideChildren()
        {
            if (ladderForm != null && !ladderForm.IsDisposed) ladderForm.Hide();
            if (bridgeForm != null && !bridgeForm.IsDisposed) bridgeForm.Hide();
            if (readerForm != null && !readerForm.IsDisposed) readerForm.Hide();
            if (decoderForm != null && !decoderForm.IsDisposed) decoderForm.Hide();
            if (calibrationForm != null && !calibrationForm.IsDisposed) calibrationForm.Hide();
            if (ilForm != null && !ilForm.IsDisposed) ilForm.Hide();
            if (updaterForm != null && !updaterForm.IsDisposed) updaterForm.Hide();
        }

        private static void HideLabel(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && string.Equals(l.Text, text, StringComparison.OrdinalIgnoreCase)) l.Visible = false;
                if (c.HasChildren) HideLabel(c, text);
            }
        }

        private void ShowLadder()
        {
            if (ladderForm == null || ladderForm.IsDisposed)
            {
                ladderForm = new LadderEditorForm();
                HideLabel(ladderForm, "PC12 LADDER STUDIO");
                HideLabel(ladderForm, "Editor Ladder moderno • WEG TP02");
            }
            Prepare(ladderForm);
            SelectTab("LADDER");
        }

        private void ShowBridge()
        {
            if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
            Prepare(bridgeForm);
            SelectTab("COMUNICAÇÃO");
        }

        private void ShowReader()
        {
            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            Prepare(readerForm);
            SelectTab("LER PLC");
        }

        private void ShowDecoder()
        {
            if (decoderForm == null || decoderForm.IsDisposed) decoderForm = new TP02AutoDecoderForm();
            Prepare(decoderForm);
            SelectTab("DECODIFICAR");
        }

        private void ShowCalibration()
        {
            if (calibrationForm == null || calibrationForm.IsDisposed) calibrationForm = new TP02CalibrationCampaignForm();
            Prepare(calibrationForm);
            SelectTab("CALIBRAÇÃO");
        }

        private void ShowIl()
        {
            if (ilForm == null || ilForm.IsDisposed) ilForm = new TP02IlToLadderForm();
            Prepare(ilForm);
            SelectTab("IL → LADDER");
        }

        private void ShowUpdater()
        {
            if (updaterForm == null || updaterForm.IsDisposed) updaterForm = new PC12UpdaterForm();
            Prepare(updaterForm);
            SelectTab("ATUALIZAR");
        }

        private void SelectTab(string text)
        {
            Control nav = null;
            foreach (Control c in Controls)
            {
                Panel p = c as Panel;
                if (p != null && p.Height == 40 && p.BackColor == Navy) { nav = p; break; }
            }
            if (nav == null) return;
            foreach (Control c in nav.Controls)
            {
                Button b = c as Button;
                if (b != null && string.Equals(b.Text, text, StringComparison.OrdinalIgnoreCase))
                {
                    SetActive(b);
                    break;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Form[] forms = new Form[] { ladderForm, bridgeForm, readerForm, decoderForm, calibrationForm, ilForm, updaterForm };
            int i;
            for (i = 0; i < forms.Length; i++)
                if (forms[i] != null && !forms[i].IsDisposed) forms[i].Dispose();
            base.OnFormClosing(e);
        }
    }
}
