using Godot;

namespace PleasureToBurn;

/// <summary>Owns the paused state and mouse mode while open. The Game scene decides what the buttons do.</summary>
public partial class PauseMenu : CanvasLayer
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void RestartRequestedEventHandler();
    [Signal] public delegate void MenuRequestedEventHandler();

    private Button _resumeButton = null!;

    public override void _Ready()
    {
        _resumeButton = GetNode<Button>("Panel/VBox/ResumeButton");
        _resumeButton.Pressed += Close;
        GetNode<Button>("Panel/VBox/RestartButton").Pressed += () => EmitSignal(SignalName.RestartRequested);
        GetNode<Button>("Panel/VBox/MenuButton").Pressed += () => EmitSignal(SignalName.MenuRequested);
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
        EmitSignal(SignalName.ResumeRequested);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("pause"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
