using System;
using System.Collections.Generic;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowRemoteControlWorkflowDecision(
    bool ShouldApplyViewState,
    bool ShouldClearRemoteButtons,
    bool ShouldRebuildCombinedState,
    bool ShouldRefreshLocalInput,
    bool ShouldApplyRequestedRemoteButtonState,
    IReadOnlyList<string> ToastMessages);

internal sealed class GameWindowRemoteControlWorkflowController
{
    private readonly GameWindowRemoteControlStateController _stateController;

    public GameWindowRemoteControlWorkflowController(GameWindowRemoteControlStateController stateController)
    {
        ArgumentNullException.ThrowIfNull(stateController);
        _stateController = stateController;
    }

    public GameWindowRemoteControlWorkflowDecision BuildAcquireDecision(bool acquired, int player, string clientIp)
    {
        if (!acquired)
            return new(false, false, false, false, false, []);

        return new(
            ShouldApplyViewState: true,
            ShouldClearRemoteButtons: true,
            ShouldRebuildCombinedState: true,
            ShouldRefreshLocalInput: false,
            ShouldApplyRequestedRemoteButtonState: false,
            ToastMessages: [_stateController.BuildRemoteConnectedToast(player, clientIp)]);
    }

    public GameWindowRemoteControlWorkflowDecision BuildReleaseDecision(int player, bool hadRemoteControl, string? reason)
    {
        List<string> toastMessages = [];
        if (hadRemoteControl)
        {
            toastMessages.Add(_stateController.BuildLocalControlRestoredToast(player));
            if (!string.IsNullOrWhiteSpace(reason))
                toastMessages.Add(reason!);
        }

        return new(
            ShouldApplyViewState: true,
            ShouldClearRemoteButtons: true,
            ShouldRebuildCombinedState: true,
            ShouldRefreshLocalInput: true,
            ShouldApplyRequestedRemoteButtonState: false,
            ToastMessages: toastMessages);
    }

    public GameWindowRemoteControlWorkflowDecision BuildHeartbeatDecision(bool refreshed) =>
        refreshed
            ? new(
                ShouldApplyViewState: true,
                ShouldClearRemoteButtons: false,
                ShouldRebuildCombinedState: false,
                ShouldRefreshLocalInput: false,
                ShouldApplyRequestedRemoteButtonState: false,
                ToastMessages: [])
            : new(false, false, false, false, false, []);

    public GameWindowRemoteControlWorkflowDecision BuildButtonStateDecision(bool authorized) =>
        authorized
            ? new(
                ShouldApplyViewState: true,
                ShouldClearRemoteButtons: false,
                ShouldRebuildCombinedState: false,
                ShouldRefreshLocalInput: false,
                ShouldApplyRequestedRemoteButtonState: true,
                ToastMessages: [])
            : new(false, false, false, false, false, []);
}
