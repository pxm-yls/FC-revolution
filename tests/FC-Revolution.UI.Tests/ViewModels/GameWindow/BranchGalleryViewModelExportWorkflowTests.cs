using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class BranchGalleryViewModelExportWorkflowTests
{
    [Fact]
    public void SetExportCommands_UpdateRangeLabel_AndStatusText()
    {
        var vm = CreateViewModel();
        var startNode = CreateNode(frame: 120);
        var endNode = CreateNode(frame: 240);
        vm.CanvasNodes.Add(startNode);
        vm.CanvasNodes.Add(endNode);

        vm.SelectedNode = startNode;
        vm.SetExportStartCommand.Execute(null);

        Assert.Equal("已设置导出起点：帧 120", vm.StatusText);
        Assert.Equal("尚未设置导出区间", vm.ExportRangeLabel);

        vm.SelectedNode = endNode;
        vm.SetExportEndCommand.Execute(null);

        Assert.Equal("已设置导出终点：帧 240", vm.StatusText);
        Assert.Equal("导出区间: 120 - 240", vm.ExportRangeLabel);
    }

    [Fact]
    public async Task ExportRangeCommand_WhenRangeIsReversed_NormalizesFrames_AndPreservesStartNode()
    {
        ExportInvocation? invocation = null;
        var vm = CreateViewModel((startNode, startFrame, endFrame) =>
        {
            invocation = new ExportInvocation(startNode, startFrame, endFrame);
            return Task.FromResult("/tmp/export-120-240.mp4");
        });
        var startNode = CreateNode(frame: 240);
        var endNode = CreateNode(frame: 120);
        vm.CanvasNodes.Add(startNode);
        vm.CanvasNodes.Add(endNode);

        vm.SetExportStartFromNodeCommand.Execute(startNode);
        vm.SetExportEndFromNodeCommand.Execute(endNode);
        await vm.ExportRangeCommand.ExecuteAsync(null);

        Assert.NotNull(invocation);
        Assert.Same(startNode, invocation.Value.StartNode);
        Assert.Equal(120, invocation.Value.StartFrame);
        Assert.Equal(240, invocation.Value.EndFrame);
        Assert.Equal("已导出 MP4: export-120-240.mp4", vm.StatusText);
    }

    [Fact]
    public async Task ExportRangeCommand_WithoutConfiguredRange_ShowsMissingRangeStatus()
    {
        var exportCallCount = 0;
        var vm = CreateViewModel((_, _, _) =>
        {
            exportCallCount++;
            return Task.FromResult("/tmp/unused.mp4");
        });

        await vm.ExportRangeCommand.ExecuteAsync(null);

        Assert.Equal(0, exportCallCount);
        Assert.Equal("请先设置导出起点和终点", vm.StatusText);
    }

    [Fact]
    public async Task ExportRangeCommand_WhenExporterThrows_ShowsFailureStatus()
    {
        var vm = CreateViewModel((_, _, _) => throw new InvalidOperationException("boom"));
        var startNode = CreateNode(frame: 180);
        var endNode = CreateNode(frame: 300);
        vm.CanvasNodes.Add(startNode);
        vm.CanvasNodes.Add(endNode);

        vm.SetExportStartFromNodeCommand.Execute(startNode);
        vm.SetExportEndFromNodeCommand.Execute(endNode);
        await vm.ExportRangeCommand.ExecuteAsync(null);

        Assert.Equal("导出失败: boom", vm.StatusText);
    }

    private static BranchGalleryViewModel CreateViewModel(
        Func<BranchCanvasNode, long, long, Task<string>>? exportRange = null) =>
        new(
            new FakeTimeTravelService(),
            new BranchTree(),
            exportRange: exportRange);

    private static BranchCanvasNode CreateNode(long frame) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = $"Node {frame}",
            Subtitle = $"帧 {frame}",
            Frame = frame,
            CreatedAt = DateTime.UtcNow,
            X = 0,
            Y = 0,
            Width = 152,
            Height = 130,
            Bitmap = null,
            IsBranchNode = false,
            IsMainlineNode = true,
            BackgroundHex = "#000000",
            BorderBrushHex = "#FFFFFF",
            BorderThicknessValue = 1,
            BranchPoint = null
        };

    private readonly record struct ExportInvocation(BranchCanvasNode StartNode, long StartFrame, long EndFrame);

    private sealed class FakeTimeTravelService : ITimeTravelService
    {
        public long CurrentFrame => 0;

        public double CurrentTimestampSeconds => 0;

        public int SnapshotInterval { get; set; } = 5;

        public int HotCacheCount => 0;

        public int WarmCacheCount => 0;

        public long NewestFrame => 0;

        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, 0, SnapshotInterval);

        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => [];

        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) =>
            throw new NotSupportedException();

        public void RestoreSnapshot(CoreTimelineSnapshot snapshot) =>
            throw new NotSupportedException();

        public long SeekToFrame(long frame) =>
            throw new NotSupportedException();

        public long RewindFrames(int frames) =>
            throw new NotSupportedException();

        public CoreTimelineSnapshot? GetNearestSnapshot(long frame) => null;

        public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false) => null;

        public void PauseRecording()
        {
        }

        public void ResumeRecording()
        {
        }
    }
}
