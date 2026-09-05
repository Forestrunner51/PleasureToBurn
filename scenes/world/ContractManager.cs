using Godot;

namespace PleasureToBurn;

public enum ContractState
{
    /// <summary>No job. Check the board at dispatch.</summary>
    Idle,
    /// <summary>Drive out and burn everything on the list.</summary>
    Accepted,
    /// <summary>All contraband destroyed. Return to the depot to settle the invoice.</summary>
    Cleared,
}

/// <summary>
/// The day loop. A shift is a clock. While it runs the board offers jobs; each job is accepted, driven to,
/// burned, and settled on an itemised invoice with a star rating. When the clock runs out the player signs
/// off, buys upgrades, and the next day starts. Money, day and reputation live in Career (persistent).
///
/// Scene setup (by hand): one per world. Sites are discovered from the "sites" group. Set Beacon and
/// DepotReturnPoint. UI panels (JobBoard, InvoicePanel, DayEndPanel) are found by group.
///
/// TUNE BY EYE: ShiftLengthSeconds against how long a job takes; pay numbers against upgrade costs.
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
        "{address} requested a smoke detector test. We are the test.",
        "Retired teacher at {address}. Self-reported. Says she 'just wants it over with'.",
    };

    [Export(PropertyHint.Range, "60,3600,10,suffix:s")] public float ShiftLengthSeconds { get; set; } = 480f;
    [Export(PropertyHint.Range, "1,6,1")] public int OffersPerVisit { get; set; } = 3;
    [Export] public int PayPerContraband { get; set; } = 20;
    /// <summary>Precision jobs pay this much more per item to compensate for the risk.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float PrecisionRateBonus { get; set; } = 0.3f;
    /// <summary>On precision jobs, every non-contraband object charred costs this much.</summary>
    [Export] public int PrecisionPenaltyPerObject { get; set; } = 8;
    [Export(PropertyHint.Range, "0,1,0.05")] public float DeadlineBonusFraction { get; set; } = 0.25f;
    /// <summary>Assumed average truck speed when estimating a fair deadline.</summary>
    [Export] public float AssumedSpeedMetresPerSecond { get; set; } = 9f;

    public ContractState State { get; private set; } = ContractState.Idle;
    public IReadOnlyList<Site> Sites { get; private set; } = Array.Empty<Site>();
    public IReadOnlyList<JobOffer> CurrentOffers => _offers;
    public JobOffer? ActiveJob { get; private set; }
    public Location? TargetLocation { get; private set; }
    public float ShiftTimeLeft { get; private set; }
    public bool ShiftOver => ShiftTimeLeft <= 0f;
    public int JobsCompletedToday { get; private set; }
    public int EarnedToday { get; private set; }

    private readonly List<JobOffer> _offers = new();
    private float _acceptedAtShiftTime;
    private bool _shiftOverAnnounced;

    public override void _Ready()
    {
        var root = GetParent();
        Sites = GetTree().GetNodesInGroup(Site.Group).OfType<Site>().Where(s => root.IsAncestorOf(s)).ToList();
        if (Sites.Count == 0)
            GD.PushWarning("ContractManager found no Site nodes in the 'sites' group.");

        ShiftTimeLeft = ShiftLengthSeconds;
        SetObjective("Check the job board at dispatch.");
        MoveBeacon(DepotReturnPoint);
        Radio($"Day {Career.Instance.Day}. Shift's started. Dispatch has work.");
    }

    public override void _Process(double delta)
    {
        if (!ShiftOver)
        {
            ShiftTimeLeft = Mathf.Max(0f, ShiftTimeLeft - (float)delta);
            if (ShiftOver && !_shiftOverAnnounced)
            {
                _shiftOverAnnounced = true;
                Radio(State == ContractState.Idle
                    ? "Shift's over. Sign off at dispatch."
                    : "Shift's over. Finish the job, then sign off at dispatch.");
                if (State == ContractState.Idle)
                    SetObjective("Shift over: sign off at dispatch.");
            }
        }
        EventBus.Instance.EmitSignal(EventBus.SignalName.ShiftTimeChanged, ShiftTimeLeft, Career.Instance.Day);
    }

    // --- Job board -------------------------------------------------------------------------------

    /// <summary>
    /// Fresh offers for this visit: distinct sites, mixed job types, distance-based deadlines.
    /// Returns a snapshot; CurrentOffers is the live list and empties when a job is accepted.
    /// </summary>
    public IReadOnlyList<JobOffer> GenerateOffers()
    {
        _offers.Clear();
        var depot = DepotReturnPoint?.GlobalPosition ?? Vector3.Zero;
        var pool = Sites.OrderBy(_ => GD.Randf()).Take(OffersPerVisit);
        foreach (var site in pool)
        {
            var precision = GD.Randf() < 0.45f;
            var count = site.Current?.ContrabandTotal ?? 0;
            var distance = depot.DistanceTo(site.GlobalPosition);
            _offers.Add(new JobOffer
            {
                Site = site,
                IsPrecision = precision,
                ContrabandCount = count,
                EstimatedPay = Mathf.RoundToInt(count * RateFor(precision) * Career.Instance.PayMultiplier),
                // Out and back at the assumed speed, plus a fixed allowance for finding and burning.
                BonusSeconds = Mathf.Round(distance * 2f / AssumedSpeedMetresPerSecond + 150f),
                DistanceMetres = distance,
                ReportLine = (ReportLines.Length > 0 ? ReportLines[GD.RandRange(0, ReportLines.Length - 1)] : "Report at {address}.")
                    .Replace("{address}", site.Address),
            });
        }
        return _offers.ToList();
    }

    public bool Accept(int offerIndex)
    {
        if (State != ContractState.Idle || ShiftOver || offerIndex < 0 || offerIndex >= _offers.Count)
            return false;

        ActiveJob = _offers[offerIndex];
        TargetLocation = ActiveJob.Site.Respawn();
        TargetLocation.ProgressChanged += OnProgress;
        _acceptedAtShiftTime = ShiftTimeLeft;
        State = ContractState.Accepted;
        _offers.Clear();

        var kind = ActiveJob.IsPrecision ? "PRECISION: contraband only, the furniture is insured." : "Standard: burn what you like.";
        Radio($"{ActiveJob.ReportLine}\n{kind} Bonus if you're back in {FormatTime(ActiveJob.BonusSeconds)}.");
        MoveBeacon(ActiveJob.Site);
        UpdateObjective();
        return true;
    }

    // --- Settlement ------------------------------------------------------------------------------

    /// <summary>What the invoice would say right now. Valid while Cleared.</summary>
    public Invoice? PreviewInvoice()
    {
        if (State != ContractState.Cleared || ActiveJob is null || TargetLocation is null)
            return null;

        var taken = _acceptedAtShiftTime - ShiftTimeLeft;
        var rate = Mathf.RoundToInt(RateFor(ActiveJob.IsPrecision));
        var basePay = TargetLocation.ContrabandBurned * rate;
        var penalty = ActiveJob.IsPrecision ? TargetLocation.CollateralBurned * PrecisionPenaltyPerObject : 0;
        var madeDeadline = taken <= ActiveJob.BonusSeconds;
        var bonus = madeDeadline ? Mathf.RoundToInt(basePay * DeadlineBonusFraction) : 0;
        var multiplier = Career.Instance.PayMultiplier;
        var total = Math.Max(0, Mathf.RoundToInt((basePay + bonus - penalty) * multiplier));

        var clean = !ActiveJob.IsPrecision || TargetLocation.CollateralBurned == 0;
        var stars = clean && madeDeadline ? 3 : clean || madeDeadline ? 2 : 1;
        var repDelta = stars switch { 3 => 2, 2 => 1, _ => ActiveJob.IsPrecision && !clean ? -1 : 0 };

        return new Invoice
        {
            Address = ActiveJob.Site.Address,
            IsPrecision = ActiveJob.IsPrecision,
            ContrabandBurned = TargetLocation.ContrabandBurned,
            ContrabandTotal = TargetLocation.ContrabandTotal,
            RatePerItem = rate,
            BasePay = basePay,
            CollateralCount = TargetLocation.CollateralBurned,
            CollateralPenalty = penalty,
            MadeDeadline = madeDeadline,
            DeadlineBonus = bonus,
            ReputationMultiplier = multiplier,
            Total = total,
            Stars = stars,
            ReputationDelta = repDelta,
            SecondsTaken = taken,
        };
    }

    /// <summary>Pays out, applies reputation, returns to Idle. Returns the invoice that was settled.</summary>
    public Invoice? Settle()
    {
        var invoice = PreviewInvoice();
        if (invoice is null || TargetLocation is null)
            return null;

        Career.Instance.AddMoney(invoice.Total);
        Career.Instance.AddReputation(invoice.ReputationDelta);
        JobsCompletedToday++;
        EarnedToday += invoice.Total;

        TargetLocation.ProgressChanged -= OnProgress;
        TargetLocation = null;
        ActiveJob = null;
        State = ContractState.Idle;
        SetObjective(ShiftOver ? "Shift over: sign off at dispatch." : "Check the job board for the next report.");
        MoveBeacon(DepotReturnPoint);
        Career.Instance.Save();
        return invoice;
    }

    /// <summary>Close the day: advance the calendar, reset the clock, save.</summary>
    public bool EndDay()
    {
        if (State != ContractState.Idle)
            return false;
        Career.Instance.AdvanceDay();
        ShiftTimeLeft = ShiftLengthSeconds;
        _shiftOverAnnounced = false;
        JobsCompletedToday = 0;
        EarnedToday = 0;
        _offers.Clear();
        Radio($"Day {Career.Instance.Day}. Coffee's on. Dispatch has work.");
        SetObjective("Check the job board at dispatch.");
        return true;
    }

    // --- Internals -------------------------------------------------------------------------------

    private float RateFor(bool precision) => PayPerContraband * (precision ? 1f + PrecisionRateBonus : 1f);

    private void OnProgress(Location location)
    {
        if (State != ContractState.Accepted)
            return;
        if (location.AllContrabandBurned)
        {
            State = ContractState.Cleared;
            Radio("All listed contraband destroyed. Return to the depot to settle up.");
            MoveBeacon(DepotReturnPoint);
        }
        UpdateObjective();
    }

    private void UpdateObjective()
    {
        if (TargetLocation is null || ActiveJob is null)
            return;
        if (State == ContractState.Cleared)
        {
            SetObjective("Return to the depot to settle the invoice.");
            return;
        }
        var progress = $"{TargetLocation.ContrabandBurned}/{TargetLocation.ContrabandTotal}";
        var collateral = ActiveJob.IsPrecision ? $"   Collateral: {TargetLocation.CollateralBurned}" : "";
        SetObjective($"{ActiveJob.Site.Address}: burn contraband {progress}{collateral}");
    }

    private void MoveBeacon(Node3D? target)
    {
        if (Beacon is null)
            return;
        Beacon.Visible = target is not null;
        if (target is not null)
            Beacon.GlobalPosition = target.GlobalPosition + Vector3.Up * 20f;
    }

    public static string FormatTime(float seconds)
    {
        var total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    private static void SetObjective(string text) =>
        EventBus.Instance.EmitSignal(EventBus.SignalName.ObjectiveChanged, text);

    private static void Radio(string text) =>
        EventBus.Instance.EmitSignal(EventBus.SignalName.RadioMessage, text);
}
