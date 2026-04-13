using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.FC.LegacyAdapters;

public sealed class LegacyTimelineManifestHandle
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

public sealed record LegacyTimelinePreviewEntry(
    Guid SnapshotId,
    Guid BranchId,
    long Frame,
    double TimestampSeconds,
    DateTime CreatedAtUtc,
    string? Name,
    CoreTimelineSnapshot Snapshot);

public sealed record LegacyTimelineLoadState(
    LegacyTimelineManifestHandle Manifest,
    Guid CurrentBranchId,
    IReadOnlyList<LegacyTimelinePreviewEntry> PreviewNodes,
    DateTime ManifestWriteTimeUtc);

public sealed class LegacyTimelineRepositoryAdapter
{
    private const string DefaultSnapshotFormat = "nes/fcrs";
    private readonly TimelineRepository _repository = new();

    public LegacyTimelineLoadState LoadTimelineState(
        CoreBranchTree branchTree,
        string romId,
        string displayName,
        string romPath)
    {
        ArgumentNullException.ThrowIfNull(branchTree);

        var manifest = new LegacyTimelineManifestHandle(_repository.LoadOrCreate(romId, displayName));
        PopulateCoreBranchTree(branchTree, manifest, romId, romPath);
        var previewNodes = LoadPreviewNodes(manifest);
        return new LegacyTimelineLoadState(
            manifest,
            manifest.CurrentBranchId,
            previewNodes,
            LegacyTimelineStorage.ReadManifestWriteTimeUtc(romId));
    }

    public LegacyTimelineLoadState? TryReloadTimelineState(
        CoreBranchTree branchTree,
        DateTime knownWriteTimeUtc,
        string romId,
        string displayName,
        string romPath)
    {
        if (LegacyTimelineStorage.ReadManifestWriteTimeUtc(romId) <= knownWriteTimeUtc)
            return null;

        return LoadTimelineState(branchTree, romId, displayName, romPath);
    }

    public IReadOnlyList<LegacyTimelinePreviewEntry> LoadPreviewNodes(LegacyTimelineManifestHandle manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return _repository.LoadPreviewNodes(manifest.Manifest)
            .Select(entry => CreatePreviewEntry(entry.Record, ToCoreTimelineSnapshot(entry.Snapshot)))
            .ToList();
    }

    public void PersistQuickSaveSnapshot(
        LegacyTimelineManifestHandle manifest,
        Guid branchId,
        long frame,
        double timestampSeconds)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        _ = _repository.UpsertQuickSaveSnapshot(manifest.Manifest, branchId, frame, timestampSeconds);
        _repository.Save(manifest.Manifest);
    }

    public void SyncCurrentSnapshotFromManifest(LegacyTimelineManifestHandle manifest, Guid currentBranchId)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        manifest.CurrentBranchId = currentBranchId;
        _ = _repository.GetQuickSaveSnapshot(manifest.Manifest, currentBranchId);
        _repository.Save(manifest.Manifest);
    }

    public void PersistBranchPoint(
        LegacyTimelineManifestHandle manifest,
        string romId,
        CoreBranchPoint branchPoint,
        Guid? parentBranchId,
        string romPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(branchPoint);

        _repository.SaveBranchPoint(
            manifest.Manifest,
            romId,
            ToLegacyBranchPoint(branchPoint, romPath),
            parentBranchId);
    }

    public void DeleteBranchPoint(
        LegacyTimelineManifestHandle manifest,
        string romId,
        Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _repository.DeleteBranch(manifest.Manifest, romId, branchId);
    }

    public void RenameBranchPoint(LegacyTimelineManifestHandle manifest, CoreBranchPoint branchPoint)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(branchPoint);

        _repository.RenameBranch(manifest.Manifest, branchPoint.Id, branchPoint.Name);
    }

    public Guid ActivateBranch(LegacyTimelineManifestHandle manifest, Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        manifest.CurrentBranchId = branchId;
        _repository.Save(manifest.Manifest);
        return branchId;
    }

    public LegacyTimelinePreviewEntry SavePreviewNode(
        LegacyTimelineManifestHandle manifest,
        string romId,
        Guid branchId,
        Guid previewNodeId,
        string name,
        CoreTimelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(snapshot);

        var legacySnapshot = ToLegacyFrameSnapshot(snapshot);
        var record = _repository.SavePreviewNode(
            manifest.Manifest,
            romId,
            branchId,
            previewNodeId,
            name,
            legacySnapshot);
        return CreatePreviewEntry(record, snapshot);
    }

    public void DeletePreviewNode(
        LegacyTimelineManifestHandle manifest,
        string romId,
        Guid previewNodeId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _repository.DeletePreviewNode(manifest.Manifest, romId, previewNodeId);
    }

    public void RenamePreviewNode(
        LegacyTimelineManifestHandle manifest,
        Guid previewNodeId,
        string title)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _repository.RenamePreviewNode(manifest.Manifest, previewNodeId, title);
    }

    public void PopulateCoreBranchTree(
        CoreBranchTree branchTree,
        LegacyTimelineManifestHandle manifest,
        string romId,
        string? romPath)
    {
        ArgumentNullException.ThrowIfNull(branchTree);
        ArgumentNullException.ThrowIfNull(manifest);

        var legacyBranchTree = new BranchTree();
        _repository.PopulateBranchTree(legacyBranchTree, manifest.Manifest, romId, romPath);
        branchTree.ReplaceRoots(legacyBranchTree.Roots.Select(ToCoreBranchPoint));
    }

    private static LegacyTimelinePreviewEntry CreatePreviewEntry(
        TimelineSnapshotRecord record,
        CoreTimelineSnapshot snapshot)
    {
        return new LegacyTimelinePreviewEntry(
            record.SnapshotId,
            record.BranchId,
            record.Frame,
            record.TimestampSeconds,
            record.CreatedAtUtc,
            record.Name,
            snapshot);
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
