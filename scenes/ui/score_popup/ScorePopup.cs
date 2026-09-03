using Godot;

namespace PleasureToBurn;

/// <summary>Floating "+N" text in world space. Frees itself when the animation ends.</summary>
public partial class ScorePopup : Label3D
{
    public void ShowPoints(int points)
    {
        Text = $"+{points}";
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(this, "position:y", Position.Y + 0.8f, 0.9)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate:a", 0f, 0.5).SetDelay(0.4);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
