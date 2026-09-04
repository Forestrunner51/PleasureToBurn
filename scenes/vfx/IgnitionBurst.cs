using Godot;

namespace PleasureToBurn;

/// <summary>
/// One-shot "it caught" feedback: particle pop, light pulse, whoomp. Frees itself.
/// Spawned by FireVfx; not meant to be placed by hand.
/// </summary>
public partial class IgnitionBurst : Node3D
{
    [Export] public float LifetimeSeconds { get; set; } = 0.8f;

    public override void _Ready()
    {
        GetNode<CpuParticles3D>("Particles").Restart();

        var light = GetNode<OmniLight3D>("Light");
        var tween = CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, LifetimeSeconds * 0.6f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        var audio = GetNode<AudioStreamPlayer3D>("Audio");
        audio.Stream = ProceduralAudio.Whoomp();
        audio.PitchScale = (float)GD.RandRange(0.9, 1.15);
        audio.Play();

        GetTree().CreateTimer(LifetimeSeconds).Timeout += QueueFree;
    }
}
