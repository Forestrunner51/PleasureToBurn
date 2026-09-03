using Godot;

namespace PleasureToBurn;

/// <summary>
/// Run-wide state that must survive scene changes: score, shift index, saved high score.
/// Per-shift state (quota progress, timer) lives in the Game scene instead.
/// </summary>
public partial class GameState : Node
{
    public static GameState Instance { get; private set; } = null!;

    private static readonly string[] ShiftPaths =
    {
        "res://resources/shifts/shift_1.tres",
        "res://resources/shifts/shift_2.tres",
        "res://resources/shifts/shift_3.tres",
    };

    private const string SavePath = "user://save.cfg";

    public IReadOnlyList<ShiftConfig> Shifts { get; private set; } = Array.Empty<ShiftConfig>();
    public int Score { get; private set; }
    public int BooksBurned { get; private set; }
    public int ShiftIndex { get; private set; }
    public int HighScore { get; private set; }

    /// <summary>1-based, for display.</summary>
    public int ShiftNumber => ShiftIndex + 1;
    public bool HasNextShift => ShiftIndex < Shifts.Count - 1;
    public ShiftConfig CurrentShift => Shifts[Math.Clamp(ShiftIndex, 0, Shifts.Count - 1)];

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        Shifts = ShiftPaths.Select(GD.Load<ShiftConfig>).ToArray();
        Load();
    }

    public void StartNewRun()
    {
        Score = 0;
        BooksBurned = 0;
        ShiftIndex = 0;
        EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, Score);
    }

    public void AdvanceShift()
    {
        if (HasNextShift)
            ShiftIndex++;
    }

    public void RecordBurn(int points)
    {
        Score += points;
        BooksBurned++;
        HighScore = Math.Max(HighScore, Score);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ScoreChanged, Score);
    }

    public void SaveProgress()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("progress", "high_score", HighScore);
        var err = cfg.Save(SavePath);
        if (err != Error.Ok)
            GD.PushWarning($"Could not save progress ({err})");
    }

    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) != Error.Ok)
            return;
        HighScore = (int)cfg.GetValue("progress", "high_score", 0);
    }
}
