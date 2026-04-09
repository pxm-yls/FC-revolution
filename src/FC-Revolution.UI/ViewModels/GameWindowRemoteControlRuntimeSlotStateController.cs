using System;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowRemoteControlRuntimeSlotStateController
{
    private GameWindowRemoteControlRuntimeSlot _player1Slot = new(GamePlayerControlSource.Local, null, null, null);
    private GameWindowRemoteControlRuntimeSlot _player2Slot = new(GamePlayerControlSource.Local, null, null, null);

    public GamePlayerControlSource GetPlayerControlSource(int player) =>
        player == 0 ? _player1Slot.ControlSource : _player2Slot.ControlSource;

    public GameWindowRemoteControlRuntimeViewState BuildViewState(GameWindowRemoteControlStateController stateController)
    {
        return new GameWindowRemoteControlRuntimeViewState(
            _player1Slot.ControlSource,
            _player2Slot.ControlSource,
            stateController.BuildRemoteControlStatusText(
                ToSlotState(_player1Slot),
                ToSlotState(_player2Slot)));
    }

    public bool TryGetSlotState(
        GameWindowRemoteControlStateController stateController,
        int player,
        out GameWindowRemoteControlSlotState slotState)
    {
        if (!stateController.IsSupportedPlayer(player))
        {
            slotState = default;
            return false;
        }

        slotState = ToSlotState(player == 0 ? _player1Slot : _player2Slot);
        return true;
    }

    public void SetRemoteOwner(int player, string clientIp, string? clientName, DateTime heartbeatUtc) =>
        SetSlot(player, GamePlayerControlSource.Remote, clientIp, clientName, heartbeatUtc);

    public bool TryReleaseToLocal(
        GameWindowRemoteControlStateController stateController,
        int player,
        out bool hadRemoteControl)
    {
        if (!stateController.IsSupportedPlayer(player))
        {
            hadRemoteControl = false;
            return false;
        }

        hadRemoteControl = GetPlayerControlSource(player) == GamePlayerControlSource.Remote;
        SetSlot(player, GamePlayerControlSource.Local, null, null, null);
        return true;
    }

    public bool TrySetHeartbeat(
        GameWindowRemoteControlStateController stateController,
        int player,
        DateTime heartbeatUtc)
    {
        if (!stateController.IsSupportedPlayer(player))
            return false;

        if (player == 0)
            _player1Slot = _player1Slot with { HeartbeatUtc = heartbeatUtc };
        else
            _player2Slot = _player2Slot with { HeartbeatUtc = heartbeatUtc };
        return true;
    }

    private void SetSlot(
        int player,
        GamePlayerControlSource controlSource,
        string? clientIp,
        string? clientName,
        DateTime? heartbeatUtc)
    {
        if (player == 0)
            _player1Slot = new GameWindowRemoteControlRuntimeSlot(controlSource, clientIp, clientName, heartbeatUtc);
        else
            _player2Slot = new GameWindowRemoteControlRuntimeSlot(controlSource, clientIp, clientName, heartbeatUtc);
    }

    private static GameWindowRemoteControlSlotState ToSlotState(GameWindowRemoteControlRuntimeSlot slot) =>
        new(slot.ControlSource, slot.ClientIp, slot.ClientName);
}
