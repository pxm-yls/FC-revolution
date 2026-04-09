using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class BranchGalleryPreviewNodeWorkflowControllerTests
{
    [Fact]
    public void CreatePreviewNode_WhenCreationFails_UpdatesStatusOnly()
    {
        var previewNodes = new List<BranchPreviewNode>();
        string? status = null;
        var rebuildCalls = 0;
        var controller = new BranchGalleryPreviewNodeWorkflowController(
            _ => null,
            _ => { },
            (_, _) => { },
            previewNodes,
            _ => throw new InvalidOperationException("selection should not change"),
            text => status = text,
            () => rebuildCalls++);

        controller.CreatePreviewNode(CreateCanvasNode(frame: 120));

        Assert.Empty(previewNodes);
        Assert.Equal("无法为该节点生成预览图", status);
        Assert.Equal(0, rebuildCalls);
    }

    [Fact]
    public void CreatePreviewNode_WhenCreated_SelectsNodeAndRebuilds()
    {
        var previewNodes = new List<BranchPreviewNode>();
        BranchPreviewNode? selected = null;
        string? status = null;
        var rebuildCalls = 0;
        var created = new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = 180,
            TimestampSeconds = 3,
            Title = "Node 180",
            Bitmap = null
        };
        var controller = new BranchGalleryPreviewNodeWorkflowController(
            _ => created,
            _ => { },
            (_, _) => { },
            previewNodes,
            previewNode => selected = previewNode,
            text => status = text,
            () => rebuildCalls++);

        controller.CreatePreviewNode(CreateCanvasNode(frame: 180));

        Assert.Same(created, Assert.Single(previewNodes));
        Assert.Same(created, selected);
        Assert.Equal("已创建画面节点：帧 180", status);
        Assert.Equal(1, rebuildCalls);
    }

    [Fact]
    public void DeletePreviewNode_WhenPresent_RemovesNodeAndClearsSelection()
    {
        var previewNodeId = Guid.NewGuid();
        var previewNodes = new List<BranchPreviewNode>
        {
            new()
            {
                Id = previewNodeId,
                Frame = 240,
                TimestampSeconds = 4,
                Title = "Node 240",
                Bitmap = null
            }
        };
        Guid? deletedId = null;
        BranchPreviewNode? selected = new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = 1,
            TimestampSeconds = 0,
            Title = "stale",
            Bitmap = null
        };
        string? status = null;
        var rebuildCalls = 0;
        var controller = new BranchGalleryPreviewNodeWorkflowController(
            _ => throw new InvalidOperationException("create should not be called"),
            id => deletedId = id,
            (_, _) => { },
            previewNodes,
            previewNode => selected = previewNode,
            text => status = text,
            () => rebuildCalls++);

        controller.DeletePreviewNode(previewNodeId);

        Assert.Equal(previewNodeId, deletedId);
        Assert.Empty(previewNodes);
        Assert.Null(selected);
        Assert.Equal("已删除画面节点", status);
        Assert.Equal(1, rebuildCalls);
    }

    [Fact]
    public void RenamePreviewNode_NormalizesTitlePersistsAndRebuilds()
    {
        var previewNode = new BranchPreviewNode
        {
            Id = Guid.NewGuid(),
            Frame = 300,
            TimestampSeconds = 5,
            Title = "  Boss Shot  ",
            Bitmap = null
        };
        Guid? renamedId = null;
        string? renamedTitle = null;
        string? status = null;
        var rebuildCalls = 0;
        var controller = new BranchGalleryPreviewNodeWorkflowController(
            _ => throw new InvalidOperationException("create should not be called"),
            _ => { },
            (id, title) =>
            {
                renamedId = id;
                renamedTitle = title;
            },
            new List<BranchPreviewNode> { previewNode },
            _ => { },
            text => status = text,
            () => rebuildCalls++);

        controller.RenamePreviewNode(previewNode);

        Assert.Equal("Boss Shot", previewNode.Title);
        Assert.Equal(previewNode.Id, renamedId);
        Assert.Equal("Boss Shot", renamedTitle);
        Assert.Equal("已重命名画面节点为「Boss Shot」", status);
        Assert.Equal(1, rebuildCalls);
    }

    private static BranchCanvasNode CreateCanvasNode(long frame) =>
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
}
