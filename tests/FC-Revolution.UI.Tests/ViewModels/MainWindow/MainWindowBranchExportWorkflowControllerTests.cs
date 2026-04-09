using System;
using System.Linq;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Adapters.LegacyTimeline;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowBranchExportWorkflowControllerTests
{
    [Fact]
    public void BuildPlan_WithoutRomContext_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MainWindowBranchExportWorkflowController.BuildPlan(
                currentRomId: null,
                romPath: null,
                currentBranchId: Guid.NewGuid(),
                startNode: CreateNode(frame: 120, branchPoint: null),
                startFrame: 120,
                endFrame: 180,
                resolveNearestState: _ => null));

        Assert.Equal("当前没有可导出的 ROM。", ex.Message);
    }

    [Fact]
    public void BuildPlan_WithBranchNode_UsesBranchSnapshotPathWithoutResolvingNearestState()
    {
        var currentBranchId = Guid.NewGuid();
        var branchPoint = CreateBranchPoint("Boss", frame: 240);
        var startNode = CreateNode(frame: 240, branchPoint);
        var resolveCalls = 0;

        var plan = MainWindowBranchExportWorkflowController.BuildPlan(
            currentRomId: "demo-rom",
            romPath: "/tmp/demo.nes",
            currentBranchId: currentBranchId,
            startNode: startNode,
            startFrame: 240,
            endFrame: 300,
            resolveNearestState: _ =>
            {
                resolveCalls++;
                throw new InvalidOperationException("should not resolve");
            });

        Assert.Equal(branchPoint.Id, plan.BranchId);
        Assert.Equal("/tmp/demo.nes", plan.RomPath);
        Assert.Equal(LegacyTimelineStorageAdapter.GetInputLogPath("demo-rom", branchPoint.Id), plan.InputLogPath);
        Assert.Equal(LegacyTimelineStorageAdapter.GetExportPath("demo-rom", branchPoint.Id, 240, 300), plan.OutputPath);
        Assert.Equal(LegacyTimelineStorageAdapter.GetBranchSnapshotPath("demo-rom", branchPoint.Id), plan.SnapshotPath);
        Assert.Null(plan.SnapshotBytes);
        Assert.Equal(0, resolveCalls);
    }

    [Fact]
    public void BuildPlan_WithMainlineNode_UsesCurrentBranchIdAndResolvedNearestState()
    {
        var currentBranchId = Guid.NewGuid();
        var nearestState = new CoreStateBlob
        {
            Format = "test/state",
            Data = [1, 2, 3, 4]
        };
        long? resolvedFrame = null;

        var plan = MainWindowBranchExportWorkflowController.BuildPlan(
            currentRomId: "demo-rom",
            romPath: "/tmp/demo.nes",
            currentBranchId: currentBranchId,
            startNode: CreateNode(frame: 120, branchPoint: null),
            startFrame: 120,
            endFrame: 180,
            resolveNearestState: frame =>
            {
                resolvedFrame = frame;
                return nearestState;
            });

        Assert.Equal(currentBranchId, plan.BranchId);
        Assert.Equal("/tmp/demo.nes", plan.RomPath);
        Assert.Equal(LegacyTimelineStorageAdapter.GetInputLogPath("demo-rom", currentBranchId), plan.InputLogPath);
        Assert.Equal(LegacyTimelineStorageAdapter.GetExportPath("demo-rom", currentBranchId, 120, 180), plan.OutputPath);
        Assert.Null(plan.SnapshotPath);
        Assert.True(nearestState.Data.SequenceEqual(plan.SnapshotBytes!));
        Assert.Equal(120, resolvedFrame);
    }

    [Fact]
    public void BuildPlan_WithMainlineNodeWithoutSnapshot_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MainWindowBranchExportWorkflowController.BuildPlan(
                currentRomId: "demo-rom",
                romPath: "/tmp/demo.nes",
                currentBranchId: Guid.NewGuid(),
                startNode: CreateNode(frame: 120, branchPoint: null),
                startFrame: 120,
                endFrame: 180,
                resolveNearestState: _ => null));

        Assert.Equal("当前没有可用于导出的主线快照。", ex.Message);
    }

    private static BranchCanvasNode CreateNode(long frame, CoreBranchPoint? branchPoint) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = branchPoint?.Name ?? $"Main {frame}",
            Subtitle = $"帧 {frame}",
            Frame = frame,
            CreatedAt = DateTime.UtcNow,
            X = 0,
            Y = 0,
            Width = 152,
            Height = 130,
            Bitmap = null,
            IsBranchNode = branchPoint != null,
            IsMainlineNode = branchPoint == null,
            BackgroundHex = "#000000",
            BorderBrushHex = "#FFFFFF",
            BorderThicknessValue = 1,
            BranchPoint = branchPoint
        };

    private static CoreBranchPoint CreateBranchPoint(string name, long frame) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            RomPath = "/tmp/test.nes",
            Frame = frame,
            TimestampSeconds = frame / 60.0,
            Snapshot = new CoreTimelineSnapshot
            {
                Frame = frame,
                TimestampSeconds = frame / 60.0,
                Thumbnail = Enumerable.Repeat((uint)frame, 64 * 60).ToArray(),
                State = new CoreStateBlob
                {
                    Format = "test/snapshot",
                    Data = []
                }
            },
            CreatedAt = DateTime.UtcNow
        };
}
