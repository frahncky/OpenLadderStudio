using System.Drawing.Drawing2D;
using OpenLadderStudio.WinForms;

namespace ModernPC12
{
    /// <summary>
    /// Ponto de uso de cor do LadderCanvas compartilhado na trilha moderna. Tem o nome da
    /// paleta legada para que os arquivos do canvas compilem sem mudança nas duas trilhas,
    /// mas não tem valor próprio: cada papel vem da ModernPalette, que lê os mesmos tokens
    /// (OpenLadderPaletteTokens) que a OpenLadderPalette de AppBranding.cs.
    /// </summary>
    internal static class OpenLadderPalette
    {
        public static Color Accent => ModernPalette.Accent;
        public static Color Border => ModernPalette.Border;
        public static Color BorderStrong => ModernPalette.BorderStrong;
        public static Color Canvas => ModernPalette.Canvas;
        public static Color CanvasHover => ModernPalette.CanvasHover;
        public static Color CanvasRow => ModernPalette.CanvasRow;
        public static Color Chrome => ModernPalette.Chrome;
        public static Color ChromeLight => ModernPalette.ChromeLight;
        public static Color Energized => ModernPalette.Energized;
        public static Color EnergizedBg => ModernPalette.EnergizedBg;
        public static Color Faint => ModernPalette.Faint;
        public static Color Focus => ModernPalette.Focus;
        public static Color Fore => ModernPalette.Fore;
        public static Color GridLine => ModernPalette.GridLine;
        public static Color Info => ModernPalette.Info;
        public static Color Muted => ModernPalette.Muted;
        public static Color Rail => ModernPalette.Rail;
        public static Color Wire => ModernPalette.Wire;
        public static Color SelectionFill => ModernPalette.SelectionFill;
        public static Color SelectionEdge => ModernPalette.Accent;

        /// <summary>Mesma regra da paleta legada: transparência sobre uma cor da paleta, sem cor nova.</summary>
        public static Color Tint(Color baseColor, int alpha) => Color.FromArgb(alpha, baseColor);
    }

    /// <summary>Geometria usada pelo canvas. Mesma implementação de OpenLadderMetrics.RoundedRect (AppBranding.cs).</summary>
    internal static class OpenLadderMetrics
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
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
    /// Argumento do arrasto do LadderCanvas compartilhado. No build legado esta classe é
    /// injetada em LadderEditor.build.cs por PrepareLadderInteractionV81.ps1; a trilha moderna
    /// não passa pela cadeia de preparação e precisa da mesma definição. Este arquivo é só da
    /// trilha moderna (o Build.bat não o compila). Mantenha igual à da V81.
    /// </summary>
    internal sealed class LadderMoveEventArgs : EventArgs
    {
        public readonly int FromRung;
        public readonly int FromColumn;
        public readonly int FromLane;
        public readonly int ToRung;
        public readonly int ToColumn;

        public LadderMoveEventArgs(int fromRung, int fromColumn, int fromLane, int toRung, int toColumn)
        {
            FromRung = fromRung;
            FromColumn = fromColumn;
            FromLane = fromLane;
            ToRung = toRung;
            ToColumn = toColumn;
        }
    }
}
