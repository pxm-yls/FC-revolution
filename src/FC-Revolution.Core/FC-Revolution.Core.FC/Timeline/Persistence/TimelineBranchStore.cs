namespace FCRevolution.Core.Timeline.Persistence;

internal sealed class TimelineBranchStore
{
    public TimelineBranchRecord EnsureBranch(TimelineManifest manifest, Guid branchId, string name, bool isMainBranch = false)
    {
        var branch = manifest.Branches.FirstOrDefault(item => item.BranchId == branchId);
        if (branch != null)
        {
            if (isMainBranch)
                branch.IsMainBranch = true;
            if (string.IsNullOrWhiteSpace(branch.Name))
                branch.Name = name;
            return branch;
        }

        branch = new TimelineBranchRecord
        {
            BranchId = branchId,
            Name = name,
            CreatedAtUtc = DateTime.UtcNow,
            IsMainBranch = isMainBranch,
        };
        manifest.Branches.Add(branch);
        return branch;
    }

    public void RenameBranch(TimelineManifest manifest, Guid branchId, string name)
    {
        var branch = manifest.Branches.FirstOrDefault(item => item.BranchId == branchId);
        if (branch == null)
            return;

        branch.Name = name;
        foreach (var snapshot in manifest.Snapshots.Where(item => item.BranchId == branchId && item.Kind == "BranchPoint"))
            snapshot.Name = name;
    }

    public void DeleteBranch(TimelineManifest manifest, string romId, Guid branchId)
    {
        var branchIds = CollectBranchCascade(manifest, branchId);
        manifest.Branches.RemoveAll(item => branchIds.Contains(item.BranchId));
        manifest.Snapshots.RemoveAll(item => branchIds.Contains(item.BranchId));

        foreach (var id in branchIds)
        {
            var branchDirectory = TimelineStoragePaths.GetBranchDirectory(romId, id);
            if (Directory.Exists(branchDirectory))
                Directory.Delete(branchDirectory, recursive: true);
        }

        if (manifest.Branches.All(item => item.BranchId != manifest.CurrentBranchId))
            manifest.CurrentBranchId = manifest.Branches.FirstOrDefault(item => item.IsMainBranch)?.BranchId
                ?? manifest.Branches.FirstOrDefault()?.BranchId
                ?? Guid.Empty;
    }

    private static HashSet<Guid> CollectBranchCascade(TimelineManifest manifest, Guid branchId)
    {
        var collected = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(branchId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!collected.Add(current))
                continue;

            foreach (var child in manifest.Branches.Where(item => item.ParentBranchId == current))
                stack.Push(child.BranchId);
        }

        return collected;
    }
}
