using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Color Navy = Color.FromArgb(18, 39, 63);
        private readonly Color NavyLight = Color.FromArgb(27, 55, 86);
        private readonly Color Accent = Color.FromArgb(0, 122, 204);
        private readonly Color AccentHover = Color.FromArgb(0, 102, 170);
        private readonly Color Canvas = Color.FromArgb(240, 244, 248);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);
        private readonly Color SurfaceMuted = Color.FromArgb(235, 240, 245);
        private readonly Color SurfaceMutedHover = Color.FromArgb(215, 225, 235);
        private readonly Color Border = Color.FromArgb(215, 220, 230);

        private readonly string baseDir;
        private readonly string pc12Path;
        private readonly string lastFileCpuPath;
        private readonly string lastFileDirPath;
        private Panel workspace;
        private Label titleLabel;
        private Label statusLabel;
        private Label statusDot;
        private ModernButton navHome;
        private ModernButton navConnection;
        private ModernButton navTools;
        private ModernButton navHelp;
        private ComboBox portCombo;
        private Label portStatusLabel;
        private Label portHintLabel;
        private const string AppTitle = "PC12 Modern";
        private const int SidebarWidth = 224;
        private const int TopBarHeight = 54;
        private static readonly string[] HelpCandidates = new string[] { "PC12HELP.HLP", "Tp022.hlp", "HELPDLG.HLP" };

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public MainForm()
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            pc12Path = Path.Combine(baseDir, "pc12.exe");
            lastFileCpuPath = Path.Combine(baseDir, "lastfile.cpu");
            lastFileDirPath = Path.Combine(baseDir, "lastfile.dir");

            Text = AppTitle;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 620);
            Size = new Size(1080, 700);
            BackColor = Canvas;
            FormBorderStyle = FormBorderStyle.None;
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            Icon = SystemIcons.Application;

            BuildShell();
            ShowHome();
            UpdateGlobalStatus();
        }

        private void BuildShell()
        {
            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = TopBarHeight;
            topBar.BackColor = Color.White;
            topBar.MouseDown += DragWindow;
            topBar.DoubleClick += ToggleMaximize;
            Controls.Add(topBar);

            Label brand = new Label();
            brand.AutoSize = true;
            brand.Text = "PC12  MODERN";
            brand.Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold);
            brand.ForeColor = Navy;
            brand.Location = new Point(24, 16);
            brand.MouseDown += DragWindow;
            brand.DoubleClick += ToggleMaximize;
            topBar.Controls.Add(brand);

            Label edition = new Label();
            edition.AutoSize = true;
            edition.Text = "Windows 7 compatibility layer";
            edition.Font = new Font("Segoe UI", 8.5f);
            edition.ForeColor = TextSecondary;
            edition.Location = new Point(152, 19);
            edition.MouseDown += DragWindow;
            edition.DoubleClick += ToggleMaximize;
            topBar.Controls.Add(edition);

            FlatWindowButton close = new FlatWindowButton("×");
            close.Dock = DockStyle.Right;
            close.Width = 52;
            close.Click += delegate { Close(); };
            topBar.Controls.Add(close);

            FlatWindowButton maximize = new FlatWindowButton("□");
            maximize.Dock = DockStyle.Right;
            maximize.Width = 48;
            maximize.Click += ToggleMaximize;
            topBar.Controls.Add(maximize);

            FlatWindowButton minimize = new FlatWindowButton("—");
            minimize.Dock = DockStyle.Right;
            minimize.Width = 48;
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            topBar.Controls.Add(minimize);

            Panel shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = Canvas;
            Controls.Add(shell);

            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = SidebarWidth;
            sidebar.BackColor = Navy;
            shell.Controls.Add(sidebar);

            Label product = new Label();
            product.Text = "PLC WORKSPACE";
            product.AutoSize = true;
            product.Font = new Font("Segoe UI Semibold", 9.0f, FontStyle.Bold);
            product.ForeColor = Color.FromArgb(164, 185, 207);
            product.Location = new Point(24, 28);
            sidebar.Controls.Add(product);

            Label productTitle = new Label();
            productTitle.Text = "TP02 / PC12";
            productTitle.AutoSize = true;
            productTitle.Font = new Font("Segoe UI Semibold", 18.0f, FontStyle.Bold);
            productTitle.ForeColor = Color.White;
            productTitle.Location = new Point(22, 50);
            sidebar.Controls.Add(productTitle);

            Label productSub = new Label();
            productSub.Text = "Central de programação e suporte";
            productSub.AutoSize = true;
            productSub.Font = new Font("Segoe UI", 8.4f);
            productSub.ForeColor = Color.FromArgb(171, 191, 212);
            productSub.Location = new Point(24, 86);
            sidebar.Controls.Add(productSub);

            navHome = CreateNavButton("⌂", "Início", 128);
            navHome.Click += delegate { ShowHome(); };
            sidebar.Controls.Add(navHome);

            navConnection = CreateNavButton("⇄", "Conexão", 176);
            navConnection.Click += delegate { ShowConnection(); };
            sidebar.Controls.Add(navConnection);

            navTools = CreateNavButton("⚙", "Ferramentas", 224);
            navTools.Click += delegate { ShowTools(); };
            sidebar.Controls.Add(navTools);

            navHelp = CreateNavButton("ℹ", "Ajuda e informações", 272);
            navHelp.Click += delegate { ShowHelp(); };
            sidebar.Controls.Add(navHelp);

            Panel sideStatus = new Panel();
            sideStatus.Height = 72;
            sideStatus.Dock = DockStyle.Bottom;
            sideStatus.BackColor = NavyLight;
            sidebar.Controls.Add(sideStatus);

            statusDot = new Label();
            statusDot.Text = "●";
            statusDot.AutoSize = true;
            statusDot.Font = new Font("Segoe UI", 10.0f);
            statusDot.ForeColor = Success;
            statusDot.Location = new Point(22, 18);
            sideStatus.Controls.Add(statusDot);

            statusLabel = new Label();
            statusLabel.Text = "Verificando instalação...";
            statusLabel.AutoSize = false;
            statusLabel.Size = new Size(170, 38);
            statusLabel.Font = new Font("Segoe UI", 8.3f);
            statusLabel.ForeColor = Color.White;
            statusLabel.Location = new Point(43, 17);
            sideStatus.Controls.Add(statusLabel);

            Panel contentShell = new Panel();
            contentShell.Dock = DockStyle.Fill;
            contentShell.BackColor = Canvas;
            shell.Controls.Add(contentShell);

            Panel contentHeader = new Panel();
            contentHeader.Dock = DockStyle.Top;
            contentHeader.Height = 84;
            contentHeader.BackColor = Canvas;
            contentShell.Controls.Add(contentHeader);

            titleLabel = new Label();
            titleLabel.Text = "Início";
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI Semibold", 21.0f, FontStyle.Bold);
            titleLabel.ForeColor = TextPrimary;
            titleLabel.Location = new Point(32, 26);
            contentHeader.Controls.Add(titleLabel);

            workspace = new Panel();
            workspace.Dock = DockStyle.Fill;
            workspace.AutoScroll = true;
            workspace.BackColor = Canvas;
            workspace.Padding = new Padding(32, 8, 32, 28);
            contentShell.Controls.Add(workspace);

            contentHeader.BringToFront();
            sidebar.BringToFront();
            topBar.BringToFront();
        }

        private ModernButton CreateNavButton(string glyph, string text, int top)
        {
            ModernButton b = CreateButton(WithGlyph(glyph, text), 14, top, 196, 40, 9.4f, Navy, NavyLight, Color.FromArgb(225, 235, 245));
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(14, 0, 0, 0);
            return b;
        }

        private void SetActiveNav(ModernButton active)
        {
            ModernButton[] all = new ModernButton[] { navHome, navConnection, navTools, navHelp };
            int i;
            for (i = 0; i < all.Length; i++)
            {
                all[i].NormalColor = Navy;
                all[i].ForeColor = Color.FromArgb(225, 235, 245);
            }
            active.NormalColor = NavyLight;
            active.ForeColor = Color.White;
        }

        private void ClearWorkspace(string title, ModernButton active)
        {
            titleLabel.Text = title;
            workspace.Controls.Clear();
            SetActiveNav(active);
        }

        private void ShowHome()
        {
            ClearWorkspace("Visão geral", navHome);

            HeroPanel hero = CreateHeroPanel(34, 14, 732, 188);
            workspace.Controls.Add(hero);

            Label welcome = NewLabel("PC12 com uma experiência mais moderna", 22.0f, FontStyle.Bold, Color.White);
            welcome.Location = new Point(28, 24);
            hero.Controls.Add(welcome);

            Label intro = NewLabel("Centralize inicialização, suporte e diagnóstico do TP02 sem alterar o executável legado do PC12.", 9.6f, FontStyle.Regular, Color.FromArgb(220, 232, 246));
            intro.Location = new Point(30, 62);
            intro.MaximumSize = new Size(470, 0);
            hero.Controls.Add(intro);

            AddSummaryBadge(hero, 30, 104, "✓ Compatível com Windows 7", Color.FromArgb(220, 242, 235), Success);
            AddSummaryBadge(hero, 226, 104, "◆ Sem dependências externas", Color.FromArgb(233, 240, 249), Accent);
            AddSummaryBadge(hero, 468, 104, "↗ Fallback para PC12 clássico", Color.FromArgb(250, 235, 215), Warning);

            ModernButton launchHero = PrimaryButton(WithGlyph("▶", "ABRIR PC12"), 30, 138, 156);
            launchHero.Click += delegate { LaunchPc12(false); };
            hero.Controls.Add(launchHero);

            ModernButton adminHero = SecondaryButton(WithGlyph("▲", "COMO ADMIN"), 198, 138, 150);
            adminHero.Click += delegate { LaunchPc12(true); };
            hero.Controls.Add(adminHero);

            ModernButton connectionHero = SecondaryButton(WithGlyph("⇄", "VER CONEXÃO"), 360, 138, 158);
            connectionHero.Click += delegate { ShowConnection(); };
            hero.Controls.Add(connectionHero);

            Label quickTitle = NewLabel("Acesso rápido", 10.0f, FontStyle.Bold, TextSecondary);
            quickTitle.Location = new Point(36, 220);
            workspace.Controls.Add(quickTitle);

            CardPanel launchCard = CreateCard(34, 248, 356, 190);
            launchCard.AccentColor = Accent;
            workspace.Controls.Add(launchCard);
            AddCardTitle(launchCard, "Programar PLC", "Abra o PC12 Design Center 2.1 mantendo toda a compatibilidade com os projetos existentes.");
            ModernButton launch = PrimaryButton(WithGlyph("▶", "ABRIR PC12"), 24, 128, 150);
            launch.Click += delegate { LaunchPc12(false); };
            launchCard.Controls.Add(launch);

            ModernButton launchAdmin = SecondaryButton(WithGlyph("▲", "COMO ADMIN"), 186, 128, 146);
            launchAdmin.Click += delegate { LaunchPc12(true); };
            launchCard.Controls.Add(launchAdmin);

            CardPanel connectionCard = CreateCard(410, 248, 356, 190);
            connectionCard.AccentColor = Success;
            workspace.Controls.Add(connectionCard);
            AddCardTitle(connectionCard, "Conexão serial", "Veja rapidamente as portas COM disponíveis e acesse as verificações de comunicação com o TP02.");
            ModernButton connection = SecondaryButton(WithGlyph("⇄", "VER CONEXÃO"), 24, 128, 170);
            connection.Click += delegate { ShowConnection(); };
            connectionCard.Controls.Add(connection);

            Label diagnosticsTitle = NewLabel("Estado do ambiente", 10.0f, FontStyle.Bold, TextSecondary);
            diagnosticsTitle.Location = new Point(36, 456);
            workspace.Controls.Add(diagnosticsTitle);

            CardPanel healthCard = CreateCard(34, 484, 732, 214);
            healthCard.AccentColor = Warning;
            workspace.Controls.Add(healthCard);
            AddCardTitle(healthCard, "Diagnóstico rápido", "Situação do pacote portátil e recursos necessários para iniciar o software.");

            bool exeOk = File.Exists(pc12Path);
            string ports = GetPortsSummary();
            bool helpOk = GetHelpFilePath() != null;
            AddInfoPill(healthCard, exeOk ? "Executável encontrado" : "Executável ausente", 24, 96, exeOk);
            AddInfoPill(healthCard, ports, 220, 96, true);
            AddInfoPill(healthCard, helpOk ? "Ajuda local pronta" : "Ajuda local ausente", 420, 96, helpOk);

            Label recentProjectTitle = NewLabel("Último projeto conhecido", 10.2f, FontStyle.Bold, TextPrimary);
            recentProjectTitle.Location = new Point(24, 138);
            healthCard.Controls.Add(recentProjectTitle);

            Label recentProject = NewLabel(GetRecentProjectSummary(), 8.6f, FontStyle.Regular, TextSecondary);
            recentProject.Location = new Point(24, 164);
            recentProject.MaximumSize = new Size(500, 0);
            healthCard.Controls.Add(recentProject);

            ModernButton openRecent = SecondaryButton(WithGlyph("↗", "ABRIR PASTA"), 534, 154, 170);
            openRecent.Enabled = HasRecentProjectFolder();
            openRecent.Click += delegate { OpenRecentProjectFolder(); };
            healthCard.Controls.Add(openRecent);

            ModernButton copyDiagnostics = SecondaryButton(WithGlyph("≡", "COPIAR DIAGNÓSTICO"), 534, 106, 170);
            copyDiagnostics.Click += delegate { CopyDiagnostics(); };
            healthCard.Controls.Add(copyDiagnostics);

            Label note = NewLabel("Compatibilidade preservada: o editor ladder e o protocolo de comunicação continuam sendo executados pelo PC12 original.", 8.6f, FontStyle.Regular, TextSecondary);
            note.Location = new Point(36, 724);
            note.MaximumSize = new Size(720, 0);
            workspace.Controls.Add(note);
        }

        private void ShowConnection()
        {
            ClearWorkspace("Conexão com o PLC", navConnection);

            HeroPanel hero = CreateHeroPanel(34, 14, 732, 170);
            hero.StartColor = Color.FromArgb(23, 66, 72);
            hero.EndColor = Color.FromArgb(27, 132, 86);
            workspace.Controls.Add(hero);

            Label intro = NewLabel("Diagnóstico de comunicação serial", 20.0f, FontStyle.Bold, Color.White);
            intro.Location = new Point(28, 22);
            hero.Controls.Add(intro);

            Label detail = NewLabel("Confirme rapidamente a porta COM reconhecida pelo Windows antes de abrir o PC12 e programar o TP02.", 9.5f, FontStyle.Regular, Color.FromArgb(220, 239, 230));
            detail.Location = new Point(30, 56);
            detail.MaximumSize = new Size(460, 0);
            hero.Controls.Add(detail);

            AddSummaryBadge(hero, 30, 100, "⇄ Porta COM detectada em tempo real", Color.FromArgb(220, 242, 235), Success);
            AddSummaryBadge(hero, 292, 100, "≡ Copie a porta selecionada", Color.FromArgb(233, 240, 249), Accent);

            ModernButton openPc12Hero = PrimaryButton(WithGlyph("▶", "ABRIR PC12"), 540, 96, 156);
            openPc12Hero.Click += delegate { LaunchPc12(false); };
            hero.Controls.Add(openPc12Hero);

            Label connectionTitle = NewLabel("Ferramentas de conexão", 10.0f, FontStyle.Bold, TextSecondary);
            connectionTitle.Location = new Point(36, 202);
            workspace.Controls.Add(connectionTitle);

            CardPanel portCard = CreateCard(34, 230, 732, 204);
            portCard.AccentColor = Accent;
            workspace.Controls.Add(portCard);
            AddCardTitle(portCard, "Porta serial", "As portas abaixo são as detectadas pelo Windows neste momento.");

            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Font = new Font("Segoe UI", 10.0f);
            portCombo.Location = new Point(24, 104);
            portCombo.Size = new Size(240, 28);
            portCombo.SelectedIndexChanged += delegate { UpdatePortDetails(); };
            portCard.Controls.Add(portCombo);
            RefreshPorts();

            ModernButton refresh = SecondaryButton(WithGlyph("⟳", "ATUALIZAR"), 280, 101, 140);
            refresh.Click += delegate { RefreshPorts(); };
            portCard.Controls.Add(refresh);

            ModernButton copyPort = SecondaryButton(WithGlyph("≡", "COPIAR PORTA"), 436, 101, 160);
            copyPort.Click += delegate { CopySelectedPort(); };
            portCard.Controls.Add(copyPort);

            ModernButton openPc12 = PrimaryButton(WithGlyph("▶", "ABRIR PC12"), 604, 101, 120);
            openPc12.Click += delegate { LaunchPc12(false); };
            portCard.Controls.Add(openPc12);

            portStatusLabel = NewLabel(string.Empty, 8.8f, FontStyle.Bold, TextPrimary);
            portStatusLabel.Location = new Point(24, 168);
            portCard.Controls.Add(portStatusLabel);

            portHintLabel = NewLabel(string.Empty, 8.3f, FontStyle.Regular, TextSecondary);
            portHintLabel.Location = new Point(24, 186);
            portHintLabel.MaximumSize = new Size(690, 0);
            portCard.Controls.Add(portHintLabel);

            ModernButton deviceManager = SecondaryButton(WithGlyph("↗", "GERENCIADOR DE DISPOSITIVOS"), 436, 101, 230);
            deviceManager.Click += delegate { OpenDeviceManager(); };
            deviceManager.Location = new Point(436, 130);
            deviceManager.Size = new Size(288, 28);
            portCard.Controls.Add(deviceManager);

            Label checklistTitle = NewLabel("Preparação do TP02", 10.0f, FontStyle.Bold, TextSecondary);
            checklistTitle.Location = new Point(36, 452);
            workspace.Controls.Add(checklistTitle);

            CardPanel checklist = CreateCard(34, 480, 732, 230);
            checklist.AccentColor = Success;
            checklist.FillColor = Color.FromArgb(248, 251, 253);
            workspace.Controls.Add(checklist);
            AddCardTitle(checklist, "Checklist TP02", "Antes de tentar conectar no PLC, confirme estes pontos:");
            AddChecklist(checklist, 24, 100, "Conversor USB/serial conectado ao computador e reconhecido pelo Windows.");
            AddChecklist(checklist, 24, 130, "Cabo de programação ligado ao PLC TP02 e ao conversor serial.");
            AddChecklist(checklist, 24, 160, "A mesma porta COM selecionada no Windows deve ser configurada dentro do PC12.");
            AddChecklist(checklist, 24, 190, "PLC energizado antes de iniciar a tentativa de comunicação.");
        }

        private void ShowTools()
        {
            ClearWorkspace("Ferramentas", navTools);

            HeroPanel hero = CreateHeroPanel(34, 14, 732, 170);
            hero.StartColor = Color.FromArgb(58, 44, 95);
            hero.EndColor = Color.FromArgb(92, 66, 158);
            workspace.Controls.Add(hero);

            Label intro = NewLabel("Manutenção e compatibilidade", 20.0f, FontStyle.Bold, Color.White);
            intro.Location = new Point(28, 22);
            hero.Controls.Add(intro);

            Label detail = NewLabel("Acesse ações rápidas para suporte, limpeza e retomada de trabalho sem alterar os arquivos centrais do PC12.", 9.5f, FontStyle.Regular, Color.FromArgb(228, 223, 245));
            detail.Location = new Point(30, 56);
            detail.MaximumSize = new Size(470, 0);
            hero.Controls.Add(detail);

            AddSummaryBadge(hero, 30, 100, "⚙ Suporte e manutenção", Color.FromArgb(238, 235, 250), Color.FromArgb(74, 54, 130));
            AddSummaryBadge(hero, 220, 100, "↗ Acesso rápido a pastas", Color.FromArgb(233, 240, 249), Accent);
            AddSummaryBadge(hero, 422, 100, "▲ Execução administrativa", Color.FromArgb(250, 235, 215), Warning);

            Label toolsTitle = NewLabel("Ações disponíveis", 10.0f, FontStyle.Bold, TextSecondary);
            toolsTitle.Location = new Point(36, 202);
            workspace.Controls.Add(toolsTitle);

            CardPanel card = CreateCard(34, 230, 732, 442);
            card.AccentColor = Accent;
            workspace.Controls.Add(card);

            AddToolRow(card, 24, 24, "Abrir como administrador", "Use se o Windows estiver bloqueando gravação ou acesso à porta serial.", WithGlyph("▲", "EXECUTAR"), delegate { LaunchPc12(true); });
            AddToolRow(card, 24, 94, "Resetar último arquivo", "Remove somente os arquivos lastfile.cpu e lastfile.dir para evitar o erro de abertura automática.", WithGlyph("×", "RESETAR"), delegate { ResetLastFile(); });
            AddToolRow(card, 24, 164, "Abrir pasta do PC12", "Acesse diretamente os arquivos do pacote portátil.", WithGlyph("↗", "ABRIR PASTA"), delegate { OpenFolder(); });
            AddToolRow(card, 24, 234, "Abrir pasta do último projeto", "Facilita retomar o trabalho recente quando o caminho salvo ainda está disponível.", WithGlyph("↗", "ÚLTIMO PROJETO"), delegate { OpenRecentProjectFolder(); });
            AddToolRow(card, 24, 304, "Copiar diagnóstico", "Gera um resumo rápido do ambiente, portas COM e arquivos de suporte para compartilhar em suporte técnico.", WithGlyph("≡", "COPIAR"), delegate { CopyDiagnostics(); });
            AddToolRow(card, 24, 374, "Modo clássico", "Inicia o executável original diretamente, sem passar por esta central.", WithGlyph("▶", "INICIAR"), delegate { LaunchPc12(false); });
        }

        private void ShowHelp()
        {
            ClearWorkspace("Ajuda e informações", navHelp);

            HeroPanel hero = CreateHeroPanel(34, 14, 732, 170);
            hero.StartColor = Color.FromArgb(71, 56, 28);
            hero.EndColor = Color.FromArgb(190, 112, 20);
            workspace.Controls.Add(hero);

            Label intro = NewLabel("Ajuda e informações", 20.0f, FontStyle.Bold, Color.White);
            intro.Location = new Point(28, 22);
            hero.Controls.Add(intro);

            Label detail = NewLabel("Consulte a ajuda local, revise as limitações da modernização e acesse rapidamente os arquivos do pacote.", 9.5f, FontStyle.Regular, Color.FromArgb(247, 236, 214));
            detail.Location = new Point(30, 56);
            detail.MaximumSize = new Size(470, 0);
            hero.Controls.Add(detail);

            AddSummaryBadge(hero, 30, 100, "? Ajuda local integrada", Color.FromArgb(255, 245, 224), Warning);
            AddSummaryBadge(hero, 224, 100, "ℹ Camada compatível com PC12", Color.FromArgb(233, 240, 249), Accent);

            ModernButton helpHero = PrimaryButton(WithGlyph("?", "ABRIR AJUDA"), 542, 96, 154);
            helpHero.Click += delegate { OpenHelpFile(); };
            hero.Controls.Add(helpHero);

            Label helpTitle = NewLabel("Contexto da versão", 10.0f, FontStyle.Bold, TextSecondary);
            helpTitle.Location = new Point(36, 202);
            workspace.Controls.Add(helpTitle);

            CardPanel info = CreateCard(34, 230, 732, 220);
            info.AccentColor = Accent;
            info.FillColor = Color.FromArgb(248, 251, 253);
            workspace.Controls.Add(info);
            AddCardTitle(info, "Sobre esta versão", "O executável original foi mantido para preservar compatibilidade com o TP02 e com os arquivos de projeto existentes.");

            Label body = NewLabel("A modernização desta etapa atua como central de inicialização, diagnóstico e suporte. Menus, editor ladder e janelas internas do PC12 continuam pertencendo ao software legado. Uma substituição completa dessas telas exigiria reimplementar o editor e o protocolo de comunicação a partir do código-fonte ou por engenharia reversa controlada.", 9.3f, FontStyle.Regular, TextSecondary);
            body.Location = new Point(24, 100);
            body.MaximumSize = new Size(680, 0);
            info.Controls.Add(body);

            Label helpActionsTitle = NewLabel("Ações rápidas", 10.0f, FontStyle.Bold, TextSecondary);
            helpActionsTitle.Location = new Point(36, 468);
            workspace.Controls.Add(helpActionsTitle);

            ModernButton localHelp = PrimaryButton(WithGlyph("?", "ABRIR AJUDA DO PC12"), 34, 496, 210);
            localHelp.Click += delegate { OpenHelpFile(); };
            workspace.Controls.Add(localHelp);

            ModernButton folder = SecondaryButton(WithGlyph("↗", "ABRIR PASTA"), 260, 496, 150);
            folder.Click += delegate { OpenFolder(); };
            workspace.Controls.Add(folder);
        }

        private void AddCardTitle(Control parent, string title, string description)
        {
            Label t = NewLabel(title, 13.5f, FontStyle.Bold, TextPrimary);
            t.Location = new Point(24, 22);
            parent.Controls.Add(t);

            Label d = NewLabel(description, 8.8f, FontStyle.Regular, TextSecondary);
            d.Location = new Point(24, 54);
            d.MaximumSize = new Size(parent.Width - 48, 0);
            parent.Controls.Add(d);
        }

        private void AddToolRow(Control parent, int left, int top, string title, string description, string buttonText, EventHandler action)
        {
            Label t = NewLabel(title, 10.4f, FontStyle.Bold, TextPrimary);
            t.Location = new Point(left, top);
            parent.Controls.Add(t);

            Label d = NewLabel(description, 8.3f, FontStyle.Regular, TextSecondary);
            d.Location = new Point(left, top + 24);
            d.MaximumSize = new Size(470, 0);
            parent.Controls.Add(d);

            ModernButton b = SecondaryButton(buttonText, 542, top + 6, 150);
            b.Click += action;
            parent.Controls.Add(b);
        }

        private void AddSummaryBadge(Control parent, int left, int top, string text, Color backColor, Color foreColor)
        {
            Label badge = new Label();
            badge.AutoSize = false;
            badge.Size = new Size(TextRenderer.MeasureText(text, new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold)).Width + 24, 26);
            badge.Location = new Point(left, top);
            badge.Text = "  " + text;
            badge.TextAlign = ContentAlignment.MiddleLeft;
            badge.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            badge.BackColor = backColor;
            badge.ForeColor = foreColor;
            parent.Controls.Add(badge);
        }

        private void AddChecklist(Control parent, int left, int top, string text)
        {
            Label dot = NewLabel("✓", 10.0f, FontStyle.Bold, Success);
            dot.Location = new Point(left, top);
            parent.Controls.Add(dot);

            Label line = NewLabel(text, 8.8f, FontStyle.Regular, TextPrimary);
            line.Location = new Point(left + 26, top);
            line.MaximumSize = new Size(parent.Width - left - 55, 0);
            parent.Controls.Add(line);
        }

        private void AddInfoPill(Control parent, string text, int left, int top, bool ok)
        {
            Label pill = new Label();
            pill.AutoSize = false;
            pill.Size = new Size(180, 28);
            pill.Location = new Point(left, top);
            pill.Text = "  " + text;
            pill.TextAlign = ContentAlignment.MiddleLeft;
            pill.Font = new Font("Segoe UI", 8.2f, FontStyle.Bold);
            pill.BackColor = ok ? Color.FromArgb(220, 242, 235) : Color.FromArgb(250, 235, 215);
            pill.ForeColor = ok ? Success : Warning;
            pill.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(pill);
        }

        private CardPanel CreateCard(int left, int top, int width, int height)
        {
            CardPanel p = new CardPanel();
            p.Location = new Point(left, top);
            p.Size = new Size(width, height);
            p.BackColor = Color.White;
            p.BorderColor = Border;
            return p;
        }

        private HeroPanel CreateHeroPanel(int left, int top, int width, int height)
        {
            HeroPanel hero = new HeroPanel();
            hero.Location = new Point(left, top);
            hero.Size = new Size(width, height);
            hero.StartColor = Color.FromArgb(22, 49, 82);
            hero.EndColor = Color.FromArgb(0, 122, 204);
            return hero;
        }

        private Label NewLabel(string text, float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font("Segoe UI", size, style, GraphicsUnit.Point);
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            return l;
        }

        private string WithGlyph(string glyph, string text)
        {
            return glyph + "  " + text;
        }

        private ModernButton CreateButton(string text, int left, int top, int width, int height, float fontSize, Color normalColor, Color hoverColor, Color foreColor)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, height);
            b.Font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold);
            b.NormalColor = normalColor;
            b.HoverColor = hoverColor;
            b.PressedColor = ControlPaint.Dark(hoverColor, 0.05f);
            b.ForeColor = foreColor;
            return b;
        }

        private ModernButton PrimaryButton(string text, int left, int top, int width)
        {
            return CreateButton(text, left, top, width, 38, 8.6f, Accent, AccentHover, Color.White);
        }

        private ModernButton SecondaryButton(string text, int left, int top, int width)
        {
            return CreateButton(text, left, top, width, 38, 8.4f, SurfaceMuted, SurfaceMutedHover, Navy);
        }

        private void LaunchPc12(bool admin)
        {
            if (!File.Exists(pc12Path))
            {
                MessageBox.Show("O arquivo pc12.exe não foi encontrado nesta pasta.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateGlobalStatus();
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = pc12Path;
                psi.WorkingDirectory = baseDir;
                psi.UseShellExecute = true;
                if (admin) psi.Verb = "runas";
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível iniciar o PC12.\r\n\r\n" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetLastFile()
        {
            DialogResult result = MessageBox.Show("Deseja limpar a referência ao último projeto aberto?\r\n\r\nIsso não apaga seus projetos. Apenas remove lastfile.cpu e lastfile.dir.", "Resetar último arquivo", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            try
            {
                if (File.Exists(lastFileCpuPath)) File.Delete(lastFileCpuPath);
                if (File.Exists(lastFileDirPath)) File.Delete(lastFileDirPath);
                MessageBox.Show("Referência ao último arquivo removida com sucesso.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível resetar os arquivos.\r\n\r\n" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenFolder()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("explorer.exe", baseDir);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenHelpFile()
        {
            string file = GetHelpFilePath();
            if (file == null)
            {
                MessageBox.Show("Arquivo de ajuda não encontrado.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show("O Windows não conseguiu abrir o arquivo de ajuda antigo.\r\n\r\n" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenDeviceManager()
        {
            try
            {
                Process.Start("mmc.exe", "devmgmt.msc");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetHelpFilePath()
        {
            int i;
            for (i = 0; i < HelpCandidates.Length; i++)
            {
                string file = Path.Combine(baseDir, HelpCandidates[i]);
                if (File.Exists(file)) return file;
            }
            return null;
        }

        private string ReadFirstUsefulLine(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                string[] lines = File.ReadAllLines(path);
                int i;
                for (i = 0; i < lines.Length; i++)
                {
                    string candidate = lines[i].Trim().Trim('"');
                    if (candidate.Length > 0 && candidate != "@") return candidate;
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        private string GetRecentProjectFolder()
        {
            string dirValue = ReadFirstUsefulLine(lastFileDirPath);
            if (!string.IsNullOrEmpty(dirValue))
            {
                if (Directory.Exists(dirValue)) return dirValue;

                string dirParent = Path.GetDirectoryName(dirValue);
                if (!string.IsNullOrEmpty(dirParent) && File.Exists(dirValue) && Directory.Exists(dirParent)) return dirParent;
            }

            string cpuValue = ReadFirstUsefulLine(lastFileCpuPath);
            if (string.IsNullOrEmpty(cpuValue)) return null;
            if (Directory.Exists(cpuValue)) return cpuValue;

            string folder = Path.GetDirectoryName(cpuValue);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) return folder;
            return null;
        }

        private bool HasRecentProjectFolder()
        {
            return GetRecentProjectFolder() != null;
        }

        private string GetRecentProjectSummary()
        {
            string dirValue = ReadFirstUsefulLine(lastFileDirPath);
            string cpuValue = ReadFirstUsefulLine(lastFileCpuPath);
            if (string.IsNullOrEmpty(dirValue) && string.IsNullOrEmpty(cpuValue)) return "Nenhum projeto recente salvo nos arquivos lastfile.*";

            string recentProject;
            if (!string.IsNullOrEmpty(cpuValue))
            {
                if (Path.IsPathRooted(cpuValue) || string.IsNullOrEmpty(dirValue))
                {
                    recentProject = cpuValue;
                }
                else
                {
                    string cpuName = Path.GetFileName(cpuValue);
                    recentProject = string.IsNullOrEmpty(cpuName) ? dirValue : Path.Combine(dirValue, cpuName);
                }
            }
            else
            {
                recentProject = dirValue;
            }

            string folder = GetRecentProjectFolder();
            if (folder == null) return recentProject + " (caminho salvo, mas não encontrado neste computador)";
            return recentProject;
        }

        private void OpenRecentProjectFolder()
        {
            string folder = GetRecentProjectFolder();
            if (folder == null)
            {
                MessageBox.Show("Nenhuma pasta de projeto recente está disponível no momento.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("explorer.exe", folder);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir a pasta do último projeto.\r\n\r\n" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string BuildDiagnosticsReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine(AppTitle);
            report.AppendLine("Pasta: " + baseDir);
            report.AppendLine("pc12.exe: " + (File.Exists(pc12Path) ? "encontrado" : "ausente"));
            report.AppendLine("Ajuda local: " + (GetHelpFilePath() != null ? "disponível" : "ausente"));
            report.AppendLine("Último projeto: " + GetRecentProjectSummary());
            report.AppendLine("Sistema: " + Environment.OSVersion.VersionString);
            report.AppendLine("Portas COM: " + GetPortListSummary());
            return report.ToString();
        }

        private void CopyDiagnostics()
        {
            try
            {
                Clipboard.SetText(BuildDiagnosticsReport());
                MessageBox.Show("Diagnóstico copiado para a área de transferência.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível copiar o diagnóstico.\r\n\r\n" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopySelectedPort()
        {
            string selectedPort = GetSelectedPort();
            if (selectedPort == null)
            {
                MessageBox.Show("Nenhuma porta COM disponível para copiar.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Clipboard.SetText(selectedPort);
                MessageBox.Show("Porta copiada: " + selectedPort, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível copiar a porta selecionada.\r\n\r\n" + ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetSelectedPort()
        {
            if (portCombo == null || portCombo.SelectedItem == null) return null;

            string selectedPort = Convert.ToString(portCombo.SelectedItem);
            if (string.IsNullOrEmpty(selectedPort) || !selectedPort.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) return null;
            return selectedPort;
        }

        private void UpdatePortDetails()
        {
            if (portStatusLabel == null || portHintLabel == null) return;

            string selectedPort = GetSelectedPort();
            if (selectedPort == null)
            {
                portStatusLabel.Text = "Nenhuma porta COM detectada";
                portHintLabel.Text = "Conecte o conversor USB/serial, clique em ATUALIZAR e depois use a mesma porta dentro do PC12.";
                return;
            }

            portStatusLabel.Text = "Porta pronta para configurar: " + selectedPort;
            portHintLabel.Text = "Use " + selectedPort + " nas configurações de comunicação do PC12 para manter o PLC e o Windows apontando para a mesma porta serial.";
        }

        private void RefreshPorts()
        {
            if (portCombo == null) return;
            string previousSelection = GetSelectedPort();
            portCombo.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            int i;
            for (i = 0; i < ports.Length; i++) portCombo.Items.Add(ports[i]);

            if (portCombo.Items.Count > 0)
            {
                if (!string.IsNullOrEmpty(previousSelection) && portCombo.Items.Contains(previousSelection))
                {
                    portCombo.SelectedItem = previousSelection;
                }
                else
                {
                    portCombo.SelectedIndex = 0;
                }
            }
            else
            {
                portCombo.Items.Add("Nenhuma porta COM detectada");
            }

            if (portCombo.SelectedIndex < 0) portCombo.SelectedIndex = 0;
            UpdatePortDetails();
            UpdateGlobalStatus();
        }

        private string GetPortsSummary()
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length == 0) return "Nenhuma porta COM";
            if (ports.Length == 1) return "1 porta COM detectada";
            return ports.Length.ToString() + " portas COM detectadas";
        }

        private string GetPortListSummary()
        {
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            if (ports.Length == 0) return "nenhuma";
            return string.Join(", ", ports);
        }

        private void UpdateGlobalStatus()
        {
            if (statusDot == null || statusLabel == null) return;

            if (File.Exists(pc12Path))
            {
                statusDot.ForeColor = Success;
                statusLabel.Text = "PC12 pronto\r\n" + GetPortsSummary();
            }
            else
            {
                statusDot.ForeColor = Warning;
                statusLabel.Text = "pc12.exe não encontrado";
            }
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private void ToggleMaximize(object sender, EventArgs e)
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }
    }

    internal sealed class ModernButton : Button
    {
        private Color normalColor;
        private Color hoverColor;
        private Color pressedColor;
        private bool isPressed;

        public Color NormalColor
        {
            get { return normalColor; }
            set
            {
                normalColor = value;
                BackColor = value;
            }
        }

        public Color HoverColor
        {
            get { return hoverColor; }
            set { hoverColor = value; }
        }

        public Color PressedColor
        {
            get { return pressedColor; }
            set { pressedColor = value; }
        }

        public ModernButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            normalColor = Color.White;
            hoverColor = Color.Gainsboro;
            pressedColor = Color.Silver;
            BackColor = normalColor;
            UseVisualStyleBackColor = false;
            MouseEnter += delegate { if (!isPressed) BackColor = hoverColor; };
            MouseLeave += delegate { if (!isPressed) BackColor = normalColor; };
            MouseDown += delegate { isPressed = true; BackColor = pressedColor; };
            MouseUp += delegate { isPressed = false; BackColor = ClientRectangle.Contains(PointToClient(Cursor.Position)) ? hoverColor : normalColor; };
        }
    }

    internal sealed class FlatWindowButton : Button
    {
        public FlatWindowButton(string text)
        {
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.White;
            ForeColor = Color.FromArgb(68, 79, 91);
            Font = new Font("Segoe UI", 11.0f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            MouseEnter += delegate { BackColor = Color.FromArgb(235, 240, 245); };
            MouseLeave += delegate { BackColor = Color.White; };
        }
    }

    internal sealed class CardPanel : Panel
    {
        public Color BorderColor { get; set; }
        public Color FillColor { get; set; }
        public Color AccentColor { get; set; }

        public CardPanel()
        {
            BorderColor = Color.FromArgb(225, 230, 236);
            FillColor = Color.White;
            AccentColor = Color.Transparent;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle shadowRect = new Rectangle(3, 4, Width - 7, Height - 7);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(16, 23, 35, 48)))
            {
                e.Graphics.FillRectangle(shadow, shadowRect);
            }

            Rectangle cardRect = new Rectangle(0, 0, Width - 4, Height - 4);
            using (SolidBrush fill = new SolidBrush(FillColor))
            {
                e.Graphics.FillRectangle(fill, cardRect);
            }

            if (AccentColor.A > 0)
            {
                using (SolidBrush accent = new SolidBrush(AccentColor))
                {
                    e.Graphics.FillRectangle(accent, 0, 0, cardRect.Width, 4);
                }
            }

            using (Pen p = new Pen(BorderColor))
            {
                e.Graphics.DrawRectangle(p, cardRect);
            }

            base.OnPaint(e);
        }
    }

    internal sealed class HeroPanel : Panel
    {
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }

        public HeroPanel()
        {
            DoubleBuffered = true;
            StartColor = Color.FromArgb(22, 49, 82);
            EndColor = Color.FromArgb(0, 122, 204);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (LinearGradientBrush background = new LinearGradientBrush(ClientRectangle, StartColor, EndColor, LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
            {
                e.Graphics.FillEllipse(glow, Width - 210, -40, 190, 190);
                e.Graphics.FillEllipse(glow, Width - 150, 70, 110, 110);
            }
        }
    }
}
