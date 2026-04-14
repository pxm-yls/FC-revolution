using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class GameWindowViewModel
{
    public void ApplyShortcutBindings(IReadOnlyDictionary<string, ShortcutGesture> shortcutBindings)
    {
        _shortcutRouter.ApplyShortcutBindings(shortcutBindings);
        ShortcutHintText = _shortcutRouter.BuildShortcutHintText();
    }

    public void OnKeyDown(Key key) => OnKeyDown(key, KeyModifiers.None);

    public void OnKeyDown(Key key, KeyModifiers modifiers)
    {
        if (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameToggleInfoOverlay, key, modifiers))
        {
            IsOverlayVisible = !IsOverlayVisible;
            return;
        }

        if (_hasSessionFailure &&
            (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameQuickSave, key, modifiers) ||
             _shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameQuickLoad, key, modifiers) ||
             _shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameTogglePause, key, modifiers) ||
             _shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameRewind, key, modifiers) ||
             _shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameOpenDebugWindow, key, modifiers)))
        {
            ShowToast("当前游戏会话已终止，请关闭并重新打开游戏");
            return;
        }

        if (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameQuickSave, key, modifiers))
        {
            QuickSave();
            return;
        }

        if (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameQuickLoad, key, modifiers))
        {
            QuickLoad();
            return;
        }

        if (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameToggleBranchGallery, key, modifiers))
        {
            ToggleBranchGallery();
            return;
        }

        if (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameTogglePause, key, modifiers))
        {
            TogglePause();
            return;
        }

        if (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameRewind, key, modifiers))
        {
            _ = PlayRewindAsync(5);
            return;
        }

        if (_shortcutRouter.IsShortcutMatch(ShortcutCatalog.GameOpenDebugWindow, key, modifiers))
        {
            TryOpenDebugWindow();
            return;
        }

        if (GetHandledKeys().Contains(key))
        {
            _pressedKeys.Add(key);
            RefreshLocalInputState();
        }
    }

    public void OnKeyUp(Key key) => OnKeyUp(key, KeyModifiers.None);

    public void OnKeyUp(Key key, KeyModifiers modifiers)
    {
        if (_pressedKeys.Remove(key))
            RefreshLocalInputState();
    }

    public bool ShouldHandleKey(Key key) => ShouldHandleKey(key, KeyModifiers.None);

    public bool ShouldHandleKey(Key key, KeyModifiers modifiers) =>
        _shortcutRouter.ShouldHandleKey(key, modifiers) ||
        GetHandledKeys().Contains(key);

    public bool AcquireRemoteControl(string portId, string clientIp, string? clientName = null)
    {
        if (!_inputBindingSchema.TryResolvePort(portId, out var player, out var normalizedPortId) ||
            !_remoteControlRuntime.TryAcquire(normalizedPortId, clientIp, clientName, DateTime.UtcNow, out var viewState))
        {
            return false;
        }

        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildAcquireDecision(
                acquired: true,
                normalizedPortId,
                clientIp));
        return true;
    }

    public void ReleaseRemoteControl(string portId, string? reason = null)
    {
        if (!_inputBindingSchema.TryResolvePort(portId, out var player, out var normalizedPortId) ||
            !_remoteControlRuntime.TryRelease(normalizedPortId, out var hadRemoteControl, out var viewState))
        {
            return;
        }

        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildReleaseDecision(normalizedPortId, hadRemoteControl, reason));
    }

    public void RefreshRemoteHeartbeat(string portId)
    {
        if (!_inputBindingSchema.TryResolvePort(portId, out var player, out var normalizedPortId))
            return;

        var refreshed = _remoteControlRuntime.TryRefreshHeartbeat(normalizedPortId, DateTime.UtcNow, out var viewState);
        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildHeartbeatDecision(refreshed));
    }

    public bool SetRemoteInputState(string portId, string actionId, float value, string? clientIp = null, string? clientName = null)
    {
        if (!_inputBindingSchema.TryResolvePort(portId, out var player, out var normalizedPortId) ||
            string.IsNullOrWhiteSpace(actionId) ||
            !_inputBindingSchema.IsSupportedInputAction(normalizedPortId, actionId.Trim()))
        {
            return false;
        }

        var normalizedActionId = actionId.Trim();
        var authorized = _remoteControlRuntime.TryAuthorizeRemoteButtonState(
            normalizedPortId,
            clientIp,
            clientName,
            DateTime.UtcNow,
            out var viewState);

        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildButtonStateDecision(authorized));
        if (!authorized)
            return false;

        if (_inputBindingSchema.TryNormalizeActionId(player, normalizedActionId, out var canonicalActionId))
        {
            SetActionState(player, canonicalActionId, value >= 0.5f);
            return true;
        }

        return true;
    }

    public bool IsRemoteOwner(string portId, string clientIp, string? clientName = null) =>
        _inputBindingSchema.TryNormalizePortId(portId, out var normalizedPortId) &&
        _remoteControlRuntime.IsRemoteOwner(normalizedPortId, clientIp, clientName);

    public void ClearRemoteButtons(int player)
    {
        if (player is not 0 and not 1)
            return;

        ApplyInputStateChanges(_inputState.ClearRemoteButtons(player, CanAcceptLocalInput(player)));
    }

    private bool CanAcceptLocalInput(int player) =>
        _remoteControlRuntime.GetPortControlSource(_inputBindingSchema.GetPortId(player)) == GamePlayerControlSource.Local;

    private void SetActionState(int player, string actionId, bool pressed)
    {
        ApplyInputStateChanges(_inputState.SetRemoteActionState(
            player,
            actionId,
            pressed,
            CanAcceptLocalInput(player)));
    }

    private void RefreshLocalInputState()
    {
        var desiredActions = BuildDesiredLocalInputActions();
        ApplyInputStateChanges(_inputState.ApplyDesiredLocalInputActionsForPlayer(
            0,
            desiredActions.Player1Actions,
            CanAcceptLocalInput(0)));
        ApplyInputStateChanges(_inputState.ApplyDesiredLocalInputActionsForPlayer(
            1,
            desiredActions.Player2Actions,
            CanAcceptLocalInput(1)));
    }

    private GameWindowDesiredLocalInputActions BuildDesiredLocalInputActions() =>
        GameWindowLocalInputProjectionController.BuildDesiredLocalInputActions(
            _pressedKeys,
            _keyMap,
            _extraInputBindings,
            _turboTickCounters);

    private void ApplyCombinedStateForPlayer(int player)
    {
        ApplyInputStateChanges(_inputState.RebuildCombinedStateForPlayer(
            player,
            CanAcceptLocalInput(player)));
    }

    private void ApplyInputStateChanges(IReadOnlyList<GameWindowInputStateChange> changes)
    {
        foreach (var change in changes)
        {
            _sessionRuntime.SetInputState(
                _inputBindingSchema.GetPortId(change.Player),
                change.ActionId,
                change.Pressed ? 1f : 0f);
        }
    }

    private void UpdateTurboPulse()
    {
        var changed = false;
        var hasActiveTurbo = false;

        foreach (var binding in _extraInputBindings)
        {
            if (binding.Kind != ExtraInputBindingKind.Turbo)
                continue;

            if (!_pressedKeys.Contains(binding.Key))
            {
                _turboTickCounters.Remove(binding.Key);
                continue;
            }

            hasActiveTurbo = true;
            if (!_turboTickCounters.TryGetValue(binding.Key, out var ticks))
                ticks = 0;

            var periodTicks = Math.Max(1, 60 / Math.Clamp(binding.TurboHz, 1, 30));
            _turboTickCounters[binding.Key] = ticks + 1;
        }

        if (!hasActiveTurbo)
        {
            if (_turboPulseActive)
            {
                _turboPulseActive = false;
                changed = true;
            }

            if (changed)
                RefreshLocalInputState();
            return;
        }

        var newPulseActive = _extraInputBindings
            .Where(b => b.Kind == ExtraInputBindingKind.Turbo && _pressedKeys.Contains(b.Key))
            .Any(b =>
            {
                var periodTicks = Math.Max(1, 60 / Math.Clamp(b.TurboHz, 1, 30));
                var ticks = _turboTickCounters.TryGetValue(b.Key, out var t) ? t : 0;
                return (ticks % (periodTicks * 2)) < periodTicks;
            });

        if (newPulseActive != _turboPulseActive)
        {
            _turboPulseActive = newPulseActive;
            changed = true;
        }

        if (changed)
            RefreshLocalInputState();
    }

    private void ApplyRemoteControlWorkflowDecision(
        int player,
        GameWindowRemoteControlRuntimeViewState viewState,
        GameWindowRemoteControlWorkflowDecision decision,
        string? actionId = null,
        bool pressed = false)
    {
        if (decision.ShouldClearRemoteButtons)
            ClearRemoteButtons(player);

        if (decision.ShouldApplyViewState)
            ApplyRemoteControlViewState(viewState);

        if (decision.ShouldApplyRequestedRemoteButtonState && !string.IsNullOrWhiteSpace(actionId))
            SetActionState(player, actionId, pressed);

        if (decision.ShouldRebuildCombinedState)
            ApplyCombinedStateForPlayer(player);

        if (decision.ShouldRefreshLocalInput)
            RefreshLocalInputState();

        foreach (var toastMessage in decision.ToastMessages)
            ShowToast(toastMessage);
    }

    private void ApplyRemoteControlViewState(GameWindowRemoteControlRuntimeViewState viewState)
    {
        RemoteControlStatusText = viewState.RemoteControlStatusText;
        RemoteControlPortsVersion++;
    }

    internal byte GetCombinedInputMask(int player) => _inputState.GetCombinedMask(player);

    private HashSet<Key> GetHandledKeys() =>
        GameWindowInputBindingResolver.BuildHandledKeys(_keyMap, _extraInputBindings);
}
