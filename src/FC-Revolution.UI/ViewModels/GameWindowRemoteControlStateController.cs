using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowRemoteControlSlotState(
    GamePlayerControlSource ControlSource,
    string? ClientIp,
    string? ClientName);

internal sealed class GameWindowRemoteControlStateController
{
    private readonly IReadOnlyList<InputPortDescriptor> _supportedPorts;
    private readonly IReadOnlyDictionary<string, InputPortDescriptor> _portsById;

    public GameWindowRemoteControlStateController(IReadOnlyList<InputPortDescriptor>? supportedPorts = null)
    {
        var normalizedPorts = (supportedPorts ?? Array.Empty<InputPortDescriptor>())
            .Where(static port => !string.IsNullOrWhiteSpace(port.PortId))
            .Select(static port => new InputPortDescriptor(port.PortId.Trim(), ResolveDisplayName(port), port.PlayerIndex))
            .GroupBy(static port => port.PortId, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static port => port.PlayerIndex)
            .ThenBy(static port => port.PortId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _supportedPorts = normalizedPorts;
        _portsById = new ReadOnlyDictionary<string, InputPortDescriptor>(
            normalizedPorts.ToDictionary(static port => port.PortId, StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<InputPortDescriptor> GetSupportedPorts() => _supportedPorts;

    public bool TryNormalizePortId(string? portId, out string normalizedPortId)
    {
        normalizedPortId = string.Empty;
        if (string.IsNullOrWhiteSpace(portId) ||
            !_portsById.TryGetValue(portId.Trim(), out var port))
        {
            return false;
        }

        normalizedPortId = port.PortId;
        return true;
    }

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

    public string BuildRemoteControlStatusText(IReadOnlyDictionary<string, GameWindowRemoteControlSlotState> slotStates)
    {
        var statuses = new List<string>(_supportedPorts.Count);
        foreach (var port in _supportedPorts)
        {
            if (!slotStates.TryGetValue(port.PortId, out var slotState))
                continue;

            AppendRemoteStatus(statuses, port, slotState);
        }

        return string.Join(" | ", statuses);
    }

    public string BuildRemoteConnectedToast(string portId, string clientIp) =>
        $"{GetPortLabel(portId)} 已切换为 {clientIp} 网页控制";

    public string BuildLocalControlRestoredToast(string portId) =>
        $"{GetPortLabel(portId)} 已恢复本地控制";

    private static void AppendRemoteStatus(
        ICollection<string> statuses,
        InputPortDescriptor port,
        GameWindowRemoteControlSlotState slotState)
    {
        if (slotState.ControlSource != GamePlayerControlSource.Remote)
            return;

        statuses.Add($"{ResolveDisplayName(port)} 正通过 {GetRemoteClientDisplay(slotState)} 网页控制");
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

    private string GetPortLabel(string portId) =>
        _portsById.TryGetValue(portId, out var port)
            ? ResolveDisplayName(port)
            : portId;

    private static string ResolveDisplayName(InputPortDescriptor port) =>
        string.IsNullOrWhiteSpace(port.DisplayName) ? port.PortId : port.DisplayName.Trim();
}
