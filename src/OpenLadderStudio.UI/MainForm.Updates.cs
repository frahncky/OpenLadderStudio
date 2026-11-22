using System.Diagnostics;
using OpenLadderStudio.Application;

namespace OpenLadderStudio.UI;

/// <summary>
/// Ajuda → Verificar atualizações. O diálogo só baixa e confere; instalar fecha o programa,
/// então antes a janela pergunta pelo projeto não salvo, como em Novo e Abrir.
/// </summary>
public sealed partial class MainForm
{
    /// <summary>Rede e execução do instalador (substituíveis no teste da janela).</summary>
    private Func<IUpdateTransport> updateTransport = () => new HttpUpdateTransport();
    private Action<string> launchInstaller = path =>
        Process.Start(new ProcessStartInfo(path, UpdateService.InstallerArguments) { UseShellExecute = true });
    private string updateFolder = Path.Combine(Path.GetTempPath(), "OpenLadderStudioUpdate");

    /// <summary>Versão desta instalação (a mesma do version.txt, carimbada no build).</summary>
    private static string InstalledVersion =>
        typeof(MainForm).Assembly.GetName().Version is { } version ? version.ToString(2) : "0.0";

    private void CheckForUpdates()
    {
        IUpdateTransport transport = updateTransport();
        try
        {
            using var dialog = new UpdateDialog(new UpdateService(transport, InstalledVersion), updateFolder);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.InstallerPath is null) return;
            InstallUpdate(dialog.InstallerPath);
        }
        finally
        {
            (transport as IDisposable)?.Dispose();
        }
    }

    private void InstallUpdate(string installer)
    {
        if (!ConfirmDiscard())
        {
            status.Text = "Atualização adiada: o instalador baixado fica em " + Path.GetDirectoryName(installer) + ".";
            return;
        }
        try
        {
            launchInstaller(installer);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            ShowError("Não foi possível iniciar o instalador.", ex);
            return;
        }
        // O projeto já foi resolvido acima: fecha sem perguntar de novo.
        dirty = false;
        status.Text = "Instalando a atualização...";
        Close();
    }
}
