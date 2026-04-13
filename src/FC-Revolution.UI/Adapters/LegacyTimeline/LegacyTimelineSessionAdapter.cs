using System;
using System.Collections.Generic;
using FCRevolution.FC.LegacyAdapters;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Adapters.LegacyTimeline;

internal readonly record struct LegacyTimelineSessionLoadState(
    IReadOnlyList<BranchPreviewNode> PreviewNodes);

internal readonly record struct LegacyTimelineSessionReloadState(
    IReadOnlyList<BranchPreviewNode> PreviewNodes);

internal sealed class LegacyTimelineSessionAdapter
{
    private readonly ITimelineRepositoryBridge _timelineRepository;
    private readonly CoreBranchTree _branchTree;
    private ITimelineManifestHandle? _timelineManifest;
    private string? _displayName;
    private string? _romPath;
    private DateTime _manifestWriteTimeUtc;

    public LegacyTimelineSessionAdapter(CoreBranchTree branchTree)
        : this(branchTree, new LegacyTimelineRepositoryAdapter())
    {
    }

    internal LegacyTimelineSessionAdapter(CoreBranchTree branchTree, ITimelineRepositoryBridge timelineRepository)
    {
        _branchTree = branchTree;
        _timelineRepository = timelineRepository;
    }

    public string? RomId { get; private set; }

    public Guid CurrentBranchId { get; private set; }

    public bool IsTimelineLoaded => _timelineManifest != null && RomId != null;

    public LegacyTimelineSessionLoadState Initialize(
        string romPath,
        string displayName,
        bool loadFullTimeline,
        int previewWidth,
        int previewHeight)
    {
        _displayName = displayName;
        _romPath = romPath;
        RomId = LegacyTimelineStorageAdapter.ComputeRomId(romPath);
        CurrentBranchId = LegacyTimelineStorageAdapter.GetStableMainBranchId(RomId);
        _timelineManifest = null;
        _branchTree.Clear();
        _manifestWriteTimeUtc = DateTime.MinValue;

        if (!loadFullTimeline)
            return new LegacyTimelineSessionLoadState(Array.Empty<BranchPreviewNode>());

        var loadState = GameWindowTimelinePersistenceController.LoadTimelineState(
            _timelineRepository,
            _branchTree,
            RomId,
            displayName,
            previewWidth,
            previewHeight,
            romPath);
        _timelineManifest = loadState.Manifest;
        CurrentBranchId = loadState.CurrentBranchId;
        _manifestWriteTimeUtc = loadState.ManifestWriteTimeUtc;
        return new LegacyTimelineSessionLoadState(loadState.PreviewNodes);
    }

    public string GetQuickSavePath()
    {
        if (RomId == null)
            return "quicksave.fcs";

        LegacyTimelineStorageAdapter.EnsureBranchDirectory(RomId, CurrentBranchId);
        return LegacyTimelineStorageAdapter.GetQuickSavePath(RomId, CurrentBranchId);
    }

    public string? GetInputLogPath()
    {
        if (RomId == null)
            return null;

        LegacyTimelineStorageAdapter.EnsureBranchDirectory(RomId, CurrentBranchId);
        return LegacyTimelineStorageAdapter.GetInputLogPath(RomId, CurrentBranchId);
    }

    public void PersistQuickSaveSnapshot(long frame, double timestampSeconds)
    {
        if (!TryRequireTimeline(out var timelineManifest))
            return;

        _timelineRepository.PersistQuickSaveSnapshot(
            timelineManifest,
            CurrentBranchId,
            frame,
            timestampSeconds);
        UpdateManifestWriteTimeUtc();
    }

    public void SyncCurrentSnapshotFromManifest()
    {
        if (!TryRequireTimeline(out var timelineManifest))
            return;

        _timelineRepository.SyncCurrentSnapshotFromManifest(timelineManifest, CurrentBranchId);
        UpdateManifestWriteTimeUtc();
    }

    public void PersistBranchPoint(CoreBranchPoint branchPoint, Guid? parentBranchId)
    {
        if (!TryRequireTimeline(out var timelineManifest, out var romId))
            return;

        GameWindowTimelinePersistenceController.PersistBranchPoint(
            _timelineRepository,
            timelineManifest,
            romId,
            branchPoint,
            parentBranchId,
            _romPath ?? branchPoint.RomPath);
        UpdateManifestWriteTimeUtc();
    }

    public void DeleteBranchPoint(Guid branchId)
    {
        if (!TryRequireTimeline(out var timelineManifest, out var romId))
            return;

        GameWindowTimelinePersistenceController.DeleteBranchPoint(
            _timelineRepository,
            timelineManifest,
            romId,
            branchId);
        CurrentBranchId = timelineManifest.CurrentBranchId;
        UpdateManifestWriteTimeUtc();
    }

    public void RenameBranchPoint(CoreBranchPoint branchPoint)
    {
        if (!TryRequireTimeline(out var timelineManifest))
            return;

        GameWindowTimelinePersistenceController.RenameBranchPoint(
            _timelineRepository,
            timelineManifest,
            branchPoint);
        UpdateManifestWriteTimeUtc();
    }

    public void ActivateBranch(Guid branchId)
    {
        if (!TryRequireTimeline(out var timelineManifest))
            return;

        CurrentBranchId = GameWindowTimelinePersistenceController.ActivateBranch(
            _timelineRepository,
            timelineManifest,
            branchId);
        UpdateManifestWriteTimeUtc();
    }

    public IReadOnlyList<BranchPreviewNode> LoadPreviewNodes(int previewWidth, int previewHeight)
    {
        if (!TryRequireTimeline(out var timelineManifest))
            return Array.Empty<BranchPreviewNode>();

        var previewNodes = GameWindowTimelinePersistenceController.LoadPreviewNodes(
            _timelineRepository,
            timelineManifest,
            previewWidth,
            previewHeight);
        UpdateManifestWriteTimeUtc();
        return previewNodes;
    }

    public BranchPreviewNode? PersistPreviewNode(
        BranchCanvasNode node,
        Func<long, CoreTimelineSnapshot?> getNearestSnapshot,
        int previewWidth,
        int previewHeight)
    {
        if (!TryRequireTimeline(out var timelineManifest, out var romId))
            return null;

        var persistResult = GameWindowTimelinePersistenceController.TryPersistPreviewNode(
            _timelineRepository,
            timelineManifest,
            romId,
            CurrentBranchId,
            node,
            getNearestSnapshot,
            previewWidth,
            previewHeight);
        if (persistResult is not null)
            _manifestWriteTimeUtc = persistResult.Value.ManifestWriteTimeUtc;
        return persistResult?.PreviewNode;
    }

    public void DeletePreviewNode(Guid previewNodeId)
    {
        if (!TryRequireTimeline(out var timelineManifest, out var romId))
            return;

        _ = GameWindowTimelinePersistenceController.DeletePreviewNode(
            _timelineRepository,
            timelineManifest,
            romId,
            previewNodeId);
        UpdateManifestWriteTimeUtc();
    }

    public void RenamePreviewNode(Guid previewNodeId, string title)
    {
        if (!TryRequireTimeline(out var timelineManifest, out var romId))
            return;

        _ = GameWindowTimelinePersistenceController.RenamePreviewNode(
            _timelineRepository,
            timelineManifest,
            romId,
            previewNodeId,
            title);
        UpdateManifestWriteTimeUtc();
    }

    public LegacyTimelineSessionReloadState? TryReload(int previewWidth, int previewHeight)
    {
        if (!TryRequireTimeline(out _, out var romId) ||
            string.IsNullOrWhiteSpace(_displayName) ||
            string.IsNullOrWhiteSpace(_romPath))
        {
            return null;
        }

        var reloadState = GameWindowTimelinePersistenceController.TryReloadTimelineState(
            _timelineRepository,
            _branchTree,
            _manifestWriteTimeUtc,
            romId,
            _displayName,
            _romPath,
            previewWidth,
            previewHeight);
        if (reloadState is null)
            return null;

        _timelineManifest = reloadState.Value.Manifest;
        CurrentBranchId = reloadState.Value.CurrentBranchId;
        _manifestWriteTimeUtc = reloadState.Value.ManifestWriteTimeUtc;
        return new LegacyTimelineSessionReloadState(reloadState.Value.PreviewNodes);
    }

    private bool TryRequireTimeline(out ITimelineManifestHandle timelineManifest)
    {
        if (_timelineManifest is not null)
        {
            timelineManifest = _timelineManifest;
            return true;
        }

        timelineManifest = null!;
        return false;
    }

    private bool TryRequireTimeline(out ITimelineManifestHandle timelineManifest, out string romId)
    {
        if (_timelineManifest is not null && RomId is not null)
        {
            timelineManifest = _timelineManifest;
            romId = RomId;
            return true;
        }

        timelineManifest = null!;
        romId = string.Empty;
        return false;
    }

    private void UpdateManifestWriteTimeUtc()
    {
        _manifestWriteTimeUtc = RomId is null
            ? DateTime.MinValue
            : LegacyTimelineStorageAdapter.ReadManifestWriteTimeUtc(RomId);
    }
}
