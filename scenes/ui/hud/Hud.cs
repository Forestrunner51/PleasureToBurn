using Godot;

namespace PleasureToBurn;

/// <summary>Prototype overlay. Purely reactive: listens to EventBus / FireSystem, never touches gameplay nodes.</summary>
public partial class Hud : CanvasLayer
{
    private ProgressBar _fuelBar = null!;
    private Label _fuelLabel = null!;
    private Label _contrabandLabel = null!;
    private Label _fireLabel = null!;

    public override void _Ready()
    {
        _fuelBar = GetNode<ProgressBar>("Root/Bottom/FuelBar");
        _fuelLabel = GetNode<Label>("Root/Bottom/FuelLabel");
        _contrabandLabel = GetNode<Label>("Root/TopLeft/ContrabandLabel");
        _fireLabel = GetNode<Label>("Root/TopLeft/FireLabel");

        EventBus.Instance.FuelChanged += OnFuelChanged;
        EventBus.Instance.ContrabandProgress += OnContrabandProgress;
        if (FireSystem.Instance is { } fire)
        {
            fire.BurningCountChanged += OnBurningCountChanged;
            OnBurningCountChanged(fire.BurningCount);
        }
    }

    public override void _ExitTree()
    {
        EventBus.Instance.FuelChanged -= OnFuelChanged;
        EventBus.Instance.ContrabandProgress -= OnContrabandProgress;
        if (FireSystem.Instance is { } fire)
            fire.BurningCountChanged -= OnBurningCountChanged;
    }

    private void OnFuelChanged(float fuel, float capacity)
    {
        _fuelBar.MaxValue = capacity;
        _fuelBar.Value = fuel;
        _fuelLabel.Text = fuel <= 0f ? "FUEL EMPTY  (R to refill)" : $"Fuel {fuel:0} / {capacity:0}";
    }

    private void OnContrabandProgress(int burned, int total) =>
        _contrabandLabel.Text = burned >= total ? $"Contraband {burned} / {total}  ✓ all burned" : $"Contraband {burned} / {total}";

    private void OnBurningCountChanged(int burning) => _fireLabel.Text = $"Burning: {burning}";
}
