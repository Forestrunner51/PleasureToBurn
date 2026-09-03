using Godot;

namespace PleasureToBurn;

/// <summary>
/// Static data for one kind of book that can appear on a shelf.
/// Author new books as .tres files in res://resources/books/.
/// </summary>
[GlobalClass]
public partial class BookData : Resource
{
    [Export] public string Title { get; set; } = "Untitled";

    /// <summary>Base points awarded when burned, before the furnace heat multiplier.</summary>
    [Export(PropertyHint.Range, "1,100")] public int BurnValue { get; set; } = 10;

    /// <summary>Heat added to the furnace when burned.</summary>
    [Export(PropertyHint.Range, "0,100")] public float Heat { get; set; } = 12f;

    [Export] public Color CoverColor { get; set; } = new(0.6f, 0.2f, 0.2f);
}
