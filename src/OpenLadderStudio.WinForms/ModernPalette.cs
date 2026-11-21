using System.Runtime.InteropServices;
using ModernPC12;

namespace OpenLadderStudio.WinForms;

/// <summary>
/// Paleta da trilha moderna. Não tem cor própria: lê os mesmos tokens que a OpenLadderPalette
/// do editor legado (OpenLadderPaletteTokens, no núcleo) e a mesma preferência de tema
/// (%APPDATA%\OpenLadder Studio\tema.txt, "escuro" ou "claro"; sem o arquivo, claro, como no
/// legado). Trocar o tema aqui troca também no editor legado, e vice-versa.
/// </summary>
public static class ModernPalette
{
    private static bool dark = LoadPreference();

    public static event EventHandler Changed;

    public static bool IsDark => dark;

    public static void Use(bool useDark)
    {
        if (dark == useDark) return;
        dark = useDark;
        SavePreference();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    // Superfícies, do fundo para a frente: Shell < Workspace < Chrome < ChromeLight < Raised.
    public static Color Shell => Of(OpenLadderPaletteTokens.Shell);
    public static Color Workspace => Of(OpenLadderPaletteTokens.Workspace);
    public static Color Chrome => Of(OpenLadderPaletteTokens.Chrome);
    public static Color ChromeLight => Of(OpenLadderPaletteTokens.ChromeLight);
    public static Color Raised => Of(OpenLadderPaletteTokens.Raised);
    public static Color Border => Of(OpenLadderPaletteTokens.Border);
    public static Color BorderStrong => Of(OpenLadderPaletteTokens.BorderStrong);
    public static Color HeaderLine => Of(OpenLadderPaletteTokens.HeaderLine);
    public static Color NavHover => Of(OpenLadderPaletteTokens.NavHover);
    public static Color NavActive => Of(OpenLadderPaletteTokens.NavActive);
    public static Color Fore => Of(OpenLadderPaletteTokens.Fore);
    public static Color Muted => Of(OpenLadderPaletteTokens.Muted);
    public static Color Faint => Of(OpenLadderPaletteTokens.Faint);
    public static Color Disabled => Of(OpenLadderPaletteTokens.Disabled);
    public static Color Accent => Of(OpenLadderPaletteTokens.Accent);
    public static Color OnAccent => Of(OpenLadderPaletteTokens.OnAccent);
    public static Color Focus => Of(OpenLadderPaletteTokens.Focus);
    public static Color Ok => Of(OpenLadderPaletteTokens.Ok);
    public static Color Warning => Of(OpenLadderPaletteTokens.Warning);
    public static Color Danger => Of(OpenLadderPaletteTokens.Danger);
    public static Color Info => Of(OpenLadderPaletteTokens.Info);
    public static Color OkBg => Of(OpenLadderPaletteTokens.OkBg);
    public static Color WarningBg => Of(OpenLadderPaletteTokens.WarningBg);
    public static Color DangerBg => Of(OpenLadderPaletteTokens.DangerBg);
    public static Color InfoBg => Of(OpenLadderPaletteTokens.InfoBg);
    public static Color Canvas => Of(OpenLadderPaletteTokens.Canvas);
    public static Color CanvasRow => Of(OpenLadderPaletteTokens.CanvasRow);
    public static Color CanvasHover => Of(OpenLadderPaletteTokens.CanvasHover);
    public static Color Rail => Of(OpenLadderPaletteTokens.Rail);
    public static Color Wire => Of(OpenLadderPaletteTokens.Wire);
    public static Color GridLine => Of(OpenLadderPaletteTokens.GridLine);
    public static Color SelectionFill => Of(OpenLadderPaletteTokens.SelectionFill);
    public static Color Energized => Of(OpenLadderPaletteTokens.Energized);
    public static Color EnergizedBg => Of(OpenLadderPaletteTokens.EnergizedBg);

    internal static Color Of(PaletteToken token) => Color.FromArgb(token.Argb(dark));

    /// <summary>
    /// Pinta uma janela e todos os controles dela com a paleta. Atribui cada cor explicitamente,
    /// então pode ser chamada de novo depois de trocar o tema para repintar a janela aberta.
    /// </summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ApplyOne(root);
        foreach (Control child in root.Controls) Apply(child);
        if (root is Form form)
        {
            if (form.IsHandleCreated) ApplyTitleBar(form);
            else form.HandleCreated -= OnFormHandleCreated;
            form.HandleCreated += OnFormHandleCreated;
        }
    }

    private static void OnFormHandleCreated(object sender, EventArgs e) => ApplyTitleBar((Form)sender);

    private static void ApplyOne(Control control)
    {
        ApplySystemTheme(control);
        switch (control)
        {
            case Form form:
                form.BackColor = Shell;
                form.ForeColor = Fore;
                break;
            case ToolStrip strip:
                strip.Renderer = new ModernToolStripRenderer();
                strip.BackColor = Chrome;
                strip.ForeColor = Fore;
                ApplyItems(strip.Items);
                if (strip.ContextMenuStrip != null) Apply(strip.ContextMenuStrip);
                break;
            case DataGridView grid:
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = Workspace;
                grid.GridColor = GridLine;
                grid.BorderStyle = BorderStyle.None;
                grid.DefaultCellStyle.BackColor = Canvas;
                grid.DefaultCellStyle.ForeColor = Fore;
                grid.DefaultCellStyle.SelectionBackColor = SelectionFill;
                grid.DefaultCellStyle.SelectionForeColor = Fore;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Chrome;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Chrome;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Muted;
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                grid.RowHeadersDefaultCellStyle.BackColor = Chrome;
                grid.RowHeadersDefaultCellStyle.ForeColor = Muted;
                break;
            case ListView list:
                list.BackColor = ChromeLight;
                list.ForeColor = Fore;
                list.BorderStyle = BorderStyle.FixedSingle;
                // O cabeçalho do ListView é desenhado pelo sistema, sempre claro: desenha com a paleta.
                if (!list.OwnerDraw)
                {
                    list.OwnerDraw = true;
                    list.DrawColumnHeader += DrawListHeader;
                    // A seleção do sistema escreve texto escuro sobre fundo escuro no tema escuro:
                    // linha e seleção também são desenhadas com a paleta.
                    list.DrawItem += (_, e) => e.DrawDefault = false;
                    list.DrawSubItem += DrawListSubItem;
                }
                list.Invalidate();
                break;
            case TextBoxBase box:
                box.BackColor = box.ReadOnly ? Canvas : ChromeLight;
                box.ForeColor = Fore;
                box.BorderStyle = BorderStyle.FixedSingle;
                break;
            case NumericUpDown number:
                number.BackColor = ChromeLight;
                number.ForeColor = Fore;
                number.BorderStyle = BorderStyle.FixedSingle;
                break;
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = ChromeLight;
                button.ForeColor = Fore;
                button.FlatAppearance.BorderColor = BorderStrong;
                button.FlatAppearance.MouseOverBackColor = NavHover;
                button.FlatAppearance.MouseDownBackColor = NavActive;
                break;
            case Splitter splitter:
                splitter.BackColor = Border;
                break;
            case Label label:
                label.ForeColor = Fore;
                break;
            default:
                if (control is Panel or FlowLayoutPanel or TableLayoutPanel or UserControl)
                {
                    control.BackColor = Shell;
                    control.ForeColor = Fore;
                }
                break;
        }
        if (control.ContextMenuStrip != null && control is not ToolStrip) Apply(control.ContextMenuStrip);
    }

    private static void ApplyItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = Fore;
            if (item is ToolStripDropDownItem dropDown)
            {
                dropDown.DropDown.BackColor = Raised;
                dropDown.DropDown.ForeColor = Fore;
                dropDown.DropDown.Renderer = new ModernToolStripRenderer();
                ApplyItems(dropDown.DropDownItems);
            }
        }
    }

    private static void DrawListHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var back = new SolidBrush(Chrome)) e.Graphics.FillRectangle(back, e.Bounds);
        using (var line = new Pen(Border))
        {
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
        }
        Rectangle text = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, e.Font, text, Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    private static void DrawListSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        var list = (ListView)sender;
        bool selected = e.Item != null && e.Item.Selected;
        using (var back = new SolidBrush(selected ? SelectionFill : list.BackColor)) e.Graphics.FillRectangle(back, e.Bounds);
        Rectangle text = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, list.Font, text, Fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if (selected && e.ColumnIndex == 0 && list.Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Item.Bounds, Fore, SelectionFill);
    }

    // Barras de rolagem e bordas de sistema no tema da paleta (Windows 10 1809 ou mais novo),
    // como o ControlTheme do editor legado; onde não existe, fica o estilo padrão.
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

    private static void ApplySystemTheme(Control control)
    {
        if (control is not (ScrollableControl or TextBoxBase or ListView or DataGridView or ListBox)) return;
        if (!control.IsHandleCreated)
        {
            control.HandleCreated -= OnThemedHandleCreated;
            control.HandleCreated += OnThemedHandleCreated;
            return;
        }
        try
        {
            SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static void OnThemedHandleCreated(object sender, EventArgs e) => ApplySystemTheme((Control)sender);

    // Barra de título escura no tema escuro (Windows 10 2004 ou mais novo); sem efeito onde não existe.
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static void ApplyTitleBar(Form form)
    {
        try
        {
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static string PreferencePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenLadder Studio", "tema.txt");

    private static bool LoadPreference()
    {
        try
        {
            string path = PreferencePath();
            if (!File.Exists(path)) return false;
            return !string.Equals(File.ReadAllText(path).Trim(), "claro", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void SavePreference()
    {
        try
        {
            string path = PreferencePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, dark ? "escuro" : "claro");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Menus e barras desenhados com a paleta, nos dois temas.</summary>
internal sealed class ModernToolStripRenderer : ToolStripProfessionalRenderer
{
    public ModernToolStripRenderer() : base(new ModernColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? ModernPalette.Fore : ModernPalette.Disabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item.Enabled ? ModernPalette.Muted : ModernPalette.Disabled;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // Marca desenhada com a cor de texto: a imagem padrão do sistema é preta e some no tema escuro.
        Rectangle box = e.ImageRectangle;
        using (var fill = new SolidBrush(ModernPalette.SelectionFill)) e.Graphics.FillRectangle(fill, box);
        using var pen = new Pen(ModernPalette.Fore, Math.Max(1.5f, box.Height / 8f));
        float x = box.Left, y = box.Top, w = box.Width, h = box.Height;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, new[]
        {
            new PointF(x + w * 0.25f, y + h * 0.52f),
            new PointF(x + w * 0.43f, y + h * 0.70f),
            new PointF(x + w * 0.76f, y + h * 0.32f)
        });
    }
}

internal sealed class ModernColorTable : ProfessionalColorTable
{
    public ModernColorTable() => UseSystemColors = false;

    public override Color MenuStripGradientBegin => ModernPalette.Chrome;
    public override Color MenuStripGradientEnd => ModernPalette.Chrome;
    public override Color StatusStripGradientBegin => ModernPalette.Chrome;
    public override Color StatusStripGradientEnd => ModernPalette.Chrome;
    public override Color ToolStripGradientBegin => ModernPalette.Chrome;
    public override Color ToolStripGradientMiddle => ModernPalette.Chrome;
    public override Color ToolStripGradientEnd => ModernPalette.Chrome;
    public override Color ToolStripBorder => ModernPalette.Border;
    public override Color ToolStripDropDownBackground => ModernPalette.Raised;
    public override Color ImageMarginGradientBegin => ModernPalette.Raised;
    public override Color ImageMarginGradientMiddle => ModernPalette.Raised;
    public override Color ImageMarginGradientEnd => ModernPalette.Raised;
    public override Color MenuBorder => ModernPalette.BorderStrong;
    public override Color MenuItemBorder => ModernPalette.Accent;
    public override Color MenuItemSelected => ModernPalette.NavHover;
    public override Color MenuItemSelectedGradientBegin => ModernPalette.NavHover;
    public override Color MenuItemSelectedGradientEnd => ModernPalette.NavHover;
    public override Color MenuItemPressedGradientBegin => ModernPalette.NavActive;
    public override Color MenuItemPressedGradientMiddle => ModernPalette.NavActive;
    public override Color MenuItemPressedGradientEnd => ModernPalette.NavActive;
    public override Color SeparatorDark => ModernPalette.Border;
    public override Color SeparatorLight => ModernPalette.Border;
    public override Color CheckBackground => ModernPalette.SelectionFill;
    public override Color CheckSelectedBackground => ModernPalette.SelectionFill;
    public override Color CheckPressedBackground => ModernPalette.SelectionFill;
    public override Color ButtonSelectedHighlight => ModernPalette.NavHover;
    public override Color ButtonSelectedBorder => ModernPalette.Accent;
}
