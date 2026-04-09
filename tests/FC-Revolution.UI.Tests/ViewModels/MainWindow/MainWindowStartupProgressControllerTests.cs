using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowStartupProgressControllerTests
{
    [Fact]
    public void BuildProgressText_FormatsAllSections()
    {
        var controller = new MainWindowStartupProgressController();
        var state = new MainWindowStartupProgressState(
            CurrentStep: "准备中",
            GameListStatus: "加载中",
            PreviewStatus: "等待开始",
            LanStatus: "等待开始",
            IsVisible: true);

        var text = controller.BuildProgressText(state);

        Assert.Contains("当前阶段: 准备中", text);
        Assert.Contains("游戏列表: 加载中", text);
        Assert.Contains("预览动画: 等待开始", text);
        Assert.Contains("局域网后台: 等待开始", text);
    }

    [Fact]
    public void Update_ReturnsChangedAndCurrentStepChanged_WhenCurrentStepDiffers()
    {
        var controller = new MainWindowStartupProgressController();
        var current = new MainWindowStartupProgressState(
            CurrentStep: "准备中",
            GameListStatus: "等待开始",
            PreviewStatus: "等待开始",
            LanStatus: "等待开始",
            IsVisible: true);

        var result = controller.Update(
            current,
            currentStep: "加载中",
            gameListStatus: "加载中",
            previewStatus: null,
            lanStatus: null,
            isVisible: null);

        Assert.True(result.Changed);
        Assert.True(result.CurrentStepChanged);
        Assert.Equal("加载中", result.State.CurrentStep);
        Assert.Equal("加载中", result.State.GameListStatus);
        Assert.Equal("等待开始", result.State.PreviewStatus);
    }

    [Fact]
    public void Update_ReturnsUnchanged_WhenInputMatchesCurrentState()
    {
        var controller = new MainWindowStartupProgressController();
        var current = new MainWindowStartupProgressState(
            CurrentStep: "准备中",
            GameListStatus: "等待开始",
            PreviewStatus: "等待开始",
            LanStatus: "等待开始",
            IsVisible: false);

        var result = controller.Update(
            current,
            currentStep: "准备中",
            gameListStatus: "等待开始",
            previewStatus: "等待开始",
            lanStatus: "等待开始",
            isVisible: false);

        Assert.False(result.Changed);
        Assert.False(result.CurrentStepChanged);
        Assert.Equal(current, result.State);
    }
}
