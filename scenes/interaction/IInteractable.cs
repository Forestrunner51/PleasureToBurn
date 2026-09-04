namespace PleasureToBurn;

/// <summary>
/// Implement on a body (StaticBody3D/RigidBody3D) that the player can use with the interact action.
/// The player finds it through the flamethrower's centre ray, so the body must be on physics layer 2 or 3
/// and within Player.InteractRange.
/// </summary>
public interface IInteractable
{
    /// <summary>Shown next to the reticle while aimed at. Return "" to show nothing.</summary>
    string Prompt { get; }

    void Interact(Player player);
}
