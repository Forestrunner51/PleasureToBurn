using Godot;

namespace PleasureToBurn;

/// <summary>Owns pause state and mouse capture while open. Restart reloads the current scene.</summary>
public partial class PauseMenu : CanvasLayer
{
    private Button _resumeButton = null!;

    public override void _Ready()
    {
        _resumeButton = GetNode<Button>("Panel/VBox/ResumeButton");
        _resumeButton.Pressed += Close;
        GetNode<Button>("Panel/VBox/RestartButton").Pressed += Restart;
        GetNode<Button>("Panel/VBox/QuitButton").Pressed += () => GetTree().Quit();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("pause") || ModalPanel.AnyOpen)
            return;
        if (Visible) Close(); else Open();
        GetViewport().SetInputAsHandled();
    }

    public void Open()
    {
        Visible = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _resumeButton.GrabFocus();
    }

    public void Close()
    {
        Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void Restart()
    {
        GetTree().Paused = false;
        FireSystem.Instance?.Reset();
        GetTree().ReloadCurrentScene();
    }
}
