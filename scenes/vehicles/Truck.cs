using Godot;

namespace PleasureToBurn;

/// <summary>
/// Arcade fire truck on Godot's built-in VehicleBody3D. Not a driving sim: it needs to feel fine
/// and get you across town. The player enters with the interact action and leaves the same way.
///
/// Scene setup (by hand):
///   Truck (VehicleBody3D, layer 2, mask 2, this script)
///   ├── CollisionShape3D (box for the chassis)
///   ├── meshes...
///   ├── WheelFL/FR (VehicleWheel3D, use_as_steering + traction) with a cylinder mesh child
///   ├── WheelRL/RR (VehicleWheel3D, use_as_traction)
///   ├── ExitPoint (Marker3D)          ← where the player stands after getting out
///   ├── CabCamera (Camera3D)           ← first-person view from the driver's seat
///   └── ChaseRig (Node3D, top_level, ChaseCamera.cs)
///       └── SpringArm3D → ChaseCamera (Camera3D)   ← arm shortens when something is behind the truck
///
/// TUNE BY EYE: EnginePower, MaxSteerDegrees, wheel suspension values in the scene, mass, centre of mass.
/// Godot 4.x notes: VehicleBody3D uses engine_force/brake/steering; there is no gearbox, so top speed comes
/// from drag (linear_damp) fighting engine force. Positive engine_force drives toward the body's +Z, the
/// opposite of Godot's usual -Z forward, so the truck's nose is +Z and the model is not rotated.
/// </summary>
public partial class Truck : VehicleBody3D, IInteractable
{
    [Export] public float EnginePower { get; set; } = 5500f;
    [Export] public float BrakeForce { get; set; } = 80f;
    [Export] public float MaxSteerDegrees { get; set; } = 30f;
    [Export] public float SteerSpeed { get; set; } = 5f;

    public bool IsDriving { get; private set; }
    public Player? Driver { get; private set; }

    public string Prompt => IsDriving ? "" : "[E] Drive the truck";

    private ChaseCamera _chaseRig = null!;
    private Camera3D _chaseCamera = null!;
    private Camera3D _cabCamera = null!;
    private Marker3D _exitPoint = null!;
    private bool _useCabCamera;

    public override void _Ready()
    {
        _chaseRig = GetNode<ChaseCamera>("ChaseRig");
        _chaseCamera = GetNode<Camera3D>("ChaseRig/SpringArm3D/ChaseCamera");
        _cabCamera = GetNode<Camera3D>("CabCamera");
        _exitPoint = GetNode<Marker3D>("ExitPoint");
        _chaseCamera.Current = false;
        _cabCamera.Current = false;
        if (Career.Instance is { } career)
            EnginePower *= career.TruckPowerMultiplier;
    }

    public void Interact(Player player) => Enter(player);

    public void Enter(Player player)
    {
        if (IsDriving)
            return;
        Driver = player;
        IsDriving = true;
        player.SetControlEnabled(false);
        _chaseRig.Snap();
        ApplyCamera();
        // The player's aim ray stops updating while disabled, so clear what it last published.
        EventBus.Instance.EmitSignal(EventBus.SignalName.AimChanged, -1f, 0, "");
        EventBus.Instance.EmitSignal(EventBus.SignalName.RadioMessage, "W/S drive · A/D steer · Space brake · C camera · E get out");
    }

    public void Exit()
    {
        if (!IsDriving || Driver is null)
            return;
        var player = Driver;
        Driver = null;
        IsDriving = false;
        EngineForce = 0f;
        Steering = 0f;
        Brake = BrakeForce;

        player.GlobalPosition = _exitPoint.GlobalPosition;
        // Face the same way as the truck (nose is +Z) so the player is not disoriented.
        var forward = GlobalBasis.Z with { Y = 0 };
        if (forward.LengthSquared() > 0.001f)
            player.LookAt(player.GlobalPosition + forward.Normalized(), Vector3.Up);
        player.SetControlEnabled(true);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsDriving)
        {
            EngineForce = 0f;
            Brake = BrakeForce * 0.5f; // parked
            return;
        }

        var dt = (float)delta;
        var throttle = Input.GetAxis("move_down", "move_up");
        var steer = Input.GetAxis("move_right", "move_left");
        Steering = Mathf.Lerp(Steering, steer * Mathf.DegToRad(MaxSteerDegrees), SteerSpeed * dt);
        EngineForce = throttle * EnginePower;
        Brake = Input.IsActionPressed("brake") ? BrakeForce : 0f;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsDriving)
            return;
        if (@event.IsActionPressed("interact"))
        {
            Exit();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("camera_toggle"))
        {
            _useCabCamera = !_useCabCamera;
            ApplyCamera();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ApplyCamera()
    {
        if (_useCabCamera)
            _cabCamera.Current = true;
        else
            _chaseCamera.Current = true;
    }
}
