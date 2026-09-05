using Godot;

namespace PleasureToBurn;

/// <summary>
/// Autoload that simulates fire spread between registered Flammables.
///
/// Design for scale:
///   - Runs on a fixed tick (default 10 Hz), not every frame.
///   - Only burning objects do work; unburnt objects cost nothing until something near them ignites.
///   - Neighbours are looked up through a spatial hash and cached per burning object at ignition,
///     with the distance falloff precomputed, so the hot loop is a few multiplies per pair.
///   - Charred objects drop out of the simulation entirely.
///
/// Godot 4.x note: the tick is driven from _PhysicsProcess so it stays deterministic under a stable
/// physics rate; do not move it to _Process.
/// </summary>
public partial class FireSystem : Node
{
    public static FireSystem? Instance { get; private set; }

    [Signal] public delegate void ObjectIgnitedEventHandler(Flammable flammable);
    [Signal] public delegate void ObjectCharredEventHandler(Flammable flammable);
    [Signal] public delegate void BurningCountChangedEventHandler(int burning);

    /// <summary>Simulation ticks per second. Lower is cheaper; 10 is plenty for spread that feels continuous.</summary>
    [Export(PropertyHint.Range, "1,60,1")] public float TickRate { get; set; } = 10f;

    /// <summary>Spatial hash cell size. Should be at least the largest SpreadRadius in use.</summary>
    [Export(PropertyHint.Range, "0.5,10,0.5,suffix:m")] public float CellSize { get; set; } = 2f;

    /// <summary>
    /// Fire rises: a neighbour directly above a source at the edge of its radius gets (1 + UpwardBias) times
    /// the heat of one beside it. TUNE BY EYE with a bookshelf: it should catch bottom to top.
    /// </summary>
    [Export(PropertyHint.Range, "0,3,0.1")] public float UpwardBias { get; set; } = 1.2f;

    /// <summary>How much less heat reaches neighbours below the source (0 = no penalty, 1 = nothing spreads down).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float DownwardPenalty { get; set; } = 0.6f;

    public int BurningCount => _burning.Count;
    public int CharredCount { get; private set; }
    public IReadOnlyCollection<Flammable> All => _all;

    private readonly HashSet<Flammable> _all = new();
    private readonly List<Flammable> _burning = new();
    private readonly HashSet<Flammable> _warm = new();
    private readonly Dictionary<Vector3I, List<Flammable>> _cells = new();
    private readonly List<Flammable> _scratch = new();
    private float _accumulator;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_burning.Count == 0 && _warm.Count == 0)
            return;

        var step = 1f / TickRate;
        _accumulator += (float)delta;
        while (_accumulator >= step)
        {
            _accumulator -= step;
            Tick(step);
        }
    }

    // --- Registration (called by Flammable) --------------------------------------------------

    public void Register(Flammable flammable)
    {
        if (!_all.Add(flammable))
            return;
        flammable.IsRegistered = true;
        CellFor(flammable.GlobalPosition, create: true)!.Add(flammable);
    }

    public void Unregister(Flammable flammable)
    {
        if (!_all.Remove(flammable))
            return;
        flammable.IsRegistered = false;
        CellFor(flammable.GlobalPosition, create: false)?.Remove(flammable);
        if (_burning.Remove(flammable))
            EmitSignal(SignalName.BurningCountChanged, _burning.Count);
        _warm.Remove(flammable);
    }

    internal void MarkWarm(Flammable flammable) => _warm.Add(flammable);

    internal void OnIgnited(Flammable flammable)
    {
        _warm.Remove(flammable);
        if (_burning.Contains(flammable))
            return;
        CacheNeighbours(flammable);
        _burning.Add(flammable);
        EmitSignal(SignalName.ObjectIgnited, flammable);
        EmitSignal(SignalName.BurningCountChanged, _burning.Count);
    }

    /// <summary>Forget everything. Call when unloading a location.</summary>
    public void Reset()
    {
        foreach (var flammable in _all)
            flammable.IsRegistered = false;
        _all.Clear();
        _burning.Clear();
        _warm.Clear();
        _cells.Clear();
        CharredCount = 0;
        _accumulator = 0f;
        EmitSignal(SignalName.BurningCountChanged, 0);
    }

    // --- Simulation ---------------------------------------------------------------------------

    private void Tick(float dt)
    {
        // Burning objects heat their neighbours and consume fuel. Iterate backwards so we can remove.
        for (var i = _burning.Count - 1; i >= 0; i--)
        {
            var source = _burning[i];
            var output = source.Profile.HeatOutput * source.Intensity * dt;
            if (output > 0f)
            {
                foreach (var (target, weight) in source.Neighbours)
                    if (target.IsRegistered && target.State == BurnState.Unburnt)
                        target.AddHeat(output * weight);
            }

            if (!source.TickBurn(dt))
                continue;

            _burning.RemoveAt(i);
            CharredCount++;
            EmitSignal(SignalName.ObjectCharred, source);
            EmitSignal(SignalName.BurningCountChanged, _burning.Count);
        }

        // Warm-but-not-burning objects cool back down.
        if (_warm.Count == 0)
            return;
        _scratch.Clear();
        foreach (var warm in _warm)
            if (warm.State != BurnState.Unburnt || warm.TickCool(dt))
                _scratch.Add(warm);
        foreach (var done in _scratch)
            _warm.Remove(done);
    }

    private void CacheNeighbours(Flammable source)
    {
        source.Neighbours.Clear();
        var radius = source.Profile.SpreadRadius;
        var radiusSq = radius * radius;
        var origin = source.GlobalPosition;
        var reach = Mathf.CeilToInt(radius / CellSize);
        var centre = CellCoord(origin);

        for (var x = -reach; x <= reach; x++)
        for (var y = -reach; y <= reach; y++)
        for (var z = -reach; z <= reach; z++)
        {
            if (!_cells.TryGetValue(centre + new Vector3I(x, y, z), out var cell))
                continue;
            foreach (var other in cell)
            {
                if (other == source || other.State != BurnState.Unburnt)
                    continue;
                var distSq = origin.DistanceSquaredTo(other.GlobalPosition);
                if (distSq > radiusSq)
                    continue;
                var falloff = 1f - Mathf.Sqrt(distSq) / radius; // linear, 1 at centre, 0 at edge
                var weight = falloff * VerticalFactor(other.GlobalPosition.Y - origin.Y, radius);
                if (weight > 0f)
                    source.Neighbours.Add((other, weight));
            }
        }
    }

    /// <summary>Multiplier for heat travelling up (dy > 0) or down (dy < 0), normalised by the spread radius.</summary>
    public float VerticalFactor(float dy, float radius)
    {
        var t = Mathf.Clamp(dy / radius, -1f, 1f);
        return t >= 0f ? 1f + UpwardBias * t : 1f + DownwardPenalty * t;
    }

    private Vector3I CellCoord(Vector3 position) => new(
        Mathf.FloorToInt(position.X / CellSize),
        Mathf.FloorToInt(position.Y / CellSize),
        Mathf.FloorToInt(position.Z / CellSize));

    private List<Flammable>? CellFor(Vector3 position, bool create)
    {
        var key = CellCoord(position);
        if (_cells.TryGetValue(key, out var cell))
            return cell;
        if (!create)
            return null;
        cell = new List<Flammable>();
        _cells[key] = cell;
        return cell;
    }
}
