using Godot;

namespace PleasureToBurn;

/// <summary>
/// Crosshair that doubles as a heat gauge for whatever the player is aiming at:
/// a ring fills clockwise toward ignition, glows solid while burning, dims when charred.
/// Teaches material differences without a tutorial.
/// </summary>
public partial class Reticle : Control
{
    private const float Radius = 14f;
    private const float Thickness = 3f;

    private static readonly Color DotColor = new(1, 1, 1, 0.85f);
    private static readonly Color TrackColor = new(1, 1, 1, 0.2f);
    private static readonly Color HeatColor = new(1f, 0.55f, 0.15f);
    private static readonly Color BurningColor = new(1f, 0.85f, 0.3f);
    private static readonly Color CharredColor = new(0.45f, 0.45f, 0.45f);

    private float _fraction = -1f;
    private BurnState _state;

    public void SetAim(float heatFraction, BurnState state)
    {
        _fraction = heatFraction;
        _state = state;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var centre = Size / 2f;
        DrawCircle(centre, 2f, DotColor);
        if (_fraction < 0f)
            return;

        DrawArc(centre, Radius, 0f, Mathf.Tau, 48, TrackColor, Thickness, true);
        var start = -Mathf.Pi / 2f;
        switch (_state)
        {
            case BurnState.Unburnt when _fraction > 0f:
                DrawArc(centre, Radius, start, start + Mathf.Tau * _fraction, 48, HeatColor, Thickness, true);
                break;
            case BurnState.Burning:
                DrawArc(centre, Radius, 0f, Mathf.Tau, 48, BurningColor, Thickness, true);
                break;
            case BurnState.Charred:
                DrawArc(centre, Radius, 0f, Mathf.Tau, 48, CharredColor, Thickness, true);
                break;
        }
    }
}
