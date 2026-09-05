using Godot;

namespace PleasureToBurn;

/// <summary>
/// Third-person rig that trails a vehicle. The rig is TopLevel so the vehicle's roll and pitch do not
/// throw the view around; only its yaw and position are followed, smoothly. The actual Camera3D hangs
/// off a SpringArm3D child, which shortens when a wall is between the vehicle and the camera.
///
/// Scene setup: ChaseRig (Node3D, this script, top_level) → SpringArm3D (spring_length = Distance,
/// collision_mask = world layer, pointing +Z) → Camera3D.
/// </summary>
public partial class ChaseCamera : Node3D
{
    [Export] public float Distance { get; set; } = 9f;
    [Export] public float Height { get; set; } = 3.5f;
    /// <summary>Degrees the camera looks down at the vehicle.</summary>
    [Export] public float PitchDegrees { get; set; } = 14f;
    [Export] public float Smoothing { get; set; } = 6f;

    private Node3D _target = null!;
    private SpringArm3D _arm = null!;

    public bool IsActive => GetNodeOrNull<Camera3D>("SpringArm3D/ChaseCamera")?.Current ?? false;

    public override void _Ready()
    {
        TopLevel = true;
        _target = GetParent<Node3D>();
        _arm = GetNode<SpringArm3D>("SpringArm3D");
        _arm.SpringLength = Distance;
        Snap();
    }

    public override void _Process(double delta)
    {
        if (!IsActive)
            return;
        var desired = DesiredTransform();
        var t = 1f - Mathf.Exp(-Smoothing * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(desired.Origin, t);
        GlobalBasis = GlobalBasis.Slerp(desired.Basis, t);
    }

    /// <summary>Jump straight to the trailing position (on enter, so the camera does not swing in from far away).</summary>
    public void Snap() => GlobalTransform = DesiredTransform();

    /// <summary>Pivot sits above the vehicle facing its forward direction; the arm extends backwards from it.</summary>
    private Transform3D DesiredTransform()
    {
        var forward = -_target.GlobalBasis.Z with { Y = 0 };
        forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;
        var origin = _target.GlobalPosition + Vector3.Up * Height;
        var basis = Basis.LookingAt(forward, Vector3.Up).Rotated(forward.Cross(Vector3.Up).Normalized() * -1f, Mathf.DegToRad(PitchDegrees));
        return new Transform3D(basis, origin);
    }
}
