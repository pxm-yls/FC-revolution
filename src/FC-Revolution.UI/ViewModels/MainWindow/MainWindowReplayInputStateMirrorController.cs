using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowReplayInputStateMirrorController
{
    private readonly Dictionary<string, HashSet<string>> _actionsByPort = new(StringComparer.OrdinalIgnoreCase);

    public void Reset() => _actionsByPort.Clear();

    public void UpdateActionState(
        string portId,
        string actionId,
        bool pressed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        if (!_actionsByPort.TryGetValue(portId, out var actions))
        {
            actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _actionsByPort[portId] = actions;
        }

        if (pressed)
        {
            actions.Add(actionId);
            return;
        }

        actions.Remove(actionId);
        if (actions.Count == 0)
            _actionsByPort.Remove(portId);
    }

    public IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSnapshot()
    {
        var snapshot = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _actionsByPort)
        {
            snapshot[pair.Key] = new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        return new ReadOnlyDictionary<string, IReadOnlySet<string>>(snapshot);
    }
}
