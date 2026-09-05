using Godot;

namespace PleasureToBurn;

/// <summary>
/// A lot in the world where a building scene is spawned. Respawn() gives a fresh copy of the building
/// (new books, unburnt furniture) so the same location can be used for many contracts.
///
/// Scene setup: a Node3D placed where the building's origin should be; set Building and Address.
/// Sites join the "sites" group so ContractManager finds them without a hand-maintained list.
/// </summary>
public partial class Site : Node3D
{
    public const string Group = "sites";

    public override void _EnterTree() => AddToGroup(Group);

    [Export] public PackedScene? Building { get; set; }
    [Export] public string Address { get; set; } = "Unnamed lot";

    public Location? Current { get; private set; }

    public override void _Ready()
    {
        if (Current is null)
            Respawn();
    }

    public Location Respawn()
    {
        if (Current is not null && IsInstanceValid(Current))
            Current.QueueFree();
        if (Building is null)
            throw new InvalidOperationException($"Site '{Name}' has no Building scene.");
        Current = Building.Instantiate<Location>();
        AddChild(Current);
        return Current;
    }
}
