using Godot;

namespace PleasureToBurn;

/// <summary>
/// Global signal hub. Scenes never hold references to each other; they emit here and
/// whoever cares subscribes. Keep this class to signal declarations only.
/// </summary>
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; } = null!;

    /// <summary>A book was taken off a shelf.</summary>
    [Signal] public delegate void BookPickedUpEventHandler(BookData data);

    /// <summary>A book was consumed by the furnace. Points already include the heat multiplier.</summary>
    [Signal] public delegate void BookBurnedEventHandler(BookData data, int points);

    /// <summary>Furnace heat changed. Fires every frame while cooling, so keep handlers cheap.</summary>
    [Signal] public delegate void HeatChangedEventHandler(float heat, float maxHeat);

    /// <summary>The number of books the player is holding changed.</summary>
    [Signal] public delegate void CarryChangedEventHandler(int count, int maxCount);

    /// <summary>The run score changed.</summary>
    [Signal] public delegate void ScoreChangedEventHandler(int score);

    /// <summary>Text to show near the crosshair for the thing the player is looking at.</summary>
    [Signal] public delegate void PromptChangedEventHandler(string prompt);

    /// <summary>A shift began. Fired once by the game scene after everything is ready.</summary>
    [Signal] public delegate void ShiftStartedEventHandler(ShiftConfig config);

    /// <summary>Progress toward the current shift's quota.</summary>
    [Signal] public delegate void ShiftProgressEventHandler(int burned, int quota);

    /// <summary>Seconds remaining in the shift. Fires every frame.</summary>
    [Signal] public delegate void ShiftTimeChangedEventHandler(float secondsLeft);

    /// <summary>The shift timer ran out.</summary>
    [Signal] public delegate void ShiftEndedEventHandler(bool success);

    public override void _EnterTree() => Instance = this;
}
