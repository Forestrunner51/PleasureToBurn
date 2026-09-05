using Godot;

namespace PleasureToBurn;

/// <summary>
/// Root script for a playable location (a building). Tracks the contraband objective:
/// counts every Flammable marked IsContraband under it and reports progress as they char,
/// plus collateral (anything else that charred) for precision contracts.
/// ContractManager subscribes to ProgressChanged; the EventBus emission is for the HUD.
/// </summary>
public partial class Location : Node3D
{
    public event Action<Location>? ProgressChanged;

    public int ContrabandTotal { get; private set; }
    public int ContrabandBurned { get; private set; }
    /// <summary>Non-contraband objects charred inside this location.</summary>
    public int CollateralBurned { get; private set; }
    public bool AllContrabandBurned => ContrabandTotal > 0 && ContrabandBurned >= ContrabandTotal;

    public override void _Ready()
    {
        // Children register with FireSystem in their own _Ready, which runs before ours, so counting here is safe.
        ContrabandTotal = CountContraband(this);
        if (FireSystem.Instance is { } fire)
            fire.ObjectCharred += OnObjectCharred;
        ReportProgress();
    }

    public override void _ExitTree()
    {
        if (FireSystem.Instance is { } fire)
            fire.ObjectCharred -= OnObjectCharred;
    }

    private void OnObjectCharred(Flammable flammable)
    {
        if (!IsAncestorOf(flammable))
            return;
        if (flammable.IsContraband)
            ContrabandBurned++;
        else
            CollateralBurned++;
        ReportProgress();
    }

    private void ReportProgress()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.ContrabandProgress, ContrabandBurned, ContrabandTotal);
        ProgressChanged?.Invoke(this);
    }

    private static int CountContraband(Node root)
    {
        var count = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is Flammable { IsContraband: true })
                count++;
            count += CountContraband(child);
        }
        return count;
    }
}
