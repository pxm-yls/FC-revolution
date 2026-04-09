using System;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowDebugWindowOpenControllerTests
{
    [Fact]
    public void TryOpen_WhenSessionFailed_ShowsFailureToastWithoutOpeningWindow()
    {
        var openCalls = 0;
        string? toast = null;
        string? status = null;
        var controller = new GameWindowDebugWindowOpenController(
            new GameWindowDebugWindowWorkflowController(),
            hasSessionFailure: () => true,
            hasUiThreadAccess: () => true,
            postToUiThread: _ => throw new InvalidOperationException("should not dispatch"),
            openWindow: () => openCalls++,
            updateStatus: (text, _) => status = text,
            showToast: message => toast = message);

        controller.TryOpen();

        Assert.Equal(0, openCalls);
        Assert.Null(status);
        Assert.Equal("当前游戏会话已终止，请重新启动游戏后再打开调试窗口", toast);
    }

    [Fact]
    public void TryOpen_WhenOffUiThread_RepostsAndOpensWindow()
    {
        var hasUiThreadAccess = false;
        var postCalls = 0;
        var openCalls = 0;
        string? toast = null;
        var controller = new GameWindowDebugWindowOpenController(
            new GameWindowDebugWindowWorkflowController(),
            hasSessionFailure: () => false,
            hasUiThreadAccess: () => hasUiThreadAccess,
            postToUiThread: action =>
            {
                postCalls++;
                hasUiThreadAccess = true;
                action();
            },
            openWindow: () => openCalls++,
            updateStatus: (_, _) => throw new InvalidOperationException("should not update status"),
            showToast: message => toast = message);

        controller.TryOpen();

        Assert.Equal(1, postCalls);
        Assert.Equal(1, openCalls);
        Assert.Equal("调试窗口已打开", toast);
    }

    [Fact]
    public void TryOpen_WhenOpenFails_UpdatesStatusAndToast()
    {
        string? status = null;
        string? toast = null;
        var controller = new GameWindowDebugWindowOpenController(
            new GameWindowDebugWindowWorkflowController(),
            hasSessionFailure: () => false,
            hasUiThreadAccess: () => true,
            postToUiThread: _ => throw new InvalidOperationException("should not dispatch"),
            openWindow: () => throw new InvalidOperationException("boom"),
            updateStatus: (text, message) =>
            {
                status = text;
                toast = message;
            },
            showToast: message => toast = message);

        controller.TryOpen();

        Assert.Equal("打开调试窗口失败: boom", status);
        Assert.Equal("调试窗口打开失败: boom", toast);
    }
}
