using Godot;

namespace PleasureToBurn;

/// <summary>In-game overlay. Purely reactive: listens to EventBus and never touches gameplay nodes.</summary>
public partial class Hud : CanvasLayer
{
    private Label _shiftLabel = null!;
    private Label _quotaLabel = null!;
    private Label _scoreLabel = null!;
    private Label _timeLabel = null!;
    private Label _heatLabel = null!;
    private ProgressBar _heatBar = null!;
    private Label _carryLabel = null!;
    private Label _promptLabel = null!;

    private static readonly Color QuotaMetColor = new(0.6f, 1f, 0.5f);
    private static readonly Color UrgentColor = new(1f, 0.45f, 0.35f);

    public override void _Ready()
    {
        _shiftLabel = GetNode<Label>("Root/TopLeft/ShiftLabel");
        _quotaLabel = GetNode<Label>("Root/TopLeft/QuotaLabel");
        _scoreLabel = GetNode<Label>("Root/TopLeft/ScoreLabel");
        _timeLabel = GetNode<Label>("Root/TopRight/TimeLabel");
        _heatLabel = GetNode<Label>("Root/TopCenter/HeatLabel");
        _heatBar = GetNode<ProgressBar>("Root/TopCenter/HeatBar");
        _carryLabel = GetNode<Label>("Root/BottomLeft/CarryLabel");
        _promptLabel = GetNode<Label>("Root/Prompt");

        var bus = EventBus.Instance;
        bus.ShiftProgress += OnShiftProgress;
        bus.ScoreChanged += OnScoreChanged;
        bus.ShiftTimeChanged += OnTimeChanged;
        bus.CarryChanged += OnCarryChanged;
        bus.HeatChanged += OnHeatChanged;
        bus.PromptChanged += OnPromptChanged;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        bus.ShiftProgress -= OnShiftProgress;
        bus.ScoreChanged -= OnScoreChanged;
        bus.ShiftTimeChanged -= OnTimeChanged;
        bus.CarryChanged -= OnCarryChanged;
        bus.HeatChanged -= OnHeatChanged;
        bus.PromptChanged -= OnPromptChanged;
    }

    /// <summary>Called once by the Game scene, because some emitters fire before the HUD is ready.</summary>
    public void Initialize(int shiftNumber, ShiftConfig config, int maxCarry, int score)
    {
        _shiftLabel.Text = $"Shift {shiftNumber} — {config.ShiftName}";
        OnShiftProgress(0, config.Quota);
        OnScoreChanged(score);
        OnTimeChanged(config.DurationSeconds);
        OnCarryChanged(0, maxCarry);
        OnHeatChanged(0f, 100f);
        OnPromptChanged("");
    }

    private void OnShiftProgress(int burned, int quota)
    {
        var met = burned >= quota;
        _quotaLabel.Text = met ? $"Burned {burned} / {quota}  ✓ quota met" : $"Burned {burned} / {quota}";
        _quotaLabel.AddThemeColorOverride("font_color", met ? QuotaMetColor : Colors.White);
    }

    private void OnScoreChanged(int score) => _scoreLabel.Text = $"Score {score}";

    private void OnTimeChanged(float secondsLeft)
    {
        var total = Mathf.CeilToInt(secondsLeft);
        _timeLabel.Text = $"{total / 60}:{total % 60:00}";
        _timeLabel.AddThemeColorOverride("font_color", total <= 10 ? UrgentColor : Colors.White);
    }

    private void OnCarryChanged(int count, int maxCount) => _carryLabel.Text = $"Carrying {count} / {maxCount}";

    private void OnHeatChanged(float heat, float maxHeat)
    {
        _heatBar.MaxValue = maxHeat;
        _heatBar.Value = heat;
        _heatLabel.Text = $"Furnace heat  ×{1f + heat / maxHeat:0.00}";
    }

    private void OnPromptChanged(string prompt) => _promptLabel.Text = prompt;
}
