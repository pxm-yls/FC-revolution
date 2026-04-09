using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryBranchWorkflowControllerTests
{
    [Fact]
    public void CreateBranch_WhenFrameMissing_UpdatesStatusOnly()
    {
        string? status = null;
        var refreshCalls = 0;
        var controller = new BranchGalleryBranchWorkflowController(
            new FakeTimeTravelService(),
            new CoreBranchTree(),
            persistBranch: null,
            deleteBranch: null,
            renameBranch: null,
            selectNode: _ => { },
            updateSelectedNodeId: _ => { },
            refreshAll: () => refreshCalls++,
            updateStatus: text => status = text);

        controller.CreateBranch(lastFrame: null, romPath: "/tmp/test.nes", selectedNode: null);

        Assert.Equal(0, refreshCalls);
        Assert.Equal("无法创建分支：当前没有可用画面帧", status);
    }

    [Fact]
    public void CreateBranch_WithParentNode_ForksTreePersistsAndRefreshes()
    {
        var tree = new CoreBranchTree();
        var parentCore = CreateCoreBranchPoint("Parent", frame: 120);
        tree.AddRoot(parentCore);

        var createdCore = CreateCoreBranchPoint("New Branch", frame: 180);
        var timeTravel = new FakeTimeTravelService
        {
            CreateBranchResult = createdCore
        };
        Guid? persistedParentId = null;
        string? selectedNodeId = null;
        string? status = null;
        var refreshCalls = 0;
        var controller = new BranchGalleryBranchWorkflowController(
            timeTravel,
            tree,
            persistBranch: (_, parentId) => persistedParentId = parentId,
            deleteBranch: null,
            renameBranch: null,
            selectNode: _ => { },
            updateSelectedNodeId: nodeId => selectedNodeId = nodeId,
            refreshAll: () => refreshCalls++,
            updateStatus: text => status = text);

        controller.CreateBranch(
            lastFrame: new uint[64 * 60],
            romPath: "/tmp/test.nes",
            selectedNode: CreateCanvasNode(parentCore));

        Assert.Equal("分支 2", timeTravel.LastCreateBranchName);
        Assert.Equal("已创建分支「New Branch」帧 180", status);
        Assert.Equal($"branch:{createdCore.Id}", selectedNodeId);
        Assert.Equal(parentCore.Id, persistedParentId);
        Assert.Equal(1, refreshCalls);
        Assert.Contains(parentCore.Children, node => node.Id == createdCore.Id);
        Assert.NotNull(tree.Find(createdCore.Id));
    }

    [Fact]
    public void DeleteBranch_WhenSelected_RemovesBranchClearsSelectionAndRefreshes()
    {
        var tree = new CoreBranchTree();
        var branchPoint = CreateCoreBranchPoint("Delete Me", frame: 240);
        tree.AddRoot(branchPoint);

        Guid? deletedBranchId = null;
        BranchCanvasNode? selectedNode = CreateCanvasNode(branchPoint);
        string? selectedNodeId = "branch:initial";
        string? status = null;
        var refreshCalls = 0;
        var controller = new BranchGalleryBranchWorkflowController(
            new FakeTimeTravelService(),
            tree,
            persistBranch: null,
            deleteBranch: id => deletedBranchId = id,
            renameBranch: null,
            selectNode: node => selectedNode = node,
            updateSelectedNodeId: nodeId => selectedNodeId = nodeId,
            refreshAll: () => refreshCalls++,
            updateStatus: text => status = text);

        controller.DeleteBranch(CreateCanvasNode(branchPoint));

        Assert.Equal(branchPoint.Id, deletedBranchId);
        Assert.Null(selectedNode);
        Assert.Null(selectedNodeId);
        Assert.Equal(1, refreshCalls);
        Assert.Equal("已删除分支「Delete Me」", status);
        Assert.Null(tree.Find(branchPoint.Id));
    }

    [Fact]
    public void RenameBranch_WhenSelected_UpdatesTreeInvokesCallbackAndRefreshes()
    {
        var tree = new CoreBranchTree();
        var original = CreateCoreBranchPoint("Old", frame: 300);
        tree.AddRoot(original);

        var selectedCore = CreateCoreBranchPoint("Renamed", frame: 300, id: original.Id);
        CoreBranchPoint? renamedBranch = null;
        string? status = null;
        var refreshCalls = 0;
        var controller = new BranchGalleryBranchWorkflowController(
            new FakeTimeTravelService(),
            tree,
            persistBranch: null,
            deleteBranch: null,
            renameBranch: branchPoint => renamedBranch = branchPoint,
            selectNode: _ => { },
            updateSelectedNodeId: _ => { },
            refreshAll: () => refreshCalls++,
            updateStatus: text => status = text);

        controller.RenameBranch(CreateCanvasNode(selectedCore));

        Assert.Same(selectedCore, renamedBranch);
        Assert.Equal("Renamed", tree.Find(original.Id)?.Name);
        Assert.Equal(1, refreshCalls);
        Assert.Equal("已重命名为「Renamed」", status);
    }

    private static CoreBranchPoint CreateCoreBranchPoint(string name, long frame, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            RomPath = "/tmp/test.nes",
            Frame = frame,
            TimestampSeconds = frame / 60d,
            Snapshot = new CoreTimelineSnapshot
            {
                Frame = frame,
                TimestampSeconds = frame / 60d,
                Thumbnail = [],
                State = new CoreStateBlob
                {
                    Format = "test/state",
                    Data = []
                }
            },
            CreatedAt = DateTime.UtcNow
        };

    private static BranchCanvasNode CreateCanvasNode(CoreBranchPoint branchPoint) =>
        new()
        {
            Id = $"branch:{branchPoint.Id}",
            Title = branchPoint.Name,
            Subtitle = $"帧 {branchPoint.Frame}",
            Frame = branchPoint.Frame,
            CreatedAt = branchPoint.CreatedAt,
            X = 0,
            Y = 0,
            Width = 152,
            Height = 130,
            Bitmap = null,
            IsBranchNode = true,
            IsMainlineNode = false,
            BackgroundHex = "#000000",
            BorderBrushHex = "#FFFFFF",
            BorderThicknessValue = 1,
            BranchPoint = branchPoint
        };

    private sealed class FakeTimeTravelService : ITimeTravelService
    {
        public CoreBranchPoint? CreateBranchResult { get; set; }

        public string? LastCreateBranchName { get; private set; }

        public long CurrentFrame => 0;

        public double CurrentTimestampSeconds => 0;

        public int SnapshotInterval { get; set; } = 5;

        public int HotCacheCount => 0;

        public int WarmCacheCount => 0;

        public long NewestFrame => 0;

        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, 0, SnapshotInterval);

        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => [];

        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer)
        {
            LastCreateBranchName = name;
            return CreateBranchResult ?? throw new InvalidOperationException("CreateBranchResult is not configured.");
        }

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
