using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;

namespace FCRevolution.Core.Timeline.Persistence;

internal sealed class TimelinePreviewNodeStore
{
    public TimelineSnapshotRecord SavePreviewNode(
        TimelineManifest manifest,
        string romId,
        Guid branchId,
        Guid previewNodeId,
        string name,
        FrameSnapshot snapshot)
    {
        TimelineStoragePaths.EnsurePreviewNodesDirectory(romId);
        var snapshotPath = TimelineStoragePaths.GetPreviewNodeSnapshotPath(romId, previewNodeId);
        var record = manifest.Snapshots.FirstOrDefault(item => item.SnapshotId == previewNodeId && item.Kind == "PreviewNode");
        if (record == null)
        {
            record = new TimelineSnapshotRecord
            {
                SnapshotId = previewNodeId,
                BranchId = branchId,
                Kind = "PreviewNode",
                Name = name,
                StateFile = snapshotPath,
            };
            manifest.Snapshots.Add(record);
        }

        record.BranchId = branchId;
        record.Frame = snapshot.Frame;
        record.TimestampSeconds = snapshot.Timestamp;
        record.CreatedAtUtc = DateTime.UtcNow;
        record.Name = name;
        record.StateFile = snapshotPath;

        File.WriteAllBytes(
            snapshotPath,
            StateSnapshotSerializer.Serialize(
                StateSnapshotData.FromFrameSnapshot(snapshot),
                includeThumbnail: true));
        return record;
    }

    public void DeletePreviewNode(TimelineManifest manifest, string romId, Guid previewNodeId)
    {
        var record = manifest.Snapshots.FirstOrDefault(item => item.SnapshotId == previewNodeId && item.Kind == "PreviewNode");
        if (record == null)
            return;

        manifest.Snapshots.Remove(record);
        var snapshotPath = TimelineStoragePaths.GetPreviewNodeSnapshotPath(romId, previewNodeId);
        if (File.Exists(snapshotPath))
            File.Delete(snapshotPath);
    }

    public void RenamePreviewNode(TimelineManifest manifest, Guid previewNodeId, string name)
    {
        var record = manifest.Snapshots.FirstOrDefault(item => item.SnapshotId == previewNodeId && item.Kind == "PreviewNode");
        if (record == null)
            return;

        record.Name = name;
    }

    public IReadOnlyList<(TimelineSnapshotRecord Record, FrameSnapshot Snapshot)> LoadPreviewNodes(TimelineManifest manifest)
    {
        var nodes = new List<(TimelineSnapshotRecord Record, FrameSnapshot Snapshot)>();
        foreach (var record in manifest.Snapshots
                     .Where(item => item.Kind == "PreviewNode")
                     .OrderBy(item => item.Frame)
                     .ThenBy(item => item.CreatedAtUtc))
        {
            if (!File.Exists(record.StateFile))
                continue;

            var snapshot = StateSnapshotSerializer.Deserialize(File.ReadAllBytes(record.StateFile)).ToFrameSnapshot();
            nodes.Add((record, snapshot));
        }

        return nodes;
    }
}
