using Godot;

namespace PleasureToBurn;

/// <summary>
/// Root script for a playable location. For now it only tracks the contraband objective:
/// counts every Flammable marked IsContraband under it and reports progress as they char.
/// The contract/data-driven layer will build on this later; keep it thin.
/// </summary>
public partial class Location : Node3D
{
    public int ContrabandTotal { get; private set; }
    public int ContrabandBurned { get; private set; }
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
        if (!flammable.IsContraband || !IsAncestorOf(flammable))
            return;
        ContrabandBurned++;
        ReportProgress();
    }

    private void ReportProgress() =>
        EventBus.Instance.EmitSignal(EventBus.SignalName.ContrabandProgress, ContrabandBurned, ContrabandTotal);

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
