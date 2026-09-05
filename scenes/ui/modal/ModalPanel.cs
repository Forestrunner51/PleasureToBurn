using Godot;

namespace PleasureToBurn;

/// <summary>
/// Base for full-screen menus that pause the game and show the mouse while open.
/// Subclasses fill their content, then call Show()/Hide() through Open/Close.
/// The pause menu checks AnyOpen so Esc closes a panel instead of stacking the pause screen.
/// </summary>
public partial class ModalPanel : CanvasLayer
{
    public static bool AnyOpen { get; private set; }

    protected void OpenModal()
    {
        Visible = true;
        AnyOpen = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public virtual void Close()
    {
        if (!Visible)
            return;
        Visible = false;
        AnyOpen = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("pause"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    protected static Label MakeLabel(string text, int size = 16, Color? color = null, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c)
            label.AddThemeColorOverride("font_color", c);
        return label;
    }

    protected static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
            child.QueueFree();
    }
}
