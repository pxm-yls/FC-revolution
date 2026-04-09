using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class BranchGalleryViewModelTimelineNavigationTests
{
    [Fact]
    public void LoadBranchCommand_RestoresSnapshot_ActivatesBranch_AndRaisesTimelineJump()
    {
        var timeTravel = new FakeTimeTravelService();
        var branchPoint = CreateBranchPoint("Boss Branch", frame: 180);
        var branchNode = CreateNode(frame: 180, branchPoint);
        Guid? activatedBranchId = null;
        var jumpCount = 0;
        var vm = new BranchGalleryViewModel(
            timeTravel,
            new CoreBranchTree(),
            activateBranch: branchId => activatedBranchId = branchId,
            notifyTimelineJump: () => jumpCount++);
        vm.SelectedNode = branchNode;

        vm.LoadBranchCommand.Execute(null);

        Assert.Same(branchPoint.Snapshot, timeTravel.LastRestoredSnapshot);
        Assert.Equal(branchPoint.Id, activatedBranchId);
        Assert.Equal(1, jumpCount);
        Assert.Equal("已载入节点「Boss Branch」帧 180", vm.StatusText);
    }

    [Fact]
    public void SeekToNodeCommand_WhenMainlineNode_SeeksFrameAndUpdatesStatus()
    {
        var timeTravel = new FakeTimeTravelService
        {
            SeekResult = 240
        };
        var mainlineNode = CreateNode(frame: 210, branchPoint: null);
        var jumpCount = 0;
        var vm = new BranchGalleryViewModel(
            timeTravel,
            new CoreBranchTree(),
            notifyTimelineJump: () => jumpCount++);

        vm.SeekToNodeCommand.Execute(mainlineNode);

        Assert.Same(mainlineNode, vm.SelectedNode);
        Assert.Equal(210, timeTravel.LastSeekFrame);
        Assert.Equal(1, jumpCount);
        Assert.Equal("已载入主线帧 240", vm.StatusText);
    }

    [Fact]
    public void RewindCommand_RewindsFrames_RefreshesCanvas_AndUpdatesStatus()
    {
        var timeTravel = new FakeTimeTravelService
        {
            RewindResult = 90
        };
        var vm = new BranchGalleryViewModel(
            timeTravel,
            new CoreBranchTree(),
            notifyTimelineJump: () => timeTravel.TimelineJumpNotifications++);
        var thumbnailsBefore = timeTravel.GetThumbnailsCallCount;

        vm.RewindCommand.Execute("5");

        Assert.Equal(5, timeTravel.LastRewindFrames);
        Assert.Equal(thumbnailsBefore + 1, timeTravel.GetThumbnailsCallCount);
        Assert.Equal(1, timeTravel.TimelineJumpNotifications);
        Assert.Equal("已回退至帧 90", vm.StatusText);
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

        public int GetThumbnailsCallCount { get; private set; }

        public int TimelineJumpNotifications { get; set; }

        public long CurrentFrame => 0;

        public double CurrentTimestampSeconds => 0;

        public int SnapshotInterval { get; set; } = 5;

        public int HotCacheCount => 0;

        public int WarmCacheCount => 0;

        public long NewestFrame => 0;

        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, 0, SnapshotInterval);

        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails()
        {
            GetThumbnailsCallCount++;
            return [];
        }

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
