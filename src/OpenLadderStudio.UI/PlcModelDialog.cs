using OpenLadderStudio.Application;
using OpenLadderStudio.WinForms;

namespace OpenLadderStudio.UI;

/// <summary>
/// Escolha do controlador alvo: lista de modelos (fabricante, modelo, protocolo, suporte) e,
/// para o selecionado, o driver de comunicação, o que ele faz e as observações do modelo.
/// Só troca o alvo; não abre porta nem fala com o PLC.
/// </summary>
public sealed class PlcModelDialog : Form
{
    private readonly PlcDriverCatalog catalog;
    private readonly ListView list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
    private readonly Label title = new() { AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
    private readonly Label details = new() { AutoSize = true, Margin = new Padding(0) };
    private readonly Button ok = new() { Text = "Usar este modelo", AutoSize = true, DialogResult = DialogResult.OK, Margin = new Padding(6, 0, 0, 0) };

    public PlcModelDialog(PlcDriverCatalog catalog, PlcDeviceProfile current)
    {
        this.catalog = catalog;
        Selected = current;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Modelo do PLC";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 520);
        MinimumSize = new Size(560, 400);
        ShowInTaskbar = false;
        MinimizeBox = false;

        list.Columns.Add("Fabricante", 150);
        list.Columns.Add("Modelo", 210);
        list.Columns.Add("Protocolo", 200);
        list.Columns.Add("Suporte", 110);
        foreach (PlcDeviceProfile profile in PlcProfileCatalog.Profiles)
        {
            var item = new ListViewItem(new[] { profile.Manufacturer, profile.Model, profile.Protocol, profile.SupportText }) { Tag = profile };
            item.SubItems[3].Tag = profile.SupportLevel switch
            {
                PlcSupportLevel.Implemented => ModernStatus.On,
                PlcSupportLevel.Experimental => ModernStatus.Forced,
                _ => ModernStatus.Off
            };
            list.Items.Add(item);
        }

        var info = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1, Padding = new Padding(14, 12, 14, 8) };
        info.Controls.Add(title);
        info.Controls.Add(details);
        title.Font = new Font(Font.FontFamily, Font.Size * 1.15f, FontStyle.Bold);

        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(6, 0, 0, 0) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(list);
        Controls.Add(info);
        Controls.Add(buttons);

        list.SelectedIndexChanged += (_, _) => ShowSelection();
        list.ItemActivate += (_, _) => { if (ok.Enabled) { DialogResult = DialogResult.OK; Close(); } };
        Resize += (_, _) => details.MaximumSize = new Size(Math.Max(200, ClientSize.Width - info.Padding.Horizontal), 0);
        // A coluna Protocolo fica com a sobra: sem área de cabeçalho vazia (desenhada pelo sistema).
        list.ClientSizeChanged += (_, _) => FitColumns();

        WindowFit.Attach(this);
        ModernPalette.Apply(this);
        ResumeLayout(false);
        PerformLayout();

        ListViewItem? selected = list.Items.Cast<ListViewItem>().FirstOrDefault(i => ((PlcDeviceProfile)i.Tag!).Id == current.Id);
        if (selected is not null)
        {
            selected.Selected = true;
            selected.Focused = true;
            selected.EnsureVisible();
        }
        ShowSelection();
    }

    /// <summary>Título 15% maior que a fonte da janela, que o WinForms escala com o DPI.</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Font old = title.Font;
        title.Font = new Font(Font.FontFamily, Font.Size * 1.15f, FontStyle.Bold);
        if (old != Font) old.Dispose();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        BeginInvoke(FitColumns);
    }

    public PlcDeviceProfile Selected { get; private set; }

    public ListView Models => list;

    public string Details => details.Text;

    public bool CanUse => ok.Enabled;

    private void FitColumns()
    {
        // Larguras a 96 DPI, convertidas para o monitor atual; Protocolo fica com a sobra.
        double scale = list.DeviceDpi / 96.0;
        int[] logical = { 150, 240, 0, 120 };
        int others = 0;
        for (int index = 0; index < list.Columns.Count; index++)
        {
            if (index == 2) continue;
            list.Columns[index].Width = (int)Math.Round(logical[index] * scale);
            others += list.Columns[index].Width;
        }
        list.Columns[2].Width = Math.Max((int)Math.Round(120 * scale), list.ClientSize.Width - others);
    }

    private void ShowSelection()
    {
        if (list.SelectedItems.Count == 0)
        {
            title.Text = "Nenhum modelo selecionado";
            details.Text = string.Empty;
            ok.Enabled = false;
            return;
        }
        var profile = (PlcDeviceProfile)list.SelectedItems[0].Tag!;
        title.Text = profile.DisplayName;
        var driver = PlcProfileCatalog.DriverFor(catalog, profile);
        string communication = profile.SupportLevel == PlcSupportLevel.Planned
            ? "Comunicação: ainda não implementada."
            : driver is null
                ? "Comunicação: nenhuma (o programa roda no simulador do editor)."
                : "Comunicação: " + driver.DisplayName + " — " + PlcProfileCatalog.DescribeCapabilities(driver.Capabilities) + ".";
        details.Text = communication + "\n" + profile.Notes;
        // Modelo planejado pode ser estudado aqui, mas não vira alvo: não há como verificar nem conectar.
        ok.Enabled = profile.SupportLevel != PlcSupportLevel.Planned;
        Selected = ok.Enabled ? profile : Selected;
    }
}
