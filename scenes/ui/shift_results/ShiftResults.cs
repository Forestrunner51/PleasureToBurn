using Godot;

namespace PleasureToBurn;

/// <summary>End-of-shift summary. Emits what the player chose; the Game scene acts on it.</summary>
public partial class ShiftResults : CanvasLayer
{
    [Signal] public delegate void PrimaryPressedEventHandler();
    [Signal] public delegate void MenuPressedEventHandler();

    private Label _title = null!;
    private Label _body = null!;
    private Button _primaryButton = null!;

    public override void _Ready()
    {
        _title = GetNode<Label>("Panel/VBox/Title");
        _body = GetNode<Label>("Panel/VBox/Body");
        _primaryButton = GetNode<Button>("Panel/VBox/PrimaryButton");
        _primaryButton.Pressed += () => EmitSignal(SignalName.PrimaryPressed);
        GetNode<Button>("Panel/VBox/MenuButton").Pressed += () => EmitSignal(SignalName.MenuPressed);
    }

    public void ShowResults(bool success, int burned, int quota, int score, bool hasNext)
    {
        if (success)
        {
            _title.Text = "Shift Complete";
            _primaryButton.Text = hasNext ? "Next Shift" : "Finish Run";
        }
        else
        {
            _title.Text = "Quota Missed";
            _primaryButton.Text = "Retry Shift";
        }

        _body.Text = $"Books burned: {burned} / {quota}\nRun score: {score}";
        if (success && !hasNext)
            _body.Text += "\n\nThat was the last shift. Nice work.";

        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _primaryButton.GrabFocus();
    }
}
