using Godot;

namespace PleasureToBurn;

/// <summary>
/// First-person controller: mouse look, WASD movement with acceleration, gravity.
/// Everything the player *does* (flamethrower, later: interact, carry) lives in child nodes.
///
/// Scene setup (by hand):
///   Player (CharacterBody3D, layer 1, mask 2)
///   ├── CollisionShape3D (Capsule r=0.4 h=1.8, y=0.9)
///   └── Head (Node3D, y=1.6)              ← pitches
///       └── Camera3D                        ← Flamethrower is a child of this
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float MaxSpeed { get; set; } = 5f;
    [Export] public float Acceleration { get; set; } = 40f;
    [Export] public float Friction { get; set; } = 50f;
    [Export] public float MouseSensitivity { get; set; } = 0.0022f;

    private const float MaxPitch = 1.45f; // ~83 degrees

    private Node3D _head = null!;
    private float _gravity;

    public Camera3D Camera { get; private set; } = null!;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        Camera = GetNode<Camera3D>("Head/Camera3D");
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var wish = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();

        var velocity = Velocity;
        var horizontal = new Vector3(velocity.X, 0, velocity.Z);
        horizontal = input != Vector2.Zero
            ? horizontal.MoveToward(wish * MaxSpeed, Acceleration * dt)
            : horizontal.MoveToward(Vector3.Zero, Friction * dt);

        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;
        velocity.Y = IsOnFloor() ? 0f : velocity.Y - _gravity * dt;
        Velocity = velocity;
        MoveAndSlide();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);
            var pitch = _head.Rotation.X - motion.Relative.Y * MouseSensitivity;
            _head.Rotation = new Vector3(Mathf.Clamp(pitch, -MaxPitch, MaxPitch), 0, 0);
        }
    }
}
