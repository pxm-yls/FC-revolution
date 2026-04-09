namespace FCRevolution.Core.Timeline;

/// <summary>A named save-branch point in the timeline.</summary>
public sealed class BranchPoint
{
    public Guid          Id        { get; init; } = Guid.NewGuid();
    public string        Name      { get; set;  } = "Branch";
    public string        RomPath   { get; set;  } = "";
    public long          Frame     { get; init; }
    public double        Timestamp { get; init; }
    public FrameSnapshot Snapshot  { get; init; } = null!;
    public DateTime      CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Child branches forked from this point.</summary>
    public List<BranchPoint> Children { get; } = new();
}

/// <summary>Manages a tree of branch points for the Save Branch Gallery.</summary>
public sealed class BranchTree
{
    private readonly List<BranchPoint> _roots = new();
    private readonly Dictionary<Guid, BranchPoint> _index = new();

    public IReadOnlyList<BranchPoint> Roots => _roots;

    public BranchPoint AddRoot(BranchPoint bp)
    {
        _roots.Add(bp);
        _index[bp.Id] = bp;
        return bp;
    }

    public BranchPoint Fork(Guid parentId, BranchPoint child)
    {
        if (_index.TryGetValue(parentId, out var parent))
        {
            parent.Children.Add(child);
            _index[child.Id] = child;
        }
        else
        {
            AddRoot(child);
        }
        return child;
    }

    public BranchPoint? Find(Guid id) => _index.GetValueOrDefault(id);

    public void Remove(Guid id)
    {
        if (!_index.TryGetValue(id, out var bp)) return;
        _index.Remove(id);
        _roots.Remove(bp);
        // Remove from parent children list
        foreach (var node in _index.Values)
            node.Children.Remove(bp);
    }

    public IEnumerable<BranchPoint> AllBranches() => _index.Values;

    public void Clear()
    {
        _roots.Clear();
        _index.Clear();
    }
}
