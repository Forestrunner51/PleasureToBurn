using Godot;

namespace PleasureToBurn;

/// <summary>
/// Tuning for one shift (level). Author new shifts as .tres files in res://resources/shifts/
/// and register them in GameState.ShiftPaths.
/// </summary>
[GlobalClass]
public partial class ShiftConfig : Resource
{
    [Export] public string ShiftName { get; set; } = "Shift";

    /// <summary>Books that must be burned before the timer ends.</summary>
    [Export(PropertyHint.Range, "1,200")] public int Quota { get; set; } = 10;

    [Export(PropertyHint.Range, "10,600,1,suffix:s")] public float DurationSeconds { get; set; } = 90f;

    /// <summary>Seconds between a shelf refilling one empty slot.</summary>
    [Export(PropertyHint.Range, "0.2,30,0.1,suffix:s")] public float RestockSeconds { get; set; } = 3f;

    /// <summary>Books the shelves draw from at random.</summary>
    [Export] public BookData[] BookPool { get; set; } = Array.Empty<BookData>();
}
