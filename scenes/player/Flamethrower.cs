using Godot;

namespace PleasureToBurn;

/// <summary>
/// First-person flamethrower. While the fire action is held it samples a cone of rays from the
/// camera and pours heat into whatever Flammable they hit. Fuel drains while firing.
///
/// Scene setup (by hand):
///   Camera3D
///   └── Flamethrower (Node3D, this script)   ← set Stats
///       ├── Nozzle (Marker3D)                 ← where the flame jet starts, roughly bottom-right of view
///       └── FlameJet (CPUParticles3D)         ← placeholder jet, toggled by IsFiring
///
/// Physics: rays use layers 2 (world) and 3 (flammable). A hit body must have a direct child named
/// "Flammable" to receive heat; anything else just blocks the flame.
/// </summary>
public partial class Flamethrower : Node3D
{
    private const uint RayMask = (1 << 1) | (1 << 2); // layers 2 and 3

    [Signal] public delegate void FiringChangedEventHandler(bool firing);

    [Export] public FlamethrowerStats Stats { get; set; } = new();

    public float Fuel { get; private set; }
    public bool IsFiring { get; private set; }
    public bool HasFuel => Fuel > 0f;

    private Camera3D _camera = null!;
    private CpuParticles3D? _jet;
    private readonly Dictionary<ulong, Flammable?> _flammableCache = new();

    public override void _Ready()
    {
        _camera = GetParent<Camera3D>();
        _jet = GetNodeOrNull<CpuParticles3D>("FlameJet");
        Refill();
    }

    public override void _PhysicsProcess(double delta)
    {
        var wantsFire = Input.IsActionPressed("fire") && Fuel > 0f;
        if (wantsFire)
            Fire((float)delta);
        SetFiring(wantsFire);

        if (Input.IsActionJustPressed("refill"))
            Refill();
    }

    /// <summary>Delivers one physics tick of flame. Public so tests and cutscenes can drive it.</summary>
    public void Fire(float dt)
    {
        Fuel = Mathf.Max(0f, Fuel - Stats.FuelPerSecond * dt);
        EventBus.Instance.EmitSignal(EventBus.SignalName.FuelChanged, Fuel, Stats.FuelCapacity);

        var space = GetWorld3D().DirectSpaceState;
        var origin = _camera.GlobalPosition;
        var basis = _camera.GlobalBasis;
        var forward = -basis.Z;
        var heatPerRay = Stats.HeatPerSecond * dt / Stats.RayCount;
        var spread = Mathf.DegToRad(Stats.SpreadDegrees);

        for (var i = 0; i < Stats.RayCount; i++)
        {
            // First ray goes straight down the middle so a steady aim always lands; the rest jitter in the cone.
            var direction = forward;
            if (i > 0)
            {
                var yaw = (float)GD.RandRange(-spread, spread);
                var pitch = (float)GD.RandRange(-spread, spread);
                direction = forward.Rotated(basis.Y, yaw).Rotated(basis.X, pitch);
            }

            var query = PhysicsRayQueryParameters3D.Create(origin, origin + direction * Stats.Range, RayMask);
            var hit = space.IntersectRay(query);
            if (hit.Count == 0)
                continue;

            var flammable = FindFlammable(hit["collider"].AsGodotObject() as Node);
            flammable?.AddHeat(heatPerRay);
        }
    }

    public void Refill()
    {
        Fuel = Stats.FuelCapacity;
        EventBus.Instance.EmitSignal(EventBus.SignalName.FuelChanged, Fuel, Stats.FuelCapacity);
    }

    private void SetFiring(bool firing)
    {
        if (firing == IsFiring)
            return;
        IsFiring = firing;
        if (_jet is not null)
            _jet.Emitting = firing;
        EmitSignal(SignalName.FiringChanged, firing);
    }

    /// <summary>Convention: the Flammable component is a direct child of the collider named "Flammable".</summary>
    private Flammable? FindFlammable(Node? collider)
    {
        if (collider is null)
            return null;
        var id = collider.GetInstanceId();
        if (_flammableCache.TryGetValue(id, out var cached))
            return cached is not null && IsInstanceValid(cached) ? cached : null;

        var found = collider.GetNodeOrNull<Flammable>("Flammable");
        _flammableCache[id] = found;
        return found;
    }
}
