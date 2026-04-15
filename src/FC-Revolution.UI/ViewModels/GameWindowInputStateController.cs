using System;
using System.Collections.Generic;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowInputStateChange(
    string PortId,
    string ActionId,
    bool Pressed);

internal sealed class GameWindowInputStateController
{
    private readonly CoreInputBindingSchema _inputBindingSchema;
    private readonly Dictionary<string, HashSet<string>> _combinedActionsByPort = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _localActionsByPort = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _remoteActionsByPort = new(StringComparer.OrdinalIgnoreCase);

    public GameWindowInputStateController(CoreInputBindingSchema inputBindingSchema)
    {
        _inputBindingSchema = inputBindingSchema;
    }

    public GameWindowInputStateController()
        : this(CoreInputBindingSchema.CreateFallback())
    {
    }

    public IReadOnlyList<GameWindowInputStateChange> ApplyDesiredLocalInputActions(
        string portId,
        IReadOnlySet<string> desiredActions,
        bool allowLocalInput)
    {
        if (!TryNormalizePortId(portId, out var normalizedPortId))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        var localActions = GetLocalActions(normalizedPortId);
        foreach (var actionId in _inputBindingSchema.GetBindableActionIds(normalizedPortId))
        {
            var desired = allowLocalInput && desiredActions.Contains(actionId);
            if (desired)
                localActions.Add(actionId);
            else
                localActions.Remove(actionId);

            ApplyCombinedActionState(normalizedPortId, actionId, allowLocalInput, changes);
        }

        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> SetRemoteActionState(
        string portId,
        string actionId,
        bool pressed,
        bool allowLocalInput)
    {
        if (!TryNormalizeAction(portId, actionId, out var normalizedPortId, out var normalizedActionId))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        var remoteActions = GetRemoteActions(normalizedPortId);
        if (pressed)
            remoteActions.Add(normalizedActionId);
        else
            remoteActions.Remove(normalizedActionId);

        ApplyCombinedActionState(normalizedPortId, normalizedActionId, allowLocalInput, changes);
        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> ClearRemoteButtons(string portId, bool allowLocalInput)
    {
        if (!TryNormalizePortId(portId, out var normalizedPortId))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        var remoteActions = GetRemoteActions(normalizedPortId);
        remoteActions.Clear();
        foreach (var actionId in _inputBindingSchema.GetBindableActionIds(normalizedPortId))
            ApplyCombinedActionState(normalizedPortId, actionId, allowLocalInput, changes);

        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> RebuildCombinedState(string portId, bool allowLocalInput)
    {
        if (!TryNormalizePortId(portId, out var normalizedPortId))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        foreach (var actionId in _inputBindingSchema.GetBindableActionIds(normalizedPortId))
            ApplyCombinedActionState(normalizedPortId, actionId, allowLocalInput, changes);
        return changes;
    }

    private void ApplyCombinedActionState(
        string portId,
        string actionId,
        bool allowLocalInput,
        List<GameWindowInputStateChange> changes)
    {
        var localActions = GetLocalActions(portId);
        var remoteActions = GetRemoteActions(portId);
        var combinedActions = GetCombinedActions(portId);
        var desired = (allowLocalInput && localActions.Contains(actionId)) || remoteActions.Contains(actionId);
        var current = combinedActions.Contains(actionId);
        if (desired == current)
            return;

        if (desired)
            combinedActions.Add(actionId);
        else
            combinedActions.Remove(actionId);

        changes.Add(new GameWindowInputStateChange(portId, actionId, desired));
    }

    private HashSet<string> GetCombinedActions(string portId) =>
        GetOrCreateActions(_combinedActionsByPort, portId);

    private HashSet<string> GetLocalActions(string portId) =>
        GetOrCreateActions(_localActionsByPort, portId);

    private HashSet<string> GetRemoteActions(string portId) =>
        GetOrCreateActions(_remoteActionsByPort, portId);

    private static HashSet<string> GetOrCreateActions(
        IDictionary<string, HashSet<string>> actionsByPort,
        string portId)
    {
        if (!actionsByPort.TryGetValue(portId, out var actions))
        {
            actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            actionsByPort[portId] = actions;
        }

        return actions;
    }

    private bool TryNormalizePortId(string? portId, out string normalizedPortId) =>
        _inputBindingSchema.TryNormalizePortId(portId, out normalizedPortId);

    private bool TryNormalizeAction(
        string? portId,
        string? actionId,
        out string normalizedPortId,
        out string normalizedActionId)
    {
        if (_inputBindingSchema.TryNormalizePortId(portId, out normalizedPortId) &&
            _inputBindingSchema.TryNormalizeActionId(normalizedPortId, actionId, out normalizedActionId))
        {
            return true;
        }

        normalizedPortId = string.Empty;
        normalizedActionId = string.Empty;
        return false;
    }
}
