using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Ajuda → Verificar atualizações: versão instalada e disponível, download com progresso e
/// conferência do SHA-256. Termina com o instalador verificado em <see cref="InstallerPath"/>;
/// quem executa é a janela principal, depois de cuidar do projeto aberto.
/// </summary>
public sealed class UpdateDialog : Form
{
    private readonly UpdateService service;
    private readonly string downloadFolder;
    private readonly Label installed = new() { AutoSize = true, Margin = new Padding(0, 0, 40, 0) };
    private readonly Label available = new() { AutoSize = true, Text = "—", Margin = new Padding(0) };
    private readonly Label message = new() { AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0), Visible = false };
    private readonly Button check = new() { Text = "Verificar de novo", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
    private readonly Button install = new() { Text = "Baixar e instalar", AutoSize = true, Enabled = false, Margin = new Padding(6, 0, 0, 0) };
    private readonly CancellationTokenSource cancel = new();
    private UpdateCheck? result;
    private bool busy;

    public UpdateDialog(UpdateService service, string downloadFolder)
    {
        this.service = service;
        this.downloadFolder = downloadFolder;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Verificar atualizações";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        installed.Text = "v" + service.CurrentVersion;
        var body = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(20, 16, 20, 8), Dock = DockStyle.Fill };
        body.Controls.Add(Caption("Instalada"), 0, 0);
        body.Controls.Add(Caption("Disponível"), 1, 0);
        body.Controls.Add(installed, 0, 1);
        body.Controls.Add(available, 1, 1);
        body.Controls.Add(message, 0, 2);
        body.SetColumnSpan(message, 2);
        body.Controls.Add(progress, 0, 3);
        body.SetColumnSpan(progress, 2);
        message.MaximumSize = new Size(420, 0);
        message.MinimumSize = new Size(420, 0);

        var close = new Button { Text = "Fechar", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(6, 0, 0, 0) };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(14, 8, 14, 12) };
        buttons.Controls.AddRange(new Control[] { close, install, check });
        CancelButton = close;

        Controls.Add(body);
        Controls.Add(buttons);

        check.Click += async (_, _) => await CheckAsync();
        install.Click += async (_, _) => await DownloadAsync();
        FormClosing += (_, _) => cancel.Cancel();
        Disposed += (_, _) => cancel.Dispose();
        Shown += async (_, _) => await CheckAsync();

        ModernPalette.Apply(this);
        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>Instalador baixado e com SHA-256 conferido; null se o usuário não chegou lá.</summary>
    public string? InstallerPath { get; private set; }

    public string Message => message.Text;
    public string AvailableText => available.Text;
    public bool CanInstall => install.Enabled;
    public bool Busy => busy;

    public async Task CheckAsync()
    {
        if (busy) return;
        SetBusy(true, "Consultando a última versão publicada...");
        try
        {
            result = await service.CheckAsync(cancel.Token);
            available.Text = "v" + result.Release.Version;
            switch (result.State)
            {
                case UpdateState.UpToDate:
                    Show("Você já está na versão mais recente.", ModernStatus.On);
                    break;
                case UpdateState.Incomplete:
                    Show("A versão " + result.Release.Version + " foi publicada sem o instalador ou sem o SHA-256. Tente mais tarde.", ModernStatus.Forced);
                    break;
                default:
                    Show("A versão " + result.Release.Version + " está disponível.", ModernStatus.None);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or TaskCanceledException)
        {
            Show(ex is HttpRequestException or TaskCanceledException
                ? "Sem conexão com o servidor de versões. Verifique a internet e tente de novo."
                : ex.Message, ModernStatus.Forced);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    public async Task DownloadAsync()
    {
        if (busy || result is not { State: UpdateState.Available }) return;
        SetBusy(true, "Baixando a versão " + result.Release.Version + "...");
        progress.Value = 0;
        progress.Visible = true;
        try
        {
            var report = new Progress<int>(value =>
            {
                if (IsDisposed) return;
                progress.Value = Math.Clamp(value, 0, 100);
                message.Text = "Baixando a versão " + result.Release.Version + "... " + progress.Value + "%";
            });
            InstallerPath = await service.DownloadVerifiedAsync(result.Release, downloadFolder, report, cancel.Token);
            progress.Value = 100;
            Show("Instalador baixado e conferido (SHA-256).", ModernStatus.On);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            progress.Visible = false;
            Show(ex is InvalidDataException ? "Atualização bloqueada: " + ex.Message
                : ex is HttpRequestException or TaskCanceledException ? "O download falhou. Verifique a internet e tente de novo."
                : "Não foi possível gravar o instalador: " + ex.Message, ModernStatus.Forced);
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void SetBusy(bool value, string? text)
    {
        if (IsDisposed) return;
        busy = value;
        check.Enabled = !value;
        install.Enabled = !value && result is { State: UpdateState.Available };
        if (text is not null) Show(text, ModernStatus.None);
        UseWaitCursor = value;
    }

    private void Show(string text, ModernStatus tone)
    {
        if (IsDisposed) return;
        message.Text = text;
        message.ForeColor = tone switch
        {
            ModernStatus.On => ModernPalette.Ok,
            ModernStatus.Forced => ModernPalette.Warning,
            _ => ModernPalette.Muted
        };
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        foreach (Label big in new[] { installed, available })
        {
            Font old = big.Font;
            big.Font = new Font(Font.FontFamily, Font.Size * 1.6f, FontStyle.Bold);
            if (old != Font) old.Dispose();
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        OnFontChanged(EventArgs.Empty);
        // Depois do tema: rótulos discretos e o resultado da consulta com a cor do estado.
        foreach (Label caption in Controls.OfType<TableLayoutPanel>().SelectMany(t => t.Controls.OfType<Label>()).Where(l => Equals(l.Tag, "caption")))
            caption.ForeColor = ModernPalette.Muted;
    }

    private static Label Caption(string text) =>
        new() { Text = text, AutoSize = true, Tag = "caption", Margin = new Padding(0, 0, 40, 2) };
}
