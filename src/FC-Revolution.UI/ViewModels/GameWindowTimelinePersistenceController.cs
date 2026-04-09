using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Core.Timeline;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowPersistPreviewNodePlan(
    bool ShouldPersist,
    CoreTimelineSnapshot? Snapshot,
    Guid BranchId,
    Guid PreviewNodeId,
    string Title);

internal readonly record struct GameWindowPersistPreviewNodeResult(
    BranchPreviewNode PreviewNode,
    DateTime ManifestWriteTimeUtc);

internal readonly record struct GameWindowTimelineReloadState(
    TimelineManifest Manifest,
    Guid CurrentBranchId,
    IReadOnlyList<BranchPreviewNode> PreviewNodes,
    DateTime ManifestWriteTimeUtc);

internal static class GameWindowTimelinePersistenceController
{
    public static GameWindowTimelineReloadState LoadTimelineState(
        TimelineRepository timelineRepository,
        CoreBranchTree branchTree,
        string romId,
        string displayName,
        string romPath,
        int previewWidth,
        int previewHeight)
    {
        var manifest = timelineRepository.LoadOrCreate(romId, displayName);
        PopulateCoreBranchTree(timelineRepository, branchTree, manifest, romId, romPath);
        var previewNodes = LoadPreviewNodes(timelineRepository, manifest, previewWidth, previewHeight);
        return new GameWindowTimelineReloadState(
            manifest,
            manifest.CurrentBranchId,
            previewNodes,
            ReadManifestWriteTimeUtc(romId));
    }

    public static IReadOnlyList<BranchPreviewNode> LoadPreviewNodes(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        int previewWidth,
        int previewHeight)
    {
        var previewEntries = timelineRepository.LoadPreviewNodes(timelineManifest);
        return BuildPreviewNodes(previewEntries, previewWidth, previewHeight);
    }

    public static IReadOnlyList<BranchPreviewNode> BuildPreviewNodes(
        IReadOnlyList<(TimelineSnapshotRecord Record, FrameSnapshot Snapshot)> previewEntries,
        int previewWidth,
        int previewHeight)
    {
        return previewEntries
            .Select(entry => GameWindowPreviewNodeFactory.Create(
                entry.Record,
                CoreTimelineModelBridge.ToCoreTimelineSnapshot(entry.Snapshot),
                previewWidth,
                previewHeight))
            .ToList();
    }

    public static void PersistBranchPoint(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        string romId,
        CoreBranchPoint branchPoint,
        Guid? parentBranchId,
        string romPath)
    {
        timelineRepository.SaveBranchPoint(
            timelineManifest,
            romId,
            CoreTimelineModelBridge.ToLegacyBranchPoint(branchPoint, romPath),
            parentBranchId);
    }

    public static void DeleteBranchPoint(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        string romId,
        Guid branchId)
    {
        timelineRepository.DeleteBranch(timelineManifest, romId, branchId);
    }

    public static void RenameBranchPoint(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        CoreBranchPoint branchPoint)
    {
        timelineRepository.RenameBranch(timelineManifest, branchPoint.Id, branchPoint.Name);
    }

    public static Guid ActivateBranch(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        Guid branchId)
    {
        timelineManifest.CurrentBranchId = branchId;
        timelineRepository.Save(timelineManifest);
        return branchId;
    }

    public static GameWindowPersistPreviewNodePlan BuildPersistPreviewNodePlan(
        Guid currentBranchId,
        Guid? branchPointId,
        string title,
        long frame,
        CoreTimelineSnapshot? branchPointSnapshot,
        Func<long, CoreTimelineSnapshot?> getNearestSnapshot)
    {
        var snapshot = branchPointSnapshot ?? getNearestSnapshot(frame);
        if (snapshot is null)
            return new GameWindowPersistPreviewNodePlan(
                ShouldPersist: false,
                Snapshot: null,
                BranchId: Guid.Empty,
                PreviewNodeId: Guid.Empty,
                Title: string.Empty);

        return new GameWindowPersistPreviewNodePlan(
            ShouldPersist: true,
            Snapshot: snapshot,
            BranchId: branchPointId ?? currentBranchId,
            PreviewNodeId: Guid.NewGuid(),
            Title: title);
    }

    public static GameWindowPersistPreviewNodeResult? TryPersistPreviewNode(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        string romId,
        Guid currentBranchId,
        BranchCanvasNode node,
        Func<long, CoreTimelineSnapshot?> getNearestSnapshot,
        int previewWidth,
        int previewHeight)
    {
        var plan = BuildPersistPreviewNodePlan(
            currentBranchId,
            node.BranchPoint?.Id,
            node.Title,
            node.Frame,
            node.BranchPoint?.Snapshot,
            getNearestSnapshot);
        if (!plan.ShouldPersist || plan.Snapshot is null)
            return null;

        var legacySnapshot = CoreTimelineModelBridge.ToLegacyFrameSnapshot(plan.Snapshot);
        var record = timelineRepository.SavePreviewNode(
            timelineManifest,
            romId,
            plan.BranchId,
            plan.PreviewNodeId,
            plan.Title,
            legacySnapshot);
        var previewNode = GameWindowPreviewNodeFactory.Create(record, plan.Snapshot, previewWidth, previewHeight);
        return new GameWindowPersistPreviewNodeResult(
            previewNode,
            ReadManifestWriteTimeUtc(romId));
    }

    public static DateTime DeletePreviewNode(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        string romId,
        Guid previewNodeId)
    {
        timelineRepository.DeletePreviewNode(timelineManifest, romId, previewNodeId);
        return ReadManifestWriteTimeUtc(romId);
    }

    public static DateTime RenamePreviewNode(
        TimelineRepository timelineRepository,
        TimelineManifest timelineManifest,
        string romId,
        Guid previewNodeId,
        string title)
    {
        timelineRepository.RenamePreviewNode(timelineManifest, previewNodeId, title);
        return ReadManifestWriteTimeUtc(romId);
    }

    public static GameWindowTimelineManifestSyncResult BuildManifestSyncResult(
        DateTime knownWriteTimeUtc,
        string romId)
    {
        return GameWindowTimelineManifestSyncController.BuildSyncResult(
            knownWriteTimeUtc,
            ReadManifestWriteTimeUtc(romId));
    }

    public static GameWindowTimelineReloadState? TryReloadTimelineState(
        TimelineRepository timelineRepository,
        CoreBranchTree branchTree,
        DateTime knownWriteTimeUtc,
        string romId,
        string displayName,
        string romPath,
        int previewWidth,
        int previewHeight)
    {
        var syncResult = BuildManifestSyncResult(knownWriteTimeUtc, romId);
        if (!syncResult.ShouldSyncManifest)
            return null;

        return LoadTimelineState(
            timelineRepository,
            branchTree,
            romId,
            displayName,
            romPath,
            previewWidth,
            previewHeight);
    }

    public static void PopulateCoreBranchTree(
        TimelineRepository timelineRepository,
        CoreBranchTree branchTree,
        TimelineManifest timelineManifest,
        string romId,
        string? romPath)
    {
        ArgumentNullException.ThrowIfNull(timelineRepository);
        ArgumentNullException.ThrowIfNull(branchTree);
        ArgumentNullException.ThrowIfNull(timelineManifest);

        var legacyBranchTree = new BranchTree();
        timelineRepository.PopulateBranchTree(legacyBranchTree, timelineManifest, romId, romPath);
        branchTree.ReplaceRoots(legacyBranchTree.Roots.Select(CoreTimelineModelBridge.ToCoreBranchPoint));
    }

    public static DateTime ReadManifestWriteTimeUtc(string romId)
    {
        var manifestPath = TimelineStoragePaths.GetManifestPath(romId);
        return File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath) : DateTime.MinValue;
    }
}
