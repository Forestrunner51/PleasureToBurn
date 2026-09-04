using Godot;

namespace PleasureToBurn;

/// <summary>
/// Tunable numbers for one flamethrower tier. Upgrades are just different .tres files
/// in res://resources/flamethrower/, so an upgrade shop only needs to swap the resource.
///
/// TUNE BY EYE: Range, SpreadDegrees and HeatPerSecond define the feel. HeatPerSecond relative
/// to BurnProfile.IgnitionTemperature sets how long you must hold the flame on something.
/// </summary>
[GlobalClass]
public partial class FlamethrowerStats : Resource
{
    [Export] public string DisplayName { get; set; } = "Standard Issue";

    /// <summary>Metres the flame reaches.</summary>
    [Export(PropertyHint.Range, "1,30,0.5,suffix:m")] public float Range { get; set; } = 6f;

    /// <summary>Half-angle of the flame cone.</summary>
    [Export(PropertyHint.Range, "0,45,0.5,suffix:°")] public float SpreadDegrees { get; set; } = 7f;

    /// <summary>Total heat per second delivered across the cone (split between rays).</summary>
    [Export(PropertyHint.Range, "1,5000,1")] public float HeatPerSecond { get; set; } = 240f;

    [Export(PropertyHint.Range, "1,1000,1")] public float FuelCapacity { get; set; } = 100f;

    /// <summary>Fuel consumed per second while firing.</summary>
    [Export(PropertyHint.Range, "0,100,0.1")] public float FuelPerSecond { get; set; } = 6f;

    /// <summary>Rays cast per physics tick to sample the cone. More = smoother coverage, more cost.</summary>
    [Export(PropertyHint.Range, "1,32,1")] public int RayCount { get; set; } = 6;
}
