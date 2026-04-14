using System.Collections.Generic;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRemoteControlStateControllerTests
{
    [Fact]
    public void CanAcquireRemoteControl_RejectsDifferentOwnerWhenAlreadyRemote()
    {
        var controller = CreateController();
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
        var controller = CreateController();
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
        var controller = CreateController();
        var player1 = new GameWindowRemoteControlSlotState(
            GamePlayerControlSource.Remote,
            ClientIp: "10.0.0.2",
            ClientName: "Tablet");
        var player2 = new GameWindowRemoteControlSlotState(
            GamePlayerControlSource.Remote,
            ClientIp: null,
            ClientName: null);

        var status = controller.BuildRemoteControlStatusText(
            new Dictionary<string, GameWindowRemoteControlSlotState>
            {
                ["p1"] = player1,
                ["p2"] = player2
            });

        Assert.Equal("1P 正通过 Tablet (10.0.0.2) 网页控制 | 2P 正通过 未知设备 网页控制", status);
    }

    [Fact]
    public void TryNormalizePortId_ResolvesKnownPorts_AndRejectsUnknownPorts()
    {
        var controller = CreateController();

        Assert.True(controller.TryNormalizePortId(" P2 ", out var normalizedPortId));
        Assert.Equal("p2", normalizedPortId);
        Assert.False(controller.TryNormalizePortId("pad-west", out _));
    }

    [Fact]
    public void BuildToastText_ReturnsExpectedConnectedAndRestoredMessages()
    {
        var controller = CreateController();

        Assert.Equal("1P 已切换为 127.0.0.1 网页控制", controller.BuildRemoteConnectedToast("p1", "127.0.0.1"));
        Assert.Equal("2P 已恢复本地控制", controller.BuildLocalControlRestoredToast("p2"));
    }

    [Fact]
    public void BuildRemoteControlStatusText_UsesProvidedSchemaPortLabels()
    {
        var controller = new GameWindowRemoteControlStateController(
        [
            new("pad-west", "West Pad", 4),
            new("pad-east", "East Pad", 7)
        ]);

        var status = controller.BuildRemoteControlStatusText(
            new Dictionary<string, GameWindowRemoteControlSlotState>
            {
                ["pad-west"] = new(GamePlayerControlSource.Remote, "10.0.0.9", "Tablet")
            });

        Assert.Equal("West Pad 正通过 Tablet (10.0.0.9) 网页控制", status);
    }

    private static GameWindowRemoteControlStateController CreateController() =>
        new(
        [
            new InputPortDescriptor("p1", "1P", 0),
            new InputPortDescriptor("p2", "2P", 1)
        ]);
}
