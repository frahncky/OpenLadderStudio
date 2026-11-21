using System.Text;
using OpenLadderStudio.Application;

namespace OpenLadderStudio.UI;

/// <summary>
/// Ferramentas TP02 offline do editor moderno: quadros WBP para inspeção, arquivo HEX e
/// importação de IL verificada. Nenhuma delas fala com o PLC.
/// </summary>
public sealed partial class MainForm
{
    private readonly Tp02OfflineTools tp02Offline = new();

    private void ShowWbpPreview()
    {
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;
        using var dialog = new Tp02WbpDialog(tp02Offline, project);
        dialog.ShowDialog(this);
    }

    private void ExportHex()
    {
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;

        string hex;
        try
        {
            hex = tp02Offline.ExportHex(project);
        }
        catch (InvalidOperationException ex)
        {
            // Mesma mensagem da verificação: o relatório mostra onde está o erro.
            VerifyProgram();
            status.Text = ex.Message.Split('.')[0] + ". Nada foi exportado.";
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Palavras TP02 em hexadecimal (*.tp02.hex)|*.tp02.hex|Todos os arquivos (*.*)|*.*",
            DefaultExt = "hex",
            FileName = (string.IsNullOrEmpty(currentPath) ? "Projeto_TP02" : Path.GetFileNameWithoutExtension(currentPath)) + ".tp02.hex"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, hex, Encoding.ASCII);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Não foi possível exportar o HEX.", ex);
            return;
        }
        status.Text = "HEX do TP02 exportado: " + dialog.FileName + ". Nada foi transmitido ao PLC.";
    }

    private void ImportIl()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "IL verificada (*.verified.il.txt)|*.verified.il.txt|Texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string text;
        try
        {
            text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError("Não foi possível ler a IL.", ex);
            return;
        }
        ApplyIlImport(tp02Offline.ImportIl(text, Path.GetFileName(dialog.FileName)));
    }

    /// <summary>Conversão bloqueada só mostra o relatório; liberada substitui o projeto, ainda não salvo.</summary>
    private void ApplyIlImport(Tp02IlImport import)
    {
        if (!import.Success || import.Project is null)
        {
            report.Text = import.Report.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            ShowReport(true);
            status.Text = "Importação da IL bloqueada: " + import.Errors.Count + " item(ns). O projeto atual não foi alterado.";
            return;
        }
        if (!ConfirmDiscard()) return;
        StopSimulation();
        project = import.Project;
        currentPath = null;
        history.Clear();
        savedState = projects.Serialize(projects.CreateNew());
        LoadIntoGrid();
        MarkDirty();
        ClearVerification();
        report.Text = import.Report.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        ShowReport(true);
        status.Text = "IL importada: " + import.Project.Rungs.Count + " linha(s). Salve para manter o projeto.";
    }
}
