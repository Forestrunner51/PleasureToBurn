using Godot;

namespace PleasureToBurn;

public enum BurnState
{
    Unburnt,
    Burning,
    Charred,
}

/// <summary>
/// Component that makes its parent burnable. Add it as a direct child of the prop's
/// StaticBody3D/RigidBody3D and name it "Flammable" so the flamethrower can find it.
///
/// Scene setup (by hand):
///   Prop (StaticBody3D, physics layer 3 "flammable")
///   ├── Mesh (MeshInstance3D)         ← placeholder visual; colour is driven by burn state
///   ├── CollisionShape3D
///   └── Flammable (this script)       ← set Profile; tick IsContraband for books
///
/// All simulation happens in FireSystem; this class only holds state and exposes AddHeat/Ignite.
/// </summary>
public partial class Flammable : Node
{
    [Signal] public delegate void IgnitedEventHandler(Flammable flammable);
    [Signal] public delegate void CharredEventHandler(Flammable flammable);

    [Export] public BurnProfile Profile { get; set; } = new();

    /// <summary>Burning this counts toward the location's contraband objective.</summary>
    [Export] public bool IsContraband { get; set; }

    /// <summary>
    /// Root to search for meshes to tint. Defaults to the parent body, so an imported .glb instanced under the
    /// body (with its own nested MeshInstance3Ds) is picked up automatically.
    /// </summary>
    [Export] public Node3D? Visual { get; set; }

    public BurnState State { get; private set; } = BurnState.Unburnt;

    /// <summary>Accumulated heat while unburnt. Ignites at Profile.IgnitionTemperature.</summary>
    public float Heat { get; private set; }

    /// <summary>Seconds of burning left. Only meaningful while Burning.</summary>
    public float FuelRemaining { get; private set; }

    /// <summary>0..1 how fiercely this is burning: quick ramp up, then fades as fuel runs out.</summary>
    public float Intensity { get; private set; }

    /// <summary>0..1 progress toward ignition while unburnt; 1 while burning; 0 once charred. Drives the reticle.</summary>
    public float HeatFraction => State switch
    {
        BurnState.Unburnt => Mathf.Clamp(Heat / Profile.IgnitionTemperature, 0f, 1f),
        BurnState.Burning => 1f,
        _ => 0f,
    };

    public Node3D Body { get; private set; } = null!;
    public Vector3 GlobalPosition => Body.GlobalPosition;

    /// <summary>Filled in by FireSystem when this ignites. Neighbour + precomputed distance falloff weight.</summary>
    internal List<(Flammable Target, float Weight)> Neighbours { get; } = new();

    /// <summary>
    /// True between Register and Unregister. Cached neighbour lists may still hold objects that have since
    /// left the tree (e.g. a building being respawned); this plain C# flag lets the hot loop skip them
    /// without touching the freed native object.
    /// </summary>
    internal bool IsRegistered { get; set; }

    /// <summary>One entry per mesh surface: the unique material we tint, and the colour it started with.</summary>
    private readonly List<(StandardMaterial3D Material, Color BaseColor)> _surfaces = new();
    private float _timeBurning;
    private bool _heatedSinceLastTick;

    private static readonly Color CharredColor = new(0.07f, 0.06f, 0.05f);
    private static readonly Color EmberColor = new(1f, 0.45f, 0.1f);

    public override void _Ready()
    {
        Body = GetParent<Node3D>();
        Visual ??= Body;
        SetupMaterials();
        FireSystem.Instance?.Register(this);
    }

    public override void _ExitTree() => FireSystem.Instance?.Unregister(this);

    /// <summary>Adds heat from any source (flamethrower, a burning neighbour). Ignites at the threshold.</summary>
    public void AddHeat(float amount)
    {
        if (State != BurnState.Unburnt || amount <= 0f)
            return;
        Heat += amount;
        _heatedSinceLastTick = true;
        if (Heat >= Profile.IgnitionTemperature)
            Ignite();
        else
            FireSystem.Instance?.MarkWarm(this);
    }

    public void Ignite()
    {
        if (State != BurnState.Unburnt)
            return;
        State = BurnState.Burning;
        FuelRemaining = Profile.Fuel;
        Heat = Profile.IgnitionTemperature;
        _timeBurning = 0f;
        UpdateVisual();
        EmitSignal(SignalName.Ignited, this);
        FireSystem.Instance?.OnIgnited(this);
    }

    /// <summary>Called by FireSystem on its tick while burning. Returns true when the object just became charred.</summary>
    internal bool TickBurn(float dt)
    {
        _timeBurning += dt;
        FuelRemaining -= dt;
        var rampUp = Mathf.Min(1f, _timeBurning / 1.5f);
        var fade = Mathf.Clamp(FuelRemaining / Mathf.Max(0.01f, Profile.Fuel), 0f, 1f);
        Intensity = rampUp * Mathf.Lerp(0.35f, 1f, fade);
        UpdateVisual();

        if (FuelRemaining > 0f)
            return false;

        State = BurnState.Charred;
        Intensity = 0f;
        Neighbours.Clear();
        UpdateVisual();
        EmitSignal(SignalName.Charred, this);
        return true;
    }

    /// <summary>
    /// Called by FireSystem while warm but not burning. Returns true when fully cooled.
    /// Cooling only happens once nothing has heated this object since the previous tick, so heat
    /// accumulates under a steady flame and drains only after the source stops.
    /// </summary>
    internal bool TickCool(float dt)
    {
        if (_heatedSinceLastTick)
        {
            _heatedSinceLastTick = false;
            return false;
        }
        Heat = Mathf.Max(0f, Heat - Profile.CoolingRate * dt);
        UpdateVisual();
        return Heat <= 0f;
    }

    /// <summary>Placeholder feedback: tint toward ember colour while heating, glow while burning, black when charred.</summary>
    private void UpdateVisual()
    {
        if (_surfaces.Count == 0)
            return;
        float toChar, emission;
        Color tintTarget;
        switch (State)
        {
            case BurnState.Unburnt:
                var warmth = Mathf.Clamp(Heat / Profile.IgnitionTemperature, 0f, 1f);
                tintTarget = EmberColor; toChar = warmth * 0.5f; emission = 0f;
                break;
            case BurnState.Burning:
                tintTarget = CharredColor;
                toChar = 1f - Mathf.Clamp(FuelRemaining / Profile.Fuel, 0f, 1f);
                emission = 3f * Intensity;
                break;
            default:
                tintTarget = CharredColor; toChar = 1f; emission = 0f;
                break;
        }
        foreach (var (material, baseColor) in _surfaces)
        {
            material.AlbedoColor = baseColor.Lerp(tintTarget, toChar);
            material.EmissionEnergyMultiplier = emission;
        }
    }

    /// <summary>
    /// Gives every surface of every mesh under Visual its own material so it can be tinted independently.
    /// Textured materials (Kenney colormaps) tint correctly because albedo colour multiplies the texture.
    /// NOTE: fine for a prototype; hundreds of unique materials will want a shader with per-instance data.
    /// </summary>
    private void SetupMaterials()
    {
        if (Visual is null)
            return;
        foreach (var mesh in MeshesUnder(Visual))
        {
            var surfaceCount = mesh.Mesh?.GetSurfaceCount() ?? 0;
            for (var i = 0; i < surfaceCount; i++)
            {
                var source = mesh.GetActiveMaterial(i) as StandardMaterial3D;
                var material = source?.Duplicate() as StandardMaterial3D ?? new StandardMaterial3D();
                material.EmissionEnabled = true;
                material.Emission = EmberColor;
                material.EmissionEnergyMultiplier = 0f;
                mesh.SetSurfaceOverrideMaterial(i, material);
                _surfaces.Add((material, material.AlbedoColor));
            }
        }
    }

    /// <summary>All MeshInstance3Ds under root, not descending into other flammable bodies (e.g. books on a shelf).</summary>
    private static IEnumerable<MeshInstance3D> MeshesUnder(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh)
                yield return mesh;
            if (child.GetNodeOrNull<Flammable>("Flammable") is not null)
                continue;
            foreach (var nested in MeshesUnder(child))
                yield return nested;
        }
    }
}
