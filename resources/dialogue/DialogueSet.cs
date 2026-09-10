using Godot;

namespace Alexandria;

/// <summary>
/// One conversation: who is speaking and what they say, a line at a time. An Npc holds several and
/// picks one when it spawns, so the same house does not say the same thing twice in a row.
///
/// New resident, new officer, new mood = new .tres in res://resources/dialogue/, not new code.
/// </summary>
[GlobalClass]
public partial class DialogueSet : Resource
{
    /// <summary>Name shown above the line. Falls back to the Npc's DisplayName when empty.</summary>
    [Export] public string Speaker { get; set; } = "";

    [Export(PropertyHint.MultilineText)] public string[] Lines { get; set; } = Array.Empty<string>();

    public bool IsEmpty => Lines.Length == 0;
}
