using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRemoteControlRuntimeControllerTests
{
    [Fact]
    public void TryAcquire_UpdatesViewState_AndExposesOwner()
    {
        var controller = new GameWindowRemoteControlRuntimeController(GameWindowRemoteControlStateControllerTestsAccessor.CreateDefault());

        var acquired = controller.TryAcquire("p1", "127.0.0.1", "remote-client", DateTime.UtcNow, out var viewState);

        Assert.True(acquired);
        Assert.Contains("1P 正通过 remote-client (127.0.0.1) 网页控制", viewState.RemoteControlStatusText);
        Assert.Equal(GamePlayerControlSource.Remote, controller.GetPortControlSource("p1"));
        Assert.Equal(GamePlayerControlSource.Local, controller.GetPortControlSource("p2"));
        Assert.True(controller.IsRemoteOwner("p1", "127.0.0.1", "remote-client"));
        Assert.False(controller.IsRemoteOwner("p1", "127.0.0.2", "remote-client"));
    }

    [Fact]
    public void TryAuthorizeRemoteButtonState_RequiresMatchingRemoteOwner()
    {
        var controller = new GameWindowRemoteControlRuntimeController(GameWindowRemoteControlStateControllerTestsAccessor.CreateDefault());
        controller.TryAcquire("p1", "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        Assert.True(controller.TryAuthorizeRemoteButtonState("p1", "127.0.0.1", "remote-client", DateTime.UtcNow, out _));
        Assert.False(controller.TryAuthorizeRemoteButtonState("p1", "127.0.0.2", "remote-client", DateTime.UtcNow, out _));
        Assert.False(controller.TryAuthorizeRemoteButtonState("p2", "127.0.0.1", "remote-client", DateTime.UtcNow, out _));
    }

    [Fact]
    public void TryRelease_RestoresLocalControl_AndClearsStatus()
    {
        var controller = new GameWindowRemoteControlRuntimeController(GameWindowRemoteControlStateControllerTestsAccessor.CreateDefault());
        controller.TryAcquire("p2", "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        var released = controller.TryRelease("p2", out var hadRemoteControl, out var viewState);

        Assert.True(released);
        Assert.True(hadRemoteControl);
        Assert.Equal(string.Empty, viewState.RemoteControlStatusText);
        Assert.Equal(GamePlayerControlSource.Local, controller.GetPortControlSource("p2"));
    }

    [Fact]
    public void TryAcquire_RejectsDifferentOwner_ButAllowsSameOwnerReacquire()
    {
        var controller = new GameWindowRemoteControlRuntimeController(GameWindowRemoteControlStateControllerTestsAccessor.CreateDefault());
        _ = controller.TryAcquire("p1", "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        var differentOwnerAcquire = controller.TryAcquire("p1", "127.0.0.2", "remote-client", DateTime.UtcNow, out _);
        var sameOwnerAcquire = controller.TryAcquire("p1", "127.0.0.1", "remote-client", DateTime.UtcNow, out var viewState);

        Assert.False(differentOwnerAcquire);
        Assert.True(sameOwnerAcquire);
        Assert.Equal(GamePlayerControlSource.Remote, controller.GetPortControlSource("p1"));
        Assert.Contains("remote-client (127.0.0.1)", viewState.RemoteControlStatusText);
    }

    [Fact]
    public void TryRefreshHeartbeat_UnsupportedPort_ReturnsFalseWithoutChangingViewState()
    {
        var controller = new GameWindowRemoteControlRuntimeController(GameWindowRemoteControlStateControllerTestsAccessor.CreateDefault());
        _ = controller.TryAcquire("p1", "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        var refreshed = controller.TryRefreshHeartbeat("pad-west", DateTime.UtcNow, out var viewState);

        Assert.False(refreshed);
        Assert.Contains("remote-client (127.0.0.1)", viewState.RemoteControlStatusText);
    }
}
