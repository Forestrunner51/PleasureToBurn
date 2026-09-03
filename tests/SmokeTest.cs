using Godot;

namespace PleasureToBurn.Tests;

/// <summary>
/// Headless smoke test for the core loop. Run with:
///   godot --headless --path . res://tests/smoke_test.tscn
/// Exits 0 on success, 1 on any failed check.
/// </summary>
public partial class SmokeTest : Node
{
    private int _failures;

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        await NextFrame();

        Check(EventBus.Instance is not null, "EventBus autoload registered");
        Check(GameState.Instance is not null, "GameState autoload registered");
        Check(SceneLoader.Instance is not null, "SceneLoader autoload registered");
        var state = GameState.Instance!;
        Check(state.Shifts.Count == 3, "three shifts registered");
        Check(state.CurrentShift.BookPool.Length > 0, "shift 1 has a book pool");

        var menu = GD.Load<PackedScene>(SceneLoader.MainMenu).Instantiate();
        AddChild(menu);
        await NextFrame();
        Check(menu.GetNodeOrNull<Button>("VBox/StartButton") is not null, "main menu has a start button");
        menu.QueueFree();
        await NextFrame();

        state.StartNewRun();
        var game = GD.Load<PackedScene>(SceneLoader.Game).Instantiate<Game>();
        AddChild(game);
        await NextFrame();
        await NextFrame();

        var player = game.GetNode<Player>("Player");
        var furnace = game.GetNode<Furnace>("Furnace");
        var shelves = GetTree().GetNodesInGroup("shelves").OfType<Shelf>().ToList();
        var totalSlots = shelves.Sum(s => s.SlotCount);
        Check(shelves.Count == 5, $"level has 5 shelves (got {shelves.Count})");

        var books = BooksInTree();
        Check(books.Count == totalSlots, $"every slot stocked: {books.Count} books for {totalSlots} slots");

        // Pick up one book.
        var book = books[0];
        var data = book.Data!;
        book.Interact(player);
        Check(player.CarriedCount == 1, "player picks up a book");
        Check(book.IsQueuedForDeletion(), "picked-up book leaves the shelf");

        // Burn it.
        var scoreBefore = state.Score;
        var heatBefore = furnace.Heat;
        furnace.Interact(player);
        Check(player.CarriedCount == 0, "furnace empties the player's hands");
        Check(state.Score == scoreBefore + data.BurnValue, $"cold furnace awards base value ({data.BurnValue})");
        Check(furnace.Heat > heatBefore, "furnace heats up");
        Check(game.BurnedThisShift == 1, "game counts the burn toward the shift");
        Check(!game.QuotaMet, "one book does not meet the quota");

        // Hot furnace pays more than base value.
        for (var i = 0; i < 3; i++)
            player.TryAddBook(data);
        scoreBefore = state.Score;
        furnace.Interact(player);
        Check(state.Score - scoreBefore > data.BurnValue * 3, "hot furnace applies a multiplier");

        // Carry limit.
        for (var i = 0; i < player.MaxCarry + 3; i++)
            player.TryAddBook(data);
        Check(player.CarriedCount == player.MaxCarry, $"carry limit enforced at {player.MaxCarry}");
        player.TakeAllBooks();

        // Restock refills the emptied slot.
        await NextFrame();
        Check(BooksInTree().Count == totalSlots - 1, "one slot empty before restock");
        await ToSignal(GetTree().CreateTimer(state.CurrentShift.RestockSeconds + 0.2f), SceneTreeTimer.SignalName.Timeout);
        Check(BooksInTree().Count == totalSlots, "shelf restocks the empty slot");

        // Heat decays over time.
        var hot = furnace.Heat;
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        Check(furnace.Heat < hot, "furnace heat decays");

        game.QueueFree();
        await NextFrame();

        GD.Print(_failures == 0 ? "SMOKE TEST PASSED" : $"SMOKE TEST FAILED: {_failures} check(s)");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private List<Book> BooksInTree() =>
        GetTree().GetNodesInGroup("interactables").OfType<Book>().Where(b => !b.IsQueuedForDeletion()).ToList();

    private SignalAwaiter NextFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private void Check(bool condition, string label)
    {
        if (condition)
        {
            GD.Print($"  ok   {label}");
        }
        else
        {
            _failures++;
            GD.PrintErr($"  FAIL {label}");
        }
    }
}
