using System;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowRemoteControlRuntimeViewState(
    GamePlayerControlSource Player1ControlSource,
    GamePlayerControlSource Player2ControlSource,
    string RemoteControlStatusText);

internal readonly record struct GameWindowRemoteControlRuntimeSlot(
    GamePlayerControlSource ControlSource,
    string? ClientIp,
    string? ClientName,
    DateTime? HeartbeatUtc);

internal sealed class GameWindowRemoteControlRuntimeController
{
    private readonly GameWindowRemoteControlStateController _stateController;
    private readonly GameWindowRemoteControlRuntimeSlotStateController _slotStateController = new();

    public GameWindowRemoteControlRuntimeController(GameWindowRemoteControlStateController stateController)
    {
        ArgumentNullException.ThrowIfNull(stateController);
        _stateController = stateController;
    }

    public GameWindowRemoteControlRuntimeViewState CurrentViewState => BuildViewState();

    public bool TryAcquire(
        int player,
        string clientIp,
        string? clientName,
        DateTime heartbeatUtc,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        if (!_slotStateController.TryGetSlotState(_stateController, player, out var slotState) ||
            !_stateController.CanAcquireRemoteControl(slotState, clientIp, clientName))
        {
            viewState = BuildViewState();
            return false;
        }

        _slotStateController.SetRemoteOwner(player, clientIp, clientName, heartbeatUtc);
        viewState = BuildViewState();
        return true;
    }

    public bool TryRelease(
        int player,
        out bool hadRemoteControl,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        if (!_slotStateController.TryReleaseToLocal(_stateController, player, out hadRemoteControl))
        {
            viewState = BuildViewState();
            return false;
        }

        viewState = BuildViewState();
        return true;
    }

    public bool TryRefreshHeartbeat(
        int player,
        DateTime heartbeatUtc,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        if (!_slotStateController.TrySetHeartbeat(_stateController, player, heartbeatUtc))
        {
            viewState = BuildViewState();
            return false;
        }

        viewState = BuildViewState();
        return true;
    }

    public bool TryAuthorizeRemoteButtonState(
        int player,
        string? clientIp,
        string? clientName,
        DateTime heartbeatUtc,
        out GameWindowRemoteControlRuntimeViewState viewState)
    {
        if (!_slotStateController.TryGetSlotState(_stateController, player, out var slotState) ||
            !_stateController.CanApplyRemoteButtonState(slotState, clientIp, clientName))
        {
            viewState = BuildViewState();
            return false;
        }

        _ = _slotStateController.TrySetHeartbeat(_stateController, player, heartbeatUtc);
        viewState = BuildViewState();
        return true;
    }

    public bool IsRemoteOwner(int player, string clientIp, string? clientName)
    {
        return _slotStateController.TryGetSlotState(_stateController, player, out var slotState) &&
               _stateController.IsRemoteOwner(slotState, clientIp, clientName);
    }

    public GamePlayerControlSource GetPlayerControlSource(int player) =>
        _slotStateController.GetPlayerControlSource(player);

    private GameWindowRemoteControlRuntimeViewState BuildViewState()
    {
        return _slotStateController.BuildViewState(_stateController);
    }
}
