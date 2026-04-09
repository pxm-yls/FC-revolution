using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;

namespace FCRevolution.Core.Timeline.Persistence;

internal sealed class TimelineSnapshotStore
{
    public TimelineSnapshotRecord UpsertQuickSaveSnapshot(
        TimelineManifest manifest,
        Guid branchId,
        long frame,
        double timestampSeconds,
        Func<TimelineManifest, Guid, string, bool, TimelineBranchRecord> ensureBranch)
    {
        var branch = ensureBranch(
            manifest,
            branchId,
            "主线",
            branchId == TimelineStoragePaths.GetStableMainBranchId(manifest.RomId));
        var statePath = TimelineStoragePaths.GetQuickSavePath(manifest.RomId, branchId);

        var snapshot = branch.QuickSaveSnapshotId.HasValue
            ? manifest.Snapshots.FirstOrDefault(item => item.SnapshotId == branch.QuickSaveSnapshotId.Value)
            : null;

        if (snapshot == null)
        {
            snapshot = new TimelineSnapshotRecord
            {
                SnapshotId = Guid.NewGuid(),
                BranchId = branchId,
                Kind = "QuickSave",
                Name = "快速存档",
                StateFile = statePath,
            };
            manifest.Snapshots.Add(snapshot);
        }

        snapshot.Frame = frame;
        snapshot.TimestampSeconds = timestampSeconds;
        snapshot.CreatedAtUtc = DateTime.UtcNow;
        snapshot.StateFile = statePath;

        branch.QuickSaveSnapshotId = snapshot.SnapshotId;
        branch.HeadSnapshotId = snapshot.SnapshotId;
        manifest.CurrentBranchId = branchId;

        return snapshot;
    }

    public TimelineSnapshotRecord? GetQuickSaveSnapshot(TimelineManifest manifest, Guid branchId)
    {
        var branch = manifest.Branches.FirstOrDefault(item => item.BranchId == branchId);
        return branch?.QuickSaveSnapshotId is Guid snapshotId
            ? manifest.Snapshots.FirstOrDefault(item => item.SnapshotId == snapshotId)
            : null;
    }

    public void SaveBranchPoint(
        TimelineManifest manifest,
        string romId,
        BranchPoint branchPoint,
        Guid? parentBranchId,
        Func<TimelineManifest, Guid, string, bool, TimelineBranchRecord> ensureBranch)
    {
        TimelineStoragePaths.EnsureBranchDirectory(romId, branchPoint.Id);
        var branch = ensureBranch(manifest, branchPoint.Id, branchPoint.Name, false);
        branch.ParentBranchId = parentBranchId;
        branch.CreatedFromFrame = branchPoint.Frame;
        branch.CreatedAtUtc = branchPoint.CreatedAt;
        branch.Name = branchPoint.Name;

        var snapshotPath = TimelineStoragePaths.GetBranchSnapshotPath(romId, branchPoint.Id);
        var snapshotRecord = manifest.Snapshots.FirstOrDefault(item => item.BranchId == branchPoint.Id && item.Kind == "BranchPoint");
        if (snapshotRecord == null)
        {
            snapshotRecord = new TimelineSnapshotRecord
            {
                SnapshotId = Guid.NewGuid(),
                BranchId = branchPoint.Id,
                Kind = "BranchPoint",
                Name = branchPoint.Name,
                StateFile = snapshotPath,
            };
            manifest.Snapshots.Add(snapshotRecord);
        }

        snapshotRecord.Frame = branchPoint.Frame;
        snapshotRecord.TimestampSeconds = branchPoint.Timestamp;
        snapshotRecord.CreatedAtUtc = branchPoint.CreatedAt;
        snapshotRecord.Name = branchPoint.Name;
        snapshotRecord.StateFile = snapshotPath;

        branch.HeadSnapshotId = snapshotRecord.SnapshotId;
        branch.QuickSaveSnapshotId ??= snapshotRecord.SnapshotId;

        File.WriteAllBytes(
            snapshotPath,
            StateSnapshotSerializer.Serialize(
                StateSnapshotData.FromFrameSnapshot(branchPoint.Snapshot),
                includeThumbnail: true));
    }
}
