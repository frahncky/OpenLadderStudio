using ModernPC12;
using OpenLadderStudio.Application;

namespace OpenLadderStudio.WinForms;

/// <summary>
/// Sinóptico da planta ligada a uma sessão de simulação. Usa o mesmo ProcessSynoptic do
/// simulador legado; a cena é descrita pela própria planta a cada repintura.
/// </summary>
public sealed class PlantSynoptic : UserControl
{
    private readonly ProcessSynoptic synoptic = new() { Dock = DockStyle.Fill };

    public PlantSynoptic()
    {
        Controls.Add(synoptic);
    }

    public bool HasPlant { get; private set; }

    public void Bind(LadderSimulationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        HasPlant = session.Plant != null;
        if (HasPlant) synoptic.Bind(session.Plant, session.Image);
        else synoptic.Bind(null, null);
        synoptic.BackColor = SimulatorTheme.Shell;
    }

    /// <summary>Repinta a cena com o estado atual da planta.</summary>
    public void RefreshScene()
    {
        synoptic.BackColor = SimulatorTheme.Shell;
        synoptic.Invalidate();
    }
}
