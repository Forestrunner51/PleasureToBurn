using Godot;

namespace PleasureToBurn;

/// <summary>
/// Runs one shift: applies the ShiftConfig to the level, tracks quota progress,
/// and wires the pause and results menus to scene transitions.
/// </summary>
public partial class Game : Node3D
{
    private Hud _hud = null!;
    private PauseMenu _pauseMenu = null!;
    private ShiftResults _results = null!;
    private Godot.Timer _shiftTimer = null!;
    private Player _player = null!;

    private ShiftConfig _config = null!;
    private int _burnedThisShift;
    private bool _shiftOver;

    public bool QuotaMet => _burnedThisShift >= _config.Quota;
    public int BurnedThisShift => _burnedThisShift;

    public override void _Ready()
    {
        _hud = GetNode<Hud>("HUD");
        _pauseMenu = GetNode<PauseMenu>("PauseMenu");
        _results = GetNode<ShiftResults>("ShiftResults");
        _shiftTimer = GetNode<Godot.Timer>("ShiftTimer");
        _player = GetNode<Player>("Player");

        _config = GameState.Instance.CurrentShift;

        foreach (var node in GetTree().GetNodesInGroup("shelves"))
            if (node is Shelf shelf)
                shelf.Configure(_config.BookPool, _config.RestockSeconds);

        _shiftTimer.WaitTime = _config.DurationSeconds;
        _shiftTimer.Timeout += EndShift;
        _shiftTimer.Start();

        EventBus.Instance.BookBurned += OnBookBurned;
        _pauseMenu.RestartRequested += RestartShift;
        _pauseMenu.MenuRequested += GoToMenu;
        _results.PrimaryPressed += OnResultsPrimary;
        _results.MenuPressed += GoToMenu;

        _hud.Initialize(GameState.Instance.ShiftNumber, _config, _player.MaxCarry, GameState.Instance.Score);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ShiftStarted, _config);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.BookBurned -= OnBookBurned;
    }

    public override void _Process(double delta)
    {
        if (!_shiftOver)
            EventBus.Instance.EmitSignal(EventBus.SignalName.ShiftTimeChanged, (float)_shiftTimer.TimeLeft);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause") && !_shiftOver)
        {
            _pauseMenu.Open();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBookBurned(BookData data, int points)
    {
        if (_shiftOver)
            return;
        _burnedThisShift++;
        GameState.Instance.RecordBurn(points);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ShiftProgress, _burnedThisShift, _config.Quota);
    }

    private void EndShift()
    {
        if (_shiftOver)
            return;
        _shiftOver = true;
        var success = QuotaMet;
        GameState.Instance.SaveProgress();
        EventBus.Instance.EmitSignal(EventBus.SignalName.ShiftEnded, success);
        GetTree().Paused = true;
        _results.ShowResults(success, _burnedThisShift, _config.Quota, GameState.Instance.Score, GameState.Instance.HasNextShift);
    }

    private void OnResultsPrimary()
    {
        if (QuotaMet && GameState.Instance.HasNextShift)
        {
            GameState.Instance.AdvanceShift();
            SceneLoader.Instance.ChangeScene(SceneLoader.Game);
        }
        else if (QuotaMet)
        {
            GoToMenu();
        }
        else
        {
            RestartShift();
        }
    }

    private void RestartShift() => SceneLoader.Instance.ChangeScene(SceneLoader.Game);

    private void GoToMenu()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SceneLoader.Instance.ChangeScene(SceneLoader.MainMenu);
    }
}
