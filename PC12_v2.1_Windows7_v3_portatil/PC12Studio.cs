using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class UnifiedProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UnifiedStudioForm());
        }
    }

    internal sealed class UnifiedStudioForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color NavyLight = Color.FromArgb(27, 55, 86);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);

        private readonly string baseDir;
        private readonly string legacyPath;
        private Panel host;
        private Label pageTitle;
        private Label pageSubTitle;
        private Label footerStatus;
        private StudioNavButton navHome;
        private StudioNavButton navLadder;
        private StudioNavButton navBridge;
        private StudioNavButton navReader;
        private StudioNavButton navLegacy;
        private StudioNavButton navAbout;
        private LadderEditorForm ladderForm;
        private TP02BridgeForm bridgeForm;
        private TP02ProgramReaderForm readerForm;

        public UnifiedStudioForm()
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            legacyPath = Path.Combine(baseDir, "pc12.exe");

            Text = "PC12 Studio TP02";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1120, 700);
            Size = new Size(1380, 850);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            BuildShell();
            ShowHome();
        }

        private void BuildShell()
        {
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 58;
            top.BackColor = Color.White;
            Controls.Add(top);

            Label brand = new Label();
            brand.Text = "PC12 STUDIO";
            brand.AutoSize = true;
            brand.Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold);
            brand.ForeColor = Navy;
            brand.Location = new Point(22, 11);
            top.Controls.Add(brand);

            Label version = new Label();
            version.Text = "TP02 • v0.4 • Windows 7+";
            version.AutoSize = true;
            version.Font = new Font("Segoe UI", 8.6f);
            version.ForeColor = TextSecondary;
            version.Location = new Point(24, 35);
            top.Controls.Add(version);

            Label safe = new Label();
            safe.Text = "Comunicação moderna em modo seguro: somente leitura";
            safe.AutoSize = false;
            safe.Dock = DockStyle.Right;
            safe.Width = 390;
            safe.TextAlign = ContentAlignment.MiddleRight;
            safe.Padding = new Padding(0, 0, 22, 0);
            safe.Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold);
            safe.ForeColor = Success;
            top.Controls.Add(safe);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 30;
            footer.BackColor = Color.White;
            Controls.Add(footer);

            footerStatus = new Label();
            footerStatus.Dock = DockStyle.Fill;
            footerStatus.TextAlign = ContentAlignment.MiddleLeft;
            footerStatus.Padding = new Padding(16, 0, 0, 0);
            footerStatus.ForeColor = TextSecondary;
            footerStatus.Text = "Pronto";
            footer.Controls.Add(footerStatus);

            Label compat = new Label();
            compat.Dock = DockStyle.Right;
            compat.Width = 250;
            compat.TextAlign = ContentAlignment.MiddleRight;
            compat.Padding = new Padding(0, 0, 16, 0);
            compat.ForeColor = TextSecondary;
            compat.Text = "Base mínima: Windows 7 SP1";
            footer.Controls.Add(compat);

            Panel shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = Canvas;
            Controls.Add(shell);

            Panel side = new Panel();
            side.Dock = DockStyle.Left;
            side.Width = 216;
            side.BackColor = Navy;
            shell.Controls.Add(side);

            Label section = new Label();
            section.Text = "AMBIENTE TP02";
            section.AutoSize = true;
            section.Font = new Font("Segoe UI Semibold", 8.7f, FontStyle.Bold);
            section.ForeColor = Color.FromArgb(160, 184, 207);
            section.Location = new Point(20, 22);
            side.Controls.Add(section);

            Label product = new Label();
            product.Text = "PC12 Studio";
            product.AutoSize = true;
            product.Font = new Font("Segoe UI Semibold", 18.0f, FontStyle.Bold);
            product.ForeColor = Color.White;
            product.Location = new Point(18, 43);
            side.Controls.Add(product);

            Label productSub = new Label();
            productSub.Text = "Ladder + Bridge + leitor de programa";
            productSub.AutoSize = true;
            productSub.Font = new Font("Segoe UI", 8.2f);
            productSub.ForeColor = Color.FromArgb(175, 195, 215);
            productSub.Location = new Point(20, 78);
            side.Controls.Add(productSub);

            navHome = AddNav(side, "Visão geral", 118, delegate { ShowHome(); });
            navLadder = AddNav(side, "Editor Ladder", 164, delegate { ShowLadder(); });
            navBridge = AddNav(side, "Comunicação TP02", 210, delegate { ShowBridge(); });
            navReader = AddNav(side, "Ler programa (RBP)", 256, delegate { ShowReader(); });
            navLegacy = AddNav(side, "PC12 original", 302, delegate { ShowLegacyPage(); });
            navAbout = AddNav(side, "Sobre", 348, delegate { ShowAbout(); });

            Panel sideBottom = new Panel();
            sideBottom.Dock = DockStyle.Bottom;
            sideBottom.Height = 72;
            sideBottom.BackColor = NavyLight;
            side.Controls.Add(sideBottom);

            Label sideStatus = new Label();
            sideStatus.AutoSize = false;
            sideStatus.Size = new Size(184, 48);
            sideStatus.Location = new Point(18, 12);
            sideStatus.ForeColor = Color.White;
            sideStatus.Font = new Font("Segoe UI", 8.3f);
            sideStatus.Text = File.Exists(legacyPath) ? "● PC12 legado encontrado\r\n● Studio pronto para uso" : "● PC12 legado não encontrado\r\n● Studio moderno disponível";
            sideBottom.Controls.Add(sideStatus);

            Panel main = new Panel();
            main.Dock = DockStyle.Fill;
            main.BackColor = Canvas;
            shell.Controls.Add(main);

            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 74;
            titleBar.BackColor = Canvas;
            main.Controls.Add(titleBar);

            pageTitle = new Label();
            pageTitle.AutoSize = true;
            pageTitle.Font = new Font("Segoe UI Semibold", 20.0f, FontStyle.Bold);
            pageTitle.ForeColor = TextPrimary;
            pageTitle.Location = new Point(24, 14);
            titleBar.Controls.Add(pageTitle);

            pageSubTitle = new Label();
            pageSubTitle.AutoSize = true;
            pageSubTitle.Font = new Font("Segoe UI", 8.9f);
            pageSubTitle.ForeColor = TextSecondary;
            pageSubTitle.Location = new Point(26, 47);
            titleBar.Controls.Add(pageSubTitle);

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Canvas;
            host.Padding = new Padding(18);
            main.Controls.Add(host);

            titleBar.BringToFront();
            side.BringToFront();
            top.BringToFront();
            footer.BringToFront();
        }

        private StudioNavButton AddNav(Control parent, string text, int top, EventHandler handler)
        {
            StudioNavButton b = new StudioNavButton();
            b.Text = text;
            b.Location = new Point(10, top);
            b.Size = new Size(196, 38);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(14, 0, 0, 0);
            b.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
            b.NormalColor = Navy;
            b.HoverColor = NavyLight;
            b.ForeColor = Color.FromArgb(228, 236, 245);
            b.Click += handler;
            parent.Controls.Add(b);
            return b;
        }

        private void SetActive(StudioNavButton active)
        {
            StudioNavButton[] all = new StudioNavButton[] { navHome, navLadder, navBridge, navReader, navLegacy, navAbout };
            int i;
            for (i = 0; i < all.Length; i++)
            {
                all[i].NormalColor = Navy;
                all[i].BackColor = Navy;
            }
            active.NormalColor = NavyLight;
            active.BackColor = NavyLight;
        }

        private void PreparePage(string title, string subtitle, StudioNavButton active)
        {
            HideEmbeddedForms();
            host.Controls.Clear();
            host.Padding = new Padding(18);
            pageTitle.Text = title;
            pageSubTitle.Text = subtitle;
            SetActive(active);
        }

        private void HideEmbeddedForms()
        {
            if (ladderForm != null && !ladderForm.IsDisposed) ladderForm.Hide();
            if (bridgeForm != null && !bridgeForm.IsDisposed) bridgeForm.Hide();
            if (readerForm != null && !readerForm.IsDisposed) readerForm.Hide();
        }

        private void ShowHome()
        {
            PreparePage("Visão geral", "Um único ambiente para editar, diagnosticar e manter projetos do TP02.", navHome);

            Panel card1 = NewCard(18, 18, 330, 194);
            host.Controls.Add(card1);
            AddCardText(card1, "Editor Ladder", "Crie e edite rungs com contatos, bobinas, temporizadores, contadores, SET/RESET, bordas e funções especiais.");
            Button openLadder = PrimaryButton("ABRIR LADDER", 20, 132, 142);
            openLadder.Click += delegate { ShowLadder(); };
            card1.Controls.Add(openLadder);

            Panel card2 = NewCard(366, 18, 330, 194);
            host.Controls.Add(card2);
            AddCardText(card2, "TP02 Bridge", "Teste a porta serial, leia o status do PLC e analise os arquivos nativos do PC12 sem enviar comandos de escrita.");
            Button openBridge = PrimaryButton("ABRIR BRIDGE", 20, 132, 142);
            openBridge.Click += delegate { ShowBridge(); };
            card2.Controls.Add(openBridge);

            Panel card3 = NewCard(714, 18, 330, 194);
            host.Controls.Add(card3);
            AddCardText(card3, "PC12 original", "Mantenha acesso ao software legado enquanto a nova implementação ganha compatibilidade completa.");
            Button openLegacy = SecondaryButton("ABRIR PC12", 20, 132, 142);
            openLegacy.Click += delegate { LaunchLegacy(); };
            card3.Controls.Add(openLegacy);

            Panel rbp = NewCard(18, 232, 504, 194);
            host.Controls.Add(rbp);
            AddCardText(rbp, "Leitor de programa RBP", "Leia até 100 passos da memória de programa do TP02 e visualize cada instrução de máquina como uma palavra de 3 bytes.");
            Button openReader = PrimaryButton("LER PROGRAMA", 20, 132, 150);
            openReader.Click += delegate { ShowReader(); };
            rbp.Controls.Add(openReader);

            Panel status = NewCard(540, 232, 504, 194);
            host.Controls.Add(status);
            Label stTitle = NewLabel("Situação da modernização", 13.5f, FontStyle.Bold, TextPrimary, 20, 18);
            status.Controls.Add(stTitle);
            Label st = NewLabel("✓ Interface unificada\r\n✓ Editor Ladder moderno\r\n✓ Bridge serial somente leitura\r\n✓ Leitura RBP da memória de programa\r\n○ Decodificação automática RBP → Ladder em desenvolvimento", 9.2f, FontStyle.Regular, TextSecondary, 22, 50);
            status.Controls.Add(st);

            footerStatus.Text = "Visão geral carregada.";
        }

        private void ShowLadder()
        {
            PreparePage("Editor Ladder", "Editor moderno integrado ao PC12 Studio.", navLadder);
            if (ladderForm == null || ladderForm.IsDisposed) ladderForm = new LadderEditorForm();
            EmbedForm(ladderForm);
            footerStatus.Text = "Editor Ladder integrado.";
        }

        private void ShowBridge()
        {
            PreparePage("Comunicação e diagnóstico", "TP02 Bridge Lab integrado: projeto PC12 + comunicação serial somente leitura.", navBridge);
            if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
            EmbedForm(bridgeForm);
            footerStatus.Text = "Bridge TP02 em modo somente leitura.";
        }

        private void ShowReader()
        {
            PreparePage("Leitura do programa", "Comando RBP: leitura da memória de programa do TP02 em palavras de máquina de 3 bytes.", navReader);
            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            EmbedForm(readerForm);
            footerStatus.Text = "Leitor RBP ativo — nenhum comando de escrita habilitado.";
        }

        private void EmbedForm(Form child)
        {
            child.TopLevel = false;
            child.FormBorderStyle = FormBorderStyle.None;
            child.Dock = DockStyle.Fill;
            child.StartPosition = FormStartPosition.Manual;
            host.Padding = new Padding(0);
            host.Controls.Add(child);
            child.Show();
            child.BringToFront();
        }

        private void ShowLegacyPage()
        {
            PreparePage("PC12 original", "Acesso direto ao software legado preservado para compatibilidade e contingência.", navLegacy);

            Panel card = NewCard(18, 18, 720, 238);
            host.Controls.Add(card);
            AddCardText(card, "PC12 Design Center 2.1", File.Exists(legacyPath) ? "O executável original foi encontrado no pacote. Ele continua disponível enquanto validamos importação, protocolo e gravação no TP02." : "O executável pc12.exe não foi localizado nesta pasta.");

            Button run = PrimaryButton("INICIAR PC12 ORIGINAL", 20, 132, 190);
            run.Click += delegate { LaunchLegacy(); };
            run.Enabled = File.Exists(legacyPath);
            card.Controls.Add(run);

            Button folder = SecondaryButton("ABRIR PASTA", 224, 132, 150);
            folder.Click += delegate { OpenFolder(); };
            card.Controls.Add(folder);

            Label note = NewLabel("O Studio não substitui ainda a compilação oficial nem a transferência de programa realizada pelo PC12 original.", 8.7f, FontStyle.Regular, TextSecondary, 22, 186);
            note.MaximumSize = new Size(660, 0);
            card.Controls.Add(note);

            footerStatus.Text = File.Exists(legacyPath) ? "PC12 original disponível." : "PC12 original não encontrado.";
        }

        private void ShowAbout()
        {
            PreparePage("Sobre", "Arquitetura de transição do PC12 para uma ferramenta moderna do TP02.", navAbout);

            Panel card = NewCard(18, 18, 820, 320);
            host.Controls.Add(card);
            Label title = NewLabel("PC12 Studio TP02", 18.0f, FontStyle.Bold, Navy, 22, 20);
            card.Controls.Add(title);
            Label text = NewLabel("Versão de desenvolvimento 0.4\r\n\r\nObjetivo: manter compatibilidade com Windows 7 SP1 e versões posteriores, modernizar o editor Ladder, reproduzir com segurança o formato de projeto do PC12 e implementar comunicação direta com o WEG TP02.\r\n\r\nO Studio já possui leitura RBP do programa em linguagem de máquina. A próxima etapa é mapear as palavras de 3 bytes para instruções Boolean/IL e reconstruir automaticamente o Ladder.\r\n\r\nComandos que possam alterar RUN/STOP, programa ou memória do PLC permanecem desabilitados nas ferramentas modernas.", 9.3f, FontStyle.Regular, TextSecondary, 24, 64);
            text.MaximumSize = new Size(760, 0);
            card.Controls.Add(text);
            footerStatus.Text = "PC12 Studio TP02 v0.4.";
        }

        private void LaunchLegacy()
        {
            if (!File.Exists(legacyPath))
            {
                MessageBox.Show("pc12.exe não foi encontrado na pasta do Studio.", "PC12 Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                Process.Start(legacyPath);
                footerStatus.Text = "PC12 original iniciado.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível iniciar o PC12 original.\r\n\r\n" + ex.Message, "PC12 Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenFolder()
        {
            try { Process.Start("explorer.exe", baseDir); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "PC12 Studio", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private Panel NewCard(int left, int top, int width, int height)
        {
            Panel p = new Panel();
            p.Location = new Point(left, top);
            p.Size = new Size(width, height);
            p.BackColor = Color.White;
            p.BorderStyle = BorderStyle.FixedSingle;
            return p;
        }

        private void AddCardText(Control parent, string title, string text)
        {
            Label t = NewLabel(title, 13.5f, FontStyle.Bold, TextPrimary, 20, 18);
            parent.Controls.Add(t);
            Label d = NewLabel(text, 8.9f, FontStyle.Regular, TextSecondary, 22, 54);
            d.MaximumSize = new Size(parent.Width - 44, 66);
            parent.Controls.Add(d);
        }

        private Label NewLabel(string text, float size, FontStyle style, Color color, int left, int top)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.Location = new Point(left, top);
            return l;
        }

        private Button PrimaryButton(string text, int left, int top, int width)
        {
            Button b = NewButton(text, left, top, width);
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private Button SecondaryButton(string text, int left, int top, int width)
        {
            Button b = NewButton(text, left, top, width);
            b.BackColor = Color.White;
            b.ForeColor = Navy;
            b.FlatAppearance.BorderColor = Color.FromArgb(195, 207, 220);
            return b;
        }

        private Button NewButton(string text, int left, int top, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 36);
            b.FlatStyle = FlatStyle.Flat;
            b.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (ladderForm != null && !ladderForm.IsDisposed) ladderForm.Dispose();
            if (bridgeForm != null && !bridgeForm.IsDisposed) bridgeForm.Dispose();
            if (readerForm != null && !readerForm.IsDisposed) readerForm.Dispose();
            base.OnFormClosing(e);
        }
    }

    internal sealed class StudioNavButton : Button
    {
        public Color NormalColor = Color.White;
        public Color HoverColor = Color.Gainsboro;

        public StudioNavButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            MouseEnter += delegate { BackColor = HoverColor; };
            MouseLeave += delegate { BackColor = NormalColor; };
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            BackColor = NormalColor;
        }
    }
}
