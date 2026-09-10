using Godot;

namespace Alexandria;

/// <summary>
/// Shows one line of a conversation at a time. Interact or the button advances; the last line closes it.
/// Found by group, so a world scene only needs one instance.
/// </summary>
public partial class DialoguePanel : ModalPanel
{
    public const string Group = "dialogue_panel";

    public string SpeakerName { get; private set; } = "";
    public int LineIndex { get; private set; }
    public int LineCount => _lines.Length;
    public string CurrentLine => LineIndex < _lines.Length ? _lines[LineIndex] : "";

    private Label _speaker = null!;
    private Label _line = null!;
    private Button _next = null!;
    private string[] _lines = Array.Empty<string>();

    public override void _Ready()
    {
        AddToGroup(Group);
        _speaker = GetNode<Label>("Panel/VBox/Speaker");
        _line = GetNode<Label>("Panel/VBox/Line");
        _next = GetNode<Button>("Panel/VBox/NextButton");
        _next.Pressed += Advance;
    }

    public void Open(Npc npc)
    {
        if (npc.Current is not { IsEmpty: false } set)
            return;
        _lines = set.Lines;
        LineIndex = 0;
        SpeakerName = string.IsNullOrEmpty(set.Speaker) ? npc.DisplayName : set.Speaker;
        _speaker.Text = SpeakerName;
        ShowLine();
        OpenModal();
    }

    /// <summary>Step to the next line, closing on the last. Public so tests can walk a conversation.</summary>
    public void Advance()
    {
        LineIndex++;
        if (LineIndex >= _lines.Length)
        {
            Close();
            return;
        }
        ShowLine();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("interact"))
        {
            Advance();
            GetViewport().SetInputAsHandled();
            return;
        }
        base._UnhandledInput(@event);
    }

    private void ShowLine()
    {
        _line.Text = CurrentLine;
        _next.Text = LineIndex >= _lines.Length - 1 ? "Leave" : "Continue";
    }
}
