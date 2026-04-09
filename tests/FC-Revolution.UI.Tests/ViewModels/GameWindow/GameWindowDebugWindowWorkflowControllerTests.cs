using System;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowDebugWindowWorkflowControllerTests
{
    [Fact]
    public void BuildPreOpenDecision_WhenSessionFailed_ReturnsToastOnlyRejection()
    {
        var controller = new GameWindowDebugWindowWorkflowController();

        var decision = controller.BuildPreOpenDecision(hasSessionFailure: true, hasUiThreadAccess: true);

        Assert.False(decision.ShouldDispatchToUiThread);
        Assert.False(decision.ShouldOpenWindow);
        Assert.Null(decision.StatusText);
        Assert.Equal("当前游戏会话已终止，请重新启动游戏后再打开调试窗口", decision.ToastText);
    }

    [Fact]
    public void BuildPreOpenDecision_WhenOffUiThread_ReturnsDispatchRequest()
    {
        var controller = new GameWindowDebugWindowWorkflowController();

        var decision = controller.BuildPreOpenDecision(hasSessionFailure: false, hasUiThreadAccess: false);

        Assert.True(decision.ShouldDispatchToUiThread);
        Assert.False(decision.ShouldOpenWindow);
        Assert.Null(decision.StatusText);
        Assert.Null(decision.ToastText);
    }

    [Fact]
    public void BuildPreOpenDecision_WhenReady_ReturnsOpenRequest()
    {
        var controller = new GameWindowDebugWindowWorkflowController();

        var decision = controller.BuildPreOpenDecision(hasSessionFailure: false, hasUiThreadAccess: true);

        Assert.False(decision.ShouldDispatchToUiThread);
        Assert.True(decision.ShouldOpenWindow);
        Assert.Null(decision.StatusText);
        Assert.Null(decision.ToastText);
    }

    [Fact]
    public void BuildOpenFailureDecision_FormatsStatusAndToast()
    {
        var controller = new GameWindowDebugWindowWorkflowController();

        var decision = controller.BuildOpenFailureDecision(new InvalidOperationException("boom"));

        Assert.Equal("打开调试窗口失败: boom", decision.StatusText);
        Assert.Equal("调试窗口打开失败: boom", decision.ToastText);
    }
}
