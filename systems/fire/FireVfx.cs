using Godot;

namespace PleasureToBurn;

/// <summary>
/// Hangs placeholder effects off FireSystem signals: an ignition burst when something catches,
/// a looping flame emitter on each burning object (scaled by intensity), removed when charred.
/// Put one in every location scene. Pure presentation: the simulation never depends on it.
///
/// TUNE BY EYE: everything in ignition_burst.tscn and burning_flames.tscn.
/// </summary>
public partial class FireVfx : Node3D
{
    [Export] public PackedScene IgnitionBurst { get; set; } = GD.Load<PackedScene>("res://scenes/vfx/ignition_burst.tscn");
    [Export] public PackedScene BurningFlames { get; set; } = GD.Load<PackedScene>("res://scenes/vfx/burning_flames.tscn");

    public int ActiveFlames => _flames.Count;
    public bool HasFlamesFor(Flammable flammable) => _flames.ContainsKey(flammable);

    private readonly Dictionary<Flammable, Node3D> _flames = new();

    public override void _Ready()
    {
        if (FireSystem.Instance is not { } fire)
            return;
        fire.ObjectIgnited += OnIgnited;
        fire.ObjectCharred += OnCharred;
    }

    public override void _ExitTree()
    {
        if (FireSystem.Instance is not { } fire)
            return;
        fire.ObjectIgnited -= OnIgnited;
        fire.ObjectCharred -= OnCharred;
    }

    public override void _Process(double delta)
    {
        foreach (var (flammable, flames) in _flames)
            if (IsInstanceValid(flames))
                flames.Scale = Vector3.One * Mathf.Lerp(0.3f, 1f, flammable.Intensity);
    }

    private void OnIgnited(Flammable flammable)
    {
        if (!IsInstanceValid(flammable))
            return;

        var burst = IgnitionBurst.Instantiate<Node3D>();
        AddChild(burst);
        burst.GlobalPosition = flammable.GlobalPosition;

        var flames = BurningFlames.Instantiate<Node3D>();
        flammable.Body.AddChild(flames);
        _flames[flammable] = flames;
        // A burning object can be freed mid-fire (a building being respawned); its emitter goes with it.
        flammable.Body.TreeExiting += () => _flames.Remove(flammable);
    }

    private void OnCharred(Flammable flammable)
    {
        if (!_flames.Remove(flammable, out var flames))
            return;
        if (IsInstanceValid(flames))
            flames.QueueFree();
    }
}
