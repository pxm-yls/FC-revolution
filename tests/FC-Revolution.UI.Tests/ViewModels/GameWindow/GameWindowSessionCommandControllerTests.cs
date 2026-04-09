using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowSessionCommandControllerTests
{
    [Fact]
    public void QuickSave_UsesWorkflowResultStatusAndToast()
    {
        string? status = null;
        string? toast = null;
        var controller = new GameWindowSessionCommandController(
            () => new GameWindowSaveStateWorkflowResult("save ok", "toast save"),
            () => throw new InvalidOperationException("quick load should not be called"),
            () => throw new InvalidOperationException("toggle pause should not be called"),
            (text, message) =>
            {
                status = text;
                toast = message;
            });

        controller.QuickSave();

        Assert.Equal("save ok", status);
        Assert.Equal("toast save", toast);
    }

    [Fact]
    public void QuickLoad_UsesWorkflowResultStatusAndToast()
    {
        string? status = null;
        string? toast = null;
        var controller = new GameWindowSessionCommandController(
            () => throw new InvalidOperationException("quick save should not be called"),
            () => new GameWindowSaveStateWorkflowResult("load ok", "toast load"),
            () => throw new InvalidOperationException("toggle pause should not be called"),
            (text, message) =>
            {
                status = text;
                toast = message;
            });

        controller.QuickLoad();

        Assert.Equal("load ok", status);
        Assert.Equal("toast load", toast);
    }

    [Theory]
    [InlineData(true, "游戏已暂停", "已暂停")]
    [InlineData(false, "游戏已继续", "已继续")]
    public void TogglePause_FormatsStatusFromRuntimeResult(bool paused, string expectedStatus, string expectedToast)
    {
        string? status = null;
        string? toast = null;
        var controller = new GameWindowSessionCommandController(
            () => throw new InvalidOperationException("quick save should not be called"),
            () => throw new InvalidOperationException("quick load should not be called"),
            () => paused,
            (text, message) =>
            {
                status = text;
                toast = message;
            });

        controller.TogglePause();

        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedToast, toast);
    }
}
