using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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

            // Menus suspensos e menus de contexto sem renderizador proprio passam a
            // usar a paleta; os que definem o seu continuam como estao.
            try { ToolStripManager.Renderer = new OpenLadderToolStripRenderer(); }
            catch { }

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
                    WindowChrome.Hook(form);

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
        private static OpenLadderThemeMode mode = OpenLadderThemeMode.Light;
        private static bool loaded;
        private static PropertyInfo[] tokenProperties;

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

            // As telas leem a paleta na hora de pintar, mas o que foi atribuido a
            // BackColor ou ForeColor durante a construcao ficou gravado no controle
            // e nao se atualiza sozinho. Guardamos as cores do tema que sai para
            // reconhece-las na arvore de controles e trocar cada uma pela
            // equivalente do tema que entra.
            Color[] saindo = TokenValues();
            mode = value;
            Color[] entrando = TokenValues();
            Save();

            WindowChrome.RefreshAll();
            RestyleOpenForms(BuildRemap(saindo, entrando));

            EventHandler handler = Changed;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public static void Toggle()
        {
            Use(IsDark ? OpenLadderThemeMode.Light : OpenLadderThemeMode.Dark);
        }

        // Hierarquia de superficies, do fundo para a frente. Cada nivel e um degrau
        // perceptivel do anterior, nos dois temas:
        //   Shell (janela) < Workspace/NavBg < Chrome (painel) < ChromeLight (campo) < Raised (flutuante)
        // No tema escuro o Canvas fica entre a janela e o painel, como area de trabalho
        // rebaixada; no claro e a superficie mais clara. Nenhum tema usa preto ou
        // branco puro em superficie grande: sao as extremidades que mais cansam a vista.
        public static Color Shell { get { return Pick(15, 20, 29, 232, 237, 244); } }
        public static Color Workspace { get { return Pick(19, 25, 36, 238, 242, 249); } }
        public static Color Chrome { get { return Pick(25, 32, 45, 246, 248, 252); } }
        public static Color ChromeLight { get { return Pick(33, 42, 58, 252, 253, 255); } }

        /// <summary>Superficie elevada: menu suspenso, dica, cartao, popup. Sempre acima de ChromeLight.</summary>
        public static Color Raised { get { return Pick(41, 52, 72, 254, 254, 255); } }

        // Bordas: Border separa regioes irmas; BorderStrong delimita um controle (campo,
        // cartao); HeaderLine e a aresta inferior da barra superior.
        public static Color Border { get { return Pick(48, 61, 82, 208, 217, 229); } }
        public static Color BorderStrong { get { return Pick(76, 94, 120, 163, 178, 199); } }

        /// <summary>
        /// Aresta inferior da barra superior. A borda comum e uma linha de divisao
        /// entre pares; o topo separa o comando do documento e precisa ler como
        /// aresta da moldura, nao como mais um traco igual aos outros.
        /// </summary>
        public static Color HeaderLine { get { return Pick(88, 112, 142, 166, 185, 210); } }

        public static Color NavBg { get { return Pick(17, 23, 33, 240, 244, 249); } }
        public static Color NavHover { get { return Pick(33, 43, 60, 228, 235, 245); } }
        public static Color NavActive { get { return Pick(34, 64, 106, 214, 228, 248); } }

        // Texto, do mais forte ao mais discreto. Fore, Muted e Faint passam de 4,5:1
        // sobre Shell, Chrome e ChromeLight nos dois temas; Disabled e proposital-
        // mente mais fraco (controle indisponivel esta isento do criterio).
        public static Color Fore { get { return Pick(233, 239, 247, 30, 44, 62); } }
        public static Color Muted { get { return Pick(177, 190, 208, 77, 96, 119); } }
        public static Color Faint { get { return Pick(157, 173, 193, 89, 107, 129); } }
        public static Color Disabled { get { return Pick(112, 129, 150, 139, 153, 171); } }

        // Marca e estados legiveis sobre Chrome e ChromeLight. O azul luminoso
        // do tema escuro pede texto escuro nos botoes preenchidos.
        public static Color Accent { get { return Pick(99, 177, 255, 29, 84, 200); } }
        public static Color AccentDark { get { return Pick(61, 139, 230, 22, 62, 160); } }
        public static Color OnAccent { get { return Pick(16, 30, 48, 255, 255, 255); } }

        /// <summary>Anel de foco de teclado. Distinto de Accent para nao se confundir com selecao.</summary>
        public static Color Focus { get { return Pick(147, 205, 255, 15, 70, 190); } }

        // Estados. Cada cor de texto tem o par "Bg", uma superficie tingida para
        // faixa, selo e linha de tabela; a cor de estado le com 4,5:1 sobre o proprio Bg.
        public static Color Ok { get { return Pick(96, 205, 153, 32, 113, 75); } }
        public static Color Warning { get { return Pick(242, 193, 94, 137, 91, 12); } }
        public static Color Danger { get { return Pick(247, 133, 139, 183, 48, 66); } }
        public static Color Info { get { return Pick(116, 188, 246, 29, 103, 170); } }
        public static Color OkBg { get { return Pick(22, 52, 40, 221, 243, 231); } }
        public static Color WarningBg { get { return Pick(60, 48, 22, 252, 240, 208); } }
        public static Color DangerBg { get { return Pick(64, 31, 37, 252, 225, 227); } }
        public static Color InfoBg { get { return Pick(27, 50, 80, 221, 236, 252); } }

        // Area de desenho do Ladder e do sinoptico. A superficie e uniforme: o que
        // distingue a linha selecionada e um degrau sutil (CanvasRow), nunca uma
        // celula clara no meio de celulas cinzas.
        public static Color Canvas { get { return Pick(20, 27, 39, 250, 251, 254); } }
        public static Color CanvasRow { get { return Pick(26, 36, 52, 238, 243, 251); } }
        public static Color CanvasHover { get { return Pick(31, 42, 61, 231, 238, 249); } }
        public static Color Rail { get { return Pick(156, 181, 214, 74, 90, 107); } }
        public static Color Wire { get { return Pick(144, 169, 202, 95, 110, 128); } }
        public static Color GridLine { get { return Pick(37, 49, 68, 222, 229, 239); } }
        public static Color SelectionFill { get { return Pick(30, 60, 102, 211, 227, 250); } }
        public static Color SelectionEdge { get { return Accent; } }

        /// <summary>
        /// Condutor energizado no diagrama. Verde saturado, mais vivo que Ok (que e
        /// estado de interface): precisa saltar sobre Wire e Rail, que sao cinza-azulados.
        /// </summary>
        public static Color Energized { get { return Pick(57, 224, 128, 0, 124, 56); } }
        public static Color EnergizedBg { get { return Pick(22, 66, 46, 220, 243, 229); } }

        /// <summary>
        /// Par de cores calibradas para cada tema. Usado pela paleta semantica dos
        /// icones, em que o mesmo significado precisa de um tom claro sobre fundo
        /// escuro e de um tom fechado sobre fundo claro.
        /// </summary>
        public static Color Duo(int dr, int dg, int db, int lr, int lg, int lb)
        {
            return Pick(dr, dg, db, lr, lg, lb);
        }

        /// <summary>Cor da paleta com transparencia, para destaques sobre o fundo (nao cria cor nova).</summary>
        public static Color Tint(Color baseColor, int alpha)
        {
            return Color.FromArgb(alpha, baseColor);
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

            // Janela que nao escolheu cor herda o fundo de sistema (cinza claro) e o
            // texto preto; os filhos sem cor propria herdam daqui.
            Form form = root as Form;
            if (form != null && form.BackColor == SystemColors.Control)
            {
                form.BackColor = Shell;
                if (form.ForeColor == SystemColors.ControlText) form.ForeColor = Fore;
            }

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

                SkinButton(c as Button);
                SkinTabPage(c as TabPage);
                SkinComboBox(c as ComboBox);
                SkinTabControl(c as TabControl);

                Skin(c);
            }
        }

        /// <summary>
        /// Botao de sistema (cinza claro com borda 3D) destoa em qualquer tema. So
        /// converte o que ainda esta no visual padrao; botao com estilo da tela fica.
        /// </summary>
        private static void SkinButton(Button b)
        {
            if (b == null) return;
            if (b.FlatStyle == FlatStyle.Flat) return;
            if (!b.UseVisualStyleBackColor || b.BackColor != SystemColors.Control) return;

            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = ChromeLight;
            b.ForeColor = Fore;
            b.FlatAppearance.BorderColor = BorderStrong;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = NavHover;
            b.FlatAppearance.MouseDownBackColor = NavActive;
        }

        /// <summary>
        /// Lista suspensa fixa (DropDownList) ignora BackColor no desenho de sistema e
        /// fica branca dentro da janela escura, tanto fechada quanto aberta. No tema
        /// escuro o texto e a lista passam a ser desenhados pela paleta; a seta continua
        /// sendo do sistema. No tema claro o desenho de sistema ja combina.
        /// </summary>
        private static void SkinComboBox(ComboBox combo)
        {
            if (combo == null || !IsDark) return;
            if (combo.DropDownStyle != ComboBoxStyle.DropDownList || combo.DrawMode != DrawMode.Normal) return;

            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem += DrawComboItem;
        }

        private static void DrawComboItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo == null) return;

            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool selected = !edit && (e.State & DrawItemState.Selected) != 0;
            Color back = combo.BackColor == SystemColors.Window ? ChromeLight : combo.BackColor;

            using (Brush fill = new SolidBrush(selected ? SelectionFill : back)) e.Graphics.FillRectangle(fill, e.Bounds);
            if (e.Index < 0 || e.Index >= combo.Items.Count) return;

            string text = combo.GetItemText(combo.Items[e.Index]);
            Rectangle area = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top, e.Bounds.Width - 2, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, combo.Font, area, combo.Enabled ? Fore : Disabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        /// <summary>
        /// Cabecalho de aba desenhado pela paleta no tema escuro (o do sistema e
        /// branco). A moldura do corpo da aba continua sendo do sistema.
        /// </summary>
        private static void SkinTabControl(TabControl tabs)
        {
            if (tabs == null || !IsDark || tabs.DrawMode != TabDrawMode.Normal) return;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem += DrawTabHeader;
        }

        private static void DrawTabHeader(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Rectangle bounds = e.Bounds;
            using (Brush back = new SolidBrush(selected ? ChromeLight : Chrome)) e.Graphics.FillRectangle(back, bounds);
            if (selected)
            {
                using (Brush bar = new SolidBrush(Accent)) e.Graphics.FillRectangle(bar, bounds.Left, bounds.Top, bounds.Width, 2);
            }

            using (StringFormat format = new StringFormat())
            using (Brush ink = new SolidBrush(selected ? Fore : Muted))
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString(tabs.TabPages[e.Index].Text, tabs.Font, ink, bounds, format);
            }
        }

        private static void SkinTabPage(TabPage page)
        {
            if (page == null || !(page.UseVisualStyleBackColor || page.BackColor == SystemColors.Control)) return;
            page.UseVisualStyleBackColor = false;
            page.BackColor = Shell;
            page.ForeColor = Fore;
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
            // Cabecalho com estilo proprio mas borda "Raised" (padrao) desenha o filete
            // claro/escuro de sistema, que aparece como contorno branco em tema escuro.
            if (!grid.EnableHeadersVisualStyles)
            {
                if (grid.ColumnHeadersBorderStyle == DataGridViewHeaderBorderStyle.Raised) grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                if (grid.RowHeadersBorderStyle == DataGridViewHeaderBorderStyle.Raised) grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            }

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

        /// <summary>
        /// Propriedades de cor da paleta, em ordem estavel. A lista se mantem
        /// sozinha: um token novo passa a ser considerado sem ninguem lembrar de
        /// registra-lo aqui.
        /// </summary>
        private static PropertyInfo[] TokenProperties()
        {
            if (tokenProperties != null) return tokenProperties;

            List<PropertyInfo> found = new List<PropertyInfo>();
            foreach (PropertyInfo p in typeof(OpenLadderPalette).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (p.PropertyType != typeof(Color)) continue;
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                found.Add(p);
            }
            found.Sort(delegate(PropertyInfo a, PropertyInfo b) { return string.CompareOrdinal(a.Name, b.Name); });

            tokenProperties = found.ToArray();
            return tokenProperties;
        }

        private static Color[] TokenValues()
        {
            PropertyInfo[] props = TokenProperties();
            Color[] values = new Color[props.Length];
            for (int i = 0; i < props.Length; i++) values[i] = (Color)props[i].GetValue(null, null);
            return values;
        }

        /// <summary>
        /// De-para do tema que sai para o que entra, indexado pelo ARGB de origem.
        /// Dois tokens com a mesma cor de origem chegam ao mesmo destino nesta
        /// paleta, entao a primeira entrada basta.
        /// </summary>
        private static Dictionary<int, Color> BuildRemap(Color[] saindo, Color[] entrando)
        {
            Dictionary<int, Color> map = new Dictionary<int, Color>();
            for (int i = 0; i < saindo.Length && i < entrando.Length; i++)
            {
                int key = saindo[i].ToArgb();
                if (key == entrando[i].ToArgb()) continue;
                if (!map.ContainsKey(key)) map.Add(key, entrando[i]);
            }
            return map;
        }

        /// <summary>
        /// Repinta as janelas abertas na troca de tema. Sem isto o usuario escolhe
        /// outro tema e a tela so muda quando o programa e reaberto.
        /// </summary>
        private static void RestyleOpenForms(Dictionary<int, Color> map)
        {
            if (map.Count == 0) return;

            // Copia a colecao antes de percorrer: repintar pode abrir ou fechar janela.
            List<Form> open = new List<Form>();
            foreach (Form f in Application.OpenForms) open.Add(f);

            for (int i = 0; i < open.Count; i++)
            {
                Form form = open[i];
                if (form == null || form.IsDisposed) continue;
                try
                {
                    if (form.InvokeRequired)
                    {
                        form.BeginInvoke(new RestyleCallback(RestyleForm), new object[] { form, map });
                        continue;
                    }
                    RestyleForm(form, map);
                }
                catch
                {
                    // Uma janela que recusa a repintura nao pode impedir as outras.
                }
            }
        }

        private delegate void RestyleCallback(Form form, Dictionary<int, Color> map);

        private static void RestyleForm(Form form, Dictionary<int, Color> map)
        {
            if (form == null || form.IsDisposed) return;
            Restyle(form, map);
            form.Invalidate(true);
        }

        private static void Restyle(Control c, Dictionary<int, Color> map)
        {
            if (c == null || c.IsDisposed) return;

            Color swap;
            if (map.TryGetValue(c.BackColor.ToArgb(), out swap)) c.BackColor = swap;
            if (map.TryGetValue(c.ForeColor.ToArgb(), out swap)) c.ForeColor = swap;

            ButtonBase button = c as ButtonBase;
            if (button != null)
            {
                FlatButtonAppearance flat = button.FlatAppearance;
                if (map.TryGetValue(flat.BorderColor.ToArgb(), out swap)) flat.BorderColor = swap;
                if (map.TryGetValue(flat.MouseOverBackColor.ToArgb(), out swap)) flat.MouseOverBackColor = swap;
                if (map.TryGetValue(flat.MouseDownBackColor.ToArgb(), out swap)) flat.MouseDownBackColor = swap;
            }

            DataGridView grid = c as DataGridView;
            if (grid != null) RestyleGrid(grid, map);

            ToolStrip strip = c as ToolStrip;
            if (strip != null)
            {
                for (int i = 0; i < strip.Items.Count; i++) RestyleItem(strip.Items[i], map);
            }

            for (int i = 0; i < c.Controls.Count; i++) Restyle(c.Controls[i], map);
        }

        private static void RestyleItem(ToolStripItem item, Dictionary<int, Color> map)
        {
            if (item == null) return;

            Color swap;
            if (map.TryGetValue(item.BackColor.ToArgb(), out swap)) item.BackColor = swap;
            if (map.TryGetValue(item.ForeColor.ToArgb(), out swap)) item.ForeColor = swap;

            ToolStripDropDownItem drop = item as ToolStripDropDownItem;
            if (drop == null) return;
            for (int i = 0; i < drop.DropDownItems.Count; i++) RestyleItem(drop.DropDownItems[i], map);
        }

        private static void RestyleGrid(DataGridView grid, Dictionary<int, Color> map)
        {
            Color swap;
            if (map.TryGetValue(grid.BackgroundColor.ToArgb(), out swap)) grid.BackgroundColor = swap;
            if (map.TryGetValue(grid.GridColor.ToArgb(), out swap)) grid.GridColor = swap;

            RestyleCellStyle(grid.DefaultCellStyle, map);
            RestyleCellStyle(grid.AlternatingRowsDefaultCellStyle, map);
            RestyleCellStyle(grid.ColumnHeadersDefaultCellStyle, map);
            RestyleCellStyle(grid.RowHeadersDefaultCellStyle, map);
        }

        private static void RestyleCellStyle(DataGridViewCellStyle style, Dictionary<int, Color> map)
        {
            if (style == null) return;

            Color swap;
            if (map.TryGetValue(style.BackColor.ToArgb(), out swap)) style.BackColor = swap;
            if (map.TryGetValue(style.ForeColor.ToArgb(), out swap)) style.ForeColor = swap;
            if (map.TryGetValue(style.SelectionBackColor.ToArgb(), out swap)) style.SelectionBackColor = swap;
            if (map.TryGetValue(style.SelectionForeColor.ToArgb(), out swap)) style.SelectionForeColor = swap;
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

    /// <summary>
    /// Medidas do sistema visual, em pixels a 96 dpi, para toda tela usar a mesma
    /// escala de espacamento e de raio. Quem desenha na mao converte com
    /// <see cref="Px"/>; controle posicionado pelo layout ja e escalado por AutoScaleMode.
    /// </summary>
    internal static class OpenLadderMetrics
    {
        // Espacamento, sempre multiplo de 4.
        public const int Space1 = 4;    // microespaco
        public const int Space2 = 8;    // entre icone e conteudo
        public const int Space3 = 12;   // margem interna compacta
        public const int Space4 = 16;   // margem interna de painel
        public const int Space6 = 24;   // separacao entre blocos funcionais
        public const int Space8 = 32;

        // Raio de canto. Controle pequeno (botao, campo, selo), cartao e janela flutuante.
        public const int RadiusSmall = 4;
        public const int RadiusMedium = 8;
        public const int RadiusLarge = 12;

        // Traco base. Borda e sempre de 1 px; selecao e foco usam FocusWidth.
        public const int Hairline = 1;
        public const int FocusWidth = 2;

        /// <summary>Converte uma medida de 96 dpi para os pixels reais do controle.</summary>
        public static int Px(Control control, int value)
        {
            if (control == null) return value;
            try
            {
                using (Graphics g = control.CreateGraphics())
                    return (int)Math.Round(value * g.DpiX / 96.0f);
            }
            catch
            {
                return value;
            }
        }

        /// <summary>Retangulo de cantos arredondados (raio em pixels). Raio zero devolve um retangulo comum.</summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2)) * 2;
            if (d == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Barra de titulo nativa. O Windows a desenha em claro por padrao, o que corta
    /// uma janela escura ao meio; o DWM aceita o modo escuro por janela. Ponto unico
    /// para todos os executaveis, e acompanha a troca de tema.
    /// </summary>
    internal static class WindowChrome
    {
        private const int DwmaBorderColor = 34;
        private const int DwmaCaptionColor = 35;
        private const int DwmaTextColor = 36;
        private const int DwmaUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmaUseImmersiveDarkMode = 20;

        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;

        private static readonly List<Form> hooked = new List<Form>();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        /// <summary>Aplica o tema a janela agora (se ja tem alca) e a cada vez que a alca for recriada.</summary>
        public static void Hook(Form form)
        {
            if (form == null || form.IsDisposed) return;

            if (!hooked.Contains(form))
            {
                hooked.Add(form);
                form.HandleCreated += delegate { Apply(form, false); };
                form.Disposed += delegate { hooked.Remove(form); };
                if (form.IsHandleCreated) Apply(form, form.Visible);
            }
        }

        /// <summary>Reaplica em todas as janelas conhecidas, depois de uma troca de tema.</summary>
        public static void RefreshAll()
        {
            Form[] snapshot = hooked.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                Form form = snapshot[i];
                if (form == null || form.IsDisposed || !form.IsHandleCreated) continue;
                Apply(form, true);
            }
        }

        private static void Apply(Form form, bool repaintFrame)
        {
            try
            {
                if (form == null || form.IsDisposed || !form.IsHandleCreated) return;

                int dark = OpenLadderPalette.IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(form.Handle, DwmaUseImmersiveDarkMode, ref dark, 4) != 0)
                    DwmSetWindowAttribute(form.Handle, DwmaUseImmersiveDarkModeBefore20H1, ref dark, 4);

                // Windows 11 deixa escolher a cor da barra; o que nao suporta ignora.
                int caption = ToColorRef(OpenLadderPalette.Shell);
                int text = ToColorRef(OpenLadderPalette.Fore);
                int border = ToColorRef(OpenLadderPalette.Border);
                DwmSetWindowAttribute(form.Handle, DwmaCaptionColor, ref caption, 4);
                DwmSetWindowAttribute(form.Handle, DwmaTextColor, ref text, 4);
                DwmSetWindowAttribute(form.Handle, DwmaBorderColor, ref border, 4);

                // O atributo so e relido quando a moldura e recalculada.
                if (repaintFrame)
                    SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0,
                        SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
            }
            catch
            {
                // dwmapi ausente ou recusa: a barra fica no padrao do sistema.
            }
        }

        private static int ToColorRef(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }
    }

    /// <summary>
    /// Renderizador de menus e barras na paleta. Vale para todo ToolStrip que nao
    /// definiu o seu: menu de contexto, menu suspenso, barra de status de dialogo.
    /// </summary>
    internal sealed class OpenLadderToolStripRenderer : ToolStripProfessionalRenderer
    {
        public OpenLadderToolStripRenderer() : base(new OpenLadderColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Itens de barra de menu chegam aqui com o preto de sistema mesmo quando a
            // barra herdou ForeColor da janela, entao a cor e decidida aqui.
            Color own = e.Item.ForeColor;
            bool system = own == SystemColors.ControlText || own == SystemColors.MenuText || own == SystemColors.WindowText;
            if (e.Item.Enabled) e.TextColor = system ? OpenLadderPalette.Fore : own;
            else if (system) e.TextColor = OpenLadderPalette.Disabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? OpenLadderPalette.Muted : OpenLadderPalette.Disabled;
            base.OnRenderArrow(e);
        }
    }

    internal sealed class OpenLadderColorTable : ProfessionalColorTable
    {
        public OpenLadderColorTable()
        {
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground { get { return OpenLadderPalette.Raised; } }
        public override Color ImageMarginGradientBegin { get { return OpenLadderPalette.Raised; } }
        public override Color ImageMarginGradientMiddle { get { return OpenLadderPalette.Raised; } }
        public override Color ImageMarginGradientEnd { get { return OpenLadderPalette.Raised; } }
        public override Color ImageMarginRevealedGradientBegin { get { return OpenLadderPalette.Raised; } }
        public override Color ImageMarginRevealedGradientMiddle { get { return OpenLadderPalette.Raised; } }
        public override Color ImageMarginRevealedGradientEnd { get { return OpenLadderPalette.Raised; } }
        public override Color MenuBorder { get { return OpenLadderPalette.BorderStrong; } }
        public override Color MenuItemBorder { get { return OpenLadderPalette.SelectionEdge; } }
        public override Color MenuItemSelected { get { return OpenLadderPalette.NavActive; } }
        public override Color MenuItemSelectedGradientBegin { get { return OpenLadderPalette.NavActive; } }
        public override Color MenuItemSelectedGradientEnd { get { return OpenLadderPalette.NavActive; } }
        public override Color MenuItemPressedGradientBegin { get { return OpenLadderPalette.NavActive; } }
        public override Color MenuItemPressedGradientMiddle { get { return OpenLadderPalette.NavActive; } }
        public override Color MenuItemPressedGradientEnd { get { return OpenLadderPalette.NavActive; } }
        public override Color MenuStripGradientBegin { get { return OpenLadderPalette.Shell; } }
        public override Color MenuStripGradientEnd { get { return OpenLadderPalette.Shell; } }
        public override Color SeparatorDark { get { return OpenLadderPalette.Border; } }
        public override Color SeparatorLight { get { return OpenLadderPalette.Border; } }
        public override Color CheckBackground { get { return OpenLadderPalette.SelectionFill; } }
        public override Color CheckSelectedBackground { get { return OpenLadderPalette.SelectionFill; } }
        public override Color CheckPressedBackground { get { return OpenLadderPalette.SelectionFill; } }
        public override Color ToolStripBorder { get { return OpenLadderPalette.Border; } }
        public override Color ToolStripGradientBegin { get { return OpenLadderPalette.Chrome; } }
        public override Color ToolStripGradientMiddle { get { return OpenLadderPalette.Chrome; } }
        public override Color ToolStripGradientEnd { get { return OpenLadderPalette.Chrome; } }
        public override Color ToolStripContentPanelGradientBegin { get { return OpenLadderPalette.Chrome; } }
        public override Color ToolStripContentPanelGradientEnd { get { return OpenLadderPalette.Chrome; } }
        public override Color ToolStripPanelGradientBegin { get { return OpenLadderPalette.Shell; } }
        public override Color ToolStripPanelGradientEnd { get { return OpenLadderPalette.Shell; } }
        public override Color StatusStripGradientBegin { get { return OpenLadderPalette.Chrome; } }
        public override Color StatusStripGradientEnd { get { return OpenLadderPalette.Chrome; } }
        public override Color ButtonSelectedHighlight { get { return OpenLadderPalette.NavHover; } }
        public override Color ButtonSelectedGradientBegin { get { return OpenLadderPalette.NavHover; } }
        public override Color ButtonSelectedGradientMiddle { get { return OpenLadderPalette.NavHover; } }
        public override Color ButtonSelectedGradientEnd { get { return OpenLadderPalette.NavHover; } }
        public override Color ButtonSelectedBorder { get { return OpenLadderPalette.BorderStrong; } }
        public override Color ButtonPressedHighlight { get { return OpenLadderPalette.NavActive; } }
        public override Color ButtonPressedGradientBegin { get { return OpenLadderPalette.NavActive; } }
        public override Color ButtonPressedGradientMiddle { get { return OpenLadderPalette.NavActive; } }
        public override Color ButtonPressedGradientEnd { get { return OpenLadderPalette.NavActive; } }
        public override Color ButtonPressedBorder { get { return OpenLadderPalette.SelectionEdge; } }
        public override Color GripDark { get { return OpenLadderPalette.BorderStrong; } }
        public override Color GripLight { get { return OpenLadderPalette.Border; } }
        public override Color OverflowButtonGradientBegin { get { return OpenLadderPalette.Chrome; } }
        public override Color OverflowButtonGradientMiddle { get { return OpenLadderPalette.Chrome; } }
        public override Color OverflowButtonGradientEnd { get { return OpenLadderPalette.Chrome; } }
    }
}
