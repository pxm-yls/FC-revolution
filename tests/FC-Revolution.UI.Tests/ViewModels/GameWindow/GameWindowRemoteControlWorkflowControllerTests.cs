using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRemoteControlWorkflowControllerTests
{
    [Fact]
    public void BuildAcquireDecision_WhenAcquired_ClearsRemoteStateAndShowsConnectedToast()
    {
        var controller = new GameWindowRemoteControlWorkflowController(new GameWindowRemoteControlStateController());

        var decision = controller.BuildAcquireDecision(acquired: true, portId: "p1", clientIp: "127.0.0.1");

        Assert.True(decision.ShouldApplyViewState);
        Assert.True(decision.ShouldClearRemoteButtons);
        Assert.True(decision.ShouldRebuildCombinedState);
        Assert.False(decision.ShouldRefreshLocalInput);
        Assert.False(decision.ShouldApplyRequestedRemoteButtonState);
        Assert.Collection(
            decision.ToastMessages,
            message => Assert.Equal("1P 已切换为 127.0.0.1 网页控制", message));
    }

    [Fact]
    public void BuildAcquireDecision_WhenRejected_ReturnsNoOp()
    {
        var controller = new GameWindowRemoteControlWorkflowController(new GameWindowRemoteControlStateController());

        var decision = controller.BuildAcquireDecision(acquired: false, portId: "p1", clientIp: "127.0.0.1");

        Assert.False(decision.ShouldApplyViewState);
        Assert.False(decision.ShouldClearRemoteButtons);
        Assert.Empty(decision.ToastMessages);
    }

    [Fact]
    public void BuildReleaseDecision_WhenRemoteOwned_AppendsRestoredAndReasonToasts()
    {
        var controller = new GameWindowRemoteControlWorkflowController(new GameWindowRemoteControlStateController());

        var decision = controller.BuildReleaseDecision(portId: "p2", hadRemoteControl: true, reason: "remote disconnected");

        Assert.True(decision.ShouldApplyViewState);
        Assert.True(decision.ShouldClearRemoteButtons);
        Assert.True(decision.ShouldRebuildCombinedState);
        Assert.True(decision.ShouldRefreshLocalInput);
        Assert.Collection(
            decision.ToastMessages,
            message => Assert.Equal("2P 已恢复本地控制", message),
            message => Assert.Equal("remote disconnected", message));
    }

    [Fact]
    public void BuildReleaseDecision_WhenAlreadyLocal_StillResyncsWithoutToast()
    {
        var controller = new GameWindowRemoteControlWorkflowController(new GameWindowRemoteControlStateController());

        var decision = controller.BuildReleaseDecision(portId: "p1", hadRemoteControl: false, reason: "ignored");

        Assert.True(decision.ShouldApplyViewState);
        Assert.True(decision.ShouldClearRemoteButtons);
        Assert.True(decision.ShouldRebuildCombinedState);
        Assert.True(decision.ShouldRefreshLocalInput);
        Assert.Empty(decision.ToastMessages);
    }

    [Fact]
    public void BuildButtonStateDecision_WhenAuthorized_KeepsSummaryStable()
    {
        var controller = new GameWindowRemoteControlWorkflowController(new GameWindowRemoteControlStateController());

        var decision = controller.BuildButtonStateDecision(authorized: true);

        Assert.False(decision.ShouldApplyViewState);
        Assert.True(decision.ShouldApplyRequestedRemoteButtonState);
        Assert.False(decision.ShouldClearRemoteButtons);
        Assert.Empty(decision.ToastMessages);
    }
}
