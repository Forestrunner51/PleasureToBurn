using Godot;

namespace PleasureToBurn;

/// <summary>
/// Everything that survives between days and scene reloads: money, day number, reputation, upgrade levels.
/// Saved to user:// with ConfigFile. Upgrade effects are applied here so the rest of the game only asks
/// for "effective" stats.
/// </summary>
public partial class Career : Node
{
    public static Career Instance { get; private set; } = null!;

    private static readonly string[] UpgradePaths =
    {
        "res://resources/upgrades/tank.tres",
        "res://resources/upgrades/nozzle.tres",
        "res://resources/upgrades/engine.tres",
    };

    /// <summary>Swappable so tests can use a scratch file.</summary>
    public string SavePath { get; set; } = "user://career.cfg";

    public int Money { get; private set; }
    public int Day { get; private set; } = 1;
    /// <summary>-5..20. Multiplies pay; unlocks nothing yet.</summary>
    public int Reputation { get; private set; }
    public IReadOnlyList<UpgradeDefinition> Upgrades { get; private set; } = Array.Empty<UpgradeDefinition>();

    private readonly Dictionary<string, int> _levels = new();

    public float PayMultiplier => 1f + 0.05f * Reputation;
    public float TruckPowerMultiplier => 1f + 0.2f * Level("engine");

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        Upgrades = UpgradePaths.Select(GD.Load<UpgradeDefinition>).ToArray();
        Load();
    }

    public int Level(string id) => _levels.TryGetValue(id, out var level) ? level : 0;

    public int CostOf(UpgradeDefinition def) => def.BaseCost + def.CostGrowth * Level(def.Id);

    public bool CanBuy(UpgradeDefinition def) => Level(def.Id) < def.MaxLevel && Money >= CostOf(def);

    public bool Buy(UpgradeDefinition def)
    {
        if (!CanBuy(def))
            return false;
        Money -= CostOf(def);
        _levels[def.Id] = Level(def.Id) + 1;
        EventBus.Instance.EmitSignal(EventBus.SignalName.MoneyChanged, Money);
        return true;
    }

    public void AddMoney(int amount)
    {
        Money = Math.Max(0, Money + amount);
        EventBus.Instance.EmitSignal(EventBus.SignalName.MoneyChanged, Money);
    }

    public void AddReputation(int delta)
    {
        Reputation = Math.Clamp(Reputation + delta, -5, 20);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ReputationChanged, Reputation);
    }

    public void AdvanceDay()
    {
        Day++;
        Save();
    }

    /// <summary>A copy of the base stats with every owned upgrade applied. The base resource is never mutated.</summary>
    public FlamethrowerStats EffectiveStats(FlamethrowerStats baseStats)
    {
        var stats = (FlamethrowerStats)baseStats.Duplicate();
        var tank = Level("tank");
        var nozzle = Level("nozzle");
        stats.FuelCapacity += 50f * tank;
        stats.Range += 1f * nozzle;
        stats.SpreadDegrees += 2f * nozzle;
        stats.HeatPerSecond *= 1f + 0.1f * nozzle;
        stats.RayCount += 2 * nozzle;
        return stats;
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("career", "money", Money);
        cfg.SetValue("career", "day", Day);
        cfg.SetValue("career", "reputation", Reputation);
        foreach (var (id, level) in _levels)
            cfg.SetValue("upgrades", id, level);
        var err = cfg.Save(SavePath);
        if (err != Error.Ok)
            GD.PushWarning($"Could not save career ({err})");
    }

    public void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) != Error.Ok)
            return;
        Money = (int)cfg.GetValue("career", "money", 0);
        Day = (int)cfg.GetValue("career", "day", 1);
        Reputation = (int)cfg.GetValue("career", "reputation", 0);
        _levels.Clear();
        if (cfg.HasSection("upgrades"))
            foreach (var id in cfg.GetSectionKeys("upgrades"))
                _levels[id] = (int)cfg.GetValue("upgrades", id, 0);
        EventBus.Instance.EmitSignal(EventBus.SignalName.MoneyChanged, Money);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ReputationChanged, Reputation);
    }

    /// <summary>Wipe progress (new game, tests).</summary>
    public void Reset()
    {
        Money = 0;
        Day = 1;
        Reputation = 0;
        _levels.Clear();
        EventBus.Instance.EmitSignal(EventBus.SignalName.MoneyChanged, Money);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ReputationChanged, Reputation);
    }
}
