using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

internal static class InputBindingContractAdapter
{
    private static readonly CoreInputBindingSchema FallbackSchema = CoreInputBindingSchema.CreateFallback();

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromEntries(IEnumerable<InputBindingEntry> entries)
        => BuildActionBindingsFromEntries(entries, FallbackSchema);

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromEntries(
        IEnumerable<InputBindingEntry> entries,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var bindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ActionId))
            {
                continue;
            }

            var portId = inputBindingSchema.GetPortId(entry.Player);
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
        => BuildActionBindingsFromPlayerMaps(playerMaps, FallbackSchema);

    public static IReadOnlyDictionary<string, Dictionary<string, Key>> BuildActionBindingsFromPlayerMaps(
        IReadOnlyDictionary<int, Dictionary<string, Key>> playerMaps,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(playerMaps);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var bindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var playerMap in playerMaps)
        {
            var portId = inputBindingSchema.GetPortId(playerMap.Key);
            bindingsByPort[portId] = playerMap.Value.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return bindingsByPort;
    }

    public static IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> BuildActionBindingsByRomPath(
        IReadOnlyDictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides)
        => BuildActionBindingsByRomPath(romInputOverrides, FallbackSchema);

    public static IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> BuildActionBindingsByRomPath(
        IReadOnlyDictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(romInputOverrides);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        return romInputOverrides.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<string, Dictionary<string, Key>>(
                BuildActionBindingsFromPlayerMaps(pair.Value, inputBindingSchema),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<int, Dictionary<string, Key>> BuildPlayerMapsFromActionBindings(
        IReadOnlyDictionary<string, Dictionary<string, Key>> bindingsByPort)
        => BuildPlayerMapsFromActionBindings(bindingsByPort, FallbackSchema);

    public static IReadOnlyDictionary<int, Dictionary<string, Key>> BuildPlayerMapsFromActionBindings(
        IReadOnlyDictionary<string, Dictionary<string, Key>> bindingsByPort,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(bindingsByPort);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var playerMaps = new Dictionary<int, Dictionary<string, Key>>();
        foreach (var portBindings in bindingsByPort)
        {
            if (!inputBindingSchema.TryResolvePort(portBindings.Key, out var player, out _))
                continue;

            playerMaps[player] = portBindings.Value.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return playerMaps;
    }
}
