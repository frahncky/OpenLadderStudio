using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class AppBranding
    {
        private static bool installed;
        private static Icon appIcon;
        private static readonly List<Form> skinned = new List<Form>();

        public static void Install()
        {
            if (installed) return;
            installed = true;
            appIcon = LoadIcon();
            Application.Idle += ApplyToOpenForms;
            ApplyToOpenForms(null, EventArgs.Empty);
        }

        public static void Apply(Form form)
        {
            if (form == null) return;
            if (appIcon == null) appIcon = LoadIcon();
            if (appIcon == null) return;

            try
            {
                form.Icon = (Icon)appIcon.Clone();
                form.ShowIcon = true;
            }
            catch
            {
            }
        }

        private static void ApplyToOpenForms(object sender, EventArgs e)
        {
            try
            {
                for (int i = skinned.Count - 1; i >= 0; i--)
                {
                    if (skinned[i] == null || skinned[i].IsDisposed) skinned.RemoveAt(i);
                }

                for (int i = 0; i < Application.OpenForms.Count; i++)
                {
                    Form form = Application.OpenForms[i];
                    if (form == null || form.IsDisposed) continue;
                    Apply(form);

                    // Uma janela so e pintada uma vez: repintar a cada Idle desfaria
                    // qualquer cor que a propria tela ajuste durante o uso.
                    if (!skinned.Contains(form))
                    {
                        skinned.Add(form);
                        OpenLadderPalette.Skin(form);
                    }
                }
            }
            catch
            {
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                string external = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenLadderStudio.ico");
                if (File.Exists(external)) return new Icon(external);
            }
            catch
            {
            }

            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }
    }

    internal enum OpenLadderThemeMode
    {
        Dark,
        Light
    }

    /// <summary>
    /// Paleta unica do produto. Este arquivo entra em todos os executaveis, entao o
    /// shell, o editor, o simulador e cada ferramenta separada leem as mesmas cores.
    /// Antes disso cada janela trazia a propria tabela de cores e o programa abria
    /// telas escuras a partir de um shell claro.
    /// </summary>
    internal static class OpenLadderPalette
    {
        private static OpenLadderThemeMode mode = OpenLadderThemeMode.Dark;
        private static bool loaded;

        /// <summary>Disparado quando o tema muda, para as janelas abertas se repintarem.</summary>
        public static event EventHandler Changed;

        public static OpenLadderThemeMode Mode
        {
            get { EnsureLoaded(); return mode; }
        }

        public static bool IsDark
        {
            get { return Mode == OpenLadderThemeMode.Dark; }
        }

        public static void Use(OpenLadderThemeMode value)
        {
            EnsureLoaded();
            if (mode == value) return;
            mode = value;
            Save();
            EventHandler handler = Changed;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public static void Toggle()
        {
            Use(IsDark ? OpenLadderThemeMode.Light : OpenLadderThemeMode.Dark);
        }

        // Superficies, do fundo da janela para a superficie mais elevada.
        // O tema escuro evita preto puro e o tema claro evita branco puro: as duas
        // extremidades sao o que mais cansa a vista em jornada longa.
        public static Color Shell { get { return Pick(24, 30, 40, 233, 239, 246); } }
        public static Color Chrome { get { return Pick(32, 40, 52, 244, 247, 251); } }
        public static Color ChromeLight { get { return Pick(40, 50, 65, 250, 252, 255); } }
        public static Color Border { get { return Pick(63, 78, 98, 194, 207, 223); } }
        public static Color Workspace { get { return Pick(18, 24, 33, 220, 229, 240); } }

        public static Color NavBg { get { return Pick(20, 27, 38, 226, 234, 244); } }
        public static Color NavHover { get { return Pick(34, 47, 65, 216, 229, 247); } }
        public static Color NavActive { get { return Pick(38, 65, 101, 204, 224, 250); } }

        // Texto, do mais forte ao mais discreto.
        public static Color Fore { get { return Pick(233, 239, 247, 30, 44, 62); } }
        public static Color Muted { get { return Pick(177, 190, 208, 77, 96, 119); } }
        public static Color Faint { get { return Pick(157, 173, 193, 89, 107, 129); } }
        public static Color Disabled { get { return Pick(112, 129, 150, 139, 153, 171); } }

        // Marca e estados legiveis sobre Chrome e ChromeLight. O azul luminoso
        // do tema escuro pede texto escuro nos botoes preenchidos.
        public static Color Accent { get { return Pick(110, 174, 255, 28, 105, 210); } }
        public static Color AccentDark { get { return Pick(86, 153, 239, 18, 78, 160); } }
        public static Color OnAccent { get { return Pick(16, 30, 48, 255, 255, 255); } }
        public static Color Ok { get { return Pick(96, 205, 153, 32, 113, 75); } }
        public static Color Warning { get { return Pick(242, 193, 94, 137, 91, 12); } }
        public static Color Danger { get { return Pick(247, 133, 139, 183, 48, 66); } }
        public static Color Info { get { return Pick(116, 188, 246, 29, 103, 170); } }

        // Area de desenho do Ladder e do sinoptico.
        public static Color Canvas { get { return Pick(27, 34, 45, 246, 248, 252); } }
        public static Color Rail { get { return Pick(163, 181, 204, 74, 90, 107); } }
        public static Color Wire { get { return Pick(141, 162, 188, 95, 110, 128); } }
        public static Color GridLine { get { return Pick(37, 46, 61, 225, 232, 242); } }
        public static Color SelectionFill { get { return Pick(32, 60, 96, 220, 234, 251); } }
        public static Color SelectionEdge { get { return Accent; } }

        /// <summary>
        /// Par de cores calibradas para cada tema. Usado pela paleta semantica dos
        /// icones, em que o mesmo significado precisa de um tom claro sobre fundo
        /// escuro e de um tom fechado sobre fundo claro.
        /// </summary>
        public static Color Duo(int dr, int dg, int db, int lr, int lg, int lb)
        {
            return Pick(dr, dg, db, lr, lg, lb);
        }

        private static Color Pick(int dr, int dg, int db, int lr, int lg, int lb)
        {
            return IsDark ? Color.FromArgb(dr, dg, db) : Color.FromArgb(lr, lg, lb);
        }

        /// <summary>
        /// Aplica a paleta aos controles de entrada de uma janela. Caixa de texto,
        /// lista e grade nascem com a cor de sistema (branco), o que produzia campos
        /// brancos cegantes dentro de janelas escuras. So mexe em controle que ainda
        /// esta na cor padrao: estilo definido a mao pela tela continua valendo.
        /// </summary>
        public static void Skin(Control root)
        {
            if (root == null) return;

            for (int i = 0; i < root.Controls.Count; i++)
            {
                Control c = root.Controls[i];
                if (c == null || c.IsDisposed) continue;

                DataGridView grid = c as DataGridView;
                if (grid != null)
                {
                    SkinGrid(grid);
                    continue;
                }

                if (IsInput(c) && c.BackColor == SystemColors.Window)
                {
                    c.BackColor = ChromeLight;
                    c.ForeColor = Fore;
                }

                Skin(c);
            }
        }

        private static bool IsInput(Control c)
        {
            return c is TextBoxBase
                || c is ComboBox
                || c is ListBox
                || c is ListView
                || c is TreeView
                || c is NumericUpDown;
        }

        private static void SkinGrid(DataGridView grid)
        {
            if (grid.BackgroundColor != SystemColors.AppWorkspace && grid.BackgroundColor != SystemColors.Window) return;

            grid.BackgroundColor = Chrome;
            grid.GridColor = Border;
            grid.DefaultCellStyle.BackColor = ChromeLight;
            grid.DefaultCellStyle.ForeColor = Fore;
            grid.DefaultCellStyle.SelectionBackColor = SelectionFill;
            grid.DefaultCellStyle.SelectionForeColor = Fore;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Chrome;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Chrome;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = NavActive;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Fore;
            grid.RowHeadersDefaultCellStyle.BackColor = Chrome;
            grid.RowHeadersDefaultCellStyle.ForeColor = Muted;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = NavActive;
            grid.RowHeadersDefaultCellStyle.SelectionForeColor = Fore;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.EnableHeadersVisualStyles = false;
        }

        private static string SettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(appData, "OpenLadder Studio"), "tema.txt");
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                string path = SettingsPath();
                if (!File.Exists(path)) return;
                string value = File.ReadAllText(path).Trim();
                if (string.Equals(value, "claro", StringComparison.OrdinalIgnoreCase)) mode = OpenLadderThemeMode.Light;
                else mode = OpenLadderThemeMode.Dark;
            }
            catch
            {
                // Preferencia ilegivel nao pode impedir o programa de abrir.
            }
        }

        private static void Save()
        {
            try
            {
                string path = SettingsPath();
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, IsDark ? "escuro" : "claro");
            }
            catch
            {
            }
        }
    }
}
