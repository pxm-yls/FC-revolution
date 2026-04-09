using System;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowSessionFailureControllerTests
{
    [Fact]
    public void BuildDecision_WhenAvailable_ReturnsStopPauseAndDiagnostics()
    {
        var controller = new GameWindowSessionFailureController();
        var ex = new InvalidOperationException("boom");

        var decision = controller.BuildDecision(
            isDisposed: false,
            hasSessionFailure: false,
            message: "session exploded",
            exception: ex);

        Assert.True(decision.ShouldHandleFailure);
        Assert.True(decision.ShouldStopSessionLoop);
        Assert.True(decision.ShouldClearPendingFrame);
        Assert.True(decision.ShouldPauseRuntime);
        Assert.Equal("当前游戏会话已停止 | session exploded", decision.StatusText);
        Assert.Equal("session exploded", decision.ToastText);
        Assert.Equal(2, decision.Diagnostics.Count);
        Assert.Equal("session exploded", decision.Diagnostics[0]);
        Assert.Contains("boom", decision.Diagnostics[1]);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BuildDecision_WhenDisposedOrAlreadyFailed_ReturnsNoOp(bool isDisposed, bool hasSessionFailure)
    {
        var controller = new GameWindowSessionFailureController();

        var decision = controller.BuildDecision(isDisposed, hasSessionFailure, "ignored");

        Assert.False(decision.ShouldHandleFailure);
        Assert.False(decision.ShouldStopSessionLoop);
        Assert.False(decision.ShouldClearPendingFrame);
        Assert.False(decision.ShouldPauseRuntime);
        Assert.Empty(decision.StatusText);
        Assert.Empty(decision.ToastText);
        Assert.Empty(decision.Diagnostics);
    }
}
