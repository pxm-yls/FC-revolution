using System;
using FCRevolution.Contracts.RemoteControl;

namespace FC_Revolution.UI.AppServices;

public sealed class SessionRemoteControlService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly Action<string> _reportStatus;

    public SessionRemoteControlService(IGameSessionService gameSessionService, Action<string> reportStatus)
    {
        _gameSessionService = gameSessionService;
        _reportStatus = reportStatus;
    }

    public bool ClaimControl(Guid sessionId, ClaimControlRequest request)
    {
        if (!TryResolvePlayer(request.PortId, request.Player, out var player))
            return false;

        var claimed = _gameSessionService.TryAcquireRemoteControl(sessionId, player, request.ClientIp, request.ClientName);
        if (claimed)
            _reportStatus($"已分配远程控制: {sessionId} / {GetPlayerLabel(player)}");
        return claimed;
    }

    public void ReleaseControl(Guid sessionId, ReleaseControlRequest request)
    {
        if (!TryResolvePlayer(request.PortId, request.Player, out var player))
            return;

        _gameSessionService.ReleaseRemoteControl(sessionId, player, request.Reason);
        _reportStatus($"已释放远程控制: {sessionId} / {GetPlayerLabel(player)}");
    }

    public void RefreshHeartbeat(Guid sessionId, RefreshHeartbeatRequest request)
    {
        if (!TryResolvePlayer(request.PortId, request.Player, out var player))
            return;

        _gameSessionService.RefreshRemoteHeartbeat(sessionId, player);
    }

    public bool SetInputState(Guid sessionId, SetInputStateRequest request)
    {
        var allApplied = true;
        foreach (var action in request.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.ActionId) ||
                !TryResolvePortId(action.PortId, fallbackPlayer: null, out var portId))
            {
                return false;
            }

            if (!_gameSessionService.TrySetRemoteInputState(sessionId, portId, action.ActionId.Trim(), action.Value))
                allApplied = false;
        }

        return allApplied;
    }

    private static bool TryResolvePortId(string? portId, int? fallbackPlayer, out string resolvedPortId)
    {
        if (!string.IsNullOrWhiteSpace(portId))
        {
            var normalizedPortId = RemoteControlPorts.NormalizePortId(portId);
            if (normalizedPortId != null)
            {
                resolvedPortId = normalizedPortId;
                return true;
            }

            resolvedPortId = string.Empty;
            return false;
        }

        if (fallbackPlayer is { } value && RemoteControlPorts.TryGetPortId(value, out resolvedPortId))
            return true;

        resolvedPortId = string.Empty;
        return false;
    }

    private static bool TryResolvePlayer(string? portId, int? fallbackPlayer, out int player)
    {
        if (!string.IsNullOrWhiteSpace(portId))
        {
            var normalizedPortId = RemoteControlPorts.NormalizePortId(portId);
            if (normalizedPortId != null)
            {
                player = normalizedPortId == RemoteControlPorts.Player1 ? 0 : 1;
                return true;
            }

            player = default;
            return false;
        }

        if (fallbackPlayer is { } value && RemoteControlPorts.IsSupportedPlayer(value))
        {
            player = value;
            return true;
        }

        player = default;
        return false;
    }

    private static string GetPlayerLabel(int player) => player switch
    {
        0 => "1P",
        1 => "2P",
        _ => $"P{player + 1}"
    };
}
