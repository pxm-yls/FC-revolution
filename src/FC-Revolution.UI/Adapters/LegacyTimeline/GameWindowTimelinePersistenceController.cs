using System;
using System.Collections.Generic;
using System.Linq;
using FCRevolution.FC.LegacyAdapters;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Adapters.LegacyTimeline;

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
    LegacyTimelineManifestHandle Manifest,
    Guid CurrentBranchId,
    IReadOnlyList<BranchPreviewNode> PreviewNodes,
    DateTime ManifestWriteTimeUtc);

internal static class GameWindowTimelinePersistenceController
{
    public static GameWindowTimelineReloadState LoadTimelineState(
        LegacyTimelineRepositoryAdapter timelineRepository,
        CoreBranchTree branchTree,
        string romId,
        string displayName,
        int previewWidth,
        int previewHeight,
        string romPath)
    {
        var loadState = timelineRepository.LoadTimelineState(branchTree, romId, displayName, romPath);
        return new GameWindowTimelineReloadState(
            loadState.Manifest,
            loadState.CurrentBranchId,
            BuildPreviewNodes(loadState.PreviewNodes, previewWidth, previewHeight),
            loadState.ManifestWriteTimeUtc);
    }

    public static IReadOnlyList<BranchPreviewNode> LoadPreviewNodes(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
        int previewWidth,
        int previewHeight)
    {
        var previewEntries = timelineRepository.LoadPreviewNodes(timelineManifest);
        return BuildPreviewNodes(previewEntries, previewWidth, previewHeight);
    }

    public static IReadOnlyList<BranchPreviewNode> BuildPreviewNodes(
        IReadOnlyList<LegacyTimelinePreviewEntry> previewEntries,
        int previewWidth,
        int previewHeight)
    {
        return previewEntries
            .Select(entry => GameWindowPreviewNodeFactory.Create(entry, previewWidth, previewHeight))
            .ToList();
    }

    public static void PersistBranchPoint(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
        string romId,
        CoreBranchPoint branchPoint,
        Guid? parentBranchId,
        string romPath)
    {
        timelineRepository.PersistBranchPoint(
            timelineManifest,
            romId,
            branchPoint,
            parentBranchId,
            romPath);
    }

    public static void DeleteBranchPoint(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
        string romId,
        Guid branchId)
    {
        timelineRepository.DeleteBranchPoint(timelineManifest, romId, branchId);
    }

    public static void RenameBranchPoint(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
        CoreBranchPoint branchPoint)
    {
        timelineRepository.RenameBranchPoint(timelineManifest, branchPoint);
    }

    public static Guid ActivateBranch(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
        Guid branchId)
    {
        return timelineRepository.ActivateBranch(timelineManifest, branchId);
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
        {
            return new GameWindowPersistPreviewNodePlan(
                ShouldPersist: false,
                Snapshot: null,
                BranchId: Guid.Empty,
                PreviewNodeId: Guid.Empty,
                Title: string.Empty);
        }

        return new GameWindowPersistPreviewNodePlan(
            ShouldPersist: true,
            Snapshot: snapshot,
            BranchId: branchPointId ?? currentBranchId,
            PreviewNodeId: Guid.NewGuid(),
            Title: title);
    }

    public static GameWindowPersistPreviewNodeResult? TryPersistPreviewNode(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
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

        var previewEntry = timelineRepository.SavePreviewNode(
            timelineManifest,
            romId,
            plan.BranchId,
            plan.PreviewNodeId,
            plan.Title,
            plan.Snapshot);
        var previewNode = GameWindowPreviewNodeFactory.Create(previewEntry, previewWidth, previewHeight);
        return new GameWindowPersistPreviewNodeResult(
            previewNode,
            ReadManifestWriteTimeUtc(romId));
    }

    public static DateTime DeletePreviewNode(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
        string romId,
        Guid previewNodeId)
    {
        timelineRepository.DeletePreviewNode(timelineManifest, romId, previewNodeId);
        return ReadManifestWriteTimeUtc(romId);
    }

    public static DateTime RenamePreviewNode(
        LegacyTimelineRepositoryAdapter timelineRepository,
        LegacyTimelineManifestHandle timelineManifest,
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
        LegacyTimelineRepositoryAdapter timelineRepository,
        CoreBranchTree branchTree,
        DateTime knownWriteTimeUtc,
        string romId,
        string displayName,
        string romPath,
        int previewWidth,
        int previewHeight)
    {
        var loadState = timelineRepository.TryReloadTimelineState(
            branchTree,
            knownWriteTimeUtc,
            romId,
            displayName,
            romPath);
        if (loadState is null)
            return null;

        return new GameWindowTimelineReloadState(
            loadState.Manifest,
            loadState.CurrentBranchId,
            BuildPreviewNodes(loadState.PreviewNodes, previewWidth, previewHeight),
            loadState.ManifestWriteTimeUtc);
    }

    public static DateTime ReadManifestWriteTimeUtc(string romId)
    {
        return LegacyTimelineStorageAdapter.ReadManifestWriteTimeUtc(romId);
    }
}
