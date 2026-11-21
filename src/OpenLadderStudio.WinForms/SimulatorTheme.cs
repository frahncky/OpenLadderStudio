using System.Drawing;
using OpenLadderStudio.WinForms;

namespace ModernPC12
{
    /// <summary>
    /// Cores por papel usadas pelo ProcessSynoptic compartilhado, na trilha moderna. Mesmo
    /// mapeamento do SimulatorTheme legado (LadderSimulator.cs), lido da paleta moderna, que
    /// usa os mesmos tokens. A cena mantém fundo escuro nos dois temas.
    /// </summary>
    internal static class SimulatorTheme
    {
        public static Color Shell => ModernPalette.Shell;
        public static Color Chrome => ModernPalette.Chrome;
        public static Color Panel => ModernPalette.ChromeLight;
        public static Color Border => ModernPalette.Border;
        public static Color Accent => ModernPalette.Accent;
        public static Color Fore => ModernPalette.Fore;
        public static Color Muted => ModernPalette.Muted;
        public static Color Error => ModernPalette.Danger;
        public static Color Warning => ModernPalette.Warning;
        public static Color Info => ModernPalette.Info;

        public static Color Metal => Color.FromArgb(OpenLadderPaletteTokens.SceneMetal.Argb(true));
        public static Color Cargo => Color.FromArgb(OpenLadderPaletteTokens.SceneCargo.Argb(true));
        public static Color Dark => Color.FromArgb(OpenLadderPaletteTokens.SceneDark.Argb(true));

        public static Color For(SimTone tone) => tone switch
        {
            SimTone.Structure => Metal,
            SimTone.Active => Accent,
            SimTone.Info => Info,
            SimTone.Warning => Warning,
            SimTone.Danger => Error,
            SimTone.Cargo => Cargo,
            SimTone.Muted => Muted,
            SimTone.Dark => Dark,
            _ => Fore
        };

        /// <summary>Cor de texto legível sobre um preenchimento do tom indicado.</summary>
        public static Color OnFill(SimTone tone) =>
            tone is SimTone.Active or SimTone.Warning or SimTone.Info or SimTone.Cargo ? Shell : Fore;
    }
}
