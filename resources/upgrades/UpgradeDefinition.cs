using Godot;

namespace PleasureToBurn;

/// <summary>
/// One purchasable upgrade line. The effect of each Id is applied in Career.EffectiveStats /
/// Career.TruckPowerMultiplier; adding a new Id means adding a case there. Costs and text are data.
/// </summary>
[GlobalClass]
public partial class UpgradeDefinition : Resource
{
    /// <summary>Stable key used for save files and effect lookup: "tank", "nozzle", "engine".</summary>
    [Export] public string Id { get; set; } = "tank";
    [Export] public string DisplayName { get; set; } = "Bigger Tank";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "+50 fuel capacity per level.";
    [Export] public int BaseCost { get; set; } = 400;
    /// <summary>Added to the cost for each level already owned.</summary>
    [Export] public int CostGrowth { get; set; } = 300;
    [Export(PropertyHint.Range, "1,10,1")] public int MaxLevel { get; set; } = 3;
}
