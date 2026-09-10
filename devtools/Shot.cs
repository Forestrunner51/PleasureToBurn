using Godot;

namespace Alexandria.DevTools;

/// <summary>
/// Offscreen screenshot harness, so layout and model orientation can be checked without opening the editor.
/// Not part of the game. Run it through devtools/shot.sh, which needs a desktop session because Godot
/// cannot render in --headless mode.
///
///   godot --path . res://devtools/shot.tscn -- &lt;scene_or_glb&gt; &lt;out.png&gt; [yaw] [elevation] [distance] [target_y]
///
/// yaw is degrees clockwise from north, elevation is degrees above the horizon (90 = straight down),
/// distance is metres from the target point, target_y is the height of the point being looked at.
/// </summary>
public partial class Shot : Node3D
{
    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var scenePath = args.Length > 0 ? args[0] : "res://scenes/world/world.tscn";
            var outPath = args.Length > 1 ? args[1] : "shot.png";
            var yaw = Arg(args, 2, 35f);
            var elevation = Arg(args, 3, 25f);
            var distance = Arg(args, 4, 20f);
            var targetY = Arg(args, 5, 3f);
            var targetX = Arg(args, 6, 0f);
            var targetZ = Arg(args, 7, 0f);

            AddChild(GD.Load<PackedScene>(scenePath).Instantiate());

            // A sun of our own, in case the scene is a bare .glb with no lighting.
            AddChild(new DirectionalLight3D
            {
                Rotation = new Vector3(Mathf.DegToRad(-50), Mathf.DegToRad(40), 0),
                LightEnergy = 1.1f,
            });

            var camera = new Camera3D { Fov = 55f, Far = 4000f };
            AddChild(camera);
            var target = new Vector3(targetX, targetY, targetZ);
            var offset = new Basis(Vector3.Up, Mathf.DegToRad(yaw))
                       * new Basis(Vector3.Right, Mathf.DegToRad(-elevation))
                       * Vector3.Back * distance;
            camera.GlobalPosition = target + offset;
            camera.LookAt(target, Vector3.Up);
            camera.MakeCurrent();

            // Let imports, lights and physics settle before grabbing the frame.
            for (var i = 0; i < 20; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var err = GetViewport().GetTexture().GetImage().SavePng(outPath);
            GD.Print(err == Error.Ok ? $"SHOT OK {outPath}" : $"SHOT FAILED {err}");
            GetTree().Quit(err == Error.Ok ? 0 : 1);
        }
        catch (Exception e)
        {
            GD.PrintErr($"SHOT CRASHED: {e}");
            GetTree().Quit(2);
        }
    }

    private static float Arg(string[] args, int i, float fallback) =>
        args.Length > i && float.TryParse(args[i], out var v) ? v : fallback;
}
