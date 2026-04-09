using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Contracts.RemoteControl;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

internal static class InputBindingContractAdapter
{
    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromEntries(IEnumerable<InputBindingEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var bindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (!RemoteControlPorts.TryGetPortId(entry.Player, out var portId) ||
                string.IsNullOrWhiteSpace(entry.ActionId))
            {
                continue;
            }

            if (!bindingsByPort.TryGetValue(portId, out var actionBindings))
            {
                actionBindings = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);
                bindingsByPort[portId] = actionBindings;
            }

            actionBindings[entry.ActionId.Trim()] = entry.SelectedKey;
        }

        return bindingsByPort;
    }

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromPlayerMaps(
        IReadOnlyDictionary<int, Dictionary<string, Key>> playerMaps)
    {
        ArgumentNullException.ThrowIfNull(playerMaps);

        var bindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var playerMap in playerMaps)
        {
            if (!RemoteControlPorts.TryGetPortId(playerMap.Key, out var portId))
                continue;

            bindingsByPort[portId] = playerMap.Value.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return bindingsByPort;
    }

    public static IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> BuildActionBindingsByRomPath(
        IReadOnlyDictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides)
    {
        ArgumentNullException.ThrowIfNull(romInputOverrides);

        return romInputOverrides.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<string, Dictionary<string, Key>>(
                BuildActionBindingsFromPlayerMaps(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<int, Dictionary<string, Key>> BuildPlayerMapsFromActionBindings(
        IReadOnlyDictionary<string, Dictionary<string, Key>> bindingsByPort)
    {
        ArgumentNullException.ThrowIfNull(bindingsByPort);

        var playerMaps = new Dictionary<int, Dictionary<string, Key>>();
        foreach (var portBindings in bindingsByPort)
        {
            var normalizedPortId = RemoteControlPorts.NormalizePortId(portBindings.Key);
            if (normalizedPortId == null || !RemoteControlPorts.TryGetPlayer(normalizedPortId, out var player))
                continue;

            playerMaps[player] = portBindings.Value.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return playerMaps;
    }
}
