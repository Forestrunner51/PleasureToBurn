using Godot;

namespace PleasureToBurn.Tests;

/// <summary>
/// Headless tests for the world: sites, the contract loop, and the truck hand-off. Run with:
///   godot --headless --path . res://tests/world_tests.tscn
/// </summary>
public partial class WorldTests : Node3D
{
    private int _failures;

    public override void _Ready() => _ = RunGuardedAsync();

    /// <summary>An exception inside an async test would otherwise be swallowed and the process would hang.</summary>
    private async Task RunGuardedAsync()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception e)
        {
            GD.PrintErr($"WORLD TESTS CRASHED: {e}");
            GetTree().Quit(2);
        }
    }

    private async Task RunAsync()
    {
        await NextFrame();
        var world = GD.Load<PackedScene>("res://scenes/world/world.tscn").Instantiate<Node3D>();
        AddChild(world);
        await NextFrame();
        await NextFrame();

        var manager = world.GetNode<ContractManager>("ContractManager");
        var player = world.GetNode<Player>("Player");
        var truck = world.GetNode<Truck>("Truck");
        var dispatch = world.GetNode<Dispatch>("Depot/Dispatch");
        var sites = world.GetNode("Sites").GetChildren().OfType<Site>().ToList();

        Check(player.Camera.Current, "player's first-person camera is the current camera at start");
        Check(!truck.GetNode<Camera3D>("ChaseRig/SpringArm3D/ChaseCamera").Current && !truck.GetNode<Camera3D>("CabCamera").Current, "truck cameras are not current at start");
        Check(sites.Count == 4, $"world has 4 sites (got {sites.Count})");
        Check(sites.All(s => s.Current is not null), "every site spawned its building");
        Check(manager.Sites.Count == sites.Count, "contract manager knows every site");
        Check(manager.State == ContractState.Idle, "starts idle");
        Check(dispatch.Prompt.Contains("report"), "dispatch offers a report when idle");

        // Take a report.
        Check(manager.TakeReport(), "taking a report succeeds");
        Check(manager.State == ContractState.Accepted, "state is Accepted");
        Check(manager.TargetLocation is not null && manager.TargetLocation.ContrabandTotal == 56, "target house has 56 contraband");
        Check(!manager.TakeReport(), "cannot take a second report while one is active");
        Check(dispatch.Prompt.Contains("progress"), "dispatch shows job in progress");
        var beacon = world.GetNode<Node3D>("Beacon");
        Check(beacon.GlobalPosition.DistanceTo(manager.TargetSite!.GlobalPosition) < 0.01f, "beacon moved to the target site");

        // Burn every book in the target house.
        var house = manager.TargetLocation!;
        var books = FlammablesUnder(house).Where(f => f.IsContraband).ToList();
        foreach (var book in books)
            book.Ignite();
        await Wait(GD.Load<BurnProfile>("res://resources/burn_profiles/paper.tres").Fuel + 1.5f);
        Check(house.AllContrabandBurned, $"all contraband charred ({house.ContrabandBurned}/{house.ContrabandTotal})");
        Check(manager.State == ContractState.Cleared, "state is Cleared once the house is done");
        Check(beacon.GlobalPosition.DistanceTo(world.GetNode<Node3D>("Depot/ReturnPoint").GlobalPosition) < 0.01f, "beacon moved back to the depot");
        Check(dispatch.Prompt.Contains("payment"), "dispatch offers payment");

        // Get paid.
        var expected = Math.Max(0, house.ContrabandBurned * manager.PayPerContraband
                                   - (manager.IsPrecisionJob ? house.CollateralBurned * manager.PrecisionPenaltyPerObject : 0));
        Check(manager.CollectPayment(), "collecting payment succeeds");
        Check(manager.Money == expected, $"paid ${manager.Money}, expected ${expected} (precision: {manager.IsPrecisionJob})");
        Check(manager.State == ContractState.Idle, "back to idle after payment");

        // Next report respawns a fresh house.
        var oldHouse = house;
        Check(manager.TakeReport(), "second report succeeds");
        await NextFrame();
        Check(manager.TargetLocation != oldHouse && manager.TargetLocation!.ContrabandBurned == 0, "second job gets a fresh building");
        Check(manager.TargetLocation.ContrabandTotal == 56, "fresh building is fully stocked");

        // Truck hand-off.
        Check(truck.Prompt.Contains("Drive"), "truck offers to be driven");
        truck.Interact(player);
        Check(truck.IsDriving && truck.Driver == player, "player enters the truck");
        Check(!player.Visible, "player is hidden while driving");
        Check(truck.GetNode<Camera3D>("ChaseRig/SpringArm3D/ChaseCamera").Current, "chase camera takes over");
        Check(truck.Prompt == "", "occupied truck shows no prompt");
        truck.Exit();
        Check(!truck.IsDriving && player.Visible, "player exits the truck");
        Check(player.Camera.Current, "player camera is current again");
        Check(player.GlobalPosition.DistanceTo(truck.GlobalPosition) < 4f, "player stands next to the truck");

        world.QueueFree();
        await NextFrame();
        GD.Print(_failures == 0 ? "WORLD TESTS PASSED" : $"WORLD TESTS FAILED: {_failures} check(s)");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private static IEnumerable<Flammable> FlammablesUnder(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Flammable f)
                yield return f;
            foreach (var nested in FlammablesUnder(child))
                yield return nested;
        }
    }

    private SignalAwaiter NextFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private SignalAwaiter Wait(float seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void Check(bool condition, string label)
    {
        if (condition) GD.Print($"  ok   {label}");
        else { _failures++; GD.PrintErr($"  FAIL {label}"); }
    }
}
