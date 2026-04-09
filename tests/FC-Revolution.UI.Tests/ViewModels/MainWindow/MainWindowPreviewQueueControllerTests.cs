using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewQueueControllerTests
{
    [Fact]
    public void TryEnqueuePreviewGeneration_ReturnsDuplicate_WhenPendingExists()
    {
        var controller = new MainWindowPreviewQueueController();
        var rom = new RomLibraryItem("Contra.nes", "/tmp/contra.nes", "", false, 1024, DateTime.UtcNow);
        var queue = new List<PreviewGenerationTaskItem>
        {
            new("Contra", "/tmp/contra.nes", "预览生成")
            {
                Status = "排队中",
                IsCompleted = false
            }
        };

        var result = controller.TryEnqueuePreviewGeneration(
            queue,
            rom,
            forceRegenerate: false,
            (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.False(result.Accepted);
        Assert.Null(result.TaskItem);
        Assert.Equal("Contra 的预览任务已在队列中", result.StatusText);
        Assert.Null(result.PreviewStatusText);
    }

    [Fact]
    public void TryEnqueuePreviewGeneration_CreatesTaskWithExpectedStatus()
    {
        var controller = new MainWindowPreviewQueueController();
        var rom = new RomLibraryItem("Contra.nes", "/tmp/contra.nes", "", false, 1024, DateTime.UtcNow);
        var queue = new List<PreviewGenerationTaskItem>();

        var result = controller.TryEnqueuePreviewGeneration(
            queue,
            rom,
            forceRegenerate: true,
            (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.True(result.Accepted);
        Assert.NotNull(result.TaskItem);
        Assert.Equal("排队中 · 重新生成", result.TaskItem!.Status);
        Assert.Equal("Contra 已加入预览生成队列", result.PreviewStatusText);
        Assert.Equal("Contra 预览任务已加入队列", result.StatusText);
    }

    [Fact]
    public void TryPrepareNextJob_MarksTaskFailed_WhenRomMissing()
    {
        var controller = new MainWindowPreviewQueueController();
        var queue = new List<PreviewGenerationTaskItem>
        {
            new("Contra", "/tmp/contra.nes", "预览生成")
            {
                Status = "排队中"
            }
        };
        var romLibrary = new List<RomLibraryItem>();

        var result = controller.TryPrepareNextJob(
            queue,
            romLibrary,
            (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.False(result.HasJob);
        Assert.Equal("失败: ROM 不存在", queue[0].Status);
        Assert.True(queue[0].IsCompleted);
    }

    [Fact]
    public void BuildPreviewQueueAggregateProgress_AveragesPreviewTasksOnly()
    {
        var controller = new MainWindowPreviewQueueController();
        var queue = new List<PreviewGenerationTaskItem>
        {
            new("A", "/tmp/a.nes", "预览生成") { Progress = 0.25 },
            new("B", "/tmp/b.nes", "预览生成") { Progress = 0.75 },
            new("Import", "", "导入任务") { Progress = 1.0 }
        };

        var progress = controller.BuildPreviewQueueAggregateProgress(queue);

        Assert.Equal(0.5, progress, 3);
    }
}
