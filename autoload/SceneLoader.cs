using Godot;

namespace PleasureToBurn;

/// <summary>
/// Switches between top-level scenes with a short fade.
/// Scene paths live here so the rest of the code never hard-codes them.
/// </summary>
public partial class SceneLoader : CanvasLayer
{
    public const string MainMenu = "res://scenes/main_menu/main_menu.tscn";
    public const string Game = "res://scenes/game/game.tscn";
    private const float FadeSeconds = 0.25f;

    public static SceneLoader Instance { get; private set; } = null!;

    private ColorRect _fade = null!;
    private bool _busy;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;
        _fade = new ColorRect
        {
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        };
        AddChild(_fade);
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public void ChangeScene(string path) => _ = ChangeSceneAsync(path);

    private async Task ChangeSceneAsync(string path)
    {
        if (_busy)
            return;
        _busy = true;
        _fade.MouseFilter = Control.MouseFilterEnum.Stop;
        await FadeToAsync(1f);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(path);
        await FadeToAsync(0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _busy = false;
    }

    private async Task FadeToAsync(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", alpha, FadeSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
