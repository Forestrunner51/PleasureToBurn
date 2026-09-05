using Godot;

namespace PleasureToBurn;

/// <summary>Prototype overlay. Purely reactive: listens to EventBus / FireSystem, never touches gameplay nodes.</summary>
public partial class Hud : CanvasLayer
{
    private ProgressBar _fuelBar = null!;
    private Label _fuelLabel = null!;
    private Label _contrabandLabel = null!;
    private Label _fireLabel = null!;
    private Label _promptLabel = null!;
    private Reticle _reticle = null!;
    private Label _objectiveLabel = null!;
    private Label _moneyLabel = null!;
    private Label _radioLabel = null!;
    private Label _clockLabel = null!;
    private Tween? _radioTween;
    private int _reputation;

    public override void _Ready()
    {
        _fuelBar = GetNode<ProgressBar>("Root/Bottom/FuelBar");
        _fuelLabel = GetNode<Label>("Root/Bottom/FuelLabel");
        _contrabandLabel = GetNode<Label>("Root/TopLeft/ContrabandLabel");
        _fireLabel = GetNode<Label>("Root/TopLeft/FireLabel");
        _promptLabel = GetNode<Label>("Root/Prompt");
        _reticle = GetNode<Reticle>("Root/Reticle");
        _objectiveLabel = GetNode<Label>("Root/TopCenter/ObjectiveLabel");
        _radioLabel = GetNode<Label>("Root/TopCenter/RadioLabel");
        _moneyLabel = GetNode<Label>("Root/TopRight/MoneyLabel");
        _clockLabel = GetNode<Label>("Root/TopRight/ClockLabel");
        _radioLabel.Modulate = new Color(1, 1, 1, 0);

        var bus = EventBus.Instance;
        bus.FuelChanged += OnFuelChanged;
        bus.ContrabandProgress += OnContrabandProgress;
        bus.AimChanged += OnAimChanged;
        bus.ObjectiveChanged += OnObjectiveChanged;
        bus.MoneyChanged += OnMoneyChanged;
        bus.RadioMessage += OnRadioMessage;
        bus.ShiftTimeChanged += OnShiftTimeChanged;
        bus.ReputationChanged += OnReputationChanged;
        if (FireSystem.Instance is { } fire)
        {
            fire.BurningCountChanged += OnBurningCountChanged;
            OnBurningCountChanged(fire.BurningCount);
        }
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.FuelChanged -= OnFuelChanged;
        bus.ContrabandProgress -= OnContrabandProgress;
        bus.AimChanged -= OnAimChanged;
        bus.ObjectiveChanged -= OnObjectiveChanged;
        bus.MoneyChanged -= OnMoneyChanged;
        bus.RadioMessage -= OnRadioMessage;
        bus.ShiftTimeChanged -= OnShiftTimeChanged;
        bus.ReputationChanged -= OnReputationChanged;
        if (FireSystem.Instance is { } fire)
            fire.BurningCountChanged -= OnBurningCountChanged;
    }

    private void OnFuelChanged(float fuel, float capacity)
    {
        _fuelBar.MaxValue = capacity;
        _fuelBar.Value = fuel;
        _fuelLabel.Text = fuel <= 0f ? "FUEL EMPTY  — find a fuel can" : $"Fuel {fuel:0} / {capacity:0}";
    }

    private void OnContrabandProgress(int burned, int total) =>
        _contrabandLabel.Text = burned >= total ? $"Contraband {burned} / {total}  ✓ all burned" : $"Contraband {burned} / {total}";

    private void OnBurningCountChanged(int burning) => _fireLabel.Text = $"Burning: {burning}";

    private void OnObjectiveChanged(string objective) => _objectiveLabel.Text = objective;

    private void OnMoneyChanged(int money) => _moneyLabel.Text = $"${money}";

    private void OnReputationChanged(int reputation) => _reputation = reputation;

    private void OnShiftTimeChanged(float secondsLeft, int day)
    {
        _clockLabel.Text = secondsLeft <= 0f
            ? $"Day {day}  ·  shift over  ·  rep {_reputation}"
            : $"Day {day}  ·  {ContractManager.FormatTime(secondsLeft)} left  ·  rep {_reputation}";
        _clockLabel.AddThemeColorOverride("font_color", secondsLeft <= 60f && secondsLeft > 0f ? new Color(1f, 0.5f, 0.4f) : new Color(0.85f, 0.85f, 0.8f));
    }

    /// <summary>Radio lines fade in, hold, fade out. A new line interrupts the old one.</summary>
    private void OnRadioMessage(string message)
    {
        _radioLabel.Text = message;
        _radioTween?.Kill();
        _radioTween = CreateTween();
        _radioTween.TweenProperty(_radioLabel, "modulate:a", 1f, 0.2);
        _radioTween.TweenInterval(6.0);
        _radioTween.TweenProperty(_radioLabel, "modulate:a", 0f, 0.8);
    }

    private void OnAimChanged(float heatFraction, int burnState, string prompt)
    {
        _reticle.SetAim(heatFraction, (BurnState)burnState);
        _promptLabel.Text = prompt;
    }
}
