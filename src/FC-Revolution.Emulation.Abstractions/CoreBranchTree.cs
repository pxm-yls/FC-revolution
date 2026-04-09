namespace FCRevolution.Emulation.Abstractions;

public sealed class CoreBranchTree
{
    private readonly List<CoreBranchPoint> _roots = [];
    private readonly Dictionary<Guid, CoreBranchPoint> _index = [];

    public IReadOnlyList<CoreBranchPoint> Roots => _roots;

    public CoreBranchPoint AddRoot(CoreBranchPoint branchPoint)
    {
        ArgumentNullException.ThrowIfNull(branchPoint);

        _roots.Add(branchPoint);
        IndexBranch(branchPoint);
        return branchPoint;
    }

    public CoreBranchPoint Fork(Guid parentId, CoreBranchPoint child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_index.TryGetValue(parentId, out var parent))
            parent.Children.Add(child);
        else
            _roots.Add(child);

        IndexBranch(child);
        return child;
    }

    public CoreBranchPoint? Find(Guid id) => _index.GetValueOrDefault(id);

    public void Remove(Guid id)
    {
        if (!_index.TryGetValue(id, out var branchPoint))
            return;

        _index.Remove(id);
        _roots.Remove(branchPoint);
        foreach (var node in _index.Values)
            node.Children.Remove(branchPoint);
    }

    public IEnumerable<CoreBranchPoint> AllBranches() => _index.Values;

    public void ReplaceRoots(IEnumerable<CoreBranchPoint> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        Clear();
        foreach (var root in roots)
            AddRoot(root);
    }

    public void Clear()
    {
        _roots.Clear();
        _index.Clear();
    }

    private void IndexBranch(CoreBranchPoint branchPoint)
    {
        _index[branchPoint.Id] = branchPoint;
        foreach (var child in branchPoint.Children)
            IndexBranch(child);
    }
}
