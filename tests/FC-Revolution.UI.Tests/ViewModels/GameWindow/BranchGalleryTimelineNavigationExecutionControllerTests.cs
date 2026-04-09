using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryTimelineNavigationExecutionControllerTests
{
    [Fact]
    public void Execute_LoadBranch_RestoresSnapshotActivatesAndUpdatesStatus()
    {
        var timeTravel = new FakeTimeTravelService();
        var branchPoint = CreateBranchPoint("Boss", frame: 180);
        Guid? activatedBranchId = null;
        var timelineJumpCount = 0;
        var refreshCount = 0;
        string? status = null;
        var controller = new BranchGalleryTimelineNavigationExecutionController(
            timeTravel,
            branchId => activatedBranchId = branchId,
            () => timelineJumpCount++,
            () => refreshCount++,
            text => status = text);

        controller.Execute(BranchGalleryTimelineNavigationController.BuildLoadBranchDecision(CreateNode(branchPoint)));

        Assert.Same(branchPoint.Snapshot, timeTravel.LastRestoredSnapshot);
        Assert.Equal(branchPoint.Id, activatedBranchId);
        Assert.Equal(1, timelineJumpCount);
        Assert.Equal(0, refreshCount);
        Assert.Equal("已载入节点「Boss」帧 180", status);
    }

    [Fact]
    public void Execute_SeekFrame_UsesSeekResultAndUpdatesStatus()
    {
        var timeTravel = new FakeTimeTravelService
        {
            SeekResult = 240
        };
        var timelineJumpCount = 0;
        var refreshCount = 0;
        string? status = null;
        var controller = new BranchGalleryTimelineNavigationExecutionController(
            timeTravel,
            _ => { },
            () => timelineJumpCount++,
            () => refreshCount++,
            text => status = text);

        controller.Execute(BranchGalleryTimelineNavigationController.BuildSeekDecision(CreateNode(branchPoint: null, frame: 210)));

        Assert.Equal(210, timeTravel.LastSeekFrame);
        Assert.Equal(1, timelineJumpCount);
        Assert.Equal(0, refreshCount);
        Assert.Equal("已载入主线帧 240", status);
    }

    [Fact]
    public void Execute_Rewind_UsesRewindResultAndRefreshes()
    {
        var timeTravel = new FakeTimeTravelService
        {
            RewindResult = 90
        };
        var timelineJumpCount = 0;
        var refreshCount = 0;
        string? status = null;
        var controller = new BranchGalleryTimelineNavigationExecutionController(
            timeTravel,
            _ => { },
            () => timelineJumpCount++,
            () => refreshCount++,
            text => status = text);

        controller.Execute(BranchGalleryTimelineNavigationController.BuildRewindDecision("5"));

        Assert.Equal(5, timeTravel.LastRewindFrames);
        Assert.Equal(1, timelineJumpCount);
        Assert.Equal(1, refreshCount);
        Assert.Equal("已回退至帧 90", status);
    }

    private static BranchCanvasNode CreateNode(CoreBranchPoint? branchPoint, long frame = 180) =>
        new()
        {
            Id = branchPoint == null ? $"main:{frame}" : $"branch:{branchPoint.Id}",
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
                Thumbnail = new uint[64 * 60],
                State = new CoreStateBlob
                {
                    Format = "test/state",
                    Data = []
                }
            },
            CreatedAt = DateTime.UtcNow
        };

    private sealed class FakeTimeTravelService : ITimeTravelService
    {
        public long SeekResult { get; set; } = -1;

        public long RewindResult { get; set; } = -1;

        public long LastSeekFrame { get; private set; } = -1;

        public int LastRewindFrames { get; private set; }

        public CoreTimelineSnapshot? LastRestoredSnapshot { get; private set; }

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

        public void RestoreSnapshot(CoreTimelineSnapshot snapshot) => LastRestoredSnapshot = snapshot;

        public long SeekToFrame(long frame)
        {
            LastSeekFrame = frame;
            return SeekResult;
        }

        public long RewindFrames(int frames)
        {
            LastRewindFrames = frames;
            return RewindResult;
        }

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
