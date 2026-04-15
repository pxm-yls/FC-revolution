using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record GameWindowDesiredLocalInputActions(
    IReadOnlyDictionary<string, IReadOnlySet<string>> ActionsByPort)
{
    public IReadOnlySet<string> GetActions(string portId) =>
        ActionsByPort.TryGetValue(portId, out var actions)
            ? actions
            : EmptyActions;

    private static IReadOnlySet<string> EmptyActions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

internal static class GameWindowLocalInputProjectionController
{
    public static GameWindowDesiredLocalInputActions BuildDesiredLocalInputActions(
        IReadOnlyCollection<Key> pressedKeys,
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings,
        IReadOnlyDictionary<Key, int> turboTickCounters)
        => BuildDesiredLocalInputActions(
            pressedKeys,
            keyMap,
            extraInputBindings,
            turboTickCounters,
            CoreInputBindingSchema.CreateFallback());

    public static GameWindowDesiredLocalInputActions BuildDesiredLocalInputActions(
        IReadOnlyCollection<Key> pressedKeys,
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings,
        IReadOnlyDictionary<Key, int> turboTickCounters,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var actionsByPort = inputBindingSchema.GetSupportedPorts()
            .ToDictionary(
                port => port.PortId,
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        foreach (var key in pressedKeys)
        {
            if (!keyMap.TryGetValue(key, out var binding))
                continue;

            GetTargetActions(binding.PortId, actionsByPort).Add(binding.ActionId);
        }

        foreach (var binding in extraInputBindings)
        {
            if (!pressedKeys.Contains(binding.Key))
                continue;

            if (binding.Kind == ExtraInputBindingKind.Turbo)
            {
                var periodTicks = Math.Max(1, 60 / Math.Clamp(binding.TurboHz, 1, 30));
                var ticks = turboTickCounters.TryGetValue(binding.Key, out var t) ? t : 0;
                if ((ticks % (periodTicks * 2)) >= periodTicks)
                    continue;
            }

            var targetActions = GetTargetActions(binding.PortId, actionsByPort);
            foreach (var actionId in binding.ActionIds)
                targetActions.Add(actionId);
        }

        return new GameWindowDesiredLocalInputActions(actionsByPort.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlySet<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase));
    }

    private static HashSet<string> GetTargetActions(
        string portId,
        IDictionary<string, HashSet<string>> actionsByPort)
    {
        if (!actionsByPort.TryGetValue(portId, out var targetActions))
        {
            targetActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            actionsByPort[portId] = targetActions;
        }

        return targetActions;
    }
}
