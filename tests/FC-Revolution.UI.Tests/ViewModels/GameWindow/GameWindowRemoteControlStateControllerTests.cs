using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRemoteControlStateControllerTests
{
    [Fact]
    public void IsSupportedPlayer_ReturnsTrueOnlyForPlayer1AndPlayer2()
    {
        var controller = new GameWindowRemoteControlStateController();

        Assert.True(controller.IsSupportedPlayer(0));
        Assert.True(controller.IsSupportedPlayer(1));
        Assert.False(controller.IsSupportedPlayer(-1));
        Assert.False(controller.IsSupportedPlayer(2));
    }

    [Fact]
    public void CanAcquireRemoteControl_RejectsDifferentOwnerWhenAlreadyRemote()
    {
        var controller = new GameWindowRemoteControlStateController();
        var remoteSlot = new GameWindowRemoteControlSlotState(
            GamePlayerControlSource.Remote,
            ClientIp: "192.168.1.8",
            ClientName: "Pad");

        Assert.False(controller.CanAcquireRemoteControl(remoteSlot, "192.168.1.9", "Pad"));
        Assert.True(controller.CanAcquireRemoteControl(remoteSlot, "192.168.1.8", "pad"));
    }

    [Fact]
    public void CanApplyRemoteButtonState_RequiresRemoteControlAndMatchingOwnerWhenIpProvided()
    {
        var controller = new GameWindowRemoteControlStateController();
        var localSlot = new GameWindowRemoteControlSlotState(GamePlayerControlSource.Local, null, null);
        var remoteSlot = new GameWindowRemoteControlSlotState(
            GamePlayerControlSource.Remote,
            ClientIp: "10.0.0.2",
            ClientName: "Chrome");

        Assert.False(controller.CanApplyRemoteButtonState(localSlot, clientIp: null, clientName: null));
        Assert.True(controller.CanApplyRemoteButtonState(remoteSlot, clientIp: null, clientName: null));
        Assert.True(controller.CanApplyRemoteButtonState(remoteSlot, "10.0.0.2", "chrome"));
        Assert.False(controller.CanApplyRemoteButtonState(remoteSlot, "10.0.0.3", "chrome"));
    }

    [Fact]
    public void BuildRemoteControlStatusText_FormatsRemoteClientsAndIgnoresLocalPlayers()
    {
        var controller = new GameWindowRemoteControlStateController();
        var player1 = new GameWindowRemoteControlSlotState(
            GamePlayerControlSource.Remote,
            ClientIp: "10.0.0.2",
            ClientName: "Tablet");
        var player2 = new GameWindowRemoteControlSlotState(
            GamePlayerControlSource.Remote,
            ClientIp: null,
            ClientName: null);

        var status = controller.BuildRemoteControlStatusText(player1, player2);

        Assert.Equal("1P 正通过 Tablet (10.0.0.2) 网页控制 | 2P 正通过 未知设备 网页控制", status);
    }

    [Fact]
    public void BuildToastText_ReturnsExpectedConnectedAndRestoredMessages()
    {
        var controller = new GameWindowRemoteControlStateController();

        Assert.Equal("1P 已切换为 127.0.0.1 网页控制", controller.BuildRemoteConnectedToast(0, "127.0.0.1"));
        Assert.Equal("2P 已恢复本地控制", controller.BuildLocalControlRestoredToast(1));
    }
}
