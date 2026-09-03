using Godot;

namespace PleasureToBurn;

/// <summary>
/// Burns whatever the player is carrying. Heat rises per book and decays over time;
/// a hotter furnace gives a bigger score multiplier, so steady feeding is rewarded.
/// </summary>
public partial class Furnace : Interactable
{
    private static readonly PackedScene ScorePopupScene =
        GD.Load<PackedScene>("res://scenes/ui/score_popup/score_popup.tscn");

    [Export] public float MaxHeat { get; set; } = 100f;
    [Export] public float HeatDecayPerSecond { get; set; } = 4f;

    private float _heat;
    public float Heat
    {
        get => _heat;
        private set
        {
            _heat = Mathf.Clamp(value, 0f, MaxHeat);
            EventBus.Instance.EmitSignal(EventBus.SignalName.HeatChanged, _heat, MaxHeat);
        }
    }

    /// <summary>Score multiplier at the current heat: 1.0 when cold, 2.0 at max heat.</summary>
    public float Multiplier => 1f + Heat / MaxHeat;

    private CpuParticles3D _fire = null!;
    private CpuParticles3D _embers = null!;
    private OmniLight3D _light = null!;
    private StandardMaterial3D _glowMaterial = null!;
    private float _flickerTime;

    public override void _Ready()
    {
        _fire = GetNode<CpuParticles3D>("Fire");
        _embers = GetNode<CpuParticles3D>("Embers");
        _light = GetNode<OmniLight3D>("FireLight");

        var glow = GetNode<MeshInstance3D>("Glow");
        _glowMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.05f, 0.02f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.45f, 0.1f),
            EmissionEnergyMultiplier = 0f,
        };
        glow.MaterialOverride = _glowMaterial;

        Heat = 0f;
        UpdateVisuals(0f);
    }

    public override void _Process(double delta)
    {
        if (Heat > 0f)
            Heat -= HeatDecayPerSecond * (float)delta;
        _flickerTime += (float)delta;
        UpdateVisuals((float)delta);
    }

    public override string GetPrompt(Player player)
    {
        var count = player.CarriedCount;
        if (count == 0)
            return "Bring books to feed the furnace";
        return $"[E] Feed {count} book{(count == 1 ? "" : "s")} to the furnace";
    }

    public override void Interact(Player player)
    {
        var books = player.TakeAllBooks();
        if (books.Count == 0)
            return;
        var totalPoints = 0;
        foreach (var data in books)
            totalPoints += Burn(data);
        PlayBurnEffects(totalPoints);
    }

    private int Burn(BookData data)
    {
        var points = Mathf.RoundToInt(data.BurnValue * Multiplier);
        Heat += data.Heat;
        EventBus.Instance.EmitSignal(EventBus.SignalName.BookBurned, data, points);
        return points;
    }

    private void PlayBurnEffects(int points)
    {
        _embers.Restart();
        var popup = ScorePopupScene.Instantiate<ScorePopup>();
        popup.Position = new Vector3(0, 1.6f, 0.4f);
        AddChild(popup);
        popup.ShowPoints(points);
    }

    private void UpdateVisuals(float _)
    {
        var t = Heat / MaxHeat;
        var flicker = 1f + 0.12f * Mathf.Sin(_flickerTime * 23f) + 0.08f * Mathf.Sin(_flickerTime * 7.3f);
        _fire.Emitting = t > 0.02f;
        _fire.ScaleAmountMin = Mathf.Lerp(0.5f, 1.2f, t);
        _fire.ScaleAmountMax = Mathf.Lerp(1.0f, 2.4f, t);
        _fire.InitialVelocityMax = Mathf.Lerp(1.2f, 3.0f, t);
        _light.LightEnergy = Mathf.Lerp(0.15f, 6f, t) * flicker;
        _glowMaterial.EmissionEnergyMultiplier = Mathf.Lerp(0.1f, 4f, t) * flicker;
    }
}
