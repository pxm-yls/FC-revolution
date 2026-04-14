using System;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowRemoteControlRuntimeViewState(
    string RemoteControlStatusText);

internal readonly record struct GameWindowRemoteControlRuntimeSlot(
    GamePlayerControlSource ControlSource,
    string? ClientIp,
    string? ClientName,
    DateTime? HeartbeatUtc);

internal sealed class GameWindowRemoteControlRuntimeController
{
    private readonly GameWindowRemoteControlStateController _stateController;
    private readonly GameWindowRemoteControlRuntimeSlotStateController _slotStateController;

    public GameWindowRemoteControlRuntimeController(GameWindowRemoteControlStateController stateController)
    {
        ArgumentNullException.ThrowIfNull(stateController);
        _stateController = stateController;
        _slotStateController = new GameWindowRemoteControlRuntimeSlotStateController(stateController.GetSupportedPorts());
    }

    public GameWindowRemoteControlRuntimeViewState CurrentViewState => BuildViewState();

    public bool TryAcquire(
        string portId,
        string clientIp,
        string? clientName,
        DateTime heartbeatUtc,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        if (!_stateController.TryNormalizePortId(portId, out var normalizedPortId) ||
            !_slotStateController.TryGetSlotState(normalizedPortId, out var slotState) ||
            !_stateController.CanAcquireRemoteControl(slotState, clientIp, clientName))
        {
            viewState = BuildViewState();
            return false;
        }

        _slotStateController.SetRemoteOwner(normalizedPortId, clientIp, clientName, heartbeatUtc);
        viewState = BuildViewState();
        return true;
    }

    public bool TryRelease(
        string portId,
        out bool hadRemoteControl,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        hadRemoteControl = false;
        if (!_stateController.TryNormalizePortId(portId, out var normalizedPortId) ||
            !_slotStateController.TryReleaseToLocal(normalizedPortId, out hadRemoteControl))
        {
            viewState = BuildViewState();
            return false;
        }

        viewState = BuildViewState();
        return true;
    }

    public bool TryRefreshHeartbeat(
        string portId,
        DateTime heartbeatUtc,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        if (!_stateController.TryNormalizePortId(portId, out var normalizedPortId) ||
            !_slotStateController.TrySetHeartbeat(normalizedPortId, heartbeatUtc))
        {
            viewState = BuildViewState();
            return false;
        }

        viewState = BuildViewState();
        return true;
    }

    public bool TryAuthorizeRemoteButtonState(
        string portId,
        string? clientIp,
        string? clientName,
        DateTime heartbeatUtc,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        if (!_stateController.TryNormalizePortId(portId, out var normalizedPortId) ||
            !_slotStateController.TryGetSlotState(normalizedPortId, out var slotState) ||
            !_stateController.CanApplyRemoteButtonState(slotState, clientIp, clientName))
        {
            viewState = BuildViewState();
            return false;
        }

        _ = _slotStateController.TrySetHeartbeat(normalizedPortId, heartbeatUtc);
        viewState = BuildViewState();
        return true;
    }

    public bool IsRemoteOwner(string portId, string clientIp, string? clientName)
    {
        return _stateController.TryNormalizePortId(portId, out var normalizedPortId) &&
               _slotStateController.TryGetSlotState(normalizedPortId, out var slotState) &&
               _stateController.IsRemoteOwner(slotState, clientIp, clientName);
    }

    public GamePlayerControlSource GetPortControlSource(string portId) =>
        _stateController.TryNormalizePortId(portId, out var normalizedPortId)
            ? _slotStateController.GetPortControlSource(normalizedPortId)
            : GamePlayerControlSource.Local;

    private GameWindowRemoteControlRuntimeViewState BuildViewState()
    {
        return _slotStateController.BuildViewState(_stateController);
    }
}
