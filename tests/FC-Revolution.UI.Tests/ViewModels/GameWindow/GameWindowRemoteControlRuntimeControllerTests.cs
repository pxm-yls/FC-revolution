using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRemoteControlRuntimeControllerTests
{
    [Fact]
    public void TryAcquire_UpdatesViewState_AndExposesOwner()
    {
        var controller = new GameWindowRemoteControlRuntimeController(new GameWindowRemoteControlStateController());

        var acquired = controller.TryAcquire(0, "127.0.0.1", "remote-client", DateTime.UtcNow, out var viewState);

        Assert.True(acquired);
        Assert.Equal(GamePlayerControlSource.Remote, viewState.Player1ControlSource);
        Assert.Equal(GamePlayerControlSource.Local, viewState.Player2ControlSource);
        Assert.Contains("1P 正通过 remote-client (127.0.0.1) 网页控制", viewState.RemoteControlStatusText);
        Assert.True(controller.IsRemoteOwner(0, "127.0.0.1", "remote-client"));
        Assert.False(controller.IsRemoteOwner(0, "127.0.0.2", "remote-client"));
    }

    [Fact]
    public void TryAuthorizeRemoteButtonState_RequiresMatchingRemoteOwner()
    {
        var controller = new GameWindowRemoteControlRuntimeController(new GameWindowRemoteControlStateController());
        controller.TryAcquire(0, "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        Assert.True(controller.TryAuthorizeRemoteButtonState(0, "127.0.0.1", "remote-client", DateTime.UtcNow, out _));
        Assert.False(controller.TryAuthorizeRemoteButtonState(0, "127.0.0.2", "remote-client", DateTime.UtcNow, out _));
        Assert.False(controller.TryAuthorizeRemoteButtonState(1, "127.0.0.1", "remote-client", DateTime.UtcNow, out _));
    }

    [Fact]
    public void TryRelease_RestoresLocalControl_AndClearsStatus()
    {
        var controller = new GameWindowRemoteControlRuntimeController(new GameWindowRemoteControlStateController());
        controller.TryAcquire(1, "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        var released = controller.TryRelease(1, out var hadRemoteControl, out var viewState);

        Assert.True(released);
        Assert.True(hadRemoteControl);
        Assert.Equal(GamePlayerControlSource.Local, viewState.Player1ControlSource);
        Assert.Equal(GamePlayerControlSource.Local, viewState.Player2ControlSource);
        Assert.Equal(string.Empty, viewState.RemoteControlStatusText);
        Assert.Equal(GamePlayerControlSource.Local, controller.GetPlayerControlSource(1));
    }

    [Fact]
    public void TryAcquire_RejectsDifferentOwner_ButAllowsSameOwnerReacquire()
    {
        var controller = new GameWindowRemoteControlRuntimeController(new GameWindowRemoteControlStateController());
        _ = controller.TryAcquire(0, "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        var differentOwnerAcquire = controller.TryAcquire(0, "127.0.0.2", "remote-client", DateTime.UtcNow, out _);
        var sameOwnerAcquire = controller.TryAcquire(0, "127.0.0.1", "remote-client", DateTime.UtcNow, out var viewState);

        Assert.False(differentOwnerAcquire);
        Assert.True(sameOwnerAcquire);
        Assert.Equal(GamePlayerControlSource.Remote, viewState.Player1ControlSource);
        Assert.Contains("remote-client (127.0.0.1)", viewState.RemoteControlStatusText);
    }

    [Fact]
    public void TryRefreshHeartbeat_UnsupportedPlayer_ReturnsFalseWithoutChangingViewState()
    {
        var controller = new GameWindowRemoteControlRuntimeController(new GameWindowRemoteControlStateController());
        _ = controller.TryAcquire(0, "127.0.0.1", "remote-client", DateTime.UtcNow, out _);

        var refreshed = controller.TryRefreshHeartbeat(2, DateTime.UtcNow, out var viewState);

        Assert.False(refreshed);
        Assert.Equal(GamePlayerControlSource.Remote, viewState.Player1ControlSource);
        Assert.Contains("remote-client (127.0.0.1)", viewState.RemoteControlStatusText);
    }
}
