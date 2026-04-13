namespace FCRevolution.Emulation.Abstractions;

public interface ITimelineManifestHandle
{
    string RomId { get; }

    string RomDisplayName { get; }

    int Version { get; }

    Guid CurrentBranchId { get; set; }
}

public sealed record TimelinePreviewEntry(
    Guid SnapshotId,
    Guid BranchId,
    long Frame,
    double TimestampSeconds,
    DateTime CreatedAtUtc,
    string? Name,
    CoreTimelineSnapshot Snapshot);

public sealed record TimelineLoadState(
    ITimelineManifestHandle Manifest,
    Guid CurrentBranchId,
    IReadOnlyList<TimelinePreviewEntry> PreviewNodes,
    DateTime ManifestWriteTimeUtc);

public interface ITimelineRepositoryBridge
{
    TimelineLoadState LoadTimelineState(
        CoreBranchTree branchTree,
        string romId,
        string displayName,
        string romPath);

    TimelineLoadState? TryReloadTimelineState(
        CoreBranchTree branchTree,
        DateTime knownWriteTimeUtc,
        string romId,
        string displayName,
        string romPath);

    IReadOnlyList<TimelinePreviewEntry> LoadPreviewNodes(ITimelineManifestHandle manifest);

    void PersistQuickSaveSnapshot(
        ITimelineManifestHandle manifest,
        Guid branchId,
        long frame,
        double timestampSeconds);

    void SyncCurrentSnapshotFromManifest(ITimelineManifestHandle manifest, Guid currentBranchId);

    void PersistBranchPoint(
        ITimelineManifestHandle manifest,
        string romId,
        CoreBranchPoint branchPoint,
        Guid? parentBranchId,
        string romPath);

    void DeleteBranchPoint(
        ITimelineManifestHandle manifest,
        string romId,
        Guid branchId);

    void RenameBranchPoint(ITimelineManifestHandle manifest, CoreBranchPoint branchPoint);

    Guid ActivateBranch(ITimelineManifestHandle manifest, Guid branchId);

    TimelinePreviewEntry SavePreviewNode(
        ITimelineManifestHandle manifest,
        string romId,
        Guid branchId,
        Guid previewNodeId,
        string name,
        CoreTimelineSnapshot snapshot);

    void DeletePreviewNode(
        ITimelineManifestHandle manifest,
        string romId,
        Guid previewNodeId);

    void RenamePreviewNode(
        ITimelineManifestHandle manifest,
        Guid previewNodeId,
        string title);

    void PopulateCoreBranchTree(
        CoreBranchTree branchTree,
        ITimelineManifestHandle manifest,
        string romId,
        string? romPath);
}
