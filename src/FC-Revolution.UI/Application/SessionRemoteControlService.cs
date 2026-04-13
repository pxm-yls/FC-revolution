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
        if (!TryResolvePortId(request.PortId, request.Player, out var portId))
            return false;

        var claimed = _gameSessionService.TryAcquireRemoteControl(sessionId, portId, request.ClientIp, request.ClientName);
        if (claimed)
            _reportStatus($"已分配远程控制: {sessionId} / {GetPortLabel(portId)}");
        return claimed;
    }

    public void ReleaseControl(Guid sessionId, ReleaseControlRequest request)
    {
        if (!TryResolvePortId(request.PortId, request.Player, out var portId))
            return;

        _gameSessionService.ReleaseRemoteControl(sessionId, portId, request.Reason);
        _reportStatus($"已释放远程控制: {sessionId} / {GetPortLabel(portId)}");
    }

    public void RefreshHeartbeat(Guid sessionId, RefreshHeartbeatRequest request)
    {
        if (!TryResolvePortId(request.PortId, request.Player, out var portId))
            return;

        _gameSessionService.RefreshRemoteHeartbeat(sessionId, portId);
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
            resolvedPortId = portId.Trim();
            return true;
        }

        if (TryMapCompatibilityPlayer(fallbackPlayer, out resolvedPortId))
            return true;

        resolvedPortId = string.Empty;
        return false;
    }

    private static bool TryMapCompatibilityPlayer(int? player, out string portId)
    {
        if (player is 0)
        {
            portId = "p1";
            return true;
        }

        if (player is 1)
        {
            portId = "p2";
            return true;
        }

        portId = string.Empty;
        return false;
    }

    private static string GetPortLabel(string portId) => portId switch
    {
        "p1" => "1P",
        "p2" => "2P",
        _ => portId
    };
}
