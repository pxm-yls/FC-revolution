using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewStartupControllerTests
{
    [Fact]
    public void BuildWarmupStartState_UsesCurrentRomNameWhenAvailable()
    {
        var controller = new MainWindowPreviewStartupController();

        var withRom = controller.BuildWarmupStartState("Contra");
        var withoutRom = controller.BuildWarmupStartState(null);

        Assert.Equal("正在预热 Contra 的预览动画。", withRom.CurrentStep);
        Assert.Equal("加载中", withRom.PreviewStatus);
        Assert.Equal("正在整理预览资源。", withoutRom.CurrentStep);
        Assert.Equal("加载中", withoutRom.PreviewStatus);
    }

    [Fact]
    public void BuildWarmupCompletionState_FormatsPreviewAndLanStatus()
    {
        var controller = new MainWindowPreviewStartupController();

        var empty = controller.BuildWarmupCompletionState(0, isLanArcadeEnabled: false);
        var warmed = controller.BuildWarmupCompletionState(3, isLanArcadeEnabled: true);

        Assert.Equal("预览阶段完成，当前没有可加载的预览。", empty.CurrentStep);
        Assert.Equal("完成（无预览）", empty.PreviewStatus);
        Assert.Equal("已跳过（已关闭）", empty.LanStatus);

        Assert.Equal("预览阶段完成，已预热 3 个预览。", warmed.CurrentStep);
        Assert.Equal("完成（3 个）", warmed.PreviewStatus);
        Assert.Equal("准备启动", warmed.LanStatus);
    }
}
