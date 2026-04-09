using System;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct MainWindowBranchExportPlan(
    Guid BranchId,
    string RomPath,
    string InputLogPath,
    string OutputPath,
    string? SnapshotPath,
    byte[]? SnapshotBytes);

internal static class MainWindowBranchExportWorkflowController
{
    public static MainWindowBranchExportPlan BuildPlan(
        string? currentRomId,
        string? romPath,
        Guid currentBranchId,
        BranchCanvasNode startNode,
        long startFrame,
        long endFrame,
        Func<long, CoreStateBlob?> resolveNearestState)
    {
        if (currentRomId == null || romPath == null)
            throw new InvalidOperationException("当前没有可导出的 ROM。");

        ArgumentNullException.ThrowIfNull(resolveNearestState);

        var branchId = startNode.BranchPoint?.Id ?? currentBranchId;
        var inputLogPath = TimelineStoragePaths.GetInputLogPath(currentRomId, branchId);
        var outputPath = TimelineStoragePaths.GetExportPath(currentRomId, branchId, startFrame, endFrame);

        if (startNode.BranchPoint != null)
        {
            return new MainWindowBranchExportPlan(
                BranchId: branchId,
                RomPath: romPath,
                InputLogPath: inputLogPath,
                OutputPath: outputPath,
                SnapshotPath: TimelineStoragePaths.GetBranchSnapshotPath(currentRomId, branchId),
                SnapshotBytes: null);
        }

        var nearestState = resolveNearestState(startFrame);
        if (nearestState == null)
            throw new InvalidOperationException("当前没有可用于导出的主线快照。");

        return new MainWindowBranchExportPlan(
            BranchId: branchId,
            RomPath: romPath,
            InputLogPath: inputLogPath,
            OutputPath: outputPath,
            SnapshotPath: null,
            SnapshotBytes: nearestState.Data);
    }
}
