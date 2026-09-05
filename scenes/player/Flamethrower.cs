using Godot;

namespace PleasureToBurn;

/// <summary>
/// First-person flamethrower. While the fire action is held it samples a cone of rays from the
/// camera and pours heat into whatever Flammable they hit. Fuel drains while firing and is only
/// restored by calling Refill() (fuel cans, the truck); there is no free refill.
///
/// The centre ray is also cast every physics tick while idle so the reticle can show heat progress
/// and the player can find IInteractables.
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

    /// <summary>Random wobble added to the fixed ring pattern, as a fraction of the spread angle.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float Jitter { get; set; } = 0.15f;

    public float Fuel { get; private set; }
    public bool IsFiring { get; private set; }
    public bool HasFuel => Fuel > 0f;
    public bool IsFull => Fuel >= Stats.FuelCapacity;

    /// <summary>Body under the centre ray, or null. Updated every physics tick.</summary>
    public Node? AimCollider { get; private set; }
    public Flammable? AimFlammable { get; private set; }
    public float AimDistance { get; private set; } = float.PositiveInfinity;

    private Camera3D _camera = null!;
    private CpuParticles3D? _jet;
    private readonly Dictionary<ulong, Flammable?> _flammableCache = new();
    private float _lastHeatFraction = -2f;
    private int _lastState = -1;
    private string _lastPrompt = "";
    private float _ringPhase;

    public override void _Ready()
    {
        _camera = GetParent<Camera3D>();
        _jet = GetNodeOrNull<CpuParticles3D>("FlameJet");
        if (Career.Instance is { } career)
            Stats = career.EffectiveStats(Stats); // base .tres stays untouched
        Refill();
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateAim();
        var wantsFire = Input.IsActionPressed("fire") && Fuel > 0f;
        if (wantsFire)
            Fire((float)delta);
        SetFiring(wantsFire);
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

        // Fixed pattern: one ray down the middle, the rest on a ring that slowly rotates, plus a little jitter.
        // A steady aim gives a steady result; the rotation fills the cone over a few ticks.
        _ringPhase += 0.7f;
        var ringCount = Stats.RayCount - 1;
        for (var i = 0; i < Stats.RayCount; i++)
        {
            var direction = forward;
            if (i > 0)
            {
                var angle = _ringPhase + Mathf.Tau * (i - 1) / Mathf.Max(1, ringCount);
                var radius = spread * (0.55f + 0.45f * ((i - 1) % 2)); // alternate inner/outer ring
                var yaw = Mathf.Cos(angle) * radius + (float)GD.RandRange(-1.0, 1.0) * spread * Jitter;
                var pitch = Mathf.Sin(angle) * radius + (float)GD.RandRange(-1.0, 1.0) * spread * Jitter;
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

    /// <summary>Casts the centre ray and publishes what the reticle should show. Public for tests.</summary>
    public void UpdateAim()
    {
        var space = GetWorld3D().DirectSpaceState;
        var origin = _camera.GlobalPosition;
        var forward = -_camera.GlobalBasis.Z;
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + forward * Stats.Range, RayMask);
        var hit = space.IntersectRay(query);

        if (hit.Count == 0)
        {
            AimCollider = null;
            AimFlammable = null;
            AimDistance = float.PositiveInfinity;
        }
        else
        {
            AimCollider = hit["collider"].AsGodotObject() as Node;
            AimFlammable = FindFlammable(AimCollider);
            AimDistance = origin.DistanceTo(hit["position"].AsVector3());
        }

        var heatFraction = AimFlammable?.HeatFraction ?? -1f;
        var state = (int)(AimFlammable?.State ?? BurnState.Unburnt);
        var prompt = AimCollider is IInteractable interactable && AimDistance <= Player.InteractRange
            ? interactable.Prompt
            : "";

        if (Mathf.IsEqualApprox(heatFraction, _lastHeatFraction) && state == _lastState && prompt == _lastPrompt)
            return;
        _lastHeatFraction = heatFraction;
        _lastState = state;
        _lastPrompt = prompt;
        EventBus.Instance.EmitSignal(EventBus.SignalName.AimChanged, heatFraction, state, prompt);
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
