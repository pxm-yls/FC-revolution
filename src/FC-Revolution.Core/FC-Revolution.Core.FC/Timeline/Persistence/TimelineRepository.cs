using FCRevolution.Core.Timeline;

namespace FCRevolution.Core.Timeline.Persistence;

public sealed class TimelineRepository
{
    private readonly TimelineManifestStore _manifestStore = new();
    private readonly TimelineBranchStore _branchStore = new();
    private readonly TimelineSnapshotStore _snapshotStore = new();
    private readonly TimelinePreviewNodeStore _previewNodeStore = new();
    private readonly TimelineBranchTreeLoader _branchTreeLoader = new();

    public TimelineManifest LoadOrCreate(string romId, string romDisplayName)
    {
        var manifest = _manifestStore.Load(romId);

        manifest.RomId = romId;
        manifest.RomDisplayName = romDisplayName;
        manifest.Version = Math.Max(1, manifest.Version);

        var mainBranchId = TimelineStoragePaths.GetStableMainBranchId(romId);
        _branchStore.EnsureBranch(manifest, mainBranchId, "主线", isMainBranch: true);

        if (manifest.CurrentBranchId == Guid.Empty || manifest.Branches.All(b => b.BranchId != manifest.CurrentBranchId))
            manifest.CurrentBranchId = mainBranchId;

        Save(manifest);
        return manifest;
    }

    public void Save(TimelineManifest manifest)
        => _manifestStore.Save(manifest);

    public TimelineBranchRecord EnsureBranch(TimelineManifest manifest, Guid branchId, string name, bool isMainBranch = false)
        => _branchStore.EnsureBranch(manifest, branchId, name, isMainBranch);

    public TimelineSnapshotRecord UpsertQuickSaveSnapshot(
        TimelineManifest manifest,
        Guid branchId,
        long frame,
        double timestampSeconds)
        => _snapshotStore.UpsertQuickSaveSnapshot(
            manifest,
            branchId,
            frame,
            timestampSeconds,
            (m, id, name, isMainBranch) => _branchStore.EnsureBranch(m, id, name, isMainBranch));

    public TimelineSnapshotRecord? GetQuickSaveSnapshot(TimelineManifest manifest, Guid branchId)
        => _snapshotStore.GetQuickSaveSnapshot(manifest, branchId);

    public void SaveBranchPoint(TimelineManifest manifest, string romId, BranchPoint branchPoint, Guid? parentBranchId)
    {
        _snapshotStore.SaveBranchPoint(
            manifest,
            romId,
            branchPoint,
            parentBranchId,
            (m, id, name, isMainBranch) => _branchStore.EnsureBranch(m, id, name, isMainBranch));
        Save(manifest);
    }

    public TimelineSnapshotRecord SavePreviewNode(
        TimelineManifest manifest,
        string romId,
        Guid branchId,
        Guid previewNodeId,
        string name,
        FrameSnapshot snapshot)
    {
        var record = _previewNodeStore.SavePreviewNode(manifest, romId, branchId, previewNodeId, name, snapshot);
        Save(manifest);
        return record;
    }

    public void DeletePreviewNode(TimelineManifest manifest, string romId, Guid previewNodeId)
    {
        _previewNodeStore.DeletePreviewNode(manifest, romId, previewNodeId);
        Save(manifest);
    }

    public void RenamePreviewNode(TimelineManifest manifest, Guid previewNodeId, string name)
    {
        _previewNodeStore.RenamePreviewNode(manifest, previewNodeId, name);
        Save(manifest);
    }

    public IReadOnlyList<(TimelineSnapshotRecord Record, FrameSnapshot Snapshot)> LoadPreviewNodes(TimelineManifest manifest)
        => _previewNodeStore.LoadPreviewNodes(manifest);

    public void RenameBranch(TimelineManifest manifest, Guid branchId, string name)
    {
        _branchStore.RenameBranch(manifest, branchId, name);
        Save(manifest);
    }

    public void DeleteBranch(TimelineManifest manifest, string romId, Guid branchId)
    {
        _branchStore.DeleteBranch(manifest, romId, branchId);
        Save(manifest);
    }

    public void PopulateBranchTree(BranchTree tree, TimelineManifest manifest, string romId, string? romPath = null)
        => _branchTreeLoader.PopulateBranchTree(tree, manifest, romId, romPath);
}
