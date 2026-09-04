using Godot;

namespace PleasureToBurn.Tests;

/// <summary>
/// Headless tests for the fire system and flamethrower. Run with:
///   godot --headless --path . res://tests/fire_tests.tscn
/// Exits 0 on success, 1 on any failed check. Builds its own props from code so it does not
/// depend on the test room layout.
/// </summary>
public partial class FireTests : Node3D
{
    private int _failures;
    private BurnProfile _paper = null!;

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        await NextFrame();
        var fire = FireSystem.Instance!;
        Check(fire is not null, "FireSystem autoload registered");
        _paper = GD.Load<BurnProfile>("res://resources/burn_profiles/paper.tres");
        Check(_paper.Fuel > 0, "paper burn profile loads");

        await SpreadTests(fire!);
        await CoolingTest(fire!);
        await FlamethrowerTests(fire!);
        await TestRoomTest(fire!);

        GD.Print(_failures == 0 ? "FIRE TESTS PASSED" : $"FIRE TESTS FAILED: {_failures} check(s)");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    // A row of books 0.3 m apart, plus one far away. Ignite the first; it should march down the row.
    private async Task SpreadTests(FireSystem fire)
    {
        fire.Reset();
        var chain = new List<Flammable>();
        for (var i = 0; i < 4; i++)
            chain.Add(MakeProp(new Vector3(i * 0.3f, 0, 0), _paper));
        var far = MakeProp(new Vector3(5f, 0, 0), _paper);
        await NextFrame();
        Check(fire.All.Count == 5, $"five flammables registered (got {fire.All.Count})");

        chain[0].Ignite();
        Check(chain[0].State == BurnState.Burning, "ignite sets Burning");
        Check(fire.BurningCount == 1, "burning count is 1 after ignition");
        var nearest = chain[0].Neighbours.FirstOrDefault(n => n.Target == chain[1]);
        Check(nearest.Target is not null && Mathf.IsEqualApprox(nearest.Weight, 0.5f), $"adjacent book cached as neighbour with falloff weight 0.5 (got {nearest.Weight})");
        Check(chain[0].Neighbours.All(n => n.Target != far), "far book is not a neighbour");

        await Wait(3.5f);
        Check(chain[1].State != BurnState.Unburnt, "fire spreads to the adjacent book");
        await Wait(7f);
        Check(chain[3].State != BurnState.Unburnt, "fire reaches the end of the row");
        Check(far.State == BurnState.Unburnt && far.Heat == 0f, "book out of range stays untouched");

        await Wait(_paper.Fuel + 1f);
        Check(chain[0].State == BurnState.Charred, "first book chars after its fuel runs out");
        Check(chain[0].Intensity == 0f, "charred object has zero intensity");
        Check(fire.CharredCount >= 1, "system counts charred objects");

        foreach (var f in chain) f.Body.QueueFree();
        far.Body.QueueFree();
        await NextFrame();
    }

    // Heat below the ignition threshold should bleed away.
    private async Task CoolingTest(FireSystem fire)
    {
        fire.Reset();
        var prop = MakeProp(Vector3.Zero, _paper);
        await NextFrame();
        prop.AddHeat(_paper.IgnitionTemperature * 0.5f);
        Check(prop.State == BurnState.Unburnt && prop.Heat > 0f, "sub-threshold heat does not ignite");
        await Wait(_paper.IgnitionTemperature / _paper.CoolingRate + 0.5f);
        Check(prop.Heat == 0f, "warm object cools back to zero");
        prop.Body.QueueFree();
        await NextFrame();
    }

    // Flamethrower pointed at a book: fuel drains, book heats and ignites.
    private async Task FlamethrowerTests(FireSystem fire)
    {
        fire.Reset();
        // A floor so the player stands still instead of falling between frames.
        var floor = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0, Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(20, 1, 20) } });
        AddChild(floor);
        var player = GD.Load<PackedScene>("res://scenes/player/player.tscn").Instantiate<Player>();
        player.Position = Vector3.Zero;
        AddChild(player);
        var flamethrower = player.GetNode<Flamethrower>("Head/Camera3D/Flamethrower");
        var target = MakeProp(new Vector3(0, 1.6f, -3f), _paper); // straight ahead of the camera
        var blocked = MakeProp(new Vector3(0, 1.6f, -3f - 0.5f), _paper); // behind the first one
        await NextFrame();
        await NextFrame();

        var fuelBefore = flamethrower.Fuel;
        Check(fuelBefore == flamethrower.Stats.FuelCapacity, "flamethrower starts full");
        flamethrower.Fire(0.1f);
        Check(flamethrower.Fuel < fuelBefore, "firing consumes fuel");
        Check(target.Heat > 0f, "flame heats the book in front of the camera");
        Check(blocked.Heat == 0f, "book behind the first one is shielded");

        for (var i = 0; i < 40 && target.State == BurnState.Unburnt; i++)
            flamethrower.Fire(0.05f);
        Check(target.State == BurnState.Burning, $"holding the flame ignites the book (state {target.State}, heat {target.Heat:0.0}, threshold {_paper.IgnitionTemperature}, fuel {flamethrower.Fuel:0.0})");

        var cold = MakeProp(new Vector3(0, 1.6f, -20f), _paper); // beyond range
        await NextFrame();
        flamethrower.Fire(0.1f);
        Check(cold.Heat == 0f, "flame does not reach beyond Stats.Range");

        player.QueueFree();
        floor.QueueFree();
        target.Body.QueueFree();
        blocked.Body.QueueFree();
        cold.Body.QueueFree();
        await NextFrame();
    }

    // The real prototype room loads and counts its contraband.
    private async Task TestRoomTest(FireSystem fire)
    {
        fire.Reset();
        var room = GD.Load<PackedScene>("res://scenes/locations/test_room.tscn").Instantiate<Location>();
        AddChild(room);
        await NextFrame();
        await NextFrame();
        Check(room.ContrabandTotal == 56, $"test room has 56 contraband items (3 shelves x 18 + 2 hidden), got {room.ContrabandTotal}");
        Check(fire.All.Count > room.ContrabandTotal, "furniture registers as flammable too");
        Check(room.GetNodeOrNull<Player>("Player") is not null, "room contains the player");

        var book = room.GetNode<Flammable>("Props/HiddenBookUnderTable/Flammable");
        book.Ignite();
        await Wait(_paper.Fuel + 1f);
        Check(room.ContrabandBurned >= 1, "burning a hidden book counts toward the objective");

        room.QueueFree();
        await NextFrame();
    }

    // --- helpers ---

    private Flammable MakeProp(Vector3 position, BurnProfile profile)
    {
        var body = new StaticBody3D { CollisionLayer = 4, CollisionMask = 0, Position = position };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) } });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.2f, 0.2f, 0.2f) } });
        var flammable = new Flammable { Name = "Flammable", Profile = profile, IsContraband = true };
        body.AddChild(flammable);
        AddChild(body);
        return flammable;
    }

    private SignalAwaiter NextFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private SignalAwaiter Wait(float seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

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
