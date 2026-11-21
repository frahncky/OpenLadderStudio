namespace OpenLadderStudio.UI;

/// <summary>
/// Mantém uma janela dentro da área útil do monitor. A 175% numa tela 1080p a área útil tem
/// cerca de 1097x590 pixels lógicos: o tamanho escalado pelo DPI pode passar dela, e a
/// janela abriria cortada. Vale na abertura e quando o DPI muda com a janela aberta.
/// </summary>
internal static class WindowFit
{
    public static void Attach(Form form)
    {
        form.Load += (_, _) => Fit(form);
        form.DpiChanged += (_, _) => form.BeginInvoke(() => Fit(form));
    }

    public static void Fit(Form form)
    {
        Rectangle area = Screen.FromControl(form).WorkingArea;
        form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height));
        var fitted = new Size(Math.Min(form.Width, area.Width), Math.Min(form.Height, area.Height));
        if (fitted != form.Size)
        {
            // Encolheu: centraliza, como a abertura centralizada faria com o tamanho certo.
            form.Size = fitted;
            form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
            return;
        }
        form.Location = new Point(
            Math.Clamp(form.Left, area.Left, area.Right - form.Width),
            Math.Clamp(form.Top, area.Top, area.Bottom - form.Height));
    }
}
