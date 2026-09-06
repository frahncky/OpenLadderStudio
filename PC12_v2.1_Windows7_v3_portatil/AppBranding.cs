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
        public static Color Shell { get { return Pick(27, 31, 38, 233, 238, 244); } }
        public static Color Chrome { get { return Pick(34, 39, 47, 244, 247, 250); } }
        public static Color ChromeLight { get { return Pick(42, 48, 58, 251, 252, 254); } }
        public static Color Border { get { return Pick(57, 65, 77, 210, 219, 229); } }
        public static Color Workspace { get { return Pick(21, 24, 30, 223, 230, 238); } }

        public static Color NavBg { get { return Pick(24, 28, 34, 228, 234, 242); } }
        public static Color NavHover { get { return Pick(36, 42, 51, 216, 226, 238); } }
        public static Color NavActive { get { return Pick(46, 55, 66, 203, 218, 236); } }

        // Texto, do mais forte ao mais discreto.
        public static Color Fore { get { return Pick(230, 234, 240, 30, 44, 62); } }
        public static Color Muted { get { return Pick(163, 173, 186, 90, 107, 128); } }
        public static Color Faint { get { return Pick(126, 137, 150, 122, 136, 152); } }
        public static Color Disabled { get { return Pick(104, 113, 125, 150, 162, 175); } }

        // Marca e estados. Contraste minimo de 4,5:1 sobre a superficie do tema.
        public static Color Accent { get { return Pick(76, 141, 246, 28, 105, 210); } }
        public static Color AccentDark { get { return Pick(46, 111, 214, 18, 78, 160); } }
        public static Color OnAccent { get { return Color.White; } }
        public static Color Ok { get { return Pick(75, 178, 115, 47, 133, 71); } }
        public static Color Warning { get { return Pick(217, 164, 65, 176, 120, 12); } }
        public static Color Danger { get { return Pick(224, 96, 96, 196, 54, 54); } }
        public static Color Info { get { return Pick(90, 169, 230, 24, 116, 190); } }

        // Area de desenho do Ladder e do sinoptico.
        public static Color Canvas { get { return Pick(32, 36, 43, 242, 245, 249); } }
        public static Color Rail { get { return Pick(143, 163, 184, 74, 90, 107); } }
        public static Color Wire { get { return Pick(122, 138, 156, 95, 110, 128); } }
        public static Color GridLine { get { return Pick(44, 50, 59, 222, 229, 237); } }
        public static Color SelectionFill { get { return Pick(30, 51, 80, 220, 234, 251); } }
        public static Color SelectionEdge { get { return Pick(76, 141, 246, 28, 105, 210); } }

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
