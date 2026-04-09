using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowRomDeleteWorkflowControllerTests
{
    [Fact]
    public void BuildResourceSummary_DelegatesToSummaryProvider()
    {
        var controller = new MainWindowRomDeleteWorkflowController(
            romPath => $"summary:{romPath}",
            _ => throw new InvalidOperationException("delete resources should not run"),
            _ => false,
            _ => throw new InvalidOperationException("delete file should not run"));

        var summary = controller.BuildResourceSummary("/tmp/demo.nes");

        Assert.Equal("summary:/tmp/demo.nes", summary);
    }

    [Fact]
    public void ExecuteConfirmedDelete_DeleteRomOnly_RemovesRomFileWithoutDeletingResources()
    {
        var deletedFiles = new List<string>();
        var deletedResources = new List<string>();
        var controller = new MainWindowRomDeleteWorkflowController(
            _ => "",
            romPath => deletedResources.Add(romPath),
            filePath => string.Equals(filePath, "/tmp/demo.nes", StringComparison.Ordinal),
            filePath => deletedFiles.Add(filePath));

        var result = controller.ExecuteConfirmedDelete("/tmp/demo.nes", "demo", deleteAssociatedResources: false);

        Assert.Equal(["/tmp/demo.nes"], deletedFiles);
        Assert.Empty(deletedResources);
        Assert.Equal("已删除游戏: demo", result.StatusText);
    }

    [Fact]
    public void ExecuteConfirmedDelete_DeleteRomWithResources_DeletesRomAndAssociatedResources()
    {
        var deletedFiles = new List<string>();
        var deletedResources = new List<string>();
        var controller = new MainWindowRomDeleteWorkflowController(
            _ => "",
            romPath => deletedResources.Add(romPath),
            _ => true,
            filePath => deletedFiles.Add(filePath));

        var result = controller.ExecuteConfirmedDelete("/tmp/zelda.nes", "zelda", deleteAssociatedResources: true);

        Assert.Equal(["/tmp/zelda.nes"], deletedFiles);
        Assert.Equal(["/tmp/zelda.nes"], deletedResources);
        Assert.Equal("已删除游戏及关联资源: zelda", result.StatusText);
    }
}
