using Godot;

namespace PleasureToBurn;

/// <summary>
/// A row of book slots. Fills every slot on start and refills one empty slot per restock tick.
/// Slot positions are Marker3D children of $Slots, so a shelf's layout is edited in the scene.
/// </summary>
public partial class Shelf : Node3D
{
    private static readonly PackedScene BookScene = GD.Load<PackedScene>("res://scenes/book/book.tscn");

    [Export] public BookData[] BookPool { get; set; } = Array.Empty<BookData>();

    private float _restockSeconds = 3f;
    [Export(PropertyHint.Range, "0.1,60,0.1,suffix:s")]
    public float RestockSeconds
    {
        get => _restockSeconds;
        set
        {
            _restockSeconds = Mathf.Max(0.1f, value);
            if (IsNodeReady())
                _restockTimer.WaitTime = _restockSeconds;
        }
    }

    private readonly List<Marker3D> _slots = new();
    private Book?[] _slotBooks = Array.Empty<Book?>();
    private Godot.Timer _restockTimer = null!;

    public int SlotCount => _slots.Count;

    public override void _Ready()
    {
        foreach (var child in GetNode("Slots").GetChildren())
            if (child is Marker3D marker)
                _slots.Add(marker);
        _slotBooks = new Book?[_slots.Count];

        _restockTimer = GetNode<Godot.Timer>("RestockTimer");
        _restockTimer.WaitTime = _restockSeconds;
        _restockTimer.Timeout += RestockOne;
        _restockTimer.Start();
        FillAll();
    }

    /// <summary>Called by the Game scene with the current shift's tuning.</summary>
    public void Configure(BookData[] pool, float restockSeconds)
    {
        BookPool = pool;
        RestockSeconds = restockSeconds;
        FillAll();
    }

    public void FillAll()
    {
        for (var i = 0; i < _slots.Count; i++)
            if (IsSlotEmpty(i))
                SpawnAt(i);
    }

    private void RestockOne()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            if (!IsSlotEmpty(i))
                continue;
            SpawnAt(i);
            return;
        }
    }

    private bool IsSlotEmpty(int index)
    {
        var book = _slotBooks[index];
        return book is null || !IsInstanceValid(book) || book.IsQueuedForDeletion();
    }

    private void SpawnAt(int index)
    {
        if (BookPool.Length == 0)
            return;
        var book = BookScene.Instantiate<Book>();
        book.Data = BookPool[GD.RandRange(0, BookPool.Length - 1)];
        book.Position = _slots[index].Position;
        AddChild(book);
        _slotBooks[index] = book;
    }
}
