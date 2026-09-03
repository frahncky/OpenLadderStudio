using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
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
        private readonly Color Canvas = Color.FromArgb(244, 247, 250);
        private readonly Color TextPrimary = Color.FromArgb(34, 45, 57);
        private readonly Color TextSecondary = Color.FromArgb(94, 108, 124);
        private readonly Color Success = Color.FromArgb(27, 132, 86);
        private readonly Color Warning = Color.FromArgb(190, 112, 20);

        private readonly string baseDir;
        private readonly string pc12Path;
        private Panel workspace;
        private Label titleLabel;
        private Label statusLabel;
        private Label statusDot;
        private ModernButton navHome;
        private ModernButton navConnection;
        private ModernButton navTools;
        private ModernButton navHelp;
        private ComboBox portCombo;

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

            Text = "PC12 Modern";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 620);
            Size = new Size(1080, 700);
            BackColor = Canvas;
            FormBorderStyle = FormBorderStyle.None;
            Font = new Font("Segoe UI", 9.0f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            BuildShell();
            ShowHome();
            UpdateGlobalStatus();
        }

        private void BuildShell()
        {
            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 54;
            topBar.BackColor = Color.White;
            topBar.MouseDown += DragWindow;
            Controls.Add(topBar);

            Label brand = new Label();
            brand.AutoSize = true;
            brand.Text = "PC12  MODERN";
            brand.Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold);
            brand.ForeColor = Navy;
            brand.Location = new Point(24, 16);
            brand.MouseDown += DragWindow;
            topBar.Controls.Add(brand);

            Label edition = new Label();
            edition.AutoSize = true;
            edition.Text = "Windows 7 compatibility layer";
            edition.Font = new Font("Segoe UI", 8.5f);
            edition.ForeColor = TextSecondary;
            edition.Location = new Point(152, 19);
            edition.MouseDown += DragWindow;
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
            sidebar.Width = 224;
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

            navHome = CreateNavButton("Início", 128);
            navHome.Click += delegate { ShowHome(); };
            sidebar.Controls.Add(navHome);

            navConnection = CreateNavButton("Conexão", 176);
            navConnection.Click += delegate { ShowConnection(); };
            sidebar.Controls.Add(navConnection);

            navTools = CreateNavButton("Ferramentas", 224);
            navTools.Click += delegate { ShowTools(); };
            sidebar.Controls.Add(navTools);

            navHelp = CreateNavButton("Ajuda e informações", 272);
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

        private ModernButton CreateNavButton(string text, int top)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Location = new Point(14, top);
            b.Size = new Size(196, 40);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(14, 0, 0, 0);
            b.Font = new Font("Segoe UI Semibold", 9.4f, FontStyle.Bold);
            b.NormalColor = Navy;
            b.HoverColor = NavyLight;
            b.ForeColor = Color.FromArgb(225, 235, 245);
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

            Label welcome = NewLabel("PC12 com uma experiência mais organizada", 20.0f, FontStyle.Bold, TextPrimary);
            welcome.Location = new Point(34, 14);
            workspace.Controls.Add(welcome);

            Label intro = NewLabel("A interface moderna centraliza inicialização, diagnóstico, portas COM e ferramentas sem alterar o executável original do PC12.", 9.5f, FontStyle.Regular, TextSecondary);
            intro.Location = new Point(36, 50);
            intro.MaximumSize = new Size(720, 0);
            workspace.Controls.Add(intro);

            CardPanel launchCard = CreateCard(34, 98, 356, 190);
            workspace.Controls.Add(launchCard);
            AddCardTitle(launchCard, "Programar PLC", "Abra o PC12 Design Center 2.1 mantendo toda a compatibilidade com os projetos existentes.");
            ModernButton launch = PrimaryButton("ABRIR PC12", 24, 128, 150);
            launch.Click += delegate { LaunchPc12(false); };
            launchCard.Controls.Add(launch);

            CardPanel connectionCard = CreateCard(410, 98, 356, 190);
            workspace.Controls.Add(connectionCard);
            AddCardTitle(connectionCard, "Conexão serial", "Veja rapidamente as portas COM disponíveis e acesse as verificações de comunicação com o TP02.");
            ModernButton connection = SecondaryButton("VER CONEXÃO", 24, 128, 150);
            connection.Click += delegate { ShowConnection(); };
            connectionCard.Controls.Add(connection);

            CardPanel healthCard = CreateCard(34, 308, 732, 146);
            workspace.Controls.Add(healthCard);
            AddCardTitle(healthCard, "Diagnóstico rápido", "Situação do pacote portátil e recursos necessários para iniciar o software.");

            bool exeOk = File.Exists(pc12Path);
            string ports = GetPortsSummary();
            AddInfoPill(healthCard, exeOk ? "Executável encontrado" : "Executável ausente", 24, 96, exeOk);
            AddInfoPill(healthCard, ports, 220, 96, true);
            AddInfoPill(healthCard, Environment.OSVersion.VersionString, 420, 96, true);

            Label note = NewLabel("Compatibilidade preservada: o editor ladder e o protocolo de comunicação continuam sendo executados pelo PC12 original.", 8.6f, FontStyle.Regular, TextSecondary);
            note.Location = new Point(36, 476);
            note.MaximumSize = new Size(720, 0);
            workspace.Controls.Add(note);
        }

        private void ShowConnection()
        {
            ClearWorkspace("Conexão com o PLC", navConnection);

            Label intro = NewLabel("Diagnóstico de comunicação serial", 18.0f, FontStyle.Bold, TextPrimary);
            intro.Location = new Point(34, 14);
            workspace.Controls.Add(intro);

            Label detail = NewLabel("Use esta tela antes de abrir o PC12 para confirmar se o conversor USB/serial foi reconhecido pelo Windows.", 9.5f, FontStyle.Regular, TextSecondary);
            detail.Location = new Point(36, 50);
            detail.MaximumSize = new Size(730, 0);
            workspace.Controls.Add(detail);

            CardPanel portCard = CreateCard(34, 96, 732, 168);
            workspace.Controls.Add(portCard);
            AddCardTitle(portCard, "Porta serial", "As portas abaixo são as detectadas pelo Windows neste momento.");

            portCombo = new ComboBox();
            portCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            portCombo.Font = new Font("Segoe UI", 10.0f);
            portCombo.Location = new Point(24, 104);
            portCombo.Size = new Size(240, 28);
            portCard.Controls.Add(portCombo);
            RefreshPorts();

            ModernButton refresh = SecondaryButton("ATUALIZAR", 280, 101, 120);
            refresh.Click += delegate { RefreshPorts(); };
            portCard.Controls.Add(refresh);

            ModernButton deviceManager = SecondaryButton("GERENCIADOR DE DISPOSITIVOS", 416, 101, 250);
            deviceManager.Click += delegate { OpenDeviceManager(); };
            portCard.Controls.Add(deviceManager);

            CardPanel checklist = CreateCard(34, 284, 732, 230);
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

            Label intro = NewLabel("Manutenção e compatibilidade", 18.0f, FontStyle.Bold, TextPrimary);
            intro.Location = new Point(34, 14);
            workspace.Controls.Add(intro);

            Label detail = NewLabel("Ações úteis para resolver problemas comuns sem alterar os arquivos principais do PC12.", 9.5f, FontStyle.Regular, TextSecondary);
            detail.Location = new Point(36, 50);
            workspace.Controls.Add(detail);

            CardPanel card = CreateCard(34, 96, 732, 302);
            workspace.Controls.Add(card);

            AddToolRow(card, 24, 24, "Abrir como administrador", "Use se o Windows estiver bloqueando gravação ou acesso à porta serial.", "EXECUTAR", delegate { LaunchPc12(true); });
            AddToolRow(card, 24, 94, "Resetar último arquivo", "Remove somente os arquivos lastfile.cpu e lastfile.dir para evitar o erro de abertura automática.", "RESETAR", delegate { ResetLastFile(); });
            AddToolRow(card, 24, 164, "Abrir pasta do PC12", "Acesse diretamente os arquivos do pacote portátil.", "ABRIR PASTA", delegate { OpenFolder(); });
            AddToolRow(card, 24, 234, "Modo clássico", "Inicia o executável original diretamente, sem passar por esta central.", "INICIAR", delegate { LaunchPc12(false); });
        }

        private void ShowHelp()
        {
            ClearWorkspace("Ajuda e informações", navHelp);

            Label intro = NewLabel("PC12 Modern", 20.0f, FontStyle.Bold, TextPrimary);
            intro.Location = new Point(34, 14);
            workspace.Controls.Add(intro);

            Label detail = NewLabel("Camada de interface para facilitar o uso do PC12 Design Center 2.1 em Windows 7.", 9.5f, FontStyle.Regular, TextSecondary);
            detail.Location = new Point(36, 50);
            workspace.Controls.Add(detail);

            CardPanel info = CreateCard(34, 96, 732, 220);
            workspace.Controls.Add(info);
            AddCardTitle(info, "Sobre esta versão", "O executável original foi mantido para preservar compatibilidade com o TP02 e com os arquivos de projeto existentes.");

            Label body = NewLabel("A modernização desta etapa atua como central de inicialização, diagnóstico e suporte. Menus, editor ladder e janelas internas do PC12 continuam pertencendo ao software legado. Uma substituição completa dessas telas exigiria reimplementar o editor e o protocolo de comunicação a partir do código-fonte ou por engenharia reversa controlada.", 9.3f, FontStyle.Regular, TextSecondary);
            body.Location = new Point(24, 100);
            body.MaximumSize = new Size(680, 0);
            info.Controls.Add(body);

            ModernButton localHelp = PrimaryButton("ABRIR AJUDA DO PC12", 34, 342, 190);
            localHelp.Click += delegate { OpenHelpFile(); };
            workspace.Controls.Add(localHelp);

            ModernButton folder = SecondaryButton("ABRIR PASTA", 240, 342, 150);
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
            d.MaximumSize = new Size(490, 0);
            parent.Controls.Add(d);

            ModernButton b = SecondaryButton(buttonText, 560, top + 6, 132);
            b.Click += action;
            parent.Controls.Add(b);
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
            pill.BackColor = ok ? Color.FromArgb(230, 245, 238) : Color.FromArgb(252, 239, 226);
            pill.ForeColor = ok ? Success : Warning;
            parent.Controls.Add(pill);
        }

        private CardPanel CreateCard(int left, int top, int width, int height)
        {
            CardPanel p = new CardPanel();
            p.Location = new Point(left, top);
            p.Size = new Size(width, height);
            p.BackColor = Color.White;
            p.BorderColor = Color.FromArgb(224, 231, 238);
            return p;
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

        private ModernButton PrimaryButton(string text, int left, int top, int width)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 38);
            b.Font = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold);
            b.NormalColor = Accent;
            b.HoverColor = AccentHover;
            b.ForeColor = Color.White;
            return b;
        }

        private ModernButton SecondaryButton(string text, int left, int top, int width)
        {
            ModernButton b = new ModernButton();
            b.Text = text;
            b.Location = new Point(left, top);
            b.Size = new Size(width, 38);
            b.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            b.NormalColor = Color.FromArgb(235, 240, 245);
            b.HoverColor = Color.FromArgb(220, 229, 238);
            b.ForeColor = Navy;
            return b;
        }

        private void LaunchPc12(bool admin)
        {
            if (!File.Exists(pc12Path))
            {
                MessageBox.Show("O arquivo pc12.exe não foi encontrado nesta pasta.", "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Não foi possível iniciar o PC12.\r\n\r\n" + ex.Message, "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetLastFile()
        {
            DialogResult result = MessageBox.Show("Deseja limpar a referência ao último projeto aberto?\r\n\r\nIsso não apaga seus projetos. Apenas remove lastfile.cpu e lastfile.dir.", "Resetar último arquivo", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            try
            {
                string cpu = Path.Combine(baseDir, "lastfile.cpu");
                string dir = Path.Combine(baseDir, "lastfile.dir");
                if (File.Exists(cpu)) File.Delete(cpu);
                if (File.Exists(dir)) File.Delete(dir);
                MessageBox.Show("Referência ao último arquivo removida com sucesso.", "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível resetar os arquivos.\r\n\r\n" + ex.Message, "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenFolder()
        {
            try
            {
                Process.Start("explorer.exe", "\"" + baseDir + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenHelpFile()
        {
            string[] candidates = new string[] { "PC12HELP.HLP", "Tp022.hlp", "HELPDLG.HLP" };
            int i;
            for (i = 0; i < candidates.Length; i++)
            {
                string file = Path.Combine(baseDir, candidates[i]);
                if (File.Exists(file))
                {
                    try
                    {
                        Process.Start(file);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("O Windows não conseguiu abrir o arquivo de ajuda antigo.\r\n\r\n" + ex.Message, "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                }
            }
            MessageBox.Show("Arquivo de ajuda não encontrado.", "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenDeviceManager()
        {
            try
            {
                Process.Start("mmc.exe", "devmgmt.msc");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PC12 Modern", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshPorts()
        {
            if (portCombo == null) return;
            portCombo.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            int i;
            for (i = 0; i < ports.Length; i++) portCombo.Items.Add(ports[i]);
            if (portCombo.Items.Count > 0) portCombo.SelectedIndex = 0;
            else portCombo.Items.Add("Nenhuma porta COM detectada");
            if (portCombo.SelectedIndex < 0) portCombo.SelectedIndex = 0;
            UpdateGlobalStatus();
        }

        private string GetPortsSummary()
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length == 0) return "Nenhuma porta COM";
            if (ports.Length == 1) return "1 porta COM detectada";
            return ports.Length.ToString() + " portas COM detectadas";
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

        public ModernButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            normalColor = Color.White;
            hoverColor = Color.Gainsboro;
            BackColor = normalColor;
            UseVisualStyleBackColor = false;
            MouseEnter += delegate { BackColor = hoverColor; };
            MouseLeave += delegate { BackColor = normalColor; };
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

        public CardPanel()
        {
            BorderColor = Color.FromArgb(225, 230, 236);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(BorderColor))
            {
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
