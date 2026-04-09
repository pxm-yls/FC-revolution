using System;
using System.Collections.Generic;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowRemoteControlSlotState(
    GamePlayerControlSource ControlSource,
    string? ClientIp,
    string? ClientName);

internal sealed class GameWindowRemoteControlStateController
{
    public bool IsSupportedPlayer(int player) => player is 0 or 1;

    public bool CanAcquireRemoteControl(
        GameWindowRemoteControlSlotState slotState,
        string clientIp,
        string? clientName)
    {
        if (slotState.ControlSource != GamePlayerControlSource.Remote)
            return true;

        return IsSameRemoteOwner(slotState, clientIp, clientName);
    }

    public bool CanApplyRemoteButtonState(
        GameWindowRemoteControlSlotState slotState,
        string? clientIp,
        string? clientName)
    {
        if (slotState.ControlSource != GamePlayerControlSource.Remote)
            return false;

        if (string.IsNullOrWhiteSpace(clientIp))
            return true;

        return IsSameRemoteOwner(slotState, clientIp, clientName);
    }

    public bool IsRemoteOwner(
        GameWindowRemoteControlSlotState slotState,
        string clientIp,
        string? clientName)
    {
        if (slotState.ControlSource != GamePlayerControlSource.Remote)
            return false;

        return IsSameRemoteOwner(slotState, clientIp, clientName);
    }

    public string BuildRemoteControlStatusText(
        GameWindowRemoteControlSlotState player1SlotState,
        GameWindowRemoteControlSlotState player2SlotState)
    {
        var statuses = new List<string>(2);
        AppendRemoteStatus(statuses, player: 0, player1SlotState);
        AppendRemoteStatus(statuses, player: 1, player2SlotState);
        return string.Join(" | ", statuses);
    }

    public string BuildRemoteConnectedToast(int player, string clientIp) =>
        $"{GetPlayerSlotLabel(player)} 已切换为 {clientIp} 网页控制";

    public string BuildLocalControlRestoredToast(int player) =>
        $"{GetPlayerSlotLabel(player)} 已恢复本地控制";

    private static void AppendRemoteStatus(
        ICollection<string> statuses,
        int player,
        GameWindowRemoteControlSlotState slotState)
    {
        if (slotState.ControlSource != GamePlayerControlSource.Remote)
            return;

        statuses.Add($"{GetPlayerSlotLabel(player)} 正通过 {GetRemoteClientDisplay(slotState)} 网页控制");
    }

    private static string GetRemoteClientDisplay(GameWindowRemoteControlSlotState slotState)
    {
        if (!string.IsNullOrWhiteSpace(slotState.ClientName) && !string.IsNullOrWhiteSpace(slotState.ClientIp))
            return $"{slotState.ClientName} ({slotState.ClientIp})";

        return string.IsNullOrWhiteSpace(slotState.ClientIp) ? "未知设备" : slotState.ClientIp;
    }

    private static bool IsSameRemoteOwner(
        GameWindowRemoteControlSlotState slotState,
        string clientIp,
        string? clientName)
    {
        return string.Equals(slotState.ClientIp, clientIp, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(slotState.ClientName ?? string.Empty, clientName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPlayerSlotLabel(int player) => player == 0 ? "1P" : "2P";
}
