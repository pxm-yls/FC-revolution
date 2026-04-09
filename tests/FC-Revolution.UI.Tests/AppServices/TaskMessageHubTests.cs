using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class TaskMessageHubTests
{
    [Fact]
    public void TaskMessageHub_PersistsMessagesAcrossReset()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fc-task-message-tests-{Guid.NewGuid():N}");
        var storagePath = Path.Combine(tempDirectory, "task-messages.json");

        try
        {
            TaskMessageHub.ResetForTests(storagePath);
            TaskMessageHub.Instance.ClearMessages();
            TaskMessageHub.Instance.AddMessage(new TaskMessage
            {
                Category = MessageCategory.Status,
                Title = "测试",
                Content = "持久化成功",
                Severity = MessageSeverity.Success,
                IsCompleted = true
            });
            TaskMessageHub.Instance.Flush();

            TaskMessageHub.ResetForTests(storagePath);

            Assert.Contains(TaskMessageHub.Instance.Messages, message => message.Content == "持久化成功");
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
