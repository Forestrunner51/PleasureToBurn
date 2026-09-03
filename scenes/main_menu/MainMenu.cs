using Godot;

namespace PleasureToBurn;

/// <summary>Title screen. Starts a fresh run or quits.</summary>
public partial class MainMenu : Control
{
    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetNode<Label>("VBox/HighScoreLabel").Text = $"Best run: {GameState.Instance.HighScore}";

        var start = GetNode<Button>("VBox/StartButton");
        start.Pressed += OnStartPressed;
        start.GrabFocus();
        GetNode<Button>("VBox/QuitButton").Pressed += () => GetTree().Quit();
    }

    private void OnStartPressed()
    {
        GameState.Instance.StartNewRun();
        SceneLoader.Instance.ChangeScene(SceneLoader.Game);
    }
}
