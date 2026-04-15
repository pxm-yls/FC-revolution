using System;
using System.Collections.Generic;
using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowActiveInputWriteRequest(
    bool DesiredPressed,
    string PortId,
    string ActionId,
    float Value);

internal sealed record MainWindowActiveInputPlan(
    IReadOnlyList<MainWindowActiveInputWriteRequest> WriteRequests);

internal sealed class MainWindowActiveInputRuntimeController
{
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly Dictionary<string, HashSet<string>> _activeActionsByPort = new(StringComparer.OrdinalIgnoreCase);
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
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> effectiveKeyMap,
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

        foreach (var port in inputBindingSchema.GetSupportedPorts())
        {
            BuildPortWriteRequests(
                port.PortId,
                desiredActions.GetActions(port.PortId),
                GetActiveActions(port.PortId),
                inputBindingSchema,
                inputStateController,
                writeRequests);
        }

        return new MainWindowActiveInputPlan(writeRequests);
    }

    public void ApplyPlan(
        MainWindowActiveInputPlan plan,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<string, string, bool> updateInputMask)
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

            UpdateActiveActionState(writeRequest.PortId, writeRequest.ActionId, writeRequest.DesiredPressed);
            updateInputMask(writeRequest.PortId, writeRequest.ActionId, writeRequest.DesiredPressed);
        }
    }

    private static void BuildPortWriteRequests(
        string portId,
        IReadOnlySet<string> desiredActions,
        IReadOnlySet<string> currentActions,
        CoreInputBindingSchema inputBindingSchema,
        MainWindowInputStateController inputStateController,
        ICollection<MainWindowActiveInputWriteRequest> writeRequests)
    {
        var transitions = inputStateController.BuildActionTransitions(
            desiredActions,
            currentActions,
            inputBindingSchema.GetBindableActionIds(portId));
        foreach (var transition in transitions)
        {
            writeRequests.Add(new MainWindowActiveInputWriteRequest(
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
        _activeActionsByPort.Clear();
        _turboPulseActive = false;
    }

    private IReadOnlySet<string> GetActiveActions(string portId)
    {
        if (_activeActionsByPort.TryGetValue(portId, out var actions))
            return actions;

        return EmptyActions;
    }

    private void UpdateActiveActionState(string portId, string actionId, bool pressed)
    {
        if (!_activeActionsByPort.TryGetValue(portId, out var target))
        {
            target = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _activeActionsByPort[portId] = target;
        }

        if (pressed)
            target.Add(actionId);
        else
            target.Remove(actionId);
    }

    private static IReadOnlySet<string> EmptyActions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
