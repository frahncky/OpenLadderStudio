using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class UnifiedProgramV07
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UnifiedStudioV07Form());
        }
    }

    internal sealed class UnifiedStudioV07Form : Form
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
        private StudioNavButton navDecoder;
        private StudioNavButton navCampaign;
        private StudioNavButton navAuto;
        private StudioNavButton navLegacy;
        private StudioNavButton navAbout;

        private LadderEditorForm ladderForm;
        private TP02BridgeForm bridgeForm;
        private TP02ProgramReaderForm readerForm;
        private TP02MachineDecoderForm decoderForm;
        private TP02CalibrationCampaignForm campaignForm;
        private TP02AutoDecoderForm autoForm;

        public UnifiedStudioV07Form()
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            legacyPath = Path.Combine(baseDir, "pc12.exe");
            Text = "PC12 Studio TP02";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1160, 720);
            Size = new Size(1440, 880);
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
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

            Label brand = NewLabel("PC12 STUDIO", 13.5f, FontStyle.Bold, Navy, 22, 10);
            top.Controls.Add(brand);
            Label version = NewLabel("TP02 • v0.7 • Windows 7+", 8.6f, FontStyle.Regular, TextSecondary, 24, 34);
            top.Controls.Add(version);

            Label safe = new Label();
            safe.Text = "Ferramentas modernas: leitura e análise segura";
            safe.Dock = DockStyle.Right;
            safe.Width = 390;
            safe.Padding = new Padding(0, 0, 22, 0);
            safe.TextAlign = ContentAlignment.MiddleRight;
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
            footerStatus.Padding = new Padding(16, 0, 0, 0);
            footerStatus.TextAlign = ContentAlignment.MiddleLeft;
            footerStatus.ForeColor = TextSecondary;
            footerStatus.Text = "Pronto";
            footer.Controls.Add(footerStatus);

            Label compat = new Label();
            compat.Dock = DockStyle.Right;
            compat.Width = 250;
            compat.Padding = new Padding(0, 0, 16, 0);
            compat.TextAlign = ContentAlignment.MiddleRight;
            compat.ForeColor = TextSecondary;
            compat.Text = "Base mínima: Windows 7 SP1";
            footer.Controls.Add(compat);

            Panel shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = Canvas;
            Controls.Add(shell);

            Panel side = new Panel();
            side.Dock = DockStyle.Left;
            side.Width = 224;
            side.BackColor = Navy;
            shell.Controls.Add(side);

            side.Controls.Add(NewLabel("AMBIENTE TP02", 8.7f, FontStyle.Bold, Color.FromArgb(160, 184, 207), 20, 20));
            side.Controls.Add(NewLabel("PC12 Studio", 18.0f, FontStyle.Bold, Color.White, 18, 41));
            side.Controls.Add(NewLabel("Ladder • RBP • calibração", 8.2f, FontStyle.Regular, Color.FromArgb(175, 195, 215), 20, 77));

            navHome = AddNav(side, "Visão geral", 112, delegate { ShowHome(); });
            navLadder = AddNav(side, "Editor Ladder", 154, delegate { ShowLadder(); });
            navBridge = AddNav(side, "Comunicação TP02", 196, delegate { ShowBridge(); });
            navReader = AddNav(side, "Ler programa (RBP)", 238, delegate { ShowReader(); });
            navDecoder = AddNav(side, "Decoder manual", 280, delegate { ShowDecoder(); });
            navCampaign = AddNav(side, "Campanha de calibração", 322, delegate { ShowCampaign(); });
            navAuto = AddNav(side, "Decodificação automática", 364, delegate { ShowAuto(); });
            navLegacy = AddNav(side, "PC12 original", 406, delegate { ShowLegacyPage(); });
            navAbout = AddNav(side, "Sobre", 448, delegate { ShowAbout(); });

            Panel sideBottom = new Panel();
            sideBottom.Dock = DockStyle.Bottom;
            sideBottom.Height = 76;
            sideBottom.BackColor = NavyLight;
            side.Controls.Add(sideBottom);

            Label state = new Label();
            state.Dock = DockStyle.Fill;
            state.Padding = new Padding(18, 12, 12, 0);
            state.ForeColor = Color.White;
            state.Font = new Font("Segoe UI", 8.3f);
            state.Text = File.Exists(legacyPath)
                ? "● PC12 legado encontrado\r\n● Studio v0.7 disponível"
                : "● PC12 legado não encontrado\r\n● Studio v0.7 disponível";
            sideBottom.Controls.Add(state);

            Panel main = new Panel();
            main.Dock = DockStyle.Fill;
            main.BackColor = Canvas;
            shell.Controls.Add(main);

            Panel titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 74;
            titleBar.BackColor = Canvas;
            main.Controls.Add(titleBar);

            pageTitle = NewLabel(string.Empty, 20.0f, FontStyle.Bold, TextPrimary, 24, 14);
            titleBar.Controls.Add(pageTitle);
            pageSubTitle = NewLabel(string.Empty, 8.9f, FontStyle.Regular, TextSecondary, 26, 47);
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
            b.Size = new Size(204, 36);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(14, 0, 0, 0);
            b.Font = new Font("Segoe UI Semibold", 8.9f, FontStyle.Bold);
            b.NormalColor = Navy;
            b.HoverColor = NavyLight;
            b.ForeColor = Color.FromArgb(228, 236, 245);
            b.Click += handler;
            parent.Controls.Add(b);
            return b;
        }

        private void SetActive(StudioNavButton active)
        {
            StudioNavButton[] all = new StudioNavButton[] { navHome, navLadder, navBridge, navReader, navDecoder, navCampaign, navAuto, navLegacy, navAbout };
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
            if (decoderForm != null && !decoderForm.IsDisposed) decoderForm.Hide();
            if (campaignForm != null && !campaignForm.IsDisposed) campaignForm.Hide();
            if (autoForm != null && !autoForm.IsDisposed) autoForm.Hide();
        }

        private void ShowHome()
        {
            PreparePage("Visão geral", "Fluxo completo de modernização e engenharia reversa do WEG TP02.", navHome);

            Panel c1 = NewCard(18, 18, 320, 176);
            host.Controls.Add(c1);
            AddCardText(c1, "Editor Ladder", "Criação e edição moderna dos rungs do TP02.");
            Button b1 = PrimaryButton("ABRIR LADDER", 20, 118, 140);
            b1.Click += delegate { ShowLadder(); };
            c1.Controls.Add(b1);

            Panel c2 = NewCard(356, 18, 320, 176);
            host.Controls.Add(c2);
            AddCardText(c2, "Leitura RBP", "Leitura segura da memória de programa em words de 3 bytes.");
            Button b2 = PrimaryButton("LER PROGRAMA", 20, 118, 140);
            b2.Click += delegate { ShowReader(); };
            c2.Controls.Add(b2);

            Panel c3 = NewCard(694, 18, 320, 176);
            host.Controls.Add(c3);
            AddCardText(c3, "Campanha de calibração", "Organiza A1–E4 e gera regras candidatas de opcode.");
            Button b3 = PrimaryButton("ABRIR CAMPANHA", 20, 118, 155);
            b3.Click += delegate { ShowCampaign(); };
            c3.Controls.Add(b3);

            Panel c4 = NewCard(18, 212, 320, 176);
            host.Controls.Add(c4);
            AddCardText(c4, "Decodificação automática", "Aplica regras CONFIRMED e mantém CANDIDATE apenas como sugestão.");
            Button b4 = PrimaryButton("DECODIFICAR", 20, 118, 140);
            b4.Click += delegate { ShowAuto(); };
            c4.Controls.Add(b4);

            Panel c5 = NewCard(356, 212, 320, 176);
            host.Controls.Add(c5);
            AddCardText(c5, "Bridge TP02", "Diagnóstico serial e análise de arquivos do PC12 em modo seguro.");
            Button b5 = PrimaryButton("ABRIR BRIDGE", 20, 118, 140);
            b5.Click += delegate { ShowBridge(); };
            c5.Controls.Add(b5);

            Panel c6 = NewCard(694, 212, 320, 176);
            host.Controls.Add(c6);
            AddCardText(c6, "PC12 original", "Mantido disponível enquanto o novo Studio não substitui compilação e transferência.");
            Button b6 = SecondaryButton("ABRIR PC12", 20, 118, 140);
            b6.Click += delegate { LaunchLegacy(); };
            b6.Enabled = File.Exists(legacyPath);
            c6.Controls.Add(b6);

            Panel status = NewCard(18, 406, 996, 136);
            host.Controls.Add(status);
            status.Controls.Add(NewLabel("Pipeline atual", 13.5f, FontStyle.Bold, TextPrimary, 20, 18));
            Label text = NewLabel("RBP → campanha → regras CANDIDATE/CONFIRMED → decodificação segura → IL verificada → futura reconstrução Ladder", 9.1f, FontStyle.Regular, TextSecondary, 22, 55);
            text.MaximumSize = new Size(940, 0);
            status.Controls.Add(text);
            Label safe = NewLabel("Nenhuma regra candidata é tratada como fato. Operandos desconhecidos permanecem RAW até a codificação ser comprovada.", 8.8f, FontStyle.Bold, Success, 22, 88);
            safe.MaximumSize = new Size(940, 0);
            status.Controls.Add(safe);
            footerStatus.Text = "PC12 Studio TP02 v0.7 pronto.";
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
            PreparePage("Comunicação e diagnóstico", "TP02 Bridge Lab: comunicação somente leitura e análise dos arquivos nativos.", navBridge);
            if (bridgeForm == null || bridgeForm.IsDisposed) bridgeForm = new TP02BridgeForm();
            EmbedForm(bridgeForm);
            footerStatus.Text = "Bridge TP02 em modo somente leitura.";
        }

        private void ShowReader()
        {
            PreparePage("Leitura do programa", "RBP: leitura da memória de programa em palavras de máquina de 3 bytes.", navReader);
            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            EmbedForm(readerForm);
            footerStatus.Text = "Leitor RBP ativo — nenhum comando de escrita habilitado.";
        }

        private void ShowDecoder()
        {
            PreparePage("Decoder manual", "Mapeamento manual e comparação detalhada de words RBP.", navDecoder);
            if (decoderForm == null || decoderForm.IsDisposed) decoderForm = new TP02MachineDecoderForm();
            EmbedForm(decoderForm);
            footerStatus.Text = "Decoder manual offline ativo.";
        }

        private void ShowCampaign()
        {
            PreparePage("Campanha de calibração", "Organize os testes A1–E4, associe dumps e exporte regras candidatas.", navCampaign);
            if (campaignForm == null || campaignForm.IsDisposed) campaignForm = new TP02CalibrationCampaignForm();
            EmbedForm(campaignForm);
            footerStatus.Text = "Campanha offline ativa — nenhum acesso ao PLC.";
        }

        private void ShowAuto()
        {
            PreparePage("Decodificação automática", "Aplica regras comprovadas e separa claramente sugestões ainda candidatas.", navAuto);
            if (autoForm == null || autoForm.IsDisposed) autoForm = new TP02AutoDecoderForm();
            EmbedForm(autoForm);
            footerStatus.Text = "Auto Decoder offline ativo — CONFIRMED e CANDIDATE permanecem separados.";
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
            PreparePage("PC12 original", "Software legado preservado para compatibilidade e contingência.", navLegacy);
            Panel card = NewCard(18, 18, 760, 244);
            host.Controls.Add(card);
            AddCardText(card, "PC12 Design Center 2.1", File.Exists(legacyPath)
                ? "O executável original foi encontrado. Continue usando-o para compilação e transferência até a validação completa do Studio."
                : "O executável pc12.exe não foi localizado nesta pasta.");
            Button run = PrimaryButton("INICIAR PC12 ORIGINAL", 20, 132, 190);
            run.Click += delegate { LaunchLegacy(); };
            run.Enabled = File.Exists(legacyPath);
            card.Controls.Add(run);
            Button folder = SecondaryButton("ABRIR PASTA", 224, 132, 150);
            folder.Click += delegate { OpenFolder(); };
            card.Controls.Add(folder);
            Label note = NewLabel("O Studio ainda não habilita WBP, RUN, STOP, escrita de registradores ou limpeza de memória.", 8.7f, FontStyle.Regular, TextSecondary, 22, 190);
            note.MaximumSize = new Size(700, 0);
            card.Controls.Add(note);
        }

        private void ShowAbout()
        {
            PreparePage("Sobre", "Arquitetura de transição do PC12 para um ambiente moderno do TP02.", navAbout);
            Panel card = NewCard(18, 18, 850, 355);
            host.Controls.Add(card);
            card.Controls.Add(NewLabel("PC12 Studio TP02", 18.0f, FontStyle.Bold, Navy, 22, 20));
            Label text = NewLabel(
                "Versão de desenvolvimento 0.7\r\n\r\n" +
                "Compatibilidade alvo: Windows 7 SP1 em diante, usando Windows Forms + .NET Framework e sem bibliotecas externas.\r\n\r\n" +
                "A versão 0.7 adiciona um decodificador automático por regras. Regras CONFIRMED podem gerar IL verificada; regras CANDIDATE são exibidas apenas como sugestão. Quando a codificação do operando ainda não foi comprovada, o valor aparece como RAW em hexadecimal.\r\n\r\n" +
                "A reconstrução automática do Ladder só será habilitada quando as instruções e operandos necessários estiverem comprovados.",
                9.3f, FontStyle.Regular, TextSecondary, 24, 64);
            text.MaximumSize = new Size(790, 0);
            card.Controls.Add(text);
            footerStatus.Text = "PC12 Studio TP02 v0.7.";
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
            parent.Controls.Add(NewLabel(title, 13.2f, FontStyle.Bold, TextPrimary, 20, 18));
            Label d = NewLabel(text, 8.8f, FontStyle.Regular, TextSecondary, 22, 54);
            d.MaximumSize = new Size(parent.Width - 44, 58);
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
            if (decoderForm != null && !decoderForm.IsDisposed) decoderForm.Dispose();
            if (campaignForm != null && !campaignForm.IsDisposed) campaignForm.Dispose();
            if (autoForm != null && !autoForm.IsDisposed) autoForm.Dispose();
            base.OnFormClosing(e);
        }
    }
}
