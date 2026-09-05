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

        // Career starts clean for the test (scratch save file).
        var career = Career.Instance;
        career.SavePath = "user://test_career.cfg";
        career.Reset();
        Check(career.Money == 0 && career.Day == 1, "career reset");
        Check(!manager.ShiftOver && manager.ShiftTimeLeft > 0f, "shift clock is running");
        Check(dispatch.Prompt.Contains("job board"), "dispatch offers the job board when idle");

        // Job board.
        var offers = manager.GenerateOffers();
        Check(offers.Count == 3, $"board offers 3 jobs (got {offers.Count})");
        Check(offers.Select(o => o.Site).Distinct().Count() == offers.Count, "offers are at distinct sites");
        Check(offers.All(o => o.ContrabandCount == 56 && o.EstimatedPay > 0 && o.BonusSeconds > 150f), "offers carry counts, pay and a deadline");
        Check(!manager.Accept(7), "accepting a bad index fails");
        Check(manager.Accept(0), "accepting the first offer succeeds");
        var job = manager.ActiveJob!;
        Check(manager.State == ContractState.Accepted && job.Site == offers[0].Site, "state is Accepted for the chosen site");
        Check(manager.CurrentOffers.Count == 0, "board clears once a job is taken");
        Check(manager.PreviewInvoice() is null, "no invoice while the job is open");
        Check(dispatch.Prompt.Contains("progress"), "dispatch shows job in progress");
        var beacon = world.GetNode<Node3D>("Beacon");
        Check(beacon.GlobalPosition.DistanceTo(job.Site.GlobalPosition + Vector3.Up * 20f) < 0.01f, "beacon marks the target site");

        // Burn every book in the target house.
        var house = manager.TargetLocation!;
        foreach (var book in FlammablesUnder(house).Where(f => f.IsContraband).ToList())
            book.Ignite();
        await Wait(GD.Load<BurnProfile>("res://resources/burn_profiles/paper.tres").Fuel + 1.5f);
        Check(house.AllContrabandBurned, $"all contraband charred ({house.ContrabandBurned}/{house.ContrabandTotal})");
        Check(manager.State == ContractState.Cleared, "state is Cleared once the house is done");
        Check(dispatch.Prompt.Contains("invoice"), "dispatch offers to settle the invoice");

        // Invoice maths.
        var preview = manager.PreviewInvoice()!;
        var rate = Mathf.RoundToInt(manager.PayPerContraband * (job.IsPrecision ? 1f + manager.PrecisionRateBonus : 1f));
        var expectedBase = 56 * rate;
        var expectedPenalty = job.IsPrecision ? house.CollateralBurned * manager.PrecisionPenaltyPerObject : 0;
        var expectedBonus = Mathf.RoundToInt(expectedBase * manager.DeadlineBonusFraction); // we were fast
        var expectedTotal = Math.Max(0, Mathf.RoundToInt((expectedBase + expectedBonus - expectedPenalty) * career.PayMultiplier));
        Check(preview.MadeDeadline, "a job finished in seconds makes the deadline");
        Check(preview.BasePay == expectedBase && preview.DeadlineBonus == expectedBonus && preview.CollateralPenalty == expectedPenalty,
            $"invoice lines: base {preview.BasePay}, bonus {preview.DeadlineBonus}, penalty {preview.CollateralPenalty}");
        Check(preview.Total == expectedTotal, $"invoice total ${preview.Total} matches ${expectedTotal}");
        Check(preview.Stars is >= 1 and <= 3, $"rating is {preview.Stars} stars");
        var settled = manager.Settle()!;
        Check(career.Money == settled.Total, $"paid ${career.Money}");
        Check(manager.State == ContractState.Idle && manager.JobsCompletedToday == 1, "back to idle, one job logged today");
        Check(manager.Settle() is null, "cannot settle twice");

        // Upgrades change the real stats.
        var tank = career.Upgrades.First(u => u.Id == "tank");
        career.AddMoney(5000);
        var moneyBefore = career.Money;
        var cost = career.CostOf(tank);
        Check(career.Buy(tank) && career.Level("tank") == 1 && career.Money == moneyBefore - cost, $"bought {tank.DisplayName} for ${cost}");
        Check(career.CostOf(tank) == tank.BaseCost + tank.CostGrowth, "next level costs more");
        var baseStats = GD.Load<FlamethrowerStats>("res://resources/flamethrower/standard_issue.tres");
        Check(career.EffectiveStats(baseStats).FuelCapacity == baseStats.FuelCapacity + 50f, "tank upgrade adds 50 fuel");
        Check(baseStats.FuelCapacity == 100f, "base stats resource is not mutated");
        Check(Mathf.IsEqualApprox(career.TruckPowerMultiplier, 1f), "no engine upgrade, no truck bonus");

        // Save / load round trip.
        career.Save();
        var savedMoney = career.Money;
        career.Reset();
        career.Load();
        Check(career.Money == savedMoney && career.Level("tank") == 1, "career round-trips through the save file");

        // End of day.
        Check(manager.EndDay() && career.Day == 2 && Mathf.IsEqualApprox(manager.ShiftTimeLeft, manager.ShiftLengthSeconds), "ending the day advances the calendar and resets the clock");

        // Next job respawns a fresh house.
        manager.GenerateOffers();
        Check(manager.Accept(0), "second job accepted");
        await NextFrame();
        Check(manager.TargetLocation != house && manager.TargetLocation!.ContrabandBurned == 0 && manager.TargetLocation.ContrabandTotal == 56, "second job gets a fresh, fully stocked building");

        // Panels exist and can be found by group.
        Check(GetTree().GetFirstNodeInGroup(JobBoard.Group) is JobBoard, "job board panel present");
        Check(GetTree().GetFirstNodeInGroup(InvoicePanel.Group) is InvoicePanel, "invoice panel present");
        Check(GetTree().GetFirstNodeInGroup(DayEndPanel.Group) is DayEndPanel, "day-end panel present");

        career.Reset();
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://test_career.cfg"));

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
