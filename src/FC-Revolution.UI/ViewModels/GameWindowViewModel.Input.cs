using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class GameWindowViewModel
{
    private static readonly GameWindowRemoteControlStateController RemoteControlStateController = new();
    private static readonly IReadOnlyDictionary<string, NesButton> NesRemoteInputActionToButtonMap = new Dictionary<string, NesButton>(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = NesButton.A,
        ["x"] = NesButton.A,
        ["turboa"] = NesButton.A,
        ["b"] = NesButton.B,
        ["y"] = NesButton.B,
        ["turbob"] = NesButton.B,
        ["select"] = NesButton.Select,
        ["start"] = NesButton.Start,
        ["up"] = NesButton.Up,
        ["down"] = NesButton.Down,
        ["left"] = NesButton.Left,
        ["right"] = NesButton.Right
    };
    private static readonly HashSet<string> NesReservedRemoteInputActionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "l1",
        "r1",
        "l2",
        "r2",
        "l3",
        "r3"
    };

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

    public bool AcquireRemoteControl(int player, string clientIp, string? clientName = null)
    {
        if (!_remoteControlRuntime.TryAcquire(player, clientIp, clientName, DateTime.UtcNow, out var viewState))
            return false;

        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildAcquireDecision(
                acquired: true,
                player,
                clientIp));
        return true;
    }

    public void ReleaseRemoteControl(int player, string? reason = null)
    {
        if (!_remoteControlRuntime.TryRelease(player, out var hadRemoteControl, out var viewState))
            return;

        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildReleaseDecision(player, hadRemoteControl, reason));
    }

    public void RefreshRemoteHeartbeat(int player)
    {
        var refreshed = _remoteControlRuntime.TryRefreshHeartbeat(player, DateTime.UtcNow, out var viewState);
        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildHeartbeatDecision(refreshed));
    }

    public bool SetRemoteButtonState(int player, NesButton button, bool pressed, string? clientIp = null, string? clientName = null)
    {
        if (!RemoteControlPorts.TryGetPortId(player, out var portId))
            return false;

        return SetRemoteInputState(portId, GetInputActionId(button), pressed ? 1f : 0f, clientIp, clientName);
    }

    public bool SetRemoteInputState(string portId, string actionId, float value, string? clientIp = null, string? clientName = null)
    {
        var normalizedPortId = RemoteControlPorts.NormalizePortId(portId);
        if (normalizedPortId == null ||
            string.IsNullOrWhiteSpace(actionId) ||
            !RemoteControlPorts.TryGetPlayer(normalizedPortId, out var player))
        {
            return false;
        }

        var normalizedActionId = actionId.Trim();
        var authorized = _remoteControlRuntime.TryAuthorizeRemoteButtonState(
            player,
            clientIp,
            clientName,
            DateTime.UtcNow,
            out var viewState);

        if (UsesNesRemoteInputBridge() &&
            TryMapNesRemoteInputAction(normalizedActionId, out var mappedButton, out var isReserved))
        {
            ApplyRemoteControlWorkflowDecision(
                player,
                viewState,
                _remoteControlWorkflow.BuildButtonStateDecision(authorized),
                isReserved ? null : mappedButton,
                value >= 0.5f);
            return authorized;
        }

        if (!IsSupportedInputAction(normalizedPortId, normalizedActionId))
        {
            ApplyRemoteControlWorkflowDecision(
                player,
                viewState,
                _remoteControlWorkflow.BuildButtonStateDecision(authorized));
            return false;
        }

        ApplyRemoteControlWorkflowDecision(
            player,
            viewState,
            _remoteControlWorkflow.BuildButtonStateDecision(authorized));
        if (!authorized)
            return false;

        _sessionRuntime.SetInputState(normalizedPortId, normalizedActionId, value);
        return true;
    }

    public bool IsRemoteOwner(int player, string clientIp, string? clientName = null) =>
        _remoteControlRuntime.IsRemoteOwner(player, clientIp, clientName);

    public void ClearRemoteButtons(int player)
    {
        if (player is not 0 and not 1)
            return;

        ApplyInputStateChanges(_inputState.ClearRemoteButtons(player, CanAcceptLocalInput(player)));
    }

    private bool CanAcceptLocalInput(int player) =>
        _remoteControlRuntime.GetPlayerControlSource(player) == GamePlayerControlSource.Local;

    private void SetButtonState(int player, NesButton button, bool pressed)
    {
        ApplyInputStateChanges(_inputState.SetRemoteButtonState(
            player,
            button,
            pressed,
            CanAcceptLocalInput(player)));
    }

    private void RefreshLocalInputState()
    {
        var desiredMasks = BuildDesiredLocalInputMasks();
        ApplyInputStateChanges(_inputState.ApplyDesiredLocalInputMaskForPlayer(
            0,
            desiredMasks.Player1Mask,
            CanAcceptLocalInput(0)));
        ApplyInputStateChanges(_inputState.ApplyDesiredLocalInputMaskForPlayer(
            1,
            desiredMasks.Player2Mask,
            CanAcceptLocalInput(1)));
    }

    private (byte Player1Mask, byte Player2Mask) BuildDesiredLocalInputMasks()
    {
        var desiredMasks = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            _pressedKeys,
            _keyMap,
            _extraInputBindings,
            _turboTickCounters);
        return (desiredMasks.Player1Mask, desiredMasks.Player2Mask);
    }

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
                GetInputPortId(change.Player),
                GetInputActionId(change.Button),
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
        NesButton? button = null,
        bool pressed = false)
    {
        if (decision.ShouldClearRemoteButtons)
            ClearRemoteButtons(player);

        if (decision.ShouldApplyViewState)
            ApplyRemoteControlViewState(viewState);

        if (decision.ShouldApplyRequestedRemoteButtonState && button.HasValue)
            SetButtonState(player, button.Value, pressed);

        if (decision.ShouldRebuildCombinedState)
            ApplyCombinedStateForPlayer(player);

        if (decision.ShouldRefreshLocalInput)
            RefreshLocalInputState();

        foreach (var toastMessage in decision.ToastMessages)
            ShowToast(toastMessage);
    }

    private void ApplyRemoteControlViewState(GameWindowRemoteControlRuntimeViewState viewState)
    {
        Player1ControlSource = viewState.Player1ControlSource;
        Player2ControlSource = viewState.Player2ControlSource;
        RemoteControlStatusText = viewState.RemoteControlStatusText;
    }

    internal byte GetCombinedInputMask(int player) => _inputState.GetCombinedMask(player);

    private HashSet<Key> GetHandledKeys() =>
        GameWindowInputBindingResolver.BuildHandledKeys(_keyMap, _extraInputBindings);

    private static string GetInputPortId(int player) => player == 0 ? "p1" : "p2";

    private bool UsesNesRemoteInputBridge() =>
        string.Equals(_coreSession.RuntimeInfo.SystemId, "nes", StringComparison.OrdinalIgnoreCase);

    private bool IsSupportedInputAction(string portId, string actionId) =>
        _coreSession.InputSchema.Actions.Any(action =>
            string.Equals(action.PortId, portId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action.ActionId, actionId, StringComparison.OrdinalIgnoreCase));

    private static bool TryMapNesRemoteInputAction(string actionId, out NesButton button, out bool isReserved)
    {
        isReserved = false;
        if (NesRemoteInputActionToButtonMap.TryGetValue(actionId, out button))
            return true;

        if (NesReservedRemoteInputActionIds.Contains(actionId))
        {
            button = default;
            isReserved = true;
            return true;
        }

        button = default;
        return false;
    }

    private static string GetInputActionId(NesButton button) => button switch
    {
        NesButton.A => "a",
        NesButton.B => "b",
        NesButton.Select => "select",
        NesButton.Start => "start",
        NesButton.Up => "up",
        NesButton.Down => "down",
        NesButton.Left => "left",
        NesButton.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported NES button mapping.")
    };

}
