using Godot;

namespace PleasureToBurn;

/// <summary>A book sitting on a shelf. Picking it up hands its BookData to the player.</summary>
public partial class Book : Interactable
{
    [Export] public BookData? Data { get; set; }

    private MeshInstance3D _cover = null!;
    private float _restY;

    public override void _Ready()
    {
        _cover = GetNode<MeshInstance3D>("Cover");
        if (Data is null)
        {
            GD.PushWarning("Book spawned without BookData; using defaults.");
            Data = new BookData();
        }
        _cover.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = Data.CoverColor,
            Roughness = 0.8f,
        };
        _restY = Position.Y;
        StartIdleBob();
    }

    public override string GetPrompt(Player player) =>
        player.CanCarry ? $"[E] Take \"{Data!.Title}\"" : "Hands full";

    public override void Interact(Player player)
    {
        if (!player.TryAddBook(Data!))
            return;
        EventBus.Instance.EmitSignal(EventBus.SignalName.BookPickedUp, Data!);
        QueueFree();
    }

    private void StartIdleBob()
    {
        var tween = CreateTween().SetLoops();
        tween.TweenProperty(this, "position:y", _restY + 0.03f, 0.9)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(this, "position:y", _restY, 0.9)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }
}
