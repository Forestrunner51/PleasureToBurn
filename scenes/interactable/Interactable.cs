using Godot;

namespace PleasureToBurn;

/// <summary>
/// Base class for anything the player can use with the "interact" action.
/// Subclasses live on physics layer 3 ("interactable") so the player's raycast can find them.
/// </summary>
public partial class Interactable : Area3D
{
    /// <summary>Text shown at the crosshair while the player is looking at this.</summary>
    public virtual string GetPrompt(Player player) => "[E] Interact";

    /// <summary>Called when the player presses interact while looking at this.</summary>
    public virtual void Interact(Player player) { }
}
