using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowResourceRootWorkflowControllerTests
{
    [Fact]
    public void Apply_ConfiguresRoot_ThenRunsDependentRefreshActions()
    {
        var calls = new List<string>();
        var controller = new MainWindowResourceRootWorkflowController(input =>
        {
            calls.Add($"configure:{input}");
            return "/tmp/fc-root";
        });

        var result = controller.Apply(
            "/tmp/input",
            () => calls.Add("save"),
            () => calls.Add("refresh-library"),
            () => calls.Add("update-current"));

        Assert.Equal(["configure:/tmp/input", "save", "refresh-library", "update-current"], calls);
        Assert.Equal("/tmp/fc-root", result.ResourceRootPath);
        Assert.Equal("已更新资源根目录: /tmp/fc-root", result.StatusText);
    }

    [Fact]
    public void Apply_PassesThroughBlankInputToConfigureDelegate()
    {
        string? capturedInput = null;
        var controller = new MainWindowResourceRootWorkflowController(input =>
        {
            capturedInput = input;
            return "/tmp/default-root";
        });

        var result = controller.Apply(
            "",
            () => { },
            () => { },
            () => { });

        Assert.Equal("", capturedInput);
        Assert.Equal("/tmp/default-root", result.ResourceRootPath);
        Assert.Equal("已更新资源根目录: /tmp/default-root", result.StatusText);
    }
}
