using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowTaskMessageControllerTests : IDisposable
{
    private readonly string _storagePath;

    public MainWindowTaskMessageControllerTests()
    {
        _storagePath = Path.Combine(
            Path.GetTempPath(),
            $"fc-task-message-controller-tests-{Guid.NewGuid():N}",
            "task-messages.json");
        TaskMessageHub.ResetForTests(_storagePath);
    }

    [Fact]
    public void SetSearchTextAndCategoryFilter_RefreshFilteredMessages()
    {
        using var controller = new MainWindowTaskMessageController(TaskMessageHub.Instance);
        var hub = controller.Hub;

        hub.AddMessage(new TaskMessage
        {
            Category = MessageCategory.Status,
            Title = "系统",
            Content = "启动完成",
            Severity = MessageSeverity.Info,
            Timestamp = DateTime.UtcNow
        });
        hub.AddMessage(new TaskMessage
        {
            Category = MessageCategory.Error,
            Title = "预览",
            Content = "预览生成失败",
            Severity = MessageSeverity.Error,
            Timestamp = DateTime.UtcNow.AddSeconds(1)
        });

        Assert.Equal(2, controller.FilteredMessages.Count);
        Assert.True(controller.IsTaskMessageFilterAll);

        controller.SetCategoryFilter(nameof(MessageCategory.Error));
        Assert.Single(controller.FilteredMessages);
        Assert.True(controller.IsTaskMessageFilterError);
        Assert.False(controller.IsTaskMessageFilterAll);

        var changed = controller.SetSearchText("不存在的关键词");
        Assert.True(changed);
        Assert.Empty(controller.FilteredMessages);

        controller.SetSearchText("失败");
        Assert.Single(controller.FilteredMessages);
    }

    [Fact]
    public void PublishLegacyTaskMessage_SuppressesPreviewProgressAndPublishesStatus()
    {
        using var controller = new MainWindowTaskMessageController(TaskMessageHub.Instance);
        var hub = controller.Hub;

        controller.PublishLegacyTaskMessage(MainWindowLegacyTaskMessageSource.Preview, "正在生成: 魂斗罗");
        Assert.Empty(hub.Messages);

        controller.PublishLegacyTaskMessage(MainWindowLegacyTaskMessageSource.Status, "ROM 导入完成");
        Assert.Single(hub.Messages);
        Assert.Equal(MessageCategory.Status, hub.Messages[0].Category);
        Assert.Equal("ROM", hub.Messages[0].Title);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
