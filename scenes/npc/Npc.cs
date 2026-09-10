using Godot;

namespace Alexandria;

/// <summary>
/// Someone who is in the way of the job. Occupants do not resist, do not follow and cannot be hurt;
/// they stand in the room you came to burn and talk to you, which is the point.
///
/// Scene setup (see scenes/npc/npc.tscn):
///   Npc (StaticBody3D, layer 2, this script)   ← set DisplayName and Conversations
///   ├── Body (MeshInstance3D parts)
///   └── CollisionShape3D
///
/// Conversations are DialogueSet resources; one is chosen at random on spawn. Because a Site respawns
/// its building for every contract, the same address gets a different resident each job.
/// </summary>
public partial class Npc : StaticBody3D, IInteractable
{
    [Export] public string DisplayName { get; set; } = "Resident";

    /// <summary>What the prompt says before the name, e.g. "Talk to" or "Speak with".</summary>
    [Export] public string PromptVerb { get; set; } = "Talk to";

    [Export] public Godot.Collections.Array<DialogueSet> Conversations { get; set; } = new();

    /// <summary>The conversation this spawn will give. Null when the Npc has nothing to say.</summary>
    public DialogueSet? Current { get; private set; }

    /// <summary>True once the player has opened this conversation at least once.</summary>
    public bool HasSpoken { get; private set; }

    public string Prompt => Current is null ? "" : $"[E] {PromptVerb} {DisplayName}";

    public override void _Ready() => PickConversation();

    /// <summary>Choose which of the conversations this spawn will give. Public so tests can be deterministic.</summary>
    public void PickConversation(int index = -1)
    {
        var usable = Conversations.Where(set => set is { IsEmpty: false }).ToList();
        if (usable.Count == 0)
        {
            Current = null;
            return;
        }
        Current = usable[index >= 0 ? index % usable.Count : GD.RandRange(0, usable.Count - 1)];
    }

    public void Interact(Player player)
    {
        if (Current is null)
            return;
        HasSpoken = true;
        (GetTree().GetFirstNodeInGroup(DialoguePanel.Group) as DialoguePanel)?.Open(this);
    }
}
