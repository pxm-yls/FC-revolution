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
        if (!TryResolvePortId(request.PortId, out var portId))
            return false;

        var claimed = _gameSessionService.TryAcquireRemoteControl(sessionId, portId, request.ClientIp, request.ClientName);
        if (claimed)
            _reportStatus($"已分配远程控制: {sessionId} / {GetPortLabel(portId)}");
        return claimed;
    }

    public void ReleaseControl(Guid sessionId, ReleaseControlRequest request)
    {
        if (!TryResolvePortId(request.PortId, out var portId))
            return;

        _gameSessionService.ReleaseRemoteControl(sessionId, portId, request.Reason);
        _reportStatus($"已释放远程控制: {sessionId} / {GetPortLabel(portId)}");
    }

    public void RefreshHeartbeat(Guid sessionId, RefreshHeartbeatRequest request)
    {
        if (!TryResolvePortId(request.PortId, out var portId))
            return;

        _gameSessionService.RefreshRemoteHeartbeat(sessionId, portId);
    }

    public bool SetInputState(Guid sessionId, SetInputStateRequest request)
    {
        var allApplied = true;
        foreach (var action in request.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.ActionId) ||
                !TryResolvePortId(action.PortId, out var portId))
            {
                return false;
            }

            if (!_gameSessionService.TrySetRemoteInputState(sessionId, portId, action.ActionId.Trim(), action.Value))
                allApplied = false;
        }

        return allApplied;
    }

    private static bool TryResolvePortId(string? portId, out string resolvedPortId)
    {
        resolvedPortId = string.IsNullOrWhiteSpace(portId) ? string.Empty : portId.Trim();
        return !string.IsNullOrWhiteSpace(resolvedPortId);
    }

    private static string GetPortLabel(string portId) => portId switch
    {
        "p1" => "1P",
        "p2" => "2P",
        _ => portId
    };
}
