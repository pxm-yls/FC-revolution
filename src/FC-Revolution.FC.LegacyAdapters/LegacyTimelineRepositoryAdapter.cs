using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Abstractions;
using StorageTimelineStoragePaths = FCRevolution.Storage.TimelineStoragePaths;

namespace FCRevolution.FC.LegacyAdapters;

public sealed class LegacyTimelineManifestHandle : ITimelineManifestHandle
{
    internal LegacyTimelineManifestHandle(TimelineManifest manifest)
    {
        Manifest = manifest;
    }

    internal TimelineManifest Manifest { get; }

    public string RomId => Manifest.RomId;

    public string RomDisplayName => Manifest.RomDisplayName;

    public int Version => Manifest.Version;

    public Guid CurrentBranchId
    {
        get => Manifest.CurrentBranchId;
        set => Manifest.CurrentBranchId = value;
    }
}

public sealed class LegacyTimelineRepositoryAdapter : ITimelineRepositoryBridge
{
    private const string DefaultSnapshotFormat = "nes/fcrs";
    private readonly TimelineRepository _repository = new();

    public TimelineLoadState LoadTimelineState(
        CoreBranchTree branchTree,
        string romId,
        string displayName,
        string romPath)
    {
        ArgumentNullException.ThrowIfNull(branchTree);

        var manifest = new LegacyTimelineManifestHandle(_repository.LoadOrCreate(romId, displayName));
        PopulateCoreBranchTree(branchTree, manifest, romId, romPath);
        var previewNodes = LoadPreviewNodes(manifest);
        return new TimelineLoadState(
            manifest,
            manifest.CurrentBranchId,
            previewNodes,
            StorageTimelineStoragePaths.ReadManifestWriteTimeUtc(romId));
    }

    public TimelineLoadState? TryReloadTimelineState(
        CoreBranchTree branchTree,
        DateTime knownWriteTimeUtc,
        string romId,
        string displayName,
        string romPath)
    {
        if (StorageTimelineStoragePaths.ReadManifestWriteTimeUtc(romId) <= knownWriteTimeUtc)
            return null;

        return LoadTimelineState(branchTree, romId, displayName, romPath);
    }

    public IReadOnlyList<TimelinePreviewEntry> LoadPreviewNodes(ITimelineManifestHandle manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var legacyManifest = RequireManifestHandle(manifest);

        return _repository.LoadPreviewNodes(legacyManifest.Manifest)
            .Select(entry => CreatePreviewEntry(entry.Record, ToCoreTimelineSnapshot(entry.Snapshot)))
            .ToList();
    }

    public void PersistQuickSaveSnapshot(
        ITimelineManifestHandle manifest,
        Guid branchId,
        long frame,
        double timestampSeconds)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var legacyManifest = RequireManifestHandle(manifest);

        _ = _repository.UpsertQuickSaveSnapshot(legacyManifest.Manifest, branchId, frame, timestampSeconds);
        _repository.Save(legacyManifest.Manifest);
    }

    public void SyncCurrentSnapshotFromManifest(ITimelineManifestHandle manifest, Guid currentBranchId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var legacyManifest = RequireManifestHandle(manifest);

        legacyManifest.CurrentBranchId = currentBranchId;
        _ = _repository.GetQuickSaveSnapshot(legacyManifest.Manifest, currentBranchId);
        _repository.Save(legacyManifest.Manifest);
    }

    public void PersistBranchPoint(
        ITimelineManifestHandle manifest,
        string romId,
        CoreBranchPoint branchPoint,
        Guid? parentBranchId,
        string romPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(branchPoint);
        var legacyManifest = RequireManifestHandle(manifest);

        _repository.SaveBranchPoint(
            legacyManifest.Manifest,
            romId,
            ToLegacyBranchPoint(branchPoint, romPath),
            parentBranchId);
    }

    public void DeleteBranchPoint(
        ITimelineManifestHandle manifest,
        string romId,
        Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _repository.DeleteBranch(RequireManifestHandle(manifest).Manifest, romId, branchId);
    }

    public void RenameBranchPoint(ITimelineManifestHandle manifest, CoreBranchPoint branchPoint)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(branchPoint);

        _repository.RenameBranch(RequireManifestHandle(manifest).Manifest, branchPoint.Id, branchPoint.Name);
    }

    public Guid ActivateBranch(ITimelineManifestHandle manifest, Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var legacyManifest = RequireManifestHandle(manifest);

        legacyManifest.CurrentBranchId = branchId;
        _repository.Save(legacyManifest.Manifest);
        return branchId;
    }

    public TimelinePreviewEntry SavePreviewNode(
        ITimelineManifestHandle manifest,
        string romId,
        Guid branchId,
        Guid previewNodeId,
        string name,
        CoreTimelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(snapshot);
        var legacyManifest = RequireManifestHandle(manifest);

        var legacySnapshot = ToLegacyFrameSnapshot(snapshot);
        var record = _repository.SavePreviewNode(
            legacyManifest.Manifest,
            romId,
            branchId,
            previewNodeId,
            name,
            legacySnapshot);
        return CreatePreviewEntry(record, snapshot);
    }

    public void DeletePreviewNode(
        ITimelineManifestHandle manifest,
        string romId,
        Guid previewNodeId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _repository.DeletePreviewNode(RequireManifestHandle(manifest).Manifest, romId, previewNodeId);
    }

    public void RenamePreviewNode(
        ITimelineManifestHandle manifest,
        Guid previewNodeId,
        string title)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _repository.RenamePreviewNode(RequireManifestHandle(manifest).Manifest, previewNodeId, title);
    }

    public void PopulateCoreBranchTree(
        CoreBranchTree branchTree,
        ITimelineManifestHandle manifest,
        string romId,
        string? romPath)
    {
        ArgumentNullException.ThrowIfNull(branchTree);
        ArgumentNullException.ThrowIfNull(manifest);
        var legacyManifest = RequireManifestHandle(manifest);

        var legacyBranchTree = new BranchTree();
        _repository.PopulateBranchTree(legacyBranchTree, legacyManifest.Manifest, romId, romPath);
        branchTree.ReplaceRoots(legacyBranchTree.Roots.Select(ToCoreBranchPoint));
    }

    private static TimelinePreviewEntry CreatePreviewEntry(
        TimelineSnapshotRecord record,
        CoreTimelineSnapshot snapshot)
    {
        return new TimelinePreviewEntry(
            record.SnapshotId,
            record.BranchId,
            record.Frame,
            record.TimestampSeconds,
            record.CreatedAtUtc,
            record.Name,
            snapshot);
    }

    private static LegacyTimelineManifestHandle RequireManifestHandle(ITimelineManifestHandle manifest)
    {
        if (manifest is LegacyTimelineManifestHandle legacyManifest)
            return legacyManifest;

        throw new InvalidOperationException($"Unsupported timeline manifest handle type: {manifest.GetType().FullName}");
    }

    private static CoreTimelineSnapshot ToCoreTimelineSnapshot(FrameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new CoreTimelineSnapshot
        {
            Frame = snapshot.Frame,
            TimestampSeconds = snapshot.Timestamp,
            Thumbnail = snapshot.Thumbnail,
            State = new CoreStateBlob
            {
                Format = DefaultSnapshotFormat,
                Data = StateSnapshotSerializer.Serialize(
                    StateSnapshotData.FromFrameSnapshot(snapshot),
                    includeThumbnail: true)
            }
        };
    }

    private static FrameSnapshot ToLegacyFrameSnapshot(CoreTimelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.State.Data is { Length: > 0 } payload)
        {
            var parsed = StateSnapshotSerializer.Deserialize(payload).ToFrameSnapshot(thumbnailOverride: snapshot.Thumbnail);
            return new FrameSnapshot
            {
                Frame = parsed.Frame,
                Timestamp = parsed.Timestamp,
                CpuState = parsed.CpuState,
                PpuState = parsed.PpuState,
                RamState = parsed.RamState,
                CartState = parsed.CartState,
                ApuState = parsed.ApuState,
                Thumbnail = parsed.Thumbnail,
            };
        }

        return new FrameSnapshot
        {
            Frame = snapshot.Frame,
            Timestamp = snapshot.TimestampSeconds,
            Thumbnail = snapshot.Thumbnail,
        };
    }

    private static CoreBranchPoint ToCoreBranchPoint(BranchPoint branchPoint)
    {
        ArgumentNullException.ThrowIfNull(branchPoint);

        var coreBranchPoint = new CoreBranchPoint
        {
            Id = branchPoint.Id,
            Name = branchPoint.Name,
            RomPath = branchPoint.RomPath,
            Frame = branchPoint.Frame,
            TimestampSeconds = branchPoint.Timestamp,
            Snapshot = ToCoreTimelineSnapshot(branchPoint.Snapshot),
            CreatedAt = branchPoint.CreatedAt
        };

        foreach (var child in branchPoint.Children)
            coreBranchPoint.Children.Add(ToCoreBranchPoint(child));

        return coreBranchPoint;
    }

    private static BranchPoint ToLegacyBranchPoint(CoreBranchPoint branchPoint, string? romPath)
    {
        ArgumentNullException.ThrowIfNull(branchPoint);

        var legacyBranchPoint = new BranchPoint
        {
            Id = branchPoint.Id,
            Name = branchPoint.Name,
            RomPath = string.IsNullOrWhiteSpace(branchPoint.RomPath) ? romPath ?? string.Empty : branchPoint.RomPath,
            Frame = branchPoint.Frame,
            Timestamp = branchPoint.TimestampSeconds,
            Snapshot = ToLegacyFrameSnapshot(branchPoint.Snapshot),
            CreatedAt = branchPoint.CreatedAt,
        };

        foreach (var child in branchPoint.Children)
            legacyBranchPoint.Children.Add(ToLegacyBranchPoint(child, child.RomPath));

        return legacyBranchPoint;
    }
}
