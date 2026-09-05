using Godot;

namespace PleasureToBurn;

/// <summary>End-of-day summary and the upgrade shop. "Start next day" advances the calendar.</summary>
public partial class DayEndPanel : ModalPanel
{
    public const string Group = "day_end_panel";

    private ContractManager? _manager;
    private Label _title = null!;
    private Label _summary = null!;
    private VBoxContainer _shop = null!;
    private Button _nextDay = null!;

    public override void _Ready()
    {
        AddToGroup(Group);
        _title = GetNode<Label>("Panel/VBox/Title");
        _summary = GetNode<Label>("Panel/VBox/Summary");
        _shop = GetNode<VBoxContainer>("Panel/VBox/Shop");
        _nextDay = GetNode<Button>("Panel/VBox/NextDayButton");
        _nextDay.Pressed += OnNextDay;
    }

    public void Open(ContractManager manager)
    {
        _manager = manager;
        var career = Career.Instance;
        _title.Text = $"End of Day {career.Day}";
        _summary.Text = $"Jobs completed: {manager.JobsCompletedToday}    Earned today: ${manager.EarnedToday}\nBalance: ${career.Money}    Reputation: {career.Reputation}";
        _nextDay.Text = $"Start Day {career.Day + 1}";
        RebuildShop();
        OpenModal();
    }

    private void RebuildShop()
    {
        ClearChildren(_shop);
        var career = Career.Instance;
        foreach (var def in career.Upgrades)
        {
            var row = new HBoxContainer();
            var level = career.Level(def.Id);
            var maxed = level >= def.MaxLevel;
            var text = MakeLabel($"{def.DisplayName}  (Lv {level}/{def.MaxLevel})\n{def.Description}", 14);
            text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(text);

            var buy = new Button
            {
                Text = maxed ? "Maxed" : $"Buy  ${career.CostOf(def)}",
                Disabled = !career.CanBuy(def),
                CustomMinimumSize = new Vector2(140, 0),
            };
            var captured = def;
            buy.Pressed += () =>
            {
                if (career.Buy(captured))
                {
                    _summary.Text = $"Jobs completed: {_manager?.JobsCompletedToday}    Earned today: ${_manager?.EarnedToday}\nBalance: ${career.Money}    Reputation: {career.Reputation}";
                    RebuildShop();
                }
            };
            row.AddChild(buy);
            _shop.AddChild(row);
        }
    }

    private void OnNextDay()
    {
        _manager?.EndDay();
        Close();
        // Upgrades are applied when the world loads, so start the new day on a fresh world.
        GetTree().ReloadCurrentScene();
    }
}
