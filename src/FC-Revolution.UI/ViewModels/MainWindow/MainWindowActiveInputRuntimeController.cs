using System;
using System.Collections.Generic;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowActiveInputWriteRequest(
    int Player,
    NesButton Button,
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
        IReadOnlyDictionary<Key, (int Player, NesButton Button)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        byte player1CurrentMask,
        byte player2CurrentMask,
        IReadOnlyList<NesButton> controllerButtons,
        Func<int, string> getInputPortId,
        Func<NesButton, string> getInputActionId)
    {
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(effectiveKeyMap);
        ArgumentNullException.ThrowIfNull(extraBindings);
        ArgumentNullException.ThrowIfNull(controllerButtons);
        ArgumentNullException.ThrowIfNull(getInputPortId);
        ArgumentNullException.ThrowIfNull(getInputActionId);

        var desiredMasks = inputStateController.BuildDesiredMasks(
            _pressedKeys,
            effectiveKeyMap,
            extraBindings,
            _turboPulseActive);
        var writeRequests = new List<MainWindowActiveInputWriteRequest>();

        BuildPlayerWriteRequests(
            player: 0,
            desiredMasks.Player1Mask,
            player1CurrentMask,
            controllerButtons,
            getInputPortId,
            getInputActionId,
            inputStateController,
            writeRequests);
        BuildPlayerWriteRequests(
            player: 1,
            desiredMasks.Player2Mask,
            player2CurrentMask,
            controllerButtons,
            getInputPortId,
            getInputActionId,
            inputStateController,
            writeRequests);

        return new MainWindowActiveInputPlan(
            desiredMasks.Player1Mask,
            desiredMasks.Player2Mask,
            writeRequests);
    }

    public void ApplyPlan(
        MainWindowActiveInputPlan plan,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<int, NesButton, bool> updateInputMask)
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

            updateInputMask(writeRequest.Player, writeRequest.Button, writeRequest.DesiredPressed);
        }
    }

    private static void BuildPlayerWriteRequests(
        int player,
        byte desiredMask,
        byte currentMask,
        IReadOnlyList<NesButton> controllerButtons,
        Func<int, string> getInputPortId,
        Func<NesButton, string> getInputActionId,
        MainWindowInputStateController inputStateController,
        ICollection<MainWindowActiveInputWriteRequest> writeRequests)
    {
        var transitions = inputStateController.BuildMaskTransitions(
            desiredMask,
            currentMask,
            controllerButtons);
        foreach (var transition in transitions)
        {
            writeRequests.Add(new MainWindowActiveInputWriteRequest(
                player,
                transition.Button,
                transition.DesiredPressed,
                getInputPortId(player),
                getInputActionId(transition.Button),
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
        _turboPulseActive = false;
    }
}
