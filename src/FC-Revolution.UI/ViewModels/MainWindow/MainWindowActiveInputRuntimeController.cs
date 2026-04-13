using System;
using System.Collections.Generic;
using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowActiveInputWriteRequest(
    int Player,
    bool DesiredPressed,
    string PortId,
    string ActionId,
    float Value);

internal sealed record MainWindowActiveInputPlan(
    byte DesiredPlayer1Mask,
    byte DesiredPlayer2Mask,
    IReadOnlyList<MainWindowActiveInputWriteRequest> WriteRequests);

internal sealed class MainWindowActiveInputRuntimeController
{
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly HashSet<string> _player1ActiveActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _player2ActiveActions = new(StringComparer.OrdinalIgnoreCase);
    private bool _turboPulseActive;
    private string? _activeRomPath;

    public IReadOnlySet<Key> PressedKeys => _pressedKeys;

    public bool TurboPulseActive => _turboPulseActive;

    public void RefreshContext(bool isRomLoaded, string? activeRomPath)
    {
        if (!isRomLoaded)
        {
            Reset();
            return;
        }

        if (string.Equals(_activeRomPath, activeRomPath, StringComparison.OrdinalIgnoreCase))
            return;

        _activeRomPath = activeRomPath;
        ResetInputState();
    }

    public bool PressKey(Key key) => _pressedKeys.Add(key);

    public bool ReleaseKey(Key key) => _pressedKeys.Remove(key);

    public bool UpdateTurboPulse(
        MainWindowInputStateController inputStateController,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings)
    {
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(extraBindings);

        var decision = inputStateController.BuildTurboPulseDecision(
            _turboPulseActive,
            _pressedKeys,
            extraBindings);
        _turboPulseActive = decision.NextTurboPulseActive;
        return decision.ShouldRefreshActiveInputState;
    }

    public MainWindowActiveInputPlan BuildApplyPlan(
        MainWindowInputStateController inputStateController,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(effectiveKeyMap);
        ArgumentNullException.ThrowIfNull(extraBindings);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var desiredActions = inputStateController.BuildDesiredActions(
            _pressedKeys,
            effectiveKeyMap,
            extraBindings,
            _turboPulseActive);
        List<MainWindowActiveInputWriteRequest> writeRequests = [];

        BuildPlayerWriteRequests(
            player: 0,
            desiredActions.Player1Actions,
            _player1ActiveActions,
            inputBindingSchema,
            inputStateController,
            writeRequests);
        BuildPlayerWriteRequests(
            player: 1,
            desiredActions.Player2Actions,
            _player2ActiveActions,
            inputBindingSchema,
            inputStateController,
            writeRequests);

        return new MainWindowActiveInputPlan(
            BuildLegacyMask(0, desiredActions.Player1Actions, inputBindingSchema),
            BuildLegacyMask(1, desiredActions.Player2Actions, inputBindingSchema),
            writeRequests);
    }

    public MainWindowActiveInputPlan BuildApplyPlan(
        MainWindowInputStateController inputStateController,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        byte player1CurrentMask,
        byte player2CurrentMask,
        IReadOnlyList<string> controllerActionIds,
        Func<int, string> getInputPortId)
    {
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        var desiredActions = inputStateController.BuildDesiredActions(
            _pressedKeys,
            effectiveKeyMap,
            extraBindings,
            _turboPulseActive);
        var player1CurrentActions = BuildActionsFromMask(player1CurrentMask, controllerActionIds, inputBindingSchema, player: 0);
        var player2CurrentActions = BuildActionsFromMask(player2CurrentMask, controllerActionIds, inputBindingSchema, player: 1);
        List<MainWindowActiveInputWriteRequest> writeRequests = [];
        BuildCompatibilityWriteRequests(
            player: 0,
            desiredActions.Player1Actions,
            player1CurrentActions,
            controllerActionIds,
            getInputPortId,
            inputStateController,
            writeRequests);
        BuildCompatibilityWriteRequests(
            player: 1,
            desiredActions.Player2Actions,
            player2CurrentActions,
            controllerActionIds,
            getInputPortId,
            inputStateController,
            writeRequests);
        return new MainWindowActiveInputPlan(
            BuildLegacyMask(0, desiredActions.Player1Actions, inputBindingSchema),
            BuildLegacyMask(1, desiredActions.Player2Actions, inputBindingSchema),
            writeRequests);
    }

    public void ApplyPlan(
        MainWindowActiveInputPlan plan,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<int, string, bool> updateInputMask)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(inputSyncRoot);
        ArgumentNullException.ThrowIfNull(inputStateWriter);
        ArgumentNullException.ThrowIfNull(updateInputMask);

        foreach (var writeRequest in plan.WriteRequests)
        {
            lock (inputSyncRoot)
            {
                inputStateWriter.SetInputState(
                    writeRequest.PortId,
                    writeRequest.ActionId,
                    writeRequest.Value);
            }

            UpdateActiveActionState(writeRequest.Player, writeRequest.ActionId, writeRequest.DesiredPressed);
            updateInputMask(writeRequest.Player, writeRequest.ActionId, writeRequest.DesiredPressed);
        }
    }

    private static void BuildPlayerWriteRequests(
        int player,
        IReadOnlySet<string> desiredActions,
        IReadOnlySet<string> currentActions,
        CoreInputBindingSchema inputBindingSchema,
        MainWindowInputStateController inputStateController,
        ICollection<MainWindowActiveInputWriteRequest> writeRequests)
    {
        var transitions = inputStateController.BuildActionTransitions(
            desiredActions,
            currentActions,
            inputBindingSchema.GetBindableActionIds(player));
        var portId = inputBindingSchema.GetPortId(player);
        foreach (var transition in transitions)
        {
            writeRequests.Add(new MainWindowActiveInputWriteRequest(
                player,
                transition.DesiredPressed,
                portId,
                transition.ActionId,
                transition.DesiredPressed ? 1f : 0f));
        }
    }

    private void Reset()
    {
        _activeRomPath = null;
        ResetInputState();
    }

    private void ResetInputState()
    {
        _pressedKeys.Clear();
        _player1ActiveActions.Clear();
        _player2ActiveActions.Clear();
        _turboPulseActive = false;
    }

    private void UpdateActiveActionState(int player, string actionId, bool pressed)
    {
        var target = player == 0 ? _player1ActiveActions : _player2ActiveActions;
        if (pressed)
            target.Add(actionId);
        else
            target.Remove(actionId);
    }

    private static void BuildCompatibilityWriteRequests(
        int player,
        IReadOnlySet<string> desiredActions,
        IReadOnlySet<string> currentActions,
        IReadOnlyList<string> controllerActionIds,
        Func<int, string> getInputPortId,
        MainWindowInputStateController inputStateController,
        ICollection<MainWindowActiveInputWriteRequest> writeRequests)
    {
        var transitions = inputStateController.BuildActionTransitions(desiredActions, currentActions, controllerActionIds);
        foreach (var transition in transitions)
        {
            writeRequests.Add(new MainWindowActiveInputWriteRequest(
                player,
                transition.DesiredPressed,
                getInputPortId(player),
                transition.ActionId,
                transition.DesiredPressed ? 1f : 0f));
        }
    }

    private static IReadOnlySet<string> BuildActionsFromMask(
        byte mask,
        IEnumerable<string> controllerActionIds,
        CoreInputBindingSchema inputBindingSchema,
        int player)
    {
        HashSet<string> actions = new(StringComparer.OrdinalIgnoreCase);
        foreach (var actionId in controllerActionIds)
        {
            if (inputBindingSchema.TryGetLegacyBitMask(player, actionId, out var bit) &&
                (mask & bit) != 0)
            {
                actions.Add(actionId);
            }
        }

        return actions;
    }

    private static byte BuildLegacyMask(int player, IEnumerable<string> actionIds, CoreInputBindingSchema inputBindingSchema)
    {
        byte mask = 0;
        foreach (var actionId in actionIds)
        {
            if (inputBindingSchema.TryGetLegacyBitMask(player, actionId, out var bit))
                mask |= bit;
        }

        return mask;
    }
}
