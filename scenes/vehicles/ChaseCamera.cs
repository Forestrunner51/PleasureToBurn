using Godot;

namespace PleasureToBurn;

/// <summary>
/// Third-person camera that trails a vehicle. TopLevel so the vehicle's roll and pitch do not
/// throw the view around; only its yaw and position are followed, smoothly.
/// </summary>
public partial class ChaseCamera : Camera3D
{
    [Export] public float Distance { get; set; } = 9f;
    [Export] public float Height { get; set; } = 3.5f;
    [Export] public float Smoothing { get; set; } = 6f;

    private Node3D _target = null!;

    public override void _Ready()
    {
        TopLevel = true;
        _target = GetParent<Node3D>();
        Snap();
    }

    public override void _Process(double delta)
    {
        if (!Current)
            return;
        var desired = DesiredPosition();
        GlobalPosition = GlobalPosition.Lerp(desired, 1f - Mathf.Exp(-Smoothing * (float)delta));
        LookAt(_target.GlobalPosition + Vector3.Up * 1.5f, Vector3.Up);
    }

    public void Snap()
    {
        GlobalPosition = DesiredPosition();
        LookAt(_target.GlobalPosition + Vector3.Up * 1.5f, Vector3.Up);
    }

    private Vector3 DesiredPosition()
    {
        var back = _target.GlobalBasis.Z with { Y = 0 };
        back = back.LengthSquared() > 0.001f ? back.Normalized() : Vector3.Back;
        return _target.GlobalPosition + back * Distance + Vector3.Up * Height;
    }
}
