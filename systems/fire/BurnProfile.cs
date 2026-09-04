using Godot;

namespace PleasureToBurn;

/// <summary>
/// How a kind of material burns. Shared by every Flammable that uses it, so tune once per material
/// (paper, wood, fabric...) in res://resources/burn_profiles/ rather than per object.
///
/// TUNE BY EYE: every number here is a feel knob. Start from the shipped presets and adjust
/// while watching a shelf burn; the ratios between materials matter more than absolute values.
/// </summary>
[GlobalClass]
public partial class BurnProfile : Resource
{
    /// <summary>Seconds the object burns before it is charred.</summary>
    [Export(PropertyHint.Range, "0.5,600,0.5,suffix:s")] public float Fuel { get; set; } = 10f;

    /// <summary>Accumulated heat needed before the object ignites. Lower = catches faster.</summary>
    [Export(PropertyHint.Range, "1,2000,1")] public float IgnitionTemperature { get; set; } = 100f;

    /// <summary>Heat per second given to a neighbour at zero distance; falls off linearly to zero at SpreadRadius.</summary>
    [Export(PropertyHint.Range, "0,2000,1")] public float HeatOutput { get; set; } = 60f;

    /// <summary>How far this object's fire reaches, in metres, measured between object origins.</summary>
    [Export(PropertyHint.Range, "0.1,10,0.05,suffix:m")] public float SpreadRadius { get; set; } = 1.2f;

    /// <summary>Heat lost per second while warm but not burning, so a brief flame lick does not count forever.</summary>
    [Export(PropertyHint.Range, "0,500,1")] public float CoolingRate { get; set; } = 25f;
}
