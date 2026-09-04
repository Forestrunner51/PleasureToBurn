using Godot;

namespace PleasureToBurn;

/// <summary>
/// One-shot refill for the flamethrower. Fuel is the location's economy: the player must let fire
/// spread on its own instead of torching every book by hand.
///
/// Scene setup (by hand):
///   FuelCan (StaticBody3D, layer 3, this script)
///   ├── Mesh (MeshInstance3D)   ← tinted grey when empty
///   └── CollisionShape3D
/// </summary>
public partial class FuelCan : StaticBody3D, IInteractable
{
    /// <summary>How many full refills this can holds.</summary>
    [Export(PropertyHint.Range, "1,10,1")] public int Charges { get; set; } = 1;

    public bool IsEmpty => Charges <= 0;

    public string Prompt => IsEmpty ? "Fuel can (empty)" : "[E] Refill flamethrower";

    private MeshInstance3D? _mesh;

    public override void _Ready() => _mesh = GetNodeOrNull<MeshInstance3D>("Mesh");

    public void Interact(Player player)
    {
        if (IsEmpty || player.Flamethrower.IsFull)
            return;
        Charges--;
        player.Flamethrower.Refill();
        if (IsEmpty && _mesh is not null)
            _mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.35f, 0.35f) };
    }
}
