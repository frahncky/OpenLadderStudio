using System;

namespace ModernPC12
{
    /// <summary>
    /// Cor de um papel da paleta nos dois temas (RGB do escuro e do claro), sem depender da biblioteca
    /// de desenho: a conversão para Color fica com quem desenha.
    /// </summary>
    internal struct PaletteToken
    {
        public readonly int DarkR, DarkG, DarkB, LightR, LightG, LightB;

        public PaletteToken(int dr, int dg, int db, int lr, int lg, int lb)
        {
            DarkR = dr; DarkG = dg; DarkB = db;
            LightR = lr; LightG = lg; LightB = lb;
        }

        /// <summary>Mesmo valor nos dois temas (cena do sinóptico).</summary>
        public PaletteToken(int r, int g, int b) : this(r, g, b, r, g, b) { }

        public int Argb(bool dark)
        {
            return dark
                ? unchecked((int)0xFF000000) | (DarkR << 16) | (DarkG << 8) | DarkB
                : unchecked((int)0xFF000000) | (LightR << 16) | (LightG << 8) | LightB;
        }
    }

    /// <summary>
    /// Fonte única das cores do OpenLadder Studio. A OpenLadderPalette do editor legado
    /// (AppBranding.cs) e a paleta da trilha moderna leem daqui, então os dois produtos têm
    /// exatamente as mesmas cores. As regras de papel, hierarquia e contraste de cada token
    /// estão documentadas em AppBranding.cs e em docs/ui-guidelines.md.
    /// C# 5: este arquivo também é compilado pelo csc do .NET Framework.
    /// </summary>
    internal static class OpenLadderPaletteTokens
    {
        public static readonly PaletteToken Shell = new PaletteToken(15, 20, 29, 232, 237, 244);
        public static readonly PaletteToken Workspace = new PaletteToken(19, 25, 36, 238, 242, 249);
        public static readonly PaletteToken Chrome = new PaletteToken(25, 32, 45, 246, 248, 252);
        public static readonly PaletteToken ChromeLight = new PaletteToken(33, 42, 58, 252, 253, 255);
        public static readonly PaletteToken Raised = new PaletteToken(41, 52, 72, 254, 254, 255);
        public static readonly PaletteToken Border = new PaletteToken(48, 61, 82, 208, 217, 229);
        public static readonly PaletteToken BorderStrong = new PaletteToken(76, 94, 120, 163, 178, 199);
        public static readonly PaletteToken HeaderLine = new PaletteToken(88, 112, 142, 166, 185, 210);
        public static readonly PaletteToken NavBg = new PaletteToken(17, 23, 33, 240, 244, 249);
        public static readonly PaletteToken NavHover = new PaletteToken(33, 43, 60, 228, 235, 245);
        public static readonly PaletteToken NavActive = new PaletteToken(34, 64, 106, 214, 228, 248);
        public static readonly PaletteToken Fore = new PaletteToken(233, 239, 247, 30, 44, 62);
        public static readonly PaletteToken Muted = new PaletteToken(177, 190, 208, 77, 96, 119);
        public static readonly PaletteToken Faint = new PaletteToken(157, 173, 193, 82, 100, 122);
        public static readonly PaletteToken Disabled = new PaletteToken(112, 129, 150, 139, 153, 171);
        public static readonly PaletteToken Accent = new PaletteToken(99, 177, 255, 29, 84, 200);
        public static readonly PaletteToken AccentDark = new PaletteToken(61, 139, 230, 22, 62, 160);
        public static readonly PaletteToken OnAccent = new PaletteToken(16, 30, 48, 255, 255, 255);
        public static readonly PaletteToken Focus = new PaletteToken(147, 205, 255, 15, 70, 190);
        public static readonly PaletteToken Ok = new PaletteToken(96, 205, 153, 32, 113, 75);
        public static readonly PaletteToken Warning = new PaletteToken(242, 193, 94, 137, 91, 12);
        public static readonly PaletteToken Danger = new PaletteToken(247, 133, 139, 183, 48, 66);
        public static readonly PaletteToken Info = new PaletteToken(116, 188, 246, 29, 103, 170);
        public static readonly PaletteToken OkBg = new PaletteToken(22, 52, 40, 221, 243, 231);
        public static readonly PaletteToken WarningBg = new PaletteToken(60, 48, 22, 252, 240, 208);
        public static readonly PaletteToken DangerBg = new PaletteToken(64, 31, 37, 252, 225, 227);
        public static readonly PaletteToken InfoBg = new PaletteToken(27, 50, 80, 221, 236, 252);
        public static readonly PaletteToken Canvas = new PaletteToken(20, 27, 39, 250, 251, 254);
        public static readonly PaletteToken CanvasRow = new PaletteToken(26, 36, 52, 238, 243, 251);
        public static readonly PaletteToken CanvasHover = new PaletteToken(31, 42, 61, 231, 238, 249);
        public static readonly PaletteToken Rail = new PaletteToken(156, 181, 214, 74, 90, 107);
        public static readonly PaletteToken Wire = new PaletteToken(144, 169, 202, 95, 110, 128);
        public static readonly PaletteToken GridLine = new PaletteToken(37, 49, 68, 222, 229, 239);
        public static readonly PaletteToken SelectionFill = new PaletteToken(30, 60, 102, 211, 227, 250);
        public static readonly PaletteToken Energized = new PaletteToken(57, 224, 128, 0, 100, 45);
        public static readonly PaletteToken EnergizedBg = new PaletteToken(22, 66, 46, 220, 243, 229);

        // Cena do sinóptico: mantém fundo escuro nos dois temas, por convenção de sala de controle.
        public static readonly PaletteToken SceneMetal = new PaletteToken(86, 91, 98);
        public static readonly PaletteToken SceneCargo = new PaletteToken(198, 148, 84);
        public static readonly PaletteToken SceneDark = new PaletteToken(22, 24, 27);
        public static readonly PaletteToken SceneLampOff = new PaletteToken(64, 68, 74);
        /// <summary>Riscas da correia, desenhadas com alfa SceneBeltStripeAlpha sobre SceneMetal.</summary>
        public static readonly PaletteToken SceneBeltStripe = new PaletteToken(30, 33, 37);
        public const int SceneBeltStripeAlpha = 120;
    }
}
