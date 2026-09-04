using Godot;

namespace PleasureToBurn;

/// <summary>
/// Global signal hub for gameplay-to-UI communication.
/// Scenes never hold references to each other; they emit here and whoever cares subscribes.
/// Keep this file to signal declarations only.
/// </summary>
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; } = null!;

    /// <summary>Flamethrower fuel changed.</summary>
    [Signal] public delegate void FuelChangedEventHandler(float fuel, float capacity);

    /// <summary>Contraband burned so far at the current location.</summary>
    [Signal] public delegate void ContrabandProgressEventHandler(int burned, int total);

    public override void _EnterTree() => Instance = this;
}
