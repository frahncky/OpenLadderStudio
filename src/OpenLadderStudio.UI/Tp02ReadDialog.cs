using OpenLadderStudio.Core;
using OpenLadderStudio.Drivers.TP02;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Leitura do programa do TP02 pela porta serial. Somente leitura: o driver só deixa sair
/// HELLO, F0, 38 e 34, então o PLC não muda de modo nem tem a memória alterada.
/// </summary>
public sealed class Tp02ReadDialog : Form
{
    private readonly Tp02Driver driver;
    private readonly ComboBox port = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Anchor = AnchorStyles.Left };
    private readonly Button refresh = new() { Text = "Atualizar portas", AutoSize = true };
    private readonly Button read = new() { Text = "Ler do PLC", AutoSize = true };
    private readonly Button cancel = new() { Text = "Cancelar leitura", AutoSize = true, Enabled = false };
    private readonly Button open = new() { Text = "Abrir no editor", AutoSize = true, Enabled = false };
    private readonly Label state = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(12, 6, 3, 3) };
    private readonly TextBox log = new();
    private CancellationTokenSource? reading;
    private bool closeWhenDone;

    public Tp02ReadDialog(Tp02Driver driver)
    {
        this.driver = driver;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Ler programa do TP02 (somente leitura)";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 560);
        MinimumSize = new Size(640, 400);
        ShowInTaskbar = false;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        bar.Controls.Add(new Label { Text = "Porta", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) });
        bar.Controls.AddRange(new Control[] { port, refresh, read, cancel, open, state });

        var notice = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(11, 0, 8, 8),
            Text = "Cabo TP-232PG, 19200 8O1. Só são enviados HELLO, F0, 38 e 34: o PLC não muda de modo e nada é gravado nele."
        };

        log.Dock = DockStyle.Fill;
        log.Multiline = true;
        log.ReadOnly = true;
        log.WordWrap = false;
        log.ScrollBars = ScrollBars.Both;
        log.Font = new Font(FontFamily.GenericMonospace, 9F);

        Controls.Add(log);
        Controls.Add(notice);
        Controls.Add(bar);

        refresh.Click += (_, _) => LoadPorts();
        read.Click += (_, _) => StartRead();
        cancel.Click += (_, _) => reading?.Cancel();
        open.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        WindowFit.Attach(this);
        ModernPalette.Apply(this);
        ResumeLayout(false);
        PerformLayout();
        LoadPorts();
    }

    /// <summary>Resultado da última leitura concluída; nulo enquanto nada foi lido.</summary>
    public Tp02PgReadResult? Result { get; private set; }

    public string PortName => port.SelectedItem as string ?? string.Empty;

    private void LoadPorts()
    {
        string? previous = port.SelectedItem as string;
        port.Items.Clear();
        port.Items.AddRange(Tp02SerialLink.PortNames());
        if (previous != null && port.Items.Contains(previous)) port.SelectedItem = previous;
        else if (port.Items.Count > 0) port.SelectedIndex = 0;
        read.Enabled = port.Items.Count > 0 && reading == null;
        state.Text = port.Items.Count == 0 ? "Nenhuma porta COM encontrada." : string.Empty;
    }

    private async void StartRead()
    {
        string name = PortName;
        if (name.Length == 0) return;
        Result = null;
        log.Clear();
        reading = new CancellationTokenSource();
        CancellationToken token = reading.Token;
        SetBusy(true);
        state.Text = "Lendo de " + name + "...";
        try
        {
            Tp02PgReadResult result = await Task.Run(() => driver.ReadProgram(name, Append, token));
            Result = result;
            state.Text = result.Steps.Count + " passo(s), END em " + result.EndStep.ToString("0000")
                + ", PLC em " + result.PlcState
                + (result.BrawMismatches > 0 ? ", " + result.BrawMismatches + " BRAW divergente(s)" : string.Empty) + ".";
        }
        catch (OperationCanceledException)
        {
            state.Text = "Leitura cancelada.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or TimeoutException or UnauthorizedAccessException or InvalidOperationException)
        {
            Append("FALHA: " + ex.Message);
            state.Text = "A leitura falhou. Confira o cabo, a porta e se o PLC está ligado.";
        }
        finally
        {
            reading.Dispose();
            reading = null;
            SetBusy(false);
        }
        if (closeWhenDone) Close();
    }

    private void SetBusy(bool busy)
    {
        port.Enabled = refresh.Enabled = !busy;
        read.Enabled = !busy && port.Items.Count > 0;
        cancel.Enabled = busy;
        open.Enabled = !busy && Result != null;
        UseWaitCursor = busy;
    }

    private void Append(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => Append(line));
            return;
        }
        log.AppendText(line + Environment.NewLine);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // A porta é fechada pela própria leitura: fechar no meio só cancela e espera ela terminar.
        if (reading != null)
        {
            e.Cancel = true;
            closeWhenDone = true;
            reading.Cancel();
            state.Text = "Cancelando a leitura...";
            return;
        }
        base.OnFormClosing(e);
    }
}
