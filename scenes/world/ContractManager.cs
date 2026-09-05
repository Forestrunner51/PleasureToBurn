using Godot;

namespace PleasureToBurn;

public enum ContractState
{
    /// <summary>No job. Take a report at dispatch.</summary>
    Idle,
    /// <summary>Drive out and burn everything on the list.</summary>
    Accepted,
    /// <summary>All contraband destroyed. Return to the depot for payment.</summary>
    Cleared,
}

/// <summary>
/// The job loop: a report comes in, the player drives to the address, burns the contraband,
/// and returns to the depot to be paid. Owns the objective text, the beacon, and the money.
///
/// Scene setup (by hand): one per world. Sites are discovered from the "sites" group (any Site node
/// under the same parent). Set ReportLines to dispatch flavour text, Beacon to a tall marker node,
/// DepotReturnPoint to where the beacon should stand when the player must come back.
///
/// Godot 4.x note: single Node exports resolve from .tscn, but a C# Node[] export did not in 4.8 dev 3,
/// hence the group lookup.
/// </summary>
public partial class ContractManager : Node
{
    [Export] public Node3D? Beacon { get; set; }
    [Export] public Node3D? DepotReturnPoint { get; set; }

    /// <summary>Dispatch flavour text. Keep it dry. {address} is substituted.</summary>
    [Export(PropertyHint.MultilineText)] public string[] ReportLines { get; set; } =
    {
        "Caller at {address} reports the neighbours have been 'quiet in a literate way'.",
        "Landlord at {address} says the tenant owns a shelf. Requests full service.",
        "Anonymous tip: {address}. Lamp on late, pages turning. Probably nothing. Burn it anyway.",
        "Routine inspection due at {address}. Bring the big nozzle, the cat has been seen with a bookmark.",
        "Complaint from {address}: 'unauthorised alphabet in the living room'.",
    };

    [Export] public int PayPerContraband { get; set; } = 20;
    /// <summary>On precision jobs, every non-contraband object charred costs this much.</summary>
    [Export] public int PrecisionPenaltyPerObject { get; set; } = 8;
    /// <summary>Chance a report is a precision job (keep the furniture) instead of a torch-it-all job.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float PrecisionChance { get; set; } = 0.4f;

    public IReadOnlyList<Site> Sites { get; private set; } = Array.Empty<Site>();
    public ContractState State { get; private set; } = ContractState.Idle;
    public int Money { get; private set; }
    public Site? TargetSite { get; private set; }
    public Location? TargetLocation { get; private set; }
    public bool IsPrecisionJob { get; private set; }

    public override void _Ready()
    {
        // Sites spawn their buildings in their own _Ready (children before parent), so they exist here.
        var root = GetParent();
        Sites = GetTree().GetNodesInGroup(Site.Group).OfType<Site>().Where(s => root.IsAncestorOf(s)).ToList();
        if (Sites.Count == 0)
            GD.PushWarning("ContractManager found no Site nodes in the 'sites' group.");
        EventBus.Instance.EmitSignal(EventBus.SignalName.MoneyChanged, Money);
        SetObjective("Take a report at dispatch.");
        MoveBeacon(DepotReturnPoint);
    }

    /// <summary>Called by Dispatch when the player asks for work.</summary>
    public bool TakeReport()
    {
        if (State != ContractState.Idle || Sites.Count == 0)
            return false;

        TargetSite = Sites[GD.RandRange(0, Sites.Count - 1)];
        TargetLocation = TargetSite.Respawn();
        TargetLocation.ProgressChanged += OnProgress;
        IsPrecisionJob = GD.Randf() < PrecisionChance;
        State = ContractState.Accepted;

        var line = ReportLines.Length > 0 ? ReportLines[GD.RandRange(0, ReportLines.Length - 1)] : "Report at {address}.";
        var kind = IsPrecisionJob ? "PRECISION job: contraband only, the furniture is insured." : "Standard job: burn what you like.";
        Radio($"{line.Replace("{address}", TargetSite.Address)}\n{kind}");
        MoveBeacon(TargetSite);
        UpdateObjective();
        return true;
    }

    /// <summary>Called by Dispatch when the player returns after clearing the job.</summary>
    public bool CollectPayment()
    {
        if (State != ContractState.Cleared || TargetLocation is null)
            return false;

        var earned = TargetLocation.ContrabandBurned * PayPerContraband;
        var penalty = IsPrecisionJob ? TargetLocation.CollateralBurned * PrecisionPenaltyPerObject : 0;
        var pay = Math.Max(0, earned - penalty);
        Money += pay;
        EventBus.Instance.EmitSignal(EventBus.SignalName.MoneyChanged, Money);

        var note = penalty > 0 ? $" ({TargetLocation.CollateralBurned} items of insured furniture deducted: -${penalty})" : "";
        Radio($"Paid ${pay}{note}. Good work. Take a break, or don't.");

        TargetLocation.ProgressChanged -= OnProgress;
        TargetLocation = null;
        TargetSite = null;
        State = ContractState.Idle;
        SetObjective("Take the next report at dispatch.");
        MoveBeacon(DepotReturnPoint);
        return true;
    }

    private void OnProgress(Location location)
    {
        if (State != ContractState.Accepted)
            return;
        if (location.AllContrabandBurned)
        {
            State = ContractState.Cleared;
            Radio("All listed contraband destroyed. Return to the depot for payment.");
            MoveBeacon(DepotReturnPoint);
        }
        UpdateObjective();
    }

    private void UpdateObjective()
    {
        if (TargetLocation is null || TargetSite is null)
            return;
        var progress = $"{TargetLocation.ContrabandBurned}/{TargetLocation.ContrabandTotal}";
        var collateral = IsPrecisionJob ? $"   Collateral: {TargetLocation.CollateralBurned}" : "";
        SetObjective(State == ContractState.Cleared
            ? "Return to the depot for payment."
            : $"{TargetSite.Address}: burn contraband {progress}{collateral}");
    }

    private void MoveBeacon(Node3D? target)
    {
        if (Beacon is null)
            return;
        Beacon.Visible = target is not null;
        if (target is not null)
            Beacon.GlobalPosition = target.GlobalPosition;
    }

    private static void SetObjective(string text) =>
        EventBus.Instance.EmitSignal(EventBus.SignalName.ObjectiveChanged, text);

    private static void Radio(string text) =>
        EventBus.Instance.EmitSignal(EventBus.SignalName.RadioMessage, text);
}
