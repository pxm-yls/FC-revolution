using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;

namespace FCRevolution.Core.Timeline.Persistence;

internal sealed class TimelineBranchTreeLoader
{
    public void PopulateBranchTree(BranchTree tree, TimelineManifest manifest, string romId, string? romPath = null)
    {
        tree.Clear();

        var nodeMap = new Dictionary<Guid, BranchPoint>();
        foreach (var branch in manifest.Branches.Where(item => !item.IsMainBranch))
        {
            var snapshotRecord = manifest.Snapshots.FirstOrDefault(item => item.BranchId == branch.BranchId && item.Kind == "BranchPoint");
            if (snapshotRecord == null || !File.Exists(snapshotRecord.StateFile))
                continue;

            var snapshot = StateSnapshotSerializer.Deserialize(File.ReadAllBytes(snapshotRecord.StateFile)).ToFrameSnapshot();
            nodeMap[branch.BranchId] = new BranchPoint
            {
                Id = branch.BranchId,
                Name = branch.Name,
                RomPath = romPath ?? "",
                Frame = snapshot.Frame,
                Timestamp = snapshot.Timestamp,
                Snapshot = snapshot,
                CreatedAt = branch.CreatedAtUtc,
            };
        }

        foreach (var branch in manifest.Branches.Where(item => !item.IsMainBranch))
        {
            if (!nodeMap.TryGetValue(branch.BranchId, out var point))
                continue;

            if (branch.ParentBranchId is Guid parentId && nodeMap.ContainsKey(parentId))
                tree.Fork(parentId, point);
            else
                tree.AddRoot(point);
        }
    }
}
