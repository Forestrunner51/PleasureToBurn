using Godot;

namespace PleasureToBurn;

/// <summary>
/// First-person player: mouse look, WASD movement, and a raycast that finds the
/// Interactable under the crosshair. Carries a visible stack of books.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float MaxSpeed { get; set; } = 5.5f;
    [Export] public float Acceleration { get; set; } = 40f;
    [Export] public float Friction { get; set; } = 50f;
    [Export] public float MouseSensitivity { get; set; } = 0.0022f;
    [Export(PropertyHint.Range, "1,12")] public int MaxCarry { get; set; } = 4;

    private const float MaxPitch = 1.45f; // ~83 degrees

    private readonly List<BookData> _carried = new();
    private readonly List<MeshInstance3D> _handMeshes = new();
    private Interactable? _target;
    private string _lastPrompt = "";
    private float _gravity;

    private Node3D _head = null!;
    private RayCast3D _ray = null!;
    private Node3D _hands = null!;

    public int CarriedCount => _carried.Count;
    public bool CanCarry => _carried.Count < MaxCarry;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _ray = GetNode<RayCast3D>("Head/Camera3D/InteractRay");
        _hands = GetNode<Node3D>("Head/Camera3D/Hands");
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        EmitCarryChanged();
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

    public override void _Process(double delta)
    {
        _target = _ray.IsColliding() ? _ray.GetCollider() as Interactable : null;
        if (_target is not null && _target.IsQueuedForDeletion())
            _target = null;

        var prompt = _target?.GetPrompt(this) ?? "";
        if (prompt != _lastPrompt)
        {
            _lastPrompt = prompt;
            EventBus.Instance.EmitSignal(EventBus.SignalName.PromptChanged, prompt);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);
            var pitch = _head.Rotation.X - motion.Relative.Y * MouseSensitivity;
            _head.Rotation = new Vector3(Mathf.Clamp(pitch, -MaxPitch, MaxPitch), 0, 0);
        }
        else if (@event.IsActionPressed("interact") && _target is not null)
        {
            _target.Interact(this);
            GetViewport().SetInputAsHandled();
        }
    }

    // --- Carrying -------------------------------------------------------------

    /// <summary>Returns false (and takes nothing) when the player's hands are full.</summary>
    public bool TryAddBook(BookData data)
    {
        if (!CanCarry)
            return false;
        _carried.Add(data);
        EmitCarryChanged();
        return true;
    }

    /// <summary>Empties the player's hands and returns what they were holding.</summary>
    public List<BookData> TakeAllBooks()
    {
        var books = new List<BookData>(_carried);
        _carried.Clear();
        EmitCarryChanged();
        return books;
    }

    private void EmitCarryChanged()
    {
        RefreshHands();
        EventBus.Instance.EmitSignal(EventBus.SignalName.CarryChanged, _carried.Count, MaxCarry);
    }

    private void RefreshHands()
    {
        foreach (var mesh in _handMeshes)
            mesh.QueueFree();
        _handMeshes.Clear();

        for (var i = 0; i < _carried.Count; i++)
        {
            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.05f, 0.2f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = _carried[i].CoverColor },
                Position = new Vector3(0, i * 0.055f, 0),
                RotationDegrees = new Vector3(0, (i % 2 == 0 ? 4f : -4f), 0),
            };
            _hands.AddChild(mesh);
            _handMeshes.Add(mesh);
        }
    }
}
